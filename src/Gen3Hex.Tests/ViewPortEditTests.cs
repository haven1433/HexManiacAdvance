using HavenSoft.Gen3Hex.Core;
using HavenSoft.Gen3Hex.Core.Models;
using HavenSoft.Gen3Hex.Core.ViewModels;
using HavenSoft.Gen3Hex.Core.ViewModels.DataFormats;
using Xunit;

namespace HavenSoft.Gen3Hex.Tests {
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
         viewPort.SelectionStart = new Point(2, 3);
         viewPort.Edit("02");
         viewPort.Undo.Execute();

         Assert.Equal(0x01, viewPort[2, 2].Value);
         Assert.Equal(0x00, viewPort[2, 3].Value);
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

      [Fact]
      public void SingleCharacterEditChangesToUnderEditFormat() {
         var loadedFile = new LoadedFile("test", new byte[30]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };

         viewPort.SelectionStart = new Point(0, 2);
         viewPort.Edit("F");

         Assert.IsType<UnderEdit>(viewPort[0, 2].Format);
         Assert.Equal("F", ((UnderEdit)viewPort[0, 2].Format).CurrentText);
      }

      [Fact]
      public void UnsupportedCharacterRevertsChangeWithoutAddingUndoOperation() {
         var loadedFile = new LoadedFile("test", new byte[30]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };

         viewPort.SelectionStart = new Point(0, 2);
         viewPort.Edit("F");
         viewPort.Edit("|");

         Assert.IsType<None>(viewPort[0, 2].Format);
         Assert.Equal(new Point(0, 2), viewPort.SelectionStart);
         Assert.Equal(0, viewPort[0, 2].Value);
         Assert.False(viewPort.Undo.CanExecute(null));
      }

      [Fact]
      public void SelectionChangeRevertsChangeWithoutAddingUndoOperation() {
         var loadedFile = new LoadedFile("test", new byte[30]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };

         viewPort.SelectionStart = new Point(0, 2);
         viewPort.Edit("F");
         viewPort.SelectionStart = new Point(1, 2);

         Assert.IsType<None>(viewPort[0, 2].Format);
         Assert.Equal(new Point(1, 2), viewPort.SelectionStart);
         Assert.Equal(0, viewPort[0, 2].Value);
         Assert.False(viewPort.Undo.CanExecute(null));
      }

      [Fact]
      public void EditOnlyNotifiesCollectionChangeOnceWhenScrolling() {
         var loadedFile = new LoadedFile("test", new byte[30]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };
         int collectionNotifications = 0;
         viewPort.CollectionChanged += (sender, e) => collectionNotifications++;

         viewPort.SelectionStart = new Point(4, 4);
         viewPort.Edit("F");
         Assert.Equal(1, collectionNotifications);

         viewPort.Edit("F");
         Assert.Equal(2, collectionNotifications);
         Assert.Equal(new Point(0, 4), viewPort.SelectionStart);
         Assert.Equal(0xFF, viewPort[4, 3].Value);
      }

      [Fact]
      public void SelectionChangeDuringEditNotifiesCollectionChange() {
         var loadedFile = new LoadedFile("test", new byte[30]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };
         int collectionNotifications = 0;
         viewPort.CollectionChanged += (sender, e) => collectionNotifications++;

         viewPort.SelectionStart = new Point(4, 4);
         viewPort.Edit("F");
         Assert.Equal(1, collectionNotifications);

         viewPort.MoveSelectionStart.Execute(Direction.Up);
         Assert.Equal(2, collectionNotifications); // should have been notified since the visual data changed.
      }

      [Fact]
      public void UndoNotifiesCollectionChange() {
         var loadedFile = new LoadedFile("test", new byte[30]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };
         int collectionNotifications = 0;
         viewPort.CollectionChanged += (sender, e) => collectionNotifications++;

         viewPort.Edit("0102030405");
         collectionNotifications = 0;
         viewPort.Undo.Execute();

         Assert.Equal(1, collectionNotifications);
      }

      [Fact]
      public void UndoRestoresOriginalDataFormat() {
         var loadedFile = new LoadedFile("test", new byte[30]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };

         var originalFormat = viewPort[0, 0].Format;
         viewPort.Edit("ff");
         viewPort.Undo.Execute();

         Assert.IsType(originalFormat.GetType(), viewPort[0, 0].Format);
      }

      [Fact]
      void CanEnterDataAfterLastByte() {
         var loadedFile = new LoadedFile("test", new byte[20]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };

         viewPort.SelectionStart = new Point(0, 4);
         viewPort.Edit("00");

         Assert.NotEqual(Undefined.Instance, viewPort[0, 4].Format);
         Assert.Equal(new Point(1, 4), viewPort.SelectionStart);
      }

      [Fact]
      void CanClearData() {
         var loadedFile = new LoadedFile("test", new byte[1000]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };

         viewPort.SelectionStart = new Point(0, 0);
         viewPort.SelectionEnd = new Point(3, 3);
         viewPort.Clear.Execute();

         Assert.Equal(0xFF, viewPort[2, 2].Value);
         Assert.True(viewPort.Save.CanExecute(new StubFileSystem()));
      }

      [Fact]
      void CanCopyData() {
         var loadedFile = new LoadedFile("test", new byte[1000]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };
         var fileSystem = new StubFileSystem();

         viewPort.Edit("Cafe Babe");
         viewPort.SelectionStart = new Point(0, 0);
         viewPort.SelectionEnd = new Point(3, 0);
         viewPort.Copy.Execute(fileSystem);

         Assert.Equal("CA FE BA BE", fileSystem.CopyText);
      }
   }
}
