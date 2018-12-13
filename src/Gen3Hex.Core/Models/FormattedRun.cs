using HavenSoft.Gen3Hex.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.Gen3Hex.Core.Models {
   public interface IFormattedRun {
      int Start { get; }
      int Length { get; }
      IReadOnlyList<int> PointerSources { get; }
      IDataFormat CreateDataFormat(IModel data, int index);
      void MergeAnchor(IReadOnlyList<int> sources);
      void RemoveSource(int source);
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
      public IDataFormat CreateDataFormat(IModel data, int index) => throw new NotImplementedException();
      public void MergeAnchor(IReadOnlyList<int> other) => throw new NotImplementedException();
      public void RemoveSource(int source) => throw new NotImplementedException();
   }

   public abstract class BaseRun : IFormattedRun {
      public int Start { get; }
      public abstract int Length { get; }
      public IReadOnlyList<int> PointerSources { get; private set; }

      public BaseRun(int start, IReadOnlyList<int> sources = null) {
         Start = start;
         PointerSources = sources;
      }

      public abstract IDataFormat CreateDataFormat(IModel data, int index);

      public void MergeAnchor(IReadOnlyList<int> sources) {
         if (sources == null) return;
         if (PointerSources == null) { PointerSources = sources; return; }
         PointerSources = sources.Concat(PointerSources).Distinct().OrderBy(i => i).ToList();
      }

      public void RemoveSource(int source) {
         PointerSources = PointerSources.Except(new[] { source }).ToList();
      }
   }

   public class NoInfoRun : BaseRun {
      public static NoInfoRun NullRun { get; } = new NoInfoRun(int.MaxValue);  // effectively a null object

      public override int Length => 1;

      public NoInfoRun(int start, IReadOnlyList<int> sources = null) : base(start, sources) { }

      public override IDataFormat CreateDataFormat(IModel data, int index) => None.Instance;
   }

   public class PointerRun : BaseRun {
      public override int Length => 4;

      public PointerRun(int start, IReadOnlyList<int> sources = null) : base(start, sources) { }

      public override IDataFormat CreateDataFormat(IModel data, int index) {
         var destinationAddress = Math.Max(0, data.ReadPointer(Start));
         var anchor = data.GetAnchorFromAddress(Start, destinationAddress);
         var pointer = new Pointer(Start, index - Start, data.ReadPointer(Start), anchor);
         return pointer;
      }
   }
}
