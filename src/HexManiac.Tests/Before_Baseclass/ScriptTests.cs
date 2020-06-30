using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Code;
using HavenSoft.HexManiac.Core.Models.Runs;
using System;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class ScriptTests : BaseViewModelTestClass {
      private readonly ScriptParser battle;

      public ScriptTests() {
         battle = ViewPort.Tools.CodeTool.BattleScriptParser;
      }

      [Fact]
      public void DecompiledScriptsContainLabels() {
         var script = Script("jumpiftype 0 0 <0007>", "end");
         var code = battle.Compile(ViewPort.CurrentChange, Model, 0, ref script, out var _);
         Array.Copy(code, Model.RawData, code.Length);
         battle.FormatScript<BSERun>(new ModelDelta(), Model, 0);

         var lines = battle.Parse(Model, 0, code.Length).SplitLines();

         Assert.Equal<string>(new string[] {
            "  jumpiftype 00 00 <000007>",
            "000007:",
            "  end",
            "",
         }, lines);
      }

      [Fact]
      public void BattleScriptsInnerPointersDecompileToRelativeOffsets() {
         var script = Script("jumpiftype 00 00 <000007>", "000007:", "end");

         var code = battle.Compile(ViewPort.CurrentChange, Model, 0x10, ref script, out var _);
         Array.Copy(code, 0, Model.RawData, 0x10, code.Length);
         battle.FormatScript<BSERun>(new ModelDelta(), Model, 0x10);

         var run = (PointerRun)Model.GetNextRun(0x13);
         Assert.Equal(0x13, run.Start);
         Assert.Equal(0x17, Model.ReadPointer(0x13));
      }

      private string Script(params string[] lines) => lines.CombineLines();
   }
}
