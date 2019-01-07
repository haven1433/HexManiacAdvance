using HavenSoft.Gen3Hex.Core.Models;
using HavenSoft.Gen3Hex.Core.ViewModels;
using HavenSoft.Gen3Hex.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace HavenSoft.Gen3Hex.Tests {
   public class StringModelTests {
      [Fact]
      public void CanRecognizeString() {
         var buffer = new byte[0x100];
         var model = new PointerAndStringModel(buffer);
         var token = new DeltaModel();

         var data = PCSString.Convert("Hello World!").ToArray();
         Array.Copy(data, 0, buffer, 0x10, data.Length);
         model.ObserveRunWritten(token, new PCSRun(0x10, data.Length));

         var run = (PCSRun)model.GetNextRun(0);
         Assert.Equal(data.Length, run.Length);
      }

      [Fact]
      public void CanWriteString() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x100).ToArray();
         var model = new PointerAndStringModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model);

         viewPort.Edit("^bob\"\" \"Hello World!\"");

         var run = (PCSRun)model.GetNextRun(0);
         Assert.Equal(13, run.Length);
      }

      [Fact]
      public void CanFindStringsInData() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x100).ToArray();
         var data = PCSString.Convert("Hello World!").ToArray();
         Array.Copy(data, 0, buffer, 0x10, data.Length);
         var model1 = new PointerAndStringModel(buffer);
         var token = new DeltaModel();
         model1.WritePointer(token, 0x00, 0x10);

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
         Assert.False(string.IsNullOrEmpty(editor.ErrorMessage)); // should get an error, because the data located at the cursor could not convert to a string.
      }

      [Fact]
      public void CanTruncateString() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x100).ToArray();
         var model = new PointerAndStringModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model) { Width = 0x10, Height = 0x10 };

         viewPort.Edit("^bob\"\" \"Hello World!\"");

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
         Assert.Equal(24, run.Length);

         // the original data is now cleared
         Assert.Equal(0xFF, buffer[0]);
         Assert.Equal(0xFF, buffer[1]);
         Assert.Equal(0xFF, buffer[2]);
         Assert.Equal(0xFF, buffer[3]);

         // pointer should be updated
         Assert.Equal(run.Start, model.ReadPointer(0xC));
      }

      [Fact]
      public void AnchorWithNoNameIsNotValidIfNothingPointsToIt() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var model = new PointerAndStringModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model) { Width = 0x10, Height = 0x10 };
         var errors = new List<string>();
         viewPort.OnError += (sender, e) => errors.Add(e);

         viewPort.Edit("^ ");
         Assert.Equal(NoInfoRun.NullRun, model.GetNextRun(0));
         Assert.Single(errors);

         errors.Clear();

         viewPort.Edit("^\"\" ");
         Assert.Equal(NoInfoRun.NullRun, model.GetNextRun(0));
         Assert.Single(errors);
      }

      [Fact]
      public void OpeningStringFormatIncludesOpeningQuote() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var model = new PointerAndStringModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model) { Width = 0x10, Height = 0x10 };

         viewPort.Edit("^bob\"\" \"Hello World!\"");
         var anchor = (Anchor)viewPort[0, 0].Format;
         var innerFormat = (PCS)anchor.OriginalFormat;
         Assert.Equal("\"H", innerFormat.ThisCharacter);
      }

      [Fact]
      public void CannotAddNewStringAnchorUnlessItEndsBeforeNextKnownAnchor() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         for (int i = 0; i < 0x10; i++) buffer[i] = 0x00;
         var model = new PointerAndStringModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model) { Width = 0x10, Height = 0x10 };
         var errors = new List<string>();
         viewPort.OnError += (sender, e) => errors.Add(e);

         // add an anchor with some data on the 2nd line
         viewPort.SelectionStart = new Point(0, 1);
         viewPort.Edit("^bob\"\" \"Hello World!\"");

         // but now, try to add a string format in the middle of all the 00 bytes
         viewPort.SelectionStart = new Point(0, 0);
         viewPort.Edit("^tom\"\" ");

         // trying to add a string anchor should've failed
         Assert.Single(errors);
         Assert.Equal(0x10, model.GetNextRun(1).Start);
         Assert.Equal(string.Empty, ((Anchor)viewPort[0, 0].Format).Format);
      }

      [Fact]
      public void UsingBackspaceMidStringMakesTheStringEndThere() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         for (int i = 0; i < 0x10; i++) buffer[i] = 0x00;
         var model = new PointerAndStringModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model) { Width = 0x10, Height = 0x10 };

         // add an anchor with some data on the 2nd line
         viewPort.SelectionStart = new Point(0, 1);
         viewPort.Edit("^bob\"\" \"Hello World!\"");
         viewPort.SelectionStart = new Point(6, 1);
         viewPort.Edit(ConsoleKey.Backspace);

         Assert.Equal("\"Hello\"", PCSString.Convert(model, 0x10, PCSString.ReadString(model, 0x10)));
      }

      [Fact]
      public void UsingEscapeSequencesWorks() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         for (int i = 0; i < 0x10; i++) buffer[i] = 0x00;
         var model = new PointerAndStringModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model) { Width = 0x10, Height = 0x10 };

         viewPort.Edit("^bob\"\" \"Some Content \\\\03More Content\"");

         Assert.Equal(28, model.GetNextRun(0).Length);
         Assert.IsType<EscapedPCS>(viewPort[14, 0].Format);
      }

      [Fact]
      public void CanCopyStrings() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         for (int i = 0; i < 0x10; i++) buffer[i] = 0x00;
         var model = new PointerAndStringModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model) { Width = 0x10, Height = 0x10 };
         var fileSystem = new StubFileSystem();

         viewPort.Edit("^bob\"\" \"Hello World!\"");
         viewPort.SelectionStart = new Point(0, 0);
         viewPort.SelectionEnd = new Point(12, 0);
         viewPort.Copy.Execute(fileSystem);

         Assert.Equal("^bob\"\" \"Hello World!\"", fileSystem.CopyText);
      }

      [Fact]
      public void FindForStringsIsNotCaseSensitive() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         for (int i = 0; i < 0x10; i++) buffer[i] = 0x00;
         var model = new PointerAndStringModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model) { Width = 0x10, Height = 0x10 };
         viewPort.Edit("^bob\"\" \"Text and BULBASAUR!\"");

         var results = viewPort.Find("\"bulbasaur\"");
         Assert.Single(results);
         Assert.Equal(9, results[0]);
      }

      [Fact]
      public void FindForStringsWorksWithoutQuotes() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         for (int i = 0; i < 0x10; i++) buffer[i] = 0x00;
         var model = new PointerAndStringModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model) { Width = 0x10, Height = 0x10 };
         viewPort.Edit("^bob\"\" \"Text and BULBASAUR!\"");

         var results = viewPort.Find("bulbasaur");
         Assert.Single(results);
         Assert.Equal(9, results[0]);
      }
   }
}
