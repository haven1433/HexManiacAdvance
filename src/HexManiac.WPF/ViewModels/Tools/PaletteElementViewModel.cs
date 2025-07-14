using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using System.Linq;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class PaletteElementViewModel : PagedElementViewModel {
      private PaletteFormat format;

      public string TableName { get; private set; }

      public PaletteCollection Colors { get; }

      public PaletteElementViewModel(ViewPort viewPort, ChangeHistory<ModelDelta> history, string parentName, string runFormat, PaletteFormat format, int itemAddress) : base(viewPort, parentName, runFormat, itemAddress) {
         this.format = format;

         var table = (ITableRun)viewPort.Model.GetNextRun(itemAddress);
         Colors = new PaletteCollection(viewPort, viewPort.Model, history);
         AddSilentChild(Colors);
         Colors.RequestPageSet += HandleColorsPageSet;
         Colors.PaletteRepointed += HandlePaletteRepoint;
         TableName = viewPort.Model.GetAnchorFromAddress(-1, table.Start);
         var destination = Model.ReadPointer(Start);
         var run = viewPort.Model.GetNextRun(destination) as IPaletteRun;
         Pages = run?.Pages ?? 0;
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

      public void Activate() { }

      protected override bool CanExecuteAddPage() {
         var destination = ViewPort.Model.ReadPointer(Start);
         var run = ViewPort.Model.GetNextRun(destination) as IPaletteRun;
         return run is LzPaletteRun && CurrentPage == run.Pages - 1 && run.FindDependentSprites(Model).All(sprite => sprite.Pages == run.Pages && sprite is LzSpriteRun);
      }

      protected override bool CanExecuteDeletePage() {
         var destination = ViewPort.Model.ReadPointer(Start);
         var run = ViewPort.Model.GetNextRun(destination) as IPaletteRun;
         return run is LzPaletteRun && Pages > 1 && run.FindDependentSprites(Model).All(sprite => sprite.Pages == run.Pages && sprite is LzSpriteRun);
      }

      private void UpdateColors(int start, int page) {
         var destination = Model.ReadPointer(start);
         var run = ViewPort.Model.GetNextRun(destination) as IPaletteRun;
         Colors.SourcePalettePointer = start;
         var palette = run?.GetPalette(Model, page) ?? TileViewModel.CreateDefaultPalette(16);
         Colors.SetContents(palette);
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

      private void HandlePaletteRepoint(object sender, int address) => ViewPort.RaiseMessage($"Palette moved to {address:X6}. Pointers were updated.");
   }
}
