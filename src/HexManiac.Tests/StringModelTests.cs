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
   public class StringModelTests {
      [Fact]
      public void CanRecognizeString() {
         var buffer = new byte[0x100];
         var model = new PokemonModel(buffer);
         var token = new ModelDelta();

         var data = PCSString.Convert("Hello World!").ToArray();
         Array.Copy(data, 0, buffer, 0x10, data.Length);
         model.ObserveRunWritten(token, new PCSRun(0x10, data.Length));

         var run = (PCSRun)model.GetNextRun(0);
         Assert.Equal(data.Length, run.Length);
      }

      [Fact]
      public void CanWriteString() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x100).ToArray();
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort("test.txt", model);

         viewPort.Edit("^bob\"\" \"Hello World!\"");

         var run = (PCSRun)model.GetNextRun(0);
         Assert.Equal(13, run.Length);
      }

      [Fact]
      public void CanFindStringsInData() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x100).ToArray();
         var data = PCSString.Convert("Hello World!").ToArray();
         Array.Copy(data, 0, buffer, 0x10, data.Length);
         var model1 = new PokemonModel(buffer);
         var token = new ModelDelta();
         model1.WritePointer(token, 0x00, 0x10);

         var model = new PokemonModel(buffer);

         Assert.IsType<PCSRun>(model.GetNextRun(0x10));
      }

      [Fact]
      public void TryingToWriteStringFormatToNonStringFormatDataFails() {
         var buffer = new byte[0x100];
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort("test.txt", model);
         var editor = new EditorViewModel(new StubFileSystem());
         editor.Add(viewPort);

         viewPort.Edit("^\"\" ");
         Assert.False(string.IsNullOrEmpty(editor.ErrorMessage)); // should get an error, because the data located at the cursor could not convert to a string.
      }

      [Fact]
      public void CanTruncateString() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x100).ToArray();
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort("test.txt", model) { Width = 0x10, Height = 0x10 };

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
         var buffer = Enumerable.Repeat((byte)0xFF, 0x300).ToArray();
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort("test.txt", model) { Width = 0x10, Height = 0x10 };

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
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort("test.txt", model) { Width = 0x10, Height = 0x10 };

         viewPort.SelectionStart = new Point(8, 0);
         viewPort.Edit("A1 B3 64 18 <tom>");
         viewPort.SelectionStart = new Point(0, 0);
         viewPort.Edit("^tom\"\" \"Some really long string\"");

         // the run was moved
         var run = model.GetNextRun(0x10);
         Assert.IsType<PCSRun>(run);
         Assert.Equal(24, run.Length);

         // the original data is now cleared
         Assert.Equal(0xFF, model[0]);
         Assert.Equal(0xFF, model[1]);
         Assert.Equal(0xFF, model[2]);
         Assert.Equal(0xFF, model[3]);

         // pointer should be updated
         Assert.Equal(run.Start, model.ReadPointer(0xC));
      }

      [Fact]
      public void AnchorWithNoNameIsNotValidIfNothingPointsToIt() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort("test.txt", model) { Width = 0x10, Height = 0x10 };
         var errors = new List<string>();
         viewPort.OnError += (sender, e) => errors.Add(e);

         viewPort.Edit("^ ");
         Assert.Equal(NoInfoRun.NullRun, model.GetNextRun(0));
         Assert.Single(errors);
         Assert.IsType<None>(viewPort[0, 0].Format);

         errors.Clear();

         viewPort.Edit("^\"\" ");
         Assert.Equal(NoInfoRun.NullRun, model.GetNextRun(0));
         Assert.Single(errors);
      }

      [Fact]
      public void OpeningStringFormatIncludesOpeningQuote() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort("test.txt", model) { Width = 0x10, Height = 0x10 };

         viewPort.Edit("^bob\"\" \"Hello World!\"");
         var anchor = (Anchor)viewPort[0, 0].Format;
         var innerFormat = (PCS)anchor.OriginalFormat;
         Assert.Equal("\"H", innerFormat.ThisCharacter);
      }

      [Fact]
      public void CannotAddNewStringAnchorUnlessItEndsBeforeNextKnownAnchor() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         for (int i = 0; i < 0x10; i++) buffer[i] = 0x00;
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort("test.txt", model) { Width = 0x10, Height = 0x10 };
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
         Assert.IsType<None>(viewPort[0, 0].Format);
      }

      [Fact]
      public void UsingBackspaceMidStringMakesTheStringEndThere() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         for (int i = 0; i < 0x10; i++) buffer[i] = 0x00;
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort("test.txt", model) { Width = 0x10, Height = 0x10 };

         // add an anchor with some data on the 2nd line
         viewPort.SelectionStart = new Point(0, 1);
         viewPort.Edit("^bob\"\" \"Hello World!\"");
         viewPort.SelectionStart = new Point(6, 1);
         viewPort.Edit(ConsoleKey.Backspace);

         Assert.Equal("\"Hello\"", PCSString.Convert(model, 0x10, PCSString.ReadString(model, 0x10, true)));
      }

      [Fact]
      public void UsingEscapeSequencesWorks() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         for (int i = 0; i < 0x10; i++) buffer[i] = 0x00;
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort("test.txt", model) { Width = 0x10, Height = 0x10 };

         viewPort.Edit("^bob\"\" \"Some Content \\\\03More Content\"");

         Assert.Equal(28, model.GetNextRun(0).Length);
         Assert.IsType<EscapedPCS>(viewPort[14, 0].Format);
      }

      [Fact]
      public void CanCopyStrings() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         for (int i = 0; i < 0x10; i++) buffer[i] = 0x00;
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort("test.txt", model) { Width = 0x10, Height = 0x10 };
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
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort("test.txt", model) { Width = 0x10, Height = 0x10 };
         viewPort.Edit("^bob\"\" \"Text and BULBASAUR!\"");

         var results = viewPort.Find("\"bulbasaur\"").Select(result => result.start).ToList(); ;
         Assert.Single(results);
         Assert.Equal(9, results[0]);
      }

      [Fact]
      public void FindForStringsWorksWithoutQuotes() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         for (int i = 0; i < 0x10; i++) buffer[i] = 0x00;
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort("test.txt", model) { Width = 0x10, Height = 0x10 };
         viewPort.Edit("^bob\"\" \"Text and BULBASAUR!\"");

         var results = viewPort.Find("bulbasaur").Select(result => result.start).ToList();
         Assert.Single(results);
         Assert.Equal(9, results[0]);
      }

      [Fact]
      public void CanNameExistingStringAnchor() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var bytes = PCSString.Convert("Hello World!").ToArray();
         buffer[0] = 0x08;
         buffer[1] = 0x00;
         buffer[2] = 0x00;
         buffer[3] = 0x08;
         Array.Copy(bytes, 0, buffer, 0x08, bytes.Length);
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort("test.txt", model) { Width = 0x10, Height = 0x10 };

         viewPort.SelectionStart = new Point(0x08, 0);
         viewPort.Edit("^");
         viewPort.Edit("bob\"\" ");

         Assert.Equal("bob", ((Pointer)viewPort[0, 0].Format).DestinationName);
      }

      [Fact]
      public void FormatIsRemovedWhenEditingAnAnchor() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var bytes = PCSString.Convert("Hello World!").ToArray();
         buffer[0] = 0x08;
         buffer[1] = 0x00;
         buffer[2] = 0x00;
         buffer[3] = 0x08;
         Array.Copy(bytes, 0, buffer, 0x08, bytes.Length);
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort("test.txt", model) { Width = 0x10, Height = 0x10 };

         viewPort.SelectionStart = new Point(0x08, 0);
         viewPort.Edit("^");

         var underEdit = (UnderEdit)viewPort[8, 0].Format;
         Assert.Equal("^", underEdit.CurrentText);
      }

      [Fact]
      public void UsingTheAnchorEditorToSetStringFormatChangesVisibleData() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var bytes = PCSString.Convert("Hello World!").ToArray();
         Array.Copy(bytes, 0, buffer, 0x08, bytes.Length);
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort("test.txt", model) { Width = 0x10, Height = 0x10 };
         viewPort.SelectionStart = new Point(0x08, 0);
         viewPort.Edit("^bob ");

         viewPort.AnchorText = "^bob\"\"";

         var anchor = (Anchor)viewPort[8, 0].Format;
         Assert.IsType<PCS>(anchor.OriginalFormat);
      }

      [Fact]
      public void CanUndoStringTruncate() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var bytes = PCSString.Convert("Hello World!").ToArray();
         Array.Copy(bytes, 0, buffer, 0x08, bytes.Length);
         buffer[0] = 0x08;
         buffer[1] = 0x00;
         buffer[2] = 0x00;
         buffer[3] = 0x08;
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort("test.txt", model) { Width = 0x10, Height = 0x10 };

         viewPort.SelectionStart = new Point(0x0C, 0);
         viewPort.Edit(ConsoleKey.Backspace);
         viewPort.Undo.Execute();

         Assert.Equal(13, model.GetNextRun(0x08).Length);
         Assert.Equal("\"Hello World!\"", ((PCS)viewPort[0x0C, 0].Format).FullString);
      }

      [Fact]
      public void PointersPastEndOfDataDoNotCountAsPointers() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         buffer[4] = 0x20;
         buffer[5] = 0x20;
         buffer[6] = 0x20;
         buffer[7] = 0x08;

         // it should realize that the data starting at 4 isn't a pointer
         // because 202020 is after the end of the data
         var model = new PokemonModel(buffer);

         Assert.Equal(int.MaxValue, model.GetNextRun(0).Start);
      }

      [Fact]
      public void StringSearchAutomaticallySearchesForPointersToResults() {
         var text = "This is the song that never ends.";
         var bytes = PCSString.Convert(text).ToArray();
         var buffer = new byte[0x200];
         Array.Copy(bytes, 0, buffer, 0x32, bytes.Length);                // the data itself, positioned at x32 (won't be automatically found on load)
         Array.Copy(new byte[] { 0x32, 0, 0, 0x08 }, 0, buffer, 0x10, 4); // the pointer to the data. Pointer is aligned, but data is not.
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort("test.gba", model);

         // the act of searching should find the anchor
         var results = viewPort.Find("this is the song");

         Assert.Single(results);
         Assert.Single(model.GetNextRun(0x32).PointerSources);
      }

      [Fact]
      public void UnnamedStringAnchorAutomaticallySearchesForPointersToAnchor() {
         var text = "This is the song that never ends.";
         var bytes = PCSString.Convert(text).ToArray();
         var buffer = new byte[0x200];
         Array.Copy(bytes, 0, buffer, 0x32, bytes.Length);                // the data itself, positioned at x32 (won't be automatically found on load)
         Array.Copy(new byte[] { 0x32, 0, 0, 0x08 }, 0, buffer, 0x10, 4); // the pointer to the data. Pointer is aligned, but data is not.
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort("test.gba", model) { Width = 0x10, Height = 0x10 };

         // the act of dropping an anchor should search for pointers
         viewPort.SelectionStart = new Point(2, 3);
         viewPort.Edit("^\"\" ");

         Assert.Equal(0x32, model.GetNextRun(0x32).Start);
         Assert.Single(model.GetNextRun(0x32).PointerSources);
      }

      [Fact]
      public void CopyAnUnnamedStringInsertsAName() {
         var text = "This is the song that never ends.";
         var bytes = PCSString.Convert(text).ToArray();
         var buffer = new byte[0x200];
         Array.Copy(bytes, 0, buffer, 0x30, bytes.Length);
         Array.Copy(new byte[] { 0x30, 0, 0, 0x08 }, 0, buffer, 0x10, 4); // the pointer to the data. Pointer is aligned, but data is not.
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort("test.gba", model) { Width = 0x10, Height = 0x10 };
         var fileSystem = new StubFileSystem();

         viewPort.SelectionStart = new Point(0, 3);
         viewPort.ExpandSelection(0, 3);
         viewPort.Copy.Execute(fileSystem);

         Assert.Contains("^This", fileSystem.CopyText);
         Assert.Contains("\"\" \"This", fileSystem.CopyText); // format, then space, then start of text
      }

      [Fact]
      public void AddingNewAnchorWithSameNameRenamesNewAnchorWithMessage() {
         var model = new PokemonModel(new byte[0x200]);
         var viewPort = new ViewPort(string.Empty, model) { Width = 0x10, Height = 0x10 };
         var messages = new List<string>();
         viewPort.OnMessage += (sender, e) => messages.Add(e);

         viewPort.Edit("^anchor ");
         viewPort.SelectionStart = new Point(0, 1);
         viewPort.Edit("^anchor ");

         Assert.NotEqual("anchor", ((Anchor)viewPort[0, 1].Format).Name);
         Assert.Single(messages);
      }

      [Fact]
      public void CanUseViewPortToAutoFindTextWithoutKnowingAboutPointersToIt() {
         var text = PCSString.Convert("This is some text.");
         var buffer = Enumerable.Range(0, 0x200).Select(i => (byte)0xFF).ToArray();
         text.CopyTo(buffer, 0x10);
         var model = new PokemonModel(buffer);
         model.WritePointer(new ModelDelta(), 0x00, 0x10);

         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };
         viewPort.SelectionStart = new Point(3, 1); // just a random byte in the middle of the text
         var errors = new List<string>();
         viewPort.OnError += (sender, e) => errors.Add(e);

         viewPort.IsText.Execute(); // this line should find the start of the text and add a run, even with no pointer to it

         Assert.IsType<PCS>(viewPort[3, 1].Format);
      }
   }
}
