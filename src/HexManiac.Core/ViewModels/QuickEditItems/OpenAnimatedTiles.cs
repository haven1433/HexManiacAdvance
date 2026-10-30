using HavenSoft.HexManiac.Core.Models;
using System;
using System.Threading.Tasks;

namespace HavenSoft.HexManiac.Core.ViewModels.QuickEditItems {
   /// <summary>
   /// Opens the Animated Tiles editor: pick tiles in a map's tileset and give them frames that play in the overworld.
   /// </summary>
   public class OpenAnimatedTiles : IQuickEditItem {
      public string Name => "Animated Tiles Editor";
      public string Description => "Open a tab for adding animated tiles to a map's tileset: click the tiles, choose frames and speed, then draw or import the frames. The ROM's own tileset animations keep running.";
      public string WikiLink => string.Empty;
      public event EventHandler CanRunChanged;

      public bool CanRun(IViewPort viewPortInterface) {
         if (viewPortInterface is not ViewPort viewPort) return false;
         return TilesetAnimationTab.IsSupported(viewPort.Model);
      }

      public Task<ErrorInfo> Run(IViewPort viewPortInterface) {
         var viewPort = (ViewPort)viewPortInterface;
         // starts on Petalburg City (map 0-0) whatever map the map editor is on; the map list at the top switches maps
         viewPort.OpenTilesetAnimationTab(TilesetAnimationTab.DefaultGroup, TilesetAnimationTab.DefaultMap, false);
         return Task.FromResult(ErrorInfo.NoError);
      }

      public void TabChanged() => CanRunChanged?.Invoke(this, EventArgs.Empty);
   }
}
