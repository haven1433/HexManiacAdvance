using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using System;

namespace HavenSoft.HexManiac.Core.Models {
   /// <summary>
   /// Professor Birch's bag: the picture on the starter-choosing screen of Emerald (and the games built from it), found by reading the game's code, never by an address.
   ///
   /// The screen's setup function decompresses three things in a row into the video memory: the tiles (LZ, 4 bits per pixel), the bag's tilemap (to the map at 0x06003000)
   /// and the grass tilemap (to the map at 0x06003800), and later loads a palette of 64 bytes (colours 0-31, 'movs r1,#0 ; movs r2,#0x40' then a call).
   /// <see cref="TryFind"/> follows that: the load of 0x06003000 with the tilemap pointer just before it, the tiles pointer before that, and the palette pointer
   /// (either a literal, or a base pointer plus an offset when the compiler shares one base register for a whole file's tables) near the 64-byte load.
   /// A ROM that has no such code (FireRed, Ruby, a game with a changed screen) gives no result and the caller uses the built-in bag picture.
   /// </summary>
   public static class BirchBag {
      private const int BagMap = 0x06003000, GrassMap = 0x06003800;

      /// <summary>Where the pieces of the picture are in the ROM (offsets in the file).</summary>
      public readonly struct Location {
         public int Tiles { get; }
         public int Tilemap { get; }
         public int Palette { get; }
         public Location(int tiles, int tilemap, int palette) => (Tiles, Tilemap, Palette) = (tiles, tilemap, palette);
      }

      /// <summary>The colour of the picture's pixels: red, green and blue of 0-31 each, or null for the see-through colour 0 (the grass behind the bag).</summary>
      public readonly struct Pixel {
         public int Red { get; }
         public int Green { get; }
         public int Blue { get; }
         public Pixel(int red, int green, int blue) => (Red, Green, Blue) = (red, green, blue);
      }

      #region Finding the data

      public static bool TryFind(byte[] data, out Location location) {
         location = default;
         try {
            if (data == null || data.Length < 0x1000) return false;
            for (int literal = 0; literal + 4 <= data.Length; literal += 4) {
               if (IntroThumb.Word(data, literal) != BagMap) continue;
               foreach (var load in IntroThumb.FindLiteralLoads(data, literal)) {
                  if (TryFindAround(data, load, out location)) return true;
               }
            }
         } catch (Exception) {
            // decoration only
         }
         location = default;
         return false;
      }

      private static int PointerLiteral(byte[] data, int instruction, int register) {
         var half = IntroThumb.Half(data, instruction);
         if ((half & 0xF800) != 0x4800 || ((half >> 8) & 7) != register) return -1;
         var value = IntroThumb.Word(data, IntroThumb.LiteralAddress(data, instruction));
         var address = value - IntroThumb.RomBase;
         return value >= IntroThumb.RomBase + 0x200 && address < data.Length ? address : -1;
      }

      private static bool IsCompressed(byte[] data, int address, int minLength, int maxLength, out byte[] result) {
         result = null;
         if (address < 0 || address + 8 > data.Length || data[address] != 0x10) return false;
         var length = data[address + 1] | (data[address + 2] << 8) | (data[address + 3] << 16);
         if (length < minLength || length > maxLength) return false;
         try { result = LZRun.Decompress(data, address); } catch (Exception) { result = null; }
         return result != null && result.Length == length;
      }

      private static bool TryFindAround(byte[] data, int load, out Location location) {
         location = default;
         // ldr r1, =0x06003000 is the destination; the tilemap is the one 'ldr r0' right before, and the tiles are the one a few instructions before that
         if (((IntroThumb.Half(data, load) >> 8) & 7) != 1) return false;
         var tilemap = PointerLiteral(data, load - 2, 0);
         if (tilemap < 0 || !IsCompressed(data, tilemap, 0x100, 0x1000, out _)) return false;
         int tiles = -1;
         for (int p = load - 4; p >= load - 14 && tiles < 0; p -= 2) {
            var candidate = PointerLiteral(data, p, 0);
            if (candidate >= 0 && IsCompressed(data, candidate, 0x800, 0x8000, out _)) tiles = candidate;
         }
         if (tiles < 0) return false;
         // the grass tilemap follows: ldr r1, =0x06003800 and an ldr r0 next to it
         var grass = false;
         for (int p = load + 2; p <= load + 12 && !grass; p += 2) {
            var literal = IntroThumb.LiteralAddress(data, p);
            grass = literal >= 0 && IntroThumb.Word(data, literal) == GrassMap && ((IntroThumb.Half(data, p) >> 8) & 7) == 1;
         }
         if (!grass) return false;
         var palette = FindPalette(data, load);
         if (palette < 0) return false;
         var candidateLocation = new Location(tiles, tilemap, palette);
         if (Render(data, candidateLocation, out _, out _, out _) == null) return false;
         location = candidateLocation;
         return true;
      }

