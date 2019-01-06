using HavenSoft.Gen3Hex.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.Gen3Hex.Core.Models {
   public interface IFormattedRun {
      int Start { get; }
      int Length { get; }
      IReadOnlyList<int> PointerSources { get; }
      string FormatString { get; }
      IDataFormat CreateDataFormat(IModel data, int index);
      IFormattedRun MergeAnchor(IReadOnlyList<int> sources);
      IFormattedRun RemoveSource(int source);
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
      public string FormatString => throw new NotImplementedException();

      public CompareFormattedRun(int start) => Start = start;

      public IReadOnlyList<int> PointerSources => throw new NotImplementedException();
      public IDataFormat CreateDataFormat(IModel data, int index) => throw new NotImplementedException();
      public IFormattedRun MergeAnchor(IReadOnlyList<int> other) => throw new NotImplementedException();
      public IFormattedRun RemoveSource(int source) => throw new NotImplementedException();
   }

   public abstract class BaseRun : IFormattedRun {
      public int Start { get; }
      public abstract int Length { get; }
      public abstract string FormatString { get; }
      public IReadOnlyList<int> PointerSources { get; private set; }

      public BaseRun(int start, IReadOnlyList<int> sources = null) {
         Start = start;
         PointerSources = sources;
      }

      public abstract IDataFormat CreateDataFormat(IModel data, int index);

      public IFormattedRun MergeAnchor(IReadOnlyList<int> sources) {
         if (sources == null) return this;

         if (PointerSources == null) return Clone(sources);
         return Clone(sources.Concat(PointerSources).Distinct().OrderBy(i => i).ToList());
      }

      public IFormattedRun RemoveSource(int source) {
         return Clone(PointerSources.Except(new[] { source }).ToList());
      }

      protected abstract IFormattedRun Clone(IReadOnlyList<int> newPointerSources);
   }

   public class NoInfoRun : BaseRun {
      public static NoInfoRun NullRun { get; } = new NoInfoRun(int.MaxValue);  // effectively a null object

      public override int Length => 1;
      public override string FormatString => string.Empty;

      public NoInfoRun(int start, IReadOnlyList<int> sources = null) : base(start, sources) { }

      public override IDataFormat CreateDataFormat(IModel data, int index) => None.Instance;
      protected override IFormattedRun Clone(IReadOnlyList<int> newPointerSources) {
         return new NoInfoRun(Start, newPointerSources);
      }
   }

   public class PointerRun : BaseRun {
      public override int Length => 4;
      public override string FormatString => string.Empty;

      public PointerRun(int start, IReadOnlyList<int> sources = null) : base(start, sources) { }

      public override IDataFormat CreateDataFormat(IModel data, int index) {
         var destinationAddress = data.ReadPointer(Start);
         var anchor = data.GetAnchorFromAddress(Start, destinationAddress);
         var pointer = new Pointer(Start, index - Start, data.ReadPointer(Start), anchor);
         return pointer;
      }
      protected override IFormattedRun Clone(IReadOnlyList<int> newPointerSources) {
         return new PointerRun(Start, newPointerSources);
      }
   }

   public class PCSRun : BaseRun {
      public override int Length { get; }
      public override string FormatString => "\"\"";

      public PCSRun(int start, int length, IReadOnlyList<int> sources = null) : base(start, sources) => Length = length;

      public override IDataFormat CreateDataFormat(IModel data, int index) {
         bool isEscaped = index > Start && data[index - 1] == PCSString.Escape;
         var fullString = PCSString.Convert(data, Start, Length);
         if (isEscaped) {
            return new EscapedPCS(Start, index-Start, fullString, data[index]);
         } else {
            var character = PCSString.Convert(data, index, 1).Substring(1); // trim leading "
            if (index == Start) character = '"' + character; // include the opening quotation mark, only for the first character
            var pcs = new PCS(Start, index - Start, fullString, character);
            return pcs;
         }
      }
      protected override IFormattedRun Clone(IReadOnlyList<int> newPointerSources) {
         return new PCSRun(Start, Length, newPointerSources);
      }
   }
}
