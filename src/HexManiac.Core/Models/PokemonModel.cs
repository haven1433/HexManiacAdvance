using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using static HavenSoft.HexManiac.Core.Models.Runs.ArrayRun;
using static HavenSoft.HexManiac.Core.Models.Runs.AsciiRun;
using static HavenSoft.HexManiac.Core.Models.Runs.BaseRun;
using static HavenSoft.HexManiac.Core.Models.Runs.PCSRun;

namespace HavenSoft.HexManiac.Core.Models {
   public class PokemonModel : BaseModel {
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

      public virtual int EarliestAllowedAnchor => 0;

      #region Constructor

      public PokemonModel(byte[] data, StoredMetadata metadata = null) : base(data) {
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
            ApplyAnchor(this, new ModelDelta(), anchor.Address, AnchorStart + anchor.Name + anchor.Format);
         }
         foreach (var unmappedPointer in metadata.UnmappedPointers) {
            sourceToUnmappedName[unmappedPointer.Address] = unmappedPointer.Name;
            if (!unmappedNameToSources.ContainsKey(unmappedPointer.Name)) unmappedNameToSources[unmappedPointer.Name] = new List<int>();
            unmappedNameToSources[unmappedPointer.Name].Add(unmappedPointer.Address);
         }
      }

      /// <summary>
      /// Finds pointers based on Heuristics.
      /// This is definitely wrong, but it's pretty good.
      /// </summary>
      private void SearchForPointers(Dictionary<int, List<int>> pointersForDestination, SortedList<int, int> destinationForSource) {
         // pointers must be 4-byte aligned
         for (int i = 0; i < RawData.Length - 3; i += 4) {

            // pointers must end in 08 or 09
            if (RawData[i + 3] != 0x08 && RawData[i + 3] != 0x09) continue;

            // pointers must point to locations that are 4-byte aligned
            if (RawData[i] % 4 != 0) continue;
            var source = i;
            var destination = ReadPointer(i);

            // pointers must point into the data
            if (destination >= RawData.Length) continue;

            // pointers must not point at the header
            if (destination < EarliestAllowedAnchor) continue;

            // pointers must point at something useful, not just a bunch of FF
            bool pointsToManyFF = true;
            for (int j = 0; j < 4 && pointsToManyFF && destination + j < RawData.Length; j++) pointsToManyFF = RawData[destination + j] == 0xFF;
            if (pointsToManyFF) continue;

            // we found a pointer!
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
            ObserveRunWritten(new ModelDelta(), new PCSRun(destination, length, pointersForDestination[destination]));
         }
      }

      private void ResolveConflicts() {
         for (int i = 0; i < runs.Count - 1; i++) {
            if (runs[i].Start + runs[i].Length <= runs[i + 1].Start) continue;
            Debug.Fail("Pointers and Destinations are both 4-byte aligned, and pointers are only 4 bytes long. How the heck did I get a conflict?");
         }
      }

      #endregion

      public static ErrorInfo ApplyAnchor(IDataModel model, ModelDelta changeToken, int dataIndex, string text) {
         var (name, format) = SplitNameAndFormat(text);

         var errorInfo = TryParseFormat(model, format, dataIndex, out var runToWrite);
         if (errorInfo.HasError) return errorInfo;

         errorInfo = ValidateAnchorNameAndFormat(model, runToWrite, name, format, dataIndex);
         if (!errorInfo.HasError) model.ObserveAnchorWritten(changeToken, name, runToWrite);

         return errorInfo;
      }

