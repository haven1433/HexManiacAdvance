using HavenSoft.HexManiac.Core.ViewModels.DataFormats;

namespace HavenSoft.HexManiac.Core.Models.Runs {
   public class WordRun : BaseRun {
      public string SourceArrayName { get; }

      public override int Length { get; }

      public int ValueOffset { get; }

      public override string FormatString => Length == 4 ? "::" : ".";

      public WordRun(int start, string name, int length, int valueOffset, SortedSpan<int> sources = null) : base(start, sources) => (SourceArrayName, Length, ValueOffset) = (name, length, valueOffset);

      public override IDataFormat CreateDataFormat(IDataModel data, int index) {
         if (Length == 4) return new MatchedWord(Start, index - Start, "::" + SourceArrayName);
         return new Integer(Start, 0, data[index], 1);
      }

      protected override BaseRun Clone(SortedSpan<int> newPointerSources) => new WordRun(Start, SourceArrayName, Length, ValueOffset, newPointerSources);
   }
}
