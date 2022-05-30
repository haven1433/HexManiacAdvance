using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class PointerModelTests : BaseViewModelTestClass {
      [Fact]
      public void PointerModelFindsNoPointersInRandomData() {
         var rnd = new Random(0xCafe);
         var buffer = new byte[0x10000]; // 64KB
         rnd.NextBytes(buffer);
         for (int i = 0; i < buffer.Length; i++) if (buffer[i] == 0x08) buffer[i] = 0x10;

         var model = new PokemonModel(buffer);

         Assert.Equal(NoInfoRun.NullRun, model.GetNextRun(0));
      }

      [Fact]
      public void PointerModelFindsPointersInRange() {
         var rnd = new Random(0xCafe);
         var buffer = new byte[0x10000]; // 64KB
         rnd.NextBytes(buffer);
         for (int i = 0; i < buffer.Length; i++) if (buffer[i] == 0x08) buffer[i] = 0x10;

         // write two specific pointers
         var model = new PokemonModel(buffer);
         var token = new ModelDelta();
         model.WritePointer(token, 0x204, 0x4050);
         model.WritePointer(token, 0x4070, 0x101C);
         model = new PokemonModel(buffer);

         Assert.Equal(0x204, model.GetNextRun(0).Start);
         Assert.IsType<PointerRun>(model.GetNextRun(0x206));

         Assert.IsType<NoInfoRun>(model.GetNextRun(0x208));
         Assert.Single(model.GetNextRun(0x400).PointerSources);

         Assert.Equal(0x4050, model.GetNextRun(0x4050).Start);
         Assert.Equal(4, model.GetNextRun(0x4071).Length);
      }

      [Fact]
      public void PointerModelFindsSelfReferences() {
         var buffer = new byte[0x20];
         var model = new PokemonModel(buffer);
         var token = new ModelDelta();
         model.WritePointer(token, 0xC, 0xC);
         model = new PokemonModel(buffer);

         var run = model.GetNextRun(0);
         var nextRun = model.GetNextRun(run.Start + run.Length);

         Assert.NotNull(run);
         Assert.Equal(NoInfoRun.NullRun, nextRun);
      }

      [Fact]
      public void PointerModelMergesDuplicates() {
         var buffer = new byte[0x20];
         var model = new PokemonModel(buffer);
         var token = new ModelDelta();
         model.WritePointer(token, 0x0C, 0x14);
         model.WritePointer(token, 0x1C, 0x14);
         model = new PokemonModel(buffer);

         var run = model.GetNextRun(0x14);
         Assert.Equal(2, run.PointerSources.Count);
      }

      [Fact]
      public void ModelUpdatesWhenViewPortChanges() {
         ViewPort.Edit("<000020>");

         Assert.Equal(0, Model.GetNextRun(0).Start);
         Assert.Equal(0x20, Model.GetNextRun(10).Start);
      }

      [Fact]
      public void WritingNamedAnchorFollowedByPointerToNameWorks() {
         ViewPort.SelectionStart = new Point(0, 1);
         ViewPort.Edit("^bob ");
         ViewPort.SelectionStart = new Point(0, 2);
         ViewPort.Edit("<bob>");

         Assert.Equal(ViewPort.Width, ViewPort[0, 2].Value);
      }

      [Fact]
      public void WritingPointerToNameFollowedByNamedAnchorWorks() {
         ViewPort.SelectionStart = new Point(0, 2);
         ViewPort.Edit("<bob>");
         ViewPort.SelectionStart = new Point(0, 1);
         ViewPort.Edit("^bob ");

         Assert.Equal(ViewPort.Width, ViewPort[0, 2].Value);
      }

      [Fact]
      public void CanWriteAnchorToSameLocationAsPointerWithoutRemovingPointer() {
         ViewPort.SelectionStart = new Point(0, 2);
         ViewPort.Edit("<000040>");
         ViewPort.SelectionStart = new Point(0, 2);
         ViewPort.Edit("^bob ");

         Assert.IsType<Anchor>(ViewPort[0, 2].Format);
      }

      [Fact]
      public void CanWriteAnchorToSameLocationAsPointerPointingToThatAnchor() {
         ViewPort.SelectionStart = new Point(0, 2);
         ViewPort.Edit("<bob>");
         ViewPort.SelectionStart = new Point(0, 2);
         ViewPort.Edit("^bob ");

         Assert.IsType<Anchor>(ViewPort[0, 2].Format);
         Assert.Equal(0x20, ViewPort[0, 2].Value);
      }

      [Fact]
      public void WritingAnAnchorUpdatesPointersToUseThatName() {
         ViewPort.SelectionStart = new Point(0, 2);
         ViewPort.Edit("<000010>");
         ViewPort.SelectionStart = new Point(0, 1);
         ViewPort.Edit("^bob ");

         Assert.Equal("bob", ((Pointer)ViewPort[0, 2].Format).DestinationName);
      }

      [Fact]
      public void WritingAPointerOverlappingAPointerRemovesOriginalPointer() {
         var buffer = new byte[0x100];
         var model = new PokemonModel(buffer);
         var token = new ModelDelta();

         model.WritePointer(token, 16, 100);
         model.ObserveRunWritten(token, new PointerRun(16));
         Assert.Equal(16, model.GetNextRun(10).Start);
         Assert.Equal(16, model.GetNextRun(17).Start);
         Assert.Equal(16, model.GetNextRun(19).Start);
         Assert.Equal(100, model.GetNextRun(20).Start); // the reference at 100 has been added

         model.ClearFormat(token, 14, 4);
         model.WritePointer(token, 14, 200);
         model.ObserveRunWritten(token, new PointerRun(14));
         Assert.Equal(14, model.GetNextRun(10).Start);
         Assert.Equal(14, model.GetNextRun(15).Start);
         Assert.Equal(14, model.GetNextRun(16).Start);
         Assert.Equal(14, model.GetNextRun(17).Start);
         Assert.Equal(200, model.GetNextRun(18).Start); // the reference at 100 has been erased, and there's a new one at 200
      }

      [Fact]
      public void WritingAnchorIntoAPointerRemovesThatPointer() {
         var buffer = new byte[0x100];
         var model = new PokemonModel(buffer);
         var token = new ModelDelta();

         model.WritePointer(token, 16, 12);
         model.ObserveRunWritten(token, new PointerRun(16));
         model.ObserveAnchorWritten(token, "bob", new NoInfoRun(18));

         Assert.Equal(18, model.GetNextRun(10).Start);
      }

      [Fact]
      public void WritingOverAnAnchorDeletesThatAnchor() {
         var buffer = new byte[0x100];
         var model = new PokemonModel(buffer);
         var token = new ModelDelta();

         model.WritePointer(token, 16, 32);
         model.ObserveRunWritten(token, new PointerRun(16));

         model.ClearFormat(token, 30, 4);   // this format clear removes the anchor that was auto-generated from the pointer. Which means the data from before must not be a pointer.
         model.WritePointer(token, 30, 64);
         model.ObserveRunWritten(token, new PointerRun(30));

         Assert.Equal(30, model.GetNextRun(10).Start); // original pointer at 16 is no longer there. The new first data is the anchor at 30
         Assert.Equal(32, model.ReadPointer(16));      // but the data at 16 still looks like a pointer: only the format is gone
         Assert.Equal(64, model.GetNextRun(34).Start); // next data is the anchor from the pointer at 30
      }

      [Fact]
      public void PointerCanPointToNameAfterThatNameGetsDeleted() {
         var viewPort = ViewPort;
         var token = new ModelDelta();

         viewPort.SelectionStart = new Point(0, 1);
         viewPort.Edit("^bob ");
         viewPort.SelectionStart = new Point(0, 2);
         viewPort.Edit("<bob>");
         viewPort.SelectionStart = new Point(0, 1);

         // as an alternative to being able to delete an anchor from the viewPort,
         // just edit the model directly and then scroll to force the viewPort to refresh
         Model.ClearFormat(token, 0x10, 1);
         viewPort.ScrollValue = 1;
         viewPort.ScrollValue = 0;

         Assert.Equal("bob", ((Pointer)viewPort[0, 2].Format).DestinationName);
      }

      [Fact]
      public void PointerGetsSetToZeroAfterAnchorGetsDeleted() {
         var viewPort = ViewPort;
         var token = new ModelDelta();

         viewPort.SelectionStart = new Point(0, 1);
         viewPort.Edit("^bob ");
         viewPort.SelectionStart = new Point(0, 2);
         viewPort.Edit("<bob>");
         viewPort.SelectionStart = new Point(0, 1);

         // as an alternative to being able to delete an anchor from the viewPort,
         // just edit the model directly and then scroll to force the viewPort to refresh
         Model.ClearFormatAndData(token, 0xF, 2);
         viewPort.ScrollValue = 1;
         viewPort.ScrollValue = 0;

         Assert.Equal(Pointer.NULL, ((Pointer)viewPort[0, 2].Format).Destination);
      }

      [Fact]
      public void AnchorCarriesSourceInformation() {
         var viewPort = ViewPort;

         viewPort.SelectionStart = new Point(0, 1);
         viewPort.Edit("<000020>");
         var anchor = (Core.ViewModels.DataFormats.Anchor)viewPort[0, 2].Format;
         Assert.Contains(16, anchor.Sources);
      }

      [Fact]
      public void StartingAnAnchorAndGivingItNoNameClearsAnyAnchorNameAtThatPosition() {
         var viewPort = ViewPort;

         viewPort.SelectionStart = new Point(0, 1);
         viewPort.Edit("<bob>");
         viewPort.SelectionStart = new Point(0, 2);
         viewPort.Edit("^bob ");
         viewPort.Edit("^ ");

         var format = (Pointer)viewPort[0, 1].Format;
         Assert.Equal(0x20, format.Destination);
         Assert.Equal(string.Empty, format.DestinationName);
         var address = Model.GetAddressFromAnchor(new ModelDelta(), -1, string.Empty);
         Assert.Equal(Pointer.NULL, address);
      }

      [Fact]
      public void CanRemoveAnchorWithNoReferences() {
         var viewPort = ViewPort;

         viewPort.SelectionStart = new Point(0, 2);
         viewPort.Edit("^bob ^ ");

         Assert.Equal(NoInfoRun.NullRun, Model.GetNextRun(0));
      }

      [Fact]
      public void BackspaceClearsDataButNotFormats() {
         var viewPort = ViewPort;

         viewPort.SelectionStart = new Point(0, 1);
         viewPort.Edit("01 02 03 04");                // 2x4 characters to clear
         viewPort.Edit("<000020>");                   // 8 characters to clear
         viewPort.Edit("<000030>");                   // 8 characters to clear
         viewPort.SelectionStart = new Point(10, 1);  // within <000030>

         for (int i = 0; i < 21; i++) viewPort.Edit(ConsoleKey.Backspace); // should clear both pointers (16) and 2 bytes (4)
         viewPort.MoveSelectionStart.Execute(Direction.Up);

         Assert.Equal(Pointer.NULL, ((Pointer)viewPort[8, 1].Format).Destination);
         Assert.Equal(Pointer.NULL, ((Pointer)viewPort[4, 1].Format).Destination);
         Assert.Equal(0x01, viewPort[0, 1].Value);
         Assert.Equal(0x00, viewPort[1, 1].Value); // committed a '0' when the selection moved, because a '0' was all that was left
         Assert.Equal(0xFF, viewPort[2, 1].Value);
         Assert.Equal(0xFF, viewPort[3, 1].Value);
      }

      [Fact]
      public void WritingOverTwoPointersWorks() {
         var viewPort = ViewPort;

         viewPort.SelectionStart = new Point(0, 1);
         viewPort.Edit("<000020>");
         viewPort.Edit("<000030>");
         viewPort.SelectionStart = new Point(2, 1); // note that this will select all four bytes of <000020>
         viewPort.Clear.Execute();  // this should clear the data and formatting of the first pointer
         viewPort.SelectionStart = new Point(2, 1); // this selects just the one byte
         viewPort.Edit("<000040>"); // this should remove the second pointer

         Assert.Equal(0xFF, viewPort[0, 1].Value);
         Assert.Equal(0xFF, viewPort[1, 1].Value);
         Assert.Equal(0x40, viewPort[2, 1].Value);
         Assert.Equal(0x00, viewPort[3, 1].Value);
         Assert.Equal(0x00, viewPort[4, 1].Value);
         Assert.Equal(0x08, viewPort[5, 1].Value);
         Assert.Equal(0x00, viewPort[6, 1].Value); // leftover data from the pointer <000030> when we 'fixed' its format
         Assert.Equal(0x08, viewPort[7, 1].Value);

         Assert.IsNotType<Pointer>(viewPort[1, 1].Format);
         Assert.IsType<Pointer>(viewPort[2, 1].Format);
         Assert.IsType<Pointer>(viewPort[5, 1].Format);
         Assert.IsNotType<Pointer>(viewPort[6, 1].Format);

         // verify that there is no anchor at 20 and 30, but there is an anchor at 40
         Assert.Equal(0x40, Model.GetNextAnchor(0x20).Start);
      }

      [Fact]
      public void PointerToUnknownLocationShowsUpDifferent() {
         var viewPort = ViewPort;

         viewPort.SelectionStart = new Point(2, 1);
         viewPort.Edit("<bob>");

         var pointer = (Pointer)viewPort[2, 1].Format;
         Assert.Equal("bob", pointer.DestinationName);
         Assert.Equal(Pointer.NULL, pointer.Destination);
      }

      [Fact]
      public void AddingANewNamedPointerToNoLocationOverExistingNamedPointerToNoLocationWorks() {
         var viewPort = ViewPort;

         viewPort.SelectionStart = new Point(2, 1);
         viewPort.Edit("<bob>");

         viewPort.SelectionStart = new Point(0, 1);
         viewPort.Edit("<tom>");

         Assert.IsNotType<Pointer>(viewPort[4, 1].Format);
      }

      [Fact]
      public void CanGotoAnchorName() {
         var viewPort = ViewPort;

         int errorCalls = 0;
         viewPort.OnError += (sender, e) => errorCalls++;

         viewPort.SelectionStart = new Point(2, 1);
         viewPort.Edit("^bob ");
         viewPort.Goto.Execute("bob");

         Assert.Equal(0, errorCalls);
      }

      [Fact]
      public void CanFindPointer() {
         var viewPort = ViewPort;

         viewPort.SelectionStart = new Point(4, 1);
         viewPort.Edit("<000058> 23 19");
         var results = viewPort.Find("<000058> 23 19");

         Assert.Single(results);
      }

      [Fact]
      public void CanUsePointerAsLink() {
         var viewPort = ViewPort;

         viewPort.SelectionStart = new Point(4, 1);
         viewPort.Edit("<000120>");
         viewPort.FollowLink(4, 1);

         Assert.Equal("000120", viewPort.Headers[0]);
      }

      [Fact]
      public void FindAllSourcesWorks() {
         var viewPort = ViewPort;
         var editor = new EditorViewModel(new StubFileSystem());
         editor.Add(viewPort);

         viewPort.SelectionStart = new Point(4, 1);
         viewPort.Edit("<000120>");
         viewPort.FollowLink(4, 1);

         viewPort.FindAllSources(0, 0);

         Assert.Equal(1, editor.SelectedIndex);
      }

      [Fact]
      public void NewAnchorWithSameNameDoesNotMovePointersToNewAnchor() {
         var viewPort = ViewPort;

         // put some pointers in the file
         viewPort.SelectionStart = new Point(4, 1);
         viewPort.Edit("<bob>");
         viewPort.SelectionStart = new Point(4, 3);
         viewPort.Edit("<bob>");

         // make them point somewhere real
         viewPort.SelectionStart = new Point(8, 2);
         viewPort.Edit("^bob ");

         // make a new anchor with the same name (should auto-rename)
         viewPort.SelectionStart = new Point(8, 4);
         viewPort.Edit("^bob ");

         Assert.Equal(0x28, ((Pointer)viewPort[4, 1].Format).Destination);
         Assert.Equal(0x28, ((Pointer)viewPort[4, 3].Format).Destination);
      }

      [Fact]
      public void CanCopyAndPastePointers() {
         var fileSystem = new StubFileSystem();
         var viewPort = ViewPort;

         viewPort.SelectionStart = new Point(0, 2);

         viewPort.Edit("<000058>");
         viewPort.Edit("FF FF");
         viewPort.SelectionStart = new Point(0, 2);
         viewPort.SelectionEnd = new Point(5, 2);

         viewPort.Copy.Execute(fileSystem);
         Assert.Equal("<000058> FF FF", fileSystem.CopyText);
      }

      [Fact]
      public void CanWriteNullPointer() {
         var buffer = new byte[0x200];
         var model = new PokemonModel(buffer);
         var token = new ModelDelta();

         model.ObserveRunWritten(token, new PointerRun(0x10));

         var format = (Pointer)model.GetNextRun(0x10).CreateDataFormat(model, 0x10);
         Assert.Equal("null", format.DestinationName);
      }

      [Fact]
      public void CanWriteNameOverNullPointer() {
         var viewPort = ViewPort;

         viewPort.Edit("<null>");
         viewPort.SelectionStart = new Point(0, 0);
         viewPort.Edit("<bob>");

         var format = (Pointer)Model.GetNextRun(0x0).CreateDataFormat(Model, 0x00);
         Assert.Equal("bob", format.DestinationName);
      }

      [Fact]
      public void FormatClearDoesNotClearAnchorIfUnnamedAnchorIsAtStartOfClear() {
         var buffer = new byte[0x200];
         var model = new PokemonModel(buffer);
         var token = new ModelDelta();

         model.WritePointer(token, 0, 0x10);
         model.ObserveRunWritten(token, new PointerRun(0));
         model.ClearFormat(token, 0x10, 1);

         Assert.Equal(0x10, model.GetNextRun(0x10).Start);
      }

      [Fact]
      public void ArrowMovementWhileTypingAnchorCommitsChange() {
         var viewPort = ViewPort;

         viewPort.Edit("^bob"); // no trailing space: still under edit

         viewPort.SelectionStart = new Point(1, 1);

         Assert.IsNotType<None>(viewPort[0, 0].Format);
      }

      [Fact]
      public void EscapeWhileTypingAnchorCancelsChange() {
         var viewPort = ViewPort;

         viewPort.Edit("^bob"); // no trailing space: still under edit
         viewPort.Edit(ConsoleKey.Escape);

         Assert.IsType<None>(viewPort[0, 0].Format);
         Assert.Equal(NoInfoRun.NullRun, Model.GetNextRun(0));
      }

      [Fact]
      public void StartingAnAnchorOverAnAnchorClearsTheExistingAnchorInfo() {
         var viewPort = ViewPort;

         viewPort.Edit("^bob ");
         Assert.Equal("^bob", viewPort.AnchorText);

         viewPort.Edit("^");

         var format = (UnderEdit)viewPort[0, 0].Format;
         Assert.Equal("^", format.CurrentText);
         Assert.Equal("^", viewPort.AnchorText);
      }

      [Fact]
      public void AnchorEditTextUpdatesWithSelectionChange() {
         var viewPort = ViewPort;
         Model.ObserveAnchorWritten(new ModelDelta(), "bob", new NoInfoRun(0x08));

         viewPort.SelectionStart = new Point(0x08, 0);

         Assert.True(viewPort.AnchorTextVisible);
         Assert.Equal("^bob", viewPort.AnchorText);
      }

      [Fact]
      public void AnchorEditTextUpdatesWhenTypingAnAnchor() {
         var viewPort = ViewPort;

         viewPort.Edit("^partialTe"); // in the middle of typing 'partialText'

         Assert.True(viewPort.AnchorTextVisible);
         Assert.Equal("^partialTe", viewPort.AnchorText);
      }

      [Fact]
      public void ModifyingAnchorTextUpdatesTheAnchor() {
         SetFullModel(0xFF);
         var viewPort = ViewPort;
         Model.ObserveAnchorWritten(new ModelDelta(), "bob", new NoInfoRun(0x08));

         viewPort.SelectionStart = new Point(0x08, 0);
         viewPort.AnchorText = "^bob\"\"";

         Assert.IsType<PCSRun>(Model.GetNextRun(0x08));
      }

      [Fact]
      public void AnchorTextAlwaysCoercesToStartWithAnchorCharacter() {
         var viewPort = ViewPort;
         Model.ObserveAnchorWritten(new ModelDelta(), "bob", new NoInfoRun(0x08));

         viewPort.SelectionStart = new Point(0x08, 0);
         viewPort.AnchorText = "tom\"\"";

         Assert.Equal("^tom\"\"", viewPort.AnchorText); // not that the ^ was added to the front
      }

      [Fact]
      public void GivenTwoPointersCanRemoveAndUndoTheFirstWithoutEffectingTheSecond() {
         var (model, viewPort) = (Model, ViewPort);
         model.WritePointer(new ModelDelta(), 0x010, 0x100);
         model.ObserveRunWritten(new ModelDelta(), new PointerRun(0x010));
         model.WritePointer(new ModelDelta(), 0x014, 0x120);
         model.ObserveRunWritten(new ModelDelta(), new PointerRun(0x014));

         viewPort.SelectionStart = new Point(0, 2); // 0x010
         viewPort.Edit("00"); // this should remove the first pointer
         viewPort.Undo.Execute();

         Assert.Equal(0x014, model.GetNextRun(0x014).Start);
      }

      [Fact]
      public void CreatingAnOffsetPointerShouldCoerceToTheStartOfExistingPointer() {
         var (model, viewPort) = (Model, ViewPort);

         viewPort.Edit("<000100>");
         viewPort.SelectionStart = new Point(2, 0);
         viewPort.Edit("<000050>");

         // note that the second pointer should be right where the first one was
         // because trying to start a pointer edit mid-pointer should move you to the start of the pointer.
         Assert.Equal(0x50, Model[0]);
      }

      [Fact]
      public void CreatingAPointerOutsideTheDataRangeErrors() {
         var (model, viewPort) = (Model, ViewPort);

         viewPort.Edit("<000400>");

         Assert.Single(Errors);
      }

      [Fact]
      public void ClearingAPointerAlsoRemovesItsAnchor() {
         var (model, viewPort) = (Model, ViewPort);

         viewPort.Edit("<000100>");
         model.ClearFormat(new ModelDelta(), 0x00, 4);

         Assert.NotInRange(model.GetNextRun(0x00).Start, 0, Model.Count);
      }

      [Fact]
      public void TypingBracesOnDataTriesToInterpretThatDataAsPointer() {
         var (model, viewPort) = (Model, ViewPort);

         viewPort.Edit("00 01 00 08");
         viewPort.SelectionStart = new Point(0, 0);
         viewPort.Edit("<>");   // typing this should interpret the bytes as a pointer and add it.

         Assert.Equal(0x100, ((Pointer)viewPort[0, 0].Format).Destination);
      }

      [Fact]
      public void StartingPointerEditAndThenMovingClearsAllEditedCells() {
         var (model, viewPort) = (Model, ViewPort);

         viewPort.Edit("<");
         viewPort.SelectionStart = new Point(2, 2);

         for (int x = 0; x < 0x10; x++) {
            for (int y = 0; y < 0x10; y++) {
               Assert.IsNotType<UnderEdit>(viewPort[x, y].Format);
            }
         }
      }

      [Fact]
      public void AddingAnchorShouldSearchForPointersToThatLocation() {
         var (model, viewPort) = (Model, ViewPort);
         var change = new ModelDelta();
         model.WritePointer(change, 0x23, 0x050); // a pointer that isn't 4-byte aligned, pointing to data that is
         model.WritePointer(change, 0x10, 0x087); // a pointer that is 4-byte aligned, but pointing to something that isn't
         model.WritePointer(change, 2, 0xA2);    // a pointer that isn't 4-byte aligned, pointing to something not 4-byte aligned

         // got to 50 and write an anchor
         viewPort.SelectionStart = new Point(0x0, 0x5);
         viewPort.Edit("^test1 ");
         Assert.IsType<Pointer>(viewPort[0x3, 0x2].Format);
         Assert.Single(((Anchor)viewPort[0x0, 0x5].Format).Sources);

         // go to 87 and write an anchor
         viewPort.SelectionStart = new Point(0x7, 0x8);
         viewPort.Edit("^test2 ");
         Assert.IsType<Pointer>(viewPort[0x0, 0x1].Format);
         Assert.Single(((Anchor)viewPort[0x7, 0x8].Format).Sources);

         // go to A2 and write an anchor
         viewPort.SelectionStart = new Point(0x2, 0xA);
         viewPort.Edit("^test3 ");
         Assert.IsType<Pointer>(viewPort[0x2, 0x0].Format);
         Assert.Single(((Anchor)viewPort[0x2, 0xA].Format).Sources);
      }

      [Fact]
      public void ReplacingAPointerWithAnAnchorKeepsKnowledgeOfThatPointer() {
         StandardSetup(out var data, out var model, out var viewPort);
         viewPort.Edit("<000040><000040><000040>");
         viewPort.Goto.Execute("000004");
         viewPort.Edit("^bob "); // adding the anchor will remove the pointer

         Assert.Equal(3, model.GetNextRun(0x40).PointerSources.Count); // since the pointer was removed, the anchor should only have 2 things pointing to it.
      }

      [Fact]
      public void ClearingAnAnchorFormatShouldRemovePointersToTheAnchorButNotPointersToThosePointers() {
         StandardSetup(out var data, out var model, out var viewPort);

         viewPort.Edit("<000010> <000000>");
         viewPort.SelectionStart = new Point(0xE, 0);
         viewPort.Edit("<000020>"); // this overwrites (and removes) the anchor at 000010

         var run = model.GetNextRun(0);
         Assert.Equal(0, run.Start);    // there should still be an anchor at 000000, even though it's not a pointer anymore
         Assert.IsType<NoInfoRun>(run);

         run = model.GetNextRun(4);
         Assert.Equal(4, run.Start);    // there should still be a pointer a 000004 that points to 000000
         Assert.IsType<PointerRun>(run);
      }

      [Fact]
      public void AnchorWithNoFormatContextMenuContainsRemoveOption() {
         var test = new BaseViewModelTestClass();
         test.ViewPort.Edit("<000020> @20 "); // add a pointer to 0x20 and go there

         test.ViewPort.SelectionStart = new Point();
         var contextItems = test.ViewPort.GetContextMenuItems(new Point());
         var clearFormatItem = contextItems.Single(item => item.Text == "Clear Format");
         clearFormatItem.Command.Execute();

         // verify that there is no longer a run starting at 0x20
         Assert.NotEqual(0x20, test.Model.GetNextRun(0x20).Start);
      }

      [Fact]
      public void AnchorCanContaintDots() {
         var test = new BaseViewModelTestClass();
         test.ViewPort.Edit("^This.name.Is.valid ");
         Assert.Empty(test.Errors);
         var address = test.Model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, "This.name.Is.valid");
         Assert.Equal(0, address);

         test.ViewPort.AnchorText = "^This_name_is_Also_valid";
         Assert.Empty(test.Errors);
         address = test.Model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, "This_name_is_Also_valid");
         Assert.Equal(0, address);
      }

      [Fact]
      public void OffsetPointersWork() {
         StandardSetup(out var data, out var model, out var _);

         data[0] = 0x60;
         data[3] = 8;
         model.ObserveRunWritten(new ModelDelta(), new OffsetPointerRun(0, 0x30));

         Assert.Equal(0x08000060, model.ReadMultiByteValue(0, 4));
         Assert.Equal(0x30, model.ReadPointer(0));
         Assert.IsType<NoInfoRun>(model.GetNextRun(0x30));
         Assert.Equal(0x30, model.GetNextRun(0x30).Start);
      }

      [Fact]
      public void CanUndoOffsetPointerChange() {
         StandardSetup(out var data, out var model, out var viewPort);
         var metaInfo = new StubMetadataInfo { VersionNumber = string.Empty };

         data[0] = 0x60;
         data[3] = 8;
         model.ObserveRunWritten(viewPort.CurrentChange, new OffsetPointerRun(0, 0x30));

         Assert.Single(model.ExportMetadata(metaInfo).OffsetPointers);

         viewPort.Undo.Execute();

         var metadata = model.ExportMetadata(metaInfo);
         Assert.Empty(metadata.OffsetPointers);
      }

      [Fact]
      public void CanCreateOffsetPointerFromViewModel() {
         StandardSetup(out var _, out var model, out var viewPort);
         viewPort.Edit("<000100+20>");
         Assert.Equal(0x08000120, model.ReadMultiByteValue(0, 4));
         Assert.Equal(0x100, model.ReadPointer(0));
      }

      [Fact]
      public void CanOffsetPointToAnchor() {
         StandardSetup(out var _, out var model, out var viewPort);

         model.WritePointer(viewPort.CurrentChange, 0, 0x10);
         model.ObserveAnchorWritten(viewPort.CurrentChange, "bob", new NoInfoRun(0x20));
         model.ObserveRunWritten(viewPort.CurrentChange, new OffsetPointerRun(0, -0x10)); // with a value of 0x10 and an offset of 0x10, this should now point to bob

         // move the anchor
         model.ClearFormat(viewPort.CurrentChange, 0x20, 1); // clear the anchor
         viewPort.Edit("@40 ^bob ");

         // pointer should point to the new location (with offset)
         Assert.Equal(0x08000030, model.ReadValue(0));
         Assert.Equal(0x40, model.ReadPointer(0));
      }

      [Fact]
      public void Anchor_CreateOffsetPointer_NoError() {
         StandardSetup(out var _, out var model, out var viewPort);

         viewPort.Edit("@40 ^anchor @0 <anchor+2>");

         Assert.Equal(0x08000042, model.ReadValue(0));
         Assert.Equal(0x40, model.ReadPointer(0));
      }

      [Fact]
      public void RunSelected_EditAnchor_AnchorChanges() {
         ViewPort.Edit("^text`asc`8 @04 ");

         ViewPort.AnchorText = "^text`asc`12 ";

         Assert.Empty(Errors);
         Assert.Equal(0, Model.GetNextRun(0).Start);
         Assert.Equal(12, Model.GetNextRun(0).Length);
      }

      [Fact]
      public void PointerTable_Delete_PointerIsNull() {
         SetFullModel(0xFF);
         ViewPort.Edit("@100 ^text1\"\" Adam\"");
         ViewPort.Edit("@120 ^text2\"\" Bob\"");
         ViewPort.Edit("@140 ^text3\"\" Carl\"");
         ViewPort.Edit("@160 ^text4\"\" Dave\"");
         ViewPort.Edit("@00 <text1> <text2> <text3> <text4> @00 ^table[text<\"\">]4 ");

         ViewPort.SelectionStart = new Point(4, 0);
         ViewPort.Clear.Execute();

         Assert.Equal(Model.GetAddressFromAnchor(ViewPort.CurrentChange, -1, "text1"), Model.ReadPointer(0));
         Assert.Equal(Pointer.NULL, Model.ReadPointer(4));
      }

      [Fact]
      public void PointerTable_DeleteMultiplePointers_OnlyThoseAreNull() {
         SetFullModel(0xFF);
         ViewPort.Edit("@100 ^text1\"\" Adam\"");
         ViewPort.Edit("@120 ^text2\"\" Bob\"");
         ViewPort.Edit("@140 ^text3\"\" Carl\"");
         ViewPort.Edit("@160 ^text4\"\" Dave\"");
         ViewPort.Edit("@00 <text1> <text2> <text3> <text4> @00 ^table[text<\"\">]4 ");

         ViewPort.SelectionStart = new Point(4, 0);
         ViewPort.SelectionEnd = new Point(11, 0);
         ViewPort.Clear.Execute();

         Assert.Equal(Model.GetAddressFromAnchor(ViewPort.CurrentChange, -1, "text1"), Model.ReadPointer(0));
         Assert.Equal(Pointer.NULL, Model.ReadPointer(4));
         Assert.Equal(Pointer.NULL, Model.ReadPointer(8));
         Assert.Equal(Model.GetAddressFromAnchor(ViewPort.CurrentChange, -1, "text4"), Model.ReadPointer(12));
      }

      [Fact]
      public void SelfReferentialPointer_Delete_NoError() {
         ViewPort.Edit("@010 <010> @010 ");

         ViewPort.Clear.Execute();

         Assert.Equal(-1, Model.ReadMultiByteValue(0x10, 4));
         Assert.IsType<None>(ViewPort[0, 0].Format);
      }

      [Fact]
      public void PointerToTextWithinTable_DeepCopy_OnlyCopyOneElement() {
         SetFullModel(0xFF);
         ViewPort.Edit("@100 ^names^[content\"\"7]4 adam\" bob\" carl\" dave\"");
         ViewPort.Edit("@000 @!00(16) ^table[pointer<>]4 <names/0> <names/1> <names/2> <names/3>");

         ViewPort.Goto.Execute(8);
         ViewPort.DeepCopy.Execute(FileSystem);

         Assert.Contains("carl", FileSystem.CopyText.value);
         Assert.DoesNotContain("bob", FileSystem.CopyText.value);
         Assert.DoesNotContain("dave", FileSystem.CopyText.value);
      }

      [Fact]
      public void Bytes_WriteSameBytesAsPointer_ModelDoesNotThinkBytesWereChanged() {
         Model.RawData[3] = 0x08;

         ViewPort.Edit("<000>");

         Assert.All(4.Range(), i => Assert.False(Model.HasChanged(i)));
      }

      [Fact]
      public void UnmappedPointerInMetadata_Load_NullPointer() {
         var pointers = new [] { new StoredUnmappedPointer(0x20, "name") };
         var metadata = new StoredMetadata(unmappedPointers: pointers);

         var model = new PokemonModel(new byte[0x200], metadata, Singletons);

         var run = model.GetNextRun(0x20);
         Assert.IsType<PointerRun>(run);
         Assert.Equal(0x20, run.Start);
      }

      [Fact]
      public void UnmappedPointerInMetadata_WriteAnchor_ModelUpdates() {
         var pointers = new[] { new StoredUnmappedPointer(0x20, "name") };
         var metadata = new StoredMetadata(unmappedPointers: pointers);

         var model = new PokemonModel(new byte[0x200], metadata, Singletons);
         model.ObserveAnchorWritten(Token, "name", new NoInfoRun(0x80));

         Assert.Equal(0x80, model.ReadPointer(0x20));
      }

      [Fact]
      public void PointerWithAnchor_CheckAnchorText_IsFromThisPointerNotDestination() {
         ViewPort.Edit("<100> ^destination @100 <004> ");

         ViewPort.Goto.Execute(0x100);

         Assert.Equal("^", ViewPort.AnchorText);
      }

      [Fact]
      public void PointerWithAnchor_WriteAnchorIntoViewPort_EditsPointer() {
         ViewPort.Edit("<100> @100 <180> @100 ");

         ViewPort.AnchorText = "^some.name";

         Assert.Equal("some.name", Model.GetAnchorFromAddress(-1, 0x100));
      }

      [Fact]
      public void OffsetPointerOutOfRange_InitMetadata_NoOffsetPointer() {
         var metadata = new StoredMetadata(offsetPointers: new[] { new StoredOffsetPointer(0x300, 1) });

         var model = New.PokemonModel(Data, metadata, Singletons);

         Assert.Equal(int.MaxValue, model.GetNextRun(0).Start);
      }

      [Fact]
      public void OffsetPointerOutOfRange_LoadMetadata_NoOffsetPointer() {
         var metadata = new StoredMetadata(offsetPointers: new[] { new StoredOffsetPointer(0x300, 1) });

         Model.LoadMetadata(metadata);

         Assert.Equal(int.MaxValue, Model.GetNextRun(0).Start);
      }

      [Fact]
      public void RawBytes_WriteSameBytesAsOffsetPointer_MetadataOnlyChange() {
         Model.WritePointer(Token, 0, 0x104);
         Model.ObserveAnchorWritten(Token, "anchor", new NoInfoRun(0x100));
         ViewPort.Refresh();

         ViewPort.Edit("<anchor+4>");

         Assert.True(ViewPort.IsMetadataOnlyChange);
      }

      [Fact]
      public void DefaultAnchorNameInPuse_CreateDefaultAnchor_UsesDifferentName() {
         SetFullModel(0xFF);
         ViewPort.Edit("@20 ^misc.temp._000100 @00!00(8) ^table[ptr<>]2 <100> ");

         ViewPort.Goto.Execute("100");
         ViewPort.SelectionEnd = new(3, 0);
         ViewPort.Cut(FileSystem);

         Assert.Equal(0x20, Model.GetAddressFromAnchor(new(), -1, "misc.temp._000100")); // old anchor is still there
         Assert.Equal("^misc.temp._000100_1 FF FF FF FF", FileSystem.CopyText.value);    // copy includes new anchor
         Assert.Equal(new[] { 0x00 }, Model.GetUnmappedSourcesToAnchor("misc.temp._000100_1")); // address 0 should point to the new anchor
      }

      [Fact]
      public void TempAnchor_ExportMetadata_DoNotIncludeTempAnchor() {
         ViewPort.Edit("^misc.temp.stuff ");

         var metadata = Model.ExportMetadata(Singletons.MetadataInfo);

         var anchors = metadata.NamedAnchors.Select(anchor => anchor.Name).ToList();
         Assert.DoesNotContain("misc.temp.stuff", anchors);
      }

      [Fact]
      public void PointerToTempAnchor_ExportMetadata_DoIncludeUnmappedPointer() {
         ViewPort.Edit("<misc.temp.stuff>");

         var metadata = Model.ExportMetadata(Singletons.MetadataInfo);

         var unmappedPointers = metadata.UnmappedPointers.Select(up => up.Name).ToList();
         Assert.Contains("misc.temp.stuff", unmappedPointers);
      }

      private void StandardSetup(out byte[] data, out PokemonModel model, out ViewPort viewPort) {
         data = new byte[0x200];
         model = new PokemonModel(data);
         viewPort = new ViewPort("file.txt", model, InstantDispatch.Instance, BaseViewModelTestClass.Singletons) { Width = 0x10, Height = 0x10 };
      }
   }
}
