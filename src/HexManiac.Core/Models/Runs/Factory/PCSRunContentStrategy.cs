using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.Models.Runs.Factory {
   /// <summary>
   /// Format Specifier:     ""
   /// Expected usage:
   ///                 This format represents text using the pokemon character set.
   /// </summary>
   public class PCSRunContentStrategy : RunStrategy {
      public override int LengthForNewRun(IDataModel model, int pointerAdress) => 1;
      public override bool TryAddFormatAtDestination(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments, int parentIndex) {
         var length = PCSString.ReadString(owner, destination, true);

         if (length < 1) return false;

         // our token will be a no-change token if we're in the middle of exploring the data.
         // If so, don't actually add the run. It's enough to know that we _can_ add the run.
         if (!(token is NoDataChangeDeltaModel)) owner.ObserveRunWritten(token, new PCSRun(owner, destination, length));

         // even if we didn't add the format, we're _capable_ of adding it... so return true
         return true;
      }
      public override bool Matches(IFormattedRun run) => run is PCSRun;
      public override IFormattedRun WriteNewRun(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         // found freespace, so this should already be an FF. Just add the format.
         return new PCSRun(owner, destination, 1);
      }
      public override void UpdateNewRunFromPointerFormat(IDataModel model, ModelDelta token, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments, int parentIndex, ref IFormattedRun run) {
         var length = PCSString.ReadString(model, run.Start, true);
         if (length > 0) {
            var newRun = new PCSRun(model, run.Start, length, run.PointerSources);
            if (!newRun.Equals(run)) model.ClearFormat(token, newRun.Start, newRun.Length);
            run = newRun;
         }
      }
      public override ErrorInfo TryParseData(IDataModel model, string name, int dataIndex, ref IFormattedRun run) {
         var length = PCSString.ReadString(model, dataIndex, true);
         if (length < 0) {
            return new ErrorInfo($"Format was specified as a string, but no string was recognized.");
         } else if (PokemonModel.SpanContainsAnchor(model, dataIndex, length)) {
            return new ErrorInfo($"Format was specified as a string, but a string would overlap the next anchor.");
         }
         run = new PCSRun(model, dataIndex, length);

         return ErrorInfo.NoError;
      }
   }
}
