using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class PaletteElementViewModel : PagedElementViewModel, IPagedViewModel {
      private PaletteFormat format;

      public string TableName { get; private set; }

      public ObservableCollection<short> Colors { get; } = new ObservableCollection<short>();

      public int ColorWidth => (int)Math.Ceiling(Math.Sqrt(Colors.Count));
      public int ColorHeight => (int)Math.Sqrt(Colors.Count);
      public int PixelWidth => 8 * ColorWidth;
      public int PixelHeight => 8 * ColorHeight;

      public PaletteElementViewModel(ViewPort viewPort, PaletteFormat format, int itemAddress) : base(viewPort, itemAddress) {
         this.format = format;
         TableName = viewPort.Model.GetAnchorFromAddress(-1, viewPort.Model.GetNextRun(itemAddress).Start);
         UpdateColors();
      }

      protected override bool TryCopy(PagedElementViewModel other) {
         if (!(other is PaletteElementViewModel that)) return false;
         format = that.format;
         ErrorText = that.ErrorText;
         NotifyPropertyChanged(nameof(Start));
         NotifyPropertyChanged(nameof(ErrorText));
         UpdateColors();
         return true;
      }

      public void Activate() {
         foreach (var child in ViewPort.Tools.TableTool.Children) {
            if (!(child is SpriteElementViewModel saevm)) continue;
            saevm.UpdateTiles(TableName);
         }
      }

      private void UpdateColors() {
         var destination = Model.ReadPointer(Start);
         var data = LZRun.Decompress(Model, destination);
         int colorCount = (int)Math.Pow(2, format.Bits);
         int pageLength = colorCount * 2;
         Debug.Assert(data.Length % colorCount * 2 == 0);
         Colors.Clear();
         Pages = data.Length / pageLength;
         if (CurrentPage >= Pages) CurrentPage = 0;
         int pageStart = CurrentPage * pageLength;
         for (int i = 0; i < colorCount; i++) {
            var color = (short)data.ReadMultiByteValue(pageStart + i * 2, 2);
            Colors.Add(color);
         }

         NotifyPropertyChanged(nameof(CurrentPage));
         NotifyPropertyChanged(nameof(Pages));
      }
   }
}
