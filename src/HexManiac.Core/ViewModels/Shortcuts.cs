using HavenSoft.HexManiac.Core.Models.Runs;
using System.Windows.Input;
using static HavenSoft.HexManiac.Core.ICommandExtensions;

namespace HavenSoft.HexManiac.Core.ViewModels {
   public class Shortcuts : ViewModelCore {
      private StubCommand displayAsEventScript, displayAsText, displayAsSprite, displayAsColorPalette;

      public ViewPort ViewPort { get; }

      public ICommand DisplayAsEventScript => StubCommand(ref displayAsEventScript, ExecuteDisplayAsEventScript, CanExecuteDisplayAs);
      public ICommand DisplayAsText=> StubCommand(ref displayAsText, ExecuteDisplayAsText, CanExecuteDisplayAs);
      public ICommand DisplayAsSprite=> StubCommand(ref displayAsSprite, ExecuteDisplayAsSprite, CanExecuteDisplayAs);
      public ICommand DisplayAsColorPalette=> StubCommand(ref displayAsColorPalette, ExecuteDisplayAsColorPalette, CanExecuteDisplayAs);

      public Shortcuts(ViewPort viewPort) => ViewPort = viewPort;

      private bool CanExecuteDisplayAs() {
         var spot = ViewPort.ConvertViewPointToAddress(ViewPort.SelectionStart);
         var nextRun = ViewPort.Model.GetNextRun(spot);
         return nextRun.Start > spot || nextRun is NoInfoRun;
      }

      private void ExecuteDisplayAsEventScript() {
         ViewPort.Tools.CodeTool.IsEventScript.Execute();
         ViewPort.Refresh();
         ViewPort.UpdateToolsFromSelection(ViewPort.ConvertViewPointToAddress(ViewPort.SelectionStart));
      }

      private void ExecuteDisplayAsText() {
         ViewPort.IsText.Execute();
      }

      private void ExecuteDisplayAsSprite() {
         ViewPort.Tools.SpriteTool.IsSprite.Execute();
      }

      private void ExecuteDisplayAsColorPalette() {
         ViewPort.Tools.SpriteTool.IsPalette.Execute();
      }
   }
}
