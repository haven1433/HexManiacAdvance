using System;
using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.Models.Map {
   /// <summary>What reading a tileset picture gave: the new tile data (4 bits per pixel, 32 bytes per tile) and what is different from the tileset as it is now.</summary>
   public class TilesetPictureImport {
      /// <summary>The tiles to store: 32 bytes each, no palette in it.</summary>
      public byte[] TileData { get; init; }
      public int TileCount { get; init; }
      /// <summary>How many tiles the tileset has now.</summary>
      public int PreviousTileCount { get; init; }
      /// <summary>The numbers of the tiles that are not the same as before (new tiles that are not blank count too), in order.</summary>
      public IReadOnlyList<int> ChangedTiles { get; init; }
      /// <summary>True if the picture was a full colour picture whose pixels were matched to the colours of the tileset's palette.</summary>
      public bool ColorsMatched { get; init; }
      /// <summary>True if the picture was a palette picture whose own colours are not the colours of the tileset's palette (the colour numbers were used, the colours were not).</summary>
      public bool PaletteDiffers { get; init; }
      /// <summary>Pixels of a full colour picture that were see-through and became colour 0, the transparent colour of the game.</summary>
      public int TransparentPixels { get; init; }

      public bool IsSameAsBefore => ChangedTiles.Count == 0 && TileCount == PreviousTileCount;
   }

   /// <summary>
   /// A tileset as one ordinary picture: all tiles in rows of 16 (the layout of the Animated Tiles tab), 8 pixels per tile, so 128 pixels wide.
   /// The picture is a palette picture that carries one of the tileset's palettes. Only the colour NUMBERS of the pixels are the tileset's graphics:
   /// importing a picture gives tiles and never touches any palette.
   /// </summary>
   public static class TilesetPicture {
      public const int TilesPerRow = 16;
      public const int PictureWidth = TilesPerRow * 8;

      public static int RowsFor(int tileCount) => Math.Max(1, (tileCount + TilesPerRow - 1) / TilesPerRow);

      /// <summary>The colour numbers of all pixels, [x, y]. Tiles beyond 'tileCount' (the end of the last row) and tiles that have no data are blank.</summary>
      public static int[,] ToPixels(byte[] tileData, int tileCount) {
         var rows = RowsFor(tileCount);
         var pixels = new int[PictureWidth, rows * 8];
         for (int tile = 0; tile < tileCount; tile++) {
            var (left, top) = ((tile % TilesPerRow) * 8, (tile / TilesPerRow) * 8);
            for (int y = 0; y < 8; y++) {
               for (int x = 0; x < 4; x++) {
                  var at = tile * 32 + y * 4 + x;
                  if (at >= tileData.Length) continue;
                  pixels[left + x * 2, top + y] = tileData[at] & 0xF;
                  pixels[left + x * 2 + 1, top + y] = tileData[at] >> 4;
               }
            }
         }
         return pixels;
      }

      /// <summary>The colours of a palette (as the palette editor holds them: red in the high bits) as 0xRRGGBB, the way every picture export of the program writes them.</summary>
      public static IReadOnlyList<int> ToRgb(IReadOnlyList<short> palette) {
         var result = new int[16];
         for (int i = 0; i < result.Length; i++) {
            var color = i < palette.Count ? palette[i] : (short)0;
            int r = (color >> 10) & 31, g = (color >> 5) & 31, b = color & 31;
            result[i] = ((r * 255 / 31) << 16) | ((g * 255 / 31) << 8) | (b * 255 / 31);
         }
         return result;
      }

      /// <summary>The PNG file of a tileset: its tiles in rows of 16 and one palette.</summary>
      public static byte[] Export(byte[] tileData, int tileCount, IReadOnlyList<short> palette) => PlainPng.EncodeIndexed(ToPixels(tileData, tileCount), ToRgb(palette));

      /// <summary>
      /// Reads a picture file as the new tiles of a tileset.
      /// Nothing here knows about palettes of the ROM except the one that is passed in to match colours with, and nothing is written anywhere.
      /// </summary>
      /// <param name="file">The bytes of the PNG file.</param>
      /// <param name="palette">The palette the export puts in its pictures: the colours a full colour picture is matched with.</param>
      /// <param name="currentTiles">The tile data now (32 bytes per tile). A pixel of a full colour picture keeps its colour number when its colour did not change.</param>
      /// <param name="currentTileCount">How many tiles the tileset has now.</param>
      /// <param name="maxTiles">The most tiles this kind of tileset can have (512 for a tileset of Emerald).</param>
      /// <param name="canGrow">False for a tileset whose number of tiles is not known for sure (tiles stored without compression): the picture then has to have the size of the tileset's own picture and nothing in the tiles past its end.</param>
      public static bool TryRead(byte[] file, IReadOnlyList<short> palette, byte[] currentTiles, int currentTileCount, int maxTiles, out TilesetPictureImport result, out string error, bool canGrow = true) {
         result = null;
         const string untouched = " The tileset was left as it is.";
         if (!PlainPng.TryReadHeader(file, out var header, out error)) {
            error += untouched;
            return false;
         }

         var currentRows = RowsFor(currentTileCount);
         var maxRows = Math.Max(maxTiles / TilesPerRow, currentRows); // (a tileset that is already bigger than its limit may stay as big as it is)
         var rightSize = $"{PictureWidth} x {currentRows * 8} pixels";
         if (header.Width != PictureWidth) {
            error = $"The picture is {header.Width} x {header.Height} pixels, but a tileset picture has to be exactly {PictureWidth} pixels wide (16 tiles of 8 pixels in every row). The picture of this tileset is {rightSize}: export the tileset as a picture to get one with the right size.{untouched}";
            return false;
         }
         if (header.Height % 8 != 0) {
            error = $"The picture is {header.Width} x {header.Height} pixels, but its height has to be a multiple of 8 (a row of tiles is 8 pixels high). The picture of this tileset is {rightSize}.{untouched}";
            return false;
         }
         var rows = header.Height / 8;
         if (!canGrow && rows != currentRows) {
            error = $"The picture has {rows} rows of tiles, but the tiles of this tileset are stored without compression, so it can't change size: its picture has to be exactly {rightSize} ({currentTileCount} tiles).{untouched}";
            return false;
         }
         if (rows > maxRows) {
            error = $"The picture has {rows} rows of tiles ({rows * TilesPerRow} tiles), but this tileset can only have {maxRows * TilesPerRow} tiles ({maxRows} rows, {PictureWidth} x {maxRows * 8} pixels).{untouched}";
            return false;
         }
         if (rows < currentRows) {
            error = $"The picture has only {rows} rows of tiles ({rows * TilesPerRow} tiles), but this tileset has {currentTileCount} tiles ({currentRows} rows, {rightSize}). Importing it would delete tiles that blocks may still draw. Export the tileset as a picture to get one with the right size.{untouched}";
            return false;
         }
         if (!PlainPng.TryDecode(file, out var picture, out error)) {
            error += untouched;
            return false;
         }

         var romColors = ToRgb(palette);
         var indexes = new int[PictureWidth, rows * 8];
         var transparent = 0;
         bool differs = false;
         if (picture.IsIndexed) {
            for (int y = 0; y < picture.Height; y++) {
               for (int x = 0; x < picture.Width; x++) {
                  var index = picture.Indexes[x, y];
                  if (index > 15) {
                     error = $"The picture uses colour number {index} (in tile {(y / 8) * TilesPerRow + x / 8}), but a tileset only has 16 colours, numbered 0 to 15. Reduce the picture to 16 colours, or save it as a normal full colour picture: then its colours are matched with the tileset's palette.{untouched}";
                     return false;
                  }
                  indexes[x, y] = index;
               }
            }
            for (int i = 0; i < Math.Min(16, picture.Palette.Count); i++) {
               if (Quantize(picture.Palette[i]) != Quantize(romColors[i])) differs = true;
            }
         } else {
            var cache = new Dictionary<int, int>();
            for (int y = 0; y < picture.Height; y++) {
               for (int x = 0; x < picture.Width; x++) {
                  var at = (y * picture.Width + x) * 4;
                  if (picture.Rgba[at + 3] < 128) {
                     transparent++;
                     indexes[x, y] = 0; // see-through pixels are the transparent colour of the game
                     continue;
                  }
                  var rgb = (picture.Rgba[at] << 16) | (picture.Rgba[at + 1] << 8) | picture.Rgba[at + 2];
                  var existing = ExistingIndex(currentTiles, currentTileCount, x, y);
                  if (existing >= 0 && Quantize(romColors[existing]) == Quantize(rgb)) {
                     indexes[x, y] = existing; // the colour is the one this pixel has now: not a change (several numbers can have the same colour)
                     continue;
                  }
                  if (!cache.TryGetValue(rgb, out var nearest)) cache[rgb] = nearest = Nearest(romColors, rgb);
                  indexes[x, y] = nearest;
               }
            }
         }

         // the tiles of the whole picture
         var all = new byte[rows * TilesPerRow * 32];
         var lastUsed = -1;
         for (int tile = 0; tile < rows * TilesPerRow; tile++) {
            var (left, top) = ((tile % TilesPerRow) * 8, (tile / TilesPerRow) * 8);
            for (int y = 0; y < 8; y++) {
               for (int x = 0; x < 4; x++) {
                  var value = (byte)(indexes[left + x * 2, top + y] | (indexes[left + x * 2 + 1, top + y] << 4));
                  all[tile * 32 + y * 4 + x] = value;
                  if (value != 0) lastUsed = tile;
               }
            }
         }
         if (!canGrow && lastUsed + 1 > currentTileCount) {
            error = $"The picture has something in tile {lastUsed}, but this tileset only has {currentTileCount} tiles and can't get more (its tiles are stored without compression).{untouched}";
            return false;
         }
         // tiles are never taken away: the tileset keeps its tiles, and grows only as far as the last tile that has something in it
         var count = Math.Max(currentTileCount, lastUsed + 1);
         var data = new byte[count * 32];
         Array.Copy(all, data, data.Length);

         var changed = new List<int>();
         for (int tile = 0; tile < count; tile++) {
            if (tile >= currentTileCount) { // a tile the tileset did not have
               changed.Add(tile);
               continue;
            }
            for (int i = 0; i < 32; i++) {
               var before = tile * 32 + i < (currentTiles?.Length ?? 0) ? currentTiles[tile * 32 + i] : (byte)0;
               if (before == data[tile * 32 + i]) continue;
               changed.Add(tile);
               break;
            }
         }

         result = new TilesetPictureImport {
            TileData = data,
            TileCount = count,
            PreviousTileCount = currentTileCount,
            ChangedTiles = changed,
            ColorsMatched = !picture.IsIndexed,
            PaletteDiffers = differs,
            TransparentPixels = transparent,
         };
         return true;
      }

      /// <summary>The colour number the pixel has in the tile data now, or -1 if the tile is not in it.</summary>
      private static int ExistingIndex(byte[] tiles, int tileCount, int x, int y) {
         var tile = (y / 8) * TilesPerRow + x / 8;
         if (tiles == null || tile >= tileCount) return -1;
         var at = tile * 32 + (y % 8) * 4 + (x % 8) / 2;
         if (at >= tiles.Length) return -1;
         return x % 2 == 0 ? tiles[at] & 0xF : tiles[at] >> 4;
      }

      /// <summary>The colour in 5 bits per channel, which is all the GBA can show.</summary>
      private static int Quantize(int rgb) {
         int r = (rgb >> 16) & 0xFF, g = (rgb >> 8) & 0xFF, b = rgb & 0xFF;
         return (((r * 31 + 127) / 255) << 10) | (((g * 31 + 127) / 255) << 5) | ((b * 31 + 127) / 255);
      }

      /// <summary>The palette entry that looks most like the colour. Colours that are equally close: the lowest number.</summary>
      private static int Nearest(IReadOnlyList<int> colors, int rgb) {
         int best = 0, bestDistance = int.MaxValue;
         for (int i = 0; i < colors.Count; i++) {
            int dr = ((colors[i] >> 16) & 0xFF) - ((rgb >> 16) & 0xFF), dg = ((colors[i] >> 8) & 0xFF) - ((rgb >> 8) & 0xFF), db = (colors[i] & 0xFF) - (rgb & 0xFF);
            var distance = dr * dr + dg * dg + db * db;
            if (distance < bestDistance) (best, bestDistance) = (i, distance);
         }
         return best;
      }
   }
}
