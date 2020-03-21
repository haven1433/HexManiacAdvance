using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class PaletteElementViewModel : PagedElementViewModel, IPagedViewModel {
      private PaletteFormat format;
      private byte[] data;

      public string TableName { get; private set; }

      public ObservableCollection<short> Colors { get; } = new ObservableCollection<short>();

      public int ColorWidth => (int)Math.Ceiling(Math.Sqrt(Colors.Count));
      public int ColorHeight => (int)Math.Sqrt(Colors.Count);

      public PaletteElementViewModel(ViewPort viewPort, PaletteFormat format, int itemAddress) : base(viewPort, itemAddress) {
         this.format = format;
         TableName = viewPort.Model.GetAnchorFromAddress(-1, viewPort.Model.GetNextRun(itemAddress).Start);
         DecodeData();
         UpdateColors(0);
      }

      /// <summary>
      /// Note that this method runs _before_ changes are copied from the baseclass
      /// So if we want to update colors based on the new start point,
      /// Then UpdateColors can't rely on our internal start point
      /// </summary>
      protected override bool TryCopy(PagedElementViewModel other) {
         if (!(other is PaletteElementViewModel that)) return false;
         format = that.format;
         data = that.data;

         UpdateColors(other.CurrentPage);
         NotifyPropertyChanged(nameof(ColorWidth));
         NotifyPropertyChanged(nameof(ColorHeight));
         return true;
      }

      protected override void PageChanged() => UpdateColors(CurrentPage);

      public void Activate() {
         foreach (var child in ViewPort.Tools.TableTool.Children) {
            if (!(child is SpriteElementViewModel sevm)) continue;
            sevm.UpdateTiles(hint: TableName);
         }
      }

      private void DecodeData() {
         int colorCount = (int)Math.Pow(2, format.Bits);
         int pageLength = colorCount * 2;

         var destination = Model.ReadPointer(Start);
         data = LZRun.Decompress(Model, destination);
         Debug.Assert(data.Length % colorCount * 2 == 0);
         Pages = data.Length / pageLength;
      }

      private void UpdateColors(int page) {
         page %= Pages;
         Colors.Clear();
         int colorCount = (int)Math.Pow(2, format.Bits);
         int pageLength = colorCount * 2;
         int pageStart = page * pageLength;
         for (int i = 0; i < colorCount; i++) {
            var color = (short)data.ReadMultiByteValue(pageStart + i * 2, 2);
            Colors.Add(color);
         }
      }
   }
}
