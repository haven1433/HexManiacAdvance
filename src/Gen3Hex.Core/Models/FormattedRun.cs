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

      public NoInfoRun(int start, Anchor anchor = null) => (Start, Anchor) = (start, anchor);

      public IDataFormat CreateDataFormat(IReadOnlyList<byte> data, int index) => None.Instance;
      public void MergeAnchor(Anchor other) {
         if (other == null) return;
         if (Anchor == null) { Anchor = other; return; }
         var sources = other.PointerSources.Concat(Anchor.PointerSources).Distinct().OrderBy(i => i).ToList();
         Anchor = new Anchor(sources);
      }
   }

   public class PointerRun : IFormattedRun {
      private readonly IModel parent;
      public int Start { get; }
      public int Length => 4;
      public Anchor Anchor { get; private set; }

      public PointerRun(IModel parent, int start, Anchor anchor = null) {
         this.parent = parent;
         Start = start;
         Anchor = anchor;
      }

      public IDataFormat CreateDataFormat(IReadOnlyList<byte> data, int index) {
         var destinationAddress = Math.Max(0, data.ReadAddress(Start));
         var anchor = parent.GetAnchorFromAddress(Start, destinationAddress);
         var pointer = new Pointer(Start, index - Start, data.ReadAddress(Start), anchor);
         return pointer;
      }

      public void MergeAnchor(Anchor other) {
         if (other == null) return;
         if (Anchor == null) { Anchor = other; return; }
         var sources = other.PointerSources.Concat(Anchor.PointerSources).Distinct().OrderBy(i => i).ToList();
         Anchor = new Anchor(sources);
      }
   }

   public class Anchor {
      public IReadOnlyList<int> PointerSources { get; private set; }

      public Anchor(IReadOnlyList<int> sources = null) => PointerSources = sources ?? new int[0];

      public void RemoveSource(int source) {
         PointerSources = PointerSources.Except(new[] { source }).ToList();
      }
   }
}
