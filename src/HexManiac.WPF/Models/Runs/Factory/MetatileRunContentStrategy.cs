using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.Models.Runs.Factory;

public class MetatileRunContentStrategy : RunStrategy {
   public override int LengthForNewRun(IDataModel model, int pointerAddress) {
      throw new System.NotImplementedException();
   }

   public override bool Matches(IFormattedRun run) {
      throw new System.NotImplementedException();
   }

   public override bool TryAddFormatAtDestination(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments, int parentIndex) {
      throw new System.NotImplementedException();
   }

   public override ErrorInfo TryParseData(IDataModel model, string name, int dataIndex, ref IFormattedRun run) {
      throw new System.NotImplementedException();
   }

   public override void UpdateNewRunFromPointerFormat(IDataModel model, ModelDelta token, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments, int parentIndex, ref IFormattedRun run) {
      throw new System.NotImplementedException();
   }

   public override IFormattedRun WriteNewRun(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
      return new MetatileRun(owner, destination, SortedSpan.One(source));
   }
}

