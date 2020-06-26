using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.Models.Runs {
   public class WordRun : BaseRun {
      public string SourceArrayName { get; }

      public override int Length => 4;

      public override string FormatString => "::";

      public WordRun(int start, string name, SortedSpan<int> sources = null) : base(start, sources) => SourceArrayName = name;

      public override IDataFormat CreateDataFormat(IDataModel data, int index) => new MatchedWord(Start, index - Start, "::" + SourceArrayName);

      protected override BaseRun Clone(SortedSpan<int> newPointerSources) => new WordRun(Start, SourceArrayName, newPointerSources);
   }
}
