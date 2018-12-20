using HavenSoft.Gen3Hex.Core.Models;
using HavenSoft.Gen3Hex.Core.ViewModels;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace HavenSoft.Gen3Hex.Tests {
   public class ToolTests {
      [Fact]
      public void ViewPortHasTools() {
         var viewPort = new ViewPort(new Core.Models.LoadedFile("file.txt", new byte[100]));
         Assert.True(viewPort.HasTools);
      }

      [Fact]
      public void StringToolCanMoveData() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var model = new PointerAndStringModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model) { Width = 0x10, Height = 0x10 };
         viewPort.Edit("^bob\"\" \"Some Text\" 00 <000100>");
         var toolProperties = new List<string>();
         viewPort.Tools.PropertyChanged += (sender, e) => toolProperties.Add(e.PropertyName);
         viewPort.FollowLink(2, 0);

         Assert.Contains("SelectedIndex", toolProperties);
         Assert.IsType<PCSTool>(viewPort.Tools[viewPort.Tools.SelectedIndex]);
      }

      // TODO tool changes should be immediately reflected in the ViewPort
   }
}
