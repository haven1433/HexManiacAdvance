using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.HexManiac.Core.Models.Runs.Sprites {
   public interface ITilesetRun : ISpriteRun {
      TilesetFormat TilesetFormat { get; }
      ITilesetRun SetPixels(IDataModel model, ModelDelta token, IReadOnlyList<int[,]> tiles);
      int DecompressedLength { get; }
   }

   public static class ITilesetRunExtensions {
      public static IEnumerable<int> GetFillerTiles(this ITilesetRun tileset) {
         var data = tileset.GetData();
         var bytesPerTile = tileset.TilesetFormat.BitsPerPixel * 8;
         var tileCount = data.Length / bytesPerTile;
         for (int i = 0; i < tileCount; i++) {
            if (IsFiller(data, i * bytesPerTile, bytesPerTile)) yield return i;
         }
      }

      public static bool IsFiller(byte[] data, int start, int length) {
         return length.Range().All(i => data[start + i] == 0);
      }
   }

   public class LzTilesetRun : LZRun, ITilesetRun {
      SpriteFormat ISpriteRun.SpriteFormat => new SpriteFormat(TilesetFormat.BitsPerPixel, Width, Height, TilesetFormat.PaletteHint);
      public TilesetFormat TilesetFormat { get; }
      public int Pages => 1;
      public int Width { get; }
      public int Height { get; }
      public bool SupportsImport => false;
      public bool SupportsEdit => true;

      public override string FormatString {
         get {
            var format = $"`lzt{TilesetFormat.BitsPerPixel}";
            if (TilesetFormat.MaxTiles != -1) format += "x" + TilesetFormat.MaxTiles;
            var hint = TilesetFormat.PaletteHint;
            if (!string.IsNullOrEmpty(hint)) format += "|" + hint;
            return format + "`";
         }
      }

      public LzTilesetRun(TilesetFormat format, IDataModel data, int start, SortedSpan<int> sources = null) : base(data, start, allowLengthErrors: format.AllowLengthErrors, sources) {
         TilesetFormat = format;
         var tileSize = format.BitsPerPixel * 8;
         var uncompressedSize = data.ReadMultiByteValue(start + 1, 3);
         var tileCount = uncompressedSize / tileSize;
         var roughSize = Math.Sqrt(tileCount);
         Width = (int)Math.Ceiling(roughSize);
         Height = (int)roughSize;
         if (Width * Height < tileCount) Height += 1;
      }

      public static bool TryParseTilesetFormat(string format, out TilesetFormat tilesetFormat) {
         tilesetFormat = default;
         if (!(format.StartsWith("`lzt") && format.EndsWith("`"))) return false;
         format = format.Substring(4, format.Length - 5);
         bool allowLengthErrors = false;
         if (format.EndsWith("!")) {
            format = format.Substring(0, format.Length - 1);
            allowLengthErrors = true;
         }

         // parse the paletteHint
         string hint = null;
         var pipeIndex = format.IndexOf('|');
         if (pipeIndex != -1) {
            hint = format.Substring(pipeIndex + 1);
            format = format.Substring(0, pipeIndex);
         }

         int maxTiles = -1;
         if (format.Contains("x")) {
            var parts = format.Split("x");
            format = parts[0];
            if (parts.Length > 2) return false;
            if (!int.TryParse(parts[1], out maxTiles)) return false;
         }

         if (!int.TryParse(format, out int bits)) return false;
         tilesetFormat = new TilesetFormat(bits, -1, maxTiles, hint, allowLengthErrors);
         return true;
      }

      public byte[] GetData() => Decompress(Model, Start);

      int[,] ISpriteRun.GetPixels(IDataModel model, int page, int tableIndex) => GetPixels(model, page);

      public int[,] GetPixels(IDataModel model, int page) {
         var data = Decompress(model, Start);
         if (data == null) return null;
         return SpriteRun.GetPixels(data, 0, Width, Height, TilesetFormat.BitsPerPixel);
      }

      public int[,] GetPixels(IDataModel model, int page, int preferredTileWidth) {
         var data = Decompress(model, Start);
         var tileSize = TilesetFormat.BitsPerPixel * 8;
         var tileCount = data.Length / tileSize;
         var preferredTileHeight = (int)Math.Ceiling((double)tileCount / preferredTileWidth);
         return SpriteRun.GetPixels(data, 0, preferredTileWidth, preferredTileHeight, TilesetFormat.BitsPerPixel);
      }

      public ISpriteRun SetPixels(IDataModel model, ModelDelta token, int page, int[,] pixels) {
         // TODO handle the fact that pixels[,] may contain a different number of tiles compared to the existing tileset
         var data = Decompress(model, Start);
         for (int x = 0; x < pixels.GetLength(0); x++) for (int y = 0; y < pixels.GetLength(1); y++) {
            pixels[x, y] %= (int)Math.Pow(2, TilesetFormat.BitsPerPixel);
         }
         SpriteRun.SetPixels(data, 0, pixels, TilesetFormat.BitsPerPixel);
         var newModelData = Compress(data, 0, data.Length);
         var newRun = model.RelocateForExpansion(token, this, newModelData.Count);
         for (int i = 0; i < newModelData.Count; i++) token.ChangeData(model, newRun.Start + i, newModelData[i]);
         for (int i = newModelData.Count; i < Length; i++) token.ChangeData(model, newRun.Start + i, 0xFF);
         newRun = new LzTilesetRun(TilesetFormat, model, newRun.Start, newRun.PointerSources);
         model.ObserveRunWritten(token, newRun);
         return newRun;
      }

      int lastFormatRequested = int.MaxValue;
      public override IDataFormat CreateDataFormat(IDataModel data, int index) {
         var basicFormat = base.CreateDataFormat(data, index);
         if (!CreateForLeftEdge) return basicFormat;
         if (lastFormatRequested < index) {
            lastFormatRequested = index;
            return basicFormat;
         }

         var sprite = data.CurrentCacheScope.GetImage(this);
         var availableRows = (Length - (index - Start)) / ExpectedDisplayWidth;
         lastFormatRequested = index;
         return new SpriteDecorator(basicFormat, sprite, ExpectedDisplayWidth, availableRows);
      }

      protected override BaseRun Clone(SortedSpan<int> newPointerSources) => new LzTilesetRun(TilesetFormat, Model, Start, newPointerSources);

      public ISpriteRun Duplicate(SpriteFormat format) => new LzTilesetRun(new TilesetFormat(format.BitsPerPixel, TilesetFormat.Tiles, TilesetFormat.MaxTiles, format.PaletteHint), Model, Start, PointerSources);

      public ITilesetRun SetPixels(IDataModel model, ModelDelta token, IReadOnlyList<int[,]> tiles) {
         return SetPixels(this, model, token, tiles, (start, sources) => new LzTilesetRun(TilesetFormat, model, start, sources));
      }

      public static ITilesetRun SetPixels(ITilesetRun run, IDataModel model, ModelDelta token, IReadOnlyList<int[,]> tiles, Func<int, SortedSpan<int>, ITilesetRun> construct) {
         var tileSize = 8 * run.TilesetFormat.BitsPerPixel;
         var data = new byte[tiles.Count * tileSize];

         for (int i = 0; i < tiles.Count; i++) {
            SpriteRun.SetPixels(data, i * tileSize, tiles[i], run.TilesetFormat.BitsPerPixel);
         }

         var newModelData = Compress(data, 0, data.Length);
         var newRun = model.RelocateForExpansion(token, run, newModelData.Count);
         for (int i = 0; i < newModelData.Count; i++) token.ChangeData(model, newRun.Start + i, newModelData[i]);
         for (int i = newModelData.Count; i < run.Length; i++) token.ChangeData(model, newRun.Start + i, 0xFF);
         newRun = construct(newRun.Start, newRun.PointerSources);
         model.ObserveRunWritten(token, newRun);
         return newRun;
      }
   }
}
