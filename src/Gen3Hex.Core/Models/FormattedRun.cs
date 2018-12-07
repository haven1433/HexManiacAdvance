using HavenSoft.Gen3Hex.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.Gen3Hex.Core.Models {
   public interface IFormattedRun {
      int Start { get; }
      int Length { get; }
      Anchor Anchor { get; }
      IDataFormat CreateDataFormat(IReadOnlyList<byte> data, int index);
      void MergeAnchor(Anchor other);
   }

   public class FormattedRunComparer : IComparer<IFormattedRun> {
      public static FormattedRunComparer Instance { get; } = new FormattedRunComparer();
      public int Compare(IFormattedRun a, IFormattedRun b) => a.Start.CompareTo(b.Start);
   }

   /// <summary>
   /// Converts from a start index to an IFormattedRun, for comparison purposes.
   /// </summary>
   public class CompareFormattedRun : IFormattedRun {
      public int Start { get; }
      public int Length => 0;

      public CompareFormattedRun(int start) => Start = start;

      public Anchor Anchor => throw new NotImplementedException();
      public IDataFormat CreateDataFormat(IReadOnlyList<byte> data, int index) => throw new NotImplementedException();
      public void MergeAnchor(Anchor other) => throw new NotImplementedException();
   }

   public class NoInfoRun : IFormattedRun {
      public int Start { get; }
      public int Length => 1;
      public Anchor Anchor { get; private set; }

      public NoInfoRun(int start, Anchor anchor = null) => (Start, Anchor) = (start, anchor ?? new Anchor());

      public IDataFormat CreateDataFormat(IReadOnlyList<byte> data, int index) => None.Instance;
      public void MergeAnchor(Anchor other) {
         var sources = other.PointerSources.Concat(Anchor.PointerSources).Distinct().OrderBy(i => i).ToList();
         // var name = !string.IsNullOrEmpty(other.Name) ? other.Name : Anchor.Name;
         Anchor = new Anchor(sources);
      }
   }

   public class PointerRun : IFormattedRun {
      public int Start { get; }
      public int DestinationAddress { get; }
      public int Length => 4;
      public Anchor Anchor { get; private set; }

      public PointerRun(int start, int destinationAddress, Anchor anchor = null) {
         Start = start;
         DestinationAddress = destinationAddress;
         Anchor = anchor ?? new Anchor();
      }

      public IDataFormat CreateDataFormat(IReadOnlyList<byte> data, int index) {
         return new Pointer(Start, index - Start, data.ReadAddress(Start));
      }

      public void MergeAnchor(Anchor other) {
         var sources = other.PointerSources.Concat(Anchor.PointerSources).Distinct().OrderBy(i => i).ToList();
         // var name = !string.IsNullOrEmpty(other.Name) ? other.Name : Anchor.Name;
         Anchor = new Anchor(sources);
      }
   }

   public class Anchor {
      public IReadOnlyList<int> PointerSources { get; }

      public Anchor(IReadOnlyList<int> sources = null) => PointerSources = sources ?? new int[0];
   }
}
