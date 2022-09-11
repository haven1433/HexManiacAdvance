using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Factory;
using HexManiac.Core.Models.Runs.Sprites;
using System.Collections.Generic;

/*
 layout<[
   width:: height::
   borderblock<>
   map<>
   tiles1<>
   tiles2<>
   borderwidth. borderheight. unused:]1>
 */

namespace HexManiac.Core.Models.Runs.Factory {
   public class BlockmapRunContentStrategy : RunStrategy {
      public override int LengthForNewRun(IDataModel model, int pointerAddress) {
         var width = model.ReadMultiByteValue(pointerAddress - 12, 4);
         var height = model.ReadMultiByteValue(pointerAddress - 8, 4);
         return width * height * 2;
      }

      public override bool Matches(IFormattedRun run) => run is BlockmapRun;

      public override bool TryAddFormatAtDestination(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments, int parentIndex) {
         var run = new BlockmapRun(owner, destination, SortedSpan.One(source));
         owner.ClearFormat(token, run.Start, run.Length);
         owner.ObserveRunWritten(token, run);
         return true;
      }

      public override ErrorInfo TryParseData(IDataModel model, string name, int dataIndex, ref IFormattedRun run) {
         run = new BlockmapRun(model, dataIndex, run.PointerSources);
         return ErrorInfo.NoError;
      }

      public override void UpdateNewRunFromPointerFormat(IDataModel model, ModelDelta token, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments, int parentIndex, ref IFormattedRun run) {
         var runAttempt = new BlockmapRun(model, run.Start, run.PointerSources);
         if (runAttempt.Length > 0) {
            run = runAttempt.MergeAnchor(run.PointerSources);
            model.ClearFormat(token, run.Start, run.Length);
         }
      }

      public override IFormattedRun WriteNewRun(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         var runAttempt = new BlockmapRun(owner, destination, SortedSpan.One(source));
         for (int i = 0; i < runAttempt.Length; i++) token.ChangeData(owner, destination + i, 0);
         return runAttempt;
      }
   }
}
