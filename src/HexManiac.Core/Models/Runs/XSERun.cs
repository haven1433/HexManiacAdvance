using HavenSoft.HexManiac.Core.ViewModels.DataFormats;

namespace HavenSoft.HexManiac.Core.Models.Runs {
   public interface IScriptStartRun : IFormattedRun { }

   public class XSERun : BaseRun, IScriptStartRun {
      public static string SharedFormatString => "`xse`";

      public override int Length => 1;

      public override string FormatString => SharedFormatString;

      public XSERun(int start, SortedSpan<int> sources = null) : base(start, sources) { }

      public override IDataFormat CreateDataFormat(IDataModel data, int index) => None.Instance;

      protected override BaseRun Clone(SortedSpan<int> newPointerSources) => new XSERun(Start, newPointerSources);
   }

   public class BSERun : BaseRun, IScriptStartRun {
      public static string SharedFormatString => "`bse`";

      public override int Length => 1;

      public override string FormatString => SharedFormatString;

      public BSERun(int start, SortedSpan<int> sources = null) : base(start, sources) { }

      public override IDataFormat CreateDataFormat(IDataModel data, int index) => None.Instance;

      protected override BaseRun Clone(SortedSpan<int> newPointerSources) => new BSERun(Start, newPointerSources);
   }

   public class ASERun : BaseRun, IScriptStartRun {
      public static string SharedFormatString => "`ase`";

      public override int Length => 1;

      public override string FormatString => SharedFormatString;

      public ASERun(int start, SortedSpan<int> sources = null) : base(start, sources) { }

      public override IDataFormat CreateDataFormat(IDataModel data, int index) => None.Instance;

      protected override BaseRun Clone(SortedSpan<int> newPointerSources) => new ASERun(Start, newPointerSources);
   }

   public class TSERun : BaseRun, IScriptStartRun {
      public static string SharedFormatString => "`tse`";

      public override int Length => 1;

      public override string FormatString => SharedFormatString;

      public TSERun(int start, SortedSpan<int> sources = null) : base(start, sources) { }

      public override IDataFormat CreateDataFormat(IDataModel data, int index) => None.Instance;

      protected override BaseRun Clone(SortedSpan<int> newPointerSources) => new TSERun(Start, newPointerSources);
   }
}
