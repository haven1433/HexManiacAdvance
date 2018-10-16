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
         Assert.Equal(0xAD, viewPort[2, 3].Value);
         Assert.Equal(0xBE, viewPort[2, 4].Value);
         Assert.Equal(0xEF, viewPort[3, 0].Value);
      }

      [Fact]
      public void CanUndoEdit() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };

         viewPort.SelectionStart = new Point(2, 2);
         viewPort.Edit("AD");
         viewPort.Undo.Execute(null);

         Assert.Equal(0x00, viewPort[2, 2].Value);
      }

      [Fact]
      public void UndoCanReverseMultipleByteChanges() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };

         viewPort.SelectionStart = new Point(2, 2);
         viewPort.Edit("DEADBEEF");
         viewPort.Undo.Execute(null);

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
         viewPort.Undo.Execute(null);

         Assert.Equal(0x01, viewPort[2, 2].Value);
         Assert.Equal(0x00, viewPort[3, 2].Value);
      }

      [Fact]
      public void CanRedo() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };

         viewPort.Edit("DEADBEEF");
         Assert.False(viewPort.Redo.CanExecute(null));

         viewPort.Undo.Execute(null);
         Assert.True(viewPort.Redo.CanExecute(null));

         viewPort.Redo.Execute(null);
         Assert.False(viewPort.Redo.CanExecute(null));
      }
   }
}
