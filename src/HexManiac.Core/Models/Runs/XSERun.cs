using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.Models.Runs {
   public class XSERun : BaseRun {
      public static string SharedFormatString => "`xse`";

      public override int Length => 1;

      public override string FormatString => SharedFormatString;

      public XSERun(int start, IReadOnlyList<int> sources = null) : base(start, sources) { }

      public override IDataFormat CreateDataFormat(IDataModel data, int index) => None.Instance;

      protected override BaseRun Clone(IReadOnlyList<int> newPointerSources) => new XSERun(Start, newPointerSources);
   }
}
