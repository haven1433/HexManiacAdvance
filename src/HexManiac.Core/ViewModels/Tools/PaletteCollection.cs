using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;


namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class ReadonlyPaletteCollection : ViewModelCore {
      public int ColorWidth => (int)Math.Ceiling(Math.Sqrt(Colors.Count));
      public int ColorHeight => (int)Math.Sqrt(Colors.Count);
      public int Index { get; }
      public bool DisplayIndex { get; }
      public ObservableCollection<short> Colors { get; } = new ObservableCollection<short>();
      public ReadonlyPaletteCollection(IEnumerable<short> colors, int index = -1) {
         foreach (var color in colors) Colors.Add(color);
         Index = index;
         DisplayIndex = index >= 0;
      }
   }

   public class PaletteCollection : ViewModelCore {
      public readonly IRaiseMessageTab tab;
      public readonly IDataModel model;
      public readonly ChangeHistory<ModelDelta> history;

      public int sourcePalettePointer;
      public int SourcePalettePointer { get => sourcePalettePointer; set => Set(ref sourcePalettePointer, value); }
      public ObservableCollection<SelectableColor> Elements { get; } = new();

      public int ColorWidth => Elements.Count / ColorHeight;
      public int ColorHeight => (int)Math.Ceiling(Math.Sqrt(Elements.Count));
      public bool CanEditColors => SourcePalettePointer >= 0 && page >= 0 && (model == null || SourcePalettePointer <= model.Count - 4);

      public int SpriteBitsPerPixel { get; set; }

      public event EventHandler SelectionSet;

      public int selectionStart;

      public int selectionEnd;

      public int page;
      public int Page { get => page; set => Set(ref page, value); }

      public int hoverIndex;

      public bool hasMultiplePages;
      public bool HasMultiplePages { get => hasMultiplePages; set => Set(ref hasMultiplePages, value); }

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

      #region Commands

      public void ExecuteCopy(IFileSystem fileSystem) {
         var copied = new List<string>();
         foreach (var element in Elements) {
            if (element.Selected) copied.Add(UncompressedPaletteColor.Convert(element.Color));
         }
         fileSystem.CopyText = " ".Join(copied);
      }

      public bool CanExecuteCopy(IFileSystem fileSystem) => 0 <= selectionStart && selectionStart < Elements.Count;

      public static IReadOnlyList<short> ParseColor(string stream) {
         var results = new List<short>();
         var parts = stream.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
         if (parts.Length == 64 && 16.Range().All(k => parts[k * 4 + 3] == "00") && 64.Range().All(k => parts[k].Length == 2)) {
            // .pal paste
            for (int i = 0; i < 16; i++) {
               if (!byte.TryParse(parts[i * 4 + 0], NumberStyles.HexNumber, CultureInfo.CurrentCulture, out var blue)) return null;
               if (!byte.TryParse(parts[i * 4 + 1], NumberStyles.HexNumber, CultureInfo.CurrentCulture, out var green)) return null;
               if (!byte.TryParse(parts[i * 4 + 2], NumberStyles.HexNumber, CultureInfo.CurrentCulture, out var red)) return null;
               results.Add(UncompressedPaletteColor.Pack(red >> 3, green >> 3, blue >> 3));
            }
            return results;
         }

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
               results.Add((short)((high << 8) | low));
            } else {
               return null;
            }
         }
         return results;
      }

      public bool CanExecutePaste(IFileSystem fileSystem) => CanExecuteCopy(fileSystem) && ParseColor(fileSystem.CopyText) != null;

      public bool CanExecuteCreateGradient() => Elements.Count(element => element.Selected) > 2;

      public bool CanExecuteSingleReduce() => model != null && Elements.Count(element => element.Selected) > 1;

      #endregion
   }

   [DebuggerDisplay("{Index}:{Color}")]
   public class SelectableColor : ViewModelCore {
      public bool selected;
      public bool Selected { get => selected; set => Set(ref selected, value); }

      public short color;
      public short Color { get => color; set => Set(ref color, value); }

      public int index;
      public int Index { get => index; set => Set(ref index, value); }
   }
}
