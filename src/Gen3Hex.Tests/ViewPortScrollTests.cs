using HavenSoft.Gen3Hex.Core.Models;
using HavenSoft.Gen3Hex.Core.ViewModels;
using System.Collections.Generic;
using Xunit;

[assembly: System.Reflection.AssemblyTitle("HexTests")]

namespace HavenSoft.Gen3Hex.Tests {
   public class ViewPortTests {
      [Fact]
      public void ViewPortNotifiesOnSizeChange() {
         var viewPort = new ViewPort();
         var changedProperties = new List<string>();
         viewPort.PropertyChanged += (sender, e) => changedProperties.Add(e.PropertyName);

         viewPort.Width = 12;
         viewPort.Height = 50;

         Assert.Contains(nameof(viewPort.Width), changedProperties);
         Assert.Contains(nameof(viewPort.Height), changedProperties);
      }

      [Fact]
      public void ViewPortStartsEmpty() {
         var viewPort = new ViewPort();

         Assert.Equal(HexElement.Undefined, viewPort[0, 0]);
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
      public void ViewPortCannotScrollLowerThanMinimumScroll() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };

         viewPort.ScrollValue = int.MinValue;

         Assert.Equal(viewPort.MinimumScroll, viewPort.ScrollValue);
      }

      [Fact]
      public void ChangingWidthUpdatesScrollValueIfNeededOnScrollRegion() {
         var scroll = new ScrollRegion { DataLength = 25, Width = 5, Height = 5 };

         scroll.ScrollValue++;
         scroll.Width--;

         Assert.Equal(2, scroll.ScrollValue);
         Assert.Equal(6, scroll.MaximumScroll);
      }

      [Fact]
      public void ChangingWidthUpdatesScrollValueIfNeeded() {
         // ScrollValue=0 is always the line that contains the first byte of the file.
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };

         viewPort.ScrollValue++; // scroll down one line
         viewPort.Width--;      // decrease the width so that there is data 2 lines above

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
         var loadedFile = new LoadedFile("test", new byte[36]);
         var viewPort = new ViewPort(loadedFile) { Width = 6, Height = 6 };

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

         viewPort.ScrollValue = viewPort.MinimumScroll; // scroll up to top
         viewPort.Width--;                              // decrease the width to hide the last visible byte in the top row

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

      [Fact]
      public void MaximumScrollChangesBasedOnDataOffset() {
         var loadedFile = new LoadedFile("test", new byte[26]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };

         // initial condition: given 4 data per row, there should be 7 rows (0-6) because 7*4=28
         viewPort.Width--;
         Assert.Equal(6, viewPort.MaximumScroll);
         viewPort.Width++;

         viewPort.ScrollValue++;   // scroll down one line
         viewPort.Width--;         // decrease the width so that there is data 2 lines above

         // Example of what it should look like right now:
         // .. .. .. 00
         // 00 00 00 00
         // 00 00 00 00 <- this is the top line in view
         // 00 00 00 00
         // 00 00 00 00
         // 00 00 00 00
         // 00 00 00 00 <- this is the bottom line in view
         // 00 .. .. ..

         // notice from the diagram above that there should now be _8_ rows (0-7).
         Assert.Equal(7, viewPort.MaximumScroll);
      }

      [Fact]
      public void ScrollingRightUpdatesScrollValueIfNeeded() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };
         var changeCount = 0;

         viewPort.CollectionChanged += (sender, e) => changeCount += 1;

         viewPort.Scroll.Execute(Direction.Right);

         Assert.Equal(1, viewPort.ScrollValue);
         Assert.NotEqual(0, changeCount);
      }

      [Fact]
      public void ScrollingRightAndLeftCancel() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         loadedFile.Contents[3] = 0x10;
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };

         viewPort.Scroll.Execute(Direction.Left);
         viewPort.Scroll.Execute(Direction.Right);

         Assert.Equal(0x10, viewPort[3, 0].Value);
      }

      [Fact]
      public void EmptyFileMaximumScrollMatchesMinimumScroll() {
         var viewPort = new ViewPort();

         Assert.Equal(viewPort.MinimumScroll, viewPort.MaximumScroll);
      }

      [Fact]
      public void EmptyFileCannotScroll() {
         var viewPort = new ViewPort();

         Assert.False(viewPort.Scroll.CanExecute(Direction.Left));
         Assert.False(viewPort.Scroll.CanExecute(Direction.Right));
         Assert.False(viewPort.Scroll.CanExecute(Direction.Up));
         Assert.False(viewPort.Scroll.CanExecute(Direction.Down));
      }

      [Fact]
      public void NotifyCollectionChangeAfterScrolling() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };
         var propertyNotifications = new List<string>();
         viewPort.PropertyChanged += (sender, e) => propertyNotifications.Add(e.PropertyName);
         int collectionNotifications = 0;
         viewPort.CollectionChanged += (sender, e) => collectionNotifications++;

         viewPort.Scroll.Execute(Direction.Down);

         Assert.Equal(1, collectionNotifications);
      }
   }
}
