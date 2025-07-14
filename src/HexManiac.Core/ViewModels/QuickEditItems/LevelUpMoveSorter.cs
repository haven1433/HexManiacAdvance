using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using System;
using System.Threading.Tasks;

namespace HavenSoft.HexManiac.Core.ViewModels.QuickEditItems {
   public class LevelUpMoveSorter : IQuickEditItem {
      public string Name => "Level Up Move Sort";

      public string Description => "Sorts all level-up moves of all Pokemon in ascending order." +
         Environment.NewLine + "By Petuuuhhh (thanks to Haven for walking me through it!)";

      public string WikiLink => "https://github.com/haven1433/HexManiacAdvance/wiki/Sorting-Level-Up-Moves-Explained";

      public event EventHandler CanRunChanged;

      public bool CanRun(IViewPort viewPort) {
         if (!(viewPort is IEditableViewPort)) return false;
         return viewPort.Model.GetTable(HardcodeTablesModel.LevelMovesTableName) != null;
      }

      public void TabChanged() => CanRunChanged?.Invoke(this, EventArgs.Empty);
   }
}
