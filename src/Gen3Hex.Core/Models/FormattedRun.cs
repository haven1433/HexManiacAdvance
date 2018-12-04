using HavenSoft.Gen3Hex.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;

namespace HavenSoft.Gen3Hex.Core.Models {
   public interface IFormattedRun {
      int Start { get; }
      int Length { get; }
      IReadOnlyList<int> PointerSources { get; }
      IDataFormat CreateDataFormat(IReadOnlyList<byte> data, int index);
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

      public IReadOnlyList<int> PointerSources => throw new NotImplementedException();
      public IDataFormat CreateDataFormat(IReadOnlyList<byte> data, int index) => throw new NotImplementedException();
   }

   public class NoInfoRun : IFormattedRun {
      public int Start { get; }
      public int Length => 1;
      public IReadOnlyList<int> PointerSources { get; }

      public NoInfoRun(int start, IReadOnlyList<int> sources) => (Start, PointerSources) = (start, sources);

      public IDataFormat CreateDataFormat(IReadOnlyList<byte> data, int index) => None.Instance;
   }

   public class PointerRun : IFormattedRun {
      public static PointerRun Default { get; } = new PointerRun(0, new int[0]);

      public int Start { get; }
      public int Length => 4;
      public IReadOnlyList<int> PointerSources { get; }

      public PointerRun(int start, IReadOnlyList<int> sources) {
         Start = start;
         PointerSources = sources;
      }

      public IDataFormat CreateDataFormat(IReadOnlyList<byte> data, int index) {
         return new Pointer(Start, index - Start, data.ReadAddress(Start));
      }
   }
}
