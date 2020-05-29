using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class PaletteCollection : ViewModelCore {
      private readonly ViewPort viewPort;
      private readonly ChangeHistory<ModelDelta> history;

      private int sourcePalette;
      public int SourcePalette { get => sourcePalette; set => Set(ref sourcePalette, value); }
      public ObservableCollection<SelectableColor> Elements { get; } = new ObservableCollection<SelectableColor>();

      public int ColorWidth => (int)Math.Ceiling(Math.Sqrt(Elements.Count));
      public int ColorHeight => (int)Math.Sqrt(Elements.Count);

      private int selectionStart;
      public int SelectionStart {
         get => selectionStart;
         set {
            var first = Math.Min(selectionStart, selectionEnd);
            var last = Math.Max(selectionStart, selectionEnd);
            if (first <= value && value <= last) {
               for (int i = 0; i < Elements.Count; i++) Elements[i].Selected = first <= i && i <= last;
               return;
            }
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
         var model = viewPort.Model;
         if (!(model.GetNextRun(sourcePalette) is IPaletteRun source)) return;

         var oldToNew = Enumerable.Range(0, Elements.Count).Select(i => Elements.IndexOf(Elements.Single(element => element.Index == i))).ToArray();
         var newElements = Enumerable.Range(0, Elements.Count).Select(i => new SelectableColor {
            Index = i,
            Color = Elements[Elements[i].Index].Color,
            Selected = Elements[i].Selected,
         }).ToList();

         var palettesToUpdate = new List<IPaletteRun> { source };
         foreach (var sprite in source.FindDependentSprites(model).Distinct()) {
            var newSprite = sprite;
            for (int page = 0; page < newSprite.Pages; page++) {
               var pixels = newSprite.GetPixels(model, page);
               for (int y = 0; y < pixels.GetLength(1); y++) {
                  for (int x = 0; x < pixels.GetLength(0); x++) {
                     pixels[x, y] = oldToNew[pixels[x, y]];
                  }
               }
               newSprite = newSprite.SetPixels(model, history.CurrentChange, page, pixels);
            }
            if (newSprite.Start != sprite.Start) viewPort.RaiseMessage($"Sprite was moved to {newSprite.Start:X6}. Pointers were updated.");
            palettesToUpdate.AddRange(newSprite.FindRelatedPalettes(model));
         }

         foreach (var palette in palettesToUpdate.Distinct()) {
            var newPalette = palette;
            for (int page = 0; page < newPalette.Pages; page++) {
               var colors = newPalette.GetPalette(model, page);
               var newColors = Enumerable.Range(0, Elements.Count).Select(i => colors[Elements[i].Index]).ToList();
               newPalette = newPalette.SetPalette(model, history.CurrentChange, page, newColors);
            }
            if (palette.Start != newPalette.Start) viewPort.RaiseMessage($"Palette was moved to {newPalette.Start:X6}. Pointers were updated.");
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
