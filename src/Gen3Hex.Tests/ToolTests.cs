using HavenSoft.Gen3Hex.Core;
using HavenSoft.Gen3Hex.Core.Models;
using HavenSoft.Gen3Hex.Core.ViewModels;
using HavenSoft.Gen3Hex.Core.ViewModels.DataFormats;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Xunit;

namespace HavenSoft.Gen3Hex.Tests {
   public class ToolTests {
      [Fact]
      public void ViewPortHasTools() {
         var viewPort = new ViewPort(new LoadedFile("file.txt", new byte[100]));
         Assert.True(viewPort.HasTools);
      }

      [Fact]
      public void StringToolCanOpenOnChosenData() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var model = new PointerAndStringModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model) { Width = 0x10, Height = 0x10 };
         viewPort.Edit("^bob\"\" \"Some Text\" 00 <000100>");
         var toolProperties = new List<string>();
         viewPort.Tools.PropertyChanged += (sender, e) => toolProperties.Add(e.PropertyName);
         viewPort.FollowLink(0, 0);

         Assert.Contains("SelectedIndex", toolProperties);
         Assert.IsType<PCSTool>(viewPort.Tools[viewPort.Tools.SelectedIndex]);
      }

      [Fact]
      public void StringToolEditsAreReflectedInViewPort() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var model = new PointerAndStringModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model) { Width = 0x10, Height = 0x10 };
         viewPort.Edit("^bob\"\" \"Some Text\" 00 <000100>");
         viewPort.Tools.StringTool.Address = 0;

         viewPort.Tools.StringTool.Content = "Some Test"; // Text -> Test
         var pcs = (PCS)viewPort[7, 0].Format;
         Assert.Equal("s", pcs.ThisCharacter);
      }

      [Fact]
      public void StringToolCanMoveData() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var model = new PointerAndStringModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model) { Width = 0x10, Height = 0x10 };
         viewPort.Edit("^bob\"\" \"Some Text\" 00 <000100>");
         var toolProperties = new List<string>();
         viewPort.Tools.StringTool.PropertyChanged += (sender, e) => toolProperties.Add(e.PropertyName);
         viewPort.Tools.StringTool.Address = 0;

         toolProperties.Clear();
         viewPort.Tools.StringTool.Content = "Some More Text";
         Assert.Contains("Address", toolProperties);
      }

      [Fact]
      public void ViewPortMovesWhenStringToolMovesData() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var model = new PointerAndStringModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model) { Width = 0x10, Height = 0x10 };
         viewPort.Edit("^bob\"\" \"Some Text\" 00 <000100>");
         viewPort.Tools.StringTool.Address = 0;

         viewPort.Tools.StringTool.Content = "Some More Text";
         Assert.NotEqual(0, int.Parse(viewPort.Headers[0], NumberStyles.HexNumber));
      }

      [Fact]
      public void StringToolMultiCharacterDeleteCleansUpUnusedBytes() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var model = new PointerAndStringModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model) { Width = 0x10, Height = 0x10 };
         viewPort.Edit("^bob\"\" \"Some Text\" 00 <000100>");
         viewPort.Tools.StringTool.Address = 0;

         viewPort.Tools.StringTool.Content = "Some "; // removed 'Text' from the end

         Assert.Equal(0xFF, model[7]);
      }

      [Fact]
      public void HideCommandClosesAnyOpenTools() {
         var model = new PointerAndStringModel(new byte[0x200]);
         var history = new ChangeHistory<DeltaModel>(null);
         var tools = new ToolTray(model, new Selection(new ScrollRegion(), model), history);

         tools.SelectedIndex = 1;
         tools.HideCommand.Execute();

         Assert.Equal(-1, tools.SelectedIndex);
      }

      [Fact]
      public void StringToolContentUpdatesWhenViewPortChange() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var model = new PointerAndStringModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model) { Width = 0x10, Height = 0x10 };
         viewPort.Edit("^bob\"\" \"Some Text\"");

         viewPort.SelectionStart = new Point(3, 0);   // select the 'e' in 'Some'
         viewPort.FollowLink(3, 0);                   // open the string tool
         viewPort.Edit("i");                          // change the 'e' to 'i'

         Assert.Equal("Somi Text", viewPort.Tools.StringTool.Content);
      }

      [Fact]
      public void ToolSelectionChangeUpdatesViewPortSelection() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var model = new PointerAndStringModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model) { Width = 0x10, Height = 0x10 };
         viewPort.Edit("^bob\"\" \"Some Text\"");
         viewPort.SelectionStart = new Point(3, 0);
         viewPort.FollowLink(3, 0);

         viewPort.Tools.StringTool.ContentIndex = 4;

         Assert.Equal(new Point(4, 0), viewPort.SelectionStart);
      }
   }
}
