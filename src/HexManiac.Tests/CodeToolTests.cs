using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class CodeToolTests : BaseViewModelTestClass {
      private string EventScript { set => ViewPort.Tools.CodeTool.Contents[0].Content = value; }

      [Fact]
      public void AddAndRemoveAnchorInSameToken_Undo_NoAnchor() {
         SetFullModel(0xFF);
         WriteEventScript(0x10, "end");
         WriteEventScript(0x20, "goto <010>");

         EventScript = "goto <00000>";
         EventScript = "goto <000030>";
         EventScript = "goto <00000>";
         EventScript = "goto <000010>";

         ViewPort.Undo.Execute();

         var run = Model.GetNextRun(0x30);
         Assert.NotEqual(0x30, run.Start);
      }
   }
}
