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

      public static int[,] ConvertToPixels(byte[] data, uint width, uint height, int bitDepth) {
         Debug.Assert(bitDepth == 1 || bitDepth == 2 || bitDepth == 4 || bitDepth == 8);
         int[,] pixels = new int[width, height];
         if (bitDepth == 1) {
            Debug.Assert((width / 8 + 1) * height == data.Length, "Decompressed data size from IDAT does not match width and height from IDHR.");
            for (var i = 0; i < data.Length; i++) {
               if (i % (width / 8 + 1) == 0) {
                  continue;
               }
               var x = ((i - 1) * 8) % (width + 8);
               var y = i / (width / 8 + 1);
               pixels[x, y] = (data[i] >> 7) & 1;
               pixels[x + 1, y] = (data[i] >> 6) & 1;
               pixels[x + 2, y] = (data[i] >> 5) & 1;
               pixels[x + 3, y] = (data[i] >> 4) & 1;
               pixels[x + 4, y] = (data[i] >> 3) & 1;
               pixels[x + 5, y] = (data[i] >> 2) & 1;
               pixels[x + 6, y] = (data[i] >> 1) & 1;
               pixels[x + 7, y] = (data[i] >> 0) & 1;
            }
         } else if (bitDepth == 2) {
            Debug.Assert((width / 4 + 1) * height == data.Length, "Decompressed data size from IDAT does not match width and height from IDHR.");
            for (var i = 0; i < data.Length; i++) {
               if (i % (width / 4 + 1) == 0) {
                  continue;
               }
               var x = ((i - 1) * 4) % (width + 4);
               var y = i / (width / 4 + 1);
               pixels[x, y] = (data[i] >> 6) & 3;
               pixels[x + 1, y] = (data[i] >> 4) & 3;
               pixels[x + 2, y] = (data[i] >> 2) & 3;
               pixels[x + 3, y] = (data[i] >> 0) & 3;
            }
         } else if (bitDepth == 4) {
            Debug.Assert((width / 2 + 1) * height == data.Length, "Decompressed data size from IDAT does not match width and height from IDHR.");
            for (var i = 0; i < data.Length; i++) {
               if (i % (width / 2 + 1) == 0) {
                  continue;
               }
               var x = ((i - 1) * 2) % (width + 2);
               var y = i / (width / 2 + 1);
               pixels[x, y] = (data[i] >> 4) & 15;
               pixels[x + 1, y] = (data[i] >> 0) & 15;
            }
         } else if (bitDepth == 8) {
            Debug.Assert((width + 1) * height == data.Length, "Decompressed data size from IDAT does not match width and height from IDHR.");
            for (var i = 0; i < data.Length; i++) {
               if (i % (width + 1) == 0) {
                  continue;
               }
               var x = (i - 1) % (width + 1);
               var y = i / (width + 1);
               pixels[x, y] = data[i] & 255;
            }
         }
         return pixels;
      }

      public static byte[] ConvertFromPixels(int[,] pixels, int bitDepth) {
         var width = pixels.GetLength(0);
         var height = pixels.GetLength(1);
         byte[] data;
         var i = 0;

         if (bitDepth == 4) {
            Debug.Assert(width % 2 == 0, "Width must be even.");
            data = new byte[(width / 2 + 1) * height];
            for (var y = 0; y < height; y++) {
               data[i++] = 0;
               for (var x = 0; x < width; x += 2) {
                  var pixel1 = pixels[x, y] % 16;
                  var pixel2 = pixels[x + 1, y] % 16;
                  data[i++] = (byte)((pixel1 << 4) | pixel2);
               }
            }
         } else if (bitDepth == 8) {
            data = new byte[(width + 1) * height];
            for (var y = 0; y < height; y++) {
               data[i++] = 0;
               for (var x = 0; x < width; x++) {
                  data[i++] = (byte)pixels[x, y];
               }
            }
         } else {
            throw new ArgumentException();
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
