using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Code;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using HavenSoft.HexManiac.Core.ViewModels.Visitors;
using System;
using System.Collections.Generic;
using System.IO;
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
         var group = (ContextItemGroup)ViewPort.GetContextMenuItems(new Point(2, 2)).Single(cmi => cmi.Text == "Create New...");
         var item = group.Single(cmi => cmi.Text == "Event Script");
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

      [Fact]
      public void TrainerBattle3_Decode_Has3Arguments() {
         Model[0] = 0x5C;
         Model[1] = 3;
         Model[10] = 2;
         Model[14] = 2;

         var scriptText = ViewPort.Tools.CodeTool.ScriptParser.Parse(Model, 0, 2);

         var trainerBattleLine = scriptText.SplitLines()[0].Trim();
         var parts = trainerBattleLine.Split(' ');
         Assert.Equal(5, parts.Length);
      }

      [Theory]
      [InlineData("Expand01_-_Move_Stats")]
      [InlineData("Expand02_-_Pokemon_Move_Learn_Table")]
      [InlineData("Expand03_-_Relearner_move_tutor")]
      public void Asm_Compile_Thumb(string filename) {
         var inFile = $"test_code/{filename}.asm";
         var outFile = $"test_compiled/{filename}.bin";
         var lines = File.ReadAllLines(inFile);
         var expected = File.ReadAllBytes(outFile);

         var result = ViewPort.Tools.CodeTool.Parser.Compile(Model, 0, lines);

         Assert.All(expected.Length.Range(), i => Assert.Equal(expected[i], result[i]));
      }

      [Fact]
      public void BranchLinkToKnownName_Decompile_BranchLinkContainsKnownName() {
         Model.ObserveAnchorWritten(ViewPort.CurrentChange, "bob", new NoInfoRun(0x100));
         var thumb = ViewPort.Tools.CodeTool.Parser;
         Array.Copy(thumb.Compile(Model, 0, "bl <bob>").ToArray(), Model.RawData, 4);

         var decompile = thumb.Parse(Model, 0, 4).Split(Environment.NewLine);

         Assert.Equal("<bob>", decompile[1].Trim().Substring(2).Trim());
      }

      [Fact]
      public void GameCode_GameCommandWithThatCode_EditsAreMade() {
         SetGameCode("XXXX0");

         ViewPort.Edit("@!game(xxxx0) 11 ");

         Assert.Equal(0x11, Model[0]);
      }

      [Fact]
      public void GameCode_GameCommandWithDifferentCode_EditsAreNotMade() {
         SetGameCode("XXXX0");

         ViewPort.Edit("@!game(yyyy0) 11 ");

         Assert.Equal(0x00, Model[0]);
      }

      [Fact]
      public void GameCode_GameCommandWithThatCodeAndOthers_EditsAreMade() {
         SetGameCode("XXXX0");

         ViewPort.Edit("@!game(xxxx0_yyyy0) 11 ");

         Assert.Equal(0x11, Model[0]);
      }

      [Fact]
      public void EventScriptWithText_ClearText_ScriptIsUnchanged() {
         ViewPort.Edit("@100!put(FF) ^text\"\" Hello");
         var script = @"
loadpointer 00 <100>
{

}
end
";
         var code = ViewPort.Tools.CodeTool.ScriptParser.Compile(ViewPort.CurrentChange, Model, 0, ref script, out var _);

         Assert.Equal(7, code.Length);
         Assert.Equal("\"\"", PCSString.Convert(Model, 0x100, 0x100));
      }

      [Fact]
      public void LoadPointerCommand_StartsWithWhitespace_StillGetHelp() {
         Assert.NotEmpty(ViewPort.Tools.CodeTool.ScriptParser.GetHelp("  loadpointer"));
      }

      [Fact]
      public void StaticVariable_UseInThumb_ValueIsChanged() {
         ViewPort.Edit("@somevariable=0x12345678 ");
         var codeText = ".word somevariable";
         var codeBytes = ViewPort.Tools.CodeTool.Parser.Compile(Model, 0, codeText);
         Assert.Equal(0x12345678, codeBytes.ReadMultiByteValue(0, 4));
      }

      [Fact]
      public void AnimationScript_Write_Written() {
         SetFullModel(0xFF);
         ViewPort.Refresh();
         var tool = ViewPort.Tools.CodeTool;
         ViewPort.Tools.CodeToolCommand.Execute();
         ViewPort.SelectionStart = new Point(0, 0);
         tool.Mode = CodeMode.AnimationScript;

         var script = @"
Script:
   delay 10
   end";
         tool.Contents[0].Content = script;

         // 04 delay time.
         // 08 end
         var result = Model.Take(3).ToArray();
         Assert.Equal(new byte[] { 0x04, 0x10, 0x08 }, result);
      }

      [Fact]
      public void AnimationScript_CreateVisualTask_ArgumentsReadCorrectly() {
         SetFullModel(0xFF);
         ViewPort.Refresh();
         var tool = ViewPort.Tools.CodeTool;
         ViewPort.Tools.CodeToolCommand.Execute();
         ViewPort.SelectionStart = new Point(0, 0);
         tool.Mode = CodeMode.AnimationScript;

         var script = @"
Script:
   createvisualtask <0BA7F9> 0A 0001 0000 0006 0000 0000
   end
";
         tool.Contents[0].Content = script;

         // 03 createvisualtask address<> priority. [argv:]
         // 08 end
         var result = Model.Take(1 + 4 + 1 + (1 + 5 * 2) + 1).ToArray();
         //                          op    pointer               prir arg  0             1          2           3           4          end
         Assert.Equal(new byte[] { 0x03, 0xF9, 0xA7, 0x0B, 0x08, 0x0A, 5, 0x01, 0x00, 0x00, 0x00, 0x06, 0x00, 0x00, 0x00, 0x00, 0x00, 0x08 }, result);
      }

      [Fact]
      public void AnimationScript_LinkToBody_EncodeCorrectly() {
         //                     createSprite  <100>       2  (1arg)  0  end
         var compiledScript = new byte[] { 2, 128, 0, 0, 8, 2, 1, 0, 0, 8 };
         Array.Copy(compiledScript, 0, Model.RawData, 0, compiledScript.Length);

         var spriteData = new byte[] {
            /*tileTag*/ 1, 0,
            /*paletteTag*/ 2, 0,
            /*oam*/ 3, 1, 0, 8,
            /*anims*/ 4, 1, 0, 8,
            /*images*/ 5, 1, 0, 8,
            /*affineAnims*/ 6, 1, 0, 8,
            /*callback*/ 7, 1, 0, 8
         };
         Array.Copy(spriteData, 0, Model.RawData, 0x80, spriteData.Length);

         Model.ObserveRunWritten(ViewPort.CurrentChange, new PointerRun(1));
         var stream = new TableStreamRun(
            Model,
            0x80,
            new SortedSpan<int>(1),
            "[tileTag: paletteTag: oam<> anims<> images<> affineAnims<> callback<>]",
            null,
            new FixedLengthStreamStrategy(1));
         Model.ObserveRunWritten(ViewPort.CurrentChange, stream);

         var script = ViewPort.Tools.CodeTool.AnimationScriptParser.Parse(Model, 0, compiledScript.Length);
         Model.ResetChanges();

         ViewPort.Tools.CodeTool.AnimationScriptParser.Compile(ViewPort.CurrentChange, Model, 0, ref script, out var movedData);
         Assert.Contains("{", script);
         Assert.Equal(0, Model.ChangeCount);
      }

      [Fact]
      public void EventScript_SingleSpaceOnLine_NoIssue() {
         SetFullModel(0xFF);
         ViewPort.Refresh();
         var tool = ViewPort.Tools.CodeTool;
         ViewPort.Tools.CodeToolCommand.Execute();
         ViewPort.SelectionStart = new Point();
         tool.Mode = CodeMode.Script;

         var script = "Script:\n ";
         tool.Contents[0].Content = script;
         tool.Contents[0].CaretPosition = script.Length;

         // if it doesn't crash, we're good
      }

      [Fact]
      public void EventScriptThatCallsAnotherScript_ExpandInitialScript_OtherScriptPointerSourceUpdated() {
         ViewPort.Edit("@000 02 @100 02 @100 ^script`xse` ");
         var script = Script("if1 = <000000>", "end");
         ViewPort.Tools.CodeTool.Contents[0].Content = script;

         script = Script("lock", "if1 = <000000>", "end");
         ViewPort.Tools.CodeTool.Contents[0].Content = script;

         var run = Model.GetNextRun(0);
         Assert.Equal(0x100 + 3, run.PointerSources.Single());
      }

      private string Script(params string[] lines) => lines.CombineLines();
   }
}
