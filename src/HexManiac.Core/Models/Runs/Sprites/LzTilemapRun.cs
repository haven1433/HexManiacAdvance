using System;
using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.Models.Runs.Sprites {
   public class LzTilemapRun : LZRun, ISpriteRun {
      SpriteFormat ISpriteRun.SpriteFormat => new SpriteFormat(Format.BitsPerPixel, Format.TileWidth, Format.TileHeight, null);
      public int Pages => 1;
      public TilemapFormat Format { get; }

      public LzTilemapRun(TilemapFormat format, IDataModel data, int start, IReadOnlyList<int> sources = null) : base(data, start, sources) {
         Format = format;
      }

      public static bool TryParseTilemapFormat(string format, out TilemapFormat tilemapFormat) {
         tilemapFormat = default;
         if (!(format.StartsWith("`lzm") && format.EndsWith("`"))) return false;
         format = format.Substring(4, format.Length - 5);

         // parse the tilesetHint
         string hint = null;
         var pipeIndex = format.IndexOf('|');
         if (pipeIndex != -1) {
            hint = format.Substring(pipeIndex + 1);
            format = format.Substring(0, pipeIndex);
         }

         var parts = format.Split('x');
         if (parts.Length != 3) return false;
         if (!int.TryParse(parts[0], out int bits)) return false;
         if (!int.TryParse(parts[1], out int width)) return false;
         if (!int.TryParse(parts[2], out int height)) return false;

         tilemapFormat = new TilemapFormat(bits, width, height, hint);
         return true;
      }

      public int[,] GetPixels(IDataModel model, int page) {
         var result = new int[Format.TileWidth * 8, Format.TileHeight * 8];

         var mapData = Decompress(model, Start);
         var sheet = new int[Format.TileWidth * 8, Format.TileHeight * 8];
         var tilesetAddress = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, Format.MatchingTileset);
         var tileset = model.GetNextRun(tilesetAddress) as LzTilesetRun;
         // TODO handle null tileset
         var tiles = Decompress(model, tileset.Start);

         var tileSize = tileset.Format.BitsPerPixel * 8;

         for (int y = 0; y < Format.TileHeight; y++) {
            var yStart = y * 8;
            for (int x = 0; x < Format.TileWidth; x++) {
               var map = mapData.ReadMultiByteValue((Format.TileWidth * y + x) * 2, 2);
               var tileStart = (map & 0x3FF) * tileSize;
               var pixels = SpriteRun.GetPixels(tiles, tileStart, 1, 1); // TODO cache this during this method so we don't load the same tile more than once
               var hFlip = (map >> 10) & 0x1;
               var vFlip = (map >> 11) & 0x1;
               var pal = (map >> 12) & 0xF;
               pal <<= 4;
               var xStart = x * 8;
               for (int yy = 0; yy < 8; yy++) {
                  for (int xx = 0; xx < 8; xx++) {
                     var inX = hFlip == 1 ? 7 - xx : xx;
                     var inY = vFlip == 1 ? 7 - yy : yy;
                     result[xStart + xx, yStart + yy] = pixels[inX, inY] + pal;
                  }
               }
            }
         }

         return result;
      }

      public ISpriteRun SetPixels(IDataModel model, ModelDelta token, int page, int[,] pixels) {
         throw new NotImplementedException();
      }

      protected override BaseRun Clone(IReadOnlyList<int> newPointerSources) => new LzTilemapRun(Format, Model, Start, newPointerSources);
   }
}
