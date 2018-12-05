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
         Assert.Single(model.GetNextRun(0x400).PointerSources);

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
         Assert.Equal(2, run.PointerSources.Count);
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
   }
}
