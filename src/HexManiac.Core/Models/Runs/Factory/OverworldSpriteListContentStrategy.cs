using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.Models.Runs.Factory {
   public class OverworldSpriteListContentStrategy : RunStrategy {
      // starterbytes:|h paletteid:|h secondid:|h length: width: height: slot.|h overwrite. unused: distribution<> sizedraw<> animation<> sprites<> ramstore<>
      private readonly IReadOnlyList<ArrayRunElementSegment> parentTemplate, parentTemplate2;

      public string Hint { get; }

      public OverworldSpriteListContentStrategy(IFormatRunFactory factory, string format) {
         parentTemplate = new List<ArrayRunElementSegment> {
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
            new ArrayRunPointerSegment(factory, "sprites", OverworldSpriteListRun.SharedFormatString),
            new ArrayRunElementSegment(string.Empty, ElementContentType.Pointer, 4),
         };

         parentTemplate2 = new List<ArrayRunElementSegment> {
            new ArrayRunElementSegment(string.Empty, ElementContentType.Integer, 2),
            new ArrayRunElementSegment("paletteid", ElementContentType.Integer, 2),
            new ArrayRunElementSegment(string.Empty, ElementContentType.Pointer, 4),
            new ArrayRunElementSegment(string.Empty, ElementContentType.Pointer, 4),
            new ArrayRunPointerSegment(factory, "sprites", OverworldSpriteListRun.SharedFormatString),
            new ArrayRunElementSegment(string.Empty, ElementContentType.Pointer, 4),
            new ArrayRunElementSegment(string.Empty, ElementContentType.Pointer, 4),
         };

         if (format.Contains("|")) {
            Hint = format.Split("|")[1].Split("`")[0];
         }
      }

      public override int LengthForNewRun(IDataModel model, int pointerAddress) {
         var destination = model.ReadPointer(pointerAddress);
         if (destination < 0 || destination >= model.Count) return -1;
         return new OverworldSpriteListRun(model, parentTemplate, Hint, 0, destination, new SortedSpan<int>(pointerAddress)).Length;
      }

      public override bool Matches(IFormattedRun run) => run is OverworldSpriteListRun;

      public override bool TryAddFormatAtDestination(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments, int parentIndex) {
         if (sourceSegments == null) return false;
         var attempt = new OverworldSpriteListRun(owner, sourceSegments, Hint, 0, destination, new SortedSpan<int>(source));
         return attempt.Length > 0;
      }

      public override ErrorInfo TryParseData(IDataModel model, string name, int dataIndex, ref IFormattedRun run) {
         var newRun = new OverworldSpriteListRun(model, parentTemplate, Hint, 0, dataIndex, run.PointerSources);
         if (newRun.Length > 0) {
            run = newRun;
            return ErrorInfo.NoError;
         }
         newRun = new OverworldSpriteListRun(model, parentTemplate2, Hint, 0, dataIndex, run.PointerSources);
         if (newRun.Length > 0) {
            run = newRun;
            return ErrorInfo.NoError;
         }
         return new ErrorInfo($"Could not at overworld sprite at {dataIndex}");
      }

      public override void UpdateNewRunFromPointerFormat(IDataModel model, ModelDelta token, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments, int parentIndex, ref IFormattedRun run) {
         run = new OverworldSpriteListRun(model, sourceSegments, Hint, parentIndex, run.Start, run.PointerSources);

         // backup: we may get asked to make a pointer format even when it's not an overworld sprite list.
         // if that happens, fall back to a NoInfoRun.
         if (run.Length == 0) run = new NoInfoRun(run.Start, run.PointerSources);
      }

      public override IFormattedRun WriteNewRun(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         return new OverworldSpriteListRun(owner, sourceSegments, Hint, 0, destination, new SortedSpan<int>(source));
      }
   }
}
