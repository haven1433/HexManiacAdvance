using HavenSoft.Gen3Hex.Model;
using HavenSoft.Gen3Hex.ViewModel;
using System.Reflection;
using Xunit;

[assembly: AssemblyTitle("HexTests")]

namespace HexTests {
   public class Tests {
      [Fact]
      public void ViewPortNotifiesOnSizeChange() {
         var viewPort = new ViewPort();
         var counter = 0;
         viewPort.PropertyChanged += (sender, e) => counter++;

         viewPort.Width = 12;
         viewPort.Height = 50;

         Assert.Equal(2, counter);
      }

      [Fact]
      public void ViewPortStartsEmpty() {
         var viewPort = new ViewPort();

         Assert.Equal(CommonFormats.Undefined, viewPort[0, 0].Format);
      }

      /// <summary>
      /// The scroll bar is in terms of lines.
      /// The viewport should not be able to scroll such that all the data is out of view.
      /// </summary>
      [Fact]
      public void ViewPortScrollingDoesNotAllowEmptyScreen() {
         var viewPort = new ViewPort { Width = 5, Height = 5 };
         var loadedFile = new LoadedFile("test", new byte[25]);

         Assert.Equal(-4, viewPort.MinimumScroll);
         Assert.Equal(4, viewPort.MaximumScroll);
      }
   }
}
