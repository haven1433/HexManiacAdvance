using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using HavenSoft.HexManiac.Core.ViewModels.Visitors;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class StringModelTests : BaseViewModelTestClass {
      [Fact]
      public void CanRecognizeString() {
         var buffer = new byte[0x100];
         var model = new PokemonModel(buffer);
         var token = new ModelDelta();

         var data = PCSString.Convert("Hello World!").ToArray();
         Array.Copy(data, 0, buffer, 0x10, data.Length);
         model.ObserveRunWritten(token, new PCSRun(model, 0x10, data.Length));

         var run = (PCSRun)model.GetNextRun(0);
         Assert.Equal(data.Length, run.Length);
      }

      [Fact]
      public void CanWriteString() {
         SetFullModel(0xFF);
         var (model, viewPort) = (Model, ViewPort);

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
         ViewPort.Edit("^\"\" ");

         Assert.NotEmpty(Errors); // should get an error, because the data located at the cursor could not convert to a string.
      }

      [Fact]
      public void CanTruncateString() {
         SetFullModel(0xFF);
         var (model, viewPort) = (Model, ViewPort);

         viewPort.Edit("^bob\"\" \"Hello World!\"");

         viewPort.SelectionStart = new Point("Hello".Length, 0);

         viewPort.Edit("\"");

         var run = (PCSRun)model.GetNextRun(0);
         Assert.Equal(6, run.Length);
         Assert.All(new[] { 7, 8, 9, 10, 11, 12, 13 }, i => Assert.Equal(0xFF, model[i]));
      }

      [Fact]
      public void CanAutoMoveWhenHittingAnchor() {
         SetFullModel(0xFF);
         var (model, viewPort) = (Model, ViewPort);

         viewPort.SelectionStart = new Point(8, 0);
         viewPort.Edit("^bob FF FF FF FF <tom>");
         viewPort.SelectionStart = new Point(0, 0);
         viewPort.Edit("^tom\"\" \"Some really long string\"");

         // the run was moved
         var run = model.GetNextRun(0x10);
         Assert.IsType<PCSRun>(run);

         // the original data is now cleared
         Assert.All(new[] { 0, 1, 2, 3 }, i => Assert.Equal(0xFF, model[i]));

         // pointer should be updated
         Assert.Equal(run.Start, model.ReadPointer(0xC));
      }

      [Fact]
      public void CanAutoMoveWhenHittingData() {
         SetFullModel(0xFF);
         var (model, viewPort) = (Model, ViewPort);

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
         SetFullModel(0xFF);
         var (model, viewPort) = (Model, ViewPort);

         viewPort.Edit("^ ");
         Assert.Equal(NoInfoRun.NullRun, model.GetNextRun(0));
         Assert.Single(Errors);
         Assert.IsType<None>(viewPort[0, 0].Format);

         Errors.Clear();

         viewPort.Edit("^\"\" ");
         Assert.Equal(NoInfoRun.NullRun, model.GetNextRun(0));
         Assert.Single(Errors);
      }

      [Fact]
      public void OpeningStringFormatIncludesOpeningQuote() {
         SetFullModel(0xFF);
         var (model, viewPort) = (Model, ViewPort);

         viewPort.Edit("^bob\"\" \"Hello World!\"");
         var anchor = (Anchor)viewPort[0, 0].Format;
         var innerFormat = (PCS)anchor.OriginalFormat;
         Assert.Equal("\"H", innerFormat.ThisCharacter);
      }

      [Fact]
      public void CannotAddNewStringAnchorUnlessItEndsBeforeNextKnownAnchor() {
         SetFullModel(0xFF);
         var (model, viewPort) = (Model, ViewPort);
         for (int i = 0; i < 0x10; i++) model[i] = 0x00;

         // add an anchor with some data on the 2nd line
         viewPort.SelectionStart = new Point(0, 1);
         viewPort.Edit("^bob\"\" \"Hello World!\"");

         // but now, try to add a string format in the middle of all the 00 bytes
         viewPort.SelectionStart = new Point(0, 0);
         viewPort.Edit("^tom\"\" ");

         // trying to add a string anchor should've failed
         Assert.Single(Errors);
         Assert.Equal(0x10, model.GetNextRun(1).Start);
         Assert.IsType<None>(viewPort[0, 0].Format);
      }

      [Fact]
      public void UsingBackspaceMidStringMakesTheStringEndThere() {
         SetFullModel(0xFF);
         var (model, viewPort) = (Model, ViewPort);
         for (int i = 0; i < 0x10; i++) model[i] = 0x00;

         // add an anchor with some data on the 2nd line
         viewPort.SelectionStart = new Point(0, 1);
         viewPort.Edit("^bob\"\" \"Hello World!\"");
         viewPort.SelectionStart = new Point(6, 1);
         viewPort.Edit(ConsoleKey.Backspace);

         Assert.Equal("\"Hello \"", PCSString.Convert(model, 0x10, PCSString.ReadString(model, 0x10, true)));
      }

      [Fact]
      public void UsingEscapeSequencesWorks() {
         SetFullModel(0xFF);
         var (model, viewPort) = (Model, ViewPort);
         for (int i = 0; i < 0x10; i++) model[i] = 0x00;

         viewPort.Edit("^bob\"\" \"Some Content \\\\03More Content\"");

         Assert.Equal(28, model.GetNextRun(0).Length);
         Assert.IsType<EscapedPCS>(viewPort[14, 0].Format);
      }

      [Fact]
      public void CanCopyStrings() {
         SetFullModel(0xFF);
         var (model, viewPort) = (Model, ViewPort);
         for (int i = 0; i < 0x10; i++) model[i] = 0x00;
         var fileSystem = new StubFileSystem();

         viewPort.Edit("^bob\"\" \"Hello World!\"");
         viewPort.SelectionStart = new Point(0, 0);
         viewPort.SelectionEnd = new Point(12, 0);
         viewPort.Copy.Execute(fileSystem);

         Assert.Equal("^bob\"\" \"Hello World!\"", fileSystem.CopyText);
      }

      [Fact]
      public void FindForStringsIsNotCaseSensitive() {
         SetFullModel(0xFF);
         var (model, viewPort) = (Model, ViewPort);
         for (int i = 0; i < 0x10; i++) model[i] = 0x00;
         viewPort.Edit("^bob\"\" \"Text and BULBASAUR!\"");

         var results = viewPort.Find("\"bulbasaur\"").Select(result => result.start).ToList(); ;
         Assert.Single(results);
         Assert.Equal(9, results[0]);
      }

      [Fact]
      public void FindForStringsWorksWithoutQuotes() {
         SetFullModel(0xFF);
         var (model, viewPort) = (Model, ViewPort);
         for (int i = 0; i < 0x10; i++) model[i] = 0x00;
         viewPort.Edit("^bob\"\" \"Text and BULBASAUR!\"");

         var results = viewPort.Find("bulbasaur").Select(result => result.start).ToList();
         Assert.Single(results);
         Assert.Equal(9, results[0]);
      }

      [Fact]
      public void CanNameExistingStringAnchor() {
         SetFullModel(0xFF);
         var (model, viewPort) = (Model, ViewPort);
         var bytes = PCSString.Convert("Hello World!").ToArray();
         model[0] = 0x08;
         model[1] = 0x00;
         model[2] = 0x00;
         model[3] = 0x08;
         Array.Copy(bytes, 0, model.RawData, 0x08, bytes.Length);

         viewPort.SelectionStart = new Point(0x08, 0);
         viewPort.Edit("^");
         viewPort.Edit("bob\"\" ");

         Assert.Equal("bob", ((Pointer)viewPort[0, 0].Format).DestinationName);
      }

      [Fact]
      public void FormatIsRemovedWhenEditingAnAnchor() {
         SetFullModel(0xFF);
         var (model, viewPort) = (Model, ViewPort);
         var bytes = PCSString.Convert("Hello World!").ToArray();
         model[0] = 0x08;
         model[1] = 0x00;
         model[2] = 0x00;
         model[3] = 0x08;
         Array.Copy(bytes, 0, model.RawData, 0x08, bytes.Length);

         viewPort.SelectionStart = new Point(0x08, 0);
         viewPort.Edit("^");

         var underEdit = (UnderEdit)viewPort[8, 0].Format;
         Assert.Equal("^", underEdit.CurrentText);
      }

      [Fact]
      public void UsingTheAnchorEditorToSetStringFormatChangesVisibleData() {
         SetFullModel(0xFF);
         var (model, viewPort) = (Model, ViewPort);
         var bytes = PCSString.Convert("Hello World!").ToArray();
         Array.Copy(bytes, 0, model.RawData, 0x08, bytes.Length);
         viewPort.SelectionStart = new Point(0x08, 0);
         viewPort.Edit("^bob ");

         viewPort.AnchorText = "^bob\"\"";

         var anchor = (Anchor)viewPort[8, 0].Format;
         Assert.IsType<PCS>(anchor.OriginalFormat);
      }

      [Fact]
      public void CanUndoStringTruncate() {
         SetFullModel(0xFF);
         ViewPort.Edit("<008> @08 ^text\"\" \"Hello World!\" ");

         ViewPort.SelectionStart = new Point(0x0C, 0);
         ViewPort.Edit(ConsoleKey.Backspace);
         ViewPort.Undo.Execute();

         Assert.Equal(13, Model.GetNextRun(0x08).Length);
         Assert.Equal("\"Hello World!\"", ((PCS)ViewPort[0x0C, 0].Format).FullString);
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
         SetFullModel(0xFF);
         var (model, viewPort) = (Model, ViewPort);
         Array.Copy(bytes, 0, model.RawData, 0x32, bytes.Length);                // the data itself, positioned at x32 (won't be automatically found on load)
         Array.Copy(new byte[] { 0x32, 0, 0, 0x08 }, 0, model.RawData, 0x10, 4); // the pointer to the data. Pointer is aligned, but data is not.

         // the act of searching should find the anchor
         var results = viewPort.Find("this is the song");

         Assert.Single(results);
         Assert.Single(model.GetNextRun(0x32).PointerSources);
      }

      [Fact]
      public void UnnamedStringAnchorAutomaticallySearchesForPointersToAnchor() {
         var text = "This is the song that never ends.";
         var bytes = PCSString.Convert(text).ToArray();
         SetFullModel(0xFF);
         var (model, viewPort) = (Model, ViewPort);
         Array.Copy(bytes, 0, model.RawData, 0x32, bytes.Length);                // the data itself, positioned at x32 (won't be automatically found on load)
         Array.Copy(new byte[] { 0x32, 0, 0, 0x08 }, 0, model.RawData, 0x10, 4); // the pointer to the data. Pointer is aligned, but data is not.

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
         SetFullModel(0xFF);
         var (model, viewPort) = (Model, ViewPort);
         Array.Copy(bytes, 0, model.RawData, 0x30, bytes.Length);
         Array.Copy(new byte[] { 0x30, 0, 0, 0x08 }, 0, model.RawData, 0x10, 4); // the pointer to the data. Pointer is aligned, but data is not.
         var fileSystem = new StubFileSystem();
         model.Load(model.RawData, null);
         ViewPort.Refresh();

         viewPort.SelectionStart = new Point(0, 3);
         viewPort.ExpandSelection(0, 3);
         viewPort.Copy.Execute(fileSystem);

         Assert.Contains(".This", fileSystem.CopyText);
         Assert.Contains("\"\" \"This", fileSystem.CopyText); // format, then space, then start of text
      }

      [Fact]
      public void AddingNewAnchorWithSameNameRenamesNewAnchorWithMessage() {
         var (model, viewPort) = (Model, ViewPort);

         viewPort.Edit("^anchor ");
         viewPort.SelectionStart = new Point(0, 1);
         viewPort.Edit("^anchor ");

         Assert.NotEqual("anchor", ((Anchor)viewPort[0, 1].Format).Name);
         Assert.Single(Messages);
      }

      [Fact]
      public void CanUseViewPortToAutoFindTextWithoutKnowingAboutPointersToIt() {
         var text = PCSString.Convert("This is some text.");
         SetFullModel(0xFF);
         var (model, viewPort) = (Model, ViewPort);
         text.CopyTo(model.RawData, 0x10);
         model.WritePointer(new ModelDelta(), 0x00, 0x10);

         viewPort.SelectionStart = new Point(3, 1); // just a random byte in the middle of the text

         viewPort.IsText.Execute(); // this line should find the start of the text and add a run, even with no pointer to it

         Assert.IsType<PCS>(viewPort[3, 1].Format);
      }

      [Fact]
      public void ChangingTerminalByteInTableTextAddsNewTerminalByteAfter() {
         SetFullModel(0xFF);
         var (model, viewPort) = (Model, ViewPort);
         model[3] = 0xFF;
         viewPort.Edit("^table[text\"\"8]1 ");

         viewPort.Edit("abcd");

         Assert.Equal(0xFF, model[4]);
      }

      [Fact]
      public void CopyTextArrayEntryDoesNotCopyMultipleEndOfStreams() {
         var model = new PokemonModel(Enumerable.Repeat((byte)0xFF, 0x200).ToArray());
         ArrayRun.TryParse(model, "[name\"\"6]4", 0, null, out var run);
         model.ObserveAnchorWritten(new ModelDelta(), "table", run);

         var text = model.Copy(() => new ModelDelta(), 6, 6).Trim();

         Assert.Equal("+\"\"", text);
      }

      [Fact]
      public void CanAutoDetectMultipleTextRunsAtOnce() {
         var test = new BaseViewModelTestClass();
         var token = test.ViewPort.CurrentChange;

         // Arrange some undetected pointers to some undetected text
         test.Model.WritePointer(token, 0, 0x10);
         test.Model.WritePointer(token, 4, 0x15);
         int write = 0x10;
         test.Model[0xF] = 0xFF;
         PCSString.Convert("text").ForEach(b => test.Model[write++] = b);
         PCSString.Convert("more").ForEach(b => test.Model[write++] = b);

         // add some more text/pointers later on that *are* detected
         test.Model.WritePointer(token, 8, 0x40);
         test.Model[0x40] = 0xFF;
         test.ViewPort.Edit("@40 ^discovered\"\" Blob\" @00 ");

         // select from partway through the first text to partway through the second text and "Display as Text"
         test.ViewPort.SelectionStart = new Point(1, 1);
         test.ViewPort.SelectionEnd = new Point(7, 1);
         var group = (ContextItemGroup)test.ViewPort.GetContextMenuItems(new Point(4, 1)).Single(item => item.Text == "Display As...");
         var button = group.Single(item => item.Text == "Text");
         button.Command.Execute();

         // Verify that we found both
         Assert.Equal(0, test.Model.GetNextRun(0).Start);       // first pointer
         Assert.Equal(4, test.Model.GetNextRun(4).Start);       // second pointer
         Assert.Equal(0x10, test.Model.GetNextRun(0x10).Start); // first text
         Assert.Equal(0x15, test.Model.GetNextRun(0x15).Start); // second text
      }

      [Fact]
      public void PCSStringControlCodeForFuncionEscapesIsEscaped() {
         Assert.True(PCSString.IsEscaped(new byte[] { PCSString.FunctionEscape, 0x0A }, 1));
      }

      [Fact]
      public void ControlCode_Pause_OneByteIsEscaped() {
         var test = new BaseViewModelTestClass();
         int i = 0;
         Write(test.Model, ref i, "ABC");
         test.Model[i++] = PCSString.FunctionEscape;
         test.Model[i++] = 0x09; //pause: no variables
         Write(test.Model, ref i, "XYZ\"");

         test.ViewPort.Edit("^text\"\"");

         Assert.IsType<EscapedPCS>(test.ViewPort[4, 0].Format);
         Assert.IsNotType<EscapedPCS>(test.ViewPort[5, 0].Format);
         Assert.Equal("ABC\\CC09XYZ", ((PCSRun)test.Model.GetNextRun(0)).SerializeRun());
      }

      [Fact]
      public void StringWithControlCode_Parsed_CorrectBytes() {
         var model = new PokemonModel(new byte[0x100]);
         int i = 0;
         Write(model, ref i, "ABC");
         model[i++] = PCSString.FunctionEscape;
         model[i++] = 0x09; //pause: no variables
         Write(model, ref i, "XYZ\"");
         var target = model.Take(i).ToArray();

         var result = PCSString.Convert("ABC\\CC09XYZ").ToArray();

         Assert.Equal(target, result);
      }

      [Fact]
      public void Text_TypeDot_DotAdded() {
         var test = new BaseViewModelTestClass();
         Array.Copy(PCSString.Convert("Hello World").ToArray(), test.Model.RawData, 12);
         test.Model.ObserveRunWritten(test.ViewPort.CurrentChange, new PCSRun(test.Model, 0, 12));
         test.ViewPort.Refresh();

         test.ViewPort.Edit("@01 .");

         Assert.Equal(2, test.ViewPort.ConvertViewPointToAddress(test.ViewPort.SelectionStart));
         Assert.Equal("\"H.llo World\"", PCSString.Convert(test.Model, 0, 12));
      }

      [Fact]
      public void TextWithEscape_Search_Find() {
         var text = "Some\\nText";
         PCSString.Convert(text).WriteInto(Model.RawData, 0x20);

         var results = ViewPort.Find(text);

         Assert.Equal(0x20, results.Single().start);
      }

      [Fact]
      public void DisplayAsText_PreviousByteRandom_StillFindTextStartingHere() {
         PCSString.Convert("Some Content").WriteInto(Model.RawData, 0x21);
         Model.WritePointer(new ModelDelta(), 3, 0x21);
         ViewPort.Goto.Execute(0x21);

         var menu = ViewPort.GetContextMenuItems(ViewPort.SelectionStart);
         menu = menu.GetSubmenu("Display As...");
         var displayText = menu.Single(item => item.Text == "Text");
         displayText.Command.Execute();

         Assert.IsType<PCSRun>(Model.GetNextRun(0x21));
      }

      [Fact]
      public void NamelessAnchor_Copy_StillHasNoName() {
         var fs = new StubFileSystem();
         ViewPort.Edit("<100> @100 @!put(FF) ^\"\" text");

         ViewPort.ExpandSelection(0, 0);
         ViewPort.Copy.Execute(fs);

         Assert.Contains("misc", fs.CopyText.value);
         Assert.Equal("^\"\"", ViewPort.AnchorText);
      }

      [Fact]
      public void NamelessAnchor_Cut_PointerGetsName() {
         var fs = new StubFileSystem();
         ViewPort.Edit("<100> @100 @!put(FF) ^\"\" text");

         ViewPort.ExpandSelection(0, 0);
         ViewPort.Cut(fs);

         var pointer = Model.ExportMetadata(Singletons.MetadataInfo).UnmappedPointers.Single();
         Assert.Equal(0, pointer.Address);
         Assert.Contains("misc", pointer.Name);
      }

      [Fact]
      public void RedoAvailable_Copy_CanRedo() {
         var fs = new StubFileSystem();
         ViewPort.Edit("<020> @20 @!put(FF) ^\"\" text @180 DEADBEEF");
         ViewPort.Undo.Execute();

         ViewPort.Goto.Execute(0x20);
         ViewPort.ExpandSelection(0, 0);
         ViewPort.Copy.Execute(fs);

         Assert.True(ViewPort.Redo.CanExecute(default));
      }

      [Fact]
      public void TextTable_Backspace_UpdateHeaders() {
         ViewPort.UseCustomHeaders = true;
         CreateTextTable("table", 0, "adam", "bob", "carl", "dave");

         ViewPort.Goto.Execute(2);
         ViewPort.Edit(ConsoleKey.Backspace);

         Assert.NotEqual("adam", ViewPort.Headers[0]);
      }

      [Fact]
      public void TextTable_Delete_UpdateHeaders() {
         ViewPort.UseCustomHeaders = true;
         CreateTextTable("table", 0, "adam", "bob", "carl", "dave");

         ViewPort.Goto.Execute(2);
         ViewPort.Clear.Execute();

         Assert.NotEqual("adam", ViewPort.Headers[0]);
      }

      [Fact]
      public void TextTable_EditViaTableTool_UpdateHeaders() {
         ViewPort.UseCustomHeaders = true;
         CreateTextTable("table", 0, "adam", "bob", "carl", "dave");

         ViewPort.Goto.Execute(2);
         var vm = (FieldArrayElementViewModel)ViewPort.Tools.TableTool.Children[1];
         vm.Content = "am";

         Assert.NotEqual("adam", ViewPort.Headers[0]);
      }

      [Fact]
      public void ControlCharacterText_CopyPaste_SameBytesPasted() {
         SetFullModel(0xFF);
         //             T  M  1  3  cc 13 48  4  ,  0  0  0     C  O  I  N  S
         var payload = "CE C7 A2 A4 FC 13 48 A5 B8 A1 A1 A1 00 BD C9 C3 C8 CD FF".ToByteArray();
         Token.ChangeData(Model, 0, payload);
         ViewPort.Edit("^text\"\" ");

         ViewPort.ExpandSelection(0, 0);
         ViewPort.Copy.Execute(FileSystem);
         ViewPort.Goto.Execute(0x100);
         ViewPort.Edit(FileSystem.CopyText);

         var result = payload.Length.Range().Select(i => Model[i + 0x100]).ToArray();
         Assert.Equal(payload, result);
      }

      [Fact]
      public void TextList_Goto_PreferExactMatch() {
         this.CreateTextTable("names", 0x100, "content", "THUNDERPUNCH", "other", "THUNDER", "something");

         ViewPort.Goto.Execute("thunder");

         Assert.Equal(3, ViewPort.Tools.TableTool.CurrentElementSelector.SelectedIndex);
      }

      [Theory]
      [InlineData("BPRE", "[white]", "FC 01 00 FF")]
      public void TextWithMacro_Convert_CorrectBytes(string gameCode, string macro, string bytes) {
         var result = PCSString.Convert(macro, gameCode, out var containsBadCharacters);
         Assert.False(containsBadCharacters);
         Assert.Equal(bytes.ToByteArray(), result);
      }

      [Theory]
      [InlineData("BPEE", "\"[orange]\"", "FC 01 05 FF")]
      public void BytesWithMacro_Convert_CorrectText(string gameCode, string macro, string bytesAsText) {
         var bytes = bytesAsText.ToByteArray();
         var result = PCSString.Convert(gameCode, bytes, 0, bytes.Length);
         Assert.Equal(macro, result);
      }

      [Fact]
      public void TextWithMacro_ShowInHexContent_FirstByteDoesNotShowMacro() {
         SetFullModel(0xFF);
         HackTextConverter("BPRE");

         Token.ChangeData(Model, 0, new byte[] { 0xFC, 0x01, 0x05, 0xFF });
         Model.ObserveRunWritten(Token, new PCSRun(Model, 0, 4));
         ViewPort.Refresh();
         var cell = (PCS)ViewPort[0, 0].Format;

         Assert.Equal("\"\\CC", cell.ThisCharacter);
      }

      [Fact]
      public void SearchText_NoResults_NoMetadataChange() {
         var results = ViewPort.Find("Not in file");
         Assert.Empty(results);
         Assert.True(ViewPort.ChangeHistory.IsSaved);
      }

      [Fact]
      public void TextAnchor_ChangeToNoInfoAnchor_NoNameIsError() {
         SetFullModel(0xFF);
         ViewPort.Edit("^text\"\" ");

         ViewPort.AnchorText = "^";

         Assert.NotEmpty(Errors);
      }

      [Fact]
      public void AddTextAnchorName_EditWithTextTool_BytesUpdate() {
         Model[4] = 0xFF;
         Token.ChangeData(Model, 5, PCSString.Convert("test"));
         Model.WritePointer(Token, 0x100, 5);
         ViewPort.SelectionStart = new(6, 0);
         ViewPort.Shortcuts.DisplayAsText.Execute();

         ViewPort.SelectionStart = new(5, 0);
         ViewPort.AnchorText = "^name\"\"";

         ViewPort.Tools.StringTool.ContentIndex = 1;
         ViewPort.Tools.StringTool.Content = "tst";

         Assert.Equal("tst", PCSString.Convert(Model, 5, 5).Trim('"'));
      }

      [Fact]
      public void BytesWithColorMacro_CopyTextThenPaste_SameBytes() {
         SetFullModel(0xFF);
         HackTextConverter("BPRE");
         var bytes = "CD E3 E1 D9 FC 01 06 CE D9 EC E8 FF".ToByteArray(); // Some[green]Text
         Token.ChangeData(Model, 0, bytes);
         ViewPort.Refresh();
         ViewPort.Edit("^text\"\"");

         ViewPort.ExpandSelection(0, 0);
         ViewPort.Copy.Execute(FileSystem);
         ViewPort.Goto.Execute(0x30);
         ViewPort.Edit(FileSystem.CopyText);

         ViewPort.Goto.Execute(0x30);
         ViewPort.ExpandSelection(0, 0);
         ViewPort.CopyBytes.Execute(FileSystem);
         var result = FileSystem.CopyText.value.ToByteArray();
         Assert.Equal(bytes, result);
      }

      [Fact]
      public void SpecialCharacters_MatchAgainstNormalCharacters_Match() {
         Assert.True("á.é".MatchesPartialWithReordering("ea"));
         Assert.True("a.e".MatchesPartialWithReordering("éá"));
      }

      [Fact]
      public void AsciiText_LastEdit_Notify() {
         SetFullModel(0xFF);
         ViewPort.Edit("^test`asc`7 123456");

         var view = new StubView(ViewPort.Tools.StringTool);
         ViewPort.Edit("7");

         Assert.Equal("1234567", ViewPort.Tools.StringTool.Content);
         Assert.Contains(nameof(ViewPort.Tools.StringTool.Content), view.PropertyNotifications);
      }

      private void HackTextConverter(string game) {
         var converter = new PCSConverter(game);
         var property = Model.GetType().GetProperty(nameof(Model.TextConverter));
         property = property.DeclaringType.GetProperty(nameof(Model.TextConverter));
         property.GetSetMethod(true).Invoke(Model, new object[] { converter });
      }

      private void Write(IDataModel model, ref int i, string characters) {
         foreach (var c in characters.ToCharArray())
            model[i++] = (byte)PCSString.PCS.IndexOf(c.ToString());
      }
   }
}
