using HavenSoft.HexManiac.Core.Models.Runs;

using static HavenSoft.HexManiac.Core.ICommandExtensions;

namespace HavenSoft.HexManiac.Core.ViewModels {
   public class Shortcuts : ViewModelCore {
      public ViewPort ViewPort { get; }

      public Shortcuts(ViewPort viewPort) => ViewPort = viewPort;

      public bool CanExecuteDisplayAs() {
         var spot = ViewPort.ConvertViewPointToAddress(ViewPort.SelectionStart);
         var nextRun = ViewPort.Model.GetNextRun(spot);
         return nextRun.Start > spot || nextRun is NoInfoRun;
      }
   }
}
