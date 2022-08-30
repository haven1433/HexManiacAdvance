using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.HexManiac.Core.Models {
   /// <summary>
   /// Represents a single conceptual change in the loaded data, including
   /// editing data, adding / removing formats / format names,
   /// and tracking pointers that lead to a name that isn't in the ROM yet.
   /// </summary>
   public class ModelDelta : IChangeToken {
      private readonly Dictionary<int, byte> oldData = new Dictionary<int, byte>();

      private readonly Dictionary<int, IFormattedRun> addedRuns = new Dictionary<int, IFormattedRun>();
      private readonly Dictionary<int, IFormattedRun> removedRuns = new Dictionary<int, IFormattedRun>();

      private readonly Dictionary<int, string>
         addedNames = new Dictionary<int, string>(),
         removedNames = new Dictionary<int, string>(),
         addedUnmappedPointers = new Dictionary<int, string>(),
         removedUnmappedPointers = new Dictionary<int, string>(),
         addedMatchedWords = new Dictionary<int, string>(),
         removedMatchedWords = new Dictionary<int, string>();

      private readonly Dictionary<int, int>
         addedOffsetPointers = new Dictionary<int, int>(),
         removedOffsetPointers = new Dictionary<int, int>();

      private readonly Dictionary<string, int>
         addedUnmappedConstants = new Dictionary<string, int>(),
         removedUnmappedConstants = new Dictionary<string, int>();

      private readonly Dictionary<string, ValidationList>
         addedLists = new Dictionary<string, ValidationList>(),
         removedLists = new Dictionary<string, ValidationList>();

      private int oldDataLength = -1, newDataLength = -1;

      public event EventHandler OnNewChange;
      protected void NotifyOnNewChange() => OnNewChange?.Invoke(this, EventArgs.Empty);
      public bool HasDataChange { get; protected set; }
      public bool HasAnyChange =>
         HasDataChange ||
         addedRuns.Any() ||
         removedRuns.Any() ||
         addedNames.Any() ||
         removedNames.Any() ||
         addedMatchedWords.Any() ||
         removedMatchedWords.Any() ||
         addedUnmappedPointers.Any() ||
         removedUnmappedPointers.Any() ||
         addedUnmappedConstants.Any() ||
         removedLists.Any() ||
         addedLists.Any() ||
         removedUnmappedConstants.Any();

      public int EarliestChange {
         get {
            if (oldData.Count > 0) return oldData.Keys.Min();

            if (addedNames.Count > 0) return addedNames.Keys.Min();

            var filteredRuns = addedRuns.Values.Where(added => !(added is NoInfoRun)).ToList();
            if (filteredRuns.Any()) return filteredRuns.Min(added => added.Start);

            if (addedUnmappedPointers.Count > 0) return addedUnmappedPointers.Keys.Min();

            if (removedNames.Count > 0) return removedNames.Keys.Min();

            filteredRuns = removedRuns.Values.Where(removed => !(removed is NoInfoRun)).ToList();
            if (filteredRuns.Any()) return filteredRuns.Min(removed => removed.Start);

            if (removedUnmappedPointers.Count > 0) return removedUnmappedPointers.Keys.Min();

            return -1;
         }
      }

      public virtual bool ChangeData(IDataModel model, int index, byte data) {
         if (model.Count > index && model[index] == data) return false;
         if (!oldData.ContainsKey(index)) {
            if (model.Count <= index) {
               model.ExpandData(this, index);
            }
            oldData[index] = model[index];
         }

         var valueChanged = model[index] != data;
         model[index] = data;
         if (!HasDataChange) {
            HasDataChange = true;
            OnNewChange?.Invoke(this, EventArgs.Empty);
         }

         return valueChanged;
      }

      public bool ChangeData(IDataModel model, int index, IReadOnlyList<byte> array) {
         bool anyChanges = false;
         for (int i = 0; i < array.Count; i++) anyChanges |= ChangeData(model, index + i, array[i]);
         return anyChanges;
      }

      public bool SetDataLength(IDataModel model, int length) {
         if (oldDataLength == -1) oldDataLength = model.RawData.Length;
         newDataLength = length;
         if (!HasAnyChange) {
            HasDataChange |= oldDataLength != newDataLength;
            if (HasDataChange) {
               OnNewChange?.Invoke(this, EventArgs.Empty);
            }
         }
         return true;
      }

      public void ChangeList(string name, ValidationList oldValues, ValidationList newValues) {
         using (CaptureNonDataChange()) {
            if (!removedLists.ContainsKey(name)) removedLists.Add(name, oldValues);
            addedLists[name] = newValues;
         }
      }

      public void AddRun(IFormattedRun run) {
         using (CaptureNonDataChange())
         addedRuns[run.Start] = run;
      }

      public void RemoveRun(IFormattedRun run) {
         using (CaptureNonDataChange())
         if (addedRuns.ContainsKey(run.Start)) {
            addedRuns.Remove(run.Start);
         } else if (!removedRuns.ContainsKey(run.Start)) {
            removedRuns[run.Start] = run;
         }
      }

      public void AddName(int index, string name) {
         using (CaptureNonDataChange())
         addedNames[index] = name;
      }

      public void RemoveName(int index, string name) {
         using (CaptureNonDataChange())
         if (addedNames.ContainsKey(index)) {
            addedNames.Remove(index);
         } else if (!removedNames.ContainsKey(index)) {
            removedNames[index] = name;
         }
      }

      public void AddUnmappedPointer(int index, string name) {
         using (CaptureNonDataChange())
         addedUnmappedPointers[index] = name;
      }

      public void RemoveUnmappedPointer(int index, string name) {
         using (CaptureNonDataChange())
         if (addedUnmappedPointers.ContainsKey(index)) {
            addedUnmappedPointers.Remove(index);
         } else if (!removedUnmappedPointers.ContainsKey(index)) {
            removedUnmappedPointers[index] = name;
         }
      }

      public ModelDelta Revert(IDataModel model) {
         var reverse = new ModelDelta { HasDataChange = HasDataChange };

         foreach (var kvp in oldData) {
            var (index, data) = (kvp.Key, kvp.Value);
            reverse.oldData[index] = model.Count > index ? model[index] : (byte)0xFF;
            if (model.Count > index) model[index] = data;
         }

         foreach (var kvp in addedRuns) reverse.removedRuns[kvp.Key] = kvp.Value;
         foreach (var kvp in removedRuns) reverse.addedRuns[kvp.Key] = kvp.Value;
         foreach (var kvp in addedNames) reverse.removedNames[kvp.Key] = kvp.Value;
         foreach (var kvp in removedNames) reverse.addedNames[kvp.Key] = kvp.Value;
         foreach (var kvp in addedLists) reverse.removedLists[kvp.Key] = kvp.Value;
         foreach (var kvp in removedLists) reverse.addedLists[kvp.Key] = kvp.Value;
         foreach (var kvp in addedMatchedWords) reverse.removedMatchedWords[kvp.Key] = kvp.Value;
         foreach (var kvp in removedMatchedWords) reverse.addedMatchedWords[kvp.Key] = kvp.Value;
         foreach (var kvp in addedOffsetPointers) reverse.removedOffsetPointers[kvp.Key] = kvp.Value;
         foreach (var kvp in removedOffsetPointers) reverse.addedOffsetPointers[kvp.Key] = kvp.Value;
         foreach (var kvp in addedUnmappedPointers) reverse.removedUnmappedPointers[kvp.Key] = kvp.Value;
         foreach (var kvp in removedUnmappedPointers) reverse.addedUnmappedPointers[kvp.Key] = kvp.Value;
         foreach (var kvp in addedUnmappedConstants) reverse.removedUnmappedConstants[kvp.Key] = kvp.Value;
         foreach (var kvp in removedUnmappedConstants) reverse.addedUnmappedConstants[kvp.Key] = kvp.Value;

         if (oldDataLength != -1 || newDataLength != -1) {
            model.ExpandData(reverse, oldDataLength - 1);
            model.ContractData(reverse, oldDataLength - 1);
         }

         model.MassUpdateFromDelta(addedRuns, removedRuns,
            addedNames, removedNames,
            addedUnmappedPointers, removedUnmappedPointers,
            addedMatchedWords, removedMatchedWords,
            addedOffsetPointers, removedOffsetPointers,
            addedUnmappedConstants, removedUnmappedConstants,
            addedLists, removedLists);

         return reverse;
      }

      public void AddMatchedWord(IDataModel model, int memoryLocation, string parentName, int byteCount) {
         var parentAddress = model.GetAddressFromAnchor(this, -1, parentName);
         var parent = model.GetNextRun(parentAddress) as ArrayRun;
         if (parent != null && !(this is NoDataChangeDeltaModel)) model.WriteMultiByteValue(memoryLocation, byteCount, this, parent.ElementCount);
         using (CaptureNonDataChange())
            addedMatchedWords[memoryLocation] = parentName;
      }

      public void RemoveMatchedWord(int memoryLocation, string parentName) {
         using (CaptureNonDataChange())
         if (addedMatchedWords.ContainsKey(memoryLocation)) addedMatchedWords.Remove(memoryLocation);
         else removedMatchedWords[memoryLocation] = parentName;
      }

      public void AddOffsetPointer(int start, int offset) {
         using (CaptureNonDataChange())
         addedOffsetPointers[start] = offset;
      }

      public void RemoveOffsetPointer(int start, int offset) {
         using (CaptureNonDataChange())
         if (addedOffsetPointers.ContainsKey(start)) addedMatchedWords.Remove(start);
         else removedOffsetPointers[start] = offset;
      }

      public void AddUnmappedConstant(string name, int value) {
         using (CaptureNonDataChange())
         addedUnmappedConstants[name] = value;
      }

      public void RemoveUnmappedConstant(string name, int value) {
         using (CaptureNonDataChange())
         if (addedUnmappedConstants.ContainsKey(name)) addedUnmappedConstants.Remove(name);
         else removedUnmappedConstants[name] = value;
      }

      private IDisposable CaptureNonDataChange() {
         var initialState = HasAnyChange;
         if (HasAnyChange) return new StubDisposable();
         return new StubDisposable { Dispose = () => {
            if (!HasAnyChange) return;
            OnNewChange?.Invoke(this, EventArgs.Empty);
         } };
      }
   }

   public class NoDataChangeDeltaModel : ModelDelta {
      public override bool ChangeData(IDataModel model, int index, byte data) {
         throw new InvalidOperationException("This operation is not allowed to change model data!");
      }
   }

   public class NoTrackChange : ModelDelta {
      public override bool ChangeData(IDataModel model, int index, byte data) {
         if (model.Count > index && model[index] == data) return false;
         if (model.Count <= index) model.ExpandData(this, index);
         var valueChanged = model[index] != data;
         model[index] = data;
         if (!HasDataChange) {
            HasDataChange = true;
            NotifyOnNewChange();
         }

         return valueChanged;
      }
   }
}
