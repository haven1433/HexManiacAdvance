using HavenSoft.Gen3Hex.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace HavenSoft.Gen3Hex.Core.Models {
   public interface IModel {
      /// <summary>
      /// If dataIndex is in the middle of a run, returns that run.
      /// If dataIndex is between runs, returns the next available run.
      /// If dataIndex is before the first run, return the first run.
      /// If dataIndex is after the last run, return null;
      /// </summary>
      IFormattedRun GetNextRun(int dataIndex);

      void ObserveRunWritten(byte[] data, IFormattedRun run);
      void ObserveAnchorWritten(byte[] data, int location, string anchorName, string anchorFormat);
      void ClearFormat(byte[] data, int start, int length);

      int GetAddressFromAnchor(int requestSource, string anchor);
      string GetAnchorFromAddress(int requestSource, int destination);
   }

   public class PointerModel : IModel {
      // list of runs, in sorted address order. Includes no names
      private readonly List<IFormattedRun> runs;

      // for a name, where is it?
      // for a location, what is its name?
      private readonly Dictionary<string, int> addressForAnchor = new Dictionary<string, int>();
      private readonly Dictionary<int, string> anchorForAddress = new Dictionary<int, string>();

      // for a name not actually in the file, what pointers point to it?
      // for a pointer pointing to something not actually in the file, what name is it pointing to?
      private readonly Dictionary<string, List<int>> unmappedNameToSources = new Dictionary<string, List<int>>();
      private readonly Dictionary<int, string> sourceToUnmappedName = new Dictionary<int, string>();

      public PointerModel(byte[] data) {
         var pointersForDestination = new Dictionary<int, List<int>>();
         var destinationForSource = new SortedList<int, int>();
         runs = new List<IFormattedRun>();

         SearchForPointers(data, pointersForDestination, destinationForSource);
         WriteRuns(data, pointersForDestination, destinationForSource);
         ResolveConflicts();
      }

      private void SearchForPointers(byte[] data, Dictionary<int, List<int>> pointersForDestination, SortedList<int, int> destinationForSource) {
         for (int i = 0; i < data.Length - 3; i += 4) {
            if (data[i + 3] != 0x08) continue;
            if (data[i] % 4 != 0) continue;
            var source = i;
            var destination = data.ReadAddress(i);
            if (!pointersForDestination.ContainsKey(destination)) pointersForDestination[destination] = new List<int>();
            pointersForDestination[destination].Add(source);
            destinationForSource.Add(source, destination);
         }
      }

      private void WriteRuns(byte[] data, Dictionary<int, List<int>> pointersForDestination, SortedList<int, int> destinationForSource) {
         var destinations = pointersForDestination.Keys.OrderBy(i => i).GetEnumerator();
         var sources = destinationForSource.Keys.GetEnumerator();

         var moreDestinations = destinations.MoveNext();
         var moreSources = sources.MoveNext();

         while (moreDestinations && moreSources) {
            if (destinations.Current < sources.Current) {
               runs.Add(new NoInfoRun(destinations.Current, new Anchor(sources: pointersForDestination[destinations.Current])));
               moreDestinations = destinations.MoveNext();
            } else if (sources.Current < destinations.Current) {
               runs.Add(new PointerRun(this, sources.Current));
               moreSources = sources.MoveNext();
            } else {
               runs.Add(new PointerRun(this, sources.Current, new Anchor(sources: pointersForDestination[destinations.Current])));
               moreDestinations = destinations.MoveNext();
               moreSources = sources.MoveNext();
            }
         }

         while (moreDestinations) {
            runs.Add(new NoInfoRun(destinations.Current, new Anchor(sources: pointersForDestination[destinations.Current])));
            moreDestinations = destinations.MoveNext();
         }

         while (moreSources) {
            runs.Add(new PointerRun(this, sources.Current));
            moreSources = sources.MoveNext();
         }
      }

      private void ResolveConflicts() {
         for (int i = 0; i < runs.Count - 1; i++) {
            if (runs[i].Start + runs[i].Length <= runs[i + 1].Start) continue;
            Debug.Fail("Pointers and Destinations are both 4-byte aligned, and pointers are only 4 bytes long. How the heck did I get a conflict?");
         }
      }

      public int GetAddressFromAnchor(int requestSource, string anchor) {
         if (addressForAnchor.TryGetValue(anchor, out int address)) {
            return address;
         }

         // the named anchor does not exist! Add it to the list of desired anchors
         if (!unmappedNameToSources.ContainsKey(anchor)) {
            unmappedNameToSources[anchor] = new List<int>();
         }
         unmappedNameToSources[anchor].Add(requestSource);
         sourceToUnmappedName[requestSource] = anchor;

         return Pointer.NULL;
      }

      public string GetAnchorFromAddress(int requestSource, int address) {
         if (anchorForAddress.TryGetValue(address, out string anchor)) return anchor;
         if (sourceToUnmappedName.TryGetValue(requestSource, out anchor)) return anchor;
         return string.Empty;
      }

      public IFormattedRun GetNextRun(int dataIndex) {
         var index = BinarySearch(dataIndex);
         if (index < 0) {
            index = ~index;
            if (index > 0) {
               var previous = runs[index - 1];
               if (previous.Start + previous.Length > dataIndex) index -= 1;
            }
         }
         if (index >= runs.Count) return null;
         return runs[index];
      }

      public void ObserveRunWritten(byte[] data, IFormattedRun run) {
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
               if (data.ReadWord(pointerRun1.Start) == 0) {
                  var name = sourceToUnmappedName[pointerRun1.Start];
                  sourceToUnmappedName.Remove(pointerRun1.Start);
                  unmappedNameToSources[name].Remove(pointerRun1.Start);
               }
            }

            runs[index] = run;
            run.MergeAnchor(existingRun.Anchor);
         }

         if (run is PointerRun pointerRun) {
            if (data.ReadWord(pointerRun.Start) != 0) {
               var destination = data.ReadAddress(pointerRun.Start);
               index = BinarySearch(destination);
               if (index < 0) {
                  // the pointer is brand new
                  index = ~index;
                  runs.Insert(index, new NoInfoRun(destination, new Anchor(new[] { run.Start })));
               } else {
                  runs[index].MergeAnchor(new Anchor(new[] { run.Start }));
               }
            }
         }
      }

      public void ObserveAnchorWritten(byte[] data, int location, string anchorName, string anchorFormat) {
         int index = BinarySearch(location);
         if (index < 0) ClearFormat(data, location, 1); // no format starts exactly at this anchor, so clear any format that goes over this anchor.

         if (anchorForAddress.TryGetValue(location, out string oldAnchorName)) {
            anchorForAddress.Remove(location);
            addressForAnchor.Remove(oldAnchorName);
         }

         if (addressForAnchor.ContainsKey(anchorName)) {
            // TODO the desired name already exists
            // (1) remove old name and delete data
            // anything pointing to the old should now be pointing to the new
            throw new NotImplementedException();
         } else if (anchorName != string.Empty) {
            anchorForAddress.Add(location, anchorName);
            addressForAnchor.Add(anchorName, location);
         }

         List<int> sources = null;
         if (unmappedNameToSources.TryGetValue(anchorName, out sources)) {
            foreach (var source in sources) {
               index = BinarySearch(source);
               Debug.Assert(index >= 0 && runs[index] is PointerRun);
               runs[index] = new PointerRun(this, source, runs[index].Anchor);
               sourceToUnmappedName.Remove(source);
               data.WritePointer(source, location);
            }
            unmappedNameToSources.Remove(anchorName);
         }

         index = BinarySearch(location);
         if (index < 0) {
            runs.Insert(~index, new NoInfoRun(location, new Anchor(sources)));
         } else {
            runs[index].MergeAnchor(new Anchor(sources));
         }
      }

      // TODO observe removal of runs (pointer replaced with normal data)
      public void ObserveNormalDataWrite(byte[] data, int index) {
      }

      public void ClearFormat(byte[] data, int start, int length) {
         for (var run = GetNextRun(start); length > 0 && run != null; run = GetNextRun(start)) {
            if (run.Start >= start + length) return;
            if (run is PointerRun pointerRun) {
               // remove the reference from the anchor we're pointing to as well
               var destination = data.ReadAddress(pointerRun.Start);
               if (destination != Pointer.NULL) {
                  var anchorRun = runs[BinarySearch(destination)];
                  anchorRun.Anchor.RemoveSource(pointerRun.Start);
                  if (anchorRun.Anchor.PointerSources.Count == 0) {
                     ClearFormat(data, anchorRun.Start, length);
                     if (anchorForAddress.ContainsKey(anchorRun.Start)) {
                        addressForAnchor.Remove(anchorForAddress[anchorRun.Start]);
                        anchorForAddress.Remove(anchorRun.Start);
                     }
                  }
               } else {
                  var name = sourceToUnmappedName[pointerRun.Start];
                  sourceToUnmappedName.Remove(pointerRun.Start);
                  unmappedNameToSources[name].Remove(pointerRun.Start);
                  if (unmappedNameToSources[name].Count == 0) unmappedNameToSources.Remove(name);
               }
            }
            foreach (var source in run.Anchor?.PointerSources ?? new int[0]) data.Write(source, 0);
            if (anchorForAddress.ContainsKey(run.Start)) {
               unmappedNameToSources[anchorForAddress[run.Start]] = new List<int>(run.Anchor.PointerSources);
               foreach (var source in run.Anchor.PointerSources) sourceToUnmappedName[source] = anchorForAddress[run.Start];
               addressForAnchor.Remove(anchorForAddress[run.Start]);
               anchorForAddress.Remove(run.Start);
            }
            for (int i = 0; i < run.Length; i++) data[run.Start + i] = 0xFF;
            runs.RemoveAt(BinarySearch(run.Start));
            length -= run.Length + run.Start - start;
            start = run.Start + run.Length;
         }
      }

      private int BinarySearch(int start) {
         var index = runs.BinarySearch(new CompareFormattedRun(start), FormattedRunComparer.Instance);
         return index;
      }
   }

   public class BasicModel : IModel {
      public static IModel Instance { get; } = new BasicModel();
      public int GetAddressFromAnchor(int requestSource, string anchor) => 0;
      public string GetAnchorFromAddress(int requestSource, int destination) => string.Empty;
      public IFormattedRun GetNextRun(int dataIndex) => null;
      public void ObserveRunWritten(byte[] data, IFormattedRun run) { }
      public void ObserveAnchorWritten(byte[] data, int location, string anchorName, string anchorFormat) { }
      public void ClearFormat(byte[] data, int start, int length) { }
   }
}
