using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels {
   public interface IChangeToken {
      bool HasDataChange { get; }
      bool HasAnyChange { get; }
      event EventHandler OnNewChange;
   }

   /// <summary>
   /// Represents a history of changes that can undo / redo.
   /// The change can be reperesented by any class with an empty constructor.
   /// New change objects will be created automatically as needed.
   /// The user is responsible for using the change object to revert a change via the constructor delegate call.
   /// The user is responsible for converting from a backward change object to a forward change (redo) object.
   /// The user is responsible for assigning boundaries between changes by calling ChangeCompleted.
   /// </summary>
   /// <remarks>
   /// Aside from undo/redo, the ChangeHistory can also track whether the file has been changed since the last save.
   /// However, since ChangeHistory is not responsible for saving, you have to tell it whenever the data is saved.
   /// This is accomplished via the TagAsSaved() method.
   /// </remarks>
   public class ChangeHistory<T> : ViewModelCore where T : class, IChangeToken, new() {
      private readonly Func<T, T> revert;
      private readonly StubCommand undo, redo;
      private readonly int maxSize;
      private Stack<T>
         undoStack = new Stack<T>(),
         redoStack = new Stack<T>();

      private bool revertInProgress;
      private bool customChangeInProgress;
      private T currentChange;
      private int undoStackSizeAtSaveTag;

      public ICommand Undo => undo;
      public ICommand Redo => redo;
      public T CurrentChange {
         get {
            VerifyRevertNotInProgress();
            ClearRedoStack();

            if (customChangeInProgress) ChangeCompleted();

            if (currentChange == null) {
               PrepareNewToken(new T());
            }

            return currentChange;
         }
      }

      private void ClearRedoStack() {
         if (redoStack.Count > 0) {
            redoStack.Clear();
            if (undoStack.Count < undoStackSizeAtSaveTag) undoStackSizeAtSaveTag = -1;
            redo.RaiseCanExecuteChanged();
         }
      }

      private void PrepareNewToken(T token) {
         bool notifyIsSavedChanged = IsSaved;
         currentChange = token;
         currentChange.OnNewChange += OnCurrentTokenDataChanged;
         ClearRedoStack();
      }

      public bool IsSaved {
         get {
            return undoStackSizeAtSaveTag == undoStack.Count && (currentChange == null || !currentChange.HasAnyChange);
         }
      }

      public bool HasDataChange {
         get {
            if (IsSaved) return false;
            var addedElements = undoStack.Count - undoStackSizeAtSaveTag;
            var undoItems = undoStack.Reverse().ToArray();
            var redoItems = redoStack.ToArray();
            if (undoStackSizeAtSaveTag == -1) return true;
            for (int i = 0; i < addedElements; i++) {
               if (undoItems[undoStackSizeAtSaveTag + i].HasDataChange) return true;
            }
            for (int i = 0; i < -addedElements; i++) {
               if (redoItems[redoItems.Length - 1 - i].HasDataChange) return true;
            }
            return currentChange?.HasDataChange ?? false;
         }
      }

      public ChangeHistory(Func<T, T> revertChange, int maxStackSize = 100) {
         maxSize = maxStackSize;
         revert = revertChange;
         undo = new StubCommand {
            Execute = arg => UndoExecuted(),
            CanExecute = arg => undoStack.Count > 0 || (currentChange != null && currentChange.HasAnyChange),
         };
         redo = new StubCommand {
            Execute = arg => RedoExecuted(),
            CanExecute = arg => redoStack.Count > 0,
         };
      }

      public void ChangeCompleted() {
         if (!continueCurrentTransaction) customChangeInProgress = false;
         if (currentChange == null) return;
         if (!currentChange.HasAnyChange) { currentChange = null; return; }
         VerifyRevertNotInProgress();
         if (continueCurrentTransaction) return;

         undoStack.Push(currentChange);
         if (undoStack.Count > maxSize) {
            undoStack = new(undoStack.Take(maxSize).Reverse());
            undoStackSizeAtSaveTag = -1;
         }
         currentChange.OnNewChange -= OnCurrentTokenDataChanged;
         currentChange = null;
      }

      /// <summary>
      /// Inserts a custom change token into the undo stack.
      /// Any in-progress changes are considered complete.
      /// Calling for CurrentChange or ChangeComplete will complete this custom token.
      /// Returns the new in-progress change token, which you can use for your custom change.
      /// </summary>
      public T InsertCustomChange(T change) {
         if (continueCurrentTransaction) throw new InvalidOperationException("Inserting a change during a CurrentTransactionScope will cause changes to be lost.");
         ChangeCompleted();
         PrepareNewToken(change);
         OnCurrentTokenDataChanged(default, default);
         customChangeInProgress = true;
         return change;
      }

      public void TagAsSaved() {
         ChangeCompleted();
         hasDataChangeCache = false;
         if (TryUpdate(ref undoStackSizeAtSaveTag, undoStack.Count, nameof(IsSaved))) {
            NotifyPropertyChanged(nameof(HasDataChange));
         }
      }

      private bool continueCurrentTransaction;
      public IDisposable ContinueCurrentTransaction() {
         if (customChangeInProgress) ChangeCompleted();
         var previousValue = continueCurrentTransaction;
         continueCurrentTransaction = true;
         return new StubDisposable { Dispose = () => continueCurrentTransaction = previousValue };
      }

      private bool hasDataChangeCache;
      private void OnCurrentTokenDataChanged(object sender, EventArgs e) {
         if (undoStack.Count == 0) undo.RaiseCanExecuteChanged();
         var hasDataChange = HasDataChange;
         if (hasDataChange != hasDataChangeCache) {
            hasDataChangeCache = hasDataChange;
            NotifyPropertiesChanged(nameof(HasDataChange), nameof(IsSaved));
         } else if (!hasDataChange && currentChange.HasAnyChange) {
            NotifyPropertyChanged(nameof(IsSaved));
         }
      }

      private void UndoExecuted() {
         ChangeCompleted();
         if (undoStack.Count == 0) return;
         bool previouslyWasSaved = IsSaved;
         bool previouslyHadDataChanged = HasDataChange;

         using (CreateRevertScope()) {
            var originalChange = undoStack.Pop();
            if (undoStack.Count == 0) undo.RaiseCanExecuteChanged();
            var reverseChange = revert(originalChange);
            redoStack.Push(reverseChange);
            if (redoStack.Count == 1) redo.RaiseCanExecuteChanged();
         }

         if (previouslyWasSaved != IsSaved) NotifyPropertyChanged(nameof(IsSaved));
         hasDataChangeCache = HasDataChange;
         if (previouslyHadDataChanged != hasDataChangeCache) NotifyPropertyChanged(nameof(HasDataChange));
         Debug.Assert(redoStack.Count > 0, "Redo should always be available directly after an Undo!");
      }

      private void RedoExecuted() {
         if (redoStack.Count == 0) return;
         bool previouslyWasSaved = IsSaved;
         bool previouslyHadDataChanged = HasDataChange;
         VerifyRevertNotInProgress();

         using (CreateRevertScope()) {
            var reverseChange = redoStack.Pop();
            if (redoStack.Count == 0) redo.RaiseCanExecuteChanged();
            var originalChange = revert(reverseChange);
            undoStack.Push(originalChange);
            if (undoStack.Count == 1) undo.RaiseCanExecuteChanged();
         }

         if (previouslyWasSaved != IsSaved) NotifyPropertyChanged(nameof(IsSaved));
         hasDataChangeCache = HasDataChange;
         if (previouslyHadDataChanged != hasDataChangeCache) NotifyPropertyChanged(nameof(HasDataChange));
      }

      private void VerifyRevertNotInProgress([CallerMemberName] string caller = null) {
         if (!revertInProgress) return;
         throw new InvalidOperationException($"Cannot execute member {caller} while a revert is in progress.");
      }

      private IDisposable CreateRevertScope() {
         revertInProgress = true;
         var stub = new StubDisposable { Dispose = () => revertInProgress = false };
         return stub;
      }

      public void ClearHistory(bool needsSave) {
         VerifyRevertNotInProgress();
         if (!IsSaved || needsSave) undoStackSizeAtSaveTag = -1;
         undoStack.Clear();
         redoStack.Clear();
         currentChange = null;

         NotifyPropertyChanged(nameof(HasDataChange));
         NotifyPropertyChanged(nameof(IsSaved));
         undo.RaiseCanExecuteChanged();
         redo.RaiseCanExecuteChanged();
      }
   }
}
