using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.Models.Runs.Factory {
   /// <summary>
   /// Format Specifier:     `tpt`
   /// Represents a run of pokemon used by a trainer.
   /// This is one of four formats, and its number of elements is specified based on the table that points to it.
   /// As such, this is NOT just a special type of TableStream, because it needs custom logic.
   /// </summary>
   public class TrainerPokemonTeamRunContentStrategy : RunStrategy {
      public override int LengthForNewRun(IDataModel model, int pointerAdress) => new TrainerPokemonTeamRun(model, -1, new[] { pointerAdress }).Length;
      public override bool TryAddFormatAtDestination(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         var teamRun = new TrainerPokemonTeamRun(owner, destination, new[] { source });
         var length = teamRun.Length;
         if (length < 2) return false;

         if (!(token is NoDataChangeDeltaModel)) owner.ObserveRunWritten(token, teamRun);

         return true;
      }
      public override bool Matches(IFormattedRun run) => run is TrainerPokemonTeamRun;
      public override IFormattedRun WriteNewRun(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         return new TrainerPokemonTeamRun(owner, destination, new[] { source }).DeserializeRun("0 ???", token);
      }
      public override void UpdateNewRunFromPointerFormat(IDataModel model, ModelDelta token, string name, ref IFormattedRun run) {
         var runAttempt = new TrainerPokemonTeamRun(model, run.Start, run.PointerSources);
         model.ClearFormat(token, run.Start, runAttempt.Length);
         run = runAttempt;
      }
      public override ErrorInfo TryParseData(IDataModel model, string name, int dataIndex, ref IFormattedRun run) {
         run = new TrainerPokemonTeamRun(model, dataIndex, run.PointerSources);
         return ErrorInfo.NoError;
      }
   }
}
