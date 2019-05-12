using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
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
         var viewPort = new ViewPort(loadedFile) { PreferredWidth = -1, Width = 5, Height = 5 };

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
      public void EscapeRevertsChangeWithoutAddingUndoOperation() {
         var loadedFile = new LoadedFile("test", new byte[30]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };

         viewPort.SelectionStart = new Point(0, 2);
         viewPort.Edit("F");
         viewPort.Edit(ConsoleKey.Escape);

         Assert.IsType<None>(viewPort[0, 2].Format);
         Assert.Equal(new Point(0, 2), viewPort.SelectionStart);
         Assert.Equal(0, viewPort[0, 2].Value);
         Assert.False(viewPort.Undo.CanExecute(null));
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
         Assert.Equal(3, collectionNotifications); // should have been notified since the visual data changed.
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
      public void CanEnterDataAfterLastByte() {
         var loadedFile = new LoadedFile("test", new byte[20]);
         var viewPort = new ViewPort(loadedFile) { PreferredWidth = -1, Width = 5, Height = 5 };

         viewPort.SelectionStart = new Point(0, 4);
         viewPort.Edit("00");

         Assert.NotEqual(Undefined.Instance, viewPort[0, 4].Format);
         Assert.Equal(new Point(1, 4), viewPort.SelectionStart);
      }

      [Fact]
      public void CanClearData() {
         var loadedFile = new LoadedFile("test", new byte[1000]);
         var viewPort = new ViewPort(loadedFile.Name, new PokemonModel(loadedFile.Contents)) { PreferredWidth = -1, Width = 5, Height = 5 };

         viewPort.SelectionStart = new Point(0, 0);
         viewPort.SelectionEnd = new Point(3, 3);
         viewPort.Clear.Execute();

         Assert.Equal(0xFF, viewPort[2, 2].Value);
         Assert.True(viewPort.Save.CanExecute(new StubFileSystem()));
      }

      [Fact]
      public void CanCopyData() {
         var loadedFile = new LoadedFile("test", new byte[1000]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };
         var fileSystem = new StubFileSystem();

         viewPort.Edit("Cafe Babe");
         viewPort.SelectionStart = new Point(0, 0);
         viewPort.SelectionEnd = new Point(3, 0);
         viewPort.Copy.Execute(fileSystem);

         Assert.Equal("CA FE BA BE", fileSystem.CopyText);
      }

      [Fact]
      public void CanBackspaceOnEdits() {
         var buffer = Enumerable.Range(0, 255).Select(i => (byte)i).ToArray();
         var file = new LoadedFile("file.txt", buffer);
         var viewPort = new ViewPort(file) { Width = 0x10, Height = 0x10 };

         viewPort.SelectionStart = new Point(4, 4); // current value: 0x44
         viewPort.Edit("C");
         viewPort.Edit(ConsoleKey.Backspace);

         var editFormat = (UnderEdit)viewPort[4, 4].Format;
         Assert.Equal(string.Empty, editFormat.CurrentText);

         viewPort.MoveSelectionStart.Execute(Direction.Down); // any movement should change the value based on what's left in the cell
         Assert.Equal(0xFF, viewPort[4, 4].Value); // note that the cell was empty, so it got the 'empty' value of FF
      }

      [Fact]
      public void BackspaceBeforeEditChangesPreviousCurrentCell() {
         var buffer = Enumerable.Range(0, 255).Select(i => (byte)i).ToArray();
         var file = new LoadedFile("file.txt", buffer);
         var viewPort = new ViewPort(file) { Width = 0x10, Height = 0x10 };

         viewPort.SelectionStart = new Point(4, 4); // current value: 0x44
         viewPort.Edit(ConsoleKey.Backspace);
         Assert.Equal(new Point(4, 4), viewPort.SelectionStart);

         viewPort.Edit(ConsoleKey.Backspace); // if I hit an arrow key now, it'll give up on the edit and just make the value something reasonable
         viewPort.Edit(ConsoleKey.Backspace); // but since I hit backspace, it commits the erasure and starts erasing the next cell
         Assert.Equal(0xFF, viewPort[4, 4].Value);
      }

      [Fact]
      public void ClearRemovesFormats() {
         var data = new byte[0x200];
         var model = new PokemonModel(data);
         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };

         viewPort.Edit("<000100>");
         viewPort.SelectionStart = new Point(2, 0);
         viewPort.Clear.Execute();

         Assert.Equal(int.MaxValue, model.GetNextRun(0).Start);
      }

      [Fact]
      public void TypingIntoPointerAutoInsertsStartOfPointer() {
         var model = new PokemonModel(new byte[0x200]);
         var viewModel = new ViewPort(string.Empty, model);
         viewModel.Edit("<000100>");
         viewModel.SelectionStart = new Point(0, 0);

         viewModel.Edit("000060");
         viewModel.Edit(ConsoleKey.Enter);

         Assert.Equal(0x60, model[0]);
      }

      [Fact]
      public void TypingTwoHexDigitsIntoPointerWithSpaceCanRemoveFormat() {
         var model = new PokemonModel(new byte[0x200]);
         var viewModel = new ViewPort(string.Empty, model);
         viewModel.Edit("<000100>");
         viewModel.SelectionStart = new Point(0, 0);

         viewModel.Edit("20 ");

         Assert.Equal(0x20, model[0]);
         Assert.NotInRange(model.GetNextRun(0).Start, 0, 0x200);
      }

      [Fact]
      public void UnderEditCellsKnowTheirEditLength() {
         var model = new PokemonModel(new byte[0x200]);
         var viewModel = new ViewPort(string.Empty, model) { Width = 0x10, Height = 0x10 };
         viewModel.Edit("^array[a: b. c. d<>]4 ");

         viewModel.SelectionStart = new Point(0, 0);
         viewModel.Edit("2");
         Assert.Equal(2, ((UnderEdit)viewModel[0, 0].Format).EditWidth);

         viewModel.SelectionStart = new Point(6, 0);
         viewModel.Edit("2");
         Assert.Equal(4, ((UnderEdit)viewModel[4, 0].Format).EditWidth);

         viewModel.SelectionStart = new Point(8, 6);
         viewModel.Edit("^");
         Assert.Equal(4, ((UnderEdit)viewModel[8, 6].Format).EditWidth);
      }
   }
}
