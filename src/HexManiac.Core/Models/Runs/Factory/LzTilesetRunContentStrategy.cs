using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.Models.Runs.Factory {
   public class LzTilesetRunContentStrategy : RunStrategy {
      public TilesetFormat TilesetFormat { get; }
      public LzTilesetRunContentStrategy(TilesetFormat format) => TilesetFormat = format;

      public override int LengthForNewRun(IDataModel model, int pointerAddress) {
         var defaultData = new byte[TilesetFormat.BitsPerPixel * 8];
         return LZRun.Compress(defaultData, 0, defaultData.Length).Count;
      }

      public override bool Matches(IFormattedRun run) => run is LzTilesetRun tsRun && tsRun.TilesetFormat.BitsPerPixel == TilesetFormat.BitsPerPixel;

      public override bool TryAddFormatAtDestination(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments, int parentIndex) {
         var lzRun = new LZRun(owner, destination);
         if (lzRun.Length < 0) return false;
         if (lzRun.DecompressedLength % 0x20 != 0) return false;
         var newRun = new LzTilesetRun(TilesetFormat, owner, destination);
         if (!(token is NoDataChangeDeltaModel)) owner.ObserveRunWritten(token, newRun);
         return true;
      }

      public override ErrorInfo TryParseData(IDataModel model, string name, int dataIndex, ref IFormattedRun run) {
         var error = SpriteRunContentStrategy.IsValid(TilesetFormat.BitsPerPixel);
         if (error.HasError) return error;
         var lzRun = new LzTilesetRun(TilesetFormat, model, dataIndex);
         if (lzRun.Length == LZRun.DecompressedTooLong) return new ErrorInfo("Decompressed more bytes than expected. Add a ! to override this.");
         if (lzRun.Length < 0) return new ErrorInfo("Format was specified as a compressed tileset, but no compressed data was recognized.");
         if (lzRun.DecompressedLength % 0x20 != 0) return new ErrorInfo("Format was specified as a compressed tileset, but the compressed data was not the proper length to be a tileset.");
         var newRun = new LzTilesetRun(TilesetFormat, model, dataIndex);
         run = newRun;
         return ErrorInfo.NoError;
      }

      public override void UpdateNewRunFromPointerFormat(IDataModel model, ModelDelta token, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments, int parentIndex, ref IFormattedRun run) {
         var lzRun = new LZRun(model, run.Start);
         if (lzRun.Length < 0) return;
         if (lzRun.DecompressedLength % 0x20 != 0) return;
         var newRun = new LzTilesetRun(TilesetFormat, model, run.Start, run.PointerSources);
         model.ClearFormat(token, newRun.Start, newRun.Length);
         run = newRun;
      }

      public override IFormattedRun WriteNewRun(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         var defaultData = new byte[TilesetFormat.BitsPerPixel * 8];
         var data = LZRun.Compress(defaultData, 0, defaultData.Length);
         for (int i = 0; i < data.Count; i++) token.ChangeData(owner, destination + i, data[i]);
         return new LzTilesetRun(TilesetFormat, owner, destination);
      }
   }
}
