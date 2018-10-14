using HavenSoft.Gen3Hex.Model;
using HavenSoft.Gen3Hex.ViewModel;
using System.Collections.Generic;
using System.Reflection;
using Xunit;

[assembly: AssemblyTitle("HexTests")]

namespace HavenSoft.HexTests {
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

         viewPort.Scroll.Execute(Direction.Right);

         Assert.Equal(1, viewPort.ScrollValue);
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
      public void CursorCanMoveAllFourDirections() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };
         viewPort.SelectionStart = new Point(3, 0);

         viewPort.MoveSelectionStart.Execute(Direction.Right);
         viewPort.MoveSelectionStart.Execute(Direction.Down);
         viewPort.MoveSelectionStart.Execute(Direction.Left);
         viewPort.MoveSelectionStart.Execute(Direction.Up);

         Assert.Equal(new Point(3, 0), viewPort.SelectionStart);
      }

      [Fact]
      public void CursorCannotMoveAboveTopRow() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };
         viewPort.SelectionStart = new Point(3, 0);

         viewPort.MoveSelectionStart.Execute(Direction.Up);

         Assert.Equal(new Point(3, 0), viewPort.SelectionStart);
      }

      [Fact]
      public void MovingCursorRightFromRightEdgeMovesToNextLine() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };
         viewPort.SelectionStart = new Point(4, 0);

         viewPort.MoveSelectionStart.Execute(Direction.Right);

         Assert.Equal(new Point(0, 1), viewPort.SelectionStart);
      }

      [Fact]
      public void MovingCursorDownFromBottomRowScrolls() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 3 };
         viewPort.SelectionStart = new Point(0, 2);

         viewPort.MoveSelectionStart.Execute(Direction.Down);

         Assert.Equal(1, viewPort.ScrollValue);
      }

      [Fact]
      public void CursorCanMoveOutsideDataRangeButNotOutsideScrollRange() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };
         viewPort.Scroll.Execute(Direction.Right);
         viewPort.SelectionStart = new Point(0, 0);

         viewPort.MoveSelectionStart.Execute(Direction.Up);
         viewPort.MoveSelectionStart.Execute(Direction.Up);

         Assert.Equal(new Point(0, 0), viewPort.SelectionStart);
         Assert.Equal(0, viewPort.ScrollValue);

         viewPort.SelectionStart = new Point(4, 4);
         for (int i = 0; i < 6; i++) viewPort.MoveSelectionStart.Execute(Direction.Down); // 6 moves, 5 moves work, last one should do nothing

         Assert.Equal(new Point(4, 4), viewPort.SelectionStart);
         Assert.Equal(5, viewPort.ScrollValue);
      }

      [Fact]
      public void MoveSelectionEndWorks() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };
         viewPort.SelectionStart = new Point(0, 0);

         viewPort.MoveSelectionStart.Execute(Direction.Right);
         viewPort.MoveSelectionEnd.Execute(Direction.Down);

         Assert.Equal(new Point(1, 1), viewPort.SelectionEnd);
      }

      [Fact]
      public void SetSelectionStartResetsSelectionEnd() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };
         viewPort.SelectionStart = new Point(0, 0);

         viewPort.SelectionEnd = new Point(3, 3);
         viewPort.SelectionStart = new Point(1, 0);

         Assert.Equal(new Point(1, 0), viewPort.SelectionEnd);
      }

      [Fact]
      public void ScrollingUpdatesSelection() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };

         viewPort.SelectionStart = new Point(0, 2);
         viewPort.SelectionEnd = new Point(4, 2);
         viewPort.Scroll.Execute(Direction.Down);

         Assert.Equal(new Point(0, 1), viewPort.SelectionStart);
         Assert.Equal(new Point(4, 1), viewPort.SelectionEnd);
      }

      [Fact]
      public void ForwardSelectionWorks() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };

         viewPort.SelectionStart = new Point(2, 1);
         viewPort.SelectionEnd = new Point(3, 3);

         Assert.True(viewPort.IsSelected(new Point(4, 2)));
      }

      [Fact]
      public void BackSelectionWorks() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };

         viewPort.SelectionStart = new Point(3, 3);
         viewPort.SelectionEnd = new Point(2, 1);

         Assert.True(viewPort.IsSelected(new Point(4, 2)));
      }

      /// <remarks>
      /// Scrolling Down makes you see lower data.
      /// Scrolling Up makes you see higher data.
      /// Scrolling Left makes you see one more byte, left of what's currently in view.
      /// Scrolling Right makes you see one more byte, right of what's currently in view.
      /// </remarks>
      [Fact]
      public void ScrollingBeforeStartOfDataMovesSelectionOnlyWhenDataMoves() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };

         viewPort.Scroll.Execute(Direction.Right); // move the first byte out of view
         viewPort.Scroll.Execute(Direction.Up);    // scroll up, so we can see the first byte again

         // Example of what it should look like right now:
         // .. .. .. 00 <- this is the top line in the view
         // 00 00 00 00
         // 00 00 00 00
         // 00 00 00 00
         // 00 00 00 00

         viewPort.Scroll.Execute(Direction.Left); // try to scroll further. Should fail, because then the whole top row would be empty.
         Assert.Equal(new Point(4, 0), viewPort.SelectionStart); // first byte of data should still be selected.
      }
   }
}
