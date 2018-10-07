using HavenSoft.Gen3Hex.Model;
using HavenSoft.Gen3Hex.ViewModel;
using HavenSoft.ViewModel;
using HavenSoft.ViewModel.DataFormats;
using System.Collections.Generic;
using System.Reflection;
using Xunit;

[assembly: AssemblyTitle("HexTests")]

namespace HavenSoft.HexTests {
   public class ViewPortTests {
      [Fact]
      public void ViewPortNotifiesOnSizeChange() {
         var viewPort = new ViewPort();
         var changeList = new List<string>();
         viewPort.PropertyChanged += (sender, e) => changeList.Add(e.PropertyName);

         viewPort.Width = 12;
         viewPort.Height = 50;

         Assert.Contains(nameof(viewPort.Width), changeList);
         Assert.Contains(nameof(viewPort.Height), changeList);
      }

      [Fact]
      public void ViewPortStartsEmpty() {
         var viewPort = new ViewPort();

         Assert.IsType<Undefined>(viewPort[0, 0].Format);
         Assert.Equal(0, viewPort.MinimumScroll);
         Assert.Equal(0, viewPort.MaximumScroll);
      }

      [Fact]
      public void ViewPortScrollStartsAtTopRow() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };

         Assert.Equal(0, viewPort.ScrollValue);
      }

      /// <summary>
      /// The scroll bar is in terms of lines.
      /// The viewport should not be able to scroll such that all the data is out of view.
      /// </summary>
      [Fact]
      public void ViewPortScrollingDoesNotAllowEmptyScreen() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };

         Assert.Equal(0, viewPort.MinimumScroll);
         Assert.Equal(4, viewPort.MaximumScroll);
      }

      [Fact]
      public void ViewPortWillNotScrollAboveAllData() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };

         viewPort.ScrollValue = -10;

         Assert.Equal(viewPort.MinimumScroll, viewPort.ScrollValue);
      }

      [Fact]
      public void ChangingWidthUpdatesScrollValueIfNeeded() {
         // ScrollValue=0 is always the line that contains the first byte of the file.

         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };

         viewPort.ScrollValue = 1; // scroll down one line
         viewPort.Width -= 1;      // decrease the width so that there is data 2 lines above

         // Example of what it should look like:
         // .. .. .. ..
         // .. .. .. 00
         // 00 00 00 00
         // 00 00 00 00 <- this is the top line in view
         // 00 00 00 00
         // 00 00 00 00
         // 00 00 00 00
         // 00 00 00 00 <- this is the bottom line in view
         // .. .. .. ..
         Assert.Equal(2, viewPort.ScrollValue);
         Assert.Equal(6, viewPort.MaximumScroll);
      }

      [Fact]
      public void ResizingCannotLeaveTotallyBlankLineAtTop() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };

         viewPort.ScrollValue++;   // scroll down one line
         viewPort.Width--;         // decrease the width so that there is data 2 lines above

         // Example of what it should look like right now:
         // .. .. .. ..
         // .. .. .. 00
         // 00 00 00 00
         // 00 00 00 00 <- this is the top line in view
         // 00 00 00 00
         // 00 00 00 00
         // 00 00 00 00
         // 00 00 00 00 <- this is the bottom line in view
         // .. .. .. ..

         viewPort.ScrollValue = 0; // scroll up to top
         viewPort.Width--;         // decrease the width to hide the last visible byte in the top row

         // expected: viewPort should auto-scroll here to make the top line full of data again
         Assert.Equal(0, viewPort.ScrollValue);
         Assert.NotEqual(HexElement.Undefined, viewPort[0, 0]);
      }

      [Fact]
      public void RequestingOutOfRangeDataReturnsUndefinedFormat() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };

         Assert.Equal(HexElement.Undefined, viewPort[0, -1]);
         Assert.Equal(HexElement.Undefined, viewPort[5, 0]);
         Assert.Equal(HexElement.Undefined, viewPort[0, 5]);
         Assert.Equal(HexElement.Undefined, viewPort[-1, 0]);
      }
   }
}