      public static bool SpanContainsAnchor(IDataModel model, int start, int length) {
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

      public override int GetAddressFromAnchor(ModelDelta changeToken, int requestSource, string anchor) {

         if (addressForAnchor.TryGetValueCaseInsensitive(anchor, out int address)) {
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

      public override IFormattedRun GetNextAnchor(int dataIndex) {
         var index = BinarySearch(dataIndex);
         if (index < 0) index = ~index;
         for (; index < runs.Count; index++) {
            if (runs[index].Start < dataIndex) continue;
            if (runs[index].PointerSources == null) continue;
            return runs[index];
         }
         return NoInfoRun.NullRun;
      }

      public override bool IsAtEndOfArray(int dataIndex, out ArrayRun arrayRun) {
         var index = BinarySearch(dataIndex);
         if (index >= 0 && runs[index].Length == 0) {
            arrayRun = runs[index] as ArrayRun;
            return arrayRun != null;
         }

         if (index < 0) index = ~index;
         index -= 1;

         if (index < 0) {
            arrayRun = null;
            return false;
         }

         arrayRun = runs[index] as ArrayRun;
         return arrayRun != null && runs[index].Start + runs[index].Length == dataIndex;
      }

      public override void ObserveRunWritten(ModelDelta changeToken, IFormattedRun run) {
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
            changeToken.RemoveRun(existingRun);
            run = run.MergeAnchor(existingRun.PointerSources);
            runs[index] = run;
            changeToken.AddRun(run);
         }

         if (run is PointerRun) AddPointerToAnchor(changeToken, run.Start);
         if (run is ArrayRun arrayRun) ModifyAnchorsFromPointerArray(changeToken, arrayRun, AddPointerToAnchor);

         if (run is NoInfoRun && run.PointerSources.Count == 0 && !anchorForAddress.ContainsKey(run.Start)) {
            // this run has no useful information. Remove it.
            changeToken.RemoveRun(runs[index]);
            runs.RemoveAt(index);
         }
      }

      private void ModifyAnchorsFromPointerArray(ModelDelta changeToken, ArrayRun arrayRun, Action<ModelDelta, int> changeAchors) {
         int segmentOffset = arrayRun.Start;
         for (int i = 0; i < arrayRun.ElementContent.Count; i++) {
            if (arrayRun.ElementContent[i].Type != ElementContentType.Pointer) { segmentOffset += arrayRun.ElementContent[i].Length; continue; }
            for (int j = 0; j < arrayRun.ElementCount; j++) {
               var start = segmentOffset + arrayRun.ElementLength * j;
               changeAchors(changeToken, start);
            }
            segmentOffset += arrayRun.ElementContent[i].Length;
         }
      }

      private void AddPointerToAnchor(ModelDelta changeToken, int start) {
         var destination = ReadPointer(start);
         if (destination < 0 || destination >= Count) return;
         int index = BinarySearch(destination);
         if (index < 0) {
            // the pointer is brand new
            index = ~index;
            var newRun = new NoInfoRun(destination, new[] { start });
            runs.Insert(index, newRun);
            changeToken.AddRun(newRun);
         } else {
            var existingRun = runs[index];
            changeToken.RemoveRun(existingRun);
            runs[index] = existingRun.MergeAnchor(new[] { start });
            changeToken.AddRun(runs[index]);
         }
      }

      public override void ObserveAnchorWritten(ModelDelta changeToken, string anchorName, IFormattedRun run) {
         int location = run.Start;
         int index = BinarySearch(location);
         if (index < 0) {
            // no format starts exactly at this anchor, so clear any format that goes over this anchor.
            ClearFormat(changeToken, location, run.Length);
         } else if (!(run is NoInfoRun)) {
            // a format starts exactly at this anchor, but this new format may extend further. Clear everything but the anchor.
            ClearFormat(changeToken, run.Start, run.Length);
         }

         var existingRun = (index >= 0 && index < runs.Count) ? runs[index] : null;

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

         var seakPointers = existingRun?.PointerSources == null || existingRun?.Start != location;
         var sources = GetSourcesPointingToNewAnchor(changeToken, anchorName, seakPointers);
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

      public override IFormattedRun RelocateForExpansion(ModelDelta changeToken, IFormattedRun run, int minimumLength) {
         const int SpacerLength = 0x10;
         if (minimumLength <= run.Length) return run;
         if (CanSafelyUse(run.Start + run.Length, run.Start + minimumLength)) return run;
         minimumLength += 0x100; // make sure there's plenty of room after, so that we're not in the middle of some other data set
         var start = 0x100;
         var runIndex = 0;
         while (start < RawData.Length - minimumLength) {
            // catch the currentRun up to where we are
            while (runIndex < runs.Count && runs[runIndex].Start < start) runIndex++;
            var currentRun = runIndex < runs.Count ? runs[runIndex] : NoInfoRun.NullRun;
            if (currentRun == run) { runIndex++; continue; } // special case: if the found run is our current run, ignore it, since it'll be moving.

            // if the space we want intersects the current run, then skip past the current run
            if (start + minimumLength > currentRun.Start) {
               start = currentRun.Start + currentRun.Length + SpacerLength;
               start -= start % 4;
               continue;
            }

            // if the space we want already has some data in it that we don't have a run for, skip it
            var lastConflictingData = -1;
            for (int i = start; i < start + minimumLength; i++) if (RawData[i] != 0xFF) lastConflictingData = i;
            if (lastConflictingData != -1) {
               start = (int)lastConflictingData + SpacerLength;
               start -= start % 4;
               continue;
            }

            // found a good spot!
            // move the run
            return MoveRun(changeToken, run, start);
         }

         ExpandData(changeToken, RawData.Length + minimumLength);
         return MoveRun(changeToken, run, RawData.Length - minimumLength - 1);
      }

      public override void ClearFormat(ModelDelta changeToken, int originalStart, int length) {
         ClearFormat(changeToken, originalStart, length, alsoClearData: false);
      }

      public override void ClearFormatAndData(ModelDelta changeToken, int originalStart, int length) {
         ClearFormat(changeToken, originalStart, length, alsoClearData: true);
      }

      private void ClearFormat(ModelDelta changeToken, int originalStart, int length, bool alsoClearData) {
         int start = originalStart;
         for (var run = GetNextRun(start); length > 0 && run != null; run = GetNextRun(start)) {

            if (alsoClearData && start < run.Start) {
               for (int i = 0; i < length && i < run.Start - start; i++) changeToken.ChangeData(this, start + i, 0xFF);
            }

            if (run.Start >= start + length) return;
            if (run is PointerRun) ClearPointerFormat(changeToken, run.Start);
            if (run is ArrayRun arrayRun) ModifyAnchorsFromPointerArray(changeToken, arrayRun, ClearPointerFormat);

            ClearAnchorFormat(changeToken, originalStart, run, alsoClearData);

            if (alsoClearData) {
               for (int i = 0; i < run.Length; i++) changeToken.ChangeData(this, run.Start + i, 0xFF);
            }

            length -= run.Length + run.Start - start;
            start = run.Start + run.Length;
         }
      }

      private void ClearAnchorFormat(ModelDelta changeToken, int originalStart, IFormattedRun run, bool alsoClearData) {
         if (run.Start != originalStart) {
            // delete the anchor
            if (anchorForAddress.TryGetValue(run.Start, out string name)) {
               if (alsoClearData) {
                  foreach (var source in run.PointerSources ?? new int[0]) WriteValue(changeToken, source, 0);
               }
               unmappedNameToSources[anchorForAddress[run.Start]] = new List<int>(run.PointerSources);
               foreach (var source in run.PointerSources) {
                  changeToken.AddUnmappedPointer(source, name);
                  sourceToUnmappedName[source] = name;
               }
               changeToken.RemoveName(run.Start, name);
               addressForAnchor.Remove(name);
               anchorForAddress.Remove(run.Start);
            } else {
               foreach (var source in run.PointerSources ?? new int[0]) {
                  var sourceRunIndex = BinarySearch(source);
                  if (sourceRunIndex >= 0) {
                     changeToken.RemoveRun(runs[sourceRunIndex]);
                     runs.RemoveAt(sourceRunIndex);
                  }
               }
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

      private void ClearPointerFormat(ModelDelta changeToken, int start) {
         // remove the reference from the anchor we're pointing to as well
         var destination = ReadPointer(start);
         if (destination >= 0 && destination < Count) {
            var index = BinarySearch(destination);
            var anchorRun = runs[index];
            var newAnchorRun = anchorRun.RemoveSource(start);
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
         } else if (sourceToUnmappedName.TryGetValue(start, out var name)) {
            changeToken.RemoveUnmappedPointer(start, name);
            sourceToUnmappedName.Remove(start);
            if (unmappedNameToSources[name].Count == 1) {
               unmappedNameToSources.Remove(name);
            } else {
               unmappedNameToSources[name].Remove(start);
            }
         }
      }

      public override void UpdateArrayPointer(ModelDelta changeToken, int source, int destination) {
         ClearPointerFormat(changeToken, source);
         WritePointer(changeToken, source, destination);
         AddPointerToAnchor(changeToken, source);
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
            } else if (run is ArrayRun arrayRun) {
               arrayRun.AppendTo(this, text);
               text.Append(" ");
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

      public override IReadOnlyList<string> GetAutoCompleteAnchorNameOptions(string partial) {
         partial = partial.ToLower();
         var mappedNames = addressForAnchor.Keys;
         var results = new List<string>();
         foreach (var name in mappedNames) {
            var unmatchedName = name;
            int index = -1;
            foreach (var character in partial) {
               index = unmatchedName.IndexOf(character.ToString(), StringComparison.CurrentCultureIgnoreCase);
               if (index == -1) break;
               unmatchedName = unmatchedName.Substring(index);
            }
            if (index == -1) continue;
            results.Add(name);
         }

         return results;
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

      public override IReadOnlyList<int> SearchForPointersToAnchor(ModelDelta changeToken, int address) {
         var results = new List<int>();

         for (int i = 3; i < RawData.Length; i++) {
            if (RawData[i] != 0x08 && RawData[i] != 0x09) continue;
            int destination = ReadPointer(i - 3);
            if (destination != address) continue;
            var index = BinarySearch(i - 3);
            if (index >= 0) continue;
            index = ~index;
            if (index < runs.Count && runs[index].Start <= i) continue;
            if (index > 0 && runs[index - 1].Start + runs[index - 1].Length > i - 3) continue;
            var newRun = new PointerRun(i - 3);
            runs.Insert(index, newRun);
            changeToken.AddRun(newRun);
            results.Add(i - 3);
         }

         return results;
      }

      private static (string, string) SplitNameAndFormat(string text) {
         var name = text.Substring(1).Trim();
         string format = string.Empty;
         int split = -1;

         if (name.Contains(ArrayStart)) {
            split = name.IndexOf(ArrayStart);
         } else if (name.Contains(StringDelimeter)) {
            split = name.IndexOf(StringDelimeter);
         } else if (name.Contains(StreamDelimeter)) {
            split = name.IndexOf(StreamDelimeter);
         }

         if (split != -1) {
            format = name.Substring(split);
            name = name.Substring(0, split);
         }

         return (name, format);
      }

      private static ErrorInfo TryParseFormat(IDataModel model, string format, int dataIndex, out IFormattedRun run) {
         run = new NoInfoRun(dataIndex);

         if (format == StringDelimeter.ToString() + StringDelimeter) {
            var length = PCSString.ReadString(model, dataIndex, true);
            if (length < 0) {
               return new ErrorInfo($"Format was specified as a string, but no string was recognized.");
            } else if (SpanContainsAnchor(model, dataIndex, length)) {
               return new ErrorInfo($"Format was specified as a string, but a string would overlap the next anchor.");
            }
            run = new PCSRun(dataIndex, length);
         } else if (format.StartsWith(StreamDelimeter + "asc" + StreamDelimeter)) {
            if (int.TryParse(format.Substring(5), out var length)) {
               run = new AsciiRun(dataIndex, length);
            } else {
               return new ErrorInfo($"Ascii runs must include a length.");
            }
         } else {
            var errorInfo = TryParse(model, format, dataIndex, null, out var arrayRun);
            if (errorInfo == ErrorInfo.NoError) {
               run = arrayRun;
            } else if (format != string.Empty) {
               return new ErrorInfo($"Format {format} was not understood.");
            }
         }

         return ErrorInfo.NoError;
      }

      private static ErrorInfo ValidateAnchorNameAndFormat(IDataModel model, IFormattedRun runToWrite, string name, string format, int dataIndex) {
         var existingRun = model.GetNextRun(dataIndex);
         var nextAnchor = model.GetNextAnchor(dataIndex + 1); // existingRun.Start > dataIndex ? existingRun : model.GetNextRun(existingRun.Start + Math.Max(existingRun.Length, 1));

         if (name.ToLower() == "null") {
            return new ErrorInfo("'null' is a reserved word and cannot be used as an anchor name.");
         } else if (name == string.Empty && existingRun.Start != dataIndex) {
            // if there isn't already a run here, then clearly there's nothing pointing here
            return new ErrorInfo("An anchor with nothing pointing to it must have a name.");
         } else if (name == string.Empty && existingRun.PointerSources.Count == 0 && format != string.Empty) {
            // the next run DOES start here, but nothing points to it
            return new ErrorInfo("An anchor with nothing pointing to it must have a name.");
         } else if (nextAnchor.Start < runToWrite.Start + runToWrite.Length) {
            return new ErrorInfo("An existing anchor starts before the new one ends.");
         } else {
            return ErrorInfo.NoError;
         }
      }

      private void RemoveAnchorByName(ModelDelta changeToken, string anchorName) {
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
      private IReadOnlyList<int> GetSourcesPointingToNewAnchor(ModelDelta changeToken, string anchorName, bool seakPointers) {
         if (!addressForAnchor.TryGetValue(anchorName, out int location)) return new List<int>();     // new anchor is unnamed, so nothing points to it yet

         if (!unmappedNameToSources.TryGetValue(anchorName, out var sources)) {
            // no pointer was waiting for this anchor to be created
            // but the user thinks there's something pointing here
            if (seakPointers) return SearchForPointersToAnchor(changeToken, location);
            return new List<int>();
         }

         foreach (var source in sources) {
            var index = BinarySearch(source);
            if (index >= 0 && runs[index] is ArrayRun array1) {
               Debug.Assert(array1.ElementContent[0].Type == ElementContentType.Pointer);
            } else if (index < 0 && runs[~index - 1] is ArrayRun array2) {
               var offsets = array2.ConvertByteOffsetToArrayOffset(source);
               Debug.Assert(array2.ElementContent[offsets.SegmentIndex].Type == ElementContentType.Pointer);
            } else {
               Debug.Assert(index >= 0 && runs[index] is PointerRun);
            }
            changeToken.RemoveUnmappedPointer(source, anchorName);
            sourceToUnmappedName.Remove(source);
            WritePointer(changeToken, source, location);
         }
         unmappedNameToSources.Remove(anchorName);

         return sources;
      }

      private IFormattedRun MoveRun(ModelDelta changeToken, IFormattedRun run, int newStart) {
         // repoint
         foreach (var source in run.PointerSources) {
            WritePointer(changeToken, source, newStart);
         }

         // move data
         for (int i = 0; i < run.Length; i++) {
            changeToken.ChangeData(this, newStart + i, RawData[run.Start + i]);
            changeToken.ChangeData(this, run.Start + i, 0xFF);
         }

         // move run
         IFormattedRun newRun;
         if (run is PCSRun pcs) {
            newRun = new PCSRun(newStart, run.Length, run.PointerSources);
         } else if (run is ArrayRun array) {
            newRun = array.Move(newStart);
         } else {
            throw new NotImplementedException();
         }

         int index = BinarySearch(run.Start);
         changeToken.RemoveRun(runs[index]);
         runs.RemoveAt(index);
         int newIndex = BinarySearch(newStart);
         runs.Insert(~newIndex, newRun);
         changeToken.AddRun(newRun);

         // move anchor
         if (anchorForAddress.TryGetValue(run.Start, out var name)) {
            addressForAnchor[name] = newRun.Start;
            anchorForAddress.Remove(run.Start);
            anchorForAddress[newRun.Start] = name;
            changeToken.RemoveName(run.Start, name);
            changeToken.AddName(newRun.Start, name);
         }

         return newRun;
      }

      private bool CanSafelyUse(int rangeStart, int rangeEnd) {
         // only safe to use if there is no run in that range
         var nextRun = GetNextRun(rangeStart);

         // ignore a runs of length zero that begin at the requested rangeStart
         // because space after a run of length zero is obviously safe to use when extending that run.
         // in this case, we actually care about accidentally butting up against the _next_ run.
         if (nextRun.Start == rangeStart && nextRun.Length == 0) nextRun = GetNextRun(rangeStart + 1);

         if (nextRun.Start < rangeEnd) return false;
         if (rangeEnd >= RawData.Length) return false;

         // make sure the data is clear
         for (int i = rangeStart; i < rangeEnd; i++) if (RawData[i] != 0xFF && RawData[i] != 0x00) return false;

         return true;
      }

      // if an existing run starts exactly at start, return that index
      // otherwise, return a number such that ~index would be inserted into the list at the correct index
      // so ~index - 1 is the previous run, and ~index is the next run
      private int BinarySearch(int start) {
         var index = runs.BinarySearch(new CompareFormattedRun(start), FormattedRunComparer.Instance);
         return index;
      }
   }

   public static class StringDictionaryExtensions {
      public static bool TryGetValueCaseInsensitive<T>(this IDictionary<string, T> self, string key, out T value) {
         foreach (var option in self.Keys) {
            if (key.Equals(option, StringComparison.CurrentCultureIgnoreCase)) {
               value = self[option];
               return true;
            }
         }

         value = default(T);
         return false;
      }
   }
}
