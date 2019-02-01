using HavenSoft.Gen3Hex.Core;
using HavenSoft.Gen3Hex.Core.Models;
using HavenSoft.Gen3Hex.Core.Models.Runs;
using HavenSoft.Gen3Hex.Core.ViewModels;
using HavenSoft.Gen3Hex.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace HavenSoft.Gen3Hex.Tests {
   public class PointerModelTests {
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
         var buffer = new byte[0x100];
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model);

         viewPort.Edit("<000020>");

         Assert.Equal(0, model.GetNextRun(0).Start);
         Assert.Equal(0x20, model.GetNextRun(10).Start);
      }

      [Fact]
      public void WritingNamedAnchorFollowedByPointerToNameWorks() {
         var buffer = new byte[0x100];
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model);

         viewPort.SelectionStart = new Point(0, 1);
         viewPort.Edit("^bob ");
         viewPort.SelectionStart = new Point(0, 2);
         viewPort.Edit("<bob>");

         Assert.Equal(4, viewPort[0, 2].Value);
      }

      [Fact]
      public void WritingPointerToNameFollowedByNamedAnchorWorks() {
         var buffer = new byte[0x100];
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model);

         viewPort.SelectionStart = new Point(0, 2);
         viewPort.Edit("<bob>");
         viewPort.SelectionStart = new Point(0, 1);
         viewPort.Edit("^bob ");

         Assert.Equal(4, viewPort[0, 2].Value);
      }

      [Fact]
      public void CanWriteAnchorToSameLocationAsPointerWithoutRemovingPointer() {
         var buffer = new byte[0x100];
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model);

         viewPort.SelectionStart = new Point(0, 2);
         viewPort.Edit("<000040>");
         viewPort.SelectionStart = new Point(0, 2);
         viewPort.Edit("^bob ");

         Assert.IsType<Anchor>(viewPort[0, 2].Format);
      }

      [Fact]
      public void CanWriteAnchorToSameLocationAsPointerPointingToThatAnchor() {
         var buffer = new byte[0x100];
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model);

         viewPort.SelectionStart = new Point(0, 2);
         viewPort.Edit("<bob>");
         viewPort.SelectionStart = new Point(0, 2);
         viewPort.Edit("^bob ");

         Assert.IsType<Core.ViewModels.DataFormats.Anchor>(viewPort[0, 2].Format);
         Assert.Equal(0x8, viewPort[0, 2].Value);
      }

      [Fact]
      public void WritingAnAnchorUpdatesPointersToUseThatName() {
         var buffer = new byte[0x100];
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model);

         viewPort.SelectionStart = new Point(0, 2);
         viewPort.Edit("<000004>");
         viewPort.SelectionStart = new Point(0, 1);
         viewPort.Edit("^bob ");

         Assert.Equal("bob", ((Pointer)viewPort[0, 2].Format).DestinationName);
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
         var buffer = new byte[0x100];
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model) { Width = 16, Height = 16 };
         var token = new ModelDelta();

         viewPort.SelectionStart = new Point(0, 1);
         viewPort.Edit("^bob ");
         viewPort.SelectionStart = new Point(0, 2);
         viewPort.Edit("<bob>");
         viewPort.SelectionStart = new Point(0, 1);

         // as an alternative to being able to delete an anchor from the viewPort,
         // just edit the model directly and then scroll to force the viewPort to refresh
         model.ClearFormat(token, 0x10, 1);
         viewPort.ScrollValue = 1;
         viewPort.ScrollValue = 0;

         Assert.Equal("bob", ((Pointer)viewPort[0, 2].Format).DestinationName);
      }

      [Fact]
      public void PointerGetsSetToZeroAfterAnchorGetsDeleted() {
         var buffer = new byte[0x100];
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model) { Width = 16, Height = 16 };
         var token = new ModelDelta();

         viewPort.SelectionStart = new Point(0, 1);
         viewPort.Edit("^bob ");
         viewPort.SelectionStart = new Point(0, 2);
         viewPort.Edit("<bob>");
         viewPort.SelectionStart = new Point(0, 1);

         // as an alternative to being able to delete an anchor from the viewPort,
         // just edit the model directly and then scroll to force the viewPort to refresh
         model.ClearFormat(token, 0xF, 2);
         viewPort.ScrollValue = 1;
         viewPort.ScrollValue = 0;

         Assert.Equal(Pointer.NULL, ((Pointer)viewPort[0, 2].Format).Destination);
      }

      [Fact]
      public void AnchorCarriesSourceInformation() {
         var buffer = new byte[0x100];
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model) { Width = 16, Height = 16 };

         viewPort.SelectionStart = new Point(0, 1);
         viewPort.Edit("<000020>");
         var anchor = (Core.ViewModels.DataFormats.Anchor)viewPort[0, 2].Format;
         Assert.Contains(16, anchor.Sources);
      }

      [Fact]
      public void StartingAnAnchorAndGivingItNoNameClearsAnyAnchorNameAtThatPosition() {
         var buffer = new byte[0x100];
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model) { Width = 0x10, Height = 0x10 };

         viewPort.SelectionStart = new Point(0, 1);
         viewPort.Edit("<bob>");
         viewPort.SelectionStart = new Point(0, 2);
         viewPort.Edit("^bob ");
         viewPort.Edit("^ ");

         var format = (Pointer)viewPort[0, 1].Format;
         Assert.Equal(0x20, format.Destination);
         Assert.Equal(string.Empty, format.DestinationName);
         var address = model.GetAddressFromAnchor(new ModelDelta(), -1, string.Empty);
         Assert.Equal(Pointer.NULL, address);
      }

      [Fact]
      public void CanRemoveAnchorWithNoReferences() {
         var buffer = new byte[0x100];
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model) { Width = 0x10, Height = 0x10 };

         viewPort.SelectionStart = new Point(0, 2);
         viewPort.Edit("^bob ^ ");

         Assert.Equal(NoInfoRun.NullRun, model.GetNextRun(0));
      }

      [Fact]
      public void BackspaceClearsDataButNotFormats() {
         var buffer = new byte[0x100];
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model) { Width = 0x10, Height = 0x10 };

         viewPort.SelectionStart = new Point(0, 1);
         viewPort.Edit("01 02 03 04");                // 2x4 characters to clear
         viewPort.Edit("<000020>");                   // 8 characters to clear
         viewPort.Edit("<000030>");                   // 8 characters to clear
         viewPort.SelectionStart = new Point(10, 1);

         for (int i = 0; i < 21; i++) viewPort.Edit(ConsoleKey.Backspace); // should clear both pointers (16) and 2 bytes (4)
         viewPort.MoveSelectionStart.Execute(Direction.Up);

         Assert.Equal(Pointer.NULL, ((Pointer)viewPort[8, 1].Format).Destination);
         Assert.Equal(Pointer.NULL, ((Pointer)viewPort[4, 1].Format).Destination);
         Assert.Equal(0x01, viewPort[0, 1].Value);
         Assert.Equal(0x02, viewPort[1, 1].Value);
         Assert.Equal(0xFF, viewPort[2, 1].Value);
         Assert.Equal(0xFF, viewPort[3, 1].Value);
      }

      [Fact]
      public void WritingOverTwoPointersWorks() {
         var buffer = new byte[0x100];
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model) { Width = 0x10, Height = 0x10 };

         viewPort.SelectionStart = new Point(0, 1);
         viewPort.Edit("<000020>");
         viewPort.Edit("<000030>");
         viewPort.SelectionStart = new Point(2, 1);
         viewPort.Clear.Execute();  // this should clear the data and formatting of the first pointer
         viewPort.Edit("<000040>"); // this should remove the second pointer

         Assert.Equal(0xFF, viewPort[0, 1].Value);
         Assert.Equal(0xFF, viewPort[1, 1].Value);
         Assert.Equal(0x40, viewPort[2, 1].Value);
         Assert.Equal(0x00, viewPort[3, 1].Value);
         Assert.Equal(0x00, viewPort[4, 1].Value);
         Assert.Equal(0x08, viewPort[5, 1].Value);
         Assert.Equal(0xFF, viewPort[6, 1].Value);
         Assert.Equal(0xFF, viewPort[7, 1].Value);

         Assert.IsNotType<Pointer>(viewPort[1, 1].Format);
         Assert.IsType<Pointer>(viewPort[2, 1].Format);
         Assert.IsType<Pointer>(viewPort[5, 1].Format);
         Assert.IsNotType<Pointer>(viewPort[6, 1].Format);
      }

      [Fact]
      public void PointerToUnknownLocationShowsUpDifferent() {
         var buffer = new byte[0x100];
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model) { Width = 0x10, Height = 0x10 };

         viewPort.SelectionStart = new Point(2, 1);
         viewPort.Edit("<bob>");

         var pointer = (Pointer)viewPort[2, 1].Format;
         Assert.Equal("bob", pointer.DestinationName);
         Assert.Equal(Pointer.NULL, pointer.Destination);
      }

      [Fact]
      public void AddingANewNamedPointerToNoLocationOverExistingNamedPointerToNoLocationWorks() {
         var buffer = new byte[0x100];
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model) { Width = 0x10, Height = 0x10 };

         viewPort.SelectionStart = new Point(2, 1);
         viewPort.Edit("<bob>");

         viewPort.SelectionStart = new Point(0, 1);
         viewPort.Edit("<tom>");

         Assert.IsNotType<Pointer>(viewPort[4, 1].Format);
      }

      [Fact]
      public void CanGotoAnchorName() {
         var buffer = new byte[0x100];
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model) { Width = 0x10, Height = 0x10 };

         int errorCalls = 0;
         viewPort.OnError += (sender, e) => errorCalls++;

         viewPort.SelectionStart = new Point(2, 1);
         viewPort.Edit("^bob ");
         viewPort.Goto.Execute("bob");

         Assert.Equal(0, errorCalls);
      }

      [Fact]
      public void CanFindPointer() {
         var buffer = new byte[0x100];
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model) { Width = 0x10, Height = 0x10 };

         viewPort.SelectionStart = new Point(4, 1);
         viewPort.Edit("<000058> 23 19");
         var results = viewPort.Find("<000058> 23 19");

         Assert.Single(results);
      }

      [Fact]
      public void CanUsePointerAsLink() {
         var buffer = new byte[0x200];
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model) { Width = 0x10, Height = 0x10 };

         viewPort.SelectionStart = new Point(4, 1);
         viewPort.Edit("<000120>");
         viewPort.FollowLink(4, 1);

         Assert.Equal("000120", viewPort.Headers[0]);
      }

      [Fact]
      public void FindAllSourcesWorks() {
         var buffer = new byte[0x200];
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model) { Width = 0x10, Height = 0x10 };
         var editor = new EditorViewModel(new StubFileSystem());
         editor.Add(viewPort);

         viewPort.SelectionStart = new Point(4, 1);
         viewPort.Edit("<000120>");
         viewPort.FollowLink(4, 1);

         viewPort.FindAllSources(0, 0);

         Assert.Equal(1, editor.SelectedIndex);
      }

      [Fact]
      public void NewAnchorWithSameNameMovesPointersToNewAnchor() {
         var buffer = new byte[0x200];
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model) { Width = 0x10, Height = 0x10 };

         // put some pointers in the file
         viewPort.SelectionStart = new Point(4, 1);
         viewPort.Edit("<bob>");
         viewPort.SelectionStart = new Point(4, 3);
         viewPort.Edit("<bob>");

         // make them point somewhere real
         viewPort.SelectionStart = new Point(8, 2);
         viewPort.Edit("^bob ");

         // move them to point to somewhere else
         viewPort.SelectionStart = new Point(8, 4);
         viewPort.Edit("^bob ");

         Assert.Equal(0x48, ((Pointer)viewPort[4, 1].Format).Destination);
         Assert.Equal(0x48, ((Pointer)viewPort[4, 3].Format).Destination);
      }

      [Fact]
      public void CanCopyAndPastePointers() {
         var buffer = new byte[0x200];
         var fileSystem = new StubFileSystem();
         var viewPort = new ViewPort(new LoadedFile("file.txt", buffer), new PokemonModel(buffer)) { Width = 0x10, Height = 0x10 };

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
         var buffer = new byte[0x200];
         var model = new PokemonModel(buffer);
         var fileSystem = new StubFileSystem();
         var viewPort = new ViewPort(new LoadedFile("file.txt", buffer), model) { Width = 0x10, Height = 0x10 };

         viewPort.Edit("<null>");
         viewPort.SelectionStart = new Point(0, 0);
         viewPort.Edit("<bob>");

         var format = (Pointer)model.GetNextRun(0x0).CreateDataFormat(model, 0x00);
         Assert.Equal("bob", format.DestinationName);
      }

      [Fact]
      public void FormatClearDoesNotClearAnchorIfAnchorIsAtStartOfClear() {
         var buffer = new byte[0x200];
         var model = new PokemonModel(buffer);
         var token = new ModelDelta();

         model.ObserveAnchorWritten(token, "bob", new NoInfoRun(0x10));
         model.ClearFormat(token, 0x10, 1);

         Assert.Equal(0x10, model.GetNextRun(0x10).Start);
      }

      [Fact]
      public void ArrowMovementWhileTypingAnchorRevertsChange() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model) { Width = 0x10, Height = 0x10 };

         viewPort.Edit("^bob"); // no trailing space: still under edit

         viewPort.SelectionStart = new Point(1, 1);

         Assert.IsType<None>(viewPort[0, 0].Format);
      }

      [Fact]
      public void EscapeWhileTypingAnchorCancelsChange() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model) { Width = 0x10, Height = 0x10 };

         viewPort.Edit("^bob"); // no trailing space: still under edit
         viewPort.Edit(ConsoleKey.Escape);

         Assert.IsType<None>(viewPort[0, 0].Format);
         Assert.Equal(NoInfoRun.NullRun, model.GetNextRun(0));
      }

      [Fact]
      public void StartingAnAnchorOverAnAnchorClearsTheExistingAnchorInfo() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model) { Width = 0x10, Height = 0x10 };

         viewPort.Edit("^bob ");
         Assert.Equal("^bob", viewPort.AnchorText);

         viewPort.Edit("^");

         var format = (UnderEdit)viewPort[0, 0].Format;
         Assert.Equal("^", format.CurrentText);
         Assert.Equal("^", viewPort.AnchorText);
      }

      [Fact]
      public void AnchorEditTextUpdatesWithSelectionChange() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var model = new PokemonModel(buffer);
         model.ObserveAnchorWritten(new ModelDelta(), "bob", new NoInfoRun(0x08));
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model) { Width = 0x10, Height = 0x10 };

         viewPort.SelectionStart = new Point(0x08, 0);

         Assert.True(viewPort.AnchorTextVisible);
         Assert.Equal("^bob", viewPort.AnchorText);
      }

      [Fact]
      public void AnchorEditTextUpdatesWhenTypingAnAnchor() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model) { Width = 0x10, Height = 0x10 };

         viewPort.Edit("^partialTe"); // in the middle of typing 'partialText'

         Assert.True(viewPort.AnchorTextVisible);
         Assert.Equal("^partialTe", viewPort.AnchorText);
      }

      [Fact]
      public void ModifyingAnchorTextUpdatesTheAnchor() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var model = new PokemonModel(buffer);
         model.ObserveAnchorWritten(new ModelDelta(), "bob", new NoInfoRun(0x08));
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model) { Width = 0x10, Height = 0x10 };

         viewPort.SelectionStart = new Point(0x08, 0);
         viewPort.AnchorText = "^bob\"\"";

         Assert.IsType<PCSRun>(model.GetNextRun(0x08));
      }

      [Fact]
      public void AnchorTextAlwaysCoercesToStartWithAnchorCharacter() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var model = new PokemonModel(buffer);
         model.ObserveAnchorWritten(new ModelDelta(), "bob", new NoInfoRun(0x08));
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model) { Width = 0x10, Height = 0x10 };

         viewPort.SelectionStart = new Point(0x08, 0);
         viewPort.AnchorText = "tom\"\"";

         Assert.Equal("^tom\"\"", viewPort.AnchorText); // not that the ^ was added to the front
      }

      [Fact]
      public void GivenTwoPointersCanRemoveAndUndoTheFirstWithoutEffectingTheSecond() {
         var data = new byte[0x200];
         var model = new PokemonModel(data);
         model.WritePointer(new ModelDelta(), 0x010, 0x100);
         model.ObserveRunWritten(new ModelDelta(), new PointerRun(0x010));
         model.WritePointer(new ModelDelta(), 0x014, 0x120);
         model.ObserveRunWritten(new ModelDelta(), new PointerRun(0x014));
         var viewPort = new ViewPort(new LoadedFile("test.txt", data), model) { Width = 0x8, Height = 0x8 };

         viewPort.SelectionStart = new Point(0, 2); // 0x010
         viewPort.Edit("00"); // this should remove the first pointer
         viewPort.Undo.Execute();

         Assert.Equal(0x014, model.GetNextRun(0x014).Start);
      }

      [Fact]
      public void CreatingAnOffsetPointerShouldCoerceToTheStartOfExistingPointer() {
         var data = new byte[0x200];
         var model = new PokemonModel(data);
         var viewPort = new ViewPort(new LoadedFile("test.txt", data), model) { Width = 0x10, Height = 0x10 };

         viewPort.Edit("<000100>");
         viewPort.SelectionStart = new Point(2, 0);
         viewPort.Edit("<000050>");

         // note that the second pointer should be right where the first one was
         // because trying to start a pointer edit mid-pointer should move you to the start of the pointer.
         Assert.Equal(0x50, data[0]);
      }

      [Fact]
      public void CreatingAPointerOutsideTheDataRangeErrors() {
         var errors = new List<string>();
         var data = new byte[0x200];
         var model = new PokemonModel(data);
         var viewPort = new ViewPort(new LoadedFile("test.txt", data), model) { Width = 0x10, Height = 0x10 };

         viewPort.OnError += (sender, message) => errors.Add(message);
         viewPort.Edit("<000400>");

         Assert.Single(errors);
      }

      [Fact]
      public void ClearingAPointerAlsoRemovesItsAnchor() {
         var data = new byte[0x200];
         var model = new PokemonModel(data);
         var viewPort = new ViewPort(new LoadedFile("test.txt", data), model) { Width = 0x10, Height = 0x10 };

         viewPort.Edit("<000100>");
         model.ClearFormat(new ModelDelta(), 0x00, 4);

         Assert.NotInRange(model.GetNextRun(0x00).Start, 0, data.Length);
      }

      [Fact]
      public void TypingBracesOnDataTriesToInterpretThatDataAsPointer() {
         var data = new byte[0x200];
         var model = new PokemonModel(data);
         var viewPort = new ViewPort(new LoadedFile("test.txt", data), model) { Width = 0x10, Height = 0x10 };

         viewPort.Edit("00 01 00 08");
         viewPort.SelectionStart = new Point(0, 0);
         viewPort.Edit("<>");   // typing this should interpret the bytes as a pointer and add it.

         Assert.Equal(0x100, ((Pointer)viewPort[0, 0].Format).Destination);
      }

      [Fact]
      public void StartingPointerEditAndThenMovingClearsAllEditedCells() {
         var data = new byte[0x200];
         var model = new PokemonModel(data);
         var viewPort = new ViewPort(new LoadedFile("test.txt", data), model) { Width = 0x10, Height = 0x10 };

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
         var data = new byte[0x200];
         var model = new PokemonModel(data);
         var change = new ModelDelta();
         model.WritePointer(change, 0x23, 0x050); // a pointer that isn't 4-byte aligned, pointing to data that is
         model.WritePointer(change, 0x10, 0x087); // a pointer that is 4-byte aligned, but pointing to something that isn't
         model.WritePointer(change, 2, 0xA2);    // a pointer that isn't 4-byte aligned, pointing to something not 4-byte aligned
         var viewPort = new ViewPort(new LoadedFile("test.txt", data), model) { Width = 0x10, Height = 0x10 };

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
   }
}
