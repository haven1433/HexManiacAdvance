using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HavenSoft.HexManiac.Core.ViewModels.QuickEditItems {
   public class AddTilesetAnimation : IQuickEditItem {
      private readonly IFileSystem fileSystem;

      public string Name => "Add Tileset Animation";

      public string Description => "Add a new table and code for adding animations to a map tileset." + Environment.NewLine +
         "Look for your new table with the name `graphics.maps.tilesets.animations. <something>.table`." + Environment.NewLine +
         "Look for your new animation routine with the name `graphics.maps.tilesets.animations. <something>.init`.";

      public string WikiLink => "https://github.com/haven1433/HexManiacAdvance/wiki/Tileset-Animations-Explained";

      public event EventHandler CanRunChanged;

      public AddTilesetAnimation(IFileSystem fileSystem) => this.fileSystem = fileSystem;

      public bool CanRun(IViewPort viewPort) {
         return viewPort is IEditableViewPort vp && vp.Model.GetGameCode() == "BPRE0";
      }

      public void TabChanged() => CanRunChanged?.Invoke(this, EventArgs.Empty);
   }
}
