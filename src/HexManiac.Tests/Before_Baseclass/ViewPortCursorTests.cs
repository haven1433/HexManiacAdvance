using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
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
         var viewPort = new ViewPort(loadedFile) { PreferredWidth = -1, Width = 5, Height = 5 };
         viewPort.SelectionStart = new Point(4, 0);

         viewPort.MoveSelectionStart.Execute(Direction.Right);

         Assert.Equal(new Point(0, 1), viewPort.SelectionStart);
      }

      [Fact]
      public void MovingCursorDownFromBottomRowScrolls() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { PreferredWidth = -1, Width = 5, Height = 4 };
         viewPort.SelectionStart = new Point(0, 3);

         viewPort.MoveSelectionStart.Execute(Direction.Down);

         Assert.Equal(1, viewPort.ScrollValue);
      }

      [Fact]
      public void CursorCanMoveOutsideDataRangeButNotOutsideScrollRange() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { PreferredWidth = -1, Width = 5, Height = 5 };
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
         var viewPort = new ViewPort(loadedFile) { PreferredWidth = -1, Width = 5, Height = 5 };

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
         var viewPort = new ViewPort(loadedFile) { PreferredWidth = -1, Width = 5, Height = 5 };
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
         var viewPort = new ViewPort(loadedFile) { PreferredWidth = -1, Width = 5, Height = 5 };

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

      [Fact]
      public void CanExpandSelection() {
         CreateStandardTestSetup(out var viewPort, out var model, out var data);

         viewPort.Edit("FF @00 ^word\"\" \"bob\"");
         viewPort.SelectionStart = new Point(1, 0);
         viewPort.ExpandSelection(1, 0);

         Assert.True(viewPort.IsSelected(new Point(0, 0)));
         Assert.True(viewPort.IsSelected(new Point(1, 0)));
         Assert.True(viewPort.IsSelected(new Point(2, 0)));
         Assert.True(viewPort.IsSelected(new Point(3, 0)));
         Assert.False(viewPort.IsSelected(new Point(4, 0)));
      }

      [Theory]
      [InlineData(0)]
      [InlineData(1)]
      [InlineData(2)]
      [InlineData(3)]
      public void SelectingAnyOfAPointerSelectsAllOfAPointer(int index) {
         CreateStandardTestSetup(out var viewPort, out var model, out var data);

         viewPort.Edit("<000100>");
         viewPort.SelectionStart = new Point(index, 0);

         Assert.True(viewPort.IsSelected(new Point(0, 0)));
         Assert.True(viewPort.IsSelected(new Point(1, 0)));
         Assert.True(viewPort.IsSelected(new Point(2, 0)));
         Assert.True(viewPort.IsSelected(new Point(3, 0)));
      }

      [Fact]
      public void SelectLeftSelectsWholePointer() {
         CreateStandardTestSetup(out var viewPort, out var model, out var data);

         viewPort.SelectionStart = new Point(4, 0);
         viewPort.Edit("<000100>");
         viewPort.SelectionStart = new Point(8, 0);
         viewPort.MoveSelectionStart.Execute(Direction.Left);

         Assert.True(viewPort.IsSelected(new Point(4, 0)));
         Assert.True(viewPort.IsSelected(new Point(5, 0)));
         Assert.True(viewPort.IsSelected(new Point(6, 0)));
         Assert.True(viewPort.IsSelected(new Point(7, 0)));
      }

      [Fact]
      public void SelectRightSelectsWholePointer() {
         CreateStandardTestSetup(out var viewPort, out var model, out var data);

         viewPort.SelectionStart = new Point(4, 0);
         viewPort.Edit("<000100>");
         viewPort.SelectionStart = new Point(3, 0);
         viewPort.MoveSelectionStart.Execute(Direction.Right);

         Assert.True(viewPort.IsSelected(new Point(4, 0)));
         Assert.True(viewPort.IsSelected(new Point(5, 0)));
         Assert.True(viewPort.IsSelected(new Point(6, 0)));
         Assert.True(viewPort.IsSelected(new Point(7, 0)));
      }

      [Fact]
      public void SelectUpSelectsWholePointer() {
         CreateStandardTestSetup(out var viewPort, out var model, out var data);

         viewPort.SelectionStart = new Point(4, 0);
         viewPort.Edit("<000100>");
         viewPort.SelectionStart = new Point(5, 1);
         viewPort.MoveSelectionStart.Execute(Direction.Up);

         Assert.True(viewPort.IsSelected(new Point(4, 0)));
         Assert.True(viewPort.IsSelected(new Point(5, 0)));
         Assert.True(viewPort.IsSelected(new Point(6, 0)));
         Assert.True(viewPort.IsSelected(new Point(7, 0)));
      }

      [Fact]
      public void SelectDownSelectsWholePointer() {
         CreateStandardTestSetup(out var viewPort, out var model, out var data);

         viewPort.SelectionStart = new Point(4, 1);
         viewPort.Edit("<000100>");
         viewPort.SelectionStart = new Point(5, 0);
         viewPort.MoveSelectionStart.Execute(Direction.Down);

         Assert.True(viewPort.IsSelected(new Point(4, 1)));
         Assert.True(viewPort.IsSelected(new Point(5, 1)));
         Assert.True(viewPort.IsSelected(new Point(6, 1)));
         Assert.True(viewPort.IsSelected(new Point(7, 1)));
      }

      [Fact]
      public void HighlightLeftSelectsWholePointer() {
         CreateStandardTestSetup(out var viewPort, out var model, out var data);

         viewPort.SelectionStart = new Point(4, 1);
         viewPort.Edit("<000100>");
         viewPort.SelectionStart = new Point(6, 1);
         viewPort.SelectionEnd = new Point(2, 1);

         Assert.True(viewPort.IsSelected(new Point(4, 1)));
         Assert.True(viewPort.IsSelected(new Point(5, 1)));
         Assert.True(viewPort.IsSelected(new Point(6, 1)));
         Assert.True(viewPort.IsSelected(new Point(7, 1)));
      }

      [Fact]
      public void HighlightRightSelectsWholePointer() {
         CreateStandardTestSetup(out var viewPort, out var model, out var data);

         viewPort.SelectionStart = new Point(4, 1);
         viewPort.Edit("<000100>");
         viewPort.SelectionStart = new Point(6, 1);
         viewPort.SelectionEnd = new Point(10, 1);

         Assert.True(viewPort.IsSelected(new Point(4, 1)));
         Assert.True(viewPort.IsSelected(new Point(5, 1)));
         Assert.True(viewPort.IsSelected(new Point(6, 1)));
         Assert.True(viewPort.IsSelected(new Point(7, 1)));
      }

      [Fact]
      public void ContextMenuContainsCopyPaste() {
         CreateStandardTestSetup(out var viewPort, out var model, out var data);

         viewPort.SelectionStart = new Point(2, 2);
         viewPort.SelectionEnd = new Point(5, 2);
         var items = viewPort.GetContextMenuItems(viewPort.SelectionStart);
         items.Single(item => item.Text == "Copy");
         items.Single(item => item.Text == "Paste");

         viewPort.Edit("<000100>");
         viewPort.SelectionStart = new Point(3, 2);
         items = viewPort.GetContextMenuItems(viewPort.SelectionStart);
         items.Single(item => item.Text == "Copy");
         items.Single(item => item.Text == "Paste");

         viewPort.ClearAnchor();
         viewPort.Model.WriteMultiByteValue(0x22, 4, new ModelDelta(), 0x000000FF);
         viewPort.Edit("^text\"\" Hello World!\"");
         viewPort.SelectionStart = new Point(5, 2);
         viewPort.ExpandSelection(5, 2);
         items = viewPort.GetContextMenuItems(viewPort.SelectionStart);
         items.Single(item => item.Text == "Copy");
         items.Single(item => item.Text == "Paste");
      }

      [Fact]
      public void CanForkData() {
         CreateStandardTestSetup(out var viewPort, out var model, out var data);
         viewPort.Edit("<000008> <000008> FF @08 ^text\"\" Test\" @00");
         viewPort.SelectionStart = new Point();
         var items = viewPort.GetContextMenuItems(new Point());

         items.Single(item => item.Text.StartsWith("Repoint")).Command.Execute();

         Assert.NotEqual(0x08, model.ReadPointer(0));
         var originalRun = (PCSRun)model.GetNextRun(8);
         var newRun = (PCSRun)model.GetNextRun(model.ReadPointer(0));
         Assert.Equal(originalRun.SerializeRun(), newRun.SerializeRun());
      }

      private static void CreateStandardTestSetup(out ViewPort viewPort, out PokemonModel model, out byte[] data) {
         data = new byte[0x200];
         model = new PokemonModel(data);
         viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };
      }
   }
}
