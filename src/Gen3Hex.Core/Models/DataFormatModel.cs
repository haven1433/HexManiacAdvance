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

      int GetAddressFromAnchor(int requestSource, string anchor);
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
               runs.Add(new PointerRun(sources.Current, data.ReadAddress(sources.Current)));
               moreSources = sources.MoveNext();
            } else {
               runs.Add(new PointerRun(sources.Current, data.ReadAddress(sources.Current), anchor: new Anchor(sources: pointersForDestination[destinations.Current])));
               moreDestinations = destinations.MoveNext();
               moreSources = sources.MoveNext();
            }
         }

         while (moreDestinations) {
            runs.Add(new NoInfoRun(destinations.Current, new Anchor(sources: pointersForDestination[destinations.Current])));
            moreDestinations = destinations.MoveNext();
         }

         while (moreSources) {
            runs.Add(new PointerRun(sources.Current, data.ReadAddress(sources.Current)));
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

         return 0;
      }

      public string GetAnchorFromAddress(int address) {
         return anchorForAddress.TryGetValue(address, out string anchor) ? anchor : string.Empty;
      }

      public IFormattedRun GetNextRun(int dataIndex) {
         var index = runs.BinarySearch(new CompareFormattedRun(dataIndex), FormattedRunComparer.Instance);
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
         var index = runs.BinarySearch(run, FormattedRunComparer.Instance);
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
               if (pointerRun1.DestinationAddress == 0) {
                  var name = sourceToUnmappedName[pointerRun1.Start];
                  sourceToUnmappedName.Remove(pointerRun1.Start);
                  unmappedNameToSources[name].Remove(pointerRun1.Start);
               }
            }

            runs[index] = run;
            run.MergeAnchor(existingRun.Anchor);
         }

         if (run is PointerRun pointerRun) {
            if (pointerRun.DestinationAddress != 0) {
               index = runs.BinarySearch(new CompareFormattedRun(pointerRun.DestinationAddress), FormattedRunComparer.Instance);
               if (index < 0) {
                  // the pointer is brand new
                  index = ~index;
                  runs.Insert(index, new NoInfoRun(pointerRun.DestinationAddress, new Anchor(new[] { run.Start })));
               } else {
                  runs[index].MergeAnchor(new Anchor(new[] { run.Start }));
               }
            }
            //if (pointerRun.DestinationName == string.Empty) {
            //   var destination = data.ReadAddress(pointerRun.Start);
            //   index = runs.BinarySearch(new CompareFormattedRun(destination), FormattedRunComparer.Instance);
            //   if (index < 0) {
            //      // does not exist yet: add it
            //      runs.Insert(~index, new NoInfoRun(destination, new Anchor(null, new[] { pointerRun.Start })));
            //   } else {
            //      // does exist: add it
            //      runs[index].MergeAnchor(new Anchor(null, new[] { pointerRun.Start }));
            //   }
            //}
         }
      }

      public void ObserveAnchorWritten(byte[] data, int location, string anchorName, string anchorFormat) {
         if (anchorForAddress.TryGetValue(location, out string oldAnchorName)) {
            anchorForAddress.Remove(location);
            addressForAnchor.Remove(anchorName);
         }

         if (addressForAnchor.ContainsKey(anchorName)) {
            // TODO the desired name already exists
            // (1) remove old name
            // (2) remove old name and delete data
            // (3) rename old name
            throw new NotImplementedException();
            // no matter which is chosen, anything pointing to the old should now be pointing to the new
         } else {
            anchorForAddress.Add(location, anchorName);
            addressForAnchor.Add(anchorName, location);
         }

         if (unmappedNameToSources.TryGetValue(anchorName, out var sources)) {
            foreach (var source in sources) {
               var index = runs.BinarySearch(new CompareFormattedRun(source), FormattedRunComparer.Instance);
               Debug.Assert(index >= 0 && runs[index] is PointerRun);
               runs[index] = new PointerRun(source, location, runs[index].Anchor);
               sourceToUnmappedName.Remove(source);
               data.WritePointer(source, location);
            }
            unmappedNameToSources.Remove(anchorName);
         }

      }

      // TODO observe removal of runs (pointer replaced with normal data)
      public void ObserveNormalDataWrite(byte[] data, int index) {
      }

      // TODO observe anchor removal
   }

   public class BasicModel : IModel {
      public static IModel Instance { get; } = new BasicModel();
      public int GetAddressFromAnchor(int requestSource, string anchor) => 0;
      public IFormattedRun GetNextRun(int dataIndex) => null;
      public void ObserveRunWritten(byte[] data, IFormattedRun run) { }
      public void ObserveAnchorWritten(byte[] data, int location, string anchorName, string anchorFormat) { }
   }
}
