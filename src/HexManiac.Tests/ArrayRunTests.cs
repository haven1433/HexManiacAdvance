using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
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
         var success = ArrayRun.TryParse(model, "[\"\"10]13", 12, null, out var arrayRun); // no name given for the format member

         Assert.False(success);
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
         model.ObserveRunWritten(new ModelDelta(), arrayRun);
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
         ArrayRun.TryParse(model, "[word\"\"5]", 0, null, out var arrayRun);
         model.ObserveAnchorWritten(new ModelDelta(), "words", arrayRun);

         var text = model.Copy(0, 0x20);

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
         Assert.Equal("BULBASAUR", viewPort.Tools.StringTool.Content.Split(new[] { Environment.NewLine }, StringSplitOptions.None).Last());
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
         var lineCount = viewPort.Tools.StringTool.Content.Split(new[] { Environment.NewLine }, StringSplitOptions.None).Length;
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
         var lines = viewPort.Tools.StringTool.Content.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
         Assert.All(elements, element => Assert.Contains(element, lines));

         // assert -> nothing left behind
         Assert.All(Enumerable.Range(0x20, 0x50), i => Assert.Equal(0xFF, model[i]));
      }

      [Fact]
      public void CannotCutPasteArrayToMakeItHitAnotherRun() {
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

      [Fact(Skip = "Feature not implement yet. Feature is now prioritized beneath array support for pointers.")]
      public void ArrayExtendsIfBasedOnAnotherNameWhichIsExtended() {
         var buffer = new byte[0x200];
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };
         var errors = new List<string>();
         viewPort.OnError += (sender, e) => errors.Add(e);

         viewPort.Edit("^sample[name\"\"8]8 "); // should cover the first 4 lines

         viewPort.SelectionStart = new Point(0, 8);
         viewPort.Edit("^derived[index.]sample "); // should be the same length as sample: 8

         // test 1: enbiggen derived should enbiggen sample
         viewPort.SelectionStart = new Point(8, 8);
         viewPort.Edit("+");
         Assert.Equal(8 * 8 + 8, model.GetNextRun(0).Length);

         // test 2: enbiggen sample should enbiggen derived
         viewPort.SelectionStart = new Point(8, 4);
         viewPort.Edit("+");
         Assert.Equal(10, model.GetNextRun(0x80).Length);
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

      private static void WriteStrings(byte[] buffer, int start, params string[] content) {
         foreach (var item in content) {
            var bytes = PCSString.Convert(item).ToArray();
            Array.Copy(bytes, 0, buffer, start, bytes.Length);
            start += bytes.Length;
         }
      }
   }
}
