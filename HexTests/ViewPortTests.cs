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

         Assert.Equal(0, viewPort.MinimumScroll);
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
         viewPort.ScrollValue = 0; // scroll up to top
         viewPort.Width--;         // decrease the width to make the top line totally blank

         // expected: viewPort should auto-scroll here to make the top line full of data again
         Assert.Equal(0, viewPort.ScrollValue);
      }

      [Fact]
      public void RequestingOutOfRangeDataReturnsUnavailable() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };

         Assert.Equal(Undefined.Instance, viewPort[0, -1].Format);
         Assert.Equal(Undefined.Instance, viewPort[5, 0].Format);
         Assert.Equal(Undefined.Instance, viewPort[0, 5].Format);
         Assert.Equal(Undefined.Instance, viewPort[-1, 0].Format);
      }

      [Fact]
      public void ResizingCannotLeaveNoDataOnScreen() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };

         viewPort.ScrollValue = -10;
         viewPort.Height--;

         Assert.NotEqual(Undefined.Instance, viewPort[viewPort.Width - 1, viewPort.Height - 1].Format);
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

         Assert.Equal(0x10, viewPort[3, 0]);
      }

      [Fact]
      public void CursorCanMoveAllFourDirections() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };
         viewPort.SelectionStart = new Point(3, 0);

         viewPort.MoveSelectionStart(Direction.Right);
         viewPort.MoveSelectionStart(Direction.Down);
         viewPort.MoveSelectionStart(Direction.Left);
         viewPort.MoveSelectionStart(Direction.Up);

         Assert.Equal(new Point(3, 0), viewPort.SelectionStart);
      }

      [Fact]
      public void CursorCannotMoveAboveTopRow() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };
         viewPort.SelectionStart = new Point(3, 0);

         viewPort.MoveSelectionStart(Direction.Up);

         Assert.Equal(new Point(3, 0), viewPort.SelectionStart);
      }

      [Fact]
      public void MovingCursorRightFromRightEdgeMovesToNextLine() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };
         viewPort.SelectionStart = new Point(4, 0);

         viewPort.MoveSelectionStart(Direction.Right);

         Assert.Equal(new Point(0, 1), viewPort.SelectionStart);
      }

      [Fact]
      public void MovingCursorDownFromBottomRowScrolls() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 3 };
         viewPort.SelectionStart = new Point(0, 2);

         viewPort.MoveSelectionStart(Direction.Down);

         Assert.Equal(1, viewPort.ScrollValue);
      }

      [Fact]
      public void CursorCanMoveOutsideDataRangeButNotOutsideScrollRange() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };
         viewPort.ScrollRight.Execute(null);
         viewPort.SelectionStart = new Point(0, 0);

         viewPort.MoveSelectionStart(Direction.Up);
         viewPort.MoveSelectionStart(Direction.Up);

         Assert.Equal(new Point(0, 0), viewPort.SelectionStart);
         Assert.Equal(0, viewPort.ScrollValue);

         viewPort.SelectionStart = new Point(4, 4);
         for (int i = 0; i < 6; i++) viewPort.MoveDown.Execute(null); // 6 moves, 5 moves work, last one should do nothing

         Assert.Equal(new Point(4, 4), viewPort.SelectionStart);
         Assert.Equal(5, viewPort.ScrollValue);
      }

      [Fact]
      public void MoveSelectionEndWorks() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };
         viewPort.SelectionStart = new Point(0, 0);

         viewPort.MoveSelectionStart(Direction.Right);
         viewPort.MoveSelectionEnd(Direction.Down);

         Assert.Equal(new Point(1, 1), viewPort.SelectionEnd);
      }

      [Fact]
      public void SetSelectionStartResetsSelectionEnd() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };
         viewPort.SelectionStart = new Point(0, 0);

         viewPort.SelectionEnd = new Point(3, 3);
         viewPort.SelectionStart = new Point(0, 0);

         Assert.Equal(new Point(0, 0), viewPort.SelectionEnd);
      }
   }
}
