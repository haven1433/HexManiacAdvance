using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using System.Collections.Generic;
using System.Text;

namespace HavenSoft.HexManiac.Core.Models.Runs {
   // TODO handle \n, \r, \t, and other unprintable characters
   public class AsciiRun : BaseRun, IStreamRun {
      public const char StreamDelimeter = '`';
      public static readonly string SharedFormatString = AsciiRun.StreamDelimeter + "asc" + AsciiRun.StreamDelimeter;

      private readonly IDataModel model;

      public override int Length { get; }

      public override string FormatString => "`asc`" + Length;

      public IReadOnlyList<IPixelViewModel> Visualizations => throw new System.NotImplementedException();

      public AsciiRun(IDataModel model, int start, int length, SortedSpan<int> pointerSources = null) : base(start, pointerSources) => (this.model, Length) = (model, length.LimitToRange(1, 1000));

      public override IDataFormat CreateDataFormat(IDataModel data, int index) {
         return new Ascii(Start, index - Start, ((char)data[index]).ToString());
      }

      protected override BaseRun Clone(SortedSpan<int> newPointerSources) => new AsciiRun(model, Start, Length, newPointerSources);

      public string SerializeRun() {
         var builder = new StringBuilder();
         for (int i = 0; i < Length; i++) {
            builder.Append((char)model[Start + i]);
         }
         return builder.ToString();
      }

      public IStreamRun DeserializeRun(string content, ModelDelta token, out IReadOnlyList<int> changedOffsets) {
         for (int i = 0; i < Length; i++) {
            if (i < content.Length) token.ChangeData(model, Start + i, (byte)content[i]);
            else token.ChangeData(model, Start + i, 0);
         }
         changedOffsets = new List<int>();
         return this;
      }

      public bool DependsOn(string anchorName) => false;

      public IReadOnlyList<AutocompleteItem> GetAutoCompleteOptions(string line, int caretLineIndex, int caretCharacterIndex) => null;
   }
}
