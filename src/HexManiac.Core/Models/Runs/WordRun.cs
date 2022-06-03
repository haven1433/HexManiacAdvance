using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Diagnostics;
using System.Text;

namespace HavenSoft.HexManiac.Core.Models.Runs {
   public class WordRun : BaseRun, IAppendToBuilderRun {
      public string SourceArrayName { get; }

      public override int Length { get; }

      public int ValueOffset { get; }

      public int MultOffset { get; }

      public string Note { get; }

      public override string FormatString => Length switch {
         4 => "::",
         2 => ":",
         _ => ".",
      };

      public WordRun(int start, string name, int length, int valueOffset, int multOffset, string note = null, SortedSpan<int> sources = null) : base(start, sources) {
         SourceArrayName = name;
         Length = length;
         ValueOffset = valueOffset;
         Debug.Assert(multOffset > 0, "MultOffset must be positive!");
         MultOffset = multOffset;
         Note = note;
      }

      public override IDataFormat CreateDataFormat(IDataModel data, int index) {
         if (Length == 4) return new MatchedWord(Start, index - Start, "::" + SourceArrayName);
         if (Length == 2) return new Integer(Start, index - Start, data.ReadMultiByteValue(Start, 2), 2);
         return new Integer(Start, 0, data[index], 1);
      }

      protected override BaseRun Clone(SortedSpan<int> newPointerSources) => new WordRun(Start, SourceArrayName, Length, ValueOffset, MultOffset, Note, newPointerSources);

      public void AppendTo(IDataModel model, StringBuilder builder, int start, int length, bool deep) {
         if (Length == 4) {
            builder.Append(FormatString + SourceArrayName);
         } else if (Length == 2 || Length == 1) {
            var offset = string.Empty;
            if (MultOffset != 1) offset += "*" + MultOffset;
            if (ValueOffset > 0) offset += "+" + ValueOffset;
            if (ValueOffset < 0) offset += "-" + ValueOffset;
            builder.Append($"{FormatString}{SourceArrayName}{offset}={model[start]} ");
         } else {
            throw new NotImplementedException();
         }
      }

      public void Clear(IDataModel model, ModelDelta changeToken, int start, int length) {
         for (int i = 0; i < length; i++) changeToken.ChangeData(model, start + i, 0x00);
      }

      public int Read(IDataModel model) {
         return (model.ReadMultiByteValue(Start, Length) - ValueOffset) / MultOffset;
      }
   }
}
