using HavenSoft.Gen3Hex.Core;
using HavenSoft.Gen3Hex.Core.Models;
using HavenSoft.Gen3Hex.Core.ViewModels;
using HavenSoft.Gen3Hex.Core.ViewModels.DataFormats;
using System;
using System.Linq;
using Xunit;

namespace HavenSoft.Gen3Hex.Tests {
   public class ArrayRunTests {
      [Fact]
      public void CanParseStringArrayRun() {
         var model = new PointerAndStringModel(new byte[0x200]);
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
         var model = new PointerAndStringModel(new byte[0x200]);
         var success = ArrayRun.TryParse(model, "[\"\"10]13", 12, null, out var arrayRun); // no name given for the format member

         Assert.False(success);
      }

      [Fact]
      public void CanParseMultiStringArrayRun() {
         var model = new PointerAndStringModel(new byte[0x200]);
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
         var model = new PointerAndStringModel(buffer);
         ArrayRun.TryParse(model, "[name\"\"12]13", 12, null, out var arrayRun);
         model.ObserveRunWritten(new DeltaModel(), arrayRun);
         var viewPort = new ViewPort(new LoadedFile("file.txt", buffer), model) { Width = 12, Height = 20 };

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
         var model = new PointerAndStringModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("file.txt", buffer), model) { Width = 0x10, Height = 0x10 };

         viewPort.Edit("^bob[name\"\"14]12 ");

         Assert.IsType<ArrayRun>(model.GetNextRun(0));
      }

