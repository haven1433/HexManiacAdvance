using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.Models.Runs {
   public interface IScriptStartRun : IFormattedRun { }

   public class XSERun : BaseRun, IScriptStartRun {
      public static string SharedFormatString => "`xse`";

      public override int Length => 1;

      public override string FormatString => SharedFormatString;

      public XSERun(int start, IReadOnlyList<int> sources = null) : base(start, sources) { }

      public override IDataFormat CreateDataFormat(IDataModel data, int index) => None.Instance;

      protected override BaseRun Clone(IReadOnlyList<int> newPointerSources) => new XSERun(Start, newPointerSources);
   }

   public class BSERun : BaseRun, IScriptStartRun {
      public static string SharedFormatString => "`bse`";

      public override int Length => 1;

      public override string FormatString => SharedFormatString;

      public BSERun(int start, IReadOnlyList<int> sources = null) : base(start, sources) { }

      public override IDataFormat CreateDataFormat(IDataModel data, int index) => None.Instance;

      protected override BaseRun Clone(IReadOnlyList<int> newPointerSources) => new BSERun(Start, newPointerSources);
   }
}
