using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.Models.Runs.Factory {
   public abstract class RunStrategy {
      public string Format { get; set; }

      /// <summary>
      /// If a 'default' run is created for the pointer at the given address, how many bytes need to be available at the destination location?
      /// </summary>
      public abstract int LengthForNewRun(IDataModel model, int pointerAddress);

      /// <summary>
      /// Returns true if the format is capable of being added for the pointer at source.
      /// If the token is such that edits are allowed, actually add the format.
      /// </summary>
      public abstract bool TryAddFormatAtDestination(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments);

      /// <summary>
      /// Returns true if the input run is valid for this run 'type'.
      /// Often this is just a type comparison, but for runs with multiple
      /// formats (example, SpriteRun with width/height), it can be more complex.
      /// </summary>
      public abstract bool Matches(IFormattedRun run);

      /// <summary>
      /// Create a new run meant to go into a pointer in a table.
      /// The destination has been prepared, but is all FF.
      /// </summary>
      public abstract IFormattedRun WriteNewRun(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments);

      /// <summary>
      /// A pointer format in a table has changed.
      /// Replace the given run with a new run of the appropriate format.
      /// </summary>
      public abstract void UpdateNewRunFromPointerFormat(IDataModel model, ModelDelta token, string name, ref IFormattedRun run);

      /// <summary>
      /// Attempt to parse the existing data into a run of the desired type.
      /// </summary>
      public abstract ErrorInfo TryParseData(IDataModel model, string name, int dataIndex, ref IFormattedRun run);

      protected ITableRun GetTable(IDataModel model, int pointerAddress) => (ITableRun)model.GetNextRun(pointerAddress);

      protected ArrayRunPointerSegment GetSegment(ITableRun table, int pointerAddress) {
         var offsets = table.ConvertByteOffsetToArrayOffset(pointerAddress);
         return (ArrayRunPointerSegment)table.ElementContent[offsets.SegmentIndex];
      }
   }

   public class FormatRunFactory {
      public static RunStrategy GetStrategy(string format) {
         RunStrategy strategy = null;
         if (format == PCSRun.SharedFormatString) {
            strategy = new PCSRunContentStrategy();
         } else if (format.StartsWith(AsciiRun.SharedFormatString)) {
            strategy = new AsciiRunContentStrategy();
         } else if (format == EggMoveRun.SharedFormatString) {
            strategy = new EggRunContentStrategy();
         } else if (format == PLMRun.SharedFormatString) {
            strategy = new PLMRunContentStrategy();
         } else if (format == XSERun.SharedFormatString) {
            strategy = new XseRunContentStrategy();
         } else if (format == TrainerPokemonTeamRun.SharedFormatString) {
            strategy = new TrainerPokemonTeamRunContentStrategy();
         } else if (LzSpriteRun.TryParseSpriteFormat(format, out var spriteFormat)) {
            strategy = new LzSpriteRunContentStrategy(spriteFormat);
         } else if (LzPaletteRun.TryParsePaletteFormat(format, out var paletteFormat)) {
            strategy = new LzPaletteRunContentStrategy(paletteFormat);
         } else if (LzTilesetRun.TryParseTilesetFormat(format, out var tilesetFormat)) {
            strategy = new LzTilesetRunContentStrategy(tilesetFormat);
         } else if (LzTilemapRun.TryParseTilemapFormat(format, out var tilemapFormat)) {
            strategy = new LzTilemapRunContentStrategy(tilemapFormat);
         } else if (SpriteRun.TryParseSpriteFormat(format, out var spriteFormat1)) {
            strategy = new SpriteRunContentStrategy(spriteFormat1);
         } else if (PaletteRun.TryParsePaletteFormat(format, out var paletteFormat1)) {
            strategy = new PaletteRunContentStrategy(paletteFormat1);
         } else if (format.IndexOf("[") >= 0 && format.IndexOf("[") < format.IndexOf("]")) {
            strategy = new TableStreamRunContentStrategy();
         } else {
            return null;
         }

         strategy.Format = format;
         return strategy;
      }
   }

   public class XseRunContentStrategy : RunStrategy {
      public override int LengthForNewRun(IDataModel model, int pointerAddress) => 1;

      public override bool Matches(IFormattedRun run) => run is XSERun;

      public override bool TryAddFormatAtDestination(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         throw new System.NotImplementedException();
      }

      // TODO
      public override ErrorInfo TryParseData(IDataModel model, string name, int dataIndex, ref IFormattedRun run) {
         run = new XSERun(dataIndex, run.PointerSources);
         return ErrorInfo.NoError;
      }

      public override void UpdateNewRunFromPointerFormat(IDataModel model, ModelDelta token, string name, ref IFormattedRun run) {
         throw new System.NotImplementedException();
      }

      public override IFormattedRun WriteNewRun(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         throw new System.NotImplementedException();
      }
   }

   public class LzTilesetRunContentStrategy : RunStrategy {
      public TilesetFormat TilesetFormat { get; }
      public LzTilesetRunContentStrategy(TilesetFormat format) => TilesetFormat = format;

      public override int LengthForNewRun(IDataModel model, int pointerAddress) {
         var defaultData = new byte[TilesetFormat.BitsPerPixel * 8];
         return LZRun.Compress(defaultData, 0, defaultData.Length).Count;
      }

      public override bool Matches(IFormattedRun run) => run is LzTilesetRun tsRun && tsRun.Format.BitsPerPixel == TilesetFormat.BitsPerPixel;

      public override bool TryAddFormatAtDestination(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         var lzRun = new LZRun(owner, destination);
         if (lzRun.Length < 0) return false;
         if (lzRun.DecompressedLength % 0x20 != 0) return false;
         var newRun = new LzTilesetRun(TilesetFormat, owner, destination);
         if (!(token is NoDataChangeDeltaModel)) owner.ObserveRunWritten(token, newRun);
         return true;
      }

      public override ErrorInfo TryParseData(IDataModel model, string name, int dataIndex, ref IFormattedRun run) {
         var lzRun = new LZRun(model, dataIndex);
         if (lzRun.Length < 0) return new ErrorInfo("Format was specified as a compressed tileset, but no compressed data was recognized.");
         if (lzRun.DecompressedLength % 0x20 != 0) return new ErrorInfo("Format was specified as a compressed tileset, but the compressed data was not the proper length to be a tileset.");
         var newRun = new LzTilesetRun(TilesetFormat, model, dataIndex);
         run = newRun;
         return ErrorInfo.NoError;
      }

      public override void UpdateNewRunFromPointerFormat(IDataModel model, ModelDelta token, string name, ref IFormattedRun run) {
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

   public class LzTilemapRunContentStrategy : RunStrategy {
      public TilemapFormat TilemapFormat { get; }
      public LzTilemapRunContentStrategy(TilemapFormat format) => TilemapFormat = format;

      public override int LengthForNewRun(IDataModel model, int pointerAddress) {
         var defaultData = new byte[TilemapFormat.BitsPerPixel * 8];
         return LZRun.Compress(defaultData, 0, defaultData.Length).Count;
      }

      public override bool Matches(IFormattedRun run) => run is LzTilemapRun tmRun && tmRun.Format.BitsPerPixel == TilemapFormat.BitsPerPixel;

      public override bool TryAddFormatAtDestination(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         var lzRun = new LZRun(owner, destination);
         if (lzRun.DecompressedLength != TilemapFormat.ExpectedUncompressedLength) return false;
         var newRun = new LzTilemapRun(TilemapFormat, owner, destination);
         if (!(token is NoDataChangeDeltaModel)) owner.ObserveRunWritten(token, newRun);
         return true;
      }

      public override ErrorInfo TryParseData(IDataModel model, string name, int dataIndex, ref IFormattedRun run) {
         var lzRun = new LZRun(model, dataIndex);
         if (lzRun.DecompressedLength != TilemapFormat.ExpectedUncompressedLength) {
            return new ErrorInfo($"Expected an uncompressed length of {TilemapFormat.ExpectedUncompressedLength}, but it was {lzRun.DecompressedLength}");
         }
         if (lzRun.Length < 6) {
            return new ErrorInfo($"Unable to decompress run at {dataIndex:X6}.");
         }

         var newRun = new LzTilemapRun(TilemapFormat, model, dataIndex);
         run = newRun;
         return ErrorInfo.NoError;
      }

      public override void UpdateNewRunFromPointerFormat(IDataModel model, ModelDelta token, string name, ref IFormattedRun run) {
         var lzRun = new LZRun(model, run.Start);
         if (lzRun.Length < 0) return;
         if (lzRun.DecompressedLength != TilemapFormat.ExpectedUncompressedLength) return;
         var newRun = new LzTilemapRun(TilemapFormat, model, run.Start, run.PointerSources);
         model.ClearFormat(token, newRun.Start, newRun.Length);
         run = newRun;
      }

      public override IFormattedRun WriteNewRun(IDataModel owner, ModelDelta token, int source, int destination, string name, IReadOnlyList<ArrayRunElementSegment> sourceSegments) {
         var defaultData = new byte[TilemapFormat.BitsPerPixel * 8];
         var data = LZRun.Compress(defaultData, 0, defaultData.Length);
         for (int i = 0; i < data.Count; i++) token.ChangeData(owner, destination + i, data[i]);
         return new LzTilemapRun(TilemapFormat, owner, destination);
      }
   }
}
