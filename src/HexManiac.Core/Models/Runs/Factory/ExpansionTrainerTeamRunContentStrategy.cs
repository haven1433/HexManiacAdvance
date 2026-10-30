using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.HexManiac.Core.Models.Runs.Factory {
   /// <summary>
   /// Format Specifier:     `tpte`
   /// A pokeemerald-expansion trainer party (36-byte TrainerMon records). The element count comes from the
   /// 'pokemonCount' field of the trainer record that points to it.
   /// </summary>
   public class ExpansionTrainerTeamRunContentStrategy : RunStrategy {
      public override int LengthForNewRun(IDataModel model, int pointerAdress) => new ExpansionTrainerTeamRun(model, -1, new SortedSpan<int>(pointerAdress)).Length;

      public override bool TryAddFormatAtDestination(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments, int parentIndex) {
         var teamRun = new ExpansionTrainerTeamRun(owner, destination, new SortedSpan<int>(source));
         if (teamRun.Length < ExpansionTrainerTeamRun.ElementSize) return false;
         if (token is not NoDataChangeDeltaModel) {
            var existingRun = owner.GetNextRun(teamRun.Start);
            if (existingRun.Start >= teamRun.Start + teamRun.Length || existingRun is NoInfoRun || existingRun is PointerRun) {
               owner.ClearFormat(token, teamRun.Start, teamRun.Length);
               owner.ObserveRunWritten(token, teamRun);
            } else {
               return false;
            }
         }
         return true;
      }

      public override bool Matches(IFormattedRun run) => run is ExpansionTrainerTeamRun;

      public override IFormattedRun WriteNewRun(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         var run = new ExpansionTrainerTeamRun(owner, destination, new SortedSpan<int>(source));
         for (int i = 0; i < run.Length; i++) token.ChangeData(owner, destination + i, 0);
         return run.DeserializeRun("5 ???", token, out var _, out var _);
      }

      public override void UpdateNewRunFromPointerFormat(IDataModel model, ModelDelta token, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments, int parentIndex, ref IFormattedRun run) {
         var runAttempt = new ExpansionTrainerTeamRun(model, run.Start, run.PointerSources);
         if (runAttempt.Length < 1) return;
         if (runAttempt.PointerSources.Any(source => source.InRange(runAttempt.Start, runAttempt.Start + runAttempt.Length))) return;
         model.ClearFormat(token, run.Start, runAttempt.Length);
         run = runAttempt;
      }

      public override ErrorInfo TryParseData(IDataModel model, string name, int dataIndex, ref IFormattedRun run) {
         var pointerSources = run.PointerSources;
         if (pointerSources == null || pointerSources.Count == 0) pointerSources = model.GetUnmappedSourcesToAnchor(name);
         if (pointerSources.Count == 0) return new ErrorInfo("Cannot create a trainer team without a pointer from trainer data.");
         run = new ExpansionTrainerTeamRun(model, dataIndex, pointerSources);
         return ErrorInfo.NoError;
      }
   }
}
