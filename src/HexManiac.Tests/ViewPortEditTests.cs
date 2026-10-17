using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class ViewPortEditTests : BaseViewModelTestClass {
      [Fact]
      public void CanEditData() {
         ViewPort.SelectionStart = new Point(2, 2);
         ViewPort.Edit("A");
         ViewPort.Edit("D");

         Assert.Equal(0xAD, ViewPort[2, 2].Value);
         Assert.Equal(new Point(3, 2), ViewPort.SelectionStart);
      }

      [Fact]
      public void CanEditMultipleBytesInARow() {
         ViewPort.SelectionStart = new Point(2, 2);
         ViewPort.Edit("DEADBEEF");

         Assert.Equal(0xDE, ViewPort[2, 2].Value);
         Assert.Equal(0xAD, ViewPort[3, 2].Value);
         Assert.Equal(0xBE, ViewPort[4, 2].Value);
         Assert.Equal(0xEF, ViewPort[5, 2].Value);
      }

      [Fact]
      public void CanUndoEdit() {
         ViewPort.SelectionStart = new Point(2, 2);
         ViewPort.Edit("AD");
         ViewPort.Undo.Execute();

         Assert.Equal(0x00, ViewPort[2, 2].Value);
      }

      [Fact]
      public void UndoCanReverseMultipleByteChanges() {
         ViewPort.SelectionStart = new Point(2, 2);
         ViewPort.Edit("DEADBEEF");
         ViewPort.Undo.Execute();

         Assert.Equal(0x00, ViewPort[2, 2].Value);
         Assert.Equal(0x00, ViewPort[2, 3].Value);
         Assert.Equal(0x00, ViewPort[2, 4].Value);
         Assert.Equal(0x00, ViewPort[2, 5].Value);
      }

      [Fact]
      public void MovingBetweenChangesCausesSeparateUndo() {
         ViewPort.SelectionStart = new Point(2, 2);
         ViewPort.Edit("01");
         ViewPort.SelectionStart = new Point(2, 3);
         ViewPort.Edit("02");
         ViewPort.Undo.Execute();

         Assert.Equal(0x01, ViewPort[2, 2].Value);
         Assert.Equal(0x00, ViewPort[2, 3].Value);
      }

      [Fact]
      public void CanRedo() {
         ViewPort.Edit("DEADBEEF");
         Assert.False(ViewPort.Redo.CanExecute(null));

         ViewPort.Undo.Execute();
         Assert.True(ViewPort.Redo.CanExecute(null));

         ViewPort.Redo.Execute();
         Assert.False(ViewPort.Redo.CanExecute(null));
      }

      [Fact]
      public void UndoFixesCorrectDataAfterScroll() {
         ViewPort.SelectionStart = new Point(2, 2);
         ViewPort.Edit("FF");

         ViewPort.SelectionStart = new Point(2, 2);
         ViewPort.Edit("EE");
         ViewPort.Scroll.Execute(Direction.Down);
         ViewPort.Undo.Execute();

         Assert.Equal(1, ViewPort.ScrollValue);
         Assert.Equal(0xFF, ViewPort[2, 1].Value);
      }

      [Fact]
      public void EditMovesSelection() {
         ViewPort.SelectionStart = new Point(2, 2);
         ViewPort.Edit("FF");

         Assert.Equal(new Point(3, 2), ViewPort.SelectionStart);
      }

      [Fact]
      public void UndoDoesNotMoveSelection() {
         ViewPort.SelectionStart = new Point(2, 2);
         ViewPort.Edit("FF");
         ViewPort.Undo.Execute();

         Assert.Equal(new Point(3, 2), ViewPort.SelectionStart);
      }

      [Fact]
      public void UndoDoesNotCauseScrolling() {
         ViewPort.SelectionStart = new Point(0, 0);
         ViewPort.Edit("FF");
         ViewPort.Scroll.Execute(Direction.Down);
         ViewPort.Undo.Execute();

         Assert.Equal(1, ViewPort.ScrollValue);
      }

      [Fact]
      public void SingleCharacterEditChangesToUnderEditFormat() {
         ViewPort.SelectionStart = new Point(0, 2);
         ViewPort.Edit("F");

         Assert.IsType<UnderEdit>(ViewPort[0, 2].Format);
         Assert.Equal("F", ((UnderEdit)ViewPort[0, 2].Format).CurrentText);
      }

      [Fact]
      public void UnsupportedCharacterRevertsChangeWithoutAddingUndoOperation() {
         ViewPort.SelectionStart = new Point(0, 2);
         ViewPort.Edit("F");
         ViewPort.Edit("|");

         Assert.IsType<None>(ViewPort[0, 2].Format);
         Assert.Equal(new Point(0, 2), ViewPort.SelectionStart);
         Assert.Equal(0, ViewPort[0, 2].Value);
         Assert.False(ViewPort.Undo.CanExecute(null));
      }

      [Fact]
      public void EscapeRevertsChangeWithoutAddingUndoOperation() {
         ViewPort.SelectionStart = new Point(0, 2);
         ViewPort.Edit("F");
         ViewPort.Edit(ConsoleKey.Escape);

         Assert.IsType<None>(ViewPort[0, 2].Format);
         Assert.Equal(new Point(0, 2), ViewPort.SelectionStart);
         Assert.Equal(0, ViewPort[0, 2].Value);
         Assert.False(ViewPort.Undo.CanExecute(null));
      }

      [Fact]
      public void SelectionChangeDuringEditNotifiesCollectionChange() {
         int collectionNotifications = 0;
         ViewPort.CollectionChanged += (sender, e) => collectionNotifications++;

         ViewPort.SelectionStart = new Point(4, 4);
         ViewPort.Edit("F");
         Assert.Equal(1, collectionNotifications);

         ViewPort.MoveSelectionStart.Execute(Direction.Up);
         Assert.Equal(3, collectionNotifications); // should have been notified since the visual data changed.
      }

      [Fact]
      public void UndoNotifiesCollectionChange() {
         int collectionNotifications = 0;
         ViewPort.CollectionChanged += (sender, e) => collectionNotifications++;

         ViewPort.Edit("0102030405");
         ViewPort.SelectionStart = new Point(0, 1);
         ViewPort.Edit("060708090A");
         collectionNotifications = 0;
         ViewPort.Undo.Execute();

         Assert.Equal(1, collectionNotifications);
      }

      [Fact]
      public void UndoRestoresOriginalDataFormat() {
         var originalFormat = ViewPort[0, 0].Format;
         ViewPort.Edit("ff");
         ViewPort.Undo.Execute();

         Assert.IsType(originalFormat.GetType(), ViewPort[0, 0].Format);
      }

      [Fact]
      public void CanEnterDataAfterLastByte() {
         ViewPort.SelectionStart = new Point(0, 4);
         ViewPort.Edit("00");

         Assert.NotEqual(Undefined.Instance, ViewPort[0, 4].Format);
         Assert.Equal(new Point(1, 4), ViewPort.SelectionStart);
      }

      [Fact]
      public void CanClearData() {
         ViewPort.SelectionStart = new Point(0, 0);
         ViewPort.SelectionEnd = new Point(3, 3);
         ViewPort.Clear.Execute();

         Assert.Equal(0xFF, ViewPort[2, 2].Value);
         Assert.True(ViewPort.Save.CanExecute(new StubFileSystem()));
      }

      [Fact]
      public void CanCopyData() {
         var fileSystem = new StubFileSystem();

         ViewPort.Edit("Cafe Babe");
         ViewPort.SelectionStart = new Point(0, 0);
         ViewPort.SelectionEnd = new Point(3, 0);
         ViewPort.Copy.Execute(fileSystem);

         Assert.Equal("CA FE BA BE", fileSystem.CopyText);
      }

      [Fact]
      public void CanBackspaceOnEdits() {
         for (byte i = 0; i < 255; i++) Data[i] = i;

         ViewPort.SelectionStart = new Point(4, 4); // current value: 0x44
         ViewPort.Edit("C");
         ViewPort.Edit(ConsoleKey.Backspace);

         var editFormat = (UnderEdit)ViewPort[4, 4].Format;
         Assert.Equal(string.Empty, editFormat.CurrentText);

         ViewPort.MoveSelectionStart.Execute(Direction.Down); // any movement should change the value based on what's left in the cell
         Assert.Equal(0xFF, ViewPort[4, 4].Value); // note that the cell was empty, so it got the 'empty' value of FF
      }

      [Fact]
      public void BackspaceBeforeEditChangesPreviousCurrentCell() {
         for (byte i = 0; i < 255; i++) Data[i] = i;

         ViewPort.SelectionStart = new Point(4, 4); // current value: 0x44
         ViewPort.Edit(ConsoleKey.Backspace);
         Assert.Equal(new Point(4, 4), ViewPort.SelectionStart);

         ViewPort.Edit(ConsoleKey.Backspace); // if I hit an arrow key now, it'll give up on the edit and just make the value something reasonable
         ViewPort.Edit(ConsoleKey.Backspace); // but since I hit backspace, it commits the erasure and starts erasing the next cell
         Assert.Equal(0xFF, ViewPort[4, 4].Value);
      }

      [Theory]
      [InlineData("^table[a:: b::]2 ")]
      [InlineData("^table[a: b: c: d:]2 ")]
      [InlineData("^table[b:: p<`ucs4x1x1`>]2 ")]
      public void ReformatTable_SameElementLength_CellsThatAreNoLongerPointersAreNoLongerSources(string newFormat) {
         // the old pointer cells kept listing themselves as sources of their destinations, so the destinations rewrote what is now a plain number when they moved
         ViewPort.Edit("@00 <040> 11 22 33 44 <060> 55 66 77 88 @00 ^table[sprite<`ucs4x1x1`> value::]2 ");
         ViewPort.ChangeHistory.ChangeCompleted();
         Assert.Contains(0, Model.GetNextRun(0x40).PointerSources);
         Assert.Contains(8, Model.GetNextRun(0x60).PointerSources);

         ViewPort.Edit("@00 " + newFormat);

         var table = Assert.IsAssignableFrom<ITableRun>(Model.GetNextRun(0));
         Assert.Equal(8, table.ElementLength);
         Assert.DoesNotContain(0, (Model.GetNextRun(0x40).PointerSources ?? SortedSpan<int>.None).ToList());
         Assert.DoesNotContain(8, (Model.GetNextRun(0x60).PointerSources ?? SortedSpan<int>.None).ToList());
      }

      [Theory]
      [InlineData(0x40)]
      [InlineData(0x44)]
      [InlineData(0x41)]
      public void TypePointerToPalette_DestinationHasOtherPointersWithinThePalette_TheyAreClearedInsteadOfOverlapping(int destination) {
         // the typed pointer adds a 0x20 byte palette: the pointers (and their formats) that were stored in those 0x20 bytes are replaced by it
         ViewPort.Edit("@40 <180> <184> <188> <18C> @100 ^table[pal<`ucp4`>]2 ");
         ViewPort.ChangeHistory.ChangeCompleted();
         Assert.Equal(0x44, Model.GetNextRun(0x44).Start);
         Assert.IsType<PointerRun>(Model.GetNextRun(0x44));

         ViewPort.Edit($"@100 <{destination:X3}> ");

         var palette = Assert.IsType<HavenSoft.HexManiac.Core.Models.Runs.Sprites.PaletteRun>(Model.GetNextRun(destination));
         Assert.Equal(destination, palette.Start);
         Assert.Equal(0x20, palette.Length);
         for (var address = destination + 1; address < destination + 0x20; address++) {
            var inside = Model.GetNextRun(address);
            Assert.True(ReferenceEquals(inside, palette), $"{inside.GetType().Name} at {inside.Start:X} is inside the palette at {destination:X}");
         }

         ViewPort.Undo.Execute();

         Assert.IsType<PointerRun>(Model.GetNextRun(0x44));
         Assert.Equal(0x44, Model.GetNextRun(0x44).Start);
      }

      [Theory]
      [InlineData("ucs4x1x1", false, 0x40)]
      [InlineData("ucs4x1x1", false, 0x44)]
      [InlineData("lzs4x1x1", true, 0x40)]
      [InlineData("lzs4x1x1", true, 0x44)]
      [InlineData("lzp4", true, 0x40)]
      [InlineData("lzp4", true, 0x44)]
      [InlineData("ucs4x1x1", false, 0x41)]
      [InlineData("lzs4x1x1", true, 0x41)]
      [InlineData("lzp4", true, 0x41)]
      public void TypePointerToImageData_DestinationHasOtherPointersWithinTheData_TheyAreClearedInsteadOfOverlapping(string format, bool compressed, int destination) {
         ViewPort.Edit("@40 <180> <184> <188> <18C> <190> <194> @100 ^table[data<`" + format + "`>]2 ");
         ViewPort.ChangeHistory.ChangeCompleted();
         if (compressed) {
            // the data is a valid compressed stream, but the model still thinks of it as a few pointers
            HavenSoft.HexManiac.Core.Models.Runs.Sprites.LZRun.Compress(new byte[0x20]).WriteInto(Model.RawData, destination);
         } else if (destination == 0x41) {
            Model.RawData[0x40] = 0;
         }
         Assert.IsType<PointerRun>(Model.GetNextRun(0x44));

         ViewPort.Edit($"@100 <{destination:X3}> ");

         var created = Model.GetNextRun(destination);
         Assert.Equal(destination, created.Start);
         Assert.False(created is PointerRun || created is NoInfoRun, $"{created.GetType().Name} at {created.Start:X}: the format was not added ({string.Join("|", Errors)})");
         for (var address = destination + 1; address < destination + created.Length; address++) {
            var inside = Model.GetNextRun(address);
            Assert.True(ReferenceEquals(inside, created), $"{inside.GetType().Name} at {inside.Start:X} is inside the {created.GetType().Name} at {destination:X}+{created.Length:X}");
         }

         ViewPort.Undo.Execute();

         Assert.IsType<PointerRun>(Model.GetNextRun(0x44));
         Assert.Equal(0x44, Model.GetNextRun(0x44).Start);
      }

      [Fact]
      public void ClearTableCellWithPointer_DestinationNoLongerListsTheCellAsASource() {
         // a stale source would be rewritten (un-nulling the cell) the next time the destination moves
         ViewPort.Edit("@00 <040> <060> @00 ^table[sprite<`ucs4x1x1`>]2 ");
         ViewPort.ChangeHistory.ChangeCompleted(); // setting up is not something undo should take back
         Assert.Contains(0, Model.GetNextRun(0x40).PointerSources);
         Assert.Equal(0x08000040, Model.ReadMultiByteValue(0, 4));

         ViewPort.Goto.Execute(0);
         ViewPort.SelectionStart = new Point(0, 0);
         ViewPort.SelectionEnd = new Point(3, 0);
         ViewPort.Clear.Execute();

         Assert.DoesNotContain(0, (Model.GetNextRun(0x40).PointerSources ?? SortedSpan<int>.None).ToList());
         var second = Model.GetNextRun(0x60);
         Assert.Equal(0x60, second.Start);
         Assert.Contains(4, (second.PointerSources ?? SortedSpan<int>.None).ToList());
         Assert.Equal(0, Model.ReadMultiByteValue(0, 4));

         Assert.True(ViewPort.Undo.CanExecute(null));
         ViewPort.Undo.Execute();

         Assert.Equal(0x08000040, Model.ReadMultiByteValue(0, 4));
         var first = Model.GetNextRun(0x40);
         Assert.Equal(0x40, first.Start);
         Assert.Contains(0, (first.PointerSources ?? SortedSpan<int>.None).ToList());
         Assert.Equal(0x40, Model.ReadPointer(0));
      }

      [Fact]
      public void ClearStartingInsideAPointerCell_DestinationsNoLongerListTheClearedCells() {
         ViewPort.Edit("@00 <040> <060> @00 ^table[sprite<`ucs4x1x1`>]2 ");

         ViewPort.Goto.Execute(0);
         ViewPort.SelectionStart = new Point(2, 0);
         ViewPort.SelectionEnd = new Point(5, 0);
         ViewPort.Clear.Execute();

         // both pointers are damaged, so neither may keep claiming the cell
         var first = (Model.GetNextRun(0x40).PointerSources ?? SortedSpan<int>.None).ToList();
         var second = (Model.GetNextRun(0x60).PointerSources ?? SortedSpan<int>.None).ToList();
         foreach (var source in first) Assert.Equal(0x40, Model.ReadPointer(source));
         foreach (var source in second) Assert.Equal(0x60, Model.ReadPointer(source));
      }

      [Fact]
      public void ClearRemovesFormats() {
         ViewPort.Edit("<000100>");
         ViewPort.SelectionStart = new Point(2, 0);
         ViewPort.Clear.Execute();

         Assert.Equal(int.MaxValue, Model.GetNextRun(0).Start);
      }

      [Fact]
      public void TypingIntoPointerAutoInsertsStartOfPointer() {
         ViewPort.Edit("<000100>");
         ViewPort.SelectionStart = new Point(0, 0);

         ViewPort.Edit("000060");
         ViewPort.Edit(ConsoleKey.Enter);

         Assert.Equal(0x60, Model[0]);
      }

      [Fact]
      public void TypingTwoHexDigitsIntoPointerWithSpaceCanRemoveFormat() {
         ViewPort.Edit("<000100>");
         ViewPort.SelectionStart = new Point(0, 0);

         ViewPort.Edit("20 ");

         Assert.Equal(0x20, Model[0]);
         Assert.NotInRange(Model.GetNextRun(0).Start, 0, 0x200);
      }

      [Fact]
      public void UnderEditCellsKnowTheirEditLength() {
         ViewPort.Edit("^array[a: b. c. d<>]4 ");

         ViewPort.SelectionStart = new Point(0, 0);
         ViewPort.Edit("2");
         Assert.Equal(2, ((UnderEdit)ViewPort[0, 0].Format).EditWidth);

         ViewPort.SelectionStart = new Point(6, 0);
         ViewPort.Edit("2");
         Assert.Equal(4, ((UnderEdit)ViewPort[4, 0].Format).EditWidth);

         ViewPort.SelectionStart = new Point(8, 6);
         ViewPort.Edit("^");
         Assert.Equal(4, ((UnderEdit)ViewPort[8, 6].Format).EditWidth);
      }

      [Fact]
      public void CanCopyPasteBitArray() {
         var fileSystem = new StubFileSystem();
         CreateTextTable("names", 0x10, "Adam", "Bob", "Carl", "David", "Evan", "Fred", "Greg", "Holly", "Iggy", "Jay", "Kelly", "Lucy", "Mary", "Nate", "Ogre", "Phil"); // 0x60
         CreateEnumTable("enums", 0x80, "names", 12.Range().ToArray());
         CreateBitArrayTable("bitArray", 0xB0, "enums", 0x01, 0x02, 0x04, 0x08, 0x10, 0x20, 0x40, 0x80);

         // verify that copy works
         ViewPort.Goto.Execute("00");
         ViewPort.SelectionStart = new Point(0x4, 0xB);
         ViewPort.Copy.Execute(fileSystem);
         Assert.Contains("Carl", fileSystem.CopyText);

         // verify that direct entry works
         ViewPort.Edit("0100");
         Assert.Equal(new Point(0x6, 0xB), ViewPort.SelectionStart);
         Assert.Equal(0x0001, Model.ReadMultiByteValue(0xB4, 2));

         // verify that direct entry works with spaces
         ViewPort.Edit("02 03");
         Assert.Equal(0x0302, Model.ReadMultiByteValue(0xB6, 2));
      }

      [Fact]
      public void PlmRunsContainingNamesWithSpacesCanBeCorrectlyEditedWithTheTextTool() {
         var tool = ViewPort.Tools.StringTool;
         var delta = new ModelDelta();
         CreateTextTable(HardcodeTablesModel.MoveNamesTable, 0x00, "Option0", "Option1", "Option2 More", "Option3 More", "Option4");
         for (int i = 0; i < 4; i++) Model.WriteMultiByteValue(0x50 + i * 2, 2, delta, 0x0201);
         Model.WriteMultiByteValue(0x58, 2, new ModelDelta(), 0xFFFF);
         ViewPort.Goto.Execute("50");
         ViewPort.Edit("^sample`plm` ");

         Assert.Equal(SplitLines("1 Option1;1 Option1;1 Option1;1 Option1"), tool.Content);

         tool.Content = SplitLines("1 Option1;2 \"Option2 More\";2 Option4;2 Option4");

         Assert.Equal(SplitLines("1 Option1;2 \"Option2 More\";2 Option4;2 Option4"), tool.Content);

         var plm = (PLMRun)Model.GetNextRun(0x50);
         Assert.Equal(10, plm.Length);

         Assert.Equal(0x0201, Model.ReadMultiByteValue(0x50, 2));
         Assert.Equal(0x0402, Model.ReadMultiByteValue(0x52, 2));
         Assert.Equal(0x0404, Model.ReadMultiByteValue(0x54, 2));
         Assert.Equal(0x0404, Model.ReadMultiByteValue(0x56, 2));
      }

      /// <summary>
      /// when you copy the start of a table, it also copies the table's anchor
      /// if you paste this somewhere else, it will change the anchor name to be something new
      /// if you cut/paste, then the original anchor name is no longer in use, so the original name is used in the new spot.
      /// if you copy/paste at the original location, it should be a no-op.
      /// normally, copying from a table includes the + operator, in case you're wanting to paste into a shorter table.
      /// But the first element shouldn't have the +, to avoid possible conflicts with tables ending directly before the new table starts.
      /// </summary>
      [Fact]
      public void CopyFirstTableElementDoesNotIncludeAppendCharacter() {
         var fileSystem = new StubFileSystem();
         CreateTextTable("names", 0x10, "Adam", "Bob", "Carl", "David", "Evan", "Fred", "Greg", "Holly", "Iggy", "Jay", "Kelly", "Lucy", "Mary", "Nate", "Ogre", "Phil"); // 0x60

         // copy
         ViewPort.Goto.Execute("00");
         ViewPort.SelectionStart = new Point(0, 1);
         ViewPort.SelectionEnd = new Point(5, 1); // select the first entry
         ViewPort.Copy.Execute(fileSystem);

         // check
         Assert.DoesNotContain("+", fileSystem.CopyText);
      }

      /// <summary>
      /// A bit array's length is based on two other tables:
      /// (1) the table of entries, for example the pokemon
      /// (2) the table of bits, for example a TM list
      /// If the first changes, you need one more entry, possible a few bytes.
      /// If the second changes, you need to extend EACH entry, possibly 1 byte per entry.
      ///
      /// Verify that changing (1) does not extend the bits.
      /// </summary>
      [Fact]
      public void ExtendingPokemonNameArrayDoesNotChangeBitsInBitArray() {
         CreateTextTable("names", 0x0, "ABCDEFGHIJKLMNOP".ToCharArray().Select(c => c.ToString()).ToArray()); // 16 entries, 2 bytes each
         CreateEnumTable("enums", 0x40, "names", 0, 1, 2, 3); // note that the enum uses 4 bits
         ViewPort.Goto.Execute("80");
         ViewPort.Edit("^bits[pokemon|b[]enums]names "); // each entry is 1 byte long, 16 entries.

         // precondition
         var bitArray = (ArrayRunBitArraySegment)((ArrayRun)Model.GetNextRun(0x80)).ElementContent[0];
         Assert.Equal(1, bitArray.Length);

         // extend the name table with a new entry
         ViewPort.Goto.Execute("20");
         ViewPort.Edit("+");

         // postcondition
         bitArray = (ArrayRunBitArraySegment)((ArrayRun)Model.GetNextRun(0x80)).ElementContent[0];
         Assert.Equal(1, bitArray.Length);
      }

      [Fact]
      public void ClearTableClearsCurrentItemOnly() {
         CreateTextTable("table", 0, "A B C D E F G".Split(' '));
         ViewPort.SelectionStart = new Point(4, 0);
         ViewPort.Clear.Execute();

         Assert.IsType<PCS>(ViewPort[2, 0].Format);
      }

      [Fact]
      public void BackspaceEditsSingleCellWhenNoFormats() {
         Model.ObserveRunWritten(new ModelDelta(), new AsciiRun(Model, 0x20, 0x10));
         Model[0] = 0xA0;
         Model[1] = 0xA1;
         Model[2] = 0xA2;

         ViewPort.SelectionStart = new Point(1, 0);
         ViewPort.Edit(ConsoleKey.Backspace);

         Assert.Equal(0xA2, Model[2]);
      }

      [Fact]
      public void ClearFormatOnNamedAnchorRemovesAnchorButDoesNotClearFalsePointers() {
         ViewPort.Edit("<000004> ^fake ");

         var items = ViewPort.GetContextMenuItems(new Point(4, 0));
         var clear = items.Single(item => item.Text.Contains("Clear Format"));
         clear.Command.Execute();

         Assert.Equal(4, Model[0]);
      }

      [Fact]
      public void CanIncludeCommentsInPasteScripts() {
         var nl = Environment.NewLine;
         ViewPort.Edit($"FF @00 ^text\"\" Some # Comment Here {nl}Text");
         var matches = ViewPort.Find("Some Text");
         Assert.Single(matches);
      }

      [Fact]
      public void CanEndCommentWithPound() {
         ViewPort.Edit($"FF @00 ^text\"\" Some # Comment Here #Text");
         var matches = ViewPort.Find("Some Text");
         Assert.Single(matches);
      }

      [Fact]
      public void HexSegment_Copy_CopyHexBytes() {
         var filesystem = new StubFileSystem();
         ArrayRun.TryParse(Model, "[some: data:|h]4", 0, default, out var arrayRun);
         Model.ObserveRunWritten(ViewPort.CurrentChange, arrayRun);
         ViewPort.Refresh();

         ViewPort.SelectionStart = new Point(2, 0);
         ViewPort.Copy.Execute(filesystem);

         Assert.Equal("0000", filesystem.CopyText.value.Trim());
      }

      [Fact]
      public void Freespace_WriteBlankViaMetacommand_DataIsWritten() {
         SetFullModel(0xFF);

         ViewPort.Edit("@!00(32) ");

         Assert.All(32.Range(), i => Assert.Equal(0x00, Model[i]));
      }

      [Fact]
      public void NonEmptyModel_WriteBlankViaMetacommand_Fail() {
         ViewPort.Edit("@!00(32) ");

         Assert.Single(Errors);
      }

      [Fact]
      public void ViewPort_Align_MoveSelection() {
         ViewPort.MoveSelectionStart.Execute(Direction.Right);
         ViewPort.Edit(".align 2 ");
         Assert.Equal(new Point(2, 0), ViewPort.SelectionStart);
      }

      [Fact]
      public void ViewPort_TextDirective_DoesNothing() {
         ViewPort.Edit(".text ");

         Assert.Empty(Errors);
         Assert.Empty(Messages);
         Assert.Equal(new Point(), ViewPort.SelectionStart);
         Assert.IsType<None>(ViewPort[0, 0].Format);
      }

      [Fact]
      public void ViewPort_ThumbDirective_DoesNothing() {
         ViewPort.Edit(".thumb ");

         Assert.Empty(Errors);
         Assert.Empty(Messages);
         Assert.Equal(new Point(), ViewPort.SelectionStart);
         Assert.IsType<None>(ViewPort[0, 0].Format);
      }

      [Fact]
      public void ViewPort_Edit_RaiseUndoChanged() {
         bool lastCanExecuteResult = ViewPort.Undo.CanExecute(default);
         ViewPort.Undo.CanExecuteChanged += (sender, e) => lastCanExecuteResult = ViewPort.Undo.CanExecute(default);

         ViewPort.Edit("11 ");

         Assert.True(lastCanExecuteResult);
      }

      [Fact]
      public void AddConstant_Undo_NoConstant() {
         Model.SetUnmappedConstant(ViewPort.CurrentChange, "bob", 3);

         ViewPort.Undo.Execute();

         Assert.False(Model.TryGetUnmappedConstant("bob", out var _));
      }

      [Fact]
      public void EditingNumberCellOnBottomRow_SelectDown_CompleteCurrentChange() {
         ViewPort.Edit("@0F0 ^table[a:: b:: c:: d::]1 @000 "); // make a table
         ViewPort.SelectionStart = new Point(0, 15); // select bottom row
         ViewPort.Edit("3");                         // start an edit

         ViewPort.MoveSelectionStart.Execute(Direction.Down); // scroll down

         var table = Model.GetTable("table");
         Assert.Equal(3, Model[table.Start]);
      }

      [Theory]
      [InlineData("^anchor[[]] ")]
      public void Anchor_BadFormat_NoCrash(string text) {
         ViewPort.Edit(text);
         Assert.Single(Errors);
      }

      [Fact]
      public void Constant_TwoBytes_HasRightFormat() {
         var word = new WordRun(0, "name", 2, 0, 1);
         Assert.Equal(":", word.FormatString);
      }

      [Fact]
      public void CursorAtEndOfFile_DeleteThenGoto_CannotUndo() {
         ViewPort.Goto.Execute(0x1FF);
         ViewPort.MoveSelectionStart.Execute(Direction.Right);

         ViewPort.Clear.Execute();
         ViewPort.Goto.Execute(0);

         Assert.False(ViewPort.Undo.CanExecute(default));
      }

      [Fact]
      public void SelectMultipleBytesIncludingEndOfFile_Delete_NoEdit() {
         ViewPort.Goto.Execute(0x1FF);
         ViewPort.MoveSelectionEnd.Execute(Direction.Right);

         ViewPort.Clear.Execute();

         Assert.Equal(0x200, Model.RawData.Length);
      }
   }
}
