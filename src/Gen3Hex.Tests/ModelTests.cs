using HavenSoft.Gen3Hex.Core.Models;
using HavenSoft.Gen3Hex.Core.ViewModels;
using System;
using Xunit;

namespace HavenSoft.Gen3Hex.Tests {
   public class ModelTests {
      [Fact]
      public void PointerModelFindsNoPointersInRandomData() {
         var rnd = new Random(0xCafe);
         var buffer = new byte[0x10000]; // 64KB
         rnd.NextBytes(buffer);
         for (int i = 0; i < buffer.Length; i++) if (buffer[i] == 0x08) buffer[i] = 0x10;

         var model = new PointerModel(buffer);

         Assert.Null(model.GetNextRun(0));
      }

      [Fact]
      public void PointerModelFindsPointersInRange() {
         var rnd = new Random(0xCafe);
         var buffer = new byte[0x10000]; // 64KB
         rnd.NextBytes(buffer);
         for (int i = 0; i < buffer.Length; i++) if (buffer[i] == 0x08) buffer[i] = 0x10;

         // write two specific pointers
         buffer.WritePointer(0x204, 0x4050);
         buffer.WritePointer(0x4070, 0x101C);

         var model = new PointerModel(buffer);

         Assert.Equal(0x204, model.GetNextRun(0).Start);
         Assert.IsType<PointerRun>(model.GetNextRun(0x206));

         Assert.IsType<NoInfoRun>(model.GetNextRun(0x208));
         Assert.Single(model.GetNextRun(0x400).Anchor.PointerSources);

         Assert.Equal(0x4050, model.GetNextRun(0x4050).Start);
         Assert.Equal(4, model.GetNextRun(0x4071).Length);
      }

      [Fact]
      public void PointerModelFindsSelfReferences() {
         var buffer = new byte[0x20];
         buffer.WritePointer(0xC, 0xC);

         var model = new PointerModel(buffer);

         var run = model.GetNextRun(0);
         var nextRun = model.GetNextRun(run.Start + run.Length);

         Assert.NotNull(run);
         Assert.Null(nextRun);
      }

      [Fact]
      public void PointerModelMergesDuplicates() {
         var buffer = new byte[0x20];
         buffer.WritePointer(0x0C, 0x14);
         buffer.WritePointer(0x1C, 0x14);

         var model = new PointerModel(buffer);

         var run = model.GetNextRun(0x14);
         Assert.Equal(2, run.Anchor.PointerSources.Count);
      }

      [Fact]
      public void ModelUpdatesWhenViewPortChanges() {
         var buffer = new byte[0x100];
         var model = new PointerModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model);

         viewPort.Edit("<000020>");

         Assert.Equal(0, model.GetNextRun(0).Start);
         Assert.Equal(0x20, model.GetNextRun(10).Start);
      }

      [Fact]
      public void WritingNamedAnchorFollowedByPointerToNameWorks() {
         var buffer = new byte[0x100];
         var model = new PointerModel(buffer);
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
         var model = new PointerModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model);

         viewPort.SelectionStart = new Point(0, 2);
         viewPort.Edit("<bob>");
         viewPort.SelectionStart = new Point(0, 1);
         viewPort.Edit("^bob ");

         Assert.Equal(4, viewPort[0, 2].Value);
      }

      // TODO test writing an anchor where there is already a pointer
      // TODO test writing an anchor over a pointer that references that anchor
      // TODO test writing a pointer over the front half of an existing pointer
      // TODO test writing a pointer over the back half of an existing pointer
      // TODO test writing an anchor into the middle of a pointer (should erase the pointer)
      // TODO test writing a pointer over an existing anchor (should erase the anchor and any pointers to it)

      // TODO test getting anchor source addresses

      // TODO EDIT TEST backspace should open an edit on the byte before the selected byte
      // TODO EDIT TEST backspace during an edit should back out
      // TODO EDIT TEST backspace from the start of an edited byte should replace it with FF if it had no format

      // TODO be able to remove an anchor by typing ^, Backspace, Whitespace
      // TODO be able to remove a pointer via delete
      // TODO be able to remove a pointer via typing a normal byte
      // TODO be able to remove a pointer via backspace from within the pointer
      // TODO be able to remove a pointer via backspace from directly after the pointer
      // TODO backspace on the first byte of the pointer edits the previous byte

      // TODO undo/redo
      // TODO save/load anchor names
   }
}
