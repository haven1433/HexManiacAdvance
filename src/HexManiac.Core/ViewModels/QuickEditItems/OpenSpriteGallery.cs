using HavenSoft.HexManiac.Core.Models;
using System;
using System.Threading.Tasks;

namespace HavenSoft.HexManiac.Core.ViewModels.QuickEditItems {
   /// <summary>
   /// Opens a tab that shows every overworld sprite (first frame) with its index and name, so you can find a sprite by looking.
   /// </summary>
   public class OpenSpriteGallery : IQuickEditItem {
      public string Name => "Overworld Sprite Gallery";
      public string Description => "Open a tab that shows the first frame of every overworld sprite next to its number and name. Click one to jump to it.";
      public string WikiLink => string.Empty;
      public event EventHandler CanRunChanged;

      public bool CanRun(IViewPort viewPortInterface) {
         if (viewPortInterface is not ViewPort viewPort) return false;
         return ReorderDex.GetTable(viewPort.Model, HardcodeTablesModel.OverworldSprites) != null;
      }

      public Task<ErrorInfo> Run(IViewPort viewPortInterface) {
         ((ViewPort)viewPortInterface).OpenSpriteGalleryTab();
         return Task.FromResult(ErrorInfo.NoError);
      }

      public void TabChanged() => CanRunChanged?.Invoke(this, EventArgs.Empty);
   }
}
