using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class CodeToolTests : BaseViewModelTestClass {
      private CodeTool Tool => ViewPort.Tools.CodeTool;
      private string EventScript {
         set => Tool.Contents[0].Content = value.Replace(";", Environment.NewLine);
      }

      public CodeToolTests() => SetFullModel(0xFF);

      [Fact]
      public void AddAndRemoveAnchorInSameToken_Undo_NoAnchor() {
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

      [Fact]
      public void ScriptWithCallAndLabel_Expand_NoError() {
         Tool.Mode = CodeMode.Script;
         EventScript = "call <routine>;end;routine:;end";

         // since the routine is part of the script, it doesn't need to be a separate content box
         Assert.Single(Tool.Contents);

         EventScript = "call <routine>;nop;end;routine:;end";

         Assert.False(Tool.ShowErrorText);
      }

      [Fact]
      public void ScriptWithTwoEnds_ChangeEndToNop_NoErrors() {
         Tool.Mode = CodeMode.Script;

         EventScript = "nop;end;end";
         EventScript = "nop;nop;end";

         Assert.False(Tool.ShowErrorText);
      }

      [Fact]
      public void FireRedSpecial_Decode_HasLabel() {
         SetGameCode("BPRE0");
         foreach (var meta in BaseModel.GetDefaultMetadatas("bpre")) Model.LoadMetadata(meta);
         Token.ChangeData(Model, 0, "25 9E 00 02".ToByteArray());

         var script = Tool.ScriptParser.Parse(Model, 0, 4).SplitLines()[0].Trim();

         Assert.Equal("special ChangePokemonNickname", script);
      }
   }
}
