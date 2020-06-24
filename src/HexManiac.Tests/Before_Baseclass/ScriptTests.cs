using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models.Code;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class ScriptTests : BaseViewModelTestClass {
      private readonly ScriptParser battle;

      public ScriptTests() {
         battle = ViewPort.Tools.CodeTool.BattleScriptParser;
      }

      [Fact]
      public void BattleScriptsInnerPointersDecompileToRelativeOffsets() {
         var script = Script("jumpiftype 0 0 <0008>", "end");

         var code = battle.Compile(ViewPort.CurrentChange, Model, 0, ref script, out var movedData);
         var lines = script.SplitLines();

         Assert.Equal(new string[] {
            "jumpiftype 0 0 <000008>",
            "000008:",
            "end",
         }, lines);
      }

      private string Script(params string[] lines) => lines.CombineLines();
   }
}
