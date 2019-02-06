using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class ChangeHistoryTests {

      private readonly ChangeHistory<List<int>> history;
      private int callCount = 0;
      private List<int> recentChanges;

      public ChangeHistoryTests() {
         history = new ChangeHistory<List<int>>(changes => {
            callCount++;
            recentChanges = changes;
            return changes.Select(i => -i).ToList();
         });
      }

      [Fact]
      public void CannotUndoByDefault() {
         Assert.False(history.Undo.CanExecute(null));
      }

      [Fact]
      public void CallingUndoWhenStackIsEmptyDoesNothing() {
         history.Undo.Execute();
         history.Redo.Execute();
         Assert.Equal(0, callCount);
      }

      [Fact]
      public void CanUndoAfterCheckingCurrentTransaction() {
         history.CurrentChange.Count();
         Assert.True(history.Undo.CanExecute(null));
         Assert.False(history.Redo.CanExecute(null));
      }

      [Fact]
      public void UndoCallsRevertDelegate() {
         history.CurrentChange.Count();
         history.Undo.Execute();
         Assert.Equal(1, callCount);
         Assert.False(history.Undo.CanExecute(null));
         Assert.True(history.Redo.CanExecute(null));
      }

      [Fact]
      public void UndoPassesInRecentChanges() {
         history.CurrentChange.Add(3);
         history.Undo.Execute();

         Assert.NotNull(recentChanges);
         Assert.Single(recentChanges);
         Assert.Equal(3, recentChanges[0]);
      }

      [Fact]
      public void RedoPassesInRecentChanges() {
         history.CurrentChange.Add(3);
         history.Undo.Execute();
         history.Redo.Execute();

         Assert.NotNull(recentChanges);
         Assert.Single(recentChanges);
         Assert.Equal(-3, recentChanges[0]);
      }

      [Fact]
      public void CannotRedoAfterNewChange() {
         history.CurrentChange.Add(3);
         history.Undo.Execute();
         history.CurrentChange.Add(4);

         Assert.True(history.Undo.CanExecute(null));
         Assert.False(history.Redo.CanExecute(null));
      }

      [Fact]
      public void ClosingEmptyChangeDoesNotAddUndoItem() {
         history.ChangeCompleted();

         Assert.False(history.Undo.CanExecute(null));
      }

      [Fact]
      public void ClosingChangesAllowsForMultipleUndos() {
         history.CurrentChange.Add(3);
         history.ChangeCompleted();
         history.CurrentChange.Add(4);

         history.Undo.Execute();
         Assert.Equal(4, recentChanges[0]);

         history.Undo.Execute();
         Assert.Equal(3, recentChanges[0]);
      }

      [Fact]
      public void UndoCanExecuteChangeFiresCorrectly() {
         var canExecuteChangeCalled = 0;
         history.Undo.CanExecuteChanged += (sender, e) => canExecuteChangeCalled++;

         history.CurrentChange.Add(3);
         Assert.Equal(1, canExecuteChangeCalled);

         history.Undo.Execute();
         Assert.Equal(2, canExecuteChangeCalled);
      }

      [Fact]
      public void RedoCanExecuteChangeFiresCorrectlyForRedoOperation() {
         var canExecuteChangeCalled = 0;
         history.Redo.CanExecuteChanged += (sender, e) => canExecuteChangeCalled++;

         history.CurrentChange.Add(3);
         Assert.Equal(0, canExecuteChangeCalled);

         history.Undo.Execute();
         Assert.Equal(1, canExecuteChangeCalled);

         history.Redo.Execute();
         Assert.Equal(2, canExecuteChangeCalled);
      }

      [Fact]
      public void RedoCanExecuteChangeFiresCorrectlyForNewChange() {
         var canExecuteChangeCalled = 0;
         history.Redo.CanExecuteChanged += (sender, e) => canExecuteChangeCalled++;

         history.CurrentChange.Add(3);
         Assert.Equal(0, canExecuteChangeCalled);

         history.Undo.Execute();
         Assert.Equal(1, canExecuteChangeCalled);

         history.CurrentChange.Add(4); // adding another change clears the redo stack
         Assert.Equal(2, canExecuteChangeCalled);
      }

      [Fact]
      public void ThrowExceptionIfChangeStartsDuringUndo() {
         // setup a revert call that tries to access a change during revert
         ChangeHistory<object> history = null;
         history = new ChangeHistory<object>(token => history.CurrentChange.ToString());

         history.CurrentChange.ToString(); // create current change
         Assert.Throws<InvalidOperationException>(() => history.Undo.Execute());
      }

      [Fact]
      public void ThrowExceptionIfChangeCompletedDuringUndo() {
         ChangeHistory<object> history = null;
         history = new ChangeHistory<object>(token => {
            history.CurrentChange.ToString();
            history.ChangeCompleted();
            return new object();
         });

         history.CurrentChange.ToString(); // create current change
         Assert.Throws<InvalidOperationException>(() => history.Undo.Execute());
      }

      [Fact]
      public void IsSavedIsRecalledCorrectlyWhenSavingAfterUndo() {
         history.CurrentChange.Add(1); // 1
         history.ChangeCompleted();
         history.CurrentChange.Add(2); // 1 2
         history.ChangeCompleted();
         history.CurrentChange.Add(3); // 1 2 3
         history.ChangeCompleted();

         history.Undo.Execute();       // 1 2  |  3
         history.Undo.Execute();       // 1  |  2 3
         history.TagAsSaved();

         history.Redo.Execute();       // 1 2  |  3
         history.CurrentChange.Add(4); // 1 2 4
         history.ChangeCompleted();

         history.Undo.Execute(); // 1 2  |  4
         history.Undo.Execute(); // 1  | 2 4

         // even though the redo history is different,
         // the current state is the same as when we last saved
         Assert.True(history.IsSaved);
      }

      [Fact]
      public void CanUndoFormatChange() {
         var data = new byte[0x100];
         var model = new PokemonModel(data);
         var viewPort = new ViewPort(new LoadedFile("test.txt", data), model) { Width = 0x10, Height = 0x10 };

         viewPort.SelectionStart = new Point(4, 0);
         viewPort.Edit("<000030>");
         Assert.Equal(0x04, model.GetNextRun(0).Start);

         viewPort.Undo.Execute();
         Assert.Equal(int.MaxValue, model.GetNextRun(0).Start);
      }

      [Fact]
      public void CanUndoDataMove() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model) { Width = 0x10, Height = 0x10 };

         viewPort.SelectionStart = new Point(8, 0);
         viewPort.Edit("<000030>");

         viewPort.SelectionStart = new Point(0, 0);
         viewPort.Edit("^bob\"\" \"Hello World!\"");

         viewPort.Undo.Execute(); // should undo the entire last edit transaction

         Assert.Equal(0xFF, model[0]);
         Assert.Equal(8, model.GetNextRun(0).Start);
         Assert.Equal(0x30, model.GetNextRun(0x10).Start);
         Assert.Equal(int.MaxValue, model.GetNextRun(0x31).Start);
      }

      [Fact]
      public void CanUndoFromToolChange() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model) { Width = 0x10, Height = 0x10 };

         viewPort.Edit("^bob\"\" ");

         // move the selection to force a break in the undo history
         viewPort.SelectionStart = new Point(3, 3);
         viewPort.SelectionStart = new Point(0, 0);

         viewPort.Tools.StringTool.Address = 0;
         viewPort.Tools.StringTool.Content = "Hello World!";
         viewPort.Undo.Execute(); // should undo only the tool change, not the name change

         Assert.Equal(0xFF, model[0]);
         Assert.True(viewPort.Undo.CanExecute(null));
      }

      [Fact]
      public void UndoCanHandleNameMove() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model) { Width = 0x10, Height = 0x10 };

         // operation 1
         viewPort.Edit("<bob> 03 08 24 16 <bob>");

         // operation 2
         viewPort.SelectionStart = new Point(0, 1);
         viewPort.Edit("^bob ");

         // operation 3
         viewPort.SelectionStart = new Point(0, 2);
         viewPort.Edit("^bob ");

         Assert.Equal(0x20, model.ReadPointer(0x00));
         Assert.Equal("bob", ((Pointer)viewPort[0, 0].Format).DestinationName);

         // undo operation 3
         viewPort.Undo.Execute();
         Assert.Equal(0x10, model.ReadPointer(0x00));
         Assert.Equal("bob", ((Pointer)viewPort[0, 0].Format).DestinationName);

         // undo operation 2
         viewPort.Undo.Execute();
         Assert.Equal(Pointer.NULL, model.ReadPointer(0x00));
         Assert.Equal("bob", ((Pointer)viewPort[0, 0].Format).DestinationName);
      }

      [Fact]
      public void UndoWorksAfterMidPointerEdit() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model) { Width = 0x10, Height = 0x10 };

         viewPort.Edit("<000100>");
         viewPort.SelectionStart = new Point(1, 0);
         viewPort.Edit("<000180>");
         viewPort.Undo.Execute();

         Assert.Equal(0x000100, model.ReadPointer(0));
      }

      [Fact]
      public void UndoRedoRestoresUnmappedNames() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model) { Width = 0x10, Height = 0x10 };

         viewPort.Edit("<bob>");
         viewPort.SelectionStart = new Point(0, 0);
         viewPort.Edit("<tom>");

         viewPort.Undo.Execute();
         Assert.Equal("bob", ((Pointer)viewPort[0, 0].Format).DestinationName);

         viewPort.Redo.Execute();
         Assert.Equal("tom", ((Pointer)viewPort[0, 0].Format).DestinationName);
      }
   }
}
