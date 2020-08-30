using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.Models.Runs.Factory {
   /// <summary>
   /// Format Specifier:     `plm`
   /// Represents a stream of level up moves in a 7-9 bit split.
   /// (A future refactor will hopefully replace this as just a special case of the ITableStreamRun
   /// </summary>
   public class PLMRunContentStrategy : RunStrategy {
      public override int LengthForNewRun(IDataModel model, int pointerAddress) => 2;
      public override bool TryAddFormatAtDestination(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments, int parentIndex) {
         var plmRun = new PLMRun(owner, destination);
         var length = plmRun.Length;
         if (length < 2) return false;

         if (!(token is NoDataChangeDeltaModel)) owner.ObserveRunWritten(token, plmRun);

         return true;
      }
      public override bool Matches(IFormattedRun run) => run is PLMRun;
      public override IFormattedRun WriteNewRun(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         // PLM ends with FFFF, and this is already freespace, so just add the format.
         return new PLMRun(owner, destination);
      }
      public override void UpdateNewRunFromPointerFormat(IDataModel model, ModelDelta token, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments, int parentIndex, ref IFormattedRun run) {
         var runAttempt = new PLMRun(model, run.Start);
         if (runAttempt.Length > 0) {
            run = runAttempt.MergeAnchor(run.PointerSources);
            model.ClearFormat(token, run.Start, run.Length);
         }
      }
      public override ErrorInfo TryParseData(IDataModel model, string name, int dataIndex, ref IFormattedRun run) {
         run = new PLMRun(model, dataIndex);
         if (run.Length == 0) return new ErrorInfo("Format specified was for pokemon level-up move data, but could not parse that location as level-up move data.");
         return ErrorInfo.NoError;
      }
   }
}
