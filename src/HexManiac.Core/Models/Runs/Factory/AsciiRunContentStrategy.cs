using System;
using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.Models.Runs.Factory {
   /// <summary>
   /// Format Specifier:     `asc`
   /// ASCII runs are not currently supported within tables.
   /// </summary>
   public class AsciiRunContentStrategy : RunStrategy {
      public override int LengthForNewRun(IDataModel model, int pointerAddress) => throw new NotImplementedException();

      public override bool Matches(IFormattedRun run) => throw new NotImplementedException();

      public override bool TryAddFormatAtDestination(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments, int parentIndex) => throw new NotImplementedException();

      public override void UpdateNewRunFromPointerFormat(IDataModel model, ModelDelta token, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments, int parentIndex, ref IFormattedRun run) => throw new NotImplementedException();

      public override IFormattedRun WriteNewRun(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) => throw new NotImplementedException();

      public override ErrorInfo TryParseData(IDataModel model, string name, int dataIndex, ref IFormattedRun run) {
         if (int.TryParse(Format.Substring(5), out var length)) {
            run = new AsciiRun(model, dataIndex, length);
         } else {
            return new ErrorInfo($"Ascii runs must include a length.");
         }

         return ErrorInfo.NoError;
      }
   }
}
