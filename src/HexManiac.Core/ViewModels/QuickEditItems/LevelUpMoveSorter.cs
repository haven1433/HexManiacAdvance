using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using System;
using System.Threading.Tasks;

namespace HavenSoft.HexManiac.Core.ViewModels.QuickEditItems {
   public class LevelUpMoveSorter : IQuickEditItem {
      public string Name => "Level Up Move Sort";

      public string Description => "Sorts all level-up moves of all Pokemon in ascending order." +
         Environment.NewLine + "By Petuuuhhh (thanks to Haven for walking me through it!)";

      public string WikiLink => String.Empty;

      public event EventHandler CanRunChanged;

      public bool CanRun(IViewPort viewPort) {
         return viewPort is IEditableViewPort;
      }

      public async Task<ErrorInfo> Run(IViewPort viewPort) {
         var viewModel = (IEditableViewPort) viewPort;
         var model = viewModel.Model;
         var levelUpMoveTable = model.GetTable(HardcodeTablesModel.LevelMovesTableName);
         for (int i = 0; i < levelUpMoveTable.ElementCount; i++) {
            var destination = levelUpMoveTable.ReadPointer(model, i);
            var thisPokemonsTable = (ITableRun) model.GetNextRun(destination);
            var sortedMoves = thisPokemonsTable.Sort(model, 0);
            viewModel.ChangeHistory.CurrentChange.ChangeData(model, thisPokemonsTable.Start, sortedMoves);
            await viewModel.UpdateProgress((double)i / levelUpMoveTable.ElementCount);
         }
         viewModel.ClearProgress();
         viewModel.Refresh();
         return ErrorInfo.NoError;
      }

      public void TabChanged() {
         CanRunChanged(this, EventArgs.Empty);
      }
   }
}
