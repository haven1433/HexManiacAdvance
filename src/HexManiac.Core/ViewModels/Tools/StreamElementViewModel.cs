using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public abstract class StreamElementViewModel : ViewModelCore, IStreamArrayElementViewModel {

      public IDataModel Model { get; }
      public int Start { get; protected set; }
      public string RunFormat { get; protected set; }

      public bool visible = true;
      public bool Visible { get => visible; set => Set(ref visible, value); }


      #region IStreamArrayElementViewModel Properties

      public SplitterArrayElementViewModel Parent { get; set; }

      public int usageCount;

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

      public string theme; public string Theme { get => theme; set => Set(ref theme, value); }
      public bool IsInError => !string.IsNullOrEmpty(ErrorText);

      public string errorText;
      public string ErrorText {
         get => errorText;
         set => TryUpdate(ref errorText, value);
      }

      public string ParentName { get; set; }

      public int zIndex;
      public int ZIndex { get => zIndex; set => Set(ref zIndex, value); }

      public EventHandler<(int originalStart, int newStart)> dataMoved;
      public event EventHandler<(int originalStart, int newStart)> DataMoved { add => dataMoved += value; remove => dataMoved -= value; }
      protected void RaiseDataMoved(int originalStart, int newStart) => dataMoved?.Invoke(this, (originalStart, newStart));

      public EventHandler dataChanged;
      public event EventHandler DataChanged { add => dataChanged += value; remove => dataChanged -= value; }
      protected void RaiseDataChanged() => dataChanged?.Invoke(this, EventArgs.Empty);

      public EventHandler dataSelected;
      public event EventHandler DataSelected { add => dataSelected += value; remove => dataSelected -= value; }
      protected void RaiseDataSelected() => dataSelected?.Invoke(this, EventArgs.Empty);

      #endregion

      #region TryCopy

      // if the source of the copy attempt is a datachange that I triggered myself, then ignore the copy and keep the same contents.
      public bool overrideCopyAttempt;

      protected IDisposable PreventSelfCopy() {
         overrideCopyAttempt = true;
         return new StubDisposable { Dispose = () => overrideCopyAttempt = false };
      }

      #endregion

   }
}
