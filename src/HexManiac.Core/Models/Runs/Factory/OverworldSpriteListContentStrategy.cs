using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.Models.Runs.Factory {
   public class OverworldSpriteListContentStrategy : RunStrategy {
      // starterbytes:|h paletteid:|h secondid:|h length: width: height: slot.|h overwrite. unused: distribution<> sizedraw<> animation<> sprites<> ramstore<>
      private readonly IReadOnlyList<ArrayRunElementSegment> parentTemplate = new List<ArrayRunElementSegment> {
         new ArrayRunElementSegment(string.Empty, ElementContentType.Integer, 2),
         new ArrayRunElementSegment("paletteid", ElementContentType.Integer, 2),
         new ArrayRunElementSegment(string.Empty, ElementContentType.Integer, 2),
         new ArrayRunElementSegment("length", ElementContentType.Integer, 2),
         new ArrayRunElementSegment("width", ElementContentType.Integer, 2),
         new ArrayRunElementSegment("height", ElementContentType.Integer, 2),
         new ArrayRunElementSegment(string.Empty, ElementContentType.Integer, 4),
         new ArrayRunElementSegment(string.Empty, ElementContentType.Pointer, 4),
         new ArrayRunElementSegment(string.Empty, ElementContentType.Pointer, 4),
         new ArrayRunElementSegment(string.Empty, ElementContentType.Pointer, 4),
         new ArrayRunPointerSegment("sprites", OverworldSpriteListRun.SharedFormatString),
         new ArrayRunElementSegment(string.Empty, ElementContentType.Pointer, 4),
      };

      public override int LengthForNewRun(IDataModel model, int pointerAddress) {
         var destination = model.ReadPointer(pointerAddress);
         if (destination < 0 || destination >= model.Count) return -1;
         return new OverworldSpriteListRun(model, parentTemplate, destination, new SortedSpan<int>(pointerAddress)).Length;
      }

      public override bool Matches(IFormattedRun run) => run is OverworldSpriteListRun;

      public override bool TryAddFormatAtDestination(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         var attempt = new OverworldSpriteListRun(owner, parentTemplate, destination, new SortedSpan<int>(source));
         return attempt.Length > 0;
      }

      public override ErrorInfo TryParseData(IDataModel model, string name, int dataIndex, ref IFormattedRun run) {
         var newRun = new OverworldSpriteListRun(model, parentTemplate, dataIndex, run.PointerSources);
         if (newRun.Length > 0) {
            run = newRun;
            return ErrorInfo.NoError;
         }
         return new ErrorInfo($"Could not at overworld sprite at {dataIndex}");
      }

      public override void UpdateNewRunFromPointerFormat(IDataModel model, ModelDelta token, string name, ref IFormattedRun run) {
         run = new OverworldSpriteListRun(model, parentTemplate, run.Start, run.PointerSources);
      }

      public override IFormattedRun WriteNewRun(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         return new OverworldSpriteListRun(owner, sourceSegments, destination, new SortedSpan<int>(source));
      }
   }
}
