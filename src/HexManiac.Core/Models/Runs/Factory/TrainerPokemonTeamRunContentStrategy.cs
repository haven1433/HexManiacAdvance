using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.Models.Runs.Factory {
   /// <summary>
   /// Format Specifier:     `tpt`
   /// Represents a run of pokemon used by a trainer.
   /// This is one of four formats, and its number of elements is specified based on the table that points to it.
   /// As such, this is NOT just a special type of TableStream, because it needs custom logic.
   /// </summary>
   public class TrainerPokemonTeamRunContentStrategy : RunStrategy {
      private readonly bool showFullIVByteRange = false;
      public TrainerPokemonTeamRunContentStrategy(bool showFullIVByteRange) => this.showFullIVByteRange = showFullIVByteRange;
      public override int LengthForNewRun(IDataModel model, int pointerAdress) => new TrainerPokemonTeamRun(model, -1, showFullIVByteRange, new SortedSpan<int>(pointerAdress)).Length;
      public override bool TryAddFormatAtDestination(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments, int parentIndex) {
         var teamRun = new TrainerPokemonTeamRun(owner, destination, showFullIVByteRange, new SortedSpan<int>(source));
         var length = teamRun.Length;
         if (length < 2) return false;

         if (!(token is NoDataChangeDeltaModel)) owner.ObserveRunWritten(token, teamRun);

         return true;
      }
      public override bool Matches(IFormattedRun run) => run is TrainerPokemonTeamRun;
      public override IFormattedRun WriteNewRun(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         return new TrainerPokemonTeamRun(owner, destination, showFullIVByteRange, new SortedSpan<int>(source)).DeserializeRun("0 ???", token, out var _); // new run, so we don't care about the changes
      }
      public override void UpdateNewRunFromPointerFormat(IDataModel model, ModelDelta token, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments, int parentIndex, ref IFormattedRun run) {
         var runAttempt = new TrainerPokemonTeamRun(model, run.Start, showFullIVByteRange, run.PointerSources);
         model.ClearFormat(token, run.Start, runAttempt.Length);
         run = runAttempt;
      }
      public override ErrorInfo TryParseData(IDataModel model, string name, int dataIndex, ref IFormattedRun run) {
         var pointerSources = run.PointerSources;
         if (pointerSources == null || pointerSources.Count == 0) {
            pointerSources = model.GetUnmappedSourcesToAnchor(name);
         }
         if (pointerSources.Count == 0) {
            return new ErrorInfo("Cannot create trainer team without a pointer from trainer data.");
         }
         run = new TrainerPokemonTeamRun(model, dataIndex, showFullIVByteRange, pointerSources);
         return ErrorInfo.NoError;
      }
   }
}
