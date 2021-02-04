using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using System;
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

      [Fact]
      public void ViewPort_PutMetacommand_DataChangesButSelectionDoesNot() {
         ViewPort.Edit("@!put(1234) ");

         Assert.Equal(new Point(0, 0), ViewPort.SelectionStart);
         Assert.Equal(0x12, Model[0]);
         Assert.Equal(0x34, Model[1]);
      }

      [Fact]
      public void StreamWithCustomEnd_CutPaste_DataUpdates() {
         SetFullModel(0xFF);
         var fileSystem = new StubFileSystem();
         Array.Copy(new byte[] { 2, 2, 2, 3, 3, 3, 0xFF, 0xFF, 0x00 }, Model.RawData, 9);
         ViewPort.Edit("^table[a. b. c.]!FFFF00 ");

         // cut
         ViewPort.SelectionStart = ViewPort.ConvertAddressToViewPoint(0);
         ViewPort.ExpandSelection(0, 0);
         ViewPort.Copy.Execute(fileSystem);
         ViewPort.Clear.Execute();

         // paste
         ViewPort.Goto.Execute(0x10);
         ViewPort.Edit(fileSystem.CopyText);

         var table = Model.GetTable("table");
         Assert.Equal(0x10, table.Start);
         Assert.Equal(2, table.ElementCount);
         Assert.Equal(3, table.ElementLength);
         Assert.Equal(9, table.Length);
      }

      [Fact]
      public void NonEmptyData_MetaCommandFillZeros_RaiseErrorAndStopWriting() {
         ViewPort.Edit("@!00(10) 22 ");

         Assert.Single(Errors);
         Assert.Equal(0x00, Model[0]);
      }

      [Fact]
      public void StreamWithEnum_RequestAutocompleteAtEnum_GetOptions() {
         Model.SetList("options", new[] { "Xmatch", "matchX", "matXch", "other" });
         var stream = new TableStreamRun(Model, 0, SortedSpan<int>.None, "[a:options b:]", null, new FixedLengthStreamStrategy(2));

         var options = stream.GetAutoCompleteOptions("match, 3", caretLineIndex: 0, caretCharacterIndex: 5).Select(option => option.Text).ToArray();

         Assert.Equal(new[] { "Xmatch", "matchX", "matXch" }, options);
      }

      [Fact]
      public void TableStreamRunAutoCompleteOption_Execute_TextChanges() {
         Model.SetList("options", new[] { "Xmatch", "matchX", "matXch", "other" });
         var stream = new TableStreamRun(Model, 0, SortedSpan<int>.None, "[a:options b:]", null, new FixedLengthStreamStrategy(2));

         var options = stream.GetAutoCompleteOptions("match, 3", caretLineIndex: 0, caretCharacterIndex: 5).ToArray();

         Assert.Equal("matchX, 3", options[1].LineText);
      }

      [Fact]
      public void TableStreamRunAutoCompleteOption_MoreElementsNeededOnLine_MovesToNextElement() {
         Model.SetList("options", new[] { "Xmatch", "matchX", "matXch", "other" });
         var stream = new TableStreamRun(Model, 0, SortedSpan<int>.None, "[a:options b:]", null, new FixedLengthStreamStrategy(2));

         var options = stream.GetAutoCompleteOptions("match", caretLineIndex: 0, caretCharacterIndex: 5).ToArray();

         Assert.Equal("matXch, ", options[2].LineText);
      }

      [Fact]
      public void SingleElementTableStreamRun_AutoCompleteField_OptionsMakeSense() {
         Model.SetList("options", new[] { "Xmatch", "matchX", "matXch", "other" });
         var stream = new TableStreamRun(Model, 0, SortedSpan<int>.None, "[abc: xyz:options]", null, new FixedLengthStreamStrategy(1));

         var options = stream.GetAutoCompleteOptions("xyz: match", caretLineIndex: 0, caretCharacterIndex: 10).ToArray();

         Assert.Equal("xyz: matchX", options[1].LineText);
      }

      [Fact]
      public void TupleInTableStream_AutoCompleteField_OptionCompletesSubContent() {
         Model.SetList("options", new[] { "Xmatch", "matchX", "matXch", "other" });
         var stream = new TableStreamRun(Model, 0, SortedSpan<int>.None, "[abc:|t|i:options|j:options]", null, new FixedLengthStreamStrategy(2));

         var options = stream.GetAutoCompleteOptions("match", 0, 5).ToArray();

         Assert.Equal("Xmatch", options[0].Text);
         Assert.Equal("(Xmatch ", options[0].LineText);
      }

      [Fact]
      public void TupleInTableStream_AutoCompleteSecondField_OptionCompletesSubContent() {
         Model.SetList("options", new[] { "Xmatch", "matchX", "matXch", "other" });
         var stream = new TableStreamRun(Model, 0, SortedSpan<int>.None, "[abc:|t|i:options|j:options]", null, new FixedLengthStreamStrategy(2));

         var options = stream.GetAutoCompleteOptions("(Xmatch match", 0, 13).ToArray();

         Assert.Equal("Xmatch", options[0].Text);
         Assert.Equal("(Xmatch Xmatch)", options[0].LineText);
      }

      // TODO test what happens when you try to run autocomplete for a tuple segment with more elements than expected
      // TODO autocomplete for true/false tuple elements
      // TODO test how it works with tuples (single element format)
      // TODO test how it works with true/false tuples (single element format)

      // TODO test how it works for egg moves (pokemon)
      // TODO test how it works for egg moves (moves)
      // TODO test how it works for trainer teams (pokemon)
      // TODO test how it works for trainer teams (moves)
      // TODO test how it works for trainer teams (items)
      // TODO test how it works for item effects (elements)
      // TODO test how it works for item effects (enums)
   }
}
