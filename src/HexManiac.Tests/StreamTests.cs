using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
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

         ViewPort.Tools.StringTool.Content = SplitLines(@"5 a;6 b;7""c c"";8 d");

         // Assert that the run length is still 8 (was 10).
         Assert.Equal(8, Model.GetNextRun(0x40).Length);
      }

      [Fact]
      public void ShortenPlmRunClearsExtraUnusedBytes() {
         CreateTextTable(HardcodeTablesModel.MoveNamesTable, 0, "A", "B", "\"C C\"", "D");
         Model.WriteMultiByteValue(0x40, 2, new ModelDelta(), 0xFFFF);
         ViewPort.Edit("@40 ^bob`plm` 5 a 6 b 7 c 8 d ");

         ViewPort.Tools.StringTool.Content = SplitLines("5 a;8 d");

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
         Assert.Equal(SplitLines("0;1;2;3"), ViewPort.Tools.StringTool.Content);

         ViewPort.Tools.StringTool.Content = SplitLines("0;1;2;3;4");

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

         Assert.Equal(new[] { "matchX", "matXch", "Xmatch" }, options); // results are sorted
      }

      [Fact]
      public void TableStreamRunAutoCompleteOption_Execute_TextChanges() {
         Model.SetList("options", new[] { "Xmatch", "matchX", "matXch", "other" });
         var stream = new TableStreamRun(Model, 0, SortedSpan<int>.None, "[a:options b:]", null, new FixedLengthStreamStrategy(2));

         var options = stream.GetAutoCompleteOptions("match, 3", caretLineIndex: 0, caretCharacterIndex: 5).ToArray();

         Assert.Equal("matchX, 3", options[0].LineText);
      }

      [Fact]
      public void TableStreamRunAutoCompleteOption_MoreElementsNeededOnLine_MovesToNextElement() {
         Model.SetList("options", new[] { "Xmatch", "matchX", "matXch", "other" });
         var stream = new TableStreamRun(Model, 0, SortedSpan<int>.None, "[a:options b:]", null, new FixedLengthStreamStrategy(2));

         var options = stream.GetAutoCompleteOptions("match", caretLineIndex: 0, caretCharacterIndex: 5).ToArray();

         Assert.Equal("Xmatch, ", options[2].LineText);
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

         Assert.Equal("matchX", options[0].Text);
         Assert.Equal("(matchX ", options[0].LineText);
      }

      [Fact]
      public void TupleInTableStream_AutoCompleteSecondField_OptionCompletesSubContent() {
         Model.SetList("options", new[] { "Xmatch", "matchX", "matXch", "other" });
         var stream = new TableStreamRun(Model, 0, SortedSpan<int>.None, "[abc:|t|i:options|j:options]", null, new FixedLengthStreamStrategy(2));

         var options = stream.GetAutoCompleteOptions("(Xmatch match", 0, 13).ToArray();

         Assert.Equal("matchX", options[0].Text);
         Assert.Equal("(Xmatch matchX)", options[0].LineText);
      }

      [Fact]
      public void TupleInTableStream_AutoCompleteExtraField_NoOptions() {
         Model.SetList("options", new[] { "Xmatch", "matchX", "matXch", "other" });
         var stream = new TableStreamRun(Model, 0, SortedSpan<int>.None, "[abc:|t|i:options|j:options]", null, new FixedLengthStreamStrategy(2));

         var options = stream.GetAutoCompleteOptions("(Xmatch match ma", 0, 16).ToArray();

         Assert.Empty(options);
      }

      [Fact]
      public void TupleInTableStream_AutoCompleteBoolean_BooleanOptions() {
         var stream = new TableStreamRun(Model, 0, SortedSpan<int>.None, "[abc.|t|i:|j.]", null, new FixedLengthStreamStrategy(2));

         var options = stream.GetAutoCompleteOptions("(2 e", 0, 4).ToArray();

         Assert.Equal(2, options.Length);
         Assert.Equal("false", options[0].Text);
         Assert.Equal("(2 true)", options[1].LineText);
      }

      [Theory]
      [InlineData("abc: match", "Xmatch", "abc: (Xmatch ")]
      [InlineData("abc: (Xmatch match", "Xmatch", "abc: (Xmatch Xmatch ")]
      [InlineData("abc: (Xmatch Xmatch tr", "true", "abc: (Xmatch Xmatch true)")]
      public void TupleInSingleElementTableStream_AutoCompleteInitialField_OptionsMatch(string inputLine, string outputText, string outputLine) {
         Model.SetList("options", new[] { "Xmatch", "matchX", "matXch", "other" });
         var stream = new TableStreamRun(Model, 0, SortedSpan<int>.None, "[abc.|t|i:options|j:options|k.]", null, new FixedLengthStreamStrategy(1));

         var options = stream.GetAutoCompleteOptions(inputLine, 0, inputLine.Length).ToArray();

         Assert.Equal(outputText, options[0].Text);
         Assert.Equal(outputLine, options[0].LineText);
      }

      [Fact]
      public void OptionsWithNoQuote_StartTupleWithLeadingQuote_StillFindAutocompleteOption() {
         Model.SetList("options", new[] { "PoisonPowder", "\"Poison Gas\"", "other" });
         var stream = new TableStreamRun(Model, 0, SortedSpan<int>.None, "[abc.|t|i::options|j::]", null, new FixedLengthStreamStrategy(2));

         var options = stream.GetAutoCompleteOptions("(\"Poison", 0, 8).ToArray();

         Assert.Equal(2, options.Length);
         Assert.Equal("PoisonPowder", options[0].Text);
         Assert.Equal("\"Poison Gas\"", options[1].Text);
      }

      [Fact]
      public void OptionsWithNoQuote_StartEnumWithLeadingQuote_StillFindAutocompleteOption() {
         Model.SetList("options", new[] { "PoisonPowder", "\"Poison Gas\"", "other" });
         var stream = new TableStreamRun(Model, 0, SortedSpan<int>.None, "[abc.options]", null, new FixedLengthStreamStrategy(2));

         var options = stream.GetAutoCompleteOptions("\"Poison", 0, 7).ToArray();

         Assert.Equal(2, options.Length);
         Assert.Equal("PoisonPowder", options[0].Text);
         Assert.Equal("\"Poison Gas\"", options[1].Text);
      }

      [Fact]
      public void ParenthesisAndTupleElementWithQuotes_Autocomplete_OptionsAreFiltered() {
         Model.SetList("options", new[] { "\"Poison Gas\"", "\"Poison Sting\"", "other" });
         var stream = new TableStreamRun(Model, 0, SortedSpan<int>.None, "[abc.|t|a:options|b:]", null, new FixedLengthStreamStrategy(2));

         var options = stream.GetAutoCompleteOptions("(\"Poison sti 4)", 0, 12).ToArray();

         Assert.Single(options);
         Assert.Equal("\"Poison Sting\"", options[0].Text);
         Assert.Equal("(\"Poison Sting\" 4)", options[0].LineText);
      }

      [Fact]
      public void SingleElementStream_AutocompleteWithExtraWhitespaceBetweenElements_Works() {
         Model.SetList("options", new[] { "\"Poison Gas\"", "\"Poison Sting\"", "other" });
         var stream = new TableStreamRun(Model, 0, SortedSpan<int>.None, "[abc:options xyz:]", null, new FixedLengthStreamStrategy(1));

         var options = stream.GetAutoCompleteOptions("abc:     Poison", 0, 15).ToArray();

         Assert.Equal("\"Poison Gas\"", options[0].Text);
         Assert.Equal("\"Poison Sting\"", options[1].Text);
      }

      [Fact]
      public void SingleElementStream_AutocompleteWithLeadingWhitespace_Works() {
         Model.SetList("options", new[] { "\"Poison Gas\"", "\"Poison Sting\"", "other" });
         var stream = new TableStreamRun(Model, 0, SortedSpan<int>.None, "[abc:options xyz:]", null, new FixedLengthStreamStrategy(1));

         var options = stream.GetAutoCompleteOptions("  abc: Poison", 0, 13).ToArray();

         Assert.Equal("\"Poison Gas\"", options[0].Text);
         Assert.Equal("\"Poison Sting\"", options[1].Text);
      }

      [Fact]
      public void StreamElementViewModel_CallAutocomplete_ZIndexChanges() {
         Model.SetList("options", new[] { "PoisonPowder", "\"Poison Gas\"", "other" });
         Model.WritePointer(ViewPort.CurrentChange, 0x100, 0);
         var stream = new TableStreamRun(Model, 0, SortedSpan<int>.None, "[abc.options]", null, new FixedLengthStreamStrategy(2));
         Model.ObserveRunWritten(new NoDataChangeDeltaModel(), stream);
         var vm = new TextStreamElementViewModel(ViewPort, default, 0x100, stream.FormatString);
         var view = new StubView(vm);
         Assert.Equal(0, vm.ZIndex);

         vm.GetAutoCompleteOptions("o", 0, 0);
         Assert.Equal(1, vm.ZIndex);

         vm.ClearAutocomplete();
         Assert.Equal(0, vm.ZIndex);
         Assert.Equal(2, view.PropertyNotifications.Count(pname => pname == nameof(vm.ZIndex)));
      }

      [Fact]
      public void StreamElementViewModel_AutocompleteWithNoCompletion_ZIndexDoesNotChange() {
         Model.SetList("options", new[] { "PoisonPowder", "\"Poison Gas\"", "other" });
         Model.WritePointer(ViewPort.CurrentChange, 0x100, 0);
         var stream = new TableStreamRun(Model, 0, SortedSpan<int>.None, "[abc.options]", null, new FixedLengthStreamStrategy(2));
         Model.ObserveRunWritten(new NoDataChangeDeltaModel(), stream);
         var vm = new TextStreamElementViewModel(ViewPort, default, 0x100, stream.FormatString);

         vm.GetAutoCompleteOptions("xzy", 0, 3);

         Assert.Equal(0, vm.ZIndex);
      }

      [Fact]
      public void SingleElementStreamRunWithTuple_Serialize_TupleAppearsAsTuple() {
         var stream = new TableStreamRun(Model, 0, SortedSpan<int>.None, "[index::|t|:.|i::::::. unknown:|h unused:|h]", null, new FixedLengthStreamStrategy(1));

         var lines = stream.SerializeRun().SplitLines();

         var tokenLines = lines.Select(line => line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
         lines = tokenLines.Select(tokens => " ".Join(tokens)).ToArray();
         Assert.Equal("index: (0)", lines[0]);
         Assert.Equal("unknown: 0x0000", lines[1]);
         Assert.Equal("unused: 0x0000", lines[2]);
      }

      [Fact]
      public void TupleSkip3Bits_Write23_Write184() {
         SetFullModel(0xFF);
         var stream = new TableStreamRun(Model, 0, SortedSpan<int>.None, "[index::|t|:.|i::::::.]", null, new FixedLengthStreamStrategy(2));
         Model.ObserveRunWritten(ViewPort.CurrentChange, stream);
         ViewPort.Refresh();

         ViewPort.Edit("23 ");

         Assert.Equal(23 * 8, Model[0]);
         Assert.Equal(0, Model[1]);
         Assert.Equal(0, Model[2]);
         Assert.Equal(0, Model[3]);
      }

      [Fact]
      public void TableWithPointerToStream_StreamContainsOffsetEnumSegment_ChildStreamsAdded() {
         CreateTextTable("names", 0x100, "adam", "bob", "carl");
         Model[0] = 7;       // 0x0:  carl + 5
         Model[0x13] = 0x08; // 0x10: <000>

         ViewPort.Edit("@10 ^table[sub<[element:names+5]1>]1 ");

         var childTable = (TableStreamRun)Model.GetNextRun(0);
         var enumSegment = (ArrayRunEnumSegment)childTable.ElementContent[0];
         Assert.Equal(5, enumSegment.ValueOffset);

         var cell = (Pointer)Model.GetNextRun(0x11).CreateDataFormat(Model, 0x11);
         Assert.False(cell.HasError);
      }

      [Fact]
      public void TableStream_CreateChildTab_StreamIsOneLine() {
         ViewPort.Edit("@0A FF @06 ^table[data.]!FF ");

         var child = ViewPort.CreateChildView(7, 7);

         Assert.Equal(5, child.Width);
         Assert.Equal(6, child.DataOffset);
         Assert.Equal(1, child.Height);
         Assert.True(child.IsSelected(new Point(1, 0)));
      }

      [Fact]
      public void DynamicStreamLength_AddFormat_ChildrenHaveDifferentLengths() {
         var token = new ModelDelta();
         ViewPort.Edit("@000 <010> <020> <030> ");
         ViewPort.Edit("@010 03 00 05 00 02 00 01 00 00 00 04 00 05 00 03 00 "); // table A buts up against table 2 (8 elements)
         ViewPort.Edit("@020 03 00 05 00 02 00 01 00 FF FF FF FF FF FF FF FF "); // table B ends short (4 elements)
         ViewPort.Edit("@030 03 00 05 00 02 00 01 00 00 00 ^conflict ");         // table C ends at another anchor (5 elements)
         CreateTextTable("names", 0x100, "adam", "bob", "carl", "dave", "eric", "fred", "gary");

         ViewPort.Edit("@000 ^table[child<[entry:names]?>]3 "); // create a table where each entry is a pointer to a table of unknown length

         var table = Model.GetTable("table");
         var childA = Model.GetNextRun(table.ReadPointer(Model, 0)) as ITableRun;
         var childB = Model.GetNextRun(table.ReadPointer(Model, 1)) as ITableRun;
         var childC = Model.GetNextRun(table.ReadPointer(Model, 2)) as ITableRun;

         Assert.Equal(8, childA.ElementCount);
         Assert.Equal(4, childB.ElementCount);
         Assert.Equal(5, childC.ElementCount);
      }

      [Fact]
      public void TableStreamRun_GetAutoCompleteOptions_OptionsContainSpaces() {
         Model.SetList("names", new[] { "adam", "bob", "carl", "dave" });
         var stream = new TableStreamRun(Model, 0, SortedSpan<int>.None, "[a: b: c::names]", null, new FixedLengthStreamStrategy(2));

         var options = stream.GetAutoCompleteOptions("1, 2, a", 0, 7);

         Assert.Equal(3, options.Count);
         Assert.Equal("1, 2, adam", options[0].LineText);
         Assert.Equal("1, 2, carl", options[1].LineText);
         Assert.Equal("1, 2, dave", options[2].LineText);
      }

      [Fact]
      public void TableWithEnumAndNumber_InputTuple_ParsesData() {
         Model.SetList("list", new[] { "adam", "bob", "carl", "dave" });
         var table = new TableStreamRun(Model, 0, SortedSpan<int>.None, "[a:list b:]", default, new FixedLengthStreamStrategy(2));

         table.DeserializeRun(Environment.NewLine.Join(new[] { "(bob 3)", "(carl 4)" }), ViewPort.CurrentChange);

         Assert.Equal(1, table.ReadValue(Model, 0, "a"));
         Assert.Equal(3, table.ReadValue(Model, 0, "b"));
         Assert.Equal(2, table.ReadValue(Model, 1, "a"));
         Assert.Equal(4, table.ReadValue(Model, 1, "b"));
      }

      [Fact]
      public void Stream_Copy_IncludeEndToken() {
         SetFullModel(0xFF);
         ViewPort.Edit("^table[data:]!FFFF +13 +25 ");

         ViewPort.SelectionStart = new Point(0, 0);
         ViewPort.MoveSelectionEnd.Execute(Direction.End);
         ViewPort.Copy.Execute(FileSystem);

         Assert.Contains("[]", FileSystem.CopyText.value);
      }

      [Fact]
      public void StreamWithLengthFromParentWithNonFreeSpaceAfter_ChangeLengthInParent_RepointChildStream() {
         SetFullModel(0xFF);
         Model.WritePointer(Token, 0, 0x100);                 // 000: <100> 2
         Model.WriteMultiByteValue(4, 4, Token, 2);
         Model.WriteMultiByteValue(0x108, 4, Token, 0xDEAD);  // 108: 00 00 00 00
         ViewPort.Edit("^table[ptr<[a: b:]/count> count::]1 ");
         ViewPort.Refresh();

         // editing the 'count' parameter should try to expand the child table, which should cause a repoint
         ViewPort.Edit("@004 3 ");

         Assert.NotEqual(0x100, Model.ReadPointer(0));
         Assert.Single(Messages);
      }

      [Fact]
      public void TableStream_Append_CopyLastElement() {
         SetFullModel(0xFF);
         Token.ChangeData(Model, 0, new byte[8]);
         ViewPort.Edit("^parent[ptr<[pointer<> data::]/count> count::]1 ");

         ViewPort.Edit("@{ <100> 4 +");

         var table = new ModelTable(Model, Model.ReadPointer(0));
         Assert.Equal(0x100, table[1].GetAddress("pointer"));
         Assert.Equal(4, table[1].GetValue("data"));
      }

      [Fact]
      public void TableStreamRun_DeserializeNewPointerToText_NewTextRunAdded() {
         SetFullModel(0xFF);
         Token.ChangeData(Model, 0, new byte[8]);
         var stream = new TableStreamRun(Model, 0, SortedSpan<int>.None, "[text<\"\">]", default, new FixedLengthStreamStrategy(2));
         Model.ObserveRunWritten(Token, stream);

         var text = Environment.NewLine.Join(new[] { "<100>", "<null>" });
         stream.DeserializeRun(text, Token, out var changedOffsets, out _);

         var destinationRun = Model.GetNextRun(0x100);
         Assert.Equal(0x100, destinationRun.Start);
         Assert.IsType<PCSRun>(destinationRun);
         Assert.Equal(0, destinationRun.PointerSources.Single());
      }

      [Fact]
      public void TableStreamRun_DeserializeNewPointerDestination_OldAnchorRemovedNewAnchorAdded() {
         SetFullModel(0xFF);
         Token.ChangeData(Model, 0, new byte[4]);
         Model.WritePointer(Token, 4, 0x100);
         var stream = new TableStreamRun(Model, 0, SortedSpan<int>.None, "[pointer<>]", default, new FixedLengthStreamStrategy(2));
         Model.ObserveRunWritten(Token, stream);

         var text = Environment.NewLine.Join(new[] { "<180>", "<null>" });
         stream.DeserializeRun(text, Token, out var _, out _);

         var destinationRun = Model.GetNextRun(0x100);
         Assert.Equal(0x180, destinationRun.Start);
         Assert.Equal(0, destinationRun.PointerSources.Single());
      }

      [Fact]
      public void TableStreamRun_DeserializeNewPointerDestinationToIncorrectFormat_OldAnchorRemovedNewAnchorAdded() {
         SetFullModel(0xFF);
         Token.ChangeData(Model, 0, new byte[4]);
         Model.WritePointer(Token, 4, 0x100);
         var stream = new TableStreamRun(Model, 0, SortedSpan<int>.None, "[pointer<\"\">]", default, new FixedLengthStreamStrategy(2));
         Model.ObserveRunWritten(Token, stream);

         Model[0x180] = 0x40; // invalid text byte
         var text = Environment.NewLine.Join(new[] { "<180>", "<null>" });
         stream.DeserializeRun(text, Token, out var _, out _);

         var destinationRun = Model.GetNextRun(0x100);
         Assert.Equal(0x180, destinationRun.Start);
         Assert.IsType<NoInfoRun>(destinationRun);
         Assert.Equal(0, destinationRun.PointerSources.Single());
      }

      [Fact]
      public void TableStream_Copy_FirstElementStartsWithPlus() {
         SetFullModel(0xFF);
         ViewPort.Edit("^stream[a: b:]!FFFF +1,2 +3,4 +5,6 ");
         ViewPort.SelectionStart = new(0, 0);
         ViewPort.MoveSelectionEnd.Execute(Direction.End);

         ViewPort.Copy.Execute(FileSystem);

         string text = FileSystem.CopyText;
         var addCount = text.ToCharArray().Where(c => c == '+').Count();
         Assert.Equal(3, addCount);
      }

      [Fact]
      public void TableStream_DeepCopyPointer_CloseElementIncludedOnce() {
         SetFullModel(0xFF);
         ViewPort.Edit("^stream[a: b:]!FFFF +1,2 +3,4 +5,6 ");
         ViewPort.SelectionStart = new(0, 0);
         ViewPort.MoveSelectionEnd.Execute(Direction.End);

         ViewPort.Edit("@100 <stream> @100 ^table[ptr<>]1 ");
         ViewPort.DeepCopy.Execute(FileSystem);

         string text = FileSystem.CopyText;
         Assert.Equal(text.IndexOf("[]"), text.LastIndexOf("[]"));
      }

      [Fact]
      public void StreamWithEndToken_AppendNegative_DataCleared() {
         SetFullModel(0xFF);
         ViewPort.Edit("01 02 03 04 C0 @00 ^table[data.]!C0 ");

         var run = (ITableRun)Model.GetNextRun(0);
         run.Append(Token, -1);

         Assert.Equal(0xFF, Model[4]);
      }

      [Fact]
      public void TableStreamWithEndToken_AppendAfterCloseToken_Noop() {
         SetFullModel(0xFF);
         ViewPort.Edit("FE @00 ^table[data.]!FE +13 +15 +17 ");

         ViewPort.Edit("@04 +");

         var table = Model.GetTableModel("table");
         Assert.Equal(3, table.Count);
      }

      [Fact]
      public void ParentLengthStreamWithNoParent_Append_StillWorks() {
         ViewPort.Edit("^table[content<> unused::]/count <100> 0 ");

         ViewPort.Edit("+<180> 0 ");

         var table = Model.GetTableModel("table");
         Assert.Equal(2, table.Count);
      }

      [Fact]
      public void TableOfPointers_PointerToTextInTable_NoAsserts() {
         SetFullModel(0xFF);
         ViewPort.Edit("@100 ^stats[name\"\"8 a:: b::]8 \"0123\" 0 0 \"adam\" 0 0 \"bob\" 0 0 \"carl\" 0 0 \"dave\" 0 0 \"eric\" 0 0 ");

         ViewPort.Edit("@000 <null> <null> <null> <null> @000 ^table[pointer<\"\">]4 <080> <stats/2> <090> <060> ");

         Assert.Empty(Errors);
         Model.ResolveConflicts();
      }

      [Fact]
      public void TableOfPointers_PointerToTextInTableFromTableTool_NoAsserts() {
         SetFullModel(0xFF);
         ViewPort.Edit("@100 ^stats[name\"\"8 a:: b::]8 \"0123\" 0 0 \"adam\" 0 0 \"bob\" 0 0 \"carl\" 0 0 \"dave\" 0 0 \"eric\" 0 0 ");

         ViewPort.Edit("@000 <null> <null> <null> <null> @000 ^table[pointer<\"\">]4 @008 ");
         var field = ViewPort.Tools.TableTool.Children.OfType<FieldArrayElementViewModel>().Single(field => field.Name == "pointer");
         field.Content = "<120>";

         Assert.Empty(Errors);
         Model.ResolveConflicts();
      }
   }
}
