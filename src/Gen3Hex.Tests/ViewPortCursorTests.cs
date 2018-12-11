using HavenSoft.Gen3Hex.Core.Models;
using HavenSoft.Gen3Hex.Core.ViewModels;
using Xunit;

namespace HavenSoft.Gen3Hex.Tests {
   public class ViewPortCursorTests {
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

         Assert.Equal(new Point(0, 0), viewPort.SelectionStart); // coerced to first byte
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
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 4 };
         viewPort.SelectionStart = new Point(0, 3);

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

         Assert.Equal(new Point(4, 0), viewPort.SelectionStart);
         Assert.Equal(0, viewPort.ScrollValue);

         viewPort.SelectionStart = new Point(4, 4);
         viewPort.MoveSelectionStart.Execute(Direction.Down);
         viewPort.MoveSelectionStart.Execute(Direction.Down);

         Assert.Equal(new Point(4, 4), viewPort.SelectionStart);
         Assert.Equal(1, viewPort.ScrollValue); // I can scroll lower using Scroll.Execute, but I cannot select lower.
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

      [Fact]
      public void ChangingWidthKeepsSameDataSelected() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };
         viewPort.SelectionEnd = new Point(2, 2); // 13 cells selected
         viewPort.Width += 1;

         Assert.Equal(new Point(0, 2), viewPort.SelectionEnd); // 13 cells selected
      }

      [Fact]
      public void CannotSelectBeforeFirstByte() {
         var loadedFile = new LoadedFile("test", new byte[30]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };

         // view the cell left of the first byte
         viewPort.Scroll.Execute(Direction.Left);

         // try to select the cell left of the first byte
         viewPort.MoveSelectionStart.Execute(Direction.Left);

         // assert that the selection is still on the first byte, not the first cell
         Assert.Equal(new Point(1, 0), viewPort.SelectionStart);
      }

      [Fact]
      public void CannotSelectFarAfterLastByte() {
         var loadedFile = new LoadedFile("test", new byte[20]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };

         viewPort.SelectionStart = new Point(4, 4);

         Assert.Equal(new Point(0, 4), viewPort.SelectionStart);
      }

      [Fact]
      public void MovingSelectionInAnUnsizedPanelAutoSizesThePanel() {
         var viewPort = new ViewPort();

         viewPort.SelectionStart = new Point(4, 4);

         Assert.NotEqual(0, viewPort.Width);
         Assert.NotEqual(0, viewPort.Height);
      }

      [Fact]
      public void CannotMoveSelectEndFarPassedEndOfFile() {
         var selection = new Selection(new ScrollRegion { DataLength = 8 }, new BasicModel(new byte[8]));

         selection.SelectionEnd = new Point(3, 3);

         Assert.Equal(new Point(0, 2), selection.SelectionEnd);
      }
   }
}
