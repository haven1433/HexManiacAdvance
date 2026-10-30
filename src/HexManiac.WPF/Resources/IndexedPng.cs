using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

// Edited from code provided by wet blanket#6152 from discord
// based on https://github.com/EliotJones/BigGustave/tree/master/src/BigGustave
// massive thanks!

namespace HexManiac.WPF.Resources {
   public class IndexedPng {
      public static void Save(string filename, int[,] pixels, IReadOnlyList<short> palette)
         => IndexedPngWriter.Save(filename, pixels, palette);

      public static (int[,] pixels, IReadOnlyList<short> palette) Load(string filename)
         => IndexedPngReader.Load(filename);

      public static IReadOnlyList<short> ConvertToPalette(byte[] data) {
         Debug.Assert(data.Length % 3 == 0);
         var palette = new List<short>();
         for (var i = 0 ; i < data.Length; i += 3) {
            var (r, g, b) = (
               (int)Math.Round(data[i + 0] * 31 / 255d),
               (int)Math.Round(data[i + 1] * 31 / 255d),
               (int)Math.Round(data[i + 2] * 31 / 255d)
            );
            palette.Add((short)((r << 10)| (g << 5) | (b << 0)));
         }
         return palette;
      }

      public static byte[] ConvertFromPalette(IReadOnlyList<short> palette) {
         var data = new byte[3 * palette.Count];
         var i = 0;
         foreach (var color in palette) {
            data[i++] = (byte)(((color >> 10) & 31) * 255 / 31);
            data[i++] = (byte)(((color >> 5) & 31) * 255 / 31);
            data[i++] = (byte)(((color >> 0) & 31) * 255 / 31);
         }
         return data;
      }

      private static readonly (int xStart, int yStart, int xStep, int yStep)[] Adam7Passes = {
         (0, 0, 8, 8), (4, 0, 8, 8), (0, 4, 4, 8), (2, 0, 4, 4), (0, 2, 2, 4), (1, 0, 2, 2), (0, 1, 1, 2),
      };

      /// <summary>
      /// Turns the decompressed IDAT data into palette indexes.
      /// Every row starts with a filter byte and holds ceil(width * bitDepth / 8) bytes, whatever the width is.
      /// The five PNG filters (None, Sub, Up, Average, Paeth) are undone, and interlaced (Adam7) images are put back in order.
      /// </summary>
      public static int[,] ConvertToPixels(byte[] data, uint width, uint height, int bitDepth, bool interlaced = false) {
         if (bitDepth != 1 && bitDepth != 2 && bitDepth != 4 && bitDepth != 8) {
            throw new PngArgumentException($"Bit depth ({bitDepth}) is invalid for a palette indexed image.");
         }
         if (width == 0 || height == 0 || (long)width * height > 1L << 28) {
            throw new PngArgumentException($"Image size ({width} x {height}) is not supported.");
         }
         int[,] pixels = new int[width, height];
         var position = 0;
         if (!interlaced) {
            ReadPass(data, ref position, pixels, 0, 0, 1, 1, (int)width, (int)height, bitDepth);
         } else {
            foreach (var (xStart, yStart, xStep, yStep) in Adam7Passes) {
               var passWidth = ((int)width - xStart + xStep - 1) / xStep;
               var passHeight = ((int)height - yStart + yStep - 1) / yStep;
               if (passWidth <= 0 || passHeight <= 0) continue; // small images skip some passes entirely
               ReadPass(data, ref position, pixels, xStart, yStart, xStep, yStep, passWidth, passHeight, bitDepth);
            }
         }
         return pixels;
      }

