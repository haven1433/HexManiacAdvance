using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.Models.Runs.Factory {
   public class LzTilemapRunContentStrategy : RunStrategy {
      public TilemapFormat TilemapFormat { get; }
      public LzTilemapRunContentStrategy(TilemapFormat format) => TilemapFormat = format;

      public override int LengthForNewRun(IDataModel model, int pointerAddress) {
         var defaultData = new byte[TilemapFormat.BitsPerPixel * 8];
         return LZRun.Compress(defaultData, 0, defaultData.Length).Count;
      }

      public override bool Matches(IFormattedRun run) => run is LzTilemapRun tmRun && tmRun.Format.BitsPerPixel == TilemapFormat.BitsPerPixel;

      public override bool TryAddFormatAtDestination(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments, int parentIndex) {
         var lzRun = new LZRun(owner, destination);
         if (lzRun.DecompressedLength != TilemapFormat.ExpectedUncompressedLength) return false;
         var newRun = new LzTilemapRun(TilemapFormat, owner, destination);
         if (!(token is NoDataChangeDeltaModel)) owner.ObserveRunWritten(token, newRun);
         return true;
      }

      public override ErrorInfo TryParseData(IDataModel model, string name, int dataIndex, ref IFormattedRun run) {
         var error = SpriteRunContentStrategy.IsValid(TilemapFormat.BitsPerPixel);
         if (error.HasError) return error;
         var lzRun = new LZRun(model, dataIndex);
         var rowRemainder = lzRun.DecompressedLength % TilemapFormat.TileWidth;
         if (rowRemainder != 0) {
            return new ErrorInfo($"{lzRun.DecompressedLength} cannot be divided into {TilemapFormat.TileWidth} columns.");
         }
         if (lzRun.Length < 6) {
            return new ErrorInfo($"Unable to decompress run at {dataIndex:X6}.");
         }

         var newRun = new LzTilemapRun(TilemapFormat, model, dataIndex);
         run = newRun;
         return ErrorInfo.NoError;
      }

      public override void UpdateNewRunFromPointerFormat(IDataModel model, ModelDelta token, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments, int parentIndex, ref IFormattedRun run) {
         var lzRun = new LZRun(model, run.Start);
         if (lzRun.Length < 0) return;
         if (lzRun.DecompressedLength != TilemapFormat.ExpectedUncompressedLength) return;
         var newRun = new LzTilemapRun(TilemapFormat, model, run.Start, run.PointerSources);
         model.ClearFormat(token, newRun.Start, newRun.Length);
         run = newRun;
      }

      public override IFormattedRun WriteNewRun(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         var defaultData = new byte[TilemapFormat.ExpectedUncompressedLength];
         var data = LZRun.Compress(defaultData, 0, defaultData.Length);
         for (int i = 0; i < data.Count; i++) token.ChangeData(owner, destination + i, data[i]);
         return new LzTilemapRun(TilemapFormat, owner, destination);
      }
   }
}
