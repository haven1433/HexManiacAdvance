using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace HavenSoft.HexManiac.Core.Models {
   public enum PngColorType { Gray = 0, Rgb = 2, Indexed = 3, GrayAlpha = 4, Rgba = 6 }

   /// <summary>The first thing a PNG file says about its picture. It can be read without unpacking the picture itself.</summary>
   public class PngHeader {
      public int Width { get; init; }
      public int Height { get; init; }
      public int BitDepth { get; init; }
      public PngColorType ColorType { get; init; }
      public bool Interlaced { get; init; }
   }

   /// <summary>
   /// A picture read from a PNG file.
   /// A palette picture (<see cref="IsIndexed"/>) keeps its colour numbers and the colours the file gives them;
   /// every other kind of PNG (grey, full colour, with or without transparency, 8 or 16 bits per channel) is turned into 8-bit red/green/blue/alpha.
   /// </summary>
   public class PngPicture {
      public int Width { get; init; }
      public int Height { get; init; }
      public bool IsIndexed { get; init; }

      /// <summary>[x, y] colour numbers. Only for palette pictures.</summary>
      public int[,] Indexes { get; init; }

      /// <summary>The colours of the file's palette (0xRRGGBB each), as many as the file lists. Only for palette pictures.</summary>
      public IReadOnlyList<int> Palette { get; init; }

      /// <summary>Four bytes (red, green, blue, alpha) per pixel, row by row. Only for pictures that are not palette pictures.</summary>
      public byte[] Rgba { get; init; }
   }

   /// <summary>
   /// A small PNG reader and writer that does not need any windowing code, so the same code runs in the app and in the tests.
   /// The reader understands every PNG a picture program can save (all colour types and bit depths, interlaced or not, any scanline filter, several data chunks)
   /// and refuses damaged files with a sentence that can be shown to the user. The writer makes a palette picture (4 bits per pixel for 16 colours or fewer).
   /// </summary>
   public static class PlainPng {
      private static readonly byte[] Signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

      public static bool HasPngSignature(byte[] file) {
         if (file == null || file.Length < Signature.Length) return false;
         for (int i = 0; i < Signature.Length; i++) if (file[i] != Signature[i]) return false;
         return true;
      }

      #region Reading

      private record Chunk(string Type, int Start, int Length);

      public static bool TryReadHeader(byte[] file, out PngHeader header, out string error) {
         header = null;
         return TryReadChunks(file, out var chunks, out error) && TryParseHeader(file, chunks, out header, out error);
      }

      /// <summary>
      /// Reads the whole picture. A picture with more than <paramref name="maxPixels"/> pixels is refused (before it is unpacked).
      /// </summary>
      public static bool TryDecode(byte[] file, out PngPicture picture, out string error, int maxPixels = 1 << 24) {
         picture = null;
         if (!TryReadChunks(file, out var chunks, out error)) return false;
         if (!TryParseHeader(file, chunks, out var header, out error)) return false;
         if ((long)header.Width * header.Height > maxPixels) {
            error = $"The picture is too big ({header.Width} x {header.Height} pixels).";
            return false;
         }

         byte[] paletteChunk = null, transparency = null;
         var data = new MemoryStream();
         foreach (var chunk in chunks) {
            if (chunk.Type == "PLTE" && paletteChunk == null) paletteChunk = Slice(file, chunk);
            else if (chunk.Type == "tRNS" && transparency == null) transparency = Slice(file, chunk);
            else if (chunk.Type == "IDAT") data.Write(file, chunk.Start, chunk.Length);
         }
         if (data.Length == 0) {
            error = "The PNG file has no picture in it.";
            return false;
         }
         IReadOnlyList<int> palette = null;
         if (header.ColorType == PngColorType.Indexed) {
            if (paletteChunk == null || paletteChunk.Length == 0 || paletteChunk.Length % 3 != 0 || paletteChunk.Length > 3 * 256) {
               error = "The PNG file says it uses a palette, but the palette is missing or damaged.";
               return false;
            }
            var colors = new List<int>();
            for (int i = 0; i < paletteChunk.Length; i += 3) colors.Add((paletteChunk[i] << 16) | (paletteChunk[i + 1] << 8) | paletteChunk[i + 2]);
            palette = colors;
         }

         var channels = Channels(header.ColorType);
         var bitsPerPixel = channels * header.BitDepth;
         var bytesPerPixel = Math.Max(1, bitsPerPixel / 8);
         var passes = Passes(header.Width, header.Height, header.Interlaced);
         long expected = 0;
         foreach (var pass in passes) expected += (long)pass.Height * (1 + (pass.Width * bitsPerPixel + 7) / 8);
         if (expected > int.MaxValue / 2) {
            error = "The picture is too big.";
            return false;
         }

         var raw = new byte[expected];
         try {
            data.Position = 0;
            using var inflate = new ZLibStream(data, CompressionMode.Decompress);
            int read = 0;
            while (read < raw.Length) {
               var count = inflate.Read(raw, read, raw.Length - read);
               if (count <= 0) break;
               read += count;
            }
            if (read < raw.Length) {
               error = "The picture inside the PNG file is damaged (it is shorter than its size says).";
               return false;
            }
         } catch (Exception e) when (e is InvalidDataException || e is IOException || e is NotSupportedException) {
            error = "The picture inside the PNG file is damaged and can't be unpacked.";
            return false;
         }

         var indexed = header.ColorType == PngColorType.Indexed;
         var indexes = indexed ? new int[header.Width, header.Height] : null;
         var rgba = indexed ? null : new byte[header.Width * header.Height * 4];
         var position = 0;
         foreach (var pass in passes) {
            var rowBytes = (pass.Width * bitsPerPixel + 7) / 8;
            var previous = new byte[rowBytes];
            var current = new byte[rowBytes];
            for (int py = 0; py < pass.Height; py++) {
               var filter = raw[position];
               Array.Copy(raw, position + 1, current, 0, rowBytes);
               position += 1 + rowBytes;
               if (!TryUnfilter(filter, current, previous, bytesPerPixel)) {
                  error = $"The picture inside the PNG file is damaged (unknown line filter {filter}).";
                  return false;
               }
               var y = pass.Y + py * pass.StepY;
               for (int px = 0; px < pass.Width; px++) {
                  var x = pass.X + px * pass.StepX;
                  if (indexed) {
                     var index = Sample(current, px, 0, 1, header.BitDepth);
                     if (index >= palette.Count) {
                        error = $"The PNG file uses colour number {index}, but its palette only has {palette.Count} colours.";
                        return false;
                     }
                     indexes[x, y] = index;
                  } else {
                     WritePixel(rgba, (y * header.Width + x) * 4, current, px, header, transparency);
                  }
               }
               var swap = previous; previous = current; current = swap;
            }
         }

         picture = new PngPicture { Width = header.Width, Height = header.Height, IsIndexed = indexed, Indexes = indexes, Palette = palette, Rgba = rgba };
         return true;
      }

      private static byte[] Slice(byte[] file, Chunk chunk) {
         var result = new byte[chunk.Length];
         Array.Copy(file, chunk.Start, result, 0, chunk.Length);
         return result;
      }

      private static int Channels(PngColorType type) => type switch {
         PngColorType.Gray => 1,
         PngColorType.Rgb => 3,
         PngColorType.Indexed => 1,
         PngColorType.GrayAlpha => 2,
         _ => 4,
      };

      private static bool TryReadChunks(byte[] file, out List<Chunk> chunks, out string error) {
         chunks = new List<Chunk>();
         error = null;
         if (file == null || file.Length == 0) {
            error = "The file is empty.";
            return false;
         }
         if (!HasPngSignature(file)) {
            error = "That file is not a PNG picture.";
            return false;
         }
         int position = Signature.Length;
         var sawEnd = false;
         while (position + 12 <= file.Length) {
            long length = ReadInt(file, position) & 0xFFFFFFFFL;
            if (length > file.Length - position - 12) {
               error = "The PNG file is cut off (part of it is missing).";
               return false;
            }
            var type = Encoding.ASCII.GetString(file, position + 4, 4);
            var critical = (file[position + 4] & 0x20) == 0;
            // the checksum of a piece that is needed to show the picture has to match. Pieces that only carry extra information (text, gamma...) are not checked.
            if (critical && Crc(file, position + 4, 4 + (int)length) != ReadInt(file, position + 8 + (int)length)) {
               error = $"The PNG file is damaged (its {type} part does not match its checksum).";
               return false;
            }
            chunks.Add(new Chunk(type, position + 8, (int)length));
            position += 12 + (int)length;
            if (type == "IEND") {
               sawEnd = true;
               break;
            }
         }
         if (!sawEnd) {
            error = "The PNG file is cut off (it has no end).";
            return false;
         }
         return true;
      }

      private static bool TryParseHeader(byte[] file, List<Chunk> chunks, out PngHeader header, out string error) {
         header = null;
         error = null;
         if (chunks.Count == 0 || chunks[0].Type != "IHDR" || chunks[0].Length != 13) {
            error = "The PNG file is damaged (it does not start with a header).";
            return false;
         }
         var start = chunks[0].Start;
         var width = ReadInt(file, start);
         var height = ReadInt(file, start + 4);
         var depth = file[start + 8];
         var type = file[start + 9];
         if (width <= 0 || height <= 0) {
            error = "The PNG file is damaged (its size is not valid).";
            return false;
         }
         if (file[start + 10] != 0 || file[start + 11] != 0 || file[start + 12] > 1) {
            error = "The PNG file uses a method this program does not know.";
            return false;
         }
         var valid = type switch {
            0 => depth == 1 || depth == 2 || depth == 4 || depth == 8 || depth == 16,
            2 => depth == 8 || depth == 16,
            3 => depth == 1 || depth == 2 || depth == 4 || depth == 8,
            4 => depth == 8 || depth == 16,
            6 => depth == 8 || depth == 16,
            _ => false,
         };
         if (!valid) {
            error = $"The PNG file is damaged (colour type {type} with {depth} bits is not valid).";
            return false;
         }
         header = new PngHeader { Width = width, Height = height, BitDepth = depth, ColorType = (PngColorType)type, Interlaced = file[start + 12] == 1 };
         return true;
      }

      private static readonly (int x, int y, int stepX, int stepY)[] Adam7 = {
         (0, 0, 8, 8), (4, 0, 8, 8), (0, 4, 4, 8), (2, 0, 4, 4), (0, 2, 2, 4), (1, 0, 2, 2), (0, 1, 1, 2),
      };

      private record Pass(int X, int Y, int StepX, int StepY, int Width, int Height);

      private static List<Pass> Passes(int width, int height, bool interlaced) {
         var result = new List<Pass>();
         if (!interlaced) {
            result.Add(new Pass(0, 0, 1, 1, width, height));
            return result;
         }
         foreach (var (x, y, stepX, stepY) in Adam7) {
            var passWidth = (width - x + stepX - 1) / stepX;
            var passHeight = (height - y + stepY - 1) / stepY;
            if (passWidth <= 0 || passHeight <= 0) continue; // a small picture has no pixels in some of the passes
            result.Add(new Pass(x, y, stepX, stepY, passWidth, passHeight));
         }
         return result;
      }

      private static bool TryUnfilter(byte filter, byte[] row, byte[] previous, int bytesPerPixel) {
         switch (filter) {
            case 0: return true;
            case 1:
               for (int i = bytesPerPixel; i < row.Length; i++) row[i] = (byte)(row[i] + row[i - bytesPerPixel]);
               return true;
            case 2:
               for (int i = 0; i < row.Length; i++) row[i] = (byte)(row[i] + previous[i]);
               return true;
            case 3:
               for (int i = 0; i < row.Length; i++) row[i] = (byte)(row[i] + (((i >= bytesPerPixel ? row[i - bytesPerPixel] : 0) + previous[i]) >> 1));
               return true;
            case 4:
               for (int i = 0; i < row.Length; i++) {
                  int a = i >= bytesPerPixel ? row[i - bytesPerPixel] : 0, b = previous[i], c = i >= bytesPerPixel ? previous[i - bytesPerPixel] : 0;
                  var p = a + b - c;
                  var (pa, pb, pc) = (Math.Abs(p - a), Math.Abs(p - b), Math.Abs(p - c));
                  row[i] = (byte)(row[i] + (pa <= pb && pa <= pc ? a : pb <= pc ? b : c));
               }
               return true;
            default:
               return false;
         }
      }

      /// <summary>One sample (colour number or one channel of a colour) of the pixel at 'pixel' of an unfiltered row.</summary>
      private static int Sample(byte[] row, int pixel, int channel, int channels, int depth) {
         if (depth == 8) return row[pixel * channels + channel];
         if (depth == 16) {
            var at = (pixel * channels + channel) * 2;
            return (row[at] << 8) | row[at + 1];
         }
         var bit = (pixel * channels + channel) * depth; // 1, 2 or 4 bits: a row only has one channel
         return (row[bit >> 3] >> (8 - depth - (bit & 7))) & ((1 << depth) - 1);
      }

      /// <summary>Scales a sample of any bit depth to 0..255.</summary>
      private static byte To8(int value, int depth) {
         if (depth == 8) return (byte)value;
         var max = (1 << depth) - 1;
         return (byte)((value * 255 + max / 2) / max);
      }

      private static void WritePixel(byte[] rgba, int at, byte[] row, int pixel, PngHeader header, byte[] transparency) {
         var depth = header.BitDepth;
         switch (header.ColorType) {
            case PngColorType.Gray: {
               var gray = Sample(row, pixel, 0, 1, depth);
               var g8 = To8(gray, depth);
               rgba[at] = rgba[at + 1] = rgba[at + 2] = g8;
               rgba[at + 3] = transparency != null && transparency.Length >= 2 && gray == ((transparency[0] << 8) | transparency[1]) ? (byte)0 : (byte)255;
               break;
            }
            case PngColorType.GrayAlpha: {
               var g8 = To8(Sample(row, pixel, 0, 2, depth), depth);
               rgba[at] = rgba[at + 1] = rgba[at + 2] = g8;
               rgba[at + 3] = To8(Sample(row, pixel, 1, 2, depth), depth);
               break;
            }
            case PngColorType.Rgb: {
               int r = Sample(row, pixel, 0, 3, depth), g = Sample(row, pixel, 1, 3, depth), b = Sample(row, pixel, 2, 3, depth);
               rgba[at] = To8(r, depth);
               rgba[at + 1] = To8(g, depth);
               rgba[at + 2] = To8(b, depth);
               var keyed = transparency != null && transparency.Length >= 6
                  && r == ((transparency[0] << 8) | transparency[1]) && g == ((transparency[2] << 8) | transparency[3]) && b == ((transparency[4] << 8) | transparency[5]);
               rgba[at + 3] = keyed ? (byte)0 : (byte)255;
               break;
            }
            default: {
               rgba[at] = To8(Sample(row, pixel, 0, 4, depth), depth);
               rgba[at + 1] = To8(Sample(row, pixel, 1, 4, depth), depth);
               rgba[at + 2] = To8(Sample(row, pixel, 2, 4, depth), depth);
               rgba[at + 3] = To8(Sample(row, pixel, 3, 4, depth), depth);
               break;
            }
         }
      }

      #endregion

      #region Writing

      /// <summary>
      /// Makes a palette PNG. 'pixels' holds colour numbers ([x, y]) and 'palette' the colours (0xRRGGBB) they stand for.
      /// Up to 16 colours are stored with 4 bits per pixel, more with 8 bits.
      /// </summary>
      public static byte[] EncodeIndexed(int[,] pixels, IReadOnlyList<int> palette) {
         if (palette == null || palette.Count < 1 || palette.Count > 256) throw new ArgumentException("A palette picture needs 1 to 256 colours.", nameof(palette));
         int width = pixels.GetLength(0), height = pixels.GetLength(1);
         if (width < 1 || height < 1) throw new ArgumentException("The picture has no pixels.", nameof(pixels));
         var depth = palette.Count <= 16 ? 4 : 8;
         var rowBytes = (width * depth + 7) / 8;
         var raw = new byte[(rowBytes + 1) * height];
         for (int y = 0; y < height; y++) {
            var row = y * (rowBytes + 1) + 1; // the byte before each row is its filter: 0 means none
            for (int x = 0; x < width; x++) {
               var value = pixels[x, y];
               if (value < 0 || value >= palette.Count) throw new ArgumentException($"The pixel at {x}, {y} ({value}) is not a colour of the palette.", nameof(pixels));
               if (depth == 8) raw[row + x] = (byte)value;
               else raw[row + x / 2] |= (byte)(x % 2 == 0 ? value << 4 : value);
            }
         }

         var output = new MemoryStream();
         output.Write(Signature, 0, Signature.Length);
         var header = new byte[13];
         WriteInt(header, 0, width);
         WriteInt(header, 4, height);
         header[8] = (byte)depth;
         header[9] = (byte)PngColorType.Indexed; // (compression, filter and interlace stay 0)
         WriteChunk(output, "IHDR", header);
         var colors = new byte[palette.Count * 3];
         for (int i = 0; i < palette.Count; i++) {
            colors[i * 3] = (byte)(palette[i] >> 16);
            colors[i * 3 + 1] = (byte)(palette[i] >> 8);
            colors[i * 3 + 2] = (byte)palette[i];
         }
         WriteChunk(output, "PLTE", colors);
         var packed = new MemoryStream();
         using (var deflate = new ZLibStream(packed, CompressionLevel.Optimal, leaveOpen: true)) deflate.Write(raw, 0, raw.Length);
         WriteChunk(output, "IDAT", packed.ToArray());
         WriteChunk(output, "IEND", Array.Empty<byte>());
         return output.ToArray();
      }

      private static void WriteChunk(Stream output, string type, byte[] data) {
         var bytes = new byte[12 + data.Length];
         WriteInt(bytes, 0, data.Length);
         for (int i = 0; i < 4; i++) bytes[4 + i] = (byte)type[i];
         Array.Copy(data, 0, bytes, 8, data.Length);
         WriteInt(bytes, 8 + data.Length, Crc(bytes, 4, 4 + data.Length));
         output.Write(bytes, 0, bytes.Length);
      }

      #endregion

      #region Helpers

      private static int ReadInt(byte[] data, int at) => (data[at] << 24) | (data[at + 1] << 16) | (data[at + 2] << 8) | data[at + 3];

      private static void WriteInt(byte[] data, int at, int value) {
         data[at] = (byte)(value >> 24);
         data[at + 1] = (byte)(value >> 16);
         data[at + 2] = (byte)(value >> 8);
         data[at + 3] = (byte)value;
      }

      private static uint[] crcTable;

      private static int Crc(byte[] data, int start, int length) {
         if (crcTable == null) {
            var table = new uint[256];
            for (uint n = 0; n < 256; n++) {
               var c = n;
               for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
               table[n] = c;
            }
            crcTable = table;
         }
         var crc = 0xFFFFFFFFu;
         for (int i = 0; i < length; i++) crc = crcTable[(crc ^ data[start + i]) & 0xFF] ^ (crc >> 8);
         return (int)(crc ^ 0xFFFFFFFFu);
      }

      #endregion
   }
}