      private static void ReadPass(byte[] data, ref int position, int[,] pixels, int xStart, int yStart, int xStep, int yStep, int passWidth, int passHeight, int bitDepth) {
         var rowBytes = (passWidth * bitDepth + 7) / 8;
         var previous = new byte[rowBytes];
         var current = new byte[rowBytes];
         var mask = (1 << bitDepth) - 1;
         for (var passY = 0; passY < passHeight; passY++) {
            if (position + 1 + rowBytes > data.Length) {
               throw new PngArgumentException("Decompressed data size from IDAT does not match width and height from IHDR.");
            }
            var filter = data[position];
            Array.Copy(data, position + 1, current, 0, rowBytes);
            position += 1 + rowBytes;
            Unfilter(filter, current, previous);
            var y = yStart + passY * yStep;
            for (var passX = 0; passX < passWidth; passX++) {
               var bit = passX * bitDepth;
               pixels[xStart + passX * xStep, y] = (current[bit >> 3] >> (8 - bitDepth - (bit & 7))) & mask;
            }
            var swap = previous; previous = current; current = swap;
         }
      }

      // all indexed images have one byte per pixel or less, so the 'previous pixel' for the filters is always the previous byte
      private static void Unfilter(byte filter, byte[] row, byte[] previous) {
         switch (filter) {
            case 0: break;
            case 1: // Sub
               for (var i = 1; i < row.Length; i++) row[i] = (byte)(row[i] + row[i - 1]);
               break;
            case 2: // Up
               for (var i = 0; i < row.Length; i++) row[i] = (byte)(row[i] + previous[i]);
               break;
            case 3: // Average
               for (var i = 0; i < row.Length; i++) row[i] = (byte)(row[i] + (((i > 0 ? row[i - 1] : 0) + previous[i]) >> 1));
               break;
            case 4: // Paeth
               for (var i = 0; i < row.Length; i++) {
                  int a = i > 0 ? row[i - 1] : 0, b = previous[i], c = i > 0 ? previous[i - 1] : 0;
                  var p = a + b - c;
                  var (pa, pb, pc) = (Math.Abs(p - a), Math.Abs(p - b), Math.Abs(p - c));
                  row[i] = (byte)(row[i] + (pa <= pb && pa <= pc ? a : pb <= pc ? b : c));
               }
               break;
            default:
               throw new PngArgumentException($"Unknown scanline filter type ({filter}).");
         }
      }

      public static byte[] ConvertFromPixels(int[,] pixels, int bitDepth) {
         var width = pixels.GetLength(0);
         var height = pixels.GetLength(1);
         if (bitDepth != 1 && bitDepth != 2 && bitDepth != 4 && bitDepth != 8) throw new ArgumentException($"Bit depth ({bitDepth}) is invalid for a palette indexed image.");
         var rowBytes = (width * bitDepth + 7) / 8; // when the width doesn't fill the last byte, the unused low bits stay 0
         var mask = (1 << bitDepth) - 1;
         var data = new byte[(rowBytes + 1) * height];
         var i = 0;
         for (var y = 0; y < height; y++) {
            data[i++] = 0; // filter type: none
            for (var x = 0; x < width; x++) {
               var bit = x * bitDepth;
               data[i + (bit >> 3)] |= (byte)((pixels[x, y] & mask) << (8 - bitDepth - (bit & 7)));
            }
            i += rowBytes;
         }
         return data;
      }
   }

   public static class Png {
      public static readonly byte[] Signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

      public static int Adler32(IEnumerable<byte> data) {
         var s1 = 1;
         var s2 = 0;

         foreach (var b in data) {
            s1 = (s1 + b) % 65521;
            s2 = (s1 + s2) % 65521;
         }

         return (s2 << 16) | s1;
      }

      public static byte[] ZLibDecompress(byte[] compressedData) {
         using (var result = new MemoryStream()) {
            using (var decompressor = new ZLibStream(new MemoryStream(compressedData), CompressionMode.Decompress)) {
               decompressor.CopyTo(result);
               decompressor.Close();
            }
            return result.ToArray();
         }
      }

      public static byte[] ZLibCompress(byte[] uncompressedData) {
         using (var result = new MemoryStream()) {
            using (var compressor = new ZLibStream(result, CompressionLevel.Optimal)) {
               compressor.Write(uncompressedData, 0, uncompressedData.Length);
               compressor.Close();
            }
            return result.ToArray();
         }
      }
   }

   public class PngArgumentException : ArgumentException {
      public PngArgumentException(string text) : base(text) { }
   }
}
