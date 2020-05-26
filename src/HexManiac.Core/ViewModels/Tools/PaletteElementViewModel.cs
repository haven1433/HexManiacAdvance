using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class PaletteElementViewModel : PagedElementViewModel, IPagedViewModel {
      private PaletteFormat format;

      public string TableName { get; private set; }

      public PaletteCollection Colors { get; }

      public PaletteElementViewModel(ViewPort viewPort, ChangeHistory<ModelDelta> history, PaletteFormat format, int itemAddress) : base(viewPort, itemAddress) {
         this.format = format;

         var table = (ITableRun)viewPort.Model.GetNextRun(itemAddress);
         Colors = new PaletteCollection(viewPort, history);
         TableName = viewPort.Model.GetAnchorFromAddress(-1, table.Start);
         var destination = Model.ReadPointer(Start);
         var run = viewPort.Model.GetNextRun(destination) as IPaletteRun;
         Pages = run.Pages;
         UpdateColors(Start, 0);
      }

      /// <summary>
      /// Note that this method runs _before_ changes are copied from the baseclass
      /// So if we want to update colors based on the new start point,
      /// Then UpdateColors can't rely on our internal start point
      /// </summary>
      protected override bool TryCopy(PagedElementViewModel other) {
         if (!(other is PaletteElementViewModel that)) return false;
         format = that.format;
         UpdateColors(other.Start, other.CurrentPage);
         return true;
      }

      protected override void PageChanged() => UpdateColors(Start, CurrentPage);

      public void Activate() => UpdateSprites(TableName);

      public void UpdateAssociatedPointers() {
         Colors.RelatedPointers.Clear();
         foreach (var child in ViewPort.Tools.TableTool.Children) {
            if (child is SpriteElementViewModel sevm) {
               // TODO check that this sprite actually uses this palette
               Colors.RelatedPointers.Add(sevm.Start);
            } else if (child is PaletteElementViewModel pevm) {
               Colors.RelatedPointers.Add(pevm.Start);
            }
         }
      }

      private void UpdateSprites(string hint = null) {
         foreach (var child in ViewPort.Tools.TableTool.Children) {
            if (child == this) break;
            if (!(child is SpriteElementViewModel sevm)) continue;
            sevm.UpdateTiles(hint: hint);
         }
      }

      private void UpdateColors(int start, int page) {
         var destination = Model.ReadPointer(start);
         var run = ViewPort.Model.GetNextRun(destination) as IPaletteRun;
         Colors.SetContents(run.GetPalette(Model, page));
      }
   }

   public class RangedObservableCollection<T> : List<T>, INotifyCollectionChanged {
      private readonly NotifyCollectionChangedEventArgs Reset = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);
      public event NotifyCollectionChangedEventHandler CollectionChanged;
      public void SetContents(IEnumerable<T> elements) {
         Clear();
         AddRange(elements);
         CollectionChanged?.Invoke(this, Reset);
      }
      public new void Insert(int index, T item) {
         base.Insert(index, item);
         CollectionChanged?.Invoke(this, Reset);
      }
   }

   public class PaletteCollection : ViewModelCore {
      private readonly ViewPort viewPort;
      private readonly ChangeHistory<ModelDelta> history;

      public ObservableCollection<int> RelatedPointers { get; } = new ObservableCollection<int>();
      public ObservableCollection<SelectableColor> Elements { get; } = new ObservableCollection<SelectableColor>();

      public int ColorWidth => (int)Math.Ceiling(Math.Sqrt(Elements.Count));
      public int ColorHeight => (int)Math.Sqrt(Elements.Count);

      private int selectionStart;
      public int SelectionStart {
         get => selectionStart;
         set {
            var first = Math.Min(selectionStart, selectionEnd);
            var last = Math.Max(selectionStart, selectionEnd);
            if (first <= value && value <= last) return;
            if (!TryUpdate(ref selectionStart, value)) return;
            SelectionEnd = selectionStart;
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
         }
      }

      public PaletteCollection(ViewPort viewPort, ChangeHistory<ModelDelta> history) {
         this.viewPort = viewPort;
         this.history = history;
      }

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

      public void CompleteCurrentInteraction() {
         ReorderPalette();
         history.ChangeCompleted();
      }

      public void SetContents(IReadOnlyList<short> colors) {
         Elements.Clear();
         foreach (var element in Enumerable.Range(0, colors.Count)
            .Select(i => new SelectableColor { Color = colors[i], Index = i })) {
            Elements.Add(element);
         }
         NotifyPropertyChanged(nameof(ColorWidth));
         NotifyPropertyChanged(nameof(ColorHeight));
      }

      private void ReorderPalette() {
         var oldToNew = Enumerable.Range(0, Elements.Count).Select(i => Elements.IndexOf(Elements.Single(element => element.Index == i))).ToArray();
         var newElements = Enumerable.Range(0, Elements.Count).Select(i => new SelectableColor {
            Index = i,
            Color = Elements[Elements[i].Index].Color,
            Selected = Elements[i].Selected,
         }).ToList();

         var model = viewPort.Model;
         foreach (var pointer in RelatedPointers) {
            var run = model.GetNextRun(model.ReadPointer(pointer));
            if (run is ISpriteRun spriteRun) {
               var initialStart = spriteRun.Start;
               for (int page = 0; page < spriteRun.Pages; page++) {
                  var pixels = spriteRun.GetPixels(model, 0);
                  for (int y = 0; y < pixels.GetLength(1); y++) {
                     for (int x = 0; x < pixels.GetLength(0); x++) {
                        pixels[x, y] = oldToNew[pixels[x, y]];
                     }
                  }
                  spriteRun = spriteRun.SetPixels(model, history.CurrentChange, page, pixels);
               }
               if (initialStart != spriteRun.Start) viewPort.RaiseMessage($"Sprite was moved to {spriteRun.Start:X6}. Pointers were updated.");
            } else if (run is IPaletteRun paletteRun) {
               var initalStart = paletteRun.Start;
               for (int page = 0; page < paletteRun.Pages; page++) {
                  var colors = paletteRun.GetPalette(model, page);
                  var newColors = Enumerable.Range(0, Elements.Count).Select(i => colors[Elements[i].Index]).ToList();
                  paletteRun = paletteRun.SetPalette(model, history.CurrentChange, page, newColors);
               }
               if (initalStart != paletteRun.Start) viewPort.RaiseMessage($"Palette was moved to {paletteRun.Start:X6}. Pointers were updated.");
            } else {
               // expected this to either be a spriterun or a paletterun, not really sure what to do with it now.
               throw new NotImplementedException();
            }
         }

         viewPort.Refresh();

         for (int i = 0; i < Elements.Count; i++) Elements[i].Selected = newElements[i].Selected;
      }
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
