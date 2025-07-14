using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.Models.Runs.Factory {
   /// <summary>
   /// Table of pointers to uncompressed tiles.
   /// The kicker is that each pointer points to N tiles.
   /// N is decided by the 'tiles' field in a parent table.
   /// Number of pointers is decided by 'frames' field in parent table.
   /// </summary>
   public class MapAnimationTilesStrategy : RunStrategy {
      // parent: [animations<`mat`> frames: timer. tiles. tileOffset::]!FEFEFEFE

      public override int LengthForNewRun(IDataModel model, int pointerAddress) {
         return 4;
      }

      public override bool Matches(IFormattedRun run) {
         if (run is not TableStreamRun tableRun) return false;
         if (!tableRun.AllowsZeroElements) return false;
         if (tableRun.ElementContent.Count != 1) return false;
         if (tableRun.ElementContent[0] is not ArrayRunPointerSegment pointerSegment) return false;
         return (pointerSegment.InnerFormat.StartsWith("`uct4x") && pointerSegment.InnerFormat.EndsWith("`"));
      }

      public override bool TryAddFormatAtDestination(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments, int parentIndex) {
         IFormattedRun run = new NoInfoRun(destination, SortedSpan.One(source));
         var result = TryParseData(owner, default, default, ref run);
         if (result.HasError) return false;
         owner.ObserveRunWritten(token, run);
         return true;
      }

      public override ErrorInfo TryParseData(IDataModel model, string name, int dataIndex, ref IFormattedRun run) {
         var source = run.PointerSources[0];
         var destination = run.Start;
         // if (model.GetNextRun(source) is not ITableRun parentRun) return new ErrorInfo("No parent!");
         // if (!parentRun.ElementContent.Any(seg => seg.Name == MapAnimationTilesRun.ParentTileCountField)) return new ErrorInfo("No tile field!");
         // if (!parentRun.ElementContent.Any(seg => seg.Name == MapAnimationTilesRun.ParentFrameCountField)) return new ErrorInfo("No frame field!");
         run = new MapAnimationTilesRun(model, destination, SortedSpan.One(source));
         return ErrorInfo.NoError;
      }

      public override void UpdateNewRunFromPointerFormat(IDataModel model, ModelDelta token, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments, int parentIndex, ref IFormattedRun run) {
         TryParseData(model, name, default, ref run);
         model.ClearFormat(token, run.Start, run.Length);
      }

      public override IFormattedRun WriteNewRun(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         return new MapAnimationTilesRun(owner, destination, SortedSpan.One(source));
      }
   }
}
