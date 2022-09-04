using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using HavenSoft.HexManiac.Core.ViewModels.Visitors;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class TableTests : BaseViewModelTestClass {
      [Fact]
      public void TableCanHaveNumericLimitOnField() {
         ViewPort.Edit("^table[data.4]8 ");
         Assert.Empty(Errors);

         ViewPort.SelectionStart = new Point(2, 0);
         ViewPort.Edit("5 "); // you should still be able to manually enter bad values
         Assert.Equal(5, Model[0x02]);

         // a combobox is used for numeric limit fields
         ViewPort.Tools.TableTool.Children.Single(child => child is ComboBoxArrayElementViewModel);
      }

      [Fact]
      public void TrainerPokemonTeamEnumSelectionSelectsEntireEnum() {
         ArrangeTrainerPokemonTeamData(0, 1);

         ViewPort.SelectionStart = new Point(0, 6);

         Assert.Equal(new Point(1, 6), ViewPort.SelectionEnd);
      }

      [Fact]
      public void HexEditingWorksForTrainerPokemon() {
         ArrangeTrainerPokemonTeamData(0, 1);

         ViewPort.SelectionStart = new Point(TrainerPokemonTeamRun.PokemonFormat_PokemonStart, 6);
         ViewPort.Edit("C ");

         Assert.Equal(2, Model[0x60 + TrainerPokemonTeamRun.PokemonFormat_PokemonStart]);
      }

      [Fact]
      public void CanExtendTrainerTeamViaStream() {
         ArrangeTrainerPokemonTeamData(0, 1);
         Model[0x6C] = 0x0B; // add a random data value so that extending will cause moving

         ViewPort.SelectionStart = new Point(0, 6);
         var streamTool = ViewPort.Tools.StringTool;
         streamTool.Content = $"10 A{Environment.NewLine}10 B";

         Assert.NotEqual(0x60, Model.ReadPointer(0x24));
      }

      [Fact]
      public void CanContractTrainerTeamViaStream() {
         ArrangeTrainerPokemonTeamData(0, 4);

         ViewPort.SelectionStart = new Point(0, 6);
         var streamTool = ViewPort.Tools.StringTool;
         streamTool.Content = $"10 A";

         Assert.Equal(1, Model[TrainerPokemonTeamRun.TrainerFormat_PokemonCountOffset]);
      }

      [Fact]
      public void AppendViaTableToolUpdatesParent() {
         ArrangeTrainerPokemonTeamData(0, 2, 1);

         ViewPort.Edit("@A8 "); // select last pokemon
         var tableTool = ViewPort.Tools.TableTool;
         tableTool.Append.Execute();

         Assert.Equal(3, Model[TrainerPokemonTeamRun.TrainerFormat_PokemonCountOffset]);
      }

      [Fact]
      public void ChangePokemonCountUpdatesTrainerTeam() {
         ArrangeTrainerPokemonTeamData(0, 2, 2);

         // update count via table: child run should update
         var tool = ViewPort.Tools.TableTool;
         var childCountField = (FieldArrayElementViewModel)tool.Children[tool.Children.Count - 3];
         childCountField.Content = "3";
         Assert.Equal(3, ((ITableRun)Model.GetNextRun(0xA0)).ElementCount);

         // update count via inline: child run should update
         ViewPort.Edit("@20 1 ");
         Assert.Equal(1, ((ITableRun)Model.GetNextRun(0xA0)).ElementCount);
      }

      [Fact]
      public void ChangePokemonCountChangesOtherTeamsWithSamePointer() {
         ArrangeTrainerPokemonTeamData(0, 2, 2);
         ViewPort.Edit("@4C <0A0>");

         // update count via table: sibling should update
         var tool = ViewPort.Tools.TableTool;
         var childCountField = (FieldArrayElementViewModel)tool.Children[tool.Children.Count - 3];
         childCountField.Content = "3";
         Assert.Equal(3, Model[0x48]);

         // update count via inline: sibling should update
         ViewPort.Edit("@20 1 ");
         Assert.Equal(1, Model[0x48]);
      }

      [Fact]
      public void ChangeStructTypeChangesTrainerTeam() {
         ArrangeTrainerPokemonTeamData(0, 2, 2);

         // update count via table: child run should update
         var tool = ViewPort.Tools.TableTool;
         var childCountField = (ComboBoxArrayElementViewModel)tool.Children[1];
         childCountField.SelectedIndex = 3;
         Assert.Equal(0x10, ((ITableRun)Model.GetNextRun(0xA0)).ElementLength);

         // update count via inline: child run should update
         ViewPort.Edit("@00 2 ");
         Assert.Equal(0x08, ((ITableRun)Model.GetNextRun(0xA0)).ElementLength);
      }

      [Fact]
      public void ChangeStructTypeChangesOtherTeamsWithSamePointer() {
         ArrangeTrainerPokemonTeamData(0, 2, 2);
         ViewPort.Edit("@4C <0A0>");

         // update count via table: sibling should update
         var tool = ViewPort.Tools.TableTool;
         var childCountField = (ComboBoxArrayElementViewModel)tool.Children[1];
         childCountField.SelectedIndex = 3;
         Assert.Equal(3, Model[0x28]);

         // update count via inline: sibling should update
         ViewPort.Edit("@00 2 ");
         Assert.Equal(2, Model[0x28]);
      }

      [Fact]
      public void SimplifyStructTypeClearsExtraUnusedData() {
         ArrangeTrainerPokemonTeamData(TrainerPokemonTeamRun.INCLUDE_MOVES, 2, 1);
         ViewPort.Edit("@A0 ");
         var tool = ViewPort.Tools.StringTool;

         tool.Content = "10 A";

         Assert.Equal(0, Model[TrainerPokemonTeamRun.TrainerFormat_StructTypeOffset]); // the parent updated to type 0
         for (int i = 8; i < 0x20; i++) { // 0x10 per pokemon for 2 pokemon
            Assert.Equal(0xFF, Model[0xA0 + i]); // the child is just 8 bytes long but used to be 0x20 bytes long, so bytes 8 to 0x20 are all FF
         }
      }

      [Fact]
      public void ChangingTypeFromMovesToMovesAndItemsKeepsSameMoves() {
         ArrangeTrainerPokemonTeamData(TrainerPokemonTeamRun.INCLUDE_MOVES, 1, 1);
         ViewPort.Edit("@A0 ");
         var tool = ViewPort.Tools.StringTool;

         tool.Content = @"10 A
- w
- x
- y
- q";

         ViewPort.Edit("@00 3 @trainertable/0/pokemon/0 ");

         var moves = tool.Content.Split(new[] { Environment.NewLine }, 2, StringSplitOptions.None)[1];
         Assert.Equal(@"- w
- x
- y
- q
", moves);
      }

      [Fact]
      public void CanCreateCustomNamedStreamsWithEndToken() {
         // Arrange: create some data near the start that could be a custom stream
         CreateTextTable("info", 0x100, Enumerable.Range('a', 20).Select(c => ((char)c).ToString()).ToArray());
         Model[0x10] = 0xFF;
         Model[0x11] = 0xFF;

         // Act: create the custom stream
         ViewPort.Edit("@00 ^bob[number: category:info]!FFFF ");

         // Assert: make sure the custom stream looks right
         var run = (ITableRun)Model.GetNextRun(0);
         Assert.Equal(0x12, run.Length);
         Assert.IsAssignableFrom<IStreamRun>(run);
         Assert.Equal(4, run.ElementCount);
         Assert.Equal(4, run.ElementLength);

         var format = (EndStream)run.CreateDataFormat(Model, 0x11);
         Assert.Equal(0x10, format.Source);
         Assert.Equal(1, format.Position);
         Assert.Equal(2, format.Length);

         // Assert: selecting the end token selects the entire token
         ViewPort.Edit("@10 ");
         Assert.NotEqual(ViewPort.SelectionStart, ViewPort.SelectionEnd);
      }

      [Fact]
      public void CanCreateTableStreamWithLengthFromParent() {
         // arrange: write the data for the table
         Model.WritePointer(ViewPort.CurrentChange, 0x80, 0x00);
         Model.WriteMultiByteValue(0x84, 4, ViewPort.CurrentChange, 4);

         // act: add the run
         ViewPort.Edit("@80 ^table[data<[category.]/count> count::]1 ");

         // assert: make sure the custom stream looks right
         var run = (ITableRun)Model.GetNextRun(0);
         Assert.Equal(4, run.Length);
         Assert.IsAssignableFrom<IStreamRun>(run);
         Assert.Equal(4, run.ElementCount);
         Assert.Equal(1, run.ElementLength);
      }

      [Fact]
      public void CanExtendTableStreamWithEndToken() {
         ViewPort.Edit("@10 FF FF FF FF @00 ^bob[number.]!FF ");

         ViewPort.Edit("@10 +");

         var run = (ITableRun)Model.GetNextRun(0);
         Assert.Equal(0x11, run.ElementCount);
      }

      [Fact]
      public void CanExtendTableStreamWithLengthFromParent() {
         ViewPort.Edit("FF FF FF FF FF FF FF FF @80 <0> 02 @80 ^table[data<[value.]/count> count::]1 ");

         ViewPort.Edit("@02 +");

         var run = (ITableRun)Model.GetNextRun(0);
         Assert.Equal(3, run.ElementCount);
         Assert.Equal(3, Model[0x84]);
      }

      [Fact]
      public void CanExtendTableStreamWithLengthFromParentFromParent() {
         ViewPort.Edit("FF FF FF FF FF FF FF FF @80 <0> 02 @80 ^table[data<[value.]/count> count::]1 ");

         ViewPort.Edit("@84 3 ");

         var run = (ITableRun)Model.GetNextRun(0);
         Assert.Equal(3, run.ElementCount);
         Assert.Equal(3, Model[0x84]);
      }

      [Fact]
      public void CannotExtendLengthOfFixedLengthStreamViaTool() {
         // Arrange
         ViewPort.Edit("FF FF FF FF FF FF FF FF @80 <0> @80 ^table[data<[value.]5>]1 ");
         var segment = ViewPort.Tools.TableTool.Children.OfType<TextStreamElementViewModel>().Single();

         // precondition: the format is as expected
         Assert.Equal(@"255
255
255
255
255", segment.Content);

         // Act: change the content in a way that would change the length of a more dynamic stream.
         segment.Content = "12";

         // Assert that the stream length is still 5.
         var result = (ITableRun)Model.GetNextRun(0);
         Assert.Equal(5, result.ElementCount);
      }

      [Fact]
      public void AddingArrayWithPointerToStreamWithPointersAlsoAddsAnchorsForThosePointers() {
         ViewPort.Edit("1A 00 00 08 @33 08"); // 00 will point to 1A, 30 will point to 00

         ViewPort.Edit("@30 ^table[pointer<[inner<>]1>]1 "); // this table is a pointer that points to a pointer

         Assert.Equal(0x1A, Model.GetNextRun(0x1A).Start);
      }

      [Fact]
      public void CanCreateDataFromNull() {
         for (int i = 0x80; i < Model.RawData.Length; i++) Model.RawData[i] = 0xFF;
         ViewPort.Edit("^table[number: value: pointer<\"\">]8 @04 ");

         // verify that it's in the context menu
         var items = ViewPort.GetContextMenuItems(new Point(4, 0)).ToList();
         var group = (ContextItemGroup)items.Single(menuItem => menuItem.Text == "Pointer Operations");
         var createNewItem = group.Single(item => item.Text == "Create New Data");

         // verify that it's in the table
         var toolElements = ViewPort.Tools.TableTool.Children;
         var createNewTool = (TextStreamElementViewModel)toolElements.Single(element => element is TextStreamElementViewModel stream && stream.CanCreateNew);

         // verify that the command works
         createNewItem.Command.Execute();
         Assert.NotEqual(0, Model.ReadValue(0x04));
         var run = (PCSRun)Model.GetNextRun(Model.ReadPointer(0x04));
         Assert.Equal(1, run.Length);

         // verify that you can't use the table tool's version anymore
         Assert.False(createNewTool.CanRepoint);
      }

      [Fact]
      public void CanGetOptionsFromStringPointerTable() {
         ViewPort.Edit("^aSpot FF @aSpot ^aSpot\"\" Content1\""); // 9 bytes
         ViewPort.Edit("^bSpot FF @bSpot ^bSpot\"\" Content2\""); // 9 bytes
         ViewPort.Edit("^cSpot FF @cSpot ^cSpot\"\" Content3\""); // 9 bytes
         ViewPort.Edit("@20 <aSpot> <bSpot> <cSpot> @20 ");
         ViewPort.Edit("^table[pointer<\"\">]3 "); // 12 bytes

         using (ModelCacheScope.CreateScope(Model)) {
            var options = ModelCacheScope.GetCache(Model).GetOptions("table");
            Assert.Equal(3, options.Count);
            Assert.Equal("Content1", options[0]);
            Assert.Equal("Content2", options[1]);
            Assert.Equal("Content3", options[2]);
         }
      }

      [Fact]
      public void CanClearBitOptions() {
         CreateTextTable("source", 0x100, "Apple", "Banana", "Coconut", "Durian");
         CreateBitArrayTable("test", 0x20, "source", 0b0101, 0b1010, 0b1100, 0b0011);

         ViewPort.Edit("@test/1 -");
         Assert.Equal(0b0000, Model[0x21]);
      }

      [Fact]
      public void CanSetBitOptions() {
         CreateTextTable("source", 0x100, "Apple", "Banana", "Coconut", "Durian");
         CreateBitArrayTable("test", 0x20, "source", 0b0101, 0b1010, 0b1100, 0b0011);

         ViewPort.Edit("@test/1 apple ");
         Assert.Equal(0b1011, Model[0x21]);
      }

      [Fact]
      public void EnumFromRandomTableHasValues() {
         ViewPort.Edit("^primes[value.]10 @20 ^child[element:primes]13 ");
         var viewModel = (ComboBoxArrayElementViewModel)ViewPort.Tools.TableTool.Children.Single(child => child is ComboBoxArrayElementViewModel);
         Assert.Equal(10, viewModel.Options.Count);
      }

      [Fact]
      public void CanEditInlineWithCommas() {
         ViewPort.Edit("^table[a. b.]10 0,0 1,1 2,2 3,3 4,4 5,5 6,6 7,7 8,8 ");
         Assert.Equal(4, Model[8]);
      }

      [Fact]
      public void EnumGotoJumpsToSelectedElement() {
         CreateTextTable("names", 0x00, "Adam", "Bob", "Carl");
         CreateEnumTable("enums", 0x10, "names", 2, 2, 2);

         ViewPort.Goto.Execute(0x10);
         var enumSegment = ViewPort.Tools.TableTool.Children.OfType<ComboBoxArrayElementViewModel>().Single();
         enumSegment.GotoSource.Execute();

         Assert.Equal(10, ViewPort.DataOffset);
      }

      [Fact]
      public void AddNewToTableWithPlusLength() {
         // arrange: make a table, and then another table with +1 length
         CreateTextTable("names", 0x00, "Adam", "Bob", "Carl");
         ViewPort.Edit("@20 ^data[value:]names+1 00 10 20 40 ");

         // act: extend both tables
         ViewPort.Edit("@0F +");

         // assert: the new element was inserted, not appended, to the +1 table.
         // this keeps the +1 element at the end.
         Assert.Equal(20, Model.ReadMultiByteValue(0x20 + 2 * 3, 2));
         Assert.Equal(40, Model.ReadMultiByteValue(0x20 + 2 * 4, 2));
      }

      [Fact]
      public void RepointToOffScreenScrollsWell() {
         ViewPort.UseCustomHeaders = false;
         CreateTextTable("names", 0x00, "Audacity", "Bloomer", "Captain", "Dodongo", "Elephant");
         ViewPort.Edit("@30 22 "); // data at 48, to get in the way of an expand

         ViewPort.Edit("@names/5 +"); // do an expand

         var table = Model.GetTable("names");
         var newAddress = table.Start + table.ElementLength * 5;
         Assert.Equal(0, ViewPort.SelectionStart.X); // new element should be at start of line
         Assert.Equal(ViewPort.Headers[ViewPort.SelectionStart.Y], newAddress.ToString("X6")); // selection should be at start of new element
      }

      [Fact]
      public void ExpandTableViaAnchorEditorDoesNotRepointTables() {
         // setup some default tables
         ViewPort.Edit("@00 ^parent[items:]10 ");
         ViewPort.Edit("@40 ^child[items:]parent ");

         // setup some data that's part of the tables, but wasn't recognized.
         ViewPort.Edit("@14 20 00 @54 20 00 ");

         // Act: edit the anchor, which should not change any data, but just is a way for the user to express that the anchor was wrong.
         ViewPort.Edit("@00 ");
         ViewPort.AnchorText = "parent[items:]12";

         // Assert: the tables didn't move, but are longer
         Assert.Equal(0x00, Model.GetAddressFromAnchor(ViewPort.CurrentChange, -1, "parent"));
         Assert.Equal(0x40, Model.GetAddressFromAnchor(ViewPort.CurrentChange, -1, "child"));
         Assert.Equal(12 * 2, Model.GetNextRun(0x00).Length);
         Assert.Equal(12 * 2, Model.GetNextRun(0x40).Length);
      }

      [Fact]
      public void Enum_Tooltip_ShowsText() {
         CreateTextTable("names", 0x100, "adam", "bob", "carl");
         CreateEnumTable("enums", 0, "names", 0, 1, 2);
         ViewPort.Refresh();
         var cell = ViewPort[1, 0];

         var tooltipVisitor = new ToolTipContentVisitor(Model);
         cell.Format.Visit(tooltipVisitor, cell.Value);
         var content = tooltipVisitor.Content.Single().ToString();

         Assert.Equal("adam", content);
      }

      [Fact]
      public void TrainerTeam_ExpandPokemon_NewPokemonHasValidValues() {
         ArrangeTrainerPokemonTeamData(default, 3, 3);

         ViewPort.Edit("@trainertable/0/pokemon/3 10 ");       // put some random data after the run, so that expanding causes a repoint
         ViewPort.Edit("@trainertable/0/pokemon/2 0 10 F 0 "); // write some dummy data to the run so it's not just all 0's
         var currentLocation = ViewPort.DataOffset;
         ViewPort.Edit("+");                                   // expand the trainer's pokemon

         var trainers = (ITableRun)Model.GetNextRun(0);
         var destination = Model.ReadPointer(trainers.ElementLength - 4);
         var team = (ITableRun)Model.GetNextRun(destination);

         Assert.All(Enumerable.Range(team.Start + team.ElementLength * 3, team.ElementLength),
            i => Assert.Equal(Model[i - team.ElementLength], Model[i])
         );
         Assert.NotEqual(currentLocation, ViewPort.DataOffset);
      }

      [Fact]
      public void BitArray_Edit_Autocomplete() {
         CreateTextTable("names", 0x100, "beforeTEXT", "TEXTafter", "TEmiddleXT", "somethingelse");
         CreateEnumTable("enums", 0x180, "names", 0, 1, 2, 3);
         CreateBitArrayTable("bits", 0, "enums", 0b1, 0b10, 0b100, 0b1000);
         ViewPort.Refresh();

         ViewPort.Edit("TEXT");

         var cell = (UnderEdit)ViewPort[0, 0].Format;
         var options = cell.AutocompleteOptions.Select(option => option.CompletionText.Trim()).ToArray();
         Assert.Equal(new[] { "beforeTEXT", "TEXTafter", "TEmiddleXT" }, options);
      }

      [Fact]
      public void Table_DeepCopy_IncludesMetacommandToBlankTheData() {
         SetFullModel(0xFF);
         var fs = new StubFileSystem();
         CreateTextTable("names", 0, "adam", "bob", "carl", "dave"); // 5*4 bytes long
         ViewPort.Refresh();

         ViewPort.Edit("@00 ");
         ViewPort.SelectionEnd = ViewPort.ConvertAddressToViewPoint(Model.GetNextRun(0).Length - 1);
         ViewPort.DeepCopy.Execute(fs);

         Assert.StartsWith("@!00(20) ^names", fs.CopyText);
      }

      [Fact]
      public void MultipleTables_CheckTableSelector_TablesSortedAlphabetically() {
         CreateTextTable("xxx.yyy.zzz", 0x40, "adam", "bob", "carl", "steve");
         CreateTextTable("iii.jjj.kkk", 0x80, "adam", "bob", "carl", "steve");
         CreateTextTable("aaa.bbb.ccc", 0xC0, "adam", "bob", "carl", "steve");

         var tableSections = ViewPort.Tools.TableTool.TableSections;
         var sortedSection = tableSections.ToList();
         sortedSection.Sort();

         Assert.All(tableSections.Count.Range(), i => Assert.Equal(sortedSection[i], tableSections[i]));
      }

      [Fact]
      public void MultipleTableList_CheckTableList_TablesSortedAlphabetically() {
         CreateTextTable("aaa.bbb.zzz", 0x40, "adam", "bob", "carl", "steve");
         CreateTextTable("aaa.bbb.kkk", 0x80, "adam", "bob", "carl", "steve");
         CreateTextTable("aaa.bbb.ccc", 0xC0, "adam", "bob", "carl", "steve");
         ViewPort.Tools.TableTool.SelectedTableSection = 0;

         var tableList = ViewPort.Tools.TableTool.TableList;
         var sortedTables = tableList.ToList();
         sortedTables.Sort();

         Assert.All(tableList.Count.Range(), i => Assert.Equal(sortedTables[i], tableList[i]));
      }

      [Fact]
      public void PointersToTextInTable_RepointTable_PointersUpdate() {
         SetFullModel(0xFF);
         CreateTextTableWithInnerPointers("table", 0x20, "adam", "bob", "carl", "1234567");
         ViewPort.Edit("@00!00(16) ^pointers[ptr<>]4 <000048> <000028> <000030> <000010> @40 10");
         var table = (ArrayRun)Model.GetTable("table");
         Assert.Equal(new[] { 0, 1, 1, 0 }, table.PointerSourcesForInnerElements.Select(sources => sources.Count).ToArray());

         ViewPort.Goto.Execute("table");
         ViewPort.Edit(ConsoleKey.End);
         ViewPort.Tools.TableTool.Append.Execute();

         table = (ArrayRun)Model.GetTable("table");
         Assert.Equal(table.Start + table.ElementLength, Model.ReadPointer(4));
         Assert.Equal(table.Start + table.ElementLength * 2, Model.ReadPointer(8));
      }

      [Fact]
      public void BitArray_SetValueIn5thByte_ByteChanges() {
         CreateTextTable("text", 0x40, 38.Range().Select(i => "entry" + i).ToArray());
         CreateEnumTable("enums", 0x180, "text", 38.Range().ToArray());
         CreateBitArrayTable("bits", 0, "enums", 0, 0, 0, 0);
         ViewPort.Refresh();

         ViewPort.Edit("entry34 ");

         Assert.Equal(0b100, Model[4]);
      }

      [Fact]
      public void TextTable_MakeEnumTableWithOffset_ValuesAppearCorrectly() {
         CreateTextTable("text", 0x80, 10.Range().Select(i => "entry" + i).ToArray());
         ViewPort.Refresh();

         ViewPort.Edit("@00 0A 0B 0C");
         ViewPort.Edit("@00 ^enums[entry.text+10]3 ");

         var segment = (ArrayRunEnumSegment)Model.GetTable("enums").ElementContent[0];
         Assert.Equal("text", segment.EnumName);
         Assert.Equal(10, segment.ValueOffset);
         var cell = (IntegerEnum)ViewPort[1, 0].Format;
         Assert.Equal("entry1", cell.DisplayValue);
      }

      [Fact]
      public void EnumTableWithOffset_Edit_ValueAppearsCorrectly() {
         CreateTextTable("text", 0x80, 10.Range().Select(i => "entry" + i).ToArray());
         ViewPort.Refresh();
         ViewPort.Edit("@00 0A 0B 0C");
         ViewPort.Edit("@00 ^enums[entry.text+10]3 ");

         ViewPort.SelectionStart = new Point(1, 0);
         ViewPort.Edit("entry7 ");

         var cell = (IntegerEnum)ViewPort[1, 0].Format;
         Assert.Equal("entry7", cell.DisplayValue);
      }

      [Fact]
      public void MatchedTable_MinusFrontAndBack_ElementsCorrect() {
         CreateTextTable("names", 0x100, "adam", "bob", "carl", "dave");

         ViewPort.Edit("@00 ^table[field.]names-1-1 ");

         var table = (ArrayRun)Model.GetTable("table");
         Assert.Equal(2, table.ElementCount);
         Assert.Equal("bob", table.ElementNames[0]);
         Assert.Equal("carl", table.ElementNames[1]);
      }

      [Fact]
      public void MatchedTableWithMinusEnd_AppendTwo_NoMinus() {
         CreateTextTable("names", 0x100, "adam", "bob", "carl", "dave");
         ViewPort.Edit("@00 ^table[field.]names-0-2 ");

         ViewPort.Goto.Execute("names");
         Array.ForEach(new object[] { Direction.End, Direction.Right }, ViewPort.MoveSelectionStart.Execute);
         ViewPort.Edit("+");

         var table = (ArrayRun)Model.GetTable("table");
         Assert.Equal(ParentOffset.Default, table.ParentOffset);
         Assert.EndsWith("names", table.FormatString);
         Assert.Equal(5, table.ElementCount);
      }

      [Fact]
      public void TextTable_QuoteTwice_MoveToNextElement() {
         CreateTextTable("names", 0x0, "adam", "bob", "carl", "dave");
         ViewPort.Goto.Execute(0);

         ViewPort.Edit("\"\"");

         var selectedAddress = ViewPort.ConvertViewPointToAddress(ViewPort.SelectionStart);
         Assert.Equal(5, selectedAddress);
      }

      [Fact]
      public void TextTableWithInnerPointers_CutPaste_InnerPointersMove() {
         SetFullModel(0xFF);
         var fileSystem = new StubFileSystem();
         ViewPort.Edit("^table^[name\"\"16]2 \"Alpha\" \"Beta\"");
         ViewPort.Edit("@40 <0000> <0010>");

         // cut
         ViewPort.Goto.Execute(0);
         ViewPort.SelectionEnd = ViewPort.ConvertAddressToViewPoint(0x20 - 1);
         ViewPort.Copy.Execute(fileSystem);
         ViewPort.Clear.Execute();

         // paste
         ViewPort.Goto.Execute(0x60);
         ViewPort.Edit(fileSystem.CopyText);

         Assert.Equal(0x60, Model.ReadPointer(0x40));
         Assert.Equal(0x70, Model.ReadPointer(0x44));
      }

      [Fact]
      public void TableWithNegativeOffset_ShrinkParentTableSuchThatChildTableHasNegativeLength_ChildTableHasLengthOne() {
         CreateTextTable("names", 0, "adam", "bob", "carl", "dave");
         ViewPort.Edit("@100 ^child[a.]names-1-1 ");

         ViewPort.Goto.Execute(0);
         var text = ViewPort.AnchorText;
         ViewPort.AnchorText = text.Substring(0, text.Length - 1) + "1";

         var table = Model.GetTable("child");
         Assert.Equal(1, table.ElementCount);
      }

      [Fact]
      public void TableWithNegativeOffset_ShrinkAndGrowParent_ChildLengthRestored() {
         CreateTextTable("names", 0, "adam", "bob", "carl", "dave");
         ViewPort.Edit("@100 ^child[a.]names-1-1 ");

         ViewPort.Goto.Execute(0);
         var text = ViewPort.AnchorText;
         text = text.Substring(0, text.Length - 1);
         ViewPort.AnchorText = text + "1";
         ViewPort.AnchorText = text + "4";

         var table = Model.GetTable("child");
         Assert.Equal(2, table.ElementCount);
      }

      [Fact]
      public void IntTable_Sort_ValuesSorted() {
         ViewPort.Edit("^table[a:]3 10 12 11 ");

         var table = Model.GetTable("table");
         var newData = table.Sort(Model, 0);
         ViewPort.CurrentChange.ChangeData(Model, 0, newData);

         Assert.Equal(10, Model.ReadMultiByteValue(0, 2));
         Assert.Equal(11, Model.ReadMultiByteValue(2, 2));
         Assert.Equal(12, Model.ReadMultiByteValue(4, 2));
      }

      [Fact]
      public void PointerTable_RequestHeaderForDestination_GetTableIndexName() {
         Model.SetList("options", new[] { "Adam", "Bob", "Carl", "Dave" });
         ViewPort.Edit("^table[entry<>]options <100> <120> <140> <160>");

         Model.TryGetUsefulHeader(0x120, out var header);

         Assert.Equal("Bob", header);
      }

      [Fact]
      public void CalculatedSegment_CheckValue_IsCalculated() {
         ViewPort.Edit("^table[a: c|=a*b b:]2 3 4 @table ");
         var table = Model.GetTable("table");
         var segment = (ArrayRunCalculatedSegment)table.ElementContent[1];
         var viewmodel = (CalculatedElementViewModel)ViewPort.Tools.TableTool.Children.Single(vm => vm is CalculatedElementViewModel);

         Assert.Equal(12, segment.CalculatedValue(0));
         Assert.Equal(12, table.ReadValue(Model, 0, "c"));
         Assert.Equal(12, viewmodel.CalculatedValue);
      }

      [Fact]
      public void CalculatedSegment_CalculateSource_GetSource() {
         ViewPort.Edit("^table[a: c|=a*b b:]2 3 4 ");
         var table = Model.GetTable("table");

         int source = ArrayRunCalculatedSegment.CalculateSource(Model, table, 0, "b");

         Assert.Equal(2, source);
      }

      [Fact]
      public void CalculatedSegment_CheckNestedValueFromEnum_IsCalculated() {
         CreateTextTable("names", 0x180, "adam", "bob", "carl");
         ViewPort.Edit("@100 ^correlate[data:names number:]4 carl 10 bob 5 ");
         ViewPort.Edit("@00 ^table[thing:names calc|=(correlate/data=thing)/number]2 bob ");

         var table = Model.GetTable("table");
         var segment = (ArrayRunCalculatedSegment)table.ElementContent[1];
         var value = segment.CalculatedValue(0);

         Assert.Equal(5, value);
      }

      [Fact]
      public void CalculatedSegment_CheckNestedValueFromPointer_IsCalculated() {
         ViewPort.Edit("@100 ^destination[data: number:]4 1 2 3 4 ");
         ViewPort.Edit("@00 ^table[thing<> calc|=thing/1/data]2 <100> ");

         var table = Model.GetTable("table");
         var segment = (ArrayRunCalculatedSegment)table.ElementContent[1];
         var value = segment.CalculatedValue(0);

         Assert.Equal(3, value);
      }

      [Fact]
      public void CalculatedSegment_SourceTableDoesNotExist_NoSource() {
         var contract = "(not.a.table/field1=content)/field2";

         ArrayRun.TryParse(Model, "[content:]1", 0, default, out var table);
         var source = ArrayRunCalculatedSegment.CalculateSource(Model, table, 0, contract);

         Assert.Equal(Pointer.NULL, source);
      }

      [Fact]
      public void CalculatedSegment_SourceTableDoesNotExist_NoJump() {
         var contract = "(not.a.table/field1=content)/field2";

         var segment = new ArrayRunCalculatedSegment(Model, "name", contract);
         var viewModel = new CalculatedElementViewModel(ViewPort, segment, 0);

         Assert.False(viewModel.Operands[0].JumpTo.CanExecute(default));
      }

      [Fact]
      public void CalculatedSegment_SourceTableDoesNotExist_CanCalculate() {
         var contract = "(not.a.table/field1=content)/field2";
         Model.ObserveRunWritten(Token, new TableStreamRun(Model, 0, SortedSpan<int>.None, "[content:]", default, new FixedLengthStreamStrategy(1)));
         var segment = new ArrayRunCalculatedSegment(Model, "name", contract);

         var value = segment.CalculatedValue(0);

         Assert.Equal(0, value);
      }

      [Fact]
      public void CalculatedSegment_SourceTableDoesNotHaveMatch_UseLastValue() {
         ViewPort.Edit("@100 ^some.table[key: value:]2 1 1 2 2 ");
         var contract = "(some.table/key=content)/value";
         Model.ObserveRunWritten(Token, new TableStreamRun(Model, 0, SortedSpan<int>.None, "[content:]", default, new FixedLengthStreamStrategy(1)));
         var segment = new ArrayRunCalculatedSegment(Model, "name", contract);

         var value = segment.CalculatedValue(0);

         Assert.Equal(2, value);
      }

      [Fact]
      public void ZeroWidthSegment_GetColumnHeaders_NoHeaderForZeroWidthColumn() {
         ViewPort.Edit("^table[a: b|=a c:]2 ");
         var table = (ArrayRun)Model.GetTable("table");

         var headers = table.GetColumnHeaders(4, 0)[0].ColumnHeaders;
         Assert.Equal(2, headers.Count);
         Assert.Equal("a", headers[0].ColumnTitle);
         Assert.Equal("c", headers[1].ColumnTitle);
      }

      [Fact]
      public void TextTable_ChangeText_HeaderChanges() {
         ViewPort.AllowMultipleElementsPerLine = false;
         ViewPort.UseCustomHeaders = true;
         CreateTextTable("table", 0, "adam", "bob", "carl", "dave");
         ViewPort.Goto.Execute("table/bob ");

         ViewPort.Edit("B");

         Assert.Equal("Bob", ViewPort.Headers[0]);
      }

      [Fact]
      public void CalculatedSegment_Serialize_ReasonableText() {
         var segment = new ArrayRunCalculatedSegment(Model, "bob", "4+a");
         Assert.Equal("bob|=4+a", segment.SerializeFormat);
      }

      [Fact]
      public void CreateTable_BadEndTokenFormat_NoCrash() {
         ViewPort.Edit("^table[a:]1 ");
         ViewPort.AnchorText = "^table[a:]!";
         Assert.Single(Errors);
      }

      [Fact]
      public void Table_SignedIntFormat_NumbersCanBeNegative() {
         ViewPort.Edit("^table[a.|z b:|z c::|z]1 -1 -2 -3 ");
         var table = Model.GetTable("table");
         Assert.Equal(0xFF, Model[0]);
         Assert.Equal(ushort.MaxValue - 1, table.ReadValue(Model, 0, 1));
         Assert.Equal(-3, Model.ReadValue(3));
         Assert.Equal(-2, ((Integer)ViewPort[1, 0].Format).Value);
      }

      [Fact]
      public void TableMatchedToConstant_IncreaseConstantCausesRepoint_NotifyUserOfRepoint() {
         SetFullModel(0xFF);
         ViewPort.Edit("@10 .table.count 12 @20 ^table[data::]table.count @50 DEADBEEF ");
         var count = 0;
         ViewPort.OnMessage += (sender, e) => count += 1;

         ViewPort.Edit("@10 13 ");

         Assert.Equal(1, count);
      }

      [Fact]
      public void TableWithLengthOffset_FindElementReferences_NewTabNameCorrect() {
         IViewPort newTab = null;
         ViewPort.RequestTabChange += (sender, tab) => newTab = (IViewPort)tab;
         CreateTextTable("names", 0x100, "adam", "bob", "carl", "dave", "erik", "fred", "greg", "hal");
         ViewPort.Edit("@00 ^table1[data:]names-1 @20 ^table2[value:names]4 dave carl erik dave ");

         ViewPort.Goto.Execute("table1/dave"); // goto 'dave' in table1
         var buttons = ViewPort.Tools.TableTool.UsageChildren.Where(child => child is ButtonArrayElementViewModel).Cast<ButtonArrayElementViewModel>().ToList();
         buttons.Single(button => button.Text.Contains("table2")).Command.Execute();

         Assert.Contains("dave", newTab.Name);
      }

      [Fact]
      public void TableWithEndToken_TypeEndToken_MoveCursor() {
         ViewPort.Edit("^table[content:]!0000 ");

         ViewPort.Edit("[]");

         Assert.Equal(new Point(2, 0), ViewPort.SelectionStart);
      }

      [Fact]
      public void Anchor_UpdateTablePointerToAnchor_AnchorExists() {
         ViewPort.Edit("@00 ^inner ");
         ViewPort.Edit("@10 ^outer[ptr<[a.]1>]1 ");

         ViewPort.Edit("<inner>");

         Assert.Equal(0, Model.ReadPointer(0x10));
         Assert.Equal("inner", Model.GetAnchorFromAddress(-1, 0));
      }

      [Fact]
      public void TablePointingToNamedAnchor_Extend_AnchorIsStillNamed() {
         ViewPort.Edit("@00 ^inner ");
         ViewPort.Edit("@10 ^outer[ptr<[a.]1>]1 <inner> ");

         ViewPort.Edit("+");

         Assert.Equal(0, Model.ReadPointer(0x10));
         Assert.Equal(0, Model.ReadPointer(0x14));
         Assert.Equal("inner", Model.GetAnchorFromAddress(-1, 0));
      }

      [Fact]
      public void TwoTablePointersToSameAddress_ReplaceOneWithNull_DestinationUpdates() {
         ViewPort.Edit("^table[ptr<>]2 <010> <010> ");

         ViewPort.Edit("@table <null> ");

         var run = Model.GetNextRun(0x10);
         Assert.Single(run.PointerSources);
      }

      [Fact]
      public void SplitterSegment_Serialize_NoCrash() {
         var segment = new ArrayRunSplitterSegment();

         var format = segment.SerializeFormat;

         Assert.Equal("|", format);
      }

      [Fact]
      public void TableWithNoName_FindRelatedTables_NoTablesRelated() {
         ArrayRun.TryParse(Model, "[data:]4", 0x100, SortedSpan<int>.None, out var table1);
         ArrayRun.TryParse(Model, "[data:]4", 0x180, SortedSpan<int>.None, out var table2);
         Model.ObserveAnchorWritten(Token, string.Empty, table1);
         Model.ObserveAnchorWritten(Token, "some.name", table2);

         var related = Model.GetRelatedArrays((ArrayRun)table1).ToList();

         Assert.Equal(new[] { (ArrayRun)table1 }, related);
      }

      [Fact]
      public void TableWithPointersToSelf_Repoint_AnchorUpdated() {
         SetFullModel(0xFF);
         ViewPort.Edit("^table[data:: ptr<>]2 13 <table> 14 <table> DEADBEEF ");

         ViewPort.Edit("@010 +");

         var table = Model.GetTable("table");
         var source1 = table.Start + 4;
         var source2 = table.Start + 12;
         var source3 = table.Start + 20;
         Assert.Equal(new[] { source1, source2, source3 }, table.PointerSources);
      }

      [Fact]
      public void TablePointersToNamedSprites_UpdatePointerToNamedSprite_NoNull() {
         Token.ChangeData(Model, 0, LZRun.Compress(new byte[32]));
         ViewPort.Refresh();
         ViewPort.Shortcuts.DisplayAsSprite.Execute();
         ViewPort.Edit("@100 ^table[ptr<`lzs4x1x1`>]2 <000> <null> ");
         ViewPort.Edit("@000 ^some.name ");

         ViewPort.Edit("@104 <some.name> ");

         Assert.Equal("some.name", Model.GetAnchorFromAddress(-1, 0));
         Assert.Equal(0, Model.ReadPointer(0x100));
         Assert.Equal(0, Model.ReadPointer(0x104));
      }

      [Fact]
      public void MetadataHasTwoCloseTablesWithOverlappingConstantLength_Load_KeepSecondTableWithPointer() {
         Model.WritePointer(Token, 0x100, 0x10); // pointer to table1
         Model.WritePointer(Token, 0x110, 0x20); // pointer to table2 (16 bytes after table1)
         Model[0x120] = 6;                       // table length (24 bytes)

         var metadata = new StoredMetadata(
            anchors: new StoredAnchor[] { new(0x10, "table1", "[a::]constant"), new(0x20, "table2", "[b::]constant") },
            matchedWords: new[] { new StoredMatchedWord(0x120, "constant", 1, 0, 1, "note") }
         );
         var model = new PokemonModel(Model.RawData, metadata, Singletons);

         var pointerRun = model.GetNextRun(0x110);
         Assert.IsType<PointerRun>(pointerRun);
         Assert.Equal(0x110, pointerRun.Start);
      }

      [Fact]
      public void MetadataHasTwoCloseTablesWithBarelyOverlappingConstantLength_Load_KeepSecondTableWithPointer() {
         Model.WritePointer(Token, 0x100, 0x10); // pointer to table1
         Model.WritePointer(Token, 0x110, 0x20); // pointer to table2 (16 bytes after table1)
         Model[0x120] = 5;                       // table length (24 bytes)

         var metadata = new StoredMetadata(
            anchors: new StoredAnchor[] { new(0x10, "table1", "[a::]constant"), new(0x20, "table2", "[b::]constant") },
            matchedWords: new[] { new StoredMatchedWord(0x120, "constant", 1, 0, 1, "note") }
         );
         var model = new PokemonModel(Model.RawData, metadata, Singletons);

         var pointerRun = model.GetNextRun(0x110);
         Assert.IsType<PointerRun>(pointerRun);
         Assert.Equal(0x110, pointerRun.Start);
      }

      [Fact]
      public void MetadataHasTwoCloseTablesWithOverlappingParentTableLength_ParentTableConstantLoad_KeepSecondTableWithPointer() {
         ViewPort.Edit("@000 ^child1[a::]parent @010 ^child2[b::]parent @080 <child1> <child2> ");

         ArrayRun.TryParse(Model, "[a::]5", 0x100, SortedSpan<int>.None, out var parent);
         Model.ObserveAnchorWritten(new NoDataChangeDeltaModel(), "parent", parent);

         Model.ResolveConflicts();
         var pointerRun = Model.GetNextRun(0x080);
         Assert.IsType<PointerRun>(pointerRun);
         Assert.Equal(0x084, pointerRun.Start);
      }

      [Fact]
      public void ChildWithLengthFromParent_ParentWithConstantLength_GetRelatedArraysFindsChild() {
         ViewPort.Edit("@00 .some.constant 5 ");
         ViewPort.Edit("@10 ^parent[a:]some.constant ");
         ViewPort.Edit("@20 ^child[a:]parent ");

         var relatedArrays = Model.GetRelatedArrays((ArrayRun)Model.GetTable("parent")).ToList();

         Assert.Equal(2, relatedArrays.Count);
      }

      [Fact]
      public void AnchorFormat_TableWithNoContent_DoesNotParse() {
         var error = ArrayRun.TryParse(Model, "[]3", 0, SortedSpan<int>.None, out var _);
         Assert.True(error.HasError);
      }

      [Fact]
      public void AnchorFormat_TableStreamWithNoContent_DoesNotParse() {
         var error = ArrayRun.TryParse(Model, "[]!00", 0, SortedSpan<int>.None, out var table);
         Assert.True(error.HasError);
      }

      [Fact]
      public void TableContent_CopyNotWholeTable_DoNotIncludeAnchorContentInClipboard() {
         var nl = Environment.NewLine;
         ViewPort.Edit("^table[a: b:]3 1 2 3 4 5 6 ");

         ViewPort.SelectionStart = new(0, 0);
         ViewPort.SelectionEnd = new(7, 0);
         ViewPort.Copy.Execute(FileSystem);

         Assert.Equal($"1, 2{nl}+3, 4{nl}", FileSystem.CopyText.value);
      }

      [Fact]
      public void NameWithDash_Goto_Goto() {
         CreateTextTable("table", 0x100, "some", "option", "ho-oh", "steve");

         ViewPort.Goto.Execute("ho-oh");

         Assert.Equal(2, ViewPort.Tools.TableTool.CurrentElementSelector.SelectedIndex);
      }

      [Fact]
      public void OptionsWithPercentSign_EnterOption_ValueChanges() {
         Model.SetList(Token, "gender", "10%", "20%", "30%", "40%", "50%", "60%", "70%", "80%", "90%");

         ViewPort.Edit("^table[option:gender]2 30% ");

         Assert.Equal(2, Model[0]);
      }

      [Fact]
      public void OptionsWithAmpersand_EnterOption_ValueChanges() {
         Model.SetList(Token, "gender", "1&2", "3&4", "5&6", "7&8");

         ViewPort.Edit("^table[option:gender]2 5&6 ");

         Assert.Equal(2, Model[0]);
      }

      [Fact]
      public void TableWithTableStreamChild_IncreaseLengthCausesRepoint_NoAssert() {
         SetFullModel(0xFF);
         ViewPort.Edit("@100 <180> <1C0> DEAD @00 <100> 02 @00 ^table[child<[text<\"\">]/count> count.]1 ");

         ViewPort.Edit("@04 3 ");

         Model.ResolveConflicts();
      }

      /// <summary>
      /// Cloning a TableStreamRun should keep the same length as the parent, even in cases where that length is 'wrong'
      /// due to the underlying rom data being different from when the TableStreamRun was created.
      /// </summary>
      [Fact]
      public void TableStreamRun_Clone_SameLength() {
         SetFullModel(0xFF);
         ViewPort.Edit("<100> 01 @00 ^parent[child<[a.]/count> count.]1 ");
         Model.RawData[4] = 2;

         var childTable = (ITableRun)Model.GetNextRun(0x100);
         var clone = (ITableRun)childTable.Duplicate(0x100, childTable.PointerSources);

         Assert.Equal(childTable.ElementCount, clone.ElementCount);
      }

      [Fact]
      public void SingleTableMode_AppendMoveTableStream_SelectionFollowsTable() {
         SetFullModel(0xFF);
         ViewPort.AllowSingleTableMode = true;
         ViewPort.FreeSpaceStart = 0x100;
         ViewPort.Edit("01 02 03 FF DEAD @00 ^table[a.]!FF ");

         ViewPort.Edit("@03 +");

         Assert.Single(Messages);
         Assert.InRange(ViewPort.DataOffset, 0x80, 0x200);
      }

      [Fact]
      public void SingleTableMode_ExtendTableNoRepoint_VisibleSpaceExpands() {
         SetFullModel(0xFF);
         ViewPort.AllowSingleTableMode = true;
         ViewPort.Edit("01 02 03 @00 ^table[a.]!FF ");

         ViewPort.Edit("@03 +");

         Assert.IsType<EndStream>(ViewPort[1, 0].Format);
      }

      [Fact]
      public void TwoTablesWithLengthFromConstant_ExpandOneTable_BothTablesAndConstantChanged() {
         SetFullModel(0xFF);
         Model[0x20] = 2;
         ViewPort.Edit("@20 .constant.value ");
         ViewPort.Edit("@00 ^table1[a.]constant.value ");
         ViewPort.Edit("@10 ^table2[b.]constant.value ");

         ViewPort.Edit("@02 +");

         Assert.Equal(3, Model.GetTable("table1").ElementCount);
         Assert.Equal(3, Model.GetTable("table2").ElementCount);
         Assert.Equal(3, Model[0x20]);
      }

      [Fact]
      public void TwoTablesWithLengthFromContstant_ExpandConstant_BothTablesAndConstantChanged() {
         SetFullModel(0xFF);
         Model[0x20] = 2;
         ViewPort.Edit("@20 .constant.value ");
         ViewPort.Edit("@00 ^table1[a.]constant.value ");
         ViewPort.Edit("@10 ^table2[b.]constant.value ");

         ViewPort.Edit("@20 3 ");

         Assert.Equal(3, Model.GetTable("table1").ElementCount);
         Assert.Equal(3, Model.GetTable("table2").ElementCount);
         Assert.Equal(3, Model[0x20]);
      }

      [Fact]
      public void TableWithSplitterSegment_Copy_SplitterSegmentNotCopied() {
         ViewPort.Edit("^table[a:: b:: | c:: d::]3 ");
         ViewPort.SelectionStart = new(4, 1);
         ViewPort.SelectionEnd = new(11, 1);

         ViewPort.Copy.Execute(FileSystem);

         Assert.Equal("0, 0,", FileSystem.CopyText.value.Trim());
      }

      [Fact]
      public void TableWithHexLength_Parse_NoError() {
         ViewPort.Edit("^table[a:: b::]0x10 ");
         Assert.Equal(8 * 16, Model.GetNextRun(0).Length);
      }

      [Fact]
      public void TableWithHexEnum_Parse_NoErrors() {
         ViewPort.Edit("^table[a::0x10 b::]10 ");
         var table = Model.GetTable("table");
         var seg = (ArrayRunEnumSegment)table.ElementContent[0];
         Assert.Equal(16, seg.GetOptions(Model).Count());
      }

      [Fact]
      public void Table_UpdateLastElement_TableToolUpdates() {
         ViewPort.Edit("^table[a: b:]4 ");

         ViewPort.Goto.Execute(0xF);
         ViewPort.Edit("10 ");

         var field = (FieldArrayElementViewModel)ViewPort.Tools.TableTool.Children.Last();
         Assert.Equal("10", field.Content);
      }

      [Fact]
      public void Table_PasteMultipleFieldsButNoSpaceAtEnd_LastFieldChanged() {
         var editor = new EditorViewModel(FileSystem, InstantDispatch.Instance);
         editor.Add(ViewPort);
         ViewPort.IsFocused = true;
         ViewPort.Edit("^table[a:: b:: c:: d::]4 ");

         FileSystem.CopyText = "1 2 3 4";
         editor.Paste.Execute();

         Assert.Equal(4, Model.ReadMultiByteValue(12, 4));
      }

      private void ArrangeTrainerPokemonTeamData(byte structType, byte pokemonCount, int trainerCount) {
         CreateTextTable(HardcodeTablesModel.PokemonNameTable, 0x180, "ABCDEFGHIJKLMNOP".Select(c => c.ToString()).ToArray());
         CreateTextTable(HardcodeTablesModel.MoveNamesTable, 0x1B0, "qrstuvwxyz".Select(c => c.ToString()).ToArray());
         CreateTextTable(HardcodeTablesModel.ItemsTableName, 0x1D0, "0123456789".Select(c => c.ToString()).ToArray());

         // trainers start at 00. There is room for up to 4.
         // teams start at A0.    There is room for up to 3 pokemon on each team.
         for (int i = 0; i < trainerCount; i++) {
            var initialOffset = i * TrainerPokemonTeamRun.TrainerFormat_Width;
            Model[initialOffset + TrainerPokemonTeamRun.TrainerFormat_StructTypeOffset] = structType;
            Model[initialOffset + TrainerPokemonTeamRun.TrainerFormat_PokemonCountOffset] = pokemonCount;
            Model.WritePointer(new ModelDelta(), initialOffset + TrainerPokemonTeamRun.TrainerFormat_PointerOffset, 0xA0 + 0x30 * i);
         }

         ViewPort.Edit($"@00 ^trainertable[structType.4 class.data.trainers.classes.names introMusic. sprite. name\"\"12 " +
            $"item1:{HardcodeTablesModel.ItemsTableName} item2:{HardcodeTablesModel.ItemsTableName} item3:{HardcodeTablesModel.ItemsTableName} item4:{HardcodeTablesModel.ItemsTableName} " +
            $"doubleBattle:: ai:: pokemonCount:: pokemon<{TrainerPokemonTeamRun.SharedFormatString}>]{trainerCount} @00");
         ViewPort.ResetAlignment.Execute();
      }

      private void ArrangeTrainerPokemonTeamData(byte structType, byte pokemonCount) {
         CreateTextTable(HardcodeTablesModel.PokemonNameTable, 0x100, "ABCDEFGHIJKLMNOP".Select(c => c.ToString()).ToArray());
         CreateTextTable(HardcodeTablesModel.MoveNamesTable, 0x140, "qrstuvwxyz".Select(c => c.ToString()).ToArray());
         CreateTextTable(HardcodeTablesModel.ItemsTableName, 0x180, "0123456789".Select(c => c.ToString()).ToArray());

         Model[TrainerPokemonTeamRun.TrainerFormat_StructTypeOffset] = structType;
         Model[TrainerPokemonTeamRun.TrainerFormat_PokemonCountOffset] = pokemonCount;
         Model.WritePointer(new ModelDelta(), TrainerPokemonTeamRun.TrainerFormat_PointerOffset, 0x60);

         ViewPort.Goto.Execute("00");
         ViewPort.SelectionStart = new Point(4, 2);
         ViewPort.Edit($"^trainertable[team<{TrainerPokemonTeamRun.SharedFormatString}>]1 ");

         ViewPort.Goto.Execute("00");
      }
   }

   public static class TableToolTestExtensions {
      public static FieldArrayElementViewModel GetField(this TableTool table, string name) {
         var child = table.Children.Single(vm => vm is FieldArrayElementViewModel fvm && fvm.Name == name);
         return (FieldArrayElementViewModel)child;
      }
   }
}
