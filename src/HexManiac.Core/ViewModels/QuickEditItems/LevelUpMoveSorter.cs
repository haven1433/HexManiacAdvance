using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using System;
using System.Threading.Tasks;

namespace HavenSoft.HexManiac.Core.ViewModels.QuickEditItems {
   public class LevelUpMoveSorter : IQuickEditItem {
      public string Name => "Level Up Move Sort";

      public string Description => "Sorts all level-up moves of all Pokemon in ascending order." +
         Environment.NewLine + "By Petuuuhhh (thanks to Haven for walking me through it!)";

      public string WikiLink => "TODO";

      public event EventHandler CanRunChanged;

      public bool CanRun(IViewPort viewPort) {
         return viewPort is IEditableViewPort;
      }

      public async Task<ErrorInfo> Run(IViewPort viewPort) {
         var viewModel = (IEditableViewPort)viewPort;
         var model = viewModel.Model;
         var levelUpMoveTable = model.GetTable(HardcodeTablesModel.LevelMovesTableName);
         for (int i = 0; i < levelUpMoveTable.ElementCount; i++) {
            var destination = levelUpMoveTable.ReadPointer(model, i);
            var moveset = (ITableRun)model.GetNextRun(destination);
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
