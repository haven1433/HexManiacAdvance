using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Code;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class ThumbRepointTests : BaseViewModelTestClass {
      CodeTool Tool => ViewPort.Tools.CodeTool;
      ThumbParser Parser => Tool.Parser;
      ModelDelta Token => ViewPort.CurrentChange;

      public ThumbRepointTests() {
         Model.ExpandData(ViewPort.CurrentChange, 0x400);
         SetFullModel(0xFF);
         Model.FreeSpaceBuffer = 0;
         Model.FreeSpaceStart = 0x100;
         ViewPort.Refresh();
      }

      private Point ToPoint(int address) => ViewPort.ConvertAddressToViewPoint(address);
      private int ToAddress(Point p) => ViewPort.ConvertViewPointToAddress(p);
      private void SelectRange(int start, int length) => (ViewPort.SelectionStart, ViewPort.SelectionEnd) = (ToPoint(start), ToPoint(start + length - 1));

      [Theory]
      [InlineData(7)]
      [InlineData(9)]
      public void RepointThumb_Not8BytesSelected_Disabled(int length) {
         SelectRange(0, length);

         Assert.False(Tool.CanRepointThumb);
      }

      [Fact]
      public void RepointThumb_NoMovCommand_Disabled() {
         Parser.Compile(Token, Model, 0, "nop", "nop", "nop", "nop");

         SelectRange(0, 8);

         Assert.False(Tool.CanRepointThumb);
      }

      [Fact]
      public void RepointThumb_Not4Commands_Disabled() {
         Parser.Compile(Token, Model, 0, "bl <100>", "bl <100>");

         SelectRange(0, 8);

         Assert.False(Tool.CanRepointThumb);
      }

      [Fact]
      public void RepointThumb_RegisterValueNeededBeforeMove_Disabled() {
         Parser.Compile(Token, Model, 0, "add r1, r0", "mov r0, #0", "nop", "nop");

         SelectRange(0, 8);

         Assert.False(Tool.CanRepointThumb);
      }

      [Theory]
      [InlineData(1)]
      [InlineData(2)]
      public void RepointThumb_NotAligned_Disabled(int start) {
         Parser.Compile(Token, Model, start, "mov r0, #0", "nop", "nop", "nop");

         SelectRange(start, 8);

         Assert.False(Tool.CanRepointThumb);
      }

      [Fact]
      public void RepointThumb_Last2BytesAreBranchLink_Disabled() {
         Parser.Compile(Token, Model, 0, "mov r0, #0", "nop", "nop", "bl <080>");

         SelectRange(0, 8);

         Assert.False(Tool.CanRepointThumb);
      }

      [Fact]
      public void RepointThumb_LdrFromPc_Disabled() {
         Parser.Compile(Token, Model, 0, "mov r0, #0", "ldr r1, [pc, <030>]", "nop", "nop");

         SelectRange(0, 8);

         Assert.False(Tool.CanRepointThumb);
      }

      [Fact]
      public void RepointThumb_NotInstruction_Disabled() {
         Parser.Compile(Token, Model, 0, "mov r0, #0", "nop", "nop"); // 4th instruction is FFFF

         SelectRange(0, 8);

         Assert.False(Tool.CanRepointThumb);
      }

      [Fact]
      public void RepointThumb_CorrectSelection_Enabled() {
         Parser.Compile(Token, Model, 0, "mov r0, #0", "nop", "nop", "nop");
         ViewPort.Tools.SelectedTool = Tool;
         var view = new StubView(Tool);

         SelectRange(0, 8);

         Assert.True(Tool.CanRepointThumb);
         Assert.Contains(nameof(Tool.CanRepointThumb), view.PropertyNotifications);
      }

      [Theory]
      [InlineData(0)]
      [InlineData(1)]
      public void RepointThumb_ValidSelection_Repoints(int register) {
         Parser.Compile(Token, Model, 0, $"mov r{register}, #0", "nop", "nop", "nop");

         SelectRange(0, 8);
         Tool.RepointThumb();

         var expectedCode = Parser.Compile(Model, 0,
            $"ldr   r{register}, [pc, <000004>]",
            $"bx r{register}",
            "000004: .word <101>"); // destination + 1
         Assert.All(Enumerable.Range(0, expectedCode.Count), i => Assert.Equal(expectedCode[i], Model[i]));
         expectedCode = Parser.Compile(Model, 0x100,
            $"ldr r{register}, [pc, <110>]",
            $"mov lr, r{register}",
            $"mov r{register}, #0",
            "nop", "nop", "nop",
            "bx lr",
            "110: .word <9>"); // destination + 1
         Assert.All(Enumerable.Range(0, expectedCode.Count), i => Assert.Equal(expectedCode[i], Model[0x100 + i]));

         Assert.Single(Messages);
         Assert.Equal(0x100, ViewPort.DataOffset);
         Assert.Equal(0x100, ToAddress(ViewPort.SelectionStart));
         Assert.Equal(0x113, ToAddress(ViewPort.SelectionEnd));
         Assert.IsType<PointerRun>(Model.GetNextRun(0x110));
      }

      // TODO allow repointing to use mov rX, rY scratch registers
      // TODO allow repointing 4+ commands (instead of exactly 4) so long as they still fit the no-branch criteria
      // TODO allow repointing starting on an odd instruction (multiple of 2 instead of multiple of 4) if enough commands are selected
      // TODO allow repointing ldr-pc commands by copying the needed pc data to the new destination
      // TODO allow repointing without a scratch register if the number of commands is enough to add a push
   }
}
