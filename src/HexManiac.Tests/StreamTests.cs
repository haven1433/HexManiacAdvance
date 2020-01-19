using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class StreamTests : BaseViewModelTestClass {
      [Fact]
      public void ParseErrorInPlmRunDeserializeGetsSkipped() {
         CreateTextTable(HardcodeTablesModel.MoveNamesTable, 0, "A", "B", "\"C C\"", "D");
         Model.WriteMultiByteValue(0x40, 2, new ModelDelta(), 0xFFFF);
         ViewPort.Edit("@40 ^bob`plm` 5 a 6 b 7 c 8 d ");

         ViewPort.Tools.StringTool.Content = @"
5 a
6 b
7""c c""
8 d
";

         // Assert that the run length is still 8 (was 10).
         Assert.Equal(8, Model.GetNextRun(0x40).Length);
      }

      [Fact]
      public void ShortenPlmRunClearsExtraUnusedBytes() {
         CreateTextTable(HardcodeTablesModel.MoveNamesTable, 0, "A", "B", "\"C C\"", "D");
         Model.WriteMultiByteValue(0x40, 2, new ModelDelta(), 0xFFFF);
         ViewPort.Edit("@40 ^bob`plm` 5 a 6 b 7 c 8 d ");

         ViewPort.Tools.StringTool.Content = @"
5 a
8 d
";

         // assert that bytes 7/8 are FF
         Assert.Equal(0xFFFF, Model.ReadMultiByteValue(0x46, 2));
      }

      [Fact]
      public void CanCopyPlmStream() {
         var fileSystem = new StubFileSystem();
         CreateTextTable(HardcodeTablesModel.MoveNamesTable, 0x100, "Punch", "Kick", "Bite", "Snarl", "Smile", "Cry");
         ViewPort.Edit("@00 FFFF @00 ^someMoves`plm` 3 Punch 5 Kick 7 Bite 11 Snarl ");

         ViewPort.SelectionStart = new Point(2, 0);
         ViewPort.SelectionEnd = new Point(7, 0);
         ViewPort.Copy.Execute(fileSystem);

         Assert.Equal("5 Kick 7 Bite 11 Snarl", fileSystem.CopyText);
      }

      [Fact]
      public void CanShortenPlmStream() {
         var fileSystem = new StubFileSystem();
         CreateTextTable(HardcodeTablesModel.MoveNamesTable, 0x100, "Punch", "Kick", "Bite", "Snarl", "Smile", "Cry");
         ViewPort.Edit("@00 FFFF @00 ^someMoves`plm` 3 Punch 5 Kick 7 Bite 11 Snarl ");

         ViewPort.Edit("@04 []");

         Assert.Equal(6, Model.GetNextRun(0).Length);
         Assert.Equal(new Point(6, 0), ViewPort.SelectionStart);
      }
   }
}