      /// <summary>The palette: the 64-byte 'LoadPalette(palette, 0, 0x40)' that follows the setup of the tiles within the same function.</summary>
      private static int FindPalette(byte[] data, int start) {
         for (int p = start + 4; p < start + 0x200; p += 2) {
            // movs r1, #0 and movs r2, #0x40, in either order, next to each other
            var a = IntroThumb.Half(data, p);
            var b = IntroThumb.Half(data, p + 2);
            if (!((a == 0x2100 && b == 0x2240) || (a == 0x2240 && b == 0x2100))) continue;
            for (int q = p - 12; q <= p + 8; q += 2) {
               var direct = PointerLiteral(data, q, 0);
               if (direct >= 0 && direct + 0x40 <= data.Length && data[direct] != 0x10) return direct;
               // mov r0, rB (lsls r0, rB, #0) and adds r0, #imm: a base pointer in a register plus the offset of the palette
               var half = IntroThumb.Half(data, q);
               if ((half & 0xFFC7) != 0x0000 || half == 0) continue;
               var baseRegister = (half >> 3) & 7;
               for (int r = q + 2; r <= q + 10; r += 2) {
                  var add = IntroThumb.Half(data, r);
                  if ((add & 0xFF00) != 0x3000) continue;
                  for (int s = q - 2; s >= start; s -= 2) {
                     var baseAddress = PointerLiteral(data, s, baseRegister);
                     if (baseAddress < 0) continue;
                     var address = baseAddress + (add & 0xFF);
                     if (address + 0x40 <= data.Length) return address;
                     break;
                  }
               }
            }
         }
         return -1;
      }

      #endregion

      #region Drawing it

      /// <summary>
      /// The whole screen of the bag (256 pixels wide, as many rows as the tilemap has), with null for the see-through pixels. Null if the data is not a tilemap and tiles.
      /// Tilemap entries: 10 bits tile, 1 bit horizontal flip, 1 bit vertical flip, 4 bits palette. Only the first two palettes (32 colours) are read.
      /// </summary>
      public static Pixel?[,] Render(byte[] data, Location location, out int width, out int height, out int unused) {
         width = height = unused = 0;
         if (!IsCompressed(data, location.Tilemap, 0x100, 0x1000, out var map)) return null;
         if (!IsCompressed(data, location.Tiles, 0x800, 0x8000, out var tiles)) return null;
         if (location.Palette < 0 || location.Palette + 0x40 > data.Length) return null;
         var palette = new Pixel[32];
         for (int i = 0; i < 32; i++) {
            var color = data[location.Palette + i * 2] | (data[location.Palette + i * 2 + 1] << 8);
            palette[i] = new Pixel(color & 31, (color >> 5) & 31, (color >> 10) & 31);
         }
         var rows = map.Length / 64;
         if (rows < 1) return null;
         width = 256;
         height = rows * 8;
         var result = new Pixel?[width, height];
         var visible = 0;
         for (int ty = 0; ty < rows; ty++) {
            for (int tx = 0; tx < 32; tx++) {
               var entry = map[(ty * 32 + tx) * 2] | (map[(ty * 32 + tx) * 2 + 1] << 8);
               var tile = entry & 0x3FF;
               var flipX = (entry & 0x400) != 0;
               var flipY = (entry & 0x800) != 0;
               var paletteBank = entry >> 12;
               if (paletteBank > 1 || tile * 32 + 32 > tiles.Length) continue;
               for (int y = 0; y < 8; y++) {
                  for (int x = 0; x < 8; x++) {
                     var sx = flipX ? 7 - x : x;
                     var sy = flipY ? 7 - y : y;
                     var b = tiles[tile * 32 + sy * 4 + sx / 2];
                     var index = (sx & 1) != 0 ? b >> 4 : b & 15;
                     if (index == 0) continue;
                     result[tx * 8 + x, ty * 8 + y] = palette[paletteBank * 16 + index];
                     visible++;
                  }
               }
            }
         }
         // a real bag is a large patch of colour; a few stray pixels mean this was not the picture
         return visible >= 1000 ? result : null;
      }

      /// <summary>
      /// The bag as a small picture: the screen cut down to the rectangle that has something in it, then every 2x2 block of pixels turned into one (the average colour of the block's
      /// coloured pixels, see-through if fewer than two of the four have colour). Pixels are in the program's 15-bit format (red in the high bits); <paramref name="transparent"/> marks the see-through ones.
      /// Null if the ROM has no bag.
      /// </summary>
      public static short[] Draw(byte[] data, short transparent, out int width, out int height) {
         width = height = 0;
         try {
            if (!TryFind(data, out var location)) return null;
            var screen = Render(data, location, out var screenWidth, out var screenHeight, out _);
            if (screen == null) return null;
            int left = screenWidth, right = -1, top = screenHeight, bottom = -1;
            for (int y = 0; y < screenHeight; y++) {
               for (int x = 0; x < screenWidth; x++) {
                  if (screen[x, y] == null) continue;
                  left = Math.Min(left, x); right = Math.Max(right, x);
                  top = Math.Min(top, y); bottom = Math.Max(bottom, y);
               }
            }
            if (right < left || bottom < top) return null;
            left &= ~1; top &= ~1; // whole 2x2 blocks
            width = (right - left) / 2 + 1;
            height = (bottom - top) / 2 + 1;
            var pixels = new short[width * height];
            for (int by = 0; by < height; by++) {
               for (int bx = 0; bx < width; bx++) {
                  int count = 0, red = 0, green = 0, blue = 0;
                  for (int dy = 0; dy < 2; dy++) {
                     for (int dx = 0; dx < 2; dx++) {
                        int x = left + bx * 2 + dx, y = top + by * 2 + dy;
                        if (x >= screenWidth || y >= screenHeight || screen[x, y] is not Pixel pixel) continue;
                        count++; red += pixel.Red; green += pixel.Green; blue += pixel.Blue;
                     }
                  }
                  pixels[by * width + bx] = count < 2 ? transparent : Pack((red + count / 2) / count, (green + count / 2) / count, (blue + count / 2) / count, transparent);
               }
            }
            return pixels;
         } catch (Exception) {
            width = height = 0;
            return null;
         }
      }

      private static short Pack(int red, int green, int blue, short transparent) {
         var color = (short)((red << 10) | (green << 5) | blue);
         return color == transparent ? (short)(color ^ 1) : color; // never the see-through colour itself
      }

      #endregion
   }
}
