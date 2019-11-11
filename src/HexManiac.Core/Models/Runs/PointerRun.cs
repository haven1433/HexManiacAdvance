using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.Models.Runs {
   public class PointerRun : BaseRun {
      public const char PointerStart = '<';
      public const char PointerEnd = '>';

      public override int Length => 4;
      public override string FormatString => string.Empty;

      public PointerRun(int start, IReadOnlyList<int> sources = null) : base(start, sources) { }

      public override IDataFormat CreateDataFormat(IDataModel data, int index) {
         var destinationAddress = data.ReadPointer(Start);
         var anchor = data.GetAnchorFromAddress(Start, destinationAddress);
         var pointer = new Pointer(Start, index - Start, destinationAddress, anchor, destinationAddress < 0);
         return pointer;
      }
      protected override BaseRun Clone(IReadOnlyList<int> newPointerSources) {
         return new PointerRun(Start, newPointerSources);
      }
   }
}
