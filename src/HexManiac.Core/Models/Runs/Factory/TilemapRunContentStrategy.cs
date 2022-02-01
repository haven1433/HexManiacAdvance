using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.Models.Runs.Factory {
   public class TilemapRunContentStrategy : RunStrategy {
      private readonly TilemapFormat format;

      public TilemapRunContentStrategy(TilemapFormat format) {
         this.format = format;
      }

      public override int LengthForNewRun(IDataModel model, int pointerAddress) {
         return format.TileHeight * format.TileWidth * 2;
      }

      public override bool Matches(IFormattedRun run) {
         if (!(run is TilemapRun tilemapRun)) return false;
         if (tilemapRun.Format.TileWidth != format.TileWidth) return false;
         if (tilemapRun.Format.TileHeight != format.TileHeight) return false;
         if (tilemapRun.Format.BitsPerPixel != format.BitsPerPixel) return false;
         return tilemapRun.Format.ExpectedUncompressedLength == format.ExpectedUncompressedLength;
      }

      public override bool TryAddFormatAtDestination(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments, int parentIndex) {
         var newRun = new TilemapRun(owner, destination, format);
         if (!(token is NoDataChangeDeltaModel)) owner.ObserveRunWritten(token, newRun);
         return true;
      }

      public override ErrorInfo TryParseData(IDataModel model, string name, int dataIndex, ref IFormattedRun run) {
         var error = SpriteRunContentStrategy.IsValid(format.BitsPerPixel);
         if (error.HasError) return error;
         var newRun = new TilemapRun(model, dataIndex, format);
         run = newRun;
         return ErrorInfo.NoError;
      }

      public override void UpdateNewRunFromPointerFormat(IDataModel model, ModelDelta token, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments, int parentIndex, ref IFormattedRun run) {
         var newRun = new TilemapRun(model, run.Start, format, run.PointerSources);
         model.ClearFormat(token, newRun.Start, newRun.Length);
         run = newRun;
      }

      public override IFormattedRun WriteNewRun(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         var defaultData = new byte[format.TileWidth * format.TileHeight * 2];
         for (int i = 0; i < defaultData.Length; i++) token.ChangeData(owner, destination + i, defaultData[i]);
         return new TilemapRun(owner, destination, format);
      }
   }
}
