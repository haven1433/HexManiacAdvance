using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using System;
using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.Models.Runs.Factory {
   public class TilesetRunContentStrategy : RunStrategy {
      public TilesetFormat TilesetFormat { get; }
      public TilesetRunContentStrategy(TilesetFormat format) => TilesetFormat = format;

      public override int LengthForNewRun(IDataModel model, int pointerAddress) => TilesetFormat.Tiles * TilesetFormat.BitsPerPixel * 8;

      public override bool TryAddFormatAtDestination(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments, int parentIndex) {
         var newRun = new TilesetRun(TilesetFormat, owner, destination);
         if (owner.GetNextRun(destination + 1).Start < destination + newRun.Length) return false;
         if (!(token is NoDataChangeDeltaModel)) owner.ObserveRunWritten(token, newRun);
         return true;
      }

      public override bool Matches(IFormattedRun run) => run is TilesetRun tsRun && tsRun.TilesetFormat.BitsPerPixel == TilesetFormat.BitsPerPixel && tsRun.TilesetFormat.Tiles == TilesetFormat.Tiles;

      public override IFormattedRun WriteNewRun(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         var newRun = new TilesetRun(TilesetFormat, owner, destination, SortedSpan.One(source));
         return newRun;
      }

      public override void UpdateNewRunFromPointerFormat(IDataModel model, ModelDelta token, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments, int parentIndex, ref IFormattedRun run) {
         var newRun = new TilesetRun(TilesetFormat, model, run.Start, run.PointerSources);
         if (model.GetNextRun(run.Start + 1).Start < run.Start + newRun.Length) return;
         model.ClearFormat(token, newRun.Start, newRun.Length);
         run = newRun;
      }

      public override ErrorInfo TryParseData(IDataModel model, string name, int dataIndex, ref IFormattedRun run) {
         var error = SpriteRunContentStrategy.IsValid(TilesetFormat.BitsPerPixel);
         if (error.HasError) return error;
         var newRun = new TilesetRun(TilesetFormat, model, run.Start, run.PointerSources);
         var nextRun = model.GetNextRun(run.Start + 1);
         if (run.Start < nextRun.Start && nextRun.Start < run.Start + newRun.Length) return new ErrorInfo($"Format was specified as a tileset with {TilesetFormat.Tiles} tiles, but there wasn't enough space.");
         run = newRun;
         return ErrorInfo.NoError;
      }
   }
}
