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
            "  jumpiftype 0 0 <000007>",
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
         var scriptLine = Singletons.ScriptLines.Single(line => line.LineCode.Count == 1 && line.LineCode[0] == 0x44);
         var args = scriptLine.Args;

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

      [Theory]
      [InlineData("trainerbattle 02 0x009B 0000 <2967AD> <2967D8> <1EE5D8>", "5C 02 9B 00 00 00 AD 67 29 08 D8 67 29 08 D8 E5 1E 08")]
      [InlineData("trainerbattle 00 0x009B 0000 <2967AD> <2967D8>", "5C 00 9B 00 00 00 AD 67 29 08 D8 67 29 08")]
      public void XSECompileScriptToBytes(string script, string bytes) {
         SetFullModel(0xFF);

         var actual = ViewPort.Tools.CodeTool.ScriptParser.Compile(new ModelDelta(), Model, 0, ref script, out _);

         var expected = bytes.ToByteArray();
         Assert.Equal(expected, actual);
      }

      [Fact]
      public void TextWithNamedAnchor_EditScript_KeepsAnchor() {
         SetFullModel(0xFF);
         ViewPort.Edit("@10 ^text\"\" \"Hello\" @00 02 @00 ^script`xse` 0F 00 <010> 02 ");
         ViewPort.SelectionStart = ViewPort.ConvertAddressToViewPoint(0);

         var tool = ViewPort.Tools.CodeTool.Contents[0];
         var lines = tool.Content.SplitLines();
         lines[3] = "Hello!";
         tool.Content = Environment.NewLine.Join(lines);

         Assert.Equal(0x10, Model.GetAddressFromAnchor(new ModelDelta(), -1, "text"));
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
      public void Insert00_Insert_SelectionTextMatchesSelection() {
         SetFullModel(0xFF);
         var lazyGuard = ViewPort.SelectedBytes;

         var view = new StubView(ViewPort);
         ViewPort.Edit("@!00(8) ");

         Assert.Equal(new Point(0, 0), ViewPort.SelectionStart);
         Assert.Equal(new Point(3, 0), ViewPort.SelectionEnd);
         Assert.Equal("Selected Bytes: 00 00 00 00", ViewPort.SelectedBytes.Trim());
         Assert.Contains(nameof(ViewPort.SelectedBytes), view.PropertyNotifications);
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
   delay 0x10
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
   createvisualtask <0BA7F9> 0x0A 0001 0000 0006 0000 0000
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

      [Fact]
      public void IncompleteArrayArgLine_Compile_NoException() {
         var line = "createvisualtask <0A9F11> 05";

         var parser = ViewPort.Tools.CodeTool.AnimationScriptParser;
         var bytes = parser.Compile(ViewPort.CurrentChange, Model, 0, ref line, out var _);

         Assert.Equal(new byte[] { 03, 0x11, 0x9F, 0x0A, 0x08, 0x05, 0x00 }, bytes);
      }

      [Fact]
      public void ScriptWithDataAfter_AddCommandThatRequestsPointerAndCausesRepoint_RepointAndPointerChooseDifferentOffsets() {
         SetFullModel(0xFF);
         Model.FreeSpaceBuffer = 0x10;
         ViewPort.Edit("^script`xse` 02 @04 01 @00 ");

         ViewPort.Tools.CodeTool.Contents[0].Content = Script("loadpointer 0 <??????>", "end");

         var scriptAddress = Model.GetAddressFromAnchor(ViewPort.CurrentChange, -1, "script");
         var loadAddress = Model.ReadPointer(scriptAddress + 2);
         Assert.IsType<XSERun>(Model.GetNextRun(scriptAddress));
         Assert.NotEqual(scriptAddress, loadAddress);
      }

      [Fact]
      public void ScriptWithPointer_Repoint_OldPointerFormatGone() {
         SetFullModel(0xFF);
         Model.FreeSpaceBuffer = 0x10;
         ViewPort.Edit("^script`xse` 0F 00 <text> 02 FF ^text\"\" \"text\" @00 ");

         ViewPort.Tools.CodeTool.Contents[0].Content = Script("loadpointer 0 <text>", "additem 0 0", "end");

         Assert.NotEqual(2, Model.GetNextRun(2).Start);
         var text = Model.GetNextAnchor("text");
         Assert.DoesNotContain(2, text.PointerSources);
         Assert.Contains(Model.GetNextAnchor("script").Start + 2, text.PointerSources);
      }

      [Fact]
      public void ScriptWithShop_EditShopItems_DataUpdates() {
         SetFullModel(0xFF);
         CreateTextTable("data.items.stats", 0x100, "adam", "bob", "carl", "dave", "eric", "fred");
         ViewPort.Edit("@00 ^script`xse` ");
         Assert.Equal(ViewPort.Tools.SelectedTool, ViewPort.Tools.CodeTool);

         ViewPort.Tools.CodeTool.Contents[0].Content = "pokemart <000080>";
         ViewPort.Tools.CodeTool.Contents[0].Content = Script("pokemart <000080>", "{", "carl", "}");

         Assert.Equal(2, Model.ReadMultiByteValue(0x80, 2));
      }

      [Fact]
      public void MoveEffectTable_CreateNew_NoCrash() {
         ViewPort.Edit("^table[content<`bse`>]1 ");

         var streamElement = (StreamElementViewModel)ViewPort.Tools.TableTool.Children.Single(child => child is StreamElementViewModel);
         streamElement.CreateNew.Execute();

         // no assert -> test pass
      }

      [Fact]
      public void AnimationScriptTable_CreateNew_NoCrash() {
         ViewPort.Edit("^table[content<`ase`>]1 ");

         var streamElement = (StreamElementViewModel)ViewPort.Tools.TableTool.Children.Single(child => child is StreamElementViewModel);
         streamElement.CreateNew.Execute();

         // no assert -> test pass
      }

      [Fact]
      public void EventScriptWithText_ExpandTextSoItMoves_ScriptPointerUpdates() {
         SetFullModel(0xFF);
         ViewPort.Edit("^script`xse` 0F 00 <100> 02 @100 ^text\"\" \"short\" 07 @00 ");

         var tool = ViewPort.Tools.CodeTool.Contents[0];
         var lines = tool.Content.SplitLines(); // label, loadpointer, curly, text, curly, end
         lines[3] = "longer";
         tool.Content = Script(lines);

         var destination = Model.ReadPointer(2);
         Assert.Equal(destination, Model.GetNextRun(destination).Start);
         Assert.Equal("\"longer\"", PCSString.Convert(Model, destination, 10));
         Assert.Equal("text", Model.GetAnchorFromAddress(-1, destination));
      }

      [Fact]
      public void ScriptWithNoInfoAnchor_ExtendScript_NoRepoint() {
         ViewPort.Edit("<100> @100 ^script 6A 02 @100 ");
         ViewPort.Tools.CodeToolCommand.Execute();
         ViewPort.Tools.CodeTool.Mode = CodeMode.Script;

         var tool = ViewPort.Tools.CodeTool.Contents[0];
         var lines = tool.Content.SplitLines().ToList();
         lines.Insert(0, "faceplayer");
         tool.Content = Script(lines.ToArray());

         Assert.Equal(0x100, Model.GetNextAnchor("script").Start);
         Assert.Equal(0x5A, Model[0x100]);
      }

      [Fact]
      public void EditScript_ErrorInScript_ShowError() {
         SetFullModel(0xFF);
         ViewPort.Tools.CodeToolCommand.Execute();
         ViewPort.Tools.CodeTool.Mode = CodeMode.Script;
         ViewPort.Tools.CodeTool.Contents[0].Content = Script("lock", "end", "lock");

         ViewPort.SelectionStart = new Point(0, 1);
         ViewPort.SelectionStart = new Point(0, 0);
         ViewPort.Tools.CodeTool.Contents[0].Content = Script("lock", "lock", "end");

         Assert.True(ViewPort.Tools.CodeTool.ShowErrorText);
      }

      [Fact]
      public void ErrorInScript_ClickOff_ResetScriptEditor() {
         SetFullModel(0xFF);
         ViewPort.Tools.CodeToolCommand.Execute();
         ViewPort.Tools.CodeTool.Mode = CodeMode.Script;
         ViewPort.Tools.CodeTool.Contents[0].Content = Script("lock", "end", "lock");
         ViewPort.SelectionStart = new Point(0, 1);
         ViewPort.SelectionStart = new Point(0, 0);
         ViewPort.Tools.CodeTool.Contents[0].Content = Script("lock", "lock", "end");

         ViewPort.SelectionStart = new Point(0, 1);
         ViewPort.SelectionStart = new Point(0, 0);

         var script = ViewPort.Tools.CodeTool.Contents[0].Content.SplitLines()
            .Select(line => line.Trim()).Where(line => line.Length > 0).ToArray();
         Assert.Equal(new[] { "lock", "end" }, script);
         Assert.False(ViewPort.Tools.CodeTool.ShowErrorText);
      }

      [Fact]
      public void StartScriptOnOddHalfWord_LoadRegister_ConstantIsByteAligned() {
         SetFullModel(0xFF);

         ViewPort.Tools.CodeTool.Parser.Compile(Token, Model, 2,
            "ldr   r0,  =0x12345678",
            "add   r0, #1",
            "bx    r0"
            );

         Assert.Equal(0x12345678, Model.ReadMultiByteValue(8, 4));
      }

      [Fact]
      public void Anchor_WriteThumb_KeepAnchor() {
         SetFullModel(0xFF);
         ViewPort.Edit("@04 ^anchor ");

         ViewPort.Tools.CodeTool.Content = "nop";

         Assert.Equal(0, Model.ReadMultiByteValue(4, 2));
         Assert.Equal("anchor", Model.GetAnchorFromAddress(-1, 4));
      }

      [Fact]
      public void FireRedScriptWithChange_Parse_NoThrow() {
         var data = "5A 16 01 40 85 00 79 85 00 19 3D 01 FF FF FF FF FF FF FF FF FF 21 0D 80 00 00 02"
            .Split(' ').Select(t => byte.Parse(t, System.Globalization.NumberStyles.HexNumber)).ToArray();
         Array.Copy(data, Model.RawData, data.Length);
         var parser = ViewPort.Tools.CodeTool.ScriptParser;

         var length = parser.FindLength(Model, 0);
         var text = parser.Parse(Model, 0, length);

         // no throw -> pass
      }

      [Fact]
      public void AnimationScript_ContainsArgList_WriteDataCorrectly() {
         // 03 createvisualtask address<> priority. [argv:]
         SetFullModel(0xFF);
         var script = Script("createvisualtask <123456> 5 0x0 0x0", "end");

         var result = ViewPort.Tools.CodeTool.AnimationScriptParser.Compile(Token, Model, 0, ref script, out var _);

         byte[] expected = new byte[][] {
            new byte[] { 3 },                      // createvisualtask
            new byte[] { 0x56, 0x34, 0x12, 0x08 }, // address
            new byte[] { 5 },                      // priority
            new byte[] { 2 },                      // (number of variable args)
            new byte[] { 0, 0, 0,0 },              // variable args (each arg is 2 bytes)
            new byte[] { 8 },                      // end
         }.SelectMany(b => b).ToArray();
         Assert.Equal(expected, result);
      }

      [Fact]
      public void TrainerBattle_WrongNumberOfArguments_ErrorMessageIncludesTrainerBattleTypeInErrorMessage() {
         ViewPort.Tools.CodeTool.Mode = CodeMode.Script;

         ViewPort.Tools.CodeTool.Contents[0].Content = "trainerbattle 00";

         var message = "0: Command trainerbattle 00 expects 4 arguments, but received 0 instead.";
         Assert.Equal(ViewPort.Tools.CodeTool.ErrorText.Trim(), message);
      }

      [Fact]
      public void Many00AndEnd_DecompileAnimation_GetManyLoadSpriteGfx() {
         Model[18] = 8;

         var length = ViewPort.Tools.CodeTool.AnimationScriptParser.FindLength(Model, 0);
         var content = ViewPort.Tools.CodeTool.AnimationScriptParser.Parse(Model, 0, length).SplitLines();

         Assert.Equal(8, content.Length);
      }

      [Fact]
      public void CreateSpritePointingToNamedAnchor_ChangePointer_NamedAnchorRemains() {
         SetFullModel(0xFF);
         ViewPort.Tools.CodeToolCommand.Execute();
         ViewPort.Tools.CodeTool.Mode = CodeMode.AnimationScript;
         ViewPort.Tools.CodeTool.Contents[0].Content = "createsprite <100> 0";
         ViewPort.Edit("@100 ^somename @000 ");
         ViewPort.Tools.CodeToolCommand.Execute();

         var lines = ViewPort.Tools.CodeTool.Contents[0].Content.SplitLines();
         lines[0] = "createsprite <180> 0";
         ViewPort.Tools.CodeTool.Contents[0].Content = Environment.NewLine.Join(lines);

         lines[0] = "createsprite <100> 0";
         ViewPort.Tools.CodeTool.Contents[0].Content = Environment.NewLine.Join(lines);

         Assert.Equal("somename", Model.GetAnchorFromAddress(-1, 0x100));
      }

      [Fact]
      public void ScriptWithPokemonArgument_GiveVariableAsArgument_WriteVariableToRom() {
         SetFullModel(0xFF);

         var script = Script("showpokepic 0x8004 0 0", "end");
         var code = ViewPort.Tools.CodeTool.ScriptParser.Compile(Token, Model, 0, ref script, out var _);

         Assert.Equal(0x8004, code.ReadMultiByteValue(1, 2));
      }

      [Fact]
      public void ScriptCommandWithFillerArgs_Parse_FillerArgsNotShown() {
         Model[0] = 0x79;  // givePokemon
         Model[15] = 0x02; // end

         var script = ViewPort.Tools.CodeTool.ScriptParser.Parse(Model, 0, 16).SplitLines();

         Assert.Equal("givePokemon 0 0 0", script[0].Trim());
      }

      [Fact]
      public void TablePointsToScript_EditThenUndo_NoNewAnchors() {
         ViewPort.Edit("<020> @00 ^table[pointer<`xse`>]1 ");

         ViewPort.Edit("<030> ");
         ViewPort.Undo.Execute();

         var run = Model.GetNextRun(0x30);
         Assert.Equal(int.MaxValue, run.Start);
      }

      private string Script(params string[] lines) => lines.CombineLines();
   }
}
