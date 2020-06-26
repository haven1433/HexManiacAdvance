using HavenSoft.HexManiac.Core.ViewModels.DataFormats;

namespace HavenSoft.HexManiac.Core.Models.Runs {
   public class AsciiRun : BaseRun {
      public const char StreamDelimeter = '`';
      public static readonly string SharedFormatString = AsciiRun.StreamDelimeter + "asc" + AsciiRun.StreamDelimeter;

      public override int Length { get; }

      public override string FormatString => "`asc`" + Length;

      public AsciiRun(int start, int length, SortedSpan<int> pointerSources = null) : base(start, pointerSources) => Length = length;

      public override IDataFormat CreateDataFormat(IDataModel data, int index) {
         return new Ascii(Start, index - Start, (char)data[index]);
      }

      protected override BaseRun Clone(SortedSpan<int> newPointerSources) => new AsciiRun(Start, Length, newPointerSources);
   }
}
