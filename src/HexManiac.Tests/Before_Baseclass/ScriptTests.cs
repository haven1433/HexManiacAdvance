using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Code;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Linq;
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

      [Fact]
      public void CanCreateXseScriptFromContextMenu() {
         Model[0x22] = 0xFF;
         ViewPort.Refresh();

         ViewPort.SelectionStart = new Point(2, 2);
         var item = ViewPort.GetContextMenuItems(new Point(2, 2)).Single(cmi => cmi.Text == "Create New XSE Script");
         item.Command.Execute();

         Assert.Equal(ViewPort.Tools.CodeTool, ViewPort.Tools.SelectedTool);
         Assert.Equal(CodeMode.Script, ViewPort.Tools.CodeTool.Mode);
         Assert.EndsWith("end", ViewPort.Tools.CodeTool.Contents.Single().Content.Trim());
         Assert.IsType<XSERun>(Model.GetNextRun(0x22));
         var anchorName = Model.GetAnchorFromAddress(-1, 0x22);
         Assert.NotEmpty(anchorName);
         Assert.Equal(anchorName, ViewPort.AnchorText.Substring(ViewPort.AnchorTextSelectionStart, ViewPort.AnchorTextSelectionLength));
      }

      [Fact]
      public void ScriptLine_MultibyteEnum_Loads() {
         var args = Singletons.ScriptLines[0x44].Args;

         Assert.Equal(HardcodeTablesModel.ItemsTableName, args[0].EnumTableName);
         Assert.Equal(2, args[0].Length(null, 0));
      }

      private string Script(params string[] lines) => lines.CombineLines();
   }
}
