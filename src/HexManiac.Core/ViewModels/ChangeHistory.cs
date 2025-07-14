using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

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
      public readonly Func<T, T> revert;
      public readonly int maxSize;
      public Stack<T>
         undoStack = new Stack<T>(),
         redoStack = new Stack<T>();

      public bool revertInProgress;
      public bool customChangeInProgress;
      public T currentChange;
      public int undoStackSizeAtSaveTag;

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
      }

      public bool continueCurrentTransaction;
      public bool hasDataChangeCache;
      public void VerifyRevertNotInProgress([CallerMemberName] string caller = null) {
         if (!revertInProgress) return;
         throw new InvalidOperationException($"Cannot execute member {caller} while a revert is in progress.");
      }

      public IDisposable CreateRevertScope() {
         revertInProgress = true;
         var stub = new StubDisposable { Dispose = () => revertInProgress = false };
         return stub;
      }
   }
}
