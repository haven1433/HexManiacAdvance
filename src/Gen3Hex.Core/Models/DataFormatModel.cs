using HavenSoft.Gen3Hex.Core.ViewModels.DataFormats;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace HavenSoft.Gen3Hex.Core.Models {
   public interface IModel : IReadOnlyList<byte> {
      byte[] RawData { get; }
      new byte this[int index] { get; set; }

      /// <summary>
      /// If dataIndex is in the middle of a run, returns that run.
      /// If dataIndex is between runs, returns the next available run.
      /// If dataIndex is before the first run, return the first run.
      /// If dataIndex is after the last run, return null;
      /// </summary>
      IFormattedRun GetNextRun(int dataIndex);

      void ObserveRunWritten(IFormattedRun run);
      void ObserveAnchorWritten(int location, string anchorName, string anchorFormat);
      IFormattedRun RelocateForExpansion(IFormattedRun run, int minimumLength);
      void ClearFormat(int start, int length);
      string Copy(int start, int length);

      void Load(byte[] newData);
      void ExpandData(int minimumLength);

      void WritePointer(int address, int pointerDestination);
      void WriteValue(int address, int value);
      int ReadPointer(int address);
      int ReadValue(int address);

      int GetAddressFromAnchor(int requestSource, string anchor);
      string GetAnchorFromAddress(int requestSource, int destination);
   }

   public abstract class BaseModel : IModel {
      public byte[] RawData { get; private set; }

      public BaseModel(byte[] data) => RawData = data;

      public byte this[int index] { get => RawData[index]; set => RawData[index] = value; }

      byte IReadOnlyList<byte>.this[int index] => RawData[index];

      public int Count => RawData.Length;

      public abstract void ClearFormat(int start, int length);

      public abstract string Copy(int start, int length);

      public void ExpandData(int minimumIndex) {
         if (Count > minimumIndex) return;

         var newData = new byte[minimumIndex + 1];
         Array.Copy(RawData, newData, RawData.Length);
         RawData = newData;
      }

      public abstract int GetAddressFromAnchor(int requestSource, string anchor);

      public abstract string GetAnchorFromAddress(int requestSource, int destination);

      public IEnumerator<byte> GetEnumerator() => ((IList<byte>)RawData).GetEnumerator();

      public abstract IFormattedRun GetNextRun(int dataIndex);

      public virtual void Load(byte[] newData) => RawData = newData;

      public abstract void ObserveAnchorWritten(int location, string anchorName, string anchorFormat);

      public abstract void ObserveRunWritten(IFormattedRun run);

      public abstract IFormattedRun RelocateForExpansion(IFormattedRun run, int minimumLength);

      public int ReadValue(int index) {
         int word = 0;
         word |= RawData[index + 0] << 0;
         word |= RawData[index + 1] << 8;
         word |= RawData[index + 2] << 16;
         word |= RawData[index + 3] << 24;
         return word;
      }

      public void WriteValue(int index, int word) {
         RawData[index + 0] = (byte)(word >> 0);
         RawData[index + 1] = (byte)(word >> 8);
         RawData[index + 2] = (byte)(word >> 16);
         RawData[index + 3] = (byte)(word >> 24);
      }

      public int ReadPointer(int index) => ReadValue(index) - 0x08000000;

      public void WritePointer(int address, int pointerDestination) => WriteValue(address, pointerDestination + 0x08000000);

      IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
   }

   public class PointerAndStringModel : BaseModel {
      // list of runs, in sorted address order. Includes no names
      private readonly List<IFormattedRun> runs = new List<IFormattedRun>();

      // for a name, where is it?
      // for a location, what is its name?
      private readonly Dictionary<string, int> addressForAnchor = new Dictionary<string, int>();
      private readonly Dictionary<int, string> anchorForAddress = new Dictionary<int, string>();

      // for a name not actually in the file, what pointers point to it?
      // for a pointer pointing to something not actually in the file, what name is it pointing to?
      private readonly Dictionary<string, List<int>> unmappedNameToSources = new Dictionary<string, List<int>>();
      private readonly Dictionary<int, string> sourceToUnmappedName = new Dictionary<int, string>();

      public PointerAndStringModel(byte[] data) : base(data) {
         Initialize();
      }

      private void Initialize() {
         var pointersForDestination = new Dictionary<int, List<int>>();
         var destinationForSource = new SortedList<int, int>();
         SearchForPointers(pointersForDestination, destinationForSource);
         WritePointerRuns(pointersForDestination, destinationForSource);
         WriteStringRuns(pointersForDestination);
         ResolveConflicts();
      }

      private void SearchForPointers(Dictionary<int, List<int>> pointersForDestination, SortedList<int, int> destinationForSource) {
         for (int i = 0; i < RawData.Length - 3; i += 4) {
            if (RawData[i + 3] != 0x08) continue;
            if (RawData[i] % 4 != 0) continue;
            var source = i;
            var destination = ReadPointer(i);
            if (!pointersForDestination.ContainsKey(destination)) pointersForDestination[destination] = new List<int>();
            pointersForDestination[destination].Add(source);
            destinationForSource.Add(source, destination);
         }
      }

      private void WritePointerRuns(Dictionary<int, List<int>> pointersForDestination, SortedList<int, int> destinationForSource) {
         var destinations = pointersForDestination.Keys.OrderBy(i => i).GetEnumerator();
         var sources = destinationForSource.Keys.GetEnumerator();

         var moreDestinations = destinations.MoveNext();
         var moreSources = sources.MoveNext();

         while (moreDestinations && moreSources) {
            if (destinations.Current < sources.Current) {
               runs.Add(new NoInfoRun(destinations.Current, pointersForDestination[destinations.Current]));
               moreDestinations = destinations.MoveNext();
            } else if (sources.Current < destinations.Current) {
               runs.Add(new PointerRun(sources.Current));
               moreSources = sources.MoveNext();
            } else {
               runs.Add(new PointerRun(sources.Current, pointersForDestination[destinations.Current]));
               moreDestinations = destinations.MoveNext();
               moreSources = sources.MoveNext();
            }
         }

         while (moreDestinations) {
            runs.Add(new NoInfoRun(destinations.Current, pointersForDestination[destinations.Current]));
            moreDestinations = destinations.MoveNext();
         }

         while (moreSources) {
            runs.Add(new PointerRun(sources.Current));
            moreSources = sources.MoveNext();
         }
      }

      private void WriteStringRuns(Dictionary<int, List<int>> pointersForDestination) {
         var destinations = pointersForDestination.Keys.OrderBy(i => i).GetEnumerator();
         destinations.MoveNext();
         foreach (var destination in pointersForDestination.Keys.OrderBy(i => i)) {
            var length = PCSString.ReadString(RawData, destination);
            if (length < 2) continue;
            if (GetNextRun(destination + 1).Start < destination + length) continue;
            ObserveRunWritten(new PCSRun(destination, length, pointersForDestination[destination]));
         }
      }

      private void ResolveConflicts() {
         for (int i = 0; i < runs.Count - 1; i++) {
            if (runs[i].Start + runs[i].Length <= runs[i + 1].Start) continue;
            Debug.Fail("Pointers and Destinations are both 4-byte aligned, and pointers are only 4 bytes long. How the heck did I get a conflict?");
         }
      }

      public override int GetAddressFromAnchor(int requestSource, string anchor) {
         if (addressForAnchor.TryGetValue(anchor, out int address)) {
            return address;
         }

         if (requestSource < 0) return Pointer.NULL;
         if (anchor.ToLower() == "null") return Pointer.NULL;

         // the named anchor does not exist! Add it to the list of desired anchors
         if (!unmappedNameToSources.ContainsKey(anchor)) {
            unmappedNameToSources[anchor] = new List<int>();
         }
         unmappedNameToSources[anchor].Add(requestSource);
         sourceToUnmappedName[requestSource] = anchor;

         return Pointer.NULL;
      }

      public override string GetAnchorFromAddress(int requestSource, int address) {
         if (anchorForAddress.TryGetValue(address, out string anchor)) return anchor;
         if (sourceToUnmappedName.TryGetValue(requestSource, out anchor)) return anchor;
         if (address == -0x08000000) return "null";
         return string.Empty;
      }

      public override IFormattedRun GetNextRun(int dataIndex) {
         var index = BinarySearch(dataIndex);
         if (index < 0) {
            index = ~index;
            if (index > 0) {
               var previous = runs[index - 1];
               if (previous.Start + previous.Length > dataIndex) index -= 1;
            }
         }
         if (index >= runs.Count) return NoInfoRun.NullRun;
         return runs[index];
      }

      public override void ObserveRunWritten(IFormattedRun run) {
         var index = BinarySearch(run.Start);
         if (index < 0) {
            index = ~index;
            if (runs.Count == index || (runs[index].Start >= run.Start + run.Length && (index == 0 || runs[index - 1].Start + runs[index - 1].Length <= run.Start))) {
               runs.Insert(index, run);
               // if (run.Anchor.Name != string.Empty) UpdateAnchor(run.Start, run.Anchor.Name);
            } else {
               // there's a conflict: the new run was written in a space already being used, but not where another run starts
               // I'll need to do something here eventually... but for now, just error
               // the right thing to do is probably to erase the existing format in favor of the new thing the user just tried to add.
               // if the existing format was an anchor, clear all the pointers that pointed to it, since the writer is declaring that that address is not a valid anchor.
               throw new NotImplementedException();
            }
         } else {
            // replace / merge with existing
            // if the only thing changed was the anchor, then don't change the format, just merge the anchor
            var existingRun = runs[index];
            if (existingRun is PointerRun pointerRun1) {
               if (ReadValue(pointerRun1.Start) == 0) {
                  var name = sourceToUnmappedName[pointerRun1.Start];
                  sourceToUnmappedName.Remove(pointerRun1.Start);
                  unmappedNameToSources[name].Remove(pointerRun1.Start);
               }
            }

            runs[index] = run;
            run.MergeAnchor(existingRun.PointerSources);
         }

         if (run is PointerRun pointerRun) {
            if (ReadValue(pointerRun.Start) != 0) {
               var destination = ReadPointer(pointerRun.Start);
               index = BinarySearch(destination);
               if (index < 0) {
                  // the pointer is brand new
                  index = ~index;
                  runs.Insert(index, new NoInfoRun(destination, new[] { run.Start }));
               } else {
                  runs[index].MergeAnchor(new[] { run.Start });
               }
            }
         }

         if (run is NoInfoRun && run.PointerSources.Count == 0 && !anchorForAddress.ContainsKey(run.Start)) {
            // this run has no useful information. Remove it.
            runs.RemoveAt(index);
         }
      }

      public override void ObserveAnchorWritten(int location, string anchorName, string anchorFormat) {
         int index = BinarySearch(location);
         if (index < 0) ClearFormat(location, 1); // no format starts exactly at this anchor, so clear any format that goes over this anchor.

         if (anchorForAddress.TryGetValue(location, out string oldAnchorName)) {
            anchorForAddress.Remove(location);
            addressForAnchor.Remove(oldAnchorName);
         }

         if (addressForAnchor.ContainsKey(anchorName)) {
            index = BinarySearch(addressForAnchor[anchorName]);
            var oldAnchor = runs[index];
            runs.RemoveAt(index);

            foreach (var source in oldAnchor.PointerSources ?? new int[0]) {
               WriteValue(source, 0);
               sourceToUnmappedName[source] = anchorForAddress[oldAnchor.Start];
            }

            unmappedNameToSources[anchorForAddress[oldAnchor.Start]] = new List<int>(oldAnchor.PointerSources);
            addressForAnchor.Remove(anchorForAddress[oldAnchor.Start]);
            anchorForAddress.Remove(oldAnchor.Start);
         }
         if (anchorName != string.Empty) {
            anchorForAddress.Add(location, anchorName);
            addressForAnchor.Add(anchorName, location);
         }

         List<int> sources;
         if (unmappedNameToSources.TryGetValue(anchorName, out sources)) {
            foreach (var source in sources) {
               index = BinarySearch(source);
               Debug.Assert(index >= 0 && runs[index] is PointerRun);
               runs[index] = new PointerRun(source, runs[index].PointerSources);
               sourceToUnmappedName.Remove(source);
               WritePointer(source, location);
            }
            unmappedNameToSources.Remove(anchorName);
         } else {
            sources = new List<int>(); // an anchor was added: there is a list. It's just that in this case, the list is empty for now.
         }

         index = BinarySearch(location);
         IFormattedRun newRun;
         if (anchorFormat == "\"\"") {
            newRun = new PCSRun(location, PCSString.ReadString(this, location), sources);
         } else {
            newRun = new NoInfoRun(location, sources);
         }

         ObserveRunWritten(newRun);
      }

      public override IFormattedRun RelocateForExpansion(IFormattedRun run, int minimumLength) {
         if (minimumLength <= run.Length) return run;
         if (CanSafelyUse(run.Start + run.Length, run.Start + minimumLength)) return run;
         var start = 0x100;
         var runIndex = 0;
         while (start < RawData.Length - minimumLength) {
            // catch the currentRun up to where we are
            while (runIndex < runs.Count && runs[runIndex].Start < start) runIndex++;
            var currentRun = runIndex < runs.Count ? runs[runIndex] : NoInfoRun.NullRun;
            if (currentRun == run) { runIndex++; continue; } // special case: if the found run is our current run, ignore it, since it'll be moving.

            // if the space we want intersects the current run, then skip past the current run
            if (start + minimumLength > currentRun.Start) {
               start = currentRun.Start + currentRun.Length + 8;
               start -= start % 4;
               continue;
            }

            // if the space we want already has some data in it that we don't have a run for, skip it
            var firstConflictingData = Enumerable.Range(start, minimumLength).Cast<int?>().FirstOrDefault<int?>(i => RawData[(int)i] != 0xFF && RawData[(int)i] != 0x00);
            if (firstConflictingData != null) {
               start += (int)firstConflictingData + 8;
               start -= start % 4;
               continue;
            }

            // found a good spot!
            // move the run
            return MoveRun(run, start);
         }
         return null;
      }

      private IFormattedRun MoveRun(IFormattedRun run, int newStart) {
         // repoint
         foreach (var source in run.PointerSources) {
            WritePointer(source, newStart);
         }
         // move data
         for (int i = 0; i < run.Length; i++) {
            RawData[newStart + i] = RawData[run.Start + i];
            RawData[run.Start + i] = 0xFF;
         }

         if (run is PCSRun pcs) {
            var newRun = new PCSRun(newStart, run.Length, run.PointerSources);
            int index = BinarySearch(run.Start);
            runs.RemoveAt(index);
            int newIndex = BinarySearch(newStart);
            runs.Insert(~newIndex, newRun);
            return newRun;
         } else {
            throw new NotImplementedException();
         }
      }

      private bool CanSafelyUse(int rangeStart, int rangeEnd) {
         // only safe to use if there is no run in that range
         var nextRun = GetNextRun(rangeStart);
         if (nextRun.Start < rangeEnd) return false;

         // make sure the data is clear
         for (int i = rangeStart; i < rangeEnd; i++) if (RawData[i] != 0xFF && RawData[i] != 0x00) return false;

         return true;
      }

      public override void ClearFormat(int originalStart, int length) {
         int start = originalStart;
         for (var run = GetNextRun(start); length > 0 && run != null; run = GetNextRun(start)) {
            if (run.Start >= start + length) return;
            if (run is PointerRun pointerRun) {
               // remove the reference from the anchor we're pointing to as well
               var destination = ReadPointer(pointerRun.Start);
               if (destination != Pointer.NULL) {
                  var anchorRun = runs[BinarySearch(destination)];
                  anchorRun.RemoveSource(pointerRun.Start);
                  if (anchorRun.PointerSources.Count == 0) {
                     runs.RemoveAt(BinarySearch(anchorRun.Start));
                     if (anchorForAddress.ContainsKey(anchorRun.Start)) {
                        addressForAnchor.Remove(anchorForAddress[anchorRun.Start]);
                        anchorForAddress.Remove(anchorRun.Start);
                     }
                  }
               } else if (sourceToUnmappedName.TryGetValue(pointerRun.Start, out var name)) {
                  sourceToUnmappedName.Remove(pointerRun.Start);
                  unmappedNameToSources[name].Remove(pointerRun.Start);
                  if (unmappedNameToSources[name].Count == 0) unmappedNameToSources.Remove(name);
               }
            }
            if (run.Start != originalStart) {
               // delete the anchor
               foreach (var source in run.PointerSources ?? new int[0]) WriteValue(source, 0);
               if (anchorForAddress.ContainsKey(run.Start)) {
                  unmappedNameToSources[anchorForAddress[run.Start]] = new List<int>(run.PointerSources);
                  foreach (var source in run.PointerSources) sourceToUnmappedName[source] = anchorForAddress[run.Start];
                  addressForAnchor.Remove(anchorForAddress[run.Start]);
                  anchorForAddress.Remove(run.Start);
               }
               runs.RemoveAt(BinarySearch(run.Start));
            } else {
               // delete the content, but leave the anchor
               runs[BinarySearch(run.Start)] = new NoInfoRun(run.Start, run.PointerSources);
            }

            for (int i = 0; i < run.Length; i++) RawData[run.Start + i] = 0xFF;
            length -= run.Length + run.Start - start;
            start = run.Start + run.Length;
         }
      }

      public override string Copy(int start, int length) {
         var text = new StringBuilder();
         var run = GetNextRun(start);
         if (run.Start < start) {
            length += start - run.Start;
            start = run.Start;
         }

         while (length > 0) {
            run = GetNextRun(start);
            if (run.Start > start) {
               var len = Math.Min(length, run.Start - start);
               var bytes = Enumerable.Range(start, len).Select(i => RawData[i].ToString("X2"));
               text.Append(string.Join(" ", bytes) + " ");
               length -= len;
               start += len;
               continue;
            }
            if (anchorForAddress.TryGetValue(start, out string anchor)) {
               text.Append($"^{anchor}{run.FormatString} ");
            }
            if (run is PointerRun pointerRun) {
               var destination = ReadPointer(pointerRun.Start);
               var anchorName = GetAnchorFromAddress(run.Start, destination);
               if (string.IsNullOrEmpty(anchorName)) anchorName = destination.ToString("X6");
               text.Append($"<{anchorName}> ");
               start += 4;
               length -= 4;
            } else if (run is NoInfoRun noInfoRun) {
               text.Append(RawData[run.Start].ToString("X2") + " ");
               start += 1;
               length -= 1;
            } else if (run is PCSRun pcsRun) {
               text.Append(PCSString.Convert(this, run.Start, run.Length) + " ");
               start += run.Length;
               length -= run.Length;
            } else {
               throw new NotImplementedException();
            }
         }

         text.Remove(text.Length - 1, 1); // remove the trailing space
         return text.ToString();
      }

      private int BinarySearch(int start) {
         var index = runs.BinarySearch(new CompareFormattedRun(start), FormattedRunComparer.Instance);
         return index;
      }

      public override void Load(byte[] newData) {
         base.Load(newData);
         unmappedNameToSources.Clear();
         sourceToUnmappedName.Clear();
         addressForAnchor.Clear();
         anchorForAddress.Clear();
         runs.Clear();
         Initialize();
      }
   }

   public class BasicModel : BaseModel {

      public BasicModel(byte[] data) : base(data) { }

      public override int GetAddressFromAnchor(int requestSource, string anchor) => Pointer.NULL;
      public override string GetAnchorFromAddress(int requestSource, int destination) => string.Empty;
      public override IFormattedRun GetNextRun(int dataIndex) => null;
      public override void ObserveRunWritten(IFormattedRun run) { }
      public override void ObserveAnchorWritten(int location, string anchorName, string anchorFormat) { }
      public override IFormattedRun RelocateForExpansion(IFormattedRun run, int minimumLength) => throw new NotImplementedException();
      public override void ClearFormat(int start, int length) { }

      public override string Copy(int start, int length) {
         var bytes = Enumerable.Range(start, length).Select(i => RawData[i]);
         return string.Join(" ", bytes.Select(value => value.ToString("X2")));
      }
   }

   public class DeltaModel {
      private readonly Dictionary<int, byte> oldData = new Dictionary<int, byte>();
      private readonly Dictionary<int, IFormattedRun> addedRuns = new Dictionary<int, IFormattedRun>();
      private readonly Dictionary<int, IFormattedRun> removedRuns = new Dictionary<int, IFormattedRun>();

      public int EarliestChange {
         get {
            var allChanges = oldData.Keys.Concat(addedRuns.Keys).Concat(removedRuns.Keys).ToList();
            if (allChanges.Count == 0) return -1;
            return allChanges.Min();
         }
      }

      public void ChangeData(IModel model, int index, byte data) {
         if (!oldData.ContainsKey(index)) {
            if (model.Count <= index) {
               model.ExpandData(index);
            }
            oldData[index] = model[index];
         }

         model.ClearFormat(index, 1);
         model[index] = data;
      }

      public void AddRun(IModel model, int index, IFormattedRun run) {
         model.ObserveRunWritten(run);
      }

      public void RemoveRun(IModel model, int index, IFormattedRun run) {
         model.ClearFormat(run.Start, run.Length);
      }

      public DeltaModel Revert(IModel model) {
         var reverse = new DeltaModel();

         foreach (var kvp in oldData) {
            var (index, data) = (kvp.Key, kvp.Value);
            reverse.oldData[index] = model[index];
            model[index] = data;
         }

         return reverse;
      }
   }
}
