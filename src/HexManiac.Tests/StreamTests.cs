using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using System.Linq;
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

         Assert.Equal("5 Kick, 7 Bite, 11 Snarl,", fileSystem.CopyText);
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

      [Fact]
      public void ExtendingTableStreamRepoints() {
         ViewPort.Edit("00 01 02 03 FF ^bob CC @00 ^table[value.]!FF ");
         ViewPort.Tools.SelectedIndex = ViewPort.Tools.IndexOf(ViewPort.Tools.StringTool);
         Assert.Equal(@"0
1
2
3", ViewPort.Tools.StringTool.Content);

         ViewPort.Tools.StringTool.Content = @"0
1
2
3
4";

         Assert.NotEmpty(Messages);
         var anchorAddress = Model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, "table");
         Assert.NotEqual(0, anchorAddress);
      }

      [Fact]
      public void CanDeepCopy() {
         ViewPort.Edit("^table[pointer<\"\">]1 @{ Hello World!\" @} @00 ");

         var fileSystem = new StubFileSystem();
         var menuItem = ViewPort.GetContextMenuItems(ViewPort.SelectionStart).Single(item => item.Text == "Deep Copy");
         menuItem.Command.Execute(fileSystem);

         Assert.Equal(@"@!00(4) ^table[pointer<"""">]1 #""Hello World!""#, @{ ""Hello World!"" @}", fileSystem.CopyText);
      }

      [Fact]
      public void CanCopyLvlMovesData() {
         CreateTextTable(HardcodeTablesModel.PokemonNameTable, 0x100, "Adam", "Bob", "Carl", "Dave");
         CreateTextTable(HardcodeTablesModel.MoveNamesTable, 0x180, "Ate", "Bumped", "Crossed", "Dropped");
         ViewPort.Edit("@00 FF FF @00 ^table`plm` 3 Ate 4 Bumped 5 Crossed @00 ");
         var content = Model.Copy(() => ViewPort.CurrentChange, 0, 8);
         Assert.Equal(@"^table`plm` 3 Ate, 4 Bumped, 5 Crossed, []", content);
      }

      [Fact]
      public void StreamRunSerializeDeserializeIsSymmetric() {
         CreateTextTable(HardcodeTablesModel.PokemonNameTable, 0x100, "Adam", "Bob", "Carl", "Dave");

         ViewPort.Edit($"@00 00 00 01 01 02 02 03 03 FF FF @00 ^table[enum.{HardcodeTablesModel.PokemonNameTable} content.]!FFFF ");
         var stream = (IStreamRun)Model.GetNextRun(0);

         var text = stream.SerializeRun();
         Model.ObserveRunWritten(ViewPort.CurrentChange, stream.DeserializeRun(text, ViewPort.CurrentChange));

         var result = new byte[] { 0, 0, 1, 1, 2, 2, 3, 3, 255, 255 };
         Assert.All(result.Length.Range(), i => Assert.Equal(Model[i], result[i]));
      }
   }
}
