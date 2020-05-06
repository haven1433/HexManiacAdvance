using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace HavenSoft.HexManiac.Core.Models.Runs.Sprites {
   public class LzTilemapRun : LZRun, ISpriteRun {
      SpriteFormat ISpriteRun.SpriteFormat {
         get {
            string hint = null;
            var address = Model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, Format.MatchingTileset);
            if (address >= 0 && address < Model.Count) {
               var tileset = Model.GetNextRun(address) as ISpriteRun;
               if (tileset == null) tileset = Model.GetNextRun(arrayTilesetAddress) as ISpriteRun;
               if (tileset != null) hint = tileset.SpriteFormat.PaletteHint;
            }

            return new SpriteFormat(Format.BitsPerPixel, Format.TileWidth, Format.TileHeight, hint);
         }
      }
      public int Pages => 1;
      public TilemapFormat Format { get; }

      public override string FormatString =>
         $"`lzm{Format.BitsPerPixel}x{Format.TileWidth}x{Format.TileHeight}|{Format.MatchingTileset}" +
         (Format.TilesetTableMember != null ? "|" + Format.TilesetTableMember : string.Empty) +
         "`";

      public LzTilemapRun(TilemapFormat format, IDataModel data, int start, IReadOnlyList<int> sources = null) : base(data, start, sources) {
         Format = format;
      }

      public static bool TryParseTilemapFormat(string format, out TilemapFormat tilemapFormat) {
         tilemapFormat = default;
         if (!(format.StartsWith("`lzm") && format.EndsWith("`"))) return false;
         format = format.Substring(4, format.Length - 5);

         // parse the tilesetHint
         string hint = null, tableMember = null;
         var pipeIndex = format.IndexOf('|');
         if (pipeIndex != -1) {
            hint = format.Substring(pipeIndex + 1);
            format = format.Substring(0, pipeIndex);
            pipeIndex = hint.IndexOf('|');
            if (pipeIndex != -1) {
               tableMember = hint.Substring(pipeIndex + 1);
               hint = hint.Substring(0, pipeIndex);
            }
         }

         var parts = format.Split('x');
         if (parts.Length != 3) return false;
         if (!int.TryParse(parts[0], out int bits)) return false;
         if (!int.TryParse(parts[1], out int width)) return false;
         if (!int.TryParse(parts[2], out int height)) return false;

         tilemapFormat = new TilemapFormat(bits, width, height, hint, tableMember);
         return true;
      }

      public int[,] GetPixels(IDataModel model, int page) {
         var result = new int[Format.TileWidth * 8, Format.TileHeight * 8];

         var mapData = Decompress(model, Start);
         var tilesetAddress = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, Format.MatchingTileset);
         var tileset = model.GetNextRun(tilesetAddress) as LzTilesetRun;
         if (tileset == null) tileset = model.GetNextRun(arrayTilesetAddress) as LzTilesetRun;
         
         if (tileset == null) return result;

         var tiles = Decompress(model, tileset.Start);

         var tileSize = tileset.Format.BitsPerPixel * 8;

         for (int y = 0; y < Format.TileHeight; y++) {
            var yStart = y * 8;
            for (int x = 0; x < Format.TileWidth; x++) {
               var map = mapData.ReadMultiByteValue((Format.TileWidth * y + x) * 2, 2);
               var tile = map & 0x3FF;

               var tileStart = tile * tileSize;
               var pixels = SpriteRun.GetPixels(tiles, tileStart, 1, 1, Format.BitsPerPixel); // TODO cache this during this method so we don't load the same tile more than once
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

         var tileData = Tilize(pixels);
         var tiles = GetUniqueTiles(tileData);
         var tilesetAddress = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, Format.MatchingTileset);
         var tileset = model.GetNextRun(tilesetAddress) as LzTilesetRun;
         if (tileset == null) tileset = model.GetNextRun(arrayTilesetAddress) as LzTilesetRun;
         tileset.SetPixels(model, token, tiles);
         if (tiles.Length > 0x400) {
            // TODO fail: too many unique tiles
            return this;
         }
         var mapData = Decompress(model, Start);

         var tileWidth = tileData.GetLength(0);
         var tileHeight = tileData.GetLength(1);
         for (int y = 0; y < tileHeight; y++) {
            for (int x = 0; x < tileWidth; x++) {
               var i = y * tileWidth + x;
               var (tile, paletteIndex) = tileData[x, y];
               var (tileIndex, matchType) = FindMatch(tile, tiles);
               var mapping = PackMapping(paletteIndex, matchType, tileIndex);
               mapData[i * 2 + 0] = (byte)mapping;
               mapData[i * 2 + 1] = (byte)(mapping >> 8);
            }
         }

         var newModelData = Compress(mapData, 0, mapData.Length);
         var newRun = (ISpriteRun)model.RelocateForExpansion(token, this, newModelData.Count);
         for (int i = 0; i < newModelData.Count; i++) token.ChangeData(model, newRun.Start + i, newModelData[i]);
         for (int i = newModelData.Count; i < Length; i++) token.ChangeData(model, newRun.Start + i, 0xFF);
         newRun = new LzTilemapRun(Format, model, newRun.Start, newRun.PointerSources);
         model.ObserveRunWritten(token, newRun);
         return newRun;
      }

      private int PackMapping(int paletteIndex, TileMatchType matchType, int tileIndex) {
         var hFlip = (matchType == TileMatchType.HFlip || matchType == TileMatchType.BFlip) ? 1 : 0;
         var vFlip = (matchType == TileMatchType.VFlip || matchType == TileMatchType.BFlip) ? 1 : 0;
         return (paletteIndex << 12) | (vFlip << 11) | (hFlip << 10) | tileIndex;
      }

      private (int[,] pixels,int palette)[,] Tilize(int[,] pixels) {
         var width = pixels.GetLength(0);
         var tileWidth = width / 8;
         var height = pixels.GetLength(1);
         var tileHeight = height / 8;
         var result = new (int[,], int)[tileWidth, tileHeight];
         for(int y = 0; y < tileHeight; y++) {
            var yStart = y * 8;
            for(int x = 0; x < tileWidth; x++) {
               var xStart = x * 8;
               var palette = pixels[xStart, yStart] >> 4;
               var tile = new int[8, 8];
               for(int yy = 0; yy < 8; yy++) {
                  for(int xx = 0; xx < 8; xx++) {
                     tile[xx, yy] = pixels[xStart + xx, yStart + yy] % 16;
                  }
               }
               result[x, y] = (tile, palette);
            }
         }
         return result;
      }

      // TODO include an append mode, where existing unique tiles from an existing tileset are passed in.
      private int[][,] GetUniqueTiles((int[,] pixels, int palette)[,] tiles) {
         var tileWidth = tiles.GetLength(0);
         var tileHeight = tiles.GetLength(1);
         var result = new List<int[,]>();

         for (int y = 0; y < tileHeight; y++) {
            for (int x = 0; x < tileWidth; x++) {
               var pixels = tiles[x, y].pixels;
               if (result.Any(tile => TilesMatch(tile, pixels) != TileMatchType.None)) continue;
               result.Add(pixels);
            }
         }

         return result.ToArray();
      }

      // TODO mark the tiles as 'matching' if they're identical after horizantol/vertical flipping
      private TileMatchType TilesMatch(int[,] a, int[,] b) {
         Debug.Assert(a.GetLength(0) == 8);
         Debug.Assert(a.GetLength(1) == 8);
         Debug.Assert(b.GetLength(0) == 8);
         Debug.Assert(b.GetLength(1) == 8);

         for (int y = 0; y < 8; y++) {
            for (int x = 0; x < 8; x++) {
               if (a[x, y] != b[x, y]) return TileMatchType.None;
            }
         }

         return TileMatchType.Normal;
      }

      private (int, TileMatchType) FindMatch(int[,] tile, int[][,] collection) {
         for(int i = 0; i < collection.Length; i++) {
            var match = TilesMatch(tile, collection[i]);
            if (match == TileMatchType.None) continue;
            return (i, match);
         }
         return (-1, default);
      }

      private enum TileMatchType { None, Normal, HFlip, VFlip, BFlip }

      private int arrayTilesetAddress;
      public void FindMatchingTileset(IDataModel model) {
         var hint = Format.MatchingTileset;
         IFormattedRun hintRun;
         if (hint != null) {
            hintRun = model.GetNextRun(model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, hint));
         } else {
            hintRun = model.GetNextRun(PointerSources[0]);
         }

         // easy case: the hint is the address of a tileset
         if (hintRun is LzTilesetRun) {
            arrayTilesetAddress = hintRun.Start;
            return;
         }

         // harder case: the hint is a table
         if (!(hintRun is ITableRun hintTableRun)) return;
         var tilemapPointer = PointerSources[0];
         var tilemapTable = model.GetNextRun(tilemapPointer) as ITableRun;
         if (tilemapTable == null) return;
         int tilemapIndex = (tilemapPointer - tilemapTable.Start) / tilemapTable.ElementLength;

         // get which element of the table has the tileset
         var segmentOffset = 0;
         for (int i = 0; i < tilemapTable.ElementContent.Count; i++) {
            if (tilemapTable.ElementContent[i] is ArrayRunPointerSegment segment) {
               if (Format.TilesetTableMember == null || segment.Name == Format.TilesetTableMember) {
                  if (LzTilesetRun.TryParseTilesetFormat(segment.InnerFormat, out var _)) {
                     var source = tilemapTable.Start + tilemapTable.ElementLength * tilemapIndex + segmentOffset;
                     if (model.GetNextRun(model.ReadPointer(source)) is LzTilesetRun tilesetRun) {
                        arrayTilesetAddress = tilesetRun.Start;
                        return;
                     }
                  }
               }
            }
            segmentOffset += tilemapTable.ElementContent[i].Length;
         }
      }

      protected override BaseRun Clone(IReadOnlyList<int> newPointerSources) => new LzTilemapRun(Format, Model, Start, newPointerSources);
   }
}
