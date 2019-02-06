using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.Models.Runs {
   public class AsciiRun : BaseRun {
      public const char StreamDelimeter = '`';

      public override int Length { get; }

      public override string FormatString => "`asc`" + Length;

      public AsciiRun(int start, int length, IReadOnlyList<int> pointerSources = null) : base(start, pointerSources) => Length = length;

      public override IDataFormat CreateDataFormat(IDataModel data, int index) {
         return new Ascii(Start, index - Start, (char)data[index]);
      }

      protected override IFormattedRun Clone(IReadOnlyList<int> newPointerSources) {
         return new AsciiRun(Start, Length, newPointerSources);
      }
   }
}
