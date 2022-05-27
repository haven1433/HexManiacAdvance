using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Factory;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public abstract class StreamElementViewModel : ViewModelCore, IStreamArrayElementViewModel {

      public ViewPort ViewPort { get; }
      public IDataModel Model { get; }
      public int Start { get; protected set; }
      public string RunFormat { get; protected set; }

      private bool visible = true;
      public bool Visible { get => visible; set => Set(ref visible, value); }

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
            NotifyPropertyChanged(nameof(CanRepointAll));
            NotifyPropertyChanged(nameof(HasMultipleUses));
            NotifyPropertyChanged(nameof(CanCreateNew));
            repoint.CanExecuteChanged.Invoke(repoint, EventArgs.Empty);
            createNew.CanExecuteChanged.Invoke(createNew, EventArgs.Empty);
         }
      }

      public bool HasMultipleUses => UsageCount > 1;

      public bool CanRepoint => !CanCreateNew && (UsageCount > 1 || DataIsValidButNoRun || DataIsNotValid);

      public bool CanRepointAll => UsageCount > 2;

      public bool DataIsValidButNoRun {
         get {
            var destination = Model.ReadPointer(Start);
            if (destination < 0 || destination >= Model.Count) return false;
            var run = Model.GetNextRun(destination);
            if (run.Start == destination) return false;
            run = new NoInfoRun(destination, new SortedSpan<int>(Start));
            var error = Model.FormatRunFactory.GetStrategy(RunFormat).TryParseData(Model, string.Empty, destination, ref run);
            return !error.HasError;
         }
      }

      public bool DataIsNotValid {
         get {
            var destination = Model.ReadPointer(Start);
            if (destination < 0 || destination >= Model.Count) return true;
            var run = Model.GetNextRun(destination);
            if (run.Start != destination) return true;
            using (ModelCacheScope.CreateScope(Model)) {
               var error = Model.FormatRunFactory.GetStrategy(RunFormat).TryParseData(Model, string.Empty, destination, ref run);
               return error.HasError;
            }
         }
      }

      public IReadOnlyList<AutocompleteItem> GetAutoCompleteOptions(string line, int caretLineIndex, int caretCharacterIndex) {
         var destination = Model.ReadPointer(Start);
         if (Model.GetNextRun(destination) is IStreamRun streamRun) {
            var options = streamRun.GetAutoCompleteOptions(line, caretLineIndex, caretCharacterIndex);
            if (options != null && options.Count > 0) ZIndex = 1;
            return options;
         } else {
            return new List<AutocompleteItem>();
         }
      }

      public void ClearAutocomplete() => ZIndex = 0;

      private readonly StubCommand repoint = new StubCommand();
      public ICommand Repoint => repoint;

      private readonly StubCommand repointAll = new StubCommand();
      public ICommand RepointAll => repointAll;

      public bool CanCreateNew => UsageCount == 0;

      private readonly StubCommand createNew = new StubCommand();
      public ICommand CreateNew => createNew;

      public bool IsInError => !string.IsNullOrEmpty(ErrorText);

      private string errorText;
      public string ErrorText {
         get => errorText;
         set => TryUpdate(ref errorText, value);
      }

      public string ParentName { get; private set; }

      private int zIndex;
      public int ZIndex { get => zIndex; private set => Set(ref zIndex, value); }

      private EventHandler<(int originalStart, int newStart)> dataMoved;
      public event EventHandler<(int originalStart, int newStart)> DataMoved { add => dataMoved += value; remove => dataMoved -= value; }
      protected void RaiseDataMoved(int originalStart, int newStart) => dataMoved?.Invoke(this, (originalStart, newStart));

      private EventHandler dataChanged;
      public event EventHandler DataChanged { add => dataChanged += value; remove => dataChanged -= value; }
      protected void RaiseDataChanged() => dataChanged?.Invoke(this, EventArgs.Empty);

      private EventHandler dataSelected;
      public event EventHandler DataSelected { add => dataSelected += value; remove => dataSelected -= value; }
      protected void RaiseDataSelected() => dataSelected?.Invoke(this, EventArgs.Empty);

      #endregion

      public StreamElementViewModel(ViewPort viewPort, string parentName, string runFormat, int start) {
         ViewPort = viewPort;
         Model = viewPort.Model;
         Start = start;
         RunFormat = runFormat;
         ParentName = parentName;
         Undo = ViewPort.Undo;
         Redo = ViewPort.Redo;

         repoint.CanExecute = arg => CanRepoint;
         repoint.Execute = ExecuteRepoint;
         repointAll.CanExecute = arg => CanRepointAll;
         repointAll.Execute = ExecuteRepointAll;

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
         dataSelected = that.dataSelected;
         Start = that.Start;
         RunFormat = that.RunFormat;
         Visible = that.Visible;
         ParentName = that.ParentName;
         NotifyPropertyChanged(nameof(CanRepoint));
         NotifyPropertyChanged(nameof(CanRepointAll));
         repoint.RaiseCanExecuteChanged();
         NotifyPropertyChanged(nameof(DataIsValidButNoRun));
         NotifyPropertyChanged(nameof(ParentName));

         return true;
      }

      protected IDisposable PreventSelfCopy() {
         overrideCopyAttempt = true;
         return new StubDisposable { Dispose = () => overrideCopyAttempt = false };
      }

      protected abstract bool TryCopy(StreamElementViewModel other);

      #endregion

      private void ExecuteRepoint(object arg) {
         ViewPort.RepointToNewCopy(Start);
         ViewPort.Refresh();
         UsageCount = 1;
         dataChanged?.Invoke(this, EventArgs.Empty);
      }

      private void ExecuteRepointAll(object arg) {
         var destination = Model.ReadPointer(Start);
         var sources = Model.GetNextRun(destination).PointerSources.Skip(1).ToList();
         RepointAllAsync(sources).ContinueWith(completedTask => {
            ViewPort.ClearProgress();
            ViewPort.Refresh();
            UsageCount = 1;
            dataChanged?.Invoke(this, EventArgs.Empty);
         }, TaskContinuationOptions.ExecuteSynchronously);
      }

      private async Task<ErrorInfo> RepointAllAsync(IReadOnlyList<int> sources) {
         var buffer = ViewPort.Model.FreeSpaceBuffer;
         ViewPort.Model.FreeSpaceBuffer = 0;
         var groupSize = Math.Max(1, sources.Count / 100);
         for (int i = 0; i < sources.Count; i++) {
            ViewPort.RepointToNewCopy(sources[i]);
            if (i % groupSize == 0) {
               await ViewPort.UpdateProgress((i + 1) / (double)sources.Count);
            }
         }
         ViewPort.Model.FreeSpaceBuffer = buffer;
         return ErrorInfo.NoError;
      }

      private void ExecuteCreateNew(object arg) {
         ViewPort.RepointToNewCopy(Start);
         ViewPort.Refresh();
         UsageCount = 1;
         dataChanged?.Invoke(this, EventArgs.Empty);
      }
   }
}
