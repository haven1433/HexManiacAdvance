using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class ReadonlyPaletteCollection : ViewModelCore {
      public int ColorWidth => (int)Math.Ceiling(Math.Sqrt(Colors.Count));
      public int ColorHeight => (int)Math.Sqrt(Colors.Count);
      public ObservableCollection<short> Colors { get; } = new ObservableCollection<short>();
      public ReadonlyPaletteCollection(IEnumerable<short> colors) {
         foreach (var color in colors) Colors.Add(color);
      }
   }

   public class PaletteCollection : ViewModelCore {
      private readonly IRaiseMessageTab tab;
      private readonly IDataModel model;
      private readonly ChangeHistory<ModelDelta> history;

      private int sourcePalettePointer;
      public int SourcePalettePointer { get => sourcePalettePointer; set => Set(ref sourcePalettePointer, value); }
      public ObservableCollection<SelectableColor> Elements { get; } = new ObservableCollection<SelectableColor>();

      public int ColorWidth => Elements.Count / ColorHeight;
      public int ColorHeight => (int)Math.Ceiling(Math.Sqrt(Elements.Count));
      public bool CanEditColors => SourcePalettePointer >= 0 && (model == null || SourcePalettePointer <= model.Count - 4);

      public int SpriteBitsPerPixel { get; set; }

      public event EventHandler SelectionSet;

      private int selectionStart;
      public int SelectionStart {
         get => selectionStart;
         set {
            if (Elements[value].Selected || !TryUpdate(ref selectionStart, value)) {
               SelectionSet?.Invoke(this, EventArgs.Empty);
               return;
            } 

            SelectionEnd = selectionStart;
            history?.ChangeCompleted();
         }
      }

      private int selectionEnd;
      public int SelectionEnd {
         get => selectionEnd;
         set {
            TryUpdate(ref selectionEnd, value);
            var first = Math.Min(selectionStart, selectionEnd);
            var last = Math.Max(selectionStart, selectionEnd);
            for (int i = 0; i < Elements.Count; i++) Elements[i].Selected = first <= i && i <= last;
            createGradient.RaiseCanExecuteChanged();
            singleReduce.RaiseCanExecuteChanged();
            SelectionSet?.Invoke(this, EventArgs.Empty);
         }
      }

      private int page;
      public int Page { get => page; set => Set(ref page, value); }

      private int hoverIndex;
      public int HoverIndex { get => hoverIndex; set => Set(ref hoverIndex, value); }

      private bool hasMultiplePages;
      public bool HasMultiplePages { get => hasMultiplePages; set => Set(ref hasMultiplePages, value); }

      private StubCommand copy;
      public ICommand Copy => StubCommand<IFileSystem>(ref copy, ExecuteCopy, CanExecuteCopy);

      private StubCommand paste;
      public ICommand Paste => StubCommand<IFileSystem>(ref paste, ExecutePaste, CanExecutePaste);

      private StubCommand createGradient;
      public ICommand CreateGradient => StubCommand(ref createGradient, ExecuteCreateGradient, CanExecuteCreateGradient);

      private StubCommand singleReduce;
      public ICommand SingleReduce => StubCommand(ref singleReduce, ExecuteSingleReduce, CanExecuteSingleReduce);

      private StubCommand deleteColor;
      public ICommand DeleteColor => StubCommand(ref deleteColor, ExecuteDelete);

      public event EventHandler<int> RequestPageSet;
      public event EventHandler<int> PaletteRepointed;
      public event EventHandler ColorsChanged;

      /// <summary>
      /// Create a palette collection that's tied to data in a model.
      /// This collection can pull/push data from the model, raise notifications, and supports undo/redo.
      /// </summary>
      /// <param name="tab"></param>
      /// <param name="model"></param>
      /// <param name="history"></param>
      public PaletteCollection(IRaiseMessageTab tab, IDataModel model, ChangeHistory<ModelDelta> history) {
         this.tab = tab;
         this.model = model;
         this.history = history;
      }

      /// <summary>
      /// Create a palette collection that holds spare colors.
      /// This collection is not tied to the model.
      /// </summary>
      public PaletteCollection() { }

      public IList<(int index, int direction)> HandleMove(int originalIndex, int newIndex) {
         var otherMovedElements = new List<(int, int)>();
         if (originalIndex == newIndex) return otherMovedElements;

         var first = Math.Min(selectionStart, selectionEnd);
         var last = Math.Max(selectionStart, selectionEnd);
         newIndex = Math.Max(newIndex, originalIndex - first);
         newIndex = Math.Min(newIndex, Elements.Count - 1 - last + originalIndex);
         var targetElements = Enumerable.Range(first, last - first + 1).ToList();

         if (originalIndex < newIndex) targetElements.Reverse();
         foreach (var index in targetElements) {
            var element = Elements[index];
            Elements.RemoveAt(index);
            Elements.Insert(newIndex - originalIndex + index, element);
         }

         for (int i = first; i < newIndex - (originalIndex - first); i++) {
            otherMovedElements.Add((i, last - first + 1));
         }
         for (int i = last; i > newIndex + (last - originalIndex); i--) {
            otherMovedElements.Add((i, first - last - 1));
         }

         selectionStart += newIndex - originalIndex;
         SelectionEnd += newIndex - originalIndex;
         return otherMovedElements;
      }

      public void ToggleSelection(int index) {
         Elements[index].Selected = !Elements[index].Selected;
         createGradient.RaiseCanExecuteChanged();
         singleReduce.RaiseCanExecuteChanged();
         SelectionSet?.Invoke(this, EventArgs.Empty);
      }

      public void CompleteCurrentInteraction() {
         ReorderPalette();
         history?.ChangeCompleted();
      }

      public void SetContents(IReadOnlyList<short> colors) {
         Elements.Clear();
         var (left, right) = (Math.Min(SelectionStart, SelectionEnd), Math.Max(SelectionStart, SelectionEnd));
         foreach (var element in colors.Count.Range()
            .Select(i => new SelectableColor { Color = colors[i], Index = i, Selected = left <= i && i <= right })) {
            Elements.Add(element);
         }
         NotifyPropertyChanged(nameof(ColorWidth));
         NotifyPropertyChanged(nameof(ColorHeight));
      }

      public void PushColorsToModel() {
         if (model == null || !CanEditColors) return;
         int sourcePalette = model.ReadPointer(sourcePalettePointer);
         if (!(model.GetNextRun(sourcePalette) is IPaletteRun source)) return;

         // update model
         var newPalette = source;
         newPalette = newPalette.SetPalette(model, history.CurrentChange, page, Elements.Select(e => e.Color).ToList());
         if (source.Start != newPalette.Start) {
            tab.RaiseMessage($"Palette was moved to {newPalette.Start:X6}. Pointers were updated.");
            PaletteRepointed?.Invoke(this, newPalette.Start);
         }

         // update UI
         var selectionRange = (selectionStart, selectionEnd);
         var selections = Elements.Select(e => e.Selected).ToArray();
         Refresh();
         (SelectionStart, SelectionEnd) = selectionRange;
         for (int i = 0; i < Elements.Count; i++) Elements[i].Selected = selections[i];
      }

      /// <summary>
      /// If multiple colors are selected, reduce to just a single color selected.
      /// </summary>
      public void SingleSelect() {
         if (SelectionEnd != SelectionStart) {
            SelectionEnd = SelectionStart;
            return;
         }

         for (int i = 0; i < Elements.Count; i++) Elements[i].Selected = SelectionStart == i;
         createGradient.RaiseCanExecuteChanged();
         singleReduce.RaiseCanExecuteChanged();
         SelectionSet?.Invoke(this, EventArgs.Empty);
      }

      private void ReorderPalette() {
         if (model == null || !CanEditColors) return;
         if (sourcePalettePointer < 0 || sourcePalettePointer > model.Count - 4) return;
         int sourcePalette = model.ReadPointer(sourcePalettePointer);
         if (!(model.GetNextRun(sourcePalette) is IPaletteRun source)) return;

         var oldToNew = Elements.Count.Range().Select(i => Elements.IndexOf(Elements.Single(element => element.Index == i))).ToArray();
         var newElements = Elements.Count.Range().Select(i => new SelectableColor {
            Index = i,
            Color = Elements[Elements[i].Index].Color,
            Selected = Elements[i].Selected,
         }).ToList();

         // early exit if there's no actual changes
         if (Elements.Count.Range().All(i => Elements[i].Index == newElements[i].Index)) return;

         var palettesToUpdate = new List<IPaletteRun> { source };
         var sprites = source.FindDependentSprites(model).Distinct().ToList();
         bool alreadyDidGoto = false;

         foreach (var sprite in sprites) {
            var newSprite = sprite;

            if (sprite is LzTilesetRun tileset) {
               // find all tilemaps that use this tileset and update them
               var tilemaps = tileset.FindDependentTilemaps(model); // TODO working here
               foreach (var tilemap in tilemaps) {
                  var pixels = tilemap.GetPixels(model, 0);
                  for (int y = 0; y < pixels.GetLength(1); y++) {
                     for (int x = 0; x < pixels.GetLength(0); x++) {
                        int tilesetPalettePage = source.PaletteFormat.InitialBlankPages;
                        if (hasMultiplePages) tilesetPalettePage = pixels[x, y] >> 4;
                        // note that if a tilemap has a tile with a palette of 'zero', this page calculation comes out as negative, and no swapping will be done.
                        // in the game, this only happens for tiles filled with the fully transparent color, so leaving them alone is actually the right thing to do.
                        if (tilesetPalettePage - source.PaletteFormat.InitialBlankPages != this.page) continue;
                        var oldPaletteColorIndex = pixels[x, y] - (tilesetPalettePage << 4);
                        int tilesetPageOffset = 0;
                        while (oldPaletteColorIndex < 0) { oldPaletteColorIndex += 16; tilesetPageOffset += 16; }
                        while (oldPaletteColorIndex >= oldToNew.Length) { oldPaletteColorIndex -= 16; tilesetPageOffset -= 16; }
                        var newPaletteColorIndex = oldToNew[oldPaletteColorIndex];
                        pixels[x, y] = newPaletteColorIndex + (tilesetPalettePage << 4) - tilesetPageOffset;
                     }
                  }
                  var newTileMap = tilemap.SetPixels(model, history.CurrentChange, 0, pixels);
                  if (newTileMap.Start != tilemap.Start) {
                     tab.RaiseMessage($"Tilemap was moved to {newTileMap.Start:X6}. Pointers were updated.");
                     if (!alreadyDidGoto) tab.Goto?.Execute(newTileMap.Start);
                     alreadyDidGoto = true;
                  }
               }
               // find the new tileset sprite, since it could've moved
               newSprite = model.GetNextRun(model.ReadPointer(sprite.PointerSources[0])) as ISpriteRun ?? sprite;
            } else {
               // get/set the sprite data for each relavent page
               for (int page = 0; page < newSprite.Pages; page++) {
                  if (hasMultiplePages) page += this.page;
                  var pixels = newSprite.GetPixels(model, page % newSprite.Pages);
                  for (int y = 0; y < pixels.GetLength(1); y++) {
                     for (int x = 0; x < pixels.GetLength(0); x++) {
                        var pixelPage = pixels[x, y] / oldToNew.Length;
                        pixels[x, y] -= pixelPage * oldToNew.Length;
                        pixels[x, y] = oldToNew[pixels[x, y]] + pixelPage * oldToNew.Length;
                     }
                  }
                  newSprite = newSprite.SetPixels(model, history.CurrentChange, page % newSprite.Pages, pixels);
                  if (hasMultiplePages) break;
               }

               if (newSprite.Start != sprite.Start) {
                  tab.RaiseMessage($"Sprite was moved to {newSprite.Start:X6}. Pointers were updated.");
                  if (!alreadyDidGoto) tab.Goto?.Execute(newSprite.Start);
                  alreadyDidGoto = true;
               }
            }

            palettesToUpdate.AddRange(newSprite.FindRelatedPalettes(model));
         }

         foreach (var palette in palettesToUpdate.Distinct()) {
            var newPalette = palette;
            var colors = newPalette.GetPalette(model, page);
            var newColors = Elements.Count.Range().Select(i => colors[Elements[i].Index]).ToList();
            newPalette = newPalette.SetPalette(model, history.CurrentChange, page, newColors);
            if (palette.Start != newPalette.Start) tab.RaiseMessage($"Palette was moved to {newPalette.Start:X6}. Pointers were updated.");
         }

         for (int i = 0; i < Elements.Count; i++) {
            Elements[i].Selected = newElements[i].Selected;
            Elements[i].Index = newElements[i].Index;
            // the elements order changed, so the element should already have the right color.
         }
         Refresh();
      }

      private void Refresh() {
         if (tab == null) return;
         var currentPage = page;
         tab.Refresh();
         if (hasMultiplePages) RequestPageSet?.Invoke(this, currentPage);
         ColorsChanged?.Invoke(this, EventArgs.Empty);
      }

      #region Commands

      private void ExecuteCopy(IFileSystem fileSystem) {
         var copied = new List<string>();
         foreach (var element in Elements) {
            if (element.Selected) copied.Add(UncompressedPaletteColor.Convert(element.Color));
         }
         fileSystem.CopyText = " ".Join(copied);
      }

      private bool CanExecuteCopy(IFileSystem fileSystem) => 0 <= selectionStart && selectionStart < Elements.Count;

      public static IReadOnlyList<short> ParseColor(string stream) {
         var results = new List<short>();
         var parts = stream.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
         for (int i = 0; i < parts.Length; i++) {
            if (parts[i].Contains(":")) {
               var channels = parts[i].Split(':');
               if (channels.Length != 3) return null;
               if (!int.TryParse(channels[0], out var red) || !int.TryParse(channels[1], out var green) || !int.TryParse(channels[2], out var blue)) return null;
               results.Add(UncompressedPaletteColor.Pack(red, green, blue));
            } else if (parts[i].Length == 4) {
               if (!short.TryParse(parts[i], NumberStyles.HexNumber, CultureInfo.CurrentCulture, out var color)) return null;
               results.Add(color);
            } else if (parts[i].Length == 2 && i + 1 < parts.Length && parts[i + 1].Length == 2) {
               if (!byte.TryParse(parts[i + 0], NumberStyles.HexNumber, CultureInfo.CurrentCulture, out var low)) return null;
               if (!byte.TryParse(parts[i + 1], NumberStyles.HexNumber, CultureInfo.CurrentCulture, out var high)) return null;
               i += 1;
               results.Add((byte)((high << 8) | low));
            } else {
               return null;
            }
         }
         return results;
      }

      private void ExecutePaste(IFileSystem fileSystem) {
         if (model != null) {
            int sourcePalette = model.ReadPointer(sourcePalettePointer);
            if (!(model.GetNextRun(sourcePalette) is IPaletteRun)) return;
         }

         // paste data into elements
         var colors = ParseColor(fileSystem.CopyText);
         if (colors == null) return;
         var start = Math.Min(selectionStart, selectionEnd);
         for (int i = 0; i < colors.Count; i++) {
            Elements[start].Color = colors[i];
            if (start < Elements.Count - 1) start += 1;
         }

         PushColorsToModel();
         SelectionStart = start;
      }

      private bool CanExecutePaste(IFileSystem fileSystem) => CanExecuteCopy(fileSystem) && ParseColor(fileSystem.CopyText) != null;

      private void ExecuteCreateGradient() {
         var left = Elements.Count.Range().First(i => Elements[i].Selected);
         var right = Elements.Count.Range().Last(i => Elements[i].Selected);

         var (r, g, b) = UncompressedPaletteColor.ToRGB(Elements[left].Color);
         var leftHSB = Theme.ToHSB((byte)(r << 3), (byte)(g << 3), (byte)(b << 3));

         var rightRGB = UncompressedPaletteColor.ToRGB(Elements[right].Color);
         var rightHSB = Theme.ToHSB((byte)(rightRGB.r << 3), (byte)(rightRGB.g << 3), (byte)(rightRGB.b << 3));

         var deltaHue = rightHSB.hue - leftHSB.hue;
         var deltaSat = rightHSB.sat - leftHSB.sat;
         var deltaBright = rightHSB.bright - leftHSB.bright;

         var distance = right - left;
         for (int i = 1; i < distance; i++) {
            if (!Elements[left + i].Selected) continue;
            var part = (double)i / distance;
            var hue = leftHSB.hue + deltaHue * part;
            var sat = leftHSB.sat + deltaSat * part;
            var bright = leftHSB.bright + deltaBright * part;
            var (red, green, blue) = Theme.FromHSB(hue, sat, bright);
            Elements[left + i].Color = UncompressedPaletteColor.Pack(red >> 3, green >> 3, blue >> 3);
         }

         PushColorsToModel();
      }

      private bool CanExecuteCreateGradient() => Elements.Count(element => element.Selected) > 2;

      private void ExecuteSingleReduce() {
         if (model == null) return;
         int sourcePalette = model.ReadPointer(sourcePalettePointer);
         if (!(model.GetNextRun(sourcePalette) is IPaletteRun paletteRun)) return;
         int pageOffset = (paletteRun.PaletteFormat.InitialBlankPages + Page) << 4;

         var masses = new List<ColorMass>();
         for (int i = 0; i < Elements.Count; i++) {
            if (!Elements[i].Selected) continue;
            int count = 0;
            foreach (var dependent in paletteRun.FindDependentSprites(model)) {
               var items = new List<ISpriteRun> { dependent };
               if (dependent is LzTilesetRun tileset) {
                  items.Clear();
                  items.AddRange(tileset.FindDependentTilemaps(model));
               }
               foreach (var sprite in items) {
                  for (int j = 0; j < sprite.Pages; j++) {
                     foreach (int pixelIndex in sprite.GetPixels(model, j)) {
                        if (pixelIndex == pageOffset + i) count += 1;
                     }
                  }
               }
            }
            masses.Add(new ColorMass(Elements[i].Color, count));
         }

         var (keepIndex, mergeIndex) = WeightedPalette.CheapestMerge(masses, out var _);
         int elementKeepIndex = Elements.Where(e => e.Selected).Skip(keepIndex).First().Index;
         int elementMergeIndex = Elements.Where(e => e.Selected).Skip(mergeIndex).First().Index;

         foreach (var dependent in paletteRun.FindDependentSprites(model)) {
            var items = new List<ISpriteRun> { dependent };
            if (dependent is LzTilesetRun tileset) {
               items.Clear();
               items.AddRange(tileset.FindDependentTilemaps(model));
            }
            foreach (var sprite in items) {
               var newSprite = sprite;
               for (int j = 0; j < newSprite.Pages; j++) {
                  var pixels = newSprite.GetPixels(model, j);
                  for (int x = 0; x < pixels.GetLength(0); x++) {
                     for (int y = 0; y < pixels.GetLength(1); y++) {
                        if (pixels[x, y] == elementMergeIndex + pageOffset) pixels[x, y] = elementKeepIndex + pageOffset;
                     }
                  }
                  newSprite = newSprite.SetPixels(model, history.CurrentChange, j, pixels);
               }
               if (newSprite.Start != sprite.Start) {
                  tab.RaiseMessage($"Sprite was moved to {newSprite.Start:X6}. Pointers were updated.");
                  if (tab is IViewPort viewPort) {
                     viewPort.Goto.Execute(newSprite.Start);
                  }
               }
            }
         }

         Elements[elementKeepIndex].Color = (masses[keepIndex] + masses[mergeIndex]).ResultColor;
         Elements[elementMergeIndex].Color = default;
         PushColorsToModel();
      }

      private bool CanExecuteSingleReduce() => model != null && Elements.Count(element => element.Selected) > 1;

      private void ExecuteDelete() {
         for (int i = 0; i < Elements.Count; i++) {
            if (Elements[i].Selected) Elements[i].Color = 0;
         }
         PushColorsToModel();
      }

      #endregion
   }

   [DebuggerDisplay("{Index}:{Color}")]
   public class SelectableColor : ViewModelCore {
      private bool selected;
      public bool Selected { get => selected; set => Set(ref selected, value); }

      private short color;
      public short Color { get => color; set => Set(ref color, value); }

      private int index;
      public int Index { get => index; set => Set(ref index, value); }
   }
}
