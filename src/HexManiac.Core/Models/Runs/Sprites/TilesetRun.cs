using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Text;

namespace HavenSoft.HexManiac.Core.Models.Runs.Sprites {
   public class TilesetRun : BaseRun, ITilesetRun {
      private readonly IDataModel model;

      public TilesetFormat TilesetFormat { get; }

      public SpriteFormat SpriteFormat {
         get {
            var tiles = Math.Sqrt(TilesetFormat.Tiles);
            var width = (int)Math.Ceiling(tiles);
            var height = (int)Math.Ceiling((double)TilesetFormat.Tiles / width);
            return new SpriteFormat(TilesetFormat.BitsPerPixel, width, height, TilesetFormat.PaletteHint);
         }
      }

      public int Pages => 1;

      public bool SupportsImport => false;
      public bool SupportsEdit => true;

      public override int Length => TilesetFormat.Tiles * TilesetFormat.BitsPerPixel * 8;
      public int DecompressedLength => Length;

      public override string FormatString {
         get {
            var format = $"`uct4x{TilesetFormat.Tiles}";
            if (TilesetFormat.MaxTiles != -1) format += $"x{TilesetFormat.MaxTiles}";
            var hint = TilesetFormat.PaletteHint;
            format += (string.IsNullOrEmpty(hint) ? string.Empty : $"|{hint}");
            return format + "`";
         }
      }

      public TilesetRun(TilesetFormat tilesetFormat, IDataModel model, int start, SortedSpan<int> sources = null) : base(start, sources) {
         if (tilesetFormat.Tiles == -1) {
            var nextRun = model.GetNextAnchor(start + 1);
            if (nextRun.Start <= start) nextRun = model.GetNextAnchor(nextRun.Start + nextRun.Length);
            var tiles = (nextRun.Start - start) / (8 * tilesetFormat.BitsPerPixel);
            tilesetFormat = new TilesetFormat(tilesetFormat.BitsPerPixel, tiles, -1, tilesetFormat.PaletteHint);
         }
         TilesetFormat = tilesetFormat;
         this.model = model;
      }

      public static bool TryParseTilesetFormat(string format, out TilesetFormat tilesetFormat) {
         tilesetFormat = default;
         if (!format.StartsWith("`uct") || !format.EndsWith("`")) return false;
         format = format.Substring(4, format.Length - 5);
         var hintsplit = format.Split("|");
         if (hintsplit.Length > 2) return false;
         format = hintsplit[0];
         string hint = string.Empty;
         if (hintsplit.Length == 2) hint = hintsplit[1];
         var split = format.Split("x");
         if (split.Length < 2 || split.Length > 3) return false;
         if (!int.TryParse(split[0], out int bits) || !int.TryParse(split[1], out int tiles)) return false;
         var maxTiles = -1;
         if (split.Length == 3 && !int.TryParse(split[0], out maxTiles)) return false;
         tilesetFormat = new TilesetFormat(bits, tiles, maxTiles, hint);
         return true;
      }

      public void AppendTo(IDataModel model, StringBuilder builder, int start, int length, bool deep) {
         while (length > 0) {
            builder.Append(model[start].ToHexString() + " ");
            start += 1;
            length -= 1;
         }
      }

      public void Clear(IDataModel model, ModelDelta changeToken, int start, int length) {
         for (int i = 0; i < length; i++) {
            changeToken.ChangeData(model, start + i, 0x00);
         }
      }

      public override IDataFormat CreateDataFormat(IDataModel data, int index) => new LzUncompressed(index);

      public ISpriteRun Duplicate(SpriteFormat newFormat) => new SpriteRun(model, Start, newFormat, PointerSources);

      public byte[] GetData() {
         var (width, height) = (SpriteFormat.TileWidth, SpriteFormat.TileHeight);
         var data = new byte[width * height * SpriteFormat.BitsPerPixel * 8];
         Array.Copy(model.RawData, Start, data, 0, Length);
         return data;
      }

      public int[,] GetPixels(IDataModel model, int page, int tableIndex) {
         var (width, height) = (SpriteFormat.TileWidth, SpriteFormat.TileHeight);
         var data = new byte[Length];
         Array.Copy(model.RawData, Start, data, 0, Length);
         var pixels = SpriteRun.GetPixels(data, 0, width, height, TilesetFormat.BitsPerPixel);
         return pixels;
      }

      public ISpriteRun SetPixels(IDataModel model, ModelDelta token, int page, int[,] pixels) {
         var data = new byte[Length];
         SpriteRun.SetPixels(data, 0, pixels, TilesetFormat.BitsPerPixel);
         for (int i = 0; i < Length; i++) token.ChangeData(model, Start + i, data[i]);
         return this;
      }

      public ITilesetRun SetPixels(IDataModel model, ModelDelta token, IReadOnlyList<int[,]> tiles) {
         return LzTilesetRun.SetPixels(this, model, token, tiles, (start, sources) => new TilesetRun(TilesetFormat, model, start, sources));
      }

      protected override BaseRun Clone(SortedSpan<int> newPointerSources) => new TilesetRun(TilesetFormat, model, Start, newPointerSources);
   }
}
