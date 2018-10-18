using HavenSoft.Gen3Hex.Model;
using HavenSoft.Gen3Hex.ViewModel;
using Xunit;

namespace HavenSoft.HexTests {
   public class ViewPortEditTests {
      [Fact]
      public void CanEditData() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };

         viewPort.SelectionStart = new Point(2, 2);
         viewPort.Edit("A");
         viewPort.Edit("D");

         Assert.Equal(0xAD, viewPort[2, 2].Value);
         Assert.Equal(new Point(3, 2), viewPort.SelectionStart);
      }

      [Fact]
      public void CanEditMultipleBytesInARow() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };

         viewPort.SelectionStart = new Point(2, 2);
         viewPort.Edit("DEADBEEF");

         Assert.Equal(0xDE, viewPort[2, 2].Value);
         Assert.Equal(0xAD, viewPort[3, 2].Value);
         Assert.Equal(0xBE, viewPort[4, 2].Value);
         Assert.Equal(0xEF, viewPort[0, 3].Value);
      }

      [Fact]
      public void CanUndoEdit() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };

         viewPort.SelectionStart = new Point(2, 2);
         viewPort.Edit("AD");
         viewPort.Undo.Execute();

         Assert.Equal(0x00, viewPort[2, 2].Value);
      }

      [Fact]
      public void UndoCanReverseMultipleByteChanges() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };

         viewPort.SelectionStart = new Point(2, 2);
         viewPort.Edit("DEADBEEF");
         viewPort.Undo.Execute();

         Assert.Equal(0x00, viewPort[2, 2].Value);
         Assert.Equal(0x00, viewPort[2, 3].Value);
         Assert.Equal(0x00, viewPort[2, 4].Value);
         Assert.Equal(0x00, viewPort[3, 0].Value);
      }

      [Fact]
      public void MovingBetweenChangesCausesSeparateUndo() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };

         viewPort.SelectionStart = new Point(2, 2);
         viewPort.Edit("01");
         viewPort.SelectionStart = new Point(3, 2);
         viewPort.Edit("02");
         viewPort.Undo.Execute();

         Assert.Equal(0x01, viewPort[2, 2].Value);
         Assert.Equal(0x00, viewPort[3, 2].Value);
      }

      [Fact]
      public void CanRedo() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };

         viewPort.Edit("DEADBEEF");
         Assert.False(viewPort.Redo.CanExecute(null));

         viewPort.Undo.Execute();
         Assert.True(viewPort.Redo.CanExecute(null));

         viewPort.Redo.Execute();
         Assert.False(viewPort.Redo.CanExecute(null));
      }

      [Fact]
      public void UndoFixesCorrectDataAfterScroll() {
         var loadedFile = new LoadedFile("test", new byte[30]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };
         viewPort.SelectionStart = new Point(2, 2);
         viewPort.Edit("FF");

         viewPort.SelectionStart = new Point(2, 2);
         viewPort.Edit("EE");
         viewPort.Scroll.Execute(Direction.Down);
         viewPort.Undo.Execute();

         Assert.Equal(1, viewPort.ScrollValue);
         Assert.Equal(0xFF, viewPort[2, 1].Value);
      }

      [Fact]
      public void EditMovesSelection() {
         var loadedFile = new LoadedFile("test", new byte[30]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };

         viewPort.SelectionStart = new Point(2, 2);
         viewPort.Edit("FF");

         Assert.Equal(new Point(3, 2), viewPort.SelectionStart);
      }

      [Fact]
      public void UndoDoesNotMoveSelection() {
         var loadedFile = new LoadedFile("test", new byte[30]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };

         viewPort.SelectionStart = new Point(2, 2);
         viewPort.Edit("FF");
         viewPort.Undo.Execute();

         Assert.Equal(new Point(3, 2), viewPort.SelectionStart);
      }

      [Fact]
      public void UndoCanCauseScrolling() {
         var loadedFile = new LoadedFile("test", new byte[30]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };

         viewPort.SelectionStart = new Point(0, 0);
         viewPort.Edit("FF");
         viewPort.Scroll.Execute(Direction.Down);
         viewPort.Undo.Execute();

         Assert.Equal(0, viewPort.ScrollValue);
      }
   }
}
