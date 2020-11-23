using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System.Text;

namespace HavenSoft.HexManiac.Core.Models.Runs {
   public class WordRun : BaseRun, IAppendToBuilderRun {
      public string SourceArrayName { get; }

      public override int Length { get; }

      public int ValueOffset { get; }

      public string Note { get; }

      public override string FormatString => Length == 4 ? "::" : ".";

      public WordRun(int start, string name, int length, int valueOffset, string note = null, SortedSpan<int> sources = null) : base(start, sources) => (SourceArrayName, Length, ValueOffset, Note) = (name, length, valueOffset, note);

      public override IDataFormat CreateDataFormat(IDataModel data, int index) {
         if (Length == 4) return new MatchedWord(Start, index - Start, "::" + SourceArrayName);
         if (Length == 2) return new Integer(Start, index - Start, data.ReadMultiByteValue(Start, 2), 2);
         return new Integer(Start, 0, data[index], 1);
      }

      protected override BaseRun Clone(SortedSpan<int> newPointerSources) => new WordRun(Start, SourceArrayName, Length, ValueOffset, Note, newPointerSources);

      public void AppendTo(IDataModel model, StringBuilder builder, int start, int length, bool deep) {
         if (Length == 4) {
            builder.Append(FormatString + SourceArrayName);
         } else {
            builder.Append(model[start]);
         }
      }
   }
}
