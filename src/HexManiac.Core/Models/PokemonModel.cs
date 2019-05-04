using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

      public override IReadOnlyList<ArrayRun> Arrays => runs.OfType<ArrayRun>().ToList();

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

         // metadata is more important than anything already found
         foreach (var anchor in metadata.NamedAnchors) {
            // since we're loading metadata, we're pretty sure that the anchors in the metadata are right.
            // therefore, allow those anchors to overwrite anything we found during the initial quick-search phase.
            ApplyAnchor(this, new NoDataChangeDeltaModel(), anchor.Address, AnchorStart + anchor.Name + anchor.Format, allowAnchorOverwrite: true);
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
         var noDataChange = new NoDataChangeDeltaModel();
         foreach (var destination in pointersForDestination.Keys.OrderBy(i => i)) {
            var length = PCSString.ReadString(RawData, destination, false);
            if (length < 2) continue;
            if (GetNextRun(destination + 1).Start < destination + length) continue;
            ObserveRunWritten(noDataChange, new PCSRun(destination, length, pointersForDestination[destination]));
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
         return ApplyAnchor(model, changeToken, dataIndex, text, allowAnchorOverwrite: false);
      }

      private static ErrorInfo ApplyAnchor(IDataModel model, ModelDelta changeToken, int dataIndex, string text, bool allowAnchorOverwrite) {
         var (name, format) = SplitNameAndFormat(text);

         var errorInfo = TryParseFormat(model, format, dataIndex, out var runToWrite);
         if (errorInfo.HasError) return errorInfo;

         errorInfo = ValidateAnchorNameAndFormat(model, runToWrite, name, format, dataIndex, allowAnchorOverwrite);
         if (!errorInfo.HasError) {
            errorInfo = UniquifyName(model, changeToken, dataIndex, ref name);
            model.ObserveAnchorWritten(changeToken, name, runToWrite);
         }

         return errorInfo;
      }

      private static ErrorInfo UniquifyName(IDataModel model, ModelDelta changeToken, int desiredAddressForName, ref string name) {
         var address = model.GetAddressFromAnchor(changeToken, -1, name);
         if (address == Pointer.NULL || address == desiredAddressForName) return ErrorInfo.NoError;

         var info = new ErrorInfo("Chosen name was in use. The new anchor has been renamed to avoid collisions.", isWarningLevel: true);

         // so once we've verified that the new name doesn't match the name from the current address,
         // we'll need to check again for the newly created name.
         // so do some recursion in each of these return cases.

         // Append _copy to the end to avoid the collision.
         if (!name.Contains("_copy")) {
            name += "_copy";
            UniquifyName(model, changeToken, desiredAddressForName, ref name);
            return info;
         }

         // It already had _copy on the end... fine, append the number '2'.
         var number = name.Split(new[] { "_copy" }, StringSplitOptions.None).Last();
         if (number.Length == 0) {
            name += "2";
            UniquifyName(model, changeToken, desiredAddressForName, ref name);
            return info;
         }

         // It already had a number on the end of the _copy... ok, just increment it by 1.
         if (int.TryParse(number, out var result)) {
            name += result;
            UniquifyName(model, changeToken, desiredAddressForName, ref name);
            return info;
         }

         // It wasn't a number? Eh, just throw _copy on the end again, it'll be fine.
         name += "_copy";
         UniquifyName(model, changeToken, desiredAddressForName, ref name);
         return info;
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

         var nameparts = anchor.Split('/');
         anchor = nameparts.First();

         if (addressForAnchor.TryGetValueCaseInsensitive(anchor, out int address)) {
            if (nameparts.Length > 1) address = GetAddressFromAnchor(address, nameparts);
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

      private int GetAddressFromAnchor(int startingAddress, string[] nameparts) {
         var run = GetNextRun(startingAddress);

         // only support indexing into an anchor if the anchor points to an array
         if (!(run is ArrayRun array)) return Pointer.NULL;

         // support things like items/4
         if (nameparts.Length == 2 && int.TryParse(nameparts[1], out var index)) {
            return array.Start + array.ElementLength * index;
         }

         // support things like pokestats/BULBASAUR
         if (nameparts.Length == 2) {
            var elementName = nameparts[1].ToLower();
            for (int i = 0; i < array.ElementNames.Count; i++) {
               if (array.ElementNames[i].ToLower() == elementName) return array.Start + array.ElementLength * i;
            }
         }

         // not supported
         return Pointer.NULL;
      }

      public override string GetAnchorFromAddress(int requestSource, int address) {
         // option 1: a known name exists for this address
         if (anchorForAddress.TryGetValue(address, out string anchor)) return anchor;

         // option 2: a known name exists for this source, but the name doesn't actually exist in the file
         if (sourceToUnmappedName.TryGetValue(requestSource, out anchor)) return anchor;

         // option 3: pointing to nothing
         if (address == -0x08000000) return "null";

         // option 4: pointing within an array that supports inner element anchors
         var containingRun = GetNextRun(address);
         if (containingRun.Start < address && containingRun is ArrayRun array) {
            var arrayName = GetAnchorFromAddress(-1, array.Start);
            var arrayIndex = (address - array.Start) / array.ElementLength;
            var indexMod = (address - array.Start) % array.ElementLength;
            if (indexMod == 0) return $"{arrayName}{ArrayAnchorSeparator}{arrayIndex}";
         }

         return string.Empty;
      }

      public override IFormattedRun GetNextRun(int dataIndex) {
         if (dataIndex == Pointer.NULL) return NoInfoRun.NullRun;
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

      public override bool TryGetUsefulHeader(int address, out string header) {
         header = null;
         // only produce headers for arrays with length based on other arrays that start with a text member.
         var run = GetNextRun(address);
         if (run.Start > address) return false;
         if (!(run is ArrayRun array)) return false;
         if ((address - array.Start) % array.ElementLength != 0) return false;

         var index = (address - array.Start) / array.ElementLength;
         if (array.ElementNames.Count == 0) return false;
         header = array.ElementNames[index];

         return true;
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
         if (run is ArrayRun arrayRun) {
            ModifyAnchorsFromPointerArray(changeToken, arrayRun, AddPointerToAnchor);
            UpdateDependantArrayLengths(changeToken, arrayRun);
         }

         if (run is NoInfoRun && run.PointerSources.Count == 0 && !anchorForAddress.ContainsKey(run.Start)) {
            // this run has no useful information. Remove it.
            changeToken.RemoveRun(runs[index]);
            runs.RemoveAt(index);
         }
      }

      /// <summary>
      /// A new array just came in. It might have pointers.
      /// When we make a new pointer, we need to update anchors to include the new pointer.
      /// So update all the anchors based on any new pointers in this newly added array.
      /// </summary>
      private void ModifyAnchorsFromPointerArray(ModelDelta changeToken, ArrayRun arrayRun, Action<ModelDelta, int> changeAnchors) {
         int segmentOffset = arrayRun.Start;
         for (int i = 0; i < arrayRun.ElementContent.Count; i++) {
            if (arrayRun.ElementContent[i].Type != ElementContentType.Pointer) { segmentOffset += arrayRun.ElementContent[i].Length; continue; }
            for (int j = 0; j < arrayRun.ElementCount; j++) {
               var start = segmentOffset + arrayRun.ElementLength * j;
               changeAnchors(changeToken, start);
            }
            segmentOffset += arrayRun.ElementContent[i].Length;
         }
      }

      /// <summary>
      /// This new array may have other arrays who's length depend on it.
      /// Update those arrays based on this new length.
      /// (Recursively, since other arrays might depend on those ones).
      /// </summary>
      private void UpdateDependantArrayLengths(ModelDelta changeToken, ArrayRun arrayRun) {
         foreach (var table in this.GetDependantArrays(arrayRun)) {
            if (arrayRun.ElementCount == table.ElementCount) continue;
            var newTable = table.Append(arrayRun.ElementCount - table.ElementCount);
            ObserveRunWritten(changeToken, newTable);
         }
      }

      private void AddPointerToAnchor(ModelDelta changeToken, int start) {
         var destination = ReadPointer(start);
         if (destination < 0 || destination >= Count) return;
         int index = BinarySearch(destination);
         if (index < 0 && ~index > 0 && runs[~index - 1] is ArrayRun array && array.SupportsPointersToElements && (destination - array.Start) % array.ElementLength == 0) {
            // the pointer points into an array that supports inner anchors
            index = ~index - 1;
            changeToken.RemoveRun(array);
            runs[index] = array.AddSourcePointingWithinArray(start);
            changeToken.AddRun(runs[index]);
         } else if (index < 0) {
            // the pointer is brand new
            index = ~index;
            var newRun = new NoInfoRun(destination, new[] { start });
            runs.Insert(index, newRun);
            changeToken.AddRun(newRun);
         } else {
            // the pointer points to a known normal anchor
            var existingRun = runs[index];
            changeToken.RemoveRun(existingRun);
            runs[index] = existingRun.MergeAnchor(new[] { start });
            changeToken.AddRun(runs[index]);
         }
      }

      public override void ObserveAnchorWritten(ModelDelta changeToken, string anchorName, IFormattedRun run) {
         int location = run.Start;
         int index = BinarySearch(location);

         var existingRun = (index >= 0 && index < runs.Count) ? runs[index] : null;

         if (existingRun == null) {
            // no format starts exactly at this anchor, so clear any format that goes over this anchor.
            ClearFormat(changeToken, location, run.Length);
         } else if (!(run is NoInfoRun)) {
            // a format starts exactly at this anchor.
            // but the new format may extend further. If so, clear the existing format.
            if (existingRun.Length < run.Length) {
               ClearFormat(changeToken, run.Start, run.Length);
            }
         }

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

         var seekPointers = existingRun?.PointerSources == null || existingRun?.Start != location;
         var sources = GetSourcesPointingToNewAnchor(changeToken, anchorName, seekPointers);

         // if we're adding an array, update inner pointers and dependent arrays
         if (run is ArrayRun array && array.SupportsPointersToElements) run = array.AddSourcesPointingWithinArray(changeToken);

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

      // for each of the results, we recognized it as text: see if we need to add a matching string run / pointers
      public override int ConsiderResultsAsTextRuns(ModelDelta currentChange, IReadOnlyList<int> searchResults) {
         int resultsRecognizedAsTextRuns = 0;
         var parallelLock = new object();
         Parallel.ForEach(searchResults, result => {
            var run = ConsiderAddressAsText(this, result, currentChange);
            if (run != null) {
               lock (parallelLock) {
                  ObserveAnchorWritten(currentChange, string.Empty, run);
                  resultsRecognizedAsTextRuns++;
               }
            }
         });

         return resultsRecognizedAsTextRuns;
      }

      public static PCSRun ConsiderAddressAsText(IDataModel model, int address, ModelDelta currentChange) {
         var nextRun = model.GetNextRun(address);
         if (nextRun.Start < address) return null;
         if (nextRun.Start == address && !(nextRun is NoInfoRun)) return null;
         var pointers = model.SearchForPointersToAnchor(currentChange, address);
         if (pointers.Count == 0) return null;
         var length = PCSString.ReadString(model, address, true);
         if (length < 1) return null;
         if (address + length > nextRun.Start && nextRun.Start != address) return null;
         return new PCSRun(address, length, pointers);
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

            ClearAnchorFormat(changeToken, originalStart, run);

            if (alsoClearData) {
               for (int i = 0; i < run.Length; i++) changeToken.ChangeData(this, run.Start + i, 0xFF);
            }

            length -= run.Length + run.Start - start;
            start = run.Start + run.Length;
         }
      }

      private void ClearAnchorFormat(ModelDelta changeToken, int originalStart, IFormattedRun run) {
         int runIndex;

         // case 1: anchor is named
         // delete the anchor. Clear pointers to it, but keep the names. They're pointers, just not to here anymore.
         if (anchorForAddress.TryGetValue(run.Start, out string name)) {
            foreach (var source in run.PointerSources) {
               WriteValue(changeToken, source, 0);
               changeToken.AddUnmappedPointer(source, name);
               sourceToUnmappedName[source] = name;
            }
            unmappedNameToSources[name] = new List<int>(run.PointerSources);
            changeToken.RemoveName(run.Start, name);
            addressForAnchor.Remove(name);
            anchorForAddress.Remove(run.Start);
            runIndex = BinarySearch(run.Start);
            changeToken.RemoveRun(run);
            runs.RemoveAt(runIndex);
            return;
         }

         // case 2: unnamed anchor doesn't start where the delete starts
         // this anchor shouldn't exist. The things that point to it aren't real pointers.
         if (run.Start != originalStart) {
            // by removing the unnamed anchor here, we're claiming that these were never really pointers to begin with.
            // as such, we should not change their data, just remove their pointer format
            foreach (var source in run.PointerSources ?? new int[0]) {
               var sourceRunIndex = BinarySearch(source);
               if (sourceRunIndex >= 0) {
                  changeToken.RemoveRun(runs[sourceRunIndex]);
                  runs.RemoveAt(sourceRunIndex);
               }
            }
            runIndex = BinarySearch(run.Start);
            changeToken.RemoveRun(run);
            runs.RemoveAt(runIndex);
            return;
         }

         // case 3: unnamed anchor starts where the delete starts.
         // delete the content, but leave the anchor and pointers to it: we don't want to lose the pointers that point here.
         runIndex = BinarySearch(run.Start);
         changeToken.RemoveRun(run);
         if (run.PointerSources != null) {
            runs[runIndex] = new NoInfoRun(run.Start, run.PointerSources);
            changeToken.AddRun(runs[runIndex]);
         } else {
            runs.RemoveAt(runIndex);
         }
      }

      private void ClearPointerFormat(ModelDelta changeToken, int start) {
         // remove the reference from the anchor we're pointing to as well
         var destination = ReadPointer(start);
         if (destination >= 0 && destination < Count) {
            var index = BinarySearch(destination);
            if (index >= 0) {
               ClearPointerFromAnchor(changeToken, start, index);
            } else if (runs[~index - 1] is ArrayRun array) {
               ClearPointerWithinArray(changeToken, start, ~index - 1, array);
            } else {
               throw new NotImplementedException();
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

      private void ClearPointerFromAnchor(ModelDelta changeToken, int start, int index) {
         var anchorRun = runs[index];
         var newAnchorRun = anchorRun.RemoveSource(start);
         changeToken.RemoveRun(anchorRun);
         if (newAnchorRun.PointerSources.Count == 0 && !anchorForAddress.ContainsKey(newAnchorRun.Start)) {
            runs.RemoveAt(index);
         } else {
            runs[index] = newAnchorRun;
            changeToken.AddRun(newAnchorRun);
         }
      }

      private void ClearPointerWithinArray(ModelDelta changeToken, int start, int index, ArrayRun array) {
         changeToken.RemoveRun(array);
         var newArray = array.RemoveSource(start);
         runs[index] = newArray;
         changeToken.AddRun(newArray);
      }

      public override void UpdateArrayPointer(ModelDelta changeToken, int source, int destination) {
         ClearPointerFormat(changeToken, source);
         WritePointer(changeToken, source, destination);
         AddPointerToAnchor(changeToken, source);
      }

      public override string Copy(Func<ModelDelta> changeToken, int start, int length) {
         var text = new StringBuilder();
         var run = GetNextRun(start);
         if (run.Start < start && !(run is ArrayRun)) {
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
            if (run.Start == start) {
               if (!anchorForAddress.TryGetValue(start, out string anchor)) {
                  if ((run.PointerSources?.Count ?? 0) > 0) {
                     anchor = GenerateDefaultAnchorName(run);
                     ObserveAnchorWritten(changeToken(), anchor, run);
                     text.Append($"^{anchor}{run.FormatString} ");
                  }
               } else {
                  text.Append($"^{anchor}{run.FormatString} ");
               }
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
               arrayRun.AppendTo(this, text, start, length);
               text.Append(" ");
               length -= run.Start + run.Length - start;
               start = run.Start + run.Length;
            } else {
               throw new NotImplementedException();
            }
         }

         text.Remove(text.Length - 1, 1); // remove the trailing space
         return text.ToString();
      }

      private string GenerateDefaultAnchorName(IFormattedRun run) {
         var gameCodeText = ReadGameCode();
         var textSample = GetSampleText(run);
         var initialAddress = run.Start.ToString("X6");

         return textSample + gameCodeText + initialAddress;
      }

      /// <summary>
      /// If this model recognizes a GameCode AsciiRun, return that code formatted as a name.
      /// </summary>
      private string ReadGameCode() {
         if (!addressForAnchor.TryGetValue("GameCode", out int gameCodeAddress)) return string.Empty;
         var gameCode = GetNextRun(gameCodeAddress) as AsciiRun;
         if (gameCode == null || gameCode.Start != gameCodeAddress) return string.Empty;
         return new string(Enumerable.Range(0, gameCode.Length).Select(i => (char)this[gameCode.Start + i]).ToArray());
      }

      /// <summary>
      /// If the run is text, grab the first 3 words and return it formatted as a name.
      /// </summary>
      private string GetSampleText(IFormattedRun run) {
         if (!(run is PCSRun)) return string.Empty;
         var text = PCSString.Convert(this, run.Start, run.Length);
         var words = text.Split(' ');
         if (words.Length > 3) words = words.Take(3).ToArray();
         text = string.Concat(words);
         return new string(text.Where(char.IsLetterOrDigit).ToArray());
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
            var address = addressForAnchor[name];
            var run = GetNextRun(address) as ArrayRun;
            if (run == null || !partial.Contains(ArrayAnchorSeparator)) {
               if (name.MatchesPartial(partial)) results.Add(name);
            } else {
               var childNames = run.ElementNames;
               if (childNames != null && childNames.Count > 0) {
                  foreach (var childName in childNames) {
                     var full = $"{name}{ArrayAnchorSeparator}{childName}";
                     if (full.MatchesPartial(partial)) results.Add(full);
                  }
               }
            }
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

      /// <summary>
      /// This method might be called in parallel with the same changeToken
      /// </summary>
      public override IReadOnlyList<int> SearchForPointersToAnchor(ModelDelta changeToken, params int[] addresses) {
         var results = new List<int>();

         var chunkLength = 0x10000;
         var groups = (int)Math.Ceiling((double)RawData.Length / chunkLength);
         Parallel.For(0, groups, group => {
            var data = RawData;
            var chunkEnd = chunkLength * (group + 1);
            chunkEnd = Math.Min(chunkEnd, data.Length);
            for (int i = chunkLength * group + 3; i < chunkEnd; i++) {
               if (data[i] != 0x08 && data[i] != 0x09) continue;
               var destination = ReadPointer(i - 3);
               if (!addresses.Contains(destination)) continue;
               if (IsValidResult(changeToken, i)) {
                  lock (results) results.Add(i - 3);
               }
            }
         });

         return results;
      }

      private bool IsValidResult(ModelDelta changeToken, int i) {
         // I have to lock this whole block, because I need to know that 'index' remains consistent until I can call runs.Insert
         lock (runs) {
            var index = BinarySearch(i - 3);
            if (index >= 0) {
               if (runs[index] is PointerRun) return true;
               if (runs[index] is ArrayRun arrayRun && arrayRun.ElementContent[0].Type == ElementContentType.Pointer) return true;
               if (runs[index] is NoInfoRun) {
                  var pointerRun = new PointerRun(i - 3, runs[index].PointerSources);
                  changeToken.RemoveRun(runs[index]);
                  changeToken.AddRun(pointerRun);
                  runs[index] = pointerRun;
                  return true;
               }
               return false;
            }
            index = ~index;
            if (index < runs.Count && runs[index].Start <= i) return false; // can't add a pointer run if an existing run starts during the new one

            // can't add a pointer run if the new one starts during an existing one
            if (index > 0 && runs[index - 1].Start + runs[index - 1].Length > i - 3) {
               // ah, but if that run is an array and there's already a pointer here...
               var array = runs[index - 1] as ArrayRun;
               if (array != null) {
                  var offsets = array.ConvertByteOffsetToArrayOffset(i);
                  if (array.ElementContent[offsets.SegmentIndex].Type == ElementContentType.Pointer) {
                     return true;
                  }
               }
               return false;
            }
            var newRun = new PointerRun(i - 3);
            runs.Insert(index, newRun);
            changeToken.AddRun(newRun);
         }

         return true;
      }

      private static (string, string) SplitNameAndFormat(string text) {
         var name = text.Substring(1).Trim(); // lop off leading ^
         string format = string.Empty;
         int split = -1;

         if (name.Contains(AnchorStart.ToString() + ArrayStart)) {
            split = name.IndexOf(AnchorStart);
         } else if (name.Contains(ArrayStart)) {
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

      private static ErrorInfo ValidateAnchorNameAndFormat(IDataModel model, IFormattedRun runToWrite, string name, string format, int dataIndex, bool allowAnchorOverwrite = false) {
         var existingRun = model.GetNextRun(dataIndex);
         var nextAnchor = model.GetNextAnchor(dataIndex + 1);

         if (name.ToLower() == "null") {
            return new ErrorInfo("'null' is a reserved word and cannot be used as an anchor name.");
         } else if (name == string.Empty && existingRun.Start != dataIndex) {
            // if there isn't already a run here, then clearly there's nothing pointing here
            return new ErrorInfo("An anchor with nothing pointing to it must have a name.");
         } else if (name == string.Empty && existingRun.PointerSources.Count == 0 && format != string.Empty) {
            // the next run DOES start here, but nothing points to it
            return new ErrorInfo("An anchor with nothing pointing to it must have a name.");
         } else if (!allowAnchorOverwrite && nextAnchor.Start < runToWrite.Start + runToWrite.Length) {
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
