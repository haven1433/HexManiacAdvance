using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public abstract class StreamElementViewModel : ViewModelCore, IStreamArrayElementViewModel {

      public ViewPort ViewPort { get; }
      public IDataModel Model { get; }
      public int Start { get; private set; }

      public ICommand Undo { get; }
      public ICommand Redo { get; }

      #region IStreamArrayElementViewModel Properties

      public bool ShowContent => UsageCount != 0;

      private int usageCount;
      public int UsageCount {
         get => usageCount;
         set {
            if (!TryUpdate(ref usageCount, value)) return;
            NotifyPropertyChanged(nameof(ShowContent));
            NotifyPropertyChanged(nameof(CanRepoint));
            NotifyPropertyChanged(nameof(CanCreateNew));
            repoint.CanExecuteChanged.Invoke(repoint, EventArgs.Empty);
            createNew.CanExecuteChanged.Invoke(createNew, EventArgs.Empty);
         }
      }

      public bool CanRepoint => UsageCount > 1;

      private StubCommand repoint = new StubCommand();
      public ICommand Repoint => repoint;

      public bool CanCreateNew => UsageCount == 0;

      private readonly StubCommand createNew = new StubCommand();
      public ICommand CreateNew => createNew;

      public bool IsInError => !string.IsNullOrEmpty(ErrorText);

      private string errorText;
      public string ErrorText {
         get => errorText;
         set => TryUpdate(ref errorText, value);
      }

      private EventHandler<(int originalStart, int newStart)> dataMoved;
      public event EventHandler<(int originalStart, int newStart)> DataMoved { add => dataMoved += value; remove => dataMoved -= value; }
      protected void RaiseDataMoved(int originalStart, int newStart) => dataMoved?.Invoke(this, (originalStart, newStart));

      private event EventHandler dataChanged;
      public event EventHandler DataChanged { add => dataChanged += value; remove => dataChanged -= value; }
      protected void RaiseDataChanged() => dataChanged?.Invoke(this, EventArgs.Empty);

      #endregion

      public StreamElementViewModel(ViewPort viewPort, int start) {
         ViewPort = viewPort;
         Model = viewPort.Model;
         Start = start;
         Undo = ViewPort.Undo;
         Redo = ViewPort.Redo;

         repoint.CanExecute = arg => CanRepoint;
         repoint.Execute = ExecuteRepoint;

         createNew.CanExecute = arg => CanCreateNew;
         createNew.Execute = ExecuteCreateNew;

         // by the time we get this far, we're nearly guaranteed that this will be a IStreamRun.
         // if it's not an IStreamRun, it's because the pointer in the array doesn't actually point to a valid stream.
         // (It might point to an IPagedRun or some other stream-like element.)
         // at which point, we don't want to display any content.
         var destination = Model.ReadPointer(Start);
         if (destination == Pointer.NULL) {
            UsageCount = 0;
         } else {
            var run = Model.GetNextRun(destination) as IStreamRun;
            UsageCount = run?.PointerSources?.Count ?? 0;
         }
      }

      #region TryCopy

      // if the source of the copy attempt is a datachange that I triggered myself, then ignore the copy and keep the same contents.
      private bool overrideCopyAttempt;
      public bool TryCopy(IArrayElementViewModel other) {
         if (!(other is StreamElementViewModel that)) return false;
         if (overrideCopyAttempt) return true;
         if (!TryCopy(that)) return false;

         UsageCount = that.UsageCount;
         ErrorText = that.ErrorText;
         dataMoved = that.dataMoved;
         dataChanged = that.dataChanged;
         Start = that.Start;

         return true;
      }

      protected IDisposable PreventSelfCopy() {
         overrideCopyAttempt = true;
         return new StubDisposable { Dispose = () => overrideCopyAttempt = false };
      }

      protected abstract bool TryCopy(StreamElementViewModel other);

      #endregion

      private void ExecuteRepoint(object arg) {
         using (ModelCacheScope.CreateScope(Model)) {
            var originalDestination = Model.ReadPointer(Start);
            ViewPort.RepointToNewCopy(Start);
            UsageCount = 1;
            dataChanged?.Invoke(this, EventArgs.Empty);
         }
      }

      private void ExecuteCreateNew(object arg) {
         using (ModelCacheScope.CreateScope(Model)) {
            ViewPort.RepointToNewCopy(Start);
            UsageCount = 1;
            dataChanged?.Invoke(this, EventArgs.Empty);
         }
      }
   }
}
