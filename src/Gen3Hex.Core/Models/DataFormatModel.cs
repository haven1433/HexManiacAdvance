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

      void ObserveRunWritten(IFormattedRun run);
   }

   public class PointerModel : IModel {
      private readonly List<IFormattedRun> runs;

      public PointerModel(byte[] data) {
         var pointersForDestination = new Dictionary<int, List<int>>();
         var destinationForSource = new SortedList<int, int>();
         runs = new List<IFormattedRun>();

         SearchForPointers(data, pointersForDestination, destinationForSource);
         WriteRuns(pointersForDestination, destinationForSource);
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

      private void WriteRuns(Dictionary<int, List<int>> pointersForDestination, SortedList<int, int> destinationForSource) {
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

      private void ResolveConflicts() {
         for (int i = 0; i < runs.Count - 1; i++) {
            if (runs[i].Start + runs[i].Length <= runs[i + 1].Start) continue;
            Debug.Fail("Pointers and Destinations are both 4-byte aligned, and pointers are only 4 bytes long. How the heck did I get a conflict?");
         }
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

      public void ObserveRunWritten(IFormattedRun run) { }
   }

   public class BasicModel : IModel {
      public static IModel Instance { get; } = new BasicModel();
      public IFormattedRun GetNextRun(int dataIndex) => null;
      public void ObserveRunWritten(IFormattedRun run) { }
   }
}
