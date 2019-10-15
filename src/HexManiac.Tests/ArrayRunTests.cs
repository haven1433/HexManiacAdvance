using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class ArrayRunTests {
      [Fact]
      public void CanParseStringArrayRun() {
         var model = new PokemonModel(new byte[0x200]);
         ArrayRun.TryParse(model, "[name\"\"10]13", 12, null, out var arrayRun);

         Assert.Equal(12, arrayRun.Start);
         Assert.Equal(130, arrayRun.Length);
         Assert.Equal(13, arrayRun.ElementCount);
         Assert.Equal(1, arrayRun.ElementContent.Count);
         Assert.Equal("name", arrayRun.ElementContent[0].Name);
         Assert.Equal(ElementContentType.PCS, arrayRun.ElementContent[0].Type);
         Assert.Equal(10, arrayRun.ElementContent[0].Length);
      }

      [Fact]
      public void ArrayElementsMustHaveNames() {
         var model = new PokemonModel(new byte[0x200]);
         var errorInfo = ArrayRun.TryParse(model, "[\"\"10]13", 12, null, out var arrayRun); // no name given for the format member

         Assert.NotEqual(ErrorInfo.NoError, errorInfo);
      }

      [Fact]
      public void CanParseMultiStringArrayRun() {
         var model = new PokemonModel(new byte[0x200]);
         ArrayRun.TryParse(model, "[name\"\"10 detail\"\"12]13", 20, null, out var arrayRun);

         Assert.Equal(20, arrayRun.Start);
         Assert.Equal((10 + 12) * 13, arrayRun.Length);
         Assert.Equal(13, arrayRun.ElementCount);
         Assert.Equal(2, arrayRun.ElementContent.Count);
         Assert.Equal("name", arrayRun.ElementContent[0].Name);
         Assert.Equal("detail", arrayRun.ElementContent[1].Name);
         Assert.Equal(ElementContentType.PCS, arrayRun.ElementContent[0].Type);
         Assert.Equal(ElementContentType.PCS, arrayRun.ElementContent[1].Type);
         Assert.Equal(10, arrayRun.ElementContent[0].Length);
         Assert.Equal(12, arrayRun.ElementContent[1].Length);
      }

      [Fact]
      public void ViewModelArrayRunAppearsLikeABunchOfStrings() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var model = new PokemonModel(buffer);
         ArrayRun.TryParse(model, "[name\"\"12]13", 12, null, out var arrayRun);
         model.ObserveAnchorWritten(new ModelDelta(), "table", arrayRun);
         var viewPort = new ViewPort("file.txt", model) { PreferredWidth = -1, Width = 12, Height = 20 };

         // spot checks: it should look like a string that starts where the segment starts
         var pcs = (PCS)viewPort[0, 4].Format;
         Assert.Equal(0, pcs.Position);
         Assert.Equal(4 * 12, pcs.Source);

         pcs = (PCS)viewPort[6, 6].Format;
         Assert.Equal(6, pcs.Position);
         Assert.Equal(6 * 12, pcs.Source);

         // typing a " should close the current string, 0 out the rest of this segment, and move to the next segment
         viewPort.SelectionStart = new Point(6, 6);
         viewPort.Edit("\"");
         pcs = (PCS)viewPort[7, 6].Format;
         Assert.Equal(" ", pcs.ThisCharacter);
         Assert.Equal(new Point(0, 7), viewPort.SelectionStart);

         // typing Enter should move to the start of the same segment in the next element
         viewPort.Edit(ConsoleKey.Enter);
         Assert.Equal(new Point(0, 8), viewPort.SelectionStart);

         // typing Tab should move to the start of the next segment
         viewPort.Edit(ConsoleKey.Tab);
         Assert.Equal(new Point(0, 9), viewPort.SelectionStart);
      }

      [Fact]
      public void CanParseArrayAnchorAddedFromViewPort() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };

         viewPort.Edit("^bob[name\"\"14]12 ");

         Assert.IsType<ArrayRun>(model.GetNextRun(0));
      }

      [Fact]
      public void ChangingAnchorTextWhileAnchorStartIsOutOfViewWorks() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };
         viewPort.Edit("^bob[name\"\"16]16 ");

         viewPort.ScrollValue = 2;
         viewPort.AnchorText = "^bob[name\"\"16]18";

         Assert.Equal(16 * 18, model.GetNextRun(0).Length);
      }

      [Fact]
      public void CanAutoFindArrayLength() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         WriteStrings(buffer, 0x00, "bob", "tom", "sam", "car", "pal", "egg");

         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };

         viewPort.Edit("^words[word\"\"4] "); // notice, no length is given

         var run = (ArrayRun)model.GetNextRun(0);
         Assert.Equal(6, run.ElementCount);
      }

      [Fact]
      public void WidthRestrictedAfterGotoArrayAnchor() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         WriteStrings(buffer, 100, "bobb", "tomm", "samm", "carr", "pall", "eggg");

         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };

         viewPort.Edit("^words[word\"\"5] "); // notice, no length is given

         viewPort.Goto.Execute("words");
         Assert.Equal(0, viewPort.Width % 5);
      }

      [Fact]
      public void CanCopyArray() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         WriteStrings(buffer, 0, "bobb", "tomm", "samm", "carr", "pall", "eggg");
         var model = new PokemonModel(buffer);
         var delta = new ModelDelta();
         ArrayRun.TryParse(model, "[word\"\"5]", 0, null, out var arrayRun);
         model.ObserveAnchorWritten(delta, "words", arrayRun);

         var text = model.Copy(() => delta, 0, 0x20);

         Assert.StartsWith("^words[word\"\"5]", text);
      }

      [Fact]
      public void CanEditArray() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         WriteStrings(buffer, 0, "bobb", "tomm", "samm", "carr", "pall", "eggg");

         var model = new PokemonModel(buffer);
         ArrayRun.TryParse(model, "[word\"\"5]", 0, null, out var arrayRun);
         model.ObserveAnchorWritten(new ModelDelta(), "words", arrayRun);
         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };
         viewPort.SelectionStart = new Point(6, 0);

         viewPort.Edit("a"); // change "tomm" to "tamm"

         Assert.Equal("a", ((PCS)viewPort[6, 0].Format).ThisCharacter);
      }

      [Fact]
      public void CanPasteArray() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };

         viewPort.Edit("^strings[name\"\"16] ");
         viewPort.Edit("+\"bob\" +\"sam\" +\"steve\" +\"kevin\"");

         Assert.Equal(0x40, model.GetNextRun(0).Length);
         Assert.Equal("\"", ((PCS)viewPort[3, 0].Format).ThisCharacter);
      }

      [Fact]
      public void PastingArrayLeavesArrayFormat() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };

         viewPort.Edit("^pokenames[name\"\"11] +\"??????????\"+\"BULBASAUR\"");
         viewPort.SelectionStart = new Point(0, 0);
         viewPort.FollowLink(0, 0);

         Assert.Equal("^pokenames[name\"\"11]", viewPort.AnchorText);
         Assert.Equal("BULBASAUR", viewPort.Tools.StringTool.Content.Split(Environment.NewLine).Last());
      }

      [Fact]
      public void ArrayIsRecognizedByStringTool() {
         var changeToken = new ModelDelta();
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         WriteStrings(buffer, 100, "bobb", "tomm", "samm", "carr", "pall", "eggg");

         var model = new PokemonModel(buffer);
         model.WritePointer(changeToken, 200, 100);
         model.ObserveRunWritten(changeToken, new PointerRun(200));

         ArrayRun.TryParse(model, "[word\"\"5]", 100, null, out var arrayRun);
         model.ObserveAnchorWritten(new ModelDelta(), "words", arrayRun);
         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };

         viewPort.FollowLink(0, 7); // 7*16 = 112, right in the middle of our data
         // note that this will change our width to 15, because we're linking to data of width 5 when our maxwidth is 16.

         Assert.Equal(0, viewPort.Tools.SelectedIndex); // string tool is selected
         Assert.Equal(100, viewPort.Tools.StringTool.Address);
         var lineCount = viewPort.Tools.StringTool.Content.Split(Environment.NewLine).Length;
         Assert.Equal(6, lineCount);

         viewPort.Tools.StringTool.ContentIndex = viewPort.Tools.StringTool.Content.IndexOf("pall");
         Assert.Equal(new Point(120 % 16, 120 / 16), viewPort.SelectionStart);
      }

      [Fact]
      public void EditingStringToolEditsArray() {
         var changeToken = new ModelDelta();
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         WriteStrings(buffer, 100, "bobb", "tomm", "samm", "carr", "pall", "eggg");

         var model = new PokemonModel(buffer);
         model.WritePointer(changeToken, 200, 100);
         model.ObserveRunWritten(changeToken, new PointerRun(200));

         ArrayRun.TryParse(model, "[word\"\"5]", 100, null, out var arrayRun);
         model.ObserveAnchorWritten(new ModelDelta(), "words", arrayRun);
         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };

         viewPort.FollowLink(0, 7); // 7*16 = 112, right in the middle of our data

         var writer = new StringBuilder();
         writer.AppendLine(viewPort.Tools.StringTool.Content);
         writer.Append("carl");
         viewPort.Tools.StringTool.Content = writer.ToString();

         Assert.Equal(7 * 5, model.GetNextRun(100).Length);
      }

      [Fact]
      public void CanCutPasteArrayToFreeSpace() {
         // arrange
         var delta = new ModelDelta();
         var elements = new[] { "123", "alice", "candy land", "hello world", "fortify" };
         var buffer = Enumerable.Range(0, 0x200).Select(i => (byte)0xFF).ToArray();
         for (int i = 0; i < elements.Length; i++) {
            var content = PCSString.Convert(elements[i]);
            while (content.Count < 0x10) content.Add(0x00);
            Array.Copy(content.ToArray(), 0, buffer, 0x10 * i + 0x20, 0x10);
         }
         var model = new PokemonModel(buffer);
         model.WritePointer(delta, 0x00, 0x20);
         model.ObserveRunWritten(delta, new PointerRun(0x00));
         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };
         viewPort.SelectionStart = new Point(0, 2);
         viewPort.Edit("^testdata[name\"\"16]5 ");
         viewPort.Goto.Execute("000000");

         // act -> cut
         var fileSystem = new StubFileSystem();
         viewPort.SelectionStart = new Point(0, 2);
         viewPort.SelectionEnd = new Point(0xF, 6); // select all 5 elements
         viewPort.Copy.Execute(fileSystem);
         viewPort.Clear.Execute();
         string text = fileSystem.CopyText;

         // act -> paste
         viewPort.SelectionStart = new Point(0, 8);
         viewPort.Edit(text);

         // assert -> pointer moved
         var destination = model.ReadPointer(0x00);
         Assert.Equal(0x80, destination);

         // assert -> anchor moved
         var run = model.GetNextRun(0x10);
         Assert.Equal(0x80, run.Start);
         Assert.Equal("testdata", model.GetAnchorFromAddress(-1, run.Start));
         Assert.Equal(5, ((ArrayRun)run).ElementCount);
         var lines = viewPort.Tools.StringTool.Content.Split(Environment.NewLine);
         Assert.All(elements, element => Assert.Contains(element, lines));

         // assert -> nothing left behind
         Assert.All(Enumerable.Range(0x20, 0x50), i => Assert.Equal(0xFF, model[i]));
      }

      [Fact]
      public void CannotCutPasteArrayToMakeItHitAnotherAnchor() {
         // arrange
         var delta = new ModelDelta();
         var errors = new List<string>();
         var elements = new[] { "123", "alice", "candy land", "hello world", "fortify" };
         var buffer = Enumerable.Range(0, 0x200).Select(i => (byte)0xFF).ToArray();
         for (int i = 0; i < elements.Length; i++) {
            var content = PCSString.Convert(elements[i]);
            while (content.Count < 0x10) content.Add(0x00);
            Array.Copy(content.ToArray(), 0, buffer, 0x10 * i + 0x20, 0x10);
         }
         var model = new PokemonModel(buffer);
         model.WritePointer(delta, 0x00, 0x20);
         model.ObserveRunWritten(delta, new PointerRun(0x00));
         model.WritePointer(delta, 0x04, 0x90);
         model.ObserveRunWritten(delta, new PointerRun(0x04)); // the anchor at 0x90 should prevent a paste overwrite
         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };
         viewPort.SelectionStart = new Point(0, 2);
         viewPort.Edit("^testdata[name\"\"16]5 ");
         viewPort.Goto.Execute("000000");
         viewPort.OnError += (sender, message) => errors.Add(message);

         // act -> cut
         var fileSystem = new StubFileSystem();
         viewPort.SelectionStart = new Point(0, 2);
         viewPort.SelectionEnd = new Point(0xF, 6); // select all 5 elements
         viewPort.Copy.Execute(fileSystem);
         viewPort.Clear.Execute();
         string text = fileSystem.CopyText;

         // act -> paste
         viewPort.SelectionStart = new Point(0, 8);
         viewPort.Edit(text);

         // assert: could not paste
         Assert.Single(errors);
      }

      [Fact]
      public void CanCutPasteArrayOverItself() {
         // arrange
         var delta = new ModelDelta();
         var errors = new List<string>();
         var elements = new[] { "123", "alice", "candy land", "hello world", "fortify" };
         var buffer = Enumerable.Range(0, 0x200).Select(i => (byte)0xFF).ToArray();
         for (int i = 0; i < elements.Length; i++) {
            var content = PCSString.Convert(elements[i]);
            while (content.Count < 0x10) content.Add(0x00);
            Array.Copy(content.ToArray(), 0, buffer, 0x10 * i + 0x20, 0x10);
         }
         var model = new PokemonModel(buffer);
         model.WritePointer(delta, 0x00, 0x20);
         model.ObserveRunWritten(delta, new PointerRun(0x00));
         model.WritePointer(delta, 0x04, 0x90);
         model.ObserveRunWritten(delta, new PointerRun(0x04)); // the anchor at 0x90 should prevent a paste overwrite
         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };
         viewPort.SelectionStart = new Point(0, 2);
         viewPort.Edit("^testdata[name\"\"16]5 ");
         viewPort.Goto.Execute("000000");
         viewPort.OnError += (sender, message) => errors.Add(message);

         // act -> cut
         var fileSystem = new StubFileSystem();
         viewPort.SelectionStart = new Point(0, 2);
         viewPort.SelectionEnd = new Point(0xF, 6); // select all 5 elements
         viewPort.Copy.Execute(fileSystem);
         viewPort.Clear.Execute();
         string text = fileSystem.CopyText;

         // act -> paste
         viewPort.SelectionStart = new Point(0, 2);
         viewPort.Edit(text);

         // assert: no errors
         Assert.Empty(errors);
         Assert.Equal("testdata", model.GetAnchorFromAddress(-1, 0x20));
      }

      [Fact]
      public void AddingToAnArrayWithFixedLengthUpdatesTheAnchorFormat() {
         // arrange
         var delta = new ModelDelta();
         var errors = new List<string>();
         var elements = new[] { "123", "alice", "candy land", "hello world", "fortify" };
         var buffer = Enumerable.Range(0, 0x200).Select(i => (byte)0xFF).ToArray();
         for (int i = 0; i < elements.Length; i++) {
            var content = PCSString.Convert(elements[i]);
            while (content.Count < 0x10) content.Add(0x00);
            Array.Copy(content.ToArray(), 0, buffer, 0x10 * i + 0x20, 0x10);
         }
         var model = new PokemonModel(buffer);
         model.WritePointer(delta, 0x00, 0x20);
         model.ObserveRunWritten(delta, new PointerRun(0x00));
         model.WritePointer(delta, 0x04, 0x90);
         model.ObserveRunWritten(delta, new PointerRun(0x04)); // the anchor at 0x90 should prevent a paste overwrite
         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };
         viewPort.SelectionStart = new Point(0, 2);
         viewPort.Edit("^testdata[name\"\"16]5 ");
         viewPort.Goto.Execute("000000");
         viewPort.OnError += (sender, message) => errors.Add(message);

         // act -> add an element
         viewPort.SelectionStart = new Point(0, 7);
         viewPort.Edit("+\"crab\"");

         // assert -> length changed
         viewPort.SelectionStart = new Point(0, 2);
         Assert.True(viewPort.AnchorTextVisible);
         Assert.Equal("^testdata[name\"\"16]6", viewPort.AnchorText);
      }

      [Fact]
      public void ArarysSupportIntegers() {
         var buffer = new byte[0x200];
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };
         var errors = new List<string>();
         viewPort.OnError += (sender, e) => errors.Add(e);

         viewPort.Edit("^sample[initials\"\"5 age. shoeSize: extension. zip:. flog::]16 ");

         Assert.Empty(errors);
         Assert.Equal(0x100, model.GetNextRun(0).Length);
      }

      [Fact]
      public void ArrayExtendsIfBasedOnAnotherNameWhichIsExtended() {
         var buffer = new byte[0x200];
         for (int i = 0; i < buffer.Length; i++) buffer[i] = 0xFF;
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };
         var errors = new List<string>();
         viewPort.OnError += (sender, e) => errors.Add(e);

         viewPort.Edit("^sample[name\"\"8]8 "); // should cover the first 4 lines

         viewPort.SelectionStart = new Point(0, 8);
         viewPort.Edit("^derived[index.]sample "); // should be the same length as sample: 8
         viewPort.Goto.Execute("000000");

         // test 1: enbiggen derived should enbiggen sample
         viewPort.SelectionStart = new Point(8, 8);
         viewPort.Edit("+");
         Assert.Equal(8 * 9, model.GetNextRun(0).Length);

         // test 2: enbiggen sample should enbiggen derived
         viewPort.SelectionStart = new Point(8, 4);
         viewPort.Edit("+");
         Assert.Equal(1 * 10, model.GetNextRun(0x80).Length);
      }

      [Fact]
      public void CanEditIntsInArray() {
         var buffer = new byte[0x200];
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };
         var errors = new List<string>();
         viewPort.OnError += (sender, e) => errors.Add(e);
         viewPort.Edit("^sample[code:]8 ");

         viewPort.Edit("1 20 300 4000 50000 6000000 ");

         Assert.Equal(new Point(12, 0), viewPort.SelectionStart);
         Assert.Single(errors); // should've gotten one error for the 6 digit number
         Assert.Equal(1, model[5]);
      }

      [Fact]
      public void ArraysSupportPointers() {
         var buffer = new byte[0x200];
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };
         var errors = new List<string>();
         viewPort.OnError += (sender, e) => errors.Add(e);

         viewPort.Edit("^characters[name\"\"8 age. gender. weight: catchphrase<>]16 ");

         Assert.Empty(errors);
         Assert.Equal(0x100, model.GetNextRun(0).Length);
      }

      [Fact]
      public void CanEditPointersInArrays() {
         var buffer = new byte[0x200];
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };
         var errors = new List<string>();
         viewPort.OnError += (sender, e) => errors.Add(e);
         viewPort.Edit("^sample[name<>]8 ");

         viewPort.Edit("<000100> <110> <000120>");

         Assert.Equal(0x20, model.GetNextRun(0).Length); // 4 bytes per pointer * 8 pointers = 8*4 = x10*2 = x20 bytes
         Assert.Equal(new Point(0xC, 0), viewPort.SelectionStart);
         Assert.Empty(errors);
         Assert.Equal(0x120, model.ReadPointer(0x8));
      }

      [Fact]
      public void ArraysWithInnerAnchorsRenderAnchors() {
         var data = new byte[0x200];
         var changeToken = new ModelDelta();

         // arrange: setup data with a bunch of pointers pointing into an array of strings
         var model = new PokemonModel(data);
         model.WritePointer(changeToken, 0x00, 0x80);
         model.ObserveRunWritten(changeToken, new PointerRun(0x00));
         model.WritePointer(changeToken, 0x08, 0x84);
         model.ObserveRunWritten(changeToken, new PointerRun(0x08));
         model.WritePointer(changeToken, 0x10, 0x88);
         model.ObserveRunWritten(changeToken, new PointerRun(0x10));
         model.WritePointer(changeToken, 0x18, 0x8C);
         model.ObserveRunWritten(changeToken, new PointerRun(0x18));

         // arrange: setup the array of strings
         WriteStrings(data, 0x80, "cat", "bat", "hat", "sat");
         var existingAnchor = model.GetNextAnchor(0x80);
         var error = ArrayRun.TryParse(model, "^[name\"\"4]4", 0x80, existingAnchor.PointerSources, out var arrayRun);
         model.ObserveAnchorWritten(changeToken, "sample", arrayRun);

         // arrange: create the viewmodel
         var viewport = new ViewPort("name", model) { Width = 0x10, Height = 0x10 };

         // assert: viewmodel renders anchors within the array
         // note that the strings are only 4 bytes long
         Assert.IsType<Anchor>(viewport[0, 8].Format);
         Assert.IsType<Anchor>(viewport[4, 8].Format);
         Assert.IsType<Anchor>(viewport[8, 8].Format);
         Assert.IsType<Anchor>(viewport[12, 8].Format);
         Assert.IsNotType<Anchor>(viewport[0, 9].Format);

         // assert: viewmodel renders pointers with names into the array
         Assert.Equal("sample", ((Pointer)viewport[0, 0].Format).DestinationName);
         Assert.Equal("sample/1", ((Pointer)viewport[8, 0].Format).DestinationName);
         Assert.Equal("sample/2", ((Pointer)viewport[0, 1].Format).DestinationName);
         Assert.Equal("sample/3", ((Pointer)viewport[8, 1].Format).DestinationName);
      }

      [Fact]
      public void CanGotoIndexInArray() {
         var data = new byte[0x200];
         var changeToken = new ModelDelta();

         // arrange: setup data with a bunch of pointers pointing into an array of strings
         var model = new PokemonModel(data);

         // arrange: setup the array of strings
         WriteStrings(data, 0x80, "cat", "bat", "hat", "sat");
         var existingAnchor = model.GetNextAnchor(0x80);
         var error = ArrayRun.TryParse(model, "[name\"\"4]4", 0x80, existingAnchor.PointerSources, out var arrayRun);
         model.ObserveAnchorWritten(changeToken, "sample", arrayRun);

         // arrange: create the viewmodel
         var viewport = new ViewPort("name", model) { Width = 0x10, Height = 0x10 };

         var errorMessages = new List<string>();
         viewport.OnError += (sender, message) => errorMessages.Add(message);
         viewport.Goto.Execute("sample/2");

         Assert.Empty(errorMessages);
      }

      [Fact]
      public void CanRemovePointerToWithinArray() {
         var data = new byte[0x200];
         var changeToken = new ModelDelta();

         // arrange: setup data with a bunch of pointers pointing into an array of strings
         var model = new PokemonModel(data);
         model.WritePointer(changeToken, 0x00, 0x80);
         model.ObserveRunWritten(changeToken, new PointerRun(0x00));
         model.WritePointer(changeToken, 0x08, 0x84);
         model.ObserveRunWritten(changeToken, new PointerRun(0x08));
         model.WritePointer(changeToken, 0x10, 0x88);
         model.ObserveRunWritten(changeToken, new PointerRun(0x10));
         model.WritePointer(changeToken, 0x18, 0x8C);
         model.ObserveRunWritten(changeToken, new PointerRun(0x18));

         // arrange: setup the array of strings
         WriteStrings(data, 0x80, "cat", "bat", "hat", "sat");
         var existingAnchor = model.GetNextAnchor(0x80);
         var error = ArrayRun.TryParse(model, "^[name\"\"4]4", 0x80, existingAnchor.PointerSources, out var arrayRun);
         model.ObserveAnchorWritten(changeToken, "sample", arrayRun);

         // act: clear the pointer
         model.ClearFormat(changeToken, 0x08, 4);

         // assert: array doesn't have pointer anymore
         var array = (ArrayRun)model.GetNextAnchor(0x80);
         Assert.Empty(array.PointerSourcesForInnerElements[1]);
      }

      [Fact]
      public void CanCreateArraySupportingInnerAnchorsFromViewModel() {
         var data = new byte[0x200];
         var changeToken = new ModelDelta();
         var model = new PokemonModel(data);
         var viewport = new ViewPort("name", model) { Width = 0x10, Height = 0x10 };

         viewport.Edit("^array^[content\"\"16]16 ");
         var run = (ArrayRun)model.GetNextRun(0);

         Assert.StartsWith("^", run.FormatString);
      }

      [Fact]
      public void CanCreateNewPointerUsingArrayNameAndIndex() {
         var data = new byte[0x200];
         var changeToken = new ModelDelta();
         var model = new PokemonModel(data);
         var viewport = new ViewPort("name", model) { Width = 0x10, Height = 0x10 };

         viewport.Edit("^array^[content\"\"8]8 ");

         viewport.SelectionStart = new Point(0, 6);
         viewport.Edit("<array/3>");

         var run = (ArrayRun)model.GetNextRun(0);
         Assert.Single(run.PointerSourcesForInnerElements[3]);
      }

      [Fact]
      public void ArraysSupportEnums() {
         var data = new byte[0x200];
         var changeToken = new ModelDelta();
         var model = new PokemonModel(data);

         // arrange: setup the anchor used for the enums
         WriteStrings(data, 0x00, "cat", "bat", "hat", "sat");
         var error = ArrayRun.TryParse(model, "^[name\"\"4]4", 0x00, null, out var arrayRun);
         model.ObserveAnchorWritten(changeToken, "sample", arrayRun);

         // arrange: setup the anchor with the data
         error = ArrayRun.TryParse(model, "[option:sample]4", 0x40, null, out arrayRun);
         model.ObserveAnchorWritten(changeToken, "data", arrayRun);

         changeToken.ChangeData(model, 0x42, 2);

         // act: see that the arrayRun can parse according to the enum
         arrayRun = (ArrayRun)model.GetNextRun(0x40);
         Assert.Equal("cat", arrayRun.ElementContent[0].ToText(model, 0x40));
         Assert.Equal("hat", arrayRun.ElementContent[0].ToText(model, 0x42));

         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };
         var enumViewModel = (IntegerEnum)((Anchor)viewPort[0, 4].Format).OriginalFormat;
         Assert.Equal("cat", enumViewModel.Value);
      }

      [Fact]
      public void ArraysSupportEditingEnums() {
         var data = new byte[0x200];
         data[0x42] = 2; // hat
         var changeToken = new ModelDelta();
         var model = new PokemonModel(data);

         // arrange: setup the anchor used for the enums
         WriteStrings(data, 0x00, "cat", "bat", "hat", "sat");
         var error = ArrayRun.TryParse(model, "^[name\"\"4]4", 0x00, null, out var arrayRun);
         model.ObserveAnchorWritten(changeToken, "sample", arrayRun);

         // arrange: setup the anchor with the data
         error = ArrayRun.TryParse(model, "[option.sample]4", 0x40, null, out arrayRun);
         model.ObserveAnchorWritten(changeToken, "data", arrayRun);

         // act: use a viewmodel to change 0x41 to 'bat'
         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };
         viewPort.SelectionStart = new Point(1, 4); // select space 0x41
         viewPort.Edit("bat ");

         Assert.Equal(1, data[0x41]);
      }

      [Fact]
      public void ViewModelReturnsErrorWhenEnumIsNotValidValue() {
         var data = new byte[0x200];
         data[0x42] = 2; // hat
         var changeToken = new ModelDelta();
         var model = new PokemonModel(data);

         // arrange: setup the anchor used for the enums
         WriteStrings(data, 0x00, "cat", "bat", "hat", "sat");
         var error = ArrayRun.TryParse(model, "^[name\"\"4]4", 0x00, null, out var arrayRun);
         model.ObserveAnchorWritten(changeToken, "sample", arrayRun);

         // arrange: setup the anchor with the data
         error = ArrayRun.TryParse(model, "[option.sample]4", 0x40, null, out arrayRun);
         model.ObserveAnchorWritten(changeToken, "data", arrayRun);

         // act: use a viewmodel to try to change 41 to 'pat' (invalid)
         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };
         viewPort.SelectionStart = new Point(1, 4); // select space 0x41
         var errors = new List<string>();
         viewPort.OnError += (sender, e) => errors.Add(e);

         viewPort.Edit("pat ");
         Assert.Single(errors);
      }

      [Fact]
      public void MultipleEnumValuesWithSameContentAreDistinguishable() {
         var data = new byte[0x200];
         data[0x42] = 2; // bat~2
         var changeToken = new ModelDelta();
         var model = new PokemonModel(data);

         // arrange: setup the anchor used for the enums
         WriteStrings(data, 0x00, "cat", "bat", "bat", "sat");
         var error = ArrayRun.TryParse(model, "^[name\"\"4]4", 0x00, null, out var arrayRun);
         model.ObserveAnchorWritten(changeToken, "sample", arrayRun);

         // arrange: setup the anchor with the data
         error = ArrayRun.TryParse(model, "[option.sample]4", 0x40, null, out arrayRun);
         model.ObserveAnchorWritten(changeToken, "data", arrayRun);

         // act: setup a viewmodel
         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };

         // assert: viewmodel should render bat~2 at 0x42
         var format = (IntegerEnum)viewPort[2, 4].Format;
         Assert.Equal("bat~2", format.Value);
      }

      [Fact]
      public void CanEditToSecondEnumWithSameContent() {
         var data = new byte[0x200];
         data[0x42] = 3; // sat
         var changeToken = new ModelDelta();
         var model = new PokemonModel(data);

         // arrange: setup the anchor used for the enums
         WriteStrings(data, 0x00, "cat", "bat", "bat", "sat");
         var error = ArrayRun.TryParse(model, "^[name\"\"4]4", 0x00, null, out var arrayRun);
         model.ObserveAnchorWritten(changeToken, "sample", arrayRun);

         // arrange: setup the anchor with the data
         error = ArrayRun.TryParse(model, "[option.sample]4", 0x40, null, out arrayRun);
         model.ObserveAnchorWritten(changeToken, "data", arrayRun);

         // act: setup a viewmodel and change 0x41 to bat~2
         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };
         viewPort.SelectionStart = new Point(1, 4); // select space 0x41
         viewPort.Edit("bat~2 ");

         // assert: viewmodel should render bat~2 at 0x42
         Assert.Equal(2, data[0x41]);
      }

      [Fact]
      public void EditingWithTableToolUpdatesMainContent() {
         var data = new byte[0x200];
         data[0x42] = 3; // sat
         var changeToken = new ModelDelta();
         var model = new PokemonModel(data);

         // arrange: setup the anchor used for the enums
         WriteStrings(data, 0x00, "cat", "bat", "bat", "sat");
         var error = ArrayRun.TryParse(model, "^[name\"\"4]4", 0x00, null, out var arrayRun);
         model.ObserveAnchorWritten(changeToken, "sample", arrayRun);

         // act: setup a viewmodel and show table tool
         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };
         viewPort.Tools.SelectedIndex = 1;

         // act: change the table contents
         viewPort.SelectionStart = new Point(1, 0);
         var element = (FieldArrayElementViewModel)viewPort.Tools.TableTool.Children[0];
         element.Content = "dog";

         // assert: main view was updated
         Assert.Equal("o", ((PCS)viewPort[1, 0].Format).ThisCharacter);
      }

      [Fact]
      public void EditingMainContentUpdatesTableTool() {
         var data = new byte[0x200];
         data[0x42] = 3; // sat
         var changeToken = new ModelDelta();
         var model = new PokemonModel(data);

         // arrange: setup the anchor used for the enums
         WriteStrings(data, 0x00, "cat", "bat", "bat", "sat");
         var error = ArrayRun.TryParse(model, "^[name\"\"4]4", 0x00, null, out var arrayRun);
         model.ObserveAnchorWritten(changeToken, "sample", arrayRun);

         // act: setup a viewmodel and show table tool
         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };
         viewPort.Tools.SelectedIndex = 1;

         // act: change the table contents
         viewPort.SelectionStart = new Point(1, 0);
         viewPort.Edit("u");

         // assert: main view was updated
         var element = (FieldArrayElementViewModel)viewPort.Tools.TableTool.Children[0];
         Assert.Equal("cut", element.Content);
      }

      [Fact]
      public void CustomHeadersWork() {
         var data = new byte[0x200];
         var changeToken = new ModelDelta();
         var model = new PokemonModel(data);

         // arrange: setup the anchor used for the enums
         WriteStrings(data, 0x00, "cat", "bat", "bat", "sat");
         ArrayRun.TryParse(model, "^[name\"\"4]4", 0x00, null, out var parentArray);
         model.ObserveAnchorWritten(changeToken, "parent", parentArray);
         ArrayRun.TryParse(model, "[a:: b:: c:: d::]parent", 0x20, null, out var childArray);
         model.ObserveAnchorWritten(changeToken, "child", childArray);
         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };

         // act/assert: check that the headers are the names when custom headers are turned on
         viewPort.UseCustomHeaders = true;
         Assert.Equal("cat", viewPort.Headers[2]);

         // act/assert: check that the headers are normal when custom headers are turned off
         viewPort.UseCustomHeaders = false;
         Assert.Equal("000020", viewPort.Headers[2]);
      }

      [Fact]
      public void CanAddTextFormatToAnchorUsedOnlyByAnArrayAtStart() {
         var data = new byte[0x200];
         var changeToken = new ModelDelta();
         var model = new PokemonModel(data);
         WriteStrings(data, 0x10, "This is a song!");
         ArrayRun.TryParse(model, "^[content<>]1", 0x00, null, out var array);
         model.ObserveAnchorWritten(changeToken, "array", array);
         model.WritePointer(changeToken, 0x00, 0x10);

         // there is a pointer at 0x00 that points to 0x10
         // but we know about it via an array
         // at 0x10 is text
         // but we don't know that it's text
         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };
         var errors = new List<string>();
         viewPort.OnError += (sender, e) => errors.Add(e);
         viewPort.SelectionStart = new Point(0, 1);
         viewPort.Edit("^\"\" ");

         // adding the format should've stuck
         Assert.Empty(errors);
         Assert.IsType<PCS>(viewPort[1, 1].Format);
      }

      [Fact]
      public void CanAddTextFormatToAnchorUsedOnlyByAnArray() {
         var data = new byte[0x200];
         var changeToken = new ModelDelta();
         var model = new PokemonModel(data);
         WriteStrings(data, 0x10, "This is a song!");
         ArrayRun.TryParse(model, "^[content<>]4", 0x00, null, out var array);
         model.ObserveAnchorWritten(changeToken, "array", array);
         model.WritePointer(changeToken, 0x04, 0x10);

         // there is a pointer at 0x00 that points to 0x10
         // but we know about it via an array
         // at 0x10 is text
         // but we don't know that it's text
         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };
         var errors = new List<string>();
         viewPort.OnError += (sender, e) => errors.Add(e);
         viewPort.SelectionStart = new Point(0, 1);
         viewPort.Edit("^\"\" ");

         // adding the format should've stuck
         Assert.Empty(errors);
         Assert.IsType<PCS>(viewPort[1, 1].Format);
      }

      [Fact]
      public void EditingMultibyteTableEntryMovesEditToFirstByte() {
         // Arrange
         var data = new byte[0x200];
         var model = new PokemonModel(data);
         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };
         viewPort.Edit("^names[name\"\"8]8 \"bob\" \"sam\" \"john\" \"mike\" \"tommy\"");
         viewPort.SelectionStart = new Point(0, 5);
         viewPort.Edit("^table[a: b:names]8 "); // note that making a table like this does an automatic goto for the table

         // Act: try to edit a
         viewPort.SelectionStart = new Point(1, 0);
         viewPort.Edit("3");

         // Assert: selection moved
         Assert.True(viewPort.IsSelected(new Point(0, 0)));
         Assert.IsType<UnderEdit>(viewPort[0, 0].Format);

         // Act: try to edit b
         viewPort.SelectionStart = new Point(3, 0);
         viewPort.Edit("john");

         // Assert: selection moved
         Assert.True(viewPort.IsSelected(new Point(2, 0)));
         Assert.IsType<UnderEdit>(viewPort[2, 0].Format);
      }

      [Fact]
      public void CanBackspaceEnum() {
         // Arrange
         var data = new byte[0x200];
         var model = new PokemonModel(data);
         model[0x51] = 3; // 'john'
         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };
         viewPort.Edit("^names[name\"\"8]8 \"bob\" \"sam\" \"john\" \"mike\" \"tommy\"");
         viewPort.SelectionStart = new Point(0, 5);
         viewPort.Edit("^table[a:names b:]8 "); // note that making a table like this does an automatic goto for the table

         // Act: try to backspace the a enum
         viewPort.SelectionStart = new Point(2, 1); // just to the left of "john"
         viewPort.Edit(ConsoleKey.Backspace);
         viewPort.Edit(ConsoleKey.Backspace);
         viewPort.Edit(ConsoleKey.Backspace);
         viewPort.Edit(ConsoleKey.Backspace);
         viewPort.Edit(ConsoleKey.Backspace);

         Assert.Equal("bob", ((IntegerEnum)viewPort[0, 1].Format).Value);
         Assert.True(viewPort.IsSelected(new Point(0xF, 0))); // selection has moved to last row
         Assert.True(viewPort.IsSelected(new Point(0xE, 0))); // two selected bytes, since the previous entry is 2 bytes long
      }

      [Fact]
      public void CanBackspaceInt() {
         // Arrange
         var data = new byte[0x200];
         var model = new PokemonModel(data);
         model[0x53] = 9;
         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };
         viewPort.Edit("^names[name\"\"8]8 \"bob\" \"sam\" \"john\" \"mike\" \"tommy\"");
         viewPort.SelectionStart = new Point(0, 5);
         viewPort.Edit("^table[a:names b:]8 "); // note that making a table like this does an automatic goto for the table

         // Act: try to backspace the a int
         viewPort.SelectionStart = new Point(3, 1); // selected "9"
         viewPort.Edit(ConsoleKey.Backspace);
         viewPort.Edit(ConsoleKey.Backspace);

         Assert.Equal(0, ((Integer)viewPort[2, 1].Format).Value);
         Assert.True(viewPort.IsSelected(new Point(0, 1))); // selection has moved to previous element
         Assert.True(viewPort.IsSelected(new Point(1, 1))); // two selected bytes, since the previous entry is 2 bytes long
      }

      [Fact]
      public void ArrayLengthUpdatesWhenSourceTableLengthChanges() {
         // Arrange
         var data = new byte[0x200];
         var model = new PokemonModel(data);

         // Act
         ArrayRun.TryParse(model, "[a: b:]names", 0, null, out var table);
         model.ObserveAnchorWritten(new ModelDelta(), "table", table);
         ArrayRun.TryParse(model, "[name\"\"8]8", 0x30, null, out var names);
         model.ObserveAnchorWritten(new ModelDelta(), "names", names);

         // Assert that the table is now longer based on the names table
         Assert.Equal(8 * 4, model.GetNextRun(0).Length);
      }

      [Fact]
      public void ArrayLengthRightClickOptionWhenClickingInLastRow() {
         // Arrange
         var data = new byte[0x200];
         var model = new PokemonModel(data);
         ArrayRun.TryParse(model, "[a: b:]8", 0, null, out var table);
         model.ObserveAnchorWritten(new ModelDelta(), "table", table);
         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };

         // Act
         viewPort.SelectionStart = new Point(0xD, 1);
         var items = viewPort.GetContextMenuItems(viewPort.SelectionStart);
         var extendItem = items.Single(item => item.Text.Contains("Extend Table"));
         extendItem.Command.Execute();

         // Assert
         Assert.Equal(9, ((ArrayRun)model.GetNextRun(0)).ElementCount);
      }

      /// <summary>
      /// Situation:
      /// Child data is in ROM directly before parent data.
      /// When child data is extended, parent data needs to be extended first.
      /// When parent data is extended, it auto-moves.
      /// When child data is extended, suddenly there's enough room because of the other move, so it doesn't need to move.
      /// Notify that the parent data moved.
      /// </summary>
      [Fact]
      public void ParentTableAutoMoveNotifies() {
         // Arrange
         var data = Enumerable.Range(0, 0x200).Select(i => (byte)0x42).ToArray(); // fill with 'stuff' so that the parent table will have to move.
         var model = new PokemonModel(data);

         ArrayRun.TryParse(model, "[a: b:]8", 0x20, null, out var table); // parent table starts directly after child table
         model.ObserveAnchorWritten(new ModelDelta(), "parent", table);

         ArrayRun.TryParse(model, "[a: b:]parent", 0x00, null, out table);
         model.ObserveAnchorWritten(new ModelDelta(), "child", table);

         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };
         var messages = new List<string>();
         viewPort.OnMessage += (sender, e) => messages.Add(e);

         // Act
         viewPort.SelectionStart = new Point(0xD, 1);
         viewPort.Tools.SelectedIndex = viewPort.Tools.IndexOf(viewPort.Tools.TableTool);
         viewPort.Tools.TableTool.Append.Execute();

         // Assert
         Assert.Equal(0, model.GetNextRun(0).Start); // the run being edit did not move
         Assert.Single(messages);                    // user was notified about the other move
      }

      [Fact]
      public void CanHaveLooseWordRunsReferingToTables() {
         StandardSetup(out var data, out var model, out var viewPort);

         viewPort.Edit("^table[a:: b::]4 "); // 0x20 bytes
         viewPort.SelectionStart = new Point(0, 5);
         viewPort.Edit("::table ");                  // should create a new 4-byte run that stays in sync with the table

         var run = model.GetNextRun(0x50);
         Assert.IsType<WordRun>(run);
         Assert.IsType<MatchedWord>(viewPort[1, 5].Format);
         Assert.Equal(new Point(4, 5), viewPort.SelectionStart);

         var length = model.ReadValue(0x50);
         Assert.Equal(4, length);

         viewPort.SelectionStart = new Point(0, 2);
         viewPort.Edit("+");                         // should add a new element to the table

         Assert.Equal(5, ((ArrayRun)model.GetNextRun(0)).ElementCount);
         length = model.ReadValue(0x50);
         Assert.Equal(5, length);
      }

      [Fact]
      public void CanRemoveLooseWordRuns() {
         StandardSetup(out var data, out var model, out var viewPort);
         viewPort.Edit("^table[a:: b::]4 "); // 0x20 bytes

         // add the format
         viewPort.SelectionStart = new Point(0, 5);
         viewPort.Edit("::table ");                  // should create a new 4-byte run that stays in sync with the table

         // remove the format
         viewPort.SelectionStart = new Point(0, 5);
         var items = viewPort.GetContextMenuItems(new Point(0, 5));
         var contextItem = items.Single(item => item.Text == "Clear Format");
         contextItem.Command.Execute();

         // extend the table
         viewPort.SelectionStart = new Point(0, 2);
         viewPort.Edit("+");

         // verify that the table was extended, but the value at 0x50 was NOT (no longer tied together)
         Assert.Equal(5, ((ArrayRun)model.GetNextRun(0)).ElementCount);
         var length = model.ReadValue(0x50);
         Assert.Equal(4, length);
      }

      [Fact]
      public void CanSaveLooseWordRuns() {
         var fileSystem = new StubFileSystem();
         string[] metadata = null;
         fileSystem.Save = file => true;
         fileSystem.SaveMetadata = (file, lines) => { metadata = lines; return true; };
         StandardSetup(out var data, out var model, out var viewPort);

         viewPort.Edit("::test ");
         viewPort.Save.Execute(fileSystem);

         var storedMetadata = new StoredMetadata(metadata);
         var matchedWord = storedMetadata.MatchedWords.First();
         Assert.Equal(0, matchedWord.Address);
         Assert.Equal("test", matchedWord.Name);
      }

      [Fact]
      public void AppendToTableMovesTableIfConflictingWithAnchor() {
         StandardSetup(out var data, out var model, out var viewPort);
         viewPort.Edit("@0 ^table[data:]parent ");
         viewPort.Edit("@10 ^stuff @20 ^parent[data:]8 ");

         // try to extend table via tool
         viewPort.SelectionStart = new Point(0xF, 0);
         viewPort.Tools.SelectedIndex = viewPort.Tools.IndexOf(viewPort.Tools.TableTool);
         viewPort.Tools.TableTool.Append.Execute();

         // assert that the run moved
         Assert.NotEqual(0, model.GetNextRun(0).Start);
      }

      [Fact]
      public void PlmPointersMoveWhenPlmTableSourceMoves() {
         var source = new BaseViewModelTestClass();
         source.CreateTextTable(EggMoveRun.MoveNamesTable, 0x100, "A B C D E F G".Split(' '));

         // Arrange: write the data for a table @0 pointing to PLM run @4.
         source.Model.WriteMultiByteValue(4, 2, new ModelDelta(), 0x0404); // learn E at level 2
         source.Model.WriteMultiByteValue(6, 2, new ModelDelta(), 0xFFFF); // end of stream
         source.Model.WritePointer(new ModelDelta(), 0, 4);                // @0 <000004>
         source.ViewPort.Goto.Execute("00");
         source.ViewPort.Edit($"^lvlmoves[moves<{PLMRun.SharedFormatString}>]1 ");

         // Act: extend the PLM table. Note that this will automatically move it to avoid hitting the data @4.
         source.ViewPort.Goto.Execute("04");
         source.ViewPort.Edit("+");

         // Assert: the PLMRun at 04 should not have moved. There should be one thing pointing to it.
         var newTableStart = source.Model.GetAddressFromAnchor(new ModelDelta(), -1, "lvlmoves");
         var plmRun = (PLMRun)source.Model.GetNextRun(4);
         Assert.Single(plmRun.PointerSources);
         Assert.Equal(newTableStart, plmRun.PointerSources[0]);
      }

      private static void StandardSetup(out byte[] data, out PokemonModel model, out ViewPort viewPort) {
         data = new byte[0x200];
         model = new PokemonModel(data);
         viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };
      }

      private static void WriteStrings(byte[] buffer, int start, params string[] content) {
         foreach (var item in content) {
            var bytes = PCSString.Convert(item).ToArray();
            Array.Copy(bytes, 0, buffer, start, bytes.Length);
            start += bytes.Length;
         }
      }
   }
}
