using HavenSoft.HexManiac.Core.Models;
using System;
using System.Globalization;
using System.Threading.Tasks;

namespace HavenSoft.HexManiac.Core.ViewModels.QuickEditItems {
   public class ExpandRom : IQuickEditItem {
      public string Name => "Expand Rom";

      public string Description => "Change the size of the gba file (make it bigger or smaller)." + Environment.NewLine +
         "Warning: Decreasing the size of your ROM will permanently delete some data." + Environment.NewLine +
         "Edit your ROM size carefully!";

      public string WikiLink => "https://github.com/haven1433/HexManiacAdvance/wiki/Rom-Expansion-Explained";

      public IFileSystem FileSystem { get; }

      public event EventHandler CanRunChanged;

      public ExpandRom(IFileSystem fs) => FileSystem = fs;

      public bool CanRun(IViewPort viewPort) {
         return viewPort is IEditableViewPort && viewPort.FileName.EndsWith(".gba");
      }

      public void TabChanged() {
         CanRunChanged?.Invoke(this, EventArgs.Empty);
      }
   }
}
