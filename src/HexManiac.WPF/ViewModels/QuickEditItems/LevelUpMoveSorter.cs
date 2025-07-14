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

      public async Task<ErrorInfo> Run(IViewPort viewPort) {
         var viewModel = (IEditableViewPort)viewPort;
         var model = viewModel.Model;
         var levelUpMoveTable = model.GetTable(HardcodeTablesModel.LevelMovesTableName);
         for (int i = 0; i < levelUpMoveTable.ElementCount; i++) {
            var destination = levelUpMoveTable.ReadPointer(model, i);
            var moveset = model.GetNextRun(destination) as ITableRun;
            if (moveset == null) continue;
            var sortedMoves = moveset.Sort(model, moveset.ElementContent.Count - 1);
            viewModel.ChangeHistory.CurrentChange.ChangeData(model, moveset.Start, sortedMoves);
            await viewModel.UpdateProgress((double)i / levelUpMoveTable.ElementCount);
         }

         viewModel.ClearProgress();
         viewModel.ChangeHistory.ChangeCompleted();
         viewModel.Refresh();
         return ErrorInfo.NoError;
      }

      public void TabChanged() => CanRunChanged?.Invoke(this, EventArgs.Empty);
   }
}
