using HavenSoft.Gen3Hex.Core.ViewModels.DataFormats;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using static HavenSoft.Gen3Hex.Core.Models.BaseRun;
using static HavenSoft.Gen3Hex.Core.Models.ArrayRun;
using static HavenSoft.Gen3Hex.Core.Models.PCSRun;

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

      void ObserveRunWritten(DeltaModel changeToken, IFormattedRun run);
      void ObserveAnchorWritten(DeltaModel changeToken, string anchorName, IFormattedRun run);
      void MassUpdateFromDelta(IReadOnlyDictionary<int, IFormattedRun> runsToRemove, IReadOnlyDictionary<int, IFormattedRun> runsToAdd, IReadOnlyDictionary<int, string> namesToRemove, IReadOnlyDictionary<int, string> namesToAdd, IReadOnlyDictionary<int, string> unmappedPointersToRemove, IReadOnlyDictionary<int, string> unmappedPointersToAdd);
      IFormattedRun RelocateForExpansion(DeltaModel changeToken, IFormattedRun run, int minimumLength);
      void ClearFormat(DeltaModel changeToken, int start, int length);
      string Copy(int start, int length);

      void Load(byte[] newData, StoredMetadata metadata);
      void ExpandData(DeltaModel changeToken, int minimumLength);

      void WritePointer(DeltaModel changeToken, int address, int pointerDestination);
      void WriteValue(DeltaModel changeToken, int address, int value);
      int ReadPointer(int address);
      int ReadValue(int address);

      int GetAddressFromAnchor(DeltaModel changeToken, int requestSource, string anchor);
      string GetAnchorFromAddress(int requestSource, int destination);
      StoredMetadata ExportMetadata();
   }

   public abstract class BaseModel : IModel {
      public byte[] RawData { get; private set; }

      public BaseModel(byte[] data) => RawData = data;

      public byte this[int index] { get => RawData[index]; set => RawData[index] = value; }

      byte IReadOnlyList<byte>.this[int index] => RawData[index];

      public int Count => RawData.Length;

      public abstract void ClearFormat(DeltaModel changeToken, int start, int length);

      public abstract string Copy(int start, int length);

      public void ExpandData(DeltaModel changeToken, int minimumIndex) {
         if (Count > minimumIndex) return;

         var newData = new byte[minimumIndex + 1];
         Array.Copy(RawData, newData, RawData.Length);
         RawData = newData;
      }

      public abstract int GetAddressFromAnchor(DeltaModel changeToken, int requestSource, string anchor);

      public abstract string GetAnchorFromAddress(int requestSource, int destination);

      public IEnumerator<byte> GetEnumerator() => ((IList<byte>)RawData).GetEnumerator();

      public abstract IFormattedRun GetNextRun(int dataIndex);

      public virtual void Load(byte[] newData, StoredMetadata metadata) => RawData = newData;

      public abstract void ObserveAnchorWritten(DeltaModel changeToken, string anchorName, IFormattedRun run);

      public abstract void ObserveRunWritten(DeltaModel changeToken, IFormattedRun run);

      public abstract void MassUpdateFromDelta(IReadOnlyDictionary<int, IFormattedRun> runsToRemove, IReadOnlyDictionary<int, IFormattedRun> runsToAdd, IReadOnlyDictionary<int, string> namesToRemove, IReadOnlyDictionary<int, string> namesToAdd, IReadOnlyDictionary<int, string> unmappedPointersToRemove, IReadOnlyDictionary<int, string> unmappedPointersToAdd);

      public abstract IFormattedRun RelocateForExpansion(DeltaModel changeToken, IFormattedRun run, int minimumLength);

      public int ReadValue(int index) {
         int word = 0;
         word |= RawData[index + 0] << 0;
         word |= RawData[index + 1] << 8;
         word |= RawData[index + 2] << 16;
         word |= RawData[index + 3] << 24;
         return word;
      }

      public void WriteValue(DeltaModel changeToken, int index, int word) {
         changeToken.ChangeData(this, index + 0, (byte)(word >> 0));
         changeToken.ChangeData(this, index + 1, (byte)(word >> 8));
         changeToken.ChangeData(this, index + 2, (byte)(word >> 16));
         changeToken.ChangeData(this, index + 3, (byte)(word >> 24));
      }

      public int ReadPointer(int index) => ReadValue(index) - 0x08000000;

      public void WritePointer(DeltaModel changeToken, int address, int pointerDestination) => WriteValue(changeToken, address, pointerDestination + 0x08000000);

      public virtual StoredMetadata ExportMetadata() => null;

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

      #region Constructor

      public PointerAndStringModel(byte[] data, StoredMetadata metadata = null) : base(data) {
         Initialize(metadata);
      }

      private void Initialize(StoredMetadata metadata) {
         var pointersForDestination = new Dictionary<int, List<int>>();
         var destinationForSource = new SortedList<int, int>();
         SearchForPointers(pointersForDestination, destinationForSource);
         WritePointerRuns(pointersForDestination, destinationForSource);
         WriteStringRuns(pointersForDestination);
         ResolveConflicts();

         if (metadata == null) return;

         foreach (var anchor in metadata.NamedAnchors) {
            ApplyAnchor(this, new DeltaModel(), anchor.Address, AnchorStart + anchor.Name + anchor.Format);
         }
         foreach (var unmappedPointer in metadata.UnmappedPointers) {
            sourceToUnmappedName[unmappedPointer.Address] = unmappedPointer.Name;
            if (!unmappedNameToSources.ContainsKey(unmappedPointer.Name)) unmappedNameToSources[unmappedPointer.Name] = new List<int>();
            unmappedNameToSources[unmappedPointer.Name].Add(unmappedPointer.Address);
         }
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
            var length = PCSString.ReadString(RawData, destination, false);
            if (length < 2) continue;
            if (GetNextRun(destination + 1).Start < destination + length) continue;
            ObserveRunWritten(new DeltaModel(), new PCSRun(destination, length, pointersForDestination[destination]));
         }
      }

      private void ResolveConflicts() {
         for (int i = 0; i < runs.Count - 1; i++) {
            if (runs[i].Start + runs[i].Length <= runs[i + 1].Start) continue;
            Debug.Fail("Pointers and Destinations are both 4-byte aligned, and pointers are only 4 bytes long. How the heck did I get a conflict?");
         }
      }

      #endregion

      public static ErrorInfo ApplyAnchor(IModel model, DeltaModel changeToken, int dataIndex, string text) {
         var name = text.Substring(1).Trim();
         string format = string.Empty;

         if (name.Contains(ArrayStart)) {
            var split = name.IndexOf(ArrayStart);
            format = name.Substring(split);
            name = name.Substring(0, split);
         } else if (name.Contains(StringDelimeter)) {
            var split = name.IndexOf(StringDelimeter);
            format = name.Substring(split);
            name = name.Substring(0, split);
         }

         IFormattedRun runToWrite = new NoInfoRun(dataIndex);

         if (format == StringDelimeter.ToString() + StringDelimeter) {
            var length = PCSString.ReadString(model, dataIndex, true);
            if (length < 0) {
               return new ErrorInfo($"Format was specified as a string, but no string was recognized.");
            } else if (SpanContainsAnchor(model, dataIndex, length)) {
               return new ErrorInfo($"Format was specified as a string, but a string would overlap the next anchor.");
            }
            runToWrite = new PCSRun(dataIndex, length);
         } else if (ArrayRun.TryParse(model, format, dataIndex, null, out var arrayRun)) {
            runToWrite = arrayRun;
         } else if (format != string.Empty) {
            return new ErrorInfo($"Format {format} was not understood.");
         }

         var nextRun = model.GetNextRun(dataIndex);

         if (name.ToLower() == "null") {
            return new ErrorInfo("'null' is a reserved word and cannot be used as an anchor name.");
         } else if (name == string.Empty && nextRun.Start != dataIndex) {
            return new ErrorInfo("An anchor with nothing pointing to it must have a name.");
         } else if (name == string.Empty && nextRun.PointerSources.Count == 0 && format != string.Empty) {
            return new ErrorInfo("An anchor with nothing pointing to it must have a name.");
         } else {
            model.ObserveAnchorWritten(changeToken, name, runToWrite);
            return ErrorInfo.NoError;
         }
      }

      public static bool SpanContainsAnchor(IModel model, int start, int length) {
         var run = model.GetNextRun(start + 1);

         // if we're starting in the middle of a run, get the next one
         if (run.Start <= start) {
            length -= run.Length + run.Start - start;
            start = run.Start + run.Length;
            run = model.GetNextRun(start);
         }

         // move start forward to the start of the run
         length -= run.Start - start;
         start = run.Start;

         // check all the runs in the range for pointer sources / destination names
         while (length > 0) {
            if (run.PointerSources.Count > 0) return true;
            if (!string.IsNullOrEmpty(model.GetAnchorFromAddress(-1, run.Start))) return true;
            run = model.GetNextRun(run.Start + run.Length);
            length -= run.Start - start;
            start = run.Start;
         }

         return false;
      }

      public override int GetAddressFromAnchor(DeltaModel changeToken, int requestSource, string anchor) {
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
         changeToken.AddUnmappedPointer(requestSource, anchor);

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

      public override void ObserveRunWritten(DeltaModel changeToken, IFormattedRun run) {
         var index = BinarySearch(run.Start);
         if (index < 0) {
            index = ~index;
            if (runs.Count == index || (runs[index].Start >= run.Start + run.Length && (index == 0 || runs[index - 1].Start + runs[index - 1].Length <= run.Start))) {
               runs.Insert(index, run);
               changeToken.AddRun(run);
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

            changeToken.RemoveRun(existingRun);
            run = run.MergeAnchor(existingRun.PointerSources);
            runs[index] = run;
            changeToken.AddRun(run);
         }

         if (run is PointerRun pointerRun) {
            if (ReadValue(pointerRun.Start) != 0) {
               var destination = ReadPointer(pointerRun.Start);
               index = BinarySearch(destination);
               if (index < 0) {
                  // the pointer is brand new
                  index = ~index;
                  var newRun = new NoInfoRun(destination, new[] { run.Start });
                  runs.Insert(index, newRun);
                  changeToken.AddRun(newRun);
               } else {
                  changeToken.RemoveRun(runs[index]);
                  runs[index] = runs[index].MergeAnchor(new[] { run.Start });
                  changeToken.AddRun(runs[index]);
               }
            }
         }

         if (run is NoInfoRun && run.PointerSources.Count == 0 && !anchorForAddress.ContainsKey(run.Start)) {
            // this run has no useful information. Remove it.
            changeToken.RemoveRun(runs[index]);
            runs.RemoveAt(index);
         }
      }

      public override void ObserveAnchorWritten(DeltaModel changeToken, string anchorName, IFormattedRun run) {
         int location = run.Start;
         int index = BinarySearch(location);
         if (index < 0) ClearFormat(changeToken, location, 1); // no format starts exactly at this anchor, so clear any format that goes over this anchor.

         if (anchorForAddress.TryGetValue(location, out string oldAnchorName)) {
            anchorForAddress.Remove(location);
            addressForAnchor.Remove(oldAnchorName);
            changeToken.RemoveName(location, oldAnchorName);
         }

         if (addressForAnchor.ContainsKey(anchorName)) {
            RemoveAnchorByName(changeToken, anchorName);
         }

         // if this anchor was given a name, add it
         if (anchorName != string.Empty) {
            anchorForAddress.Add(location, anchorName);
            addressForAnchor.Add(anchorName, location);
            changeToken.AddName(location, anchorName);
         }

         var sources = GetSourcesPointingToNewAnchor(changeToken, anchorName);

         index = BinarySearch(location);
         var newRun = run.MergeAnchor(sources);
         ObserveRunWritten(changeToken, newRun);
      }

      public override void MassUpdateFromDelta(IReadOnlyDictionary<int, IFormattedRun> runsToRemove, IReadOnlyDictionary<int, IFormattedRun> runsToAdd, IReadOnlyDictionary<int, string> namesToRemove, IReadOnlyDictionary<int, string> namesToAdd, IReadOnlyDictionary<int, string> unmappedPointersToRemove, IReadOnlyDictionary<int, string> unmappedPointersToAdd) {
         foreach (var kvp in namesToRemove) {
            var (address, name) = (kvp.Key, kvp.Value);
            addressForAnchor.Remove(name);
            anchorForAddress.Remove(address);
         }

         foreach (var kvp in namesToAdd) {
            var (address, name) = (kvp.Key, kvp.Value);
            addressForAnchor[name] = address;
            anchorForAddress[address] = name;
         }

         foreach (var kvp in unmappedPointersToRemove) {
            var (address, name) = (kvp.Key, kvp.Value);
            unmappedNameToSources[name].Remove(address);
            if (unmappedNameToSources[name].Count == 0) unmappedNameToSources.Remove(name);
            sourceToUnmappedName.Remove(address);
         }

         foreach (var kvp in unmappedPointersToAdd) {
            var (address, name) = (kvp.Key, kvp.Value);
            if (!unmappedNameToSources.ContainsKey(name)) unmappedNameToSources[name] = new List<int>();
            unmappedNameToSources[name].Add(address);
            sourceToUnmappedName[address] = name;
         }

         foreach (var kvp in runsToRemove) {
            var index = BinarySearch(kvp.Key);
            if (index >= 0) runs.RemoveAt(index);
         }

         foreach (var kvp in runsToAdd) {
            var index = BinarySearch(kvp.Key);
            if (index >= 0) {
               runs[index] = kvp.Value;
            } else {
               index = ~index;
               if (index < runs.Count) {
                  runs.Insert(index, kvp.Value);
               } else {
                  runs.Add(kvp.Value);
               }
            }
         }
      }

      public override IFormattedRun RelocateForExpansion(DeltaModel changeToken, IFormattedRun run, int minimumLength) {
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
            var newRun = MoveRun(changeToken, run, start);
            if (anchorForAddress.TryGetValue(run.Start, out var name)) {
               addressForAnchor[name] = newRun.Start;
               anchorForAddress.Remove(run.Start);
               anchorForAddress[newRun.Start] = name;
               changeToken.RemoveName(run.Start, name);
               changeToken.AddName(newRun.Start, name);
            }
            return newRun;
         }
         return null;
      }

      public override void ClearFormat(DeltaModel changeToken, int originalStart, int length) {
         int start = originalStart;
         for (var run = GetNextRun(start); length > 0 && run != null; run = GetNextRun(start)) {
            if (run.Start >= start + length) return;
            if (run is PointerRun pointerRun) {
               ClearPointerFormat(changeToken, pointerRun);
            }
            ClearAnchorFormat(changeToken, originalStart, run);

            for (int i = 0; i < run.Length; i++) changeToken.ChangeData(this, run.Start + i, 0xFF);
            length -= run.Length + run.Start - start;
            start = run.Start + run.Length;
         }
      }

      private void ClearAnchorFormat(DeltaModel changeToken, int originalStart, IFormattedRun run) {
         if (run.Start != originalStart) {
            // delete the anchor
            foreach (var source in run.PointerSources ?? new int[0]) WriteValue(changeToken, source, 0);
            if (anchorForAddress.TryGetValue(run.Start, out string name)) {
               unmappedNameToSources[anchorForAddress[run.Start]] = new List<int>(run.PointerSources);
               foreach (var source in run.PointerSources) {
                  changeToken.AddUnmappedPointer(source, name);
                  sourceToUnmappedName[source] = name;
               }
               changeToken.RemoveName(run.Start, name);
               addressForAnchor.Remove(name);
               anchorForAddress.Remove(run.Start);
            }
            var index = BinarySearch(run.Start);
            changeToken.RemoveRun(run);
            runs.RemoveAt(index);
         } else {
            // delete the content, but leave the anchor
            var index = BinarySearch(run.Start);
            changeToken.RemoveRun(run);
            if (run.PointerSources != null) {
               runs[index] = new NoInfoRun(run.Start, run.PointerSources);
               changeToken.AddRun(runs[index]);
            } else {
               runs.RemoveAt(index);
            }
         }
      }

      private void ClearPointerFormat(DeltaModel changeToken, PointerRun pointerRun) {
         // remove the reference from the anchor we're pointing to as well
         var destination = ReadPointer(pointerRun.Start);
         if (destination != Pointer.NULL && destination >= 0 && destination < Count) {
            var index = BinarySearch(destination);
            var anchorRun = runs[index];
            var newAnchorRun = anchorRun.RemoveSource(pointerRun.Start);
            changeToken.RemoveRun(anchorRun);
            if (newAnchorRun.PointerSources.Count == 0) {
               var anchorIndex = BinarySearch(anchorRun.Start);
               runs.RemoveAt(anchorIndex);
               if (anchorForAddress.ContainsKey(anchorRun.Start)) {
                  changeToken.RemoveName(anchorRun.Start, anchorForAddress[anchorRun.Start]);
                  addressForAnchor.Remove(anchorForAddress[anchorRun.Start]);
                  anchorForAddress.Remove(anchorRun.Start);
               }
            } else {
               runs[index] = newAnchorRun;
               changeToken.AddRun(newAnchorRun);
            }
         } else if (sourceToUnmappedName.TryGetValue(pointerRun.Start, out var name)) {
            changeToken.RemoveUnmappedPointer(pointerRun.Start, name);
            sourceToUnmappedName.Remove(pointerRun.Start);
            if (unmappedNameToSources[name].Count == 1) {
               unmappedNameToSources.Remove(name);
            } else {
               unmappedNameToSources[name].Remove(pointerRun.Start);
            }
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

      public override void Load(byte[] newData, StoredMetadata metadata) {
         base.Load(newData, metadata);
         unmappedNameToSources.Clear();
         sourceToUnmappedName.Clear();
         addressForAnchor.Clear();
         anchorForAddress.Clear();
         runs.Clear();
         Initialize(metadata);
      }

      public override StoredMetadata ExportMetadata() {
         var anchors = new List<StoredAnchor>();
         foreach (var kvp in anchorForAddress) {
            var (address, name) = (kvp.Key, kvp.Value);
            var format = runs[BinarySearch(address)].FormatString;
            anchors.Add(new StoredAnchor(address, name, format));
         }

         var unmappedPointers = new List<StoredUnmappedPointers>();
         foreach (var kvp in sourceToUnmappedName) {
            var (address, name) = (kvp.Key, kvp.Value);
            unmappedPointers.Add(new StoredUnmappedPointers(address, name));
         }

         return new StoredMetadata(anchors, unmappedPointers);
      }

      private void RemoveAnchorByName(DeltaModel changeToken, string anchorName) {
         var index = BinarySearch(addressForAnchor[anchorName]);
         var oldAnchor = runs[index];
         changeToken.RemoveRun(oldAnchor);
         runs.RemoveAt(index);
         var oldAnchorName = anchorForAddress[oldAnchor.Start];

         foreach (var source in oldAnchor.PointerSources ?? new int[0]) {
            WriteValue(changeToken, source, 0);
            sourceToUnmappedName[source] = oldAnchorName;
            changeToken.AddUnmappedPointer(source, oldAnchorName);
         }

         unmappedNameToSources[oldAnchorName] = new List<int>(oldAnchor.PointerSources);
         var nameToRemove = anchorForAddress[oldAnchor.Start];
         addressForAnchor.Remove(nameToRemove);
         anchorForAddress.Remove(oldAnchor.Start);
         changeToken.RemoveName(oldAnchor.Start, nameToRemove);
      }

      /// <summary>
      /// if there are unmapped sources trying to point to this name, point them at the new anchor
      /// </summary>
      /// <returns>
      /// The list of sources that point at the new anchor
      /// </returns>
      private IReadOnlyList<int> GetSourcesPointingToNewAnchor(DeltaModel changeToken, string anchorName) {
         if (!addressForAnchor.TryGetValue(anchorName, out int location)) return new List<int>();     // new anchor is unnamed, so nothing points to it yet
         if (!unmappedNameToSources.TryGetValue(anchorName, out var sources)) return new List<int>(); // no pointer was waiting for this anchor to be created

         foreach (var source in sources) {
            var index = BinarySearch(source);
            Debug.Assert(index >= 0 && runs[index] is PointerRun);
            runs[index] = new PointerRun(source, runs[index].PointerSources);
            changeToken.RemoveUnmappedPointer(source, anchorName);
            sourceToUnmappedName.Remove(source);
            WritePointer(changeToken, source, location);
         }
         unmappedNameToSources.Remove(anchorName);

         return sources;
      }

      private IFormattedRun MoveRun(DeltaModel changeToken, IFormattedRun run, int newStart) {
         // repoint
         foreach (var source in run.PointerSources) {
            WritePointer(changeToken, source, newStart);
         }
         // move data
         for (int i = 0; i < run.Length; i++) {
            changeToken.ChangeData(this, newStart + i, RawData[run.Start + i]);
            changeToken.ChangeData(this, run.Start + i, 0xFF);
         }

         if (run is PCSRun pcs) {
            var newRun = new PCSRun(newStart, run.Length, run.PointerSources);
            int index = BinarySearch(run.Start);
            changeToken.RemoveRun(runs[index]);
            runs.RemoveAt(index);
            int newIndex = BinarySearch(newStart);
            runs.Insert(~newIndex, newRun);
            changeToken.AddRun(newRun);
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

      private int BinarySearch(int start) {
         var index = runs.BinarySearch(new CompareFormattedRun(start), FormattedRunComparer.Instance);
         return index;
      }
   }

   public class BasicModel : BaseModel {

      public BasicModel(byte[] data) : base(data) { }

      public override int GetAddressFromAnchor(DeltaModel changeToken, int requestSource, string anchor) => Pointer.NULL;
      public override string GetAnchorFromAddress(int requestSource, int destination) => string.Empty;
      public override IFormattedRun GetNextRun(int dataIndex) => NoInfoRun.NullRun;
      public override void ObserveRunWritten(DeltaModel changeToken, IFormattedRun run) { }
      public override void ObserveAnchorWritten(DeltaModel changeToken, string anchorName, IFormattedRun run) { }
      public override void MassUpdateFromDelta(IReadOnlyDictionary<int, IFormattedRun> runsToRemove, IReadOnlyDictionary<int, IFormattedRun> runsToAdd, IReadOnlyDictionary<int, string> namesToRemove, IReadOnlyDictionary<int, string> namesToAdd, IReadOnlyDictionary<int, string> unmappedPointersToRemove, IReadOnlyDictionary<int, string> unmappedPointersToAdd) { }
      public override IFormattedRun RelocateForExpansion(DeltaModel changeToken, IFormattedRun run, int minimumLength) => throw new NotImplementedException();
      public override void ClearFormat(DeltaModel changeToken, int start, int length) { }

      public override string Copy(int start, int length) {
         var bytes = Enumerable.Range(start, length).Select(i => RawData[i]);
         return string.Join(" ", bytes.Select(value => value.ToString("X2")));
      }
   }

   public class DeltaModel {
      private readonly Dictionary<int, byte> oldData = new Dictionary<int, byte>();

      private readonly Dictionary<int, IFormattedRun> addedRuns = new Dictionary<int, IFormattedRun>();
      private readonly Dictionary<int, IFormattedRun> removedRuns = new Dictionary<int, IFormattedRun>();

      private readonly Dictionary<int, string> addedNames = new Dictionary<int, string>();
      private readonly Dictionary<int, string> removedNames = new Dictionary<int, string>();

      private readonly Dictionary<int, string> addedUnmappedPointers = new Dictionary<int, string>();
      private readonly Dictionary<int, string> removedUnmappedPointers = new Dictionary<int, string>();

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
               model.ExpandData(this, index);
            }
            oldData[index] = model[index];
         }

         model[index] = data;
      }

      public void AddRun(IFormattedRun run) {
         addedRuns[run.Start] = run;
      }

      public void RemoveRun(IFormattedRun run) {
         if (addedRuns.ContainsKey(run.Start)) {
            addedRuns.Remove(run.Start);
         } else if (!removedRuns.ContainsKey(run.Start)) {
            removedRuns[run.Start] = run;
         }
      }

      public void AddName(int index, string name) {
         addedNames[index] = name;
      }

      public void RemoveName(int index, string name) {
         if (addedNames.ContainsKey(index)) {
            addedNames.Remove(index);
         } else if (!removedNames.ContainsKey(index)) {
            removedNames[index] = name;
         }
      }

      public void AddUnmappedPointer(int index, string name) {
         addedUnmappedPointers[index] = name;
      }

      public void RemoveUnmappedPointer(int index, string name) {
         if (addedUnmappedPointers.ContainsKey(index)) {
            addedUnmappedPointers.Remove(index);
         } else if (!removedUnmappedPointers.ContainsKey(index)) {
            removedUnmappedPointers[index] = name;
         }
      }

      public DeltaModel Revert(IModel model) {
         var reverse = new DeltaModel();

         foreach (var kvp in oldData) {
            var (index, data) = (kvp.Key, kvp.Value);
            reverse.oldData[index] = model[index];
            model[index] = data;
         }

         foreach (var kvp in addedRuns) reverse.removedRuns[kvp.Key] = kvp.Value;
         foreach (var kvp in removedRuns) reverse.addedRuns[kvp.Key] = kvp.Value;
         foreach (var kvp in addedNames) reverse.removedNames[kvp.Key] = kvp.Value;
         foreach (var kvp in removedNames) reverse.addedNames[kvp.Key] = kvp.Value;
         foreach (var kvp in addedUnmappedPointers) reverse.removedUnmappedPointers[kvp.Key] = kvp.Value;
         foreach (var kvp in removedUnmappedPointers) reverse.addedUnmappedPointers[kvp.Key] = kvp.Value;

         model.MassUpdateFromDelta(addedRuns, removedRuns, addedNames, removedNames, addedUnmappedPointers, removedUnmappedPointers);

         return reverse;
      }
   }
}