      [Fact]
      public void ChangingAnchorTextWhileAnchorStartIsOutOfViewWorks() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var model = new PointerAndStringModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("file.txt", buffer), model) { Width = 0x10, Height = 0x10 };
         viewPort.Edit("^bob[name\"\"16]16 ");

         viewPort.ScrollValue = 2;
         viewPort.AnchorText = "^bob[name\"\"16]18";

         Assert.Equal(16 * 18, model.GetNextRun(0).Length);
      }

      [Fact]
      public void CanAutoFindArrayLength() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         Array.Copy(PCSString.Convert("bob").ToArray(), 0, buffer, 0x00, 4);
         Array.Copy(PCSString.Convert("tom").ToArray(), 0, buffer, 0x04, 4);
         Array.Copy(PCSString.Convert("sam").ToArray(), 0, buffer, 0x08, 4);
         Array.Copy(PCSString.Convert("car").ToArray(), 0, buffer, 0x0C, 4);
         Array.Copy(PCSString.Convert("pal").ToArray(), 0, buffer, 0x10, 4);
         Array.Copy(PCSString.Convert("egg").ToArray(), 0, buffer, 0x14, 4);

         var model = new PointerAndStringModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("file.txt", buffer), model) { Width = 0x10, Height = 0x10 };

         viewPort.Edit("^words[word\"\"4] "); // notice, no length is given

         var run = (ArrayRun)model.GetNextRun(0);
         Assert.Equal(6, run.ElementCount);
      }

      [Fact]
      public void WidthRestrictedAfterGotoArrayAnchor() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         Array.Copy(PCSString.Convert("bobb").ToArray(), 0, buffer, 100, 5);
         Array.Copy(PCSString.Convert("tomm").ToArray(), 0, buffer, 105, 5);
         Array.Copy(PCSString.Convert("samm").ToArray(), 0, buffer, 110, 5);
         Array.Copy(PCSString.Convert("carr").ToArray(), 0, buffer, 115, 5);
         Array.Copy(PCSString.Convert("pall").ToArray(), 0, buffer, 120, 5);
         Array.Copy(PCSString.Convert("eggg").ToArray(), 0, buffer, 125, 5);

         var model = new PointerAndStringModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("file.txt", buffer), model) { Width = 0x10, Height = 0x10 };

         viewPort.Edit("^words[word\"\"5] "); // notice, no length is given

         viewPort.Goto.Execute("words");
         Assert.Equal(0, viewPort.Width % 5);
      }

      [Fact]
      public void CanCopyArray() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         Array.Copy(PCSString.Convert("bobb").ToArray(), 0, buffer, 0, 5);
         Array.Copy(PCSString.Convert("tomm").ToArray(), 0, buffer, 5, 5);
         Array.Copy(PCSString.Convert("samm").ToArray(), 0, buffer, 10, 5);
         Array.Copy(PCSString.Convert("carr").ToArray(), 0, buffer, 15, 5);
         Array.Copy(PCSString.Convert("pall").ToArray(), 0, buffer, 20, 5);
         Array.Copy(PCSString.Convert("eggg").ToArray(), 0, buffer, 25, 5);
         var model = new PointerAndStringModel(buffer);
         ArrayRun.TryParse(model, "[word\"\"5]", 0, null, out var arrayRun);
         model.ObserveAnchorWritten(new DeltaModel(), "words", arrayRun);

         var text = model.Copy(0, 0x20);

         Assert.StartsWith("^words[word\"\"5]", text);
      }

      [Fact]
      public void CanEditArray() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         Array.Copy(PCSString.Convert("bobb").ToArray(), 0, buffer, 0, 5);
         Array.Copy(PCSString.Convert("tomm").ToArray(), 0, buffer, 5, 5);
         Array.Copy(PCSString.Convert("samm").ToArray(), 0, buffer, 10, 5);
         Array.Copy(PCSString.Convert("carr").ToArray(), 0, buffer, 15, 5);
         Array.Copy(PCSString.Convert("pall").ToArray(), 0, buffer, 20, 5);
         Array.Copy(PCSString.Convert("eggg").ToArray(), 0, buffer, 25, 5);

         var model = new PointerAndStringModel(buffer);
         ArrayRun.TryParse(model, "[word\"\"5]", 0, null, out var arrayRun);
         model.ObserveAnchorWritten(new DeltaModel(), "words", arrayRun);
         var viewPort = new ViewPort(new LoadedFile("file.txt", buffer), model) { Width = 0x10, Height = 0x10 };
         viewPort.SelectionStart = new Point(6, 0);

         viewPort.Edit("a"); // change "tomm" to "tamm"

         Assert.Equal("a", ((PCS)viewPort[6, 0].Format).ThisCharacter);
      }

      [Fact]
      public void CanPasteArray() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var model = new PointerAndStringModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("file.txt", buffer), model) { Width = 0x10, Height = 0x10 };

         viewPort.Edit("^strings[name\"\"16] ");
         viewPort.Edit("+\"bob\" +\"sam\" +\"steve\" +\"kevin\"");

         Assert.Equal(0x40, model.GetNextRun(0).Length);
         Assert.Equal("\"", ((PCS)viewPort[3, 0].Format).ThisCharacter);
      }

      [Fact]
      public void PastingArrayLeavesArrayFormat() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var model = new PointerAndStringModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("file.txt", buffer), model) { Width = 0x10, Height = 0x10 };

         viewPort.Edit("^pokenames[name\"\"11] +\"??????????\"+\"BULBASAUR\"");
         viewPort.SelectionStart = new Point(0, 0);
         viewPort.FollowLink(0, 0);

         Assert.Equal("^pokenames[name\"\"11]", viewPort.AnchorText);
         Assert.Equal("BULBASAUR", viewPort.Tools.StringTool.Content.Split(new[] { Environment.NewLine }, StringSplitOptions.None).Last());
      }

      [Fact]
      public void ArrayIsRecognizedByStringTool() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         Array.Copy(PCSString.Convert("bobb").ToArray(), 0, buffer, 100, 5);
         Array.Copy(PCSString.Convert("tomm").ToArray(), 0, buffer, 105, 5);
         Array.Copy(PCSString.Convert("samm").ToArray(), 0, buffer, 110, 5);
         Array.Copy(PCSString.Convert("carr").ToArray(), 0, buffer, 115, 5);
         Array.Copy(PCSString.Convert("pall").ToArray(), 0, buffer, 120, 5);
         Array.Copy(PCSString.Convert("eggg").ToArray(), 0, buffer, 125, 5);

         var model = new PointerAndStringModel(buffer);
         ArrayRun.TryParse(model, "[word\"\"5]", 0, null, out var arrayRun);
         model.ObserveAnchorWritten(new DeltaModel(), "words", arrayRun);
         var viewPort = new ViewPort(new LoadedFile("file.txt", buffer), model) { Width = 0x10, Height = 0x10 };

         viewPort.FollowLink(0, 7); // 7*16 = 112, right in the middle of our data

         Assert.Equal(0, viewPort.Tools.SelectedIndex); // string tool is selected
         Assert.Equal(100, viewPort.Tools.StringTool.Address);
         var lineCount = viewPort.Tools.StringTool.Content.Split(new[] { Environment.NewLine }, StringSplitOptions.None).Length;
         Assert.Equal(6, lineCount);
      }
   }
}
