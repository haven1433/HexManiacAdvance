using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.HexManiac.Core.Models.Runs {
   public interface IFormattedRun {
      int Start { get; }
      int Length { get; }
      IReadOnlyList<int> PointerSources { get; }
      string FormatString { get; }
      IDataFormat CreateDataFormat(IDataModel data, int index);
      IFormattedRun MergeAnchor(IReadOnlyList<int> sources);
      IFormattedRun RemoveSource(int source);
      IFormattedRun Duplicate(int start, params int[] pointerSources);
   }

   /// <summary>
   /// A run representing a stream.
   /// Streams usually are variable length, and end with some sort of 'end' token.
   /// We want to be able to stringify streams, so we can use them with the tools.
   /// </summary>
   public interface IStreamRun : IFormattedRun {
      /// <summary>
      /// Should not change the data, only creates a string representation of that data.
      /// </summary>
      string SerializeRun();

      /// <summary>
      /// Updates data based on converting content back into the stream.
      /// Returns the run where the data was placed.
      /// The run usually starts at the same spot as before, but in the case of repointing it can be different.
      /// </summary>
      IStreamRun DeserializeRun(string content, ModelDelta token);

      bool DependsOn(string anchorName);
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
      public IDataFormat CreateDataFormat(IDataModel data, int index) => throw new NotImplementedException();
      public IFormattedRun MergeAnchor(IReadOnlyList<int> other) => throw new NotImplementedException();
      public IFormattedRun RemoveSource(int source) => throw new NotImplementedException();

      public IFormattedRun Duplicate(int start, params int[] pointerSources) => throw new NotImplementedException();
   }

   public abstract class BaseRun : IFormattedRun {
      public const char AnchorStart = '^';

      public int Start { get; private set; }
      public abstract int Length { get; }
      public abstract string FormatString { get; }
      public IReadOnlyList<int> PointerSources { get; private set; }

      public BaseRun(int start, IReadOnlyList<int> sources = null) {
         Start = start;
         PointerSources = sources;
      }

      public abstract IDataFormat CreateDataFormat(IDataModel data, int index);

      public IFormattedRun MergeAnchor(IReadOnlyList<int> sources) {
         if (sources == null) return this;

         if (PointerSources == null) return Clone(sources);
         return Clone(sources.Concat(PointerSources).Distinct().OrderBy(i => i).ToList());
      }

      public virtual IFormattedRun RemoveSource(int source) {
         return Clone(PointerSources.Except(new[] { source }).ToList());
      }

      public IFormattedRun Duplicate(int start, params int[] pointerSources) {
         var myStart = Start;
         Start = start;
         var copy = Clone(pointerSources);
         Start = myStart;
         return copy;
      }

      protected abstract BaseRun Clone(IReadOnlyList<int> newPointerSources);
   }

   public class NoInfoRun : BaseRun {
      public static NoInfoRun NullRun { get; } = new NoInfoRun(int.MaxValue);  // effectively a null object

      public override int Length => 1;
      public override string FormatString => string.Empty;

      public NoInfoRun(int start, IReadOnlyList<int> sources = null) : base(start, sources) { }

      public override IDataFormat CreateDataFormat(IDataModel data, int index) => None.Instance;
      protected override BaseRun Clone(IReadOnlyList<int> newPointerSources) {
         return new NoInfoRun(Start, newPointerSources);
      }
   }
}
