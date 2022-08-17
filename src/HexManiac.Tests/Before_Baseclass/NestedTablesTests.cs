using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class NestedTablesTests {
      private readonly ViewPort viewPort;
      private readonly PokemonModel model;
      private readonly ModelDelta token = new ModelDelta();
      private readonly byte[] data = new byte[0x200];
      private readonly List<string> messages = new List<string>();
      private readonly List<string> errors = new List<string>();

      public NestedTablesTests() {
         model = new PokemonModel(data);
         viewPort = new ViewPort("file.txt", model, InstantDispatch.Instance) { Width = 0x10, Height = 0x10 };
         viewPort.OnError += (sender, e) => errors.Add(e);
         viewPort.OnMessage += (sender, e) => messages.Add(e);
      }

      [Fact]
      public void SupportNestedTextStreams() {
         // can parse
         var info = ArrayRun.TryParse(model, "[description<\"\">]4", 0, null, out var table);
         Assert.False(info.HasError);

         model.ObserveAnchorWritten(token, "table", table);

         // displays pointers
         viewPort.Goto.Execute("000100");
         viewPort.Goto.Execute("000000");
         Assert.IsType<Pointer>(viewPort[4, 0].Format);

         // can't update to point to non-text
         viewPort.SelectionStart = new Point(5, 0);
         errors.Clear();
         viewPort.Edit("000100 "); // expected failure point
         Assert.Single(errors);

         // can update to point to text
         viewPort.SelectionStart = new Point(0, 4);
         viewPort.Edit("FF"); // have to put the FF first, or trying to create a text run will fail
         viewPort.SelectionStart = new Point(0, 4);
         viewPort.Edit("^text\"\" Hello World!\"");
         viewPort.SelectionStart = new Point(5, 0);
         errors.Clear();
         viewPort.Edit("000040 ");
         Assert.Empty(errors);

         // can update to point to null
         viewPort.SelectionStart = new Point(5, 0);
         errors.Clear();
         viewPort.Edit("null ");
         Assert.Empty(errors);
      }

      /// <summary>
      /// The user may want to update a pointer to a location where they know there is text,
      /// even if there's no text recognized there.
      /// In this case, there could be a pointer to NoInfoRun, or there could be no pointer at all.
      /// Either way, accept the change and automatically add the run based on what's pointed to.
      /// </summary>
      [Fact]
      public void SupportChangingPointerToWhereAFormatCouldBe() {
         viewPort.Edit("FF"); // have to put the FF first, or trying to create a text run will fail
         viewPort.MoveSelectionStart.Execute(Direction.Left);
         viewPort.Edit("^text1\"\" Hello World!\"");
         viewPort.SelectionStart = new Point(0, 0);
         viewPort.Edit("^ "); // remove the format. So we have text data, but no format for it.

         viewPort.SelectionStart = new Point(0, 2);
         viewPort.Edit("FF"); // have to put the FF first, or trying to create a text run will fail
         viewPort.MoveSelectionStart.Execute(Direction.Left);
         viewPort.Edit("^text2\"\" Other Text\"");
         viewPort.SelectionStart = new Point(0, 2);
         viewPort.Edit("^text2 "); // remove the format, but keep the anchor

         viewPort.SelectionStart = new Point(0, 4);
         viewPort.Edit("^table[description<\"\">]4 ");
         viewPort.Goto.Execute("000000");  // scroll back: making a table auto-scrolled
         viewPort.SelectionStart = new Point(0, 4);
         errors.Clear();

         // test 1: pointing to data that is the right format, but with no anchor, works
         viewPort.Edit("000000 ");
         Assert.Empty(errors);
         Assert.IsType<PCS>(viewPort[1, 0].Format);

         // test 2: pointing to data that is the right format, but with a NoInfo anchor, works
         viewPort.Edit("text2 ");
         Assert.Empty(errors);
         Assert.IsType<PCS>(viewPort[1, 2].Format);
      }

      /// <summary>
      /// When a run is added, automatically add children runs as needed.
      /// Note that removing this run doesn't remove the children run.
      /// </summary>
      [Fact]
      public void SupportAutomaticallyAddingFormatsBasedOnPointerFormat() {
         viewPort.Edit("FF"); // have to put the FF first, or trying to create a text run will fail
         viewPort.MoveSelectionStart.Execute(Direction.Left);
         viewPort.Edit("^text\"\" Some Text\"");
         viewPort.SelectionStart = new Point(0, 0);
         viewPort.Edit("^ "); // remove the anchor. Keeps the data though.

         viewPort.SelectionStart = new Point(0, 4);
         viewPort.Edit("00 00 00 08");
         viewPort.SelectionStart = new Point(0, 4);
         viewPort.Edit("^table[description<\"\">]4 "); // adding this should automatically add a format at 000000, since a pointer points there.

         viewPort.Goto.Execute("000000"); // scroll back to top
         Assert.IsType<PCSRun>(model.GetNextRun(0));
      }

      [Fact]
      public void AddTableSuchThatPointerLeadsToBadDataDoesNotAddFormat() {
         viewPort.Edit("10 00 00 08"); // point to second line, which is all zeros
         viewPort.SelectionStart = new Point(0, 0);
         viewPort.Edit("^table[description<\"\">]4 "); // adding this tries to add a format at the second line, but realizes that it's the wrong format, so it doesn't.

         Assert.IsType<None>(viewPort[1, 1].Format); // no PCS format was added
      }

      [Fact]
      public void TableToolAllowsEditingTextContent() {
         viewPort.Edit("FF"); // have to put the FF first, or trying to create a text run will fail
         viewPort.MoveSelectionStart.Execute(Direction.Left);
         viewPort.Edit("^text\"\" Some Text\"");

         viewPort.SelectionStart = new Point(0, 4);
         viewPort.Edit("^table[description<\"\">]4 <000000>"); // note that this auto-scrolls, since a table was created
         viewPort.MoveSelectionStart.Execute(Direction.Left);  // select the pointer we just completed

         Assert.Equal(3, viewPort.Tools.TableTool.Children.Count); // header, pointer, content
         Assert.IsType<TextStreamElementViewModel>(viewPort.Tools.TableTool.Children[2]);
      }

      [Fact]
      public void UpdateViaToolStreamFieldThatCausesMoveAlsoUpdatesToolPointerField() {
         viewPort.Edit("FF 00 23"); // put a valid end, then a spare byte, then some junk. This'll cause a move after adding enough characters.
         viewPort.SelectionStart = new Point(0, 0);
         viewPort.Edit("^text\"\" ?\"");

         viewPort.SelectionStart = new Point(0, 4);
         viewPort.Edit("^table[description<\"\">]4 <000000>"); // note that this auto-scrolls, since a table was created
         viewPort.SelectionStart = new Point(0, 0);
         var textViewModel = (TextStreamElementViewModel)viewPort.Tools.TableTool.Children.Single(child => child is TextStreamElementViewModel);

         // act: use the tool to change the content, forcing a repoint
         messages.Clear();
         textViewModel.Content = "Xyz";
         var pointerViewModel = (FieldArrayElementViewModel)viewPort.Tools.TableTool.Children.Single(child => child is FieldArrayElementViewModel);

         Assert.Single(messages);                               // we repointed
         Assert.NotEqual("<000000>", pointerViewModel.Content); // other tool field was updated
      }

      [Fact]
      public void SupportsUnnamedPlmStreams() {
         model.WriteMultiByteValue(0x100, 2, new ModelDelta(), 0x0202); // move two at level 1
         model.WriteMultiByteValue(0x102, 2, new ModelDelta(), 0x1404); // move four at level 10 (8+2, shifted once -> 14)
         model.WriteMultiByteValue(0x104, 2, new ModelDelta(), 0xFFFF); // end stream
         viewPort.Edit("<000100>"); // setup something to point at 000100, so we can have an unnamed stream there

         SetupMoveTable(0x20);
         viewPort.Goto.Execute("000100");

         errors.Clear();
         viewPort.Edit("^`plm`");

         Assert.Empty(errors);
         var run = model.GetNextRun(0x100);
         Assert.IsType<PLMRun>(run);
         Assert.Equal(6, run.Length);
         var format = (PlmItem)viewPort[1, 0].Format;
         Assert.Equal(1, format.Level);
         Assert.Equal(2, format.Move);
         Assert.Equal("Two", format.MoveName);

         viewPort.SelectionStart = new Point(1, 0);
         Assert.True(viewPort.IsSelected(new Point(0, 0)));
      }

      [Fact]
      public void PlmStreamAutocomplete() {
         SetupMoveTable(0x20); // goes from 20 to 60
         viewPort.Goto.Execute("000070");

         viewPort.Edit("FFFF");
         viewPort.SelectionStart = new Point(0, 0);
         viewPort.Edit("^stream`plm` 3 T"); // Two, Three
         var format = (UnderEdit)viewPort[0, 0].Format;
         Assert.Equal(2, format.AutocompleteOptions.Count);
      }

      [Fact]
      public void PlmStreamBackspaceWorks() {
         model.WriteMultiByteValue(0x100, 2, new ModelDelta(), 0x0202); // move two at level 1
         model.WriteMultiByteValue(0x102, 2, new ModelDelta(), 0x1404); // move four at level 10 (8+2, shifted once -> 14)
         model.WriteMultiByteValue(0x104, 2, new ModelDelta(), 0xFFFF); // end stream
         viewPort.Edit("<000100>"); // setup something to point at 000100, so we can have an unnamed stream there
         SetupMoveTable(0x20);
         viewPort.Goto.Execute("000100");
         errors.Clear();
         viewPort.Edit("^`plm`");

         viewPort.Goto.Execute("000102");
         viewPort.Edit(ConsoleKey.Backspace);
         var format = (UnderEdit)viewPort[2, 0].Format;
         Assert.Equal("10 Fou", format.CurrentText);
      }

      [Fact]
      public void PlmStreamsAppearInTextTool() {
         SetupMoveTable(0x20);
         SetupPlmStream(0x70, 4);
         viewPort.Goto.Execute("000000");

         viewPort.SelectionStart = new Point(2, 7);
         viewPort.FollowLink(2, 7);

         Assert.Equal(viewPort.Tools.IndexOf(viewPort.Tools.StringTool), viewPort.Tools.SelectedIndex);
         var itemCount = viewPort.Tools.StringTool.Content.Split(Environment.NewLine).Length;
         Assert.Equal(4, itemCount);

         viewPort.Tools.StringTool.ContentIndex = 17; // third line
         Assert.True(viewPort.IsSelected(new Point(4, 7)));
         Assert.True(viewPort.IsSelected(new Point(5, 7)));
      }

      [Fact]
      public void PlmStreamGetsStreamNameInAddressBarAndTextToolIfPointedToFromAMatchedLengthTable() {
         // setup text tables
         SetupNameTable(0x40);
         SetupMoveTable(0x80);

         // setup pointers that will eventually be in the lvlmoves table
         viewPort.Goto.Execute("000000");
         viewPort.Edit("<000100><000110><000120><000130><000140><000150><000160><000170>");

         // setup data that lvlmoves pointers will point to
         for (int i = 0x100; i < 0x180; i += 0x10) SetupPlmStream(i, 6, withName: false);

         // add lvlmoves table
         viewPort.Goto.Execute("000000");
         viewPort.Edit($"^{HardcodeTablesModel.LevelMovesTableName}[data<`plm`>]{HardcodeTablesModel.PokemonNameTable} ");

         viewPort.Goto.Execute("000110"); // jump to the anchor for Bob's moves
         Assert.Contains($"{HardcodeTablesModel.LevelMovesTableName}/Bob/data", viewPort.SelectedElementName);
      }

      [Fact]
      public void CanSearchForTextWithinPlmStream() {
         // setup text tables
         SetupNameTable(0x40);
         SetupMoveTable(0x80);

         // setup pointers that will eventually be in the lvlmoves table
         viewPort.Goto.Execute("000000");
         viewPort.Edit("<000100><000110><000120><000130><000140><000150><000160><000170>");

         // setup data that lvlmoves pointers will point to
         for (int i = 0x100; i < 0x180; i += 0x10) SetupPlmStream(i, 6, withName: false);

         // add lvlmoves table
         viewPort.Goto.Execute("000000");
         viewPort.Edit($"^{HardcodeTablesModel.LevelMovesTableName}[data<`plm`>]{HardcodeTablesModel.PokemonNameTable} ");

         var results = viewPort.Find("Three");
         Assert.Equal(9, results.Count); // the actual text + entries for the 8 pokemon
      }

      [Fact]
      public void EditPlmInMainViewUpdatesTextTool() {
         SetupMoveTable(0x00);
         SetupPlmStream(0x50, 8);

         viewPort.Goto.Execute("000000");
         viewPort.SelectionStart = new Point(2, 5); // should select '2 One'
         viewPort.Edit("3 One ");

         Assert.IsNotType<UnderEdit>(viewPort[2, 5].Format);
         Assert.Contains("3 One", viewPort.Tools.StringTool.Content);
      }

      [Fact]
      public void AutoCompletePlmMoveNameContainingSpaceWorksWithNoQuotes() {
         SetupMoveTable(0x00);
         viewPort.SelectionStart = new Point(0, 1); // start of move "Two"
         viewPort.Edit("Bob Par");
         SetupPlmStream(0x50, 8);

         viewPort.Goto.Execute("000000");
         viewPort.SelectionStart = new Point(2, 5); // should select '2 One'
         viewPort.Edit("2 bobpar ");

         Assert.IsNotType<UnderEdit>(viewPort[2, 5].Format);
         Assert.Contains("2 \"Bob Par\"", viewPort.Tools.StringTool.Content);
      }

      [Fact]
      public void MultilinePlmPasteWorks() {
         SetupMoveTable(0x00);
         SetupPlmStream(0x50, 2);
         viewPort.Edit(@"@000050
4 Three
6 Five
10 Zero
[]
");

         Assert.Equal(0b0000100_000000011, model.ReadMultiByteValue(0x50, 2));
         Assert.Equal(0b0000110_000000101, model.ReadMultiByteValue(0x52, 2));
         Assert.Equal(0b0001010_000000000, model.ReadMultiByteValue(0x54, 2));
         Assert.Equal(0xFFFF, model.ReadMultiByteValue(0x56, 2));
      }

      [Fact]
      public void ChoosingAutoCompleteOptionClosesPlmEdit() {
         SetupMoveTable(0x00);
         viewPort.SelectionStart = new Point(0, 1); // start of move "Two"
         viewPort.Edit("Bob Par");
         SetupPlmStream(0x50, 8);

         viewPort.Goto.Execute("000000");
         viewPort.SelectionStart = new Point(2, 5); // should select '2 One'
         viewPort.Edit("3 \"Bo");

         var format = (UnderEdit)viewPort[2, 5].Format;
         viewPort.Autocomplete(format.AutocompleteOptions[0].CompletionText);

         Assert.IsNotType<UnderEdit>(viewPort[2, 5].Format);
         Assert.Contains("3 \"Bob Par\"", viewPort.Tools.StringTool.Content);
      }

      [Fact]
      public void CanUseBitArraysInFormats() {
         SetupMoveTable(0x00);
         SetupNameTable(0x40);

         // setup a table for 5 tutor moves
         viewPort.Goto.Execute("000080");
         viewPort.Edit($"^tutormoves[move:{HardcodeTablesModel.MoveNamesTable}]5 One Two Four Five Seven ");

         // create a table that uses tutormoves as a bit array. Note 5 moves should take up 1 byte, so the overall table should by 8 bytes long (because there are 8 pokemon)
         viewPort.Goto.Execute("0000090");
         viewPort.Edit($"^table[moves|b[]tutormoves]{HardcodeTablesModel.PokemonNameTable} ");
         var run = (ArrayRun)model.GetNextRun(0x90);
         var segment = (ArrayRunBitArraySegment)run.ElementContent[0];
         Assert.Equal("tutormoves", segment.SourceArrayName);
         Assert.Equal(8, run.Length);

         var bitList = (BitListArrayElementViewModel)viewPort.Tools.TableTool.Children.Single(child => child is BitListArrayElementViewModel);
         Assert.Equal("Four", bitList[2].BitLabel);

         bitList[2].IsChecked = true;      // "Adam" should be able to learn "Four"
         Assert.Equal(0x04, model[0x90]);  // the third bit up is set because "Four" is the first tutor move

         Assert.IsType<BitArray>(((Anchor)viewPort[0, 0].Format).OriginalFormat);
      }

      [Fact]
      public void BitArraySelectionSelectsAllBytesInCurrentBitArray() {
         SetupMoveTable(0x00);
         SetupNameTable(0x40);

         // setup a table for 5 tutor moves
         viewPort.Goto.Execute("000080");
         viewPort.Edit($"^tutormoves[move:{HardcodeTablesModel.MoveNamesTable}]10 One One Two Two Four Four Five Five Seven Seven "); // note that 10 bits takes 2 bytes

         viewPort.Goto.Execute("0000100");
         viewPort.Edit($"^table[moves|b[]tutormoves]{HardcodeTablesModel.PokemonNameTable} ");
         var run = (ArrayRun)model.GetNextRun(0x100);
         Assert.Equal(16, run.Length); // 2 bytes each for 8 pokemon

         viewPort.SelectionStart = new Point(4, 0);
         Assert.True(viewPort.IsSelected(new Point(5, 0)));
      }

      [Fact]
      public void CanExpandBitArrays() {
         // Arrange a table with 8 elements
         // and a second table that uses those elements as bits
         SetupMoveTable(0x00);
         viewPort.Edit($"@40 ^mymoves[move:{HardcodeTablesModel.MoveNamesTable}]8 "); // setup a table that uses 'movenames' as an enum
         viewPort.Edit($"@60 ^table[data|b[]mymoves]4 @61 FF "); // set all 8 name bits to true for the table[1]    // 60 - 64

         // Act: expand the enum table to have 9 entries
         viewPort.Edit("@50 +");

         // Assert that the 8 true bits moved based on the expansion, and the new table uses 2 bytes per element.
         Assert.Equal(0xFF, data[0x62]);
         Assert.Equal(8, model.GetNextRun(0x60).Length);
      }

      /// <summary>
      /// You can not only make an enum out of a string table, but also out of an index list matching the length of a string table
      /// </summary>
      [Fact]
      public void EnumFromEnumFromStringUsesIndexValueAsOptions() {
         SetupNameTable(0x180);
         viewPort.Edit($"@00 ^enum1[value.]{HardcodeTablesModel.PokemonNameTable}-1 2 3 4 5 6 7 1 ");
         using (ModelCacheScope.CreateScope(model)) {
            var options = ModelCacheScope.GetCache(model).GetOptions("enum1");
            Assert.Equal("Horton", options[1]); // option 0 should be Horton because enum[Horton] = 0
            Assert.Equal("Bob", options[2]);
            Assert.Equal("Carl", options[3]);
            Assert.Equal("Dave", options[4]);
            Assert.Equal("Elen", options[5]);
            Assert.Equal("Fred", options[6]);
            Assert.Equal("Gary", options[7]);
            Assert.Equal(8, options.Count);
         }
      }

      [Fact]
      public void TableWithTableStreamPointers_RepointToMiddleOfAnotherRun_NoAssert() {
         var test = new BaseViewModelTestClass();
         test.Model[5] = 5;
         test.ViewPort.Edit("@100 ^table[ptr<[a.]!05>]2 <000> ");

         test.ViewPort.Edit("<002> ");

         var streamRun = (TableStreamRun)test.Model.GetNextRun(0);
         Assert.Equal(0, streamRun.Start);
         Assert.Equal(6, streamRun.Length);
      }

      [Fact]
      public void TableWithTableStreamPointers_RepointToContainingAnotherRun_NoAssert() {
         var test = new BaseViewModelTestClass();
         test.Model[5] = 5;
         test.ViewPort.Edit("@100 ^table[ptr<[a.]!05>]2 <002> ");

         test.ViewPort.Edit("<000> ");

         var streamRun = (TableStreamRun)test.Model.GetNextRun(0);
         Assert.Equal(0, streamRun.Start);
         Assert.Equal(6, streamRun.Length);
      }

      // creates a move table that is 0x40 bytes long
      private void SetupMoveTable(int start) {
         viewPort.Goto.Execute(start.ToString("X6"));
         viewPort.Edit("^" + HardcodeTablesModel.MoveNamesTable + "[name\"\"8]8 Zero\" One\" Two\" Three\" Four\" Five\" Six\" Seven\"");
      }

      // creates a name table that is 0x40 bytes long
      private void SetupNameTable(int start) {
         viewPort.Goto.Execute(start.ToString("X6"));
         viewPort.Edit("^" + HardcodeTablesModel.PokemonNameTable + "[name\"\"8]8 Adam\" Bob\" Carl\" Dave\" Elen\" Fred\" Gary\" Horton\"");
      }

      // creates a plm stream named 'stream' that is <0x20 bytes long. Length should be <9.
      private void SetupPlmStream(int start, int length, bool withName = true) {
         // make sure we terminate first. This will move as needed, but it allows us to add the stream cleanly by just editing inline.
         model.WriteMultiByteValue(start, 2, new ModelDelta(), 0xFFFF);
         viewPort.Goto.Execute(start.ToString("X6"));
         if (withName) viewPort.Edit("^stream`plm`");
         else viewPort.Edit("^`plm`");
         for (int i = 0; i < length; i++) {
            viewPort.Edit($"{i + 1} {i} ");
         }
      }
   }
}
