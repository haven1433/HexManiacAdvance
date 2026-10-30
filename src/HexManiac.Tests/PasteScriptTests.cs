using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class PasteScriptTests : BaseViewModelTestClass {
      [Fact]
      public void ThumbPasteScript_Paste_Compiles() {
         ViewPort.Edit(@"
@10
.thumb
push {lr}
pop  {pc}
.end
30
");

         Assert.Equal(0xB5_00, Model.ReadMultiByteValue(0x10, 2));
         Assert.Equal(0xBD_00, Model.ReadMultiByteValue(0x12, 2));
         Assert.Equal(0x30, Model[0x14]);
      }

      [Fact]
      public void ThumbPasteScript_OddOffset_WordsAlignCorrectly() {
         ViewPort.Edit(@"
@12
.thumb
  b <12>
content:
  .word 0x08123456
.end
");

         var expectedBranchCommand = ViewPort.Tools.CodeTool.Parser.Compile(Model, 0x12, "b <12>").ReadMultiByteValue(0, 2);
         var actualBranchCommand = Model.ReadMultiByteValue(0x12, 2);
         Assert.Equal(expectedBranchCommand, actualBranchCommand);
         Assert.Equal(0x08123456, Model.ReadMultiByteValue(0x14, 4));
      }

      [Fact]
      public void Run_Paste00DirectiveWithSameFormat_NoError() {
         Array.Copy(PCSString.Convert("Hello World").ToArray(), Model.RawData, 12);
         Model.ObserveRunWritten(new ModelDelta(), new PCSRun(Model, 0, 12));
         ViewPort.Refresh();

         ViewPort.Edit("@!00(12) ^anchor\"\" ");

         Assert.Empty(Errors);
      }

      [Fact]
      public void TableWithPointer_ClearPointer_ClearAnchor() {
         ViewPort.Edit("^table[pointer<>]1 <010> ");

         ViewPort.Edit("@000 @!00(4) ");

         var run = Model.GetNextRun(0x10);
         Assert.NotEqual(0x10, run.Start);
      }
   }
}
