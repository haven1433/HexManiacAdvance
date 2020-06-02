using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using System;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class PaletteElementViewModel : PagedElementViewModel, IPagedViewModel {
      private PaletteFormat format;

      public string TableName { get; private set; }

      public PaletteCollection Colors { get; }

      public PaletteElementViewModel(ViewPort viewPort, ChangeHistory<ModelDelta> history, PaletteFormat format, int itemAddress) : base(viewPort, itemAddress) {
         this.format = format;

         var table = (ITableRun)viewPort.Model.GetNextRun(itemAddress);
         Colors = new PaletteCollection(viewPort, history);
         Colors.RequestPageSet += HandleColorsPageSet;
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
         Colors.SourcePalette = run.Start;
         Colors.SetContents(run.GetPalette(Model, page));
         Colors.Page = page;
         Colors.HasMultiplePages = Pages > 1;
      }

      private void HandleColorsPageSet(object sender, int page) {
         foreach (var child in ViewPort.Tools.TableTool.Children) {
            if (!(child is IPagedViewModel pvm)) continue;
            if (pvm.Pages != Pages) continue;
            pvm.CurrentPage = page;
         }
      }
   }
}
