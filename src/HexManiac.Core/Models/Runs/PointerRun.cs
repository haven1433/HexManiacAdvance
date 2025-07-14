using HavenSoft.HexManiac.Core.ViewModels.DataFormats;

namespace HavenSoft.HexManiac.Core.Models.Runs {
   public class PointerRun : BaseRun {
      public const char PointerStart = '<';
      public const char PointerEnd = '>';

      public override int Length => 4;
      public override string FormatString => string.Empty;

      public PointerRun(int start, SortedSpan<int> sources = null) : base(start, sources) { }

      protected override BaseRun Clone(SortedSpan<int> newPointerSources) {
         return new PointerRun(Start, newPointerSources);
      }
   }

   public class OffsetPointerRun : PointerRun {
      public int Offset { get; }
      public OffsetPointerRun(int start, int offset, SortedSpan<int> sources = null) : base(start, sources) => Offset = offset;
      protected override BaseRun Clone(SortedSpan<int> newPointerSources) => new OffsetPointerRun(Start, Offset, newPointerSources);
   }
}
