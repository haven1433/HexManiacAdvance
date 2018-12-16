using HavenSoft.Gen3Hex.Core.Models;
using HavenSoft.Gen3Hex.Core.ViewModels;
using System;
using System.Linq;
using Xunit;

namespace HavenSoft.Gen3Hex.Tests {
   public class StringModelTests {
      [Fact]
      public void CanRecognizeString() {
         var buffer = new byte[0x100];
         var model = new PointerAndStringModel(buffer);

         var data = PCSString.Convert("Hello World!").ToArray();
         Array.Copy(data, 0, buffer, 0x10, data.Length);
         model.ObserveRunWritten(new PCSRun(0x10, data.Length));

         var run = (PCSRun)model.GetNextRun(0);
         Assert.Equal(data.Length, run.Length);
      }

      [Fact]
      public void CanWriteString() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x100).ToArray();
         var model = new PointerAndStringModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model);

         viewPort.Edit("^\"\" \"Hello World!\"");

         var run = (PCSRun)model.GetNextRun(0);
         Assert.Equal(13, run.Length);
      }

      [Fact]
      public void CanFindStringsInData() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x100).ToArray();
         var data = PCSString.Convert("Hello World!").ToArray();
         Array.Copy(data, 0, buffer, 0x10, data.Length);
         var model1 = new PointerAndStringModel(buffer);
         model1.WritePointer(0x00, 0x10);

         var model = new PointerAndStringModel(buffer);

         Assert.IsType<PCSRun>(model.GetNextRun(0x10));
      }

      [Fact]
      public void TryingToWriteStringFormatToNonStringFormatDataFails() {
         var buffer = new byte[0x100];
         var model = new PointerAndStringModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model);
         var editor = new EditorViewModel(new StubFileSystem());
         editor.Add(viewPort);

         viewPort.Edit("^\"\" ");
         Assert.False(string.IsNullOrEmpty(editor.ErrorMessage));
      }

      [Fact]
      public void CanTruncateString() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x100).ToArray();
         var model = new PointerAndStringModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model) { Width = 0x10, Height = 0x10 };

         viewPort.Edit("^\"\" \"Hello World!\"");

         viewPort.SelectionStart = new Point("Hello".Length, 0);

         viewPort.Edit("\"");

         var run = (PCSRun)model.GetNextRun(0);
         Assert.Equal(6, run.Length);
         Assert.Equal(0xFF, buffer[7]);
         Assert.Equal(0xFF, buffer[8]);
         Assert.Equal(0xFF, buffer[9]);
         Assert.Equal(0xFF, buffer[10]);
         Assert.Equal(0xFF, buffer[11]);
         Assert.Equal(0xFF, buffer[12]);
         Assert.Equal(0xFF, buffer[13]);
      }

      [Fact]
      public void CanAutoMoveWhenHittingAnchor() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var model = new PointerAndStringModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model) { Width = 0x10, Height = 0x10 };

         viewPort.SelectionStart = new Point(8, 0);
         viewPort.Edit("^bob FF FF FF FF <tom>");
         viewPort.SelectionStart = new Point(0, 0);
         viewPort.Edit("^tom\"\" \"Some really long string\"");

         // the run was moved
         var run = model.GetNextRun(0x10);
         Assert.IsType<PCSRun>(run);

         // the original data is now cleared
         Assert.Equal(0xFF, buffer[0]);
         Assert.Equal(0xFF, buffer[1]);
         Assert.Equal(0xFF, buffer[2]);
         Assert.Equal(0xFF, buffer[3]);

         // pointer should be updated
         Assert.Equal(run.Start, model.ReadPointer(0xC));
      }

      [Fact]
      public void CanAutoMoveWhenHittingData() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var model = new PointerAndStringModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model) { Width = 0x10, Height = 0x10 };

         viewPort.SelectionStart = new Point(8, 0);
         viewPort.Edit("A1 B3 64 18 <tom>");
         viewPort.SelectionStart = new Point(0, 0);
         viewPort.Edit("^tom\"\" \"Some really long string\"");

         // the run was moved
         var run = model.GetNextRun(0x10);
         Assert.IsType<PCSRun>(run);

         // the original data is now cleared
         Assert.Equal(0xFF, buffer[0]);
         Assert.Equal(0xFF, buffer[1]);
         Assert.Equal(0xFF, buffer[2]);
         Assert.Equal(0xFF, buffer[3]);

         // pointer should be updated
         Assert.Equal(run.Start, model.ReadPointer(0xC));
      }

      // TODO escape sequences

      // TODO copy/paste
   }
}
