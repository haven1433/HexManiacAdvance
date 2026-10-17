using HexManiac.WPF.Resources;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// The image tools exchange indexed PNG files with other programs.
   /// Other programs write PNG files in many ways: scanlines can use any of the five filters, the image data can be split across several IDAT chunks,
   /// widths don't have to fill a whole byte, and the image can be interlaced. All of those are the same picture and must load as the same pixels.
   /// </summary>
   public class IndexedPngTests {
      private static readonly Random seed = new Random(5);

      #region PNG writer for the tests (deliberately independent of IndexedPngWriter)

      private static byte[] Int(int value) => new[] { (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value };

      private static byte[] Chunk(string type, byte[] body) {
         var typeBytes = type.Select(c => (byte)c).ToArray();
         var result = new List<byte>();
         result.AddRange(Int(body.Length));
         result.AddRange(typeBytes);
         result.AddRange(body);
         result.AddRange(Int((int)Force.Crc32.Crc32Algorithm.Compute(typeBytes.Concat(body).ToArray())));
         return result.ToArray();
      }

      private static byte Paeth(int a, int b, int c) {
         var p = a + b - c;
         var (pa, pb, pc) = (Math.Abs(p - a), Math.Abs(p - b), Math.Abs(p - c));
         if (pa <= pb && pa <= pc) return (byte)a;
         return (byte)(pb <= pc ? b : c);
      }

      private static byte[] FilterRow(int filter, byte[] row, byte[] previous) {
         var result = new byte[row.Length];
         for (int i = 0; i < row.Length; i++) {
            int a = i > 0 ? row[i - 1] : 0, b = previous[i], c = i > 0 ? previous[i - 1] : 0;
            var predicted = filter == 0 ? 0 : filter == 1 ? a : filter == 2 ? b : filter == 3 ? (a + b) / 2 : Paeth(a, b, c);
            result[i] = (byte)(row[i] - predicted);
         }
         return result;
      }

      private static byte[] PackRow(int[] values, int bitDepth) {
         var row = new byte[(values.Length * bitDepth + 7) / 8];
         for (int i = 0; i < values.Length; i++) {
            var bit = i * bitDepth;
            row[bit / 8] |= (byte)(values[i] << (8 - bitDepth - bit % 8));
         }
         return row;
      }

      private static byte[] EncodePass(int[,] pixels, int x0, int y0, int dx, int dy, int bitDepth, int[] filters) {
         var width = pixels.GetLength(0);
         var height = pixels.GetLength(1);
         var xs = Enumerable.Range(0, width).Where(x => x >= x0 && (x - x0) % dx == 0).ToArray();
         var ys = Enumerable.Range(0, height).Where(y => y >= y0 && (y - y0) % dy == 0).ToArray();
         var data = new List<byte>();
         if (xs.Length == 0 || ys.Length == 0) return data.ToArray();
         var previous = new byte[(xs.Length * bitDepth + 7) / 8];
         for (int i = 0; i < ys.Length; i++) {
            var row = PackRow(xs.Select(x => pixels[x, ys[i]]).ToArray(), bitDepth);
            var filter = filters[i % filters.Length];
            data.Add((byte)filter);
            data.AddRange(FilterRow(filter, row, previous));
            previous = row;
         }
         return data.ToArray();
      }

      private static readonly (int x, int y, int dx, int dy)[] Adam7 = { (0, 0, 8, 8), (4, 0, 8, 8), (0, 4, 4, 8), (2, 0, 4, 4), (0, 2, 2, 4), (1, 0, 2, 2), (0, 1, 1, 2) };

      private static byte[] BuildPng(int[,] pixels, int bitDepth, int colorCount, int[] filters = null, int idatSize = 0, bool interlace = false, bool ancillary = false) {
         filters = filters ?? new[] { 0 };
         var width = pixels.GetLength(0);
         var height = pixels.GetLength(1);
         var raw = new List<byte>();
         if (interlace) foreach (var (x, y, dx, dy) in Adam7) raw.AddRange(EncodePass(pixels, x, y, dx, dy, bitDepth, filters));
         else raw.AddRange(EncodePass(pixels, 0, 0, 1, 1, bitDepth, filters));
         var compressed = Png.ZLibCompress(raw.ToArray());

         var file = new List<byte>(Png.Signature);
         file.AddRange(Chunk("IHDR", Int(width).Concat(Int(height)).Concat(new byte[] { (byte)bitDepth, 3, 0, 0, (byte)(interlace ? 1 : 0) }).ToArray()));
         if (ancillary) {
            file.AddRange(Chunk("gAMA", Int(45455)));
            file.AddRange(Chunk("sRGB", new byte[] { 0 }));
         }
         file.AddRange(Chunk("PLTE", Palette(colorCount).SelectMany(c => new[] { c.r, c.g, c.b }).ToArray()));
         if (ancillary) file.AddRange(Chunk("tRNS", new byte[] { 0 }));
         if (idatSize == 0) idatSize = compressed.Length;
         for (int i = 0; i < compressed.Length; i += idatSize) file.AddRange(Chunk("IDAT", compressed.Skip(i).Take(idatSize).ToArray()));
         if (ancillary) file.AddRange(Chunk("tEXt", new byte[] { (byte)'a', 0, (byte)'b' }));
         file.AddRange(Chunk("IEND", new byte[0]));
         return file.ToArray();
      }

      private static (byte r, byte g, byte b)[] Palette(int count) => Enumerable.Range(0, count).Select(i => ((byte)(i * 37 % 256), (byte)(i * 91 % 256), (byte)(255 - i * 13 % 256))).ToArray();

      /// <summary>What the loader reports for a palette color: 5 bits per channel, red in the high bits.</summary>
      private static short Expected((byte r, byte g, byte b) c) => (short)(((int)Math.Round(c.r * 31 / 255d) << 10) | ((int)Math.Round(c.g * 31 / 255d) << 5) | (int)Math.Round(c.b * 31 / 255d));

      private static int[,] Picture(int width, int height, int colorCount) {
         var pixels = new int[width, height];
         for (int x = 0; x < width; x++) for (int y = 0; y < height; y++) pixels[x, y] = (x * 7 + y * 13 + x * y / 3) % colorCount;
         return pixels;
      }

      private static (int[,] pixels, IReadOnlyList<short> palette) Load(byte[] file) {
         var path = Path.Combine(Path.GetTempPath(), "hma_png_" + Guid.NewGuid().ToString("N") + ".png");
         File.WriteAllBytes(path, file);
         try {
            return IndexedPng.Load(path);
         } finally {
            File.Delete(path);
         }
      }

      private static void AssertLoads(int[,] pixels, int colorCount, byte[] file) {
         var (loaded, palette) = Load(file);
         Assert.Equal(pixels.GetLength(0), loaded.GetLength(0));
         Assert.Equal(pixels.GetLength(1), loaded.GetLength(1));
         for (int x = 0; x < pixels.GetLength(0); x++) for (int y = 0; y < pixels.GetLength(1); y++) Assert.Equal(pixels[x, y], loaded[x, y]);
         Assert.Equal(Palette(colorCount).Select(Expected).ToArray(), palette.ToArray());
      }

      #endregion

      [Theory]
      [InlineData(1, 8, 8)]
      [InlineData(2, 16, 8)]
      [InlineData(4, 16, 16)]
      [InlineData(8, 64, 16)]
      [InlineData(8, 256, 32)]
      public void PlainImage_Load_MatchesPixelsAndPalette(int bitDepth, int colorCount, int size) {
         var colors = Math.Min(colorCount, 1 << bitDepth);
         var pixels = Picture(size, size, colors);
         AssertLoads(pixels, colors, BuildPng(pixels, bitDepth, colors));
      }

      [Theory]
      [InlineData(1, 1)]
      [InlineData(1, 2)]
      [InlineData(1, 3)]
      [InlineData(1, 4)]
      [InlineData(2, 1)]
      [InlineData(2, 2)]
      [InlineData(2, 3)]
      [InlineData(2, 4)]
      [InlineData(4, 1)]
      [InlineData(4, 2)]
      [InlineData(4, 3)]
      [InlineData(4, 4)]
      [InlineData(8, 1)]
      [InlineData(8, 2)]
      [InlineData(8, 3)]
      [InlineData(8, 4)]
      public void ScanlineFilter_Load_PixelsAreUnfiltered(int bitDepth, int filter) {
         var colors = Math.Min(16, 1 << bitDepth);
         var pixels = Picture(24, 17, colors);
         AssertLoads(pixels, colors, BuildPng(pixels, bitDepth, colors, new[] { filter }));
      }

      [Theory]
      [InlineData(1)]
      [InlineData(2)]
      [InlineData(4)]
      [InlineData(8)]
      public void EveryFilterInTheSameImage_Load_PixelsAreUnfiltered(int bitDepth) {
         var colors = Math.Min(16, 1 << bitDepth);
         var pixels = Picture(40, 30, colors);
         AssertLoads(pixels, colors, BuildPng(pixels, bitDepth, colors, new[] { 0, 1, 2, 3, 4, 4, 3, 2, 1 }));
      }

      [Theory]
      [InlineData(1, 1)]
      [InlineData(1, 7)]
      [InlineData(1, 9)]
      [InlineData(1, 33)]
      [InlineData(2, 3)]
      [InlineData(2, 5)]
      [InlineData(2, 15)]
      [InlineData(4, 1)]
      [InlineData(4, 3)]
      [InlineData(4, 15)]
      [InlineData(4, 33)]
      [InlineData(8, 1)]
      [InlineData(8, 15)]
      public void WidthThatDoesNotFillTheLastByte_Load_PixelsAreCorrect(int bitDepth, int width) {
         var colors = Math.Min(16, 1 << bitDepth);
         var pixels = Picture(width, 6, colors);
         AssertLoads(pixels, colors, BuildPng(pixels, bitDepth, colors));
         AssertLoads(pixels, colors, BuildPng(pixels, bitDepth, colors, new[] { 0, 1, 2, 3, 4 }));
      }

      [Theory]
      [InlineData(4, 16)]
      [InlineData(4, 64)]
      [InlineData(8, 16)]
      [InlineData(8, 100)]
      public void ImageDataSplitIntoSeveralIdatChunks_Load_PixelsAreCorrect(int bitDepth, int chunkSize) {
         var colors = 16;
         var pixels = new int[128, 128];
         for (int x = 0; x < 128; x++) for (int y = 0; y < 128; y++) pixels[x, y] = seed.Next(colors); // noise doesn't compress: there will be many chunks
         var file = BuildPng(pixels, bitDepth, colors, new[] { 0 }, chunkSize);
         Assert.True(file.Length > chunkSize * 3);
         AssertLoads(pixels, colors, file);
      }

      [Theory]
      [InlineData(4, 16, 16)]
      [InlineData(8, 16, 16)]
      [InlineData(4, 15, 9)]
      [InlineData(8, 3, 3)]
      [InlineData(2, 13, 11)]
      [InlineData(1, 1, 1)]
      [InlineData(1, 8, 8)]
      public void InterlacedImage_Load_PixelsAreCorrect(int bitDepth, int width, int height) {
         var colors = Math.Min(16, 1 << bitDepth);
         var pixels = Picture(width, height, colors);
         AssertLoads(pixels, colors, BuildPng(pixels, bitDepth, colors, new[] { 0, 1, 2, 3, 4 }, 50, interlace: true));
      }

      [Fact]
      public void AncillaryChunks_Load_AreSkipped() {
         var pixels = Picture(16, 16, 16);
         AssertLoads(pixels, 16, BuildPng(pixels, 4, 16, ancillary: true));
      }

      [Fact]
      public void PaletteSmallerThanTheBitDepthAllows_Load_KeepsThePalette() {
         var pixels = Picture(16, 16, 3);
         AssertLoads(pixels, 3, BuildPng(pixels, 4, 3));
      }

      [Fact]
      public void SavedFile_Load_SamePixelsAndPalette() {
         var palette = Palette(16).Select(Expected).ToArray();
         var pixels = Picture(32, 24, 16);
         var path = Path.Combine(Path.GetTempPath(), "hma_png_" + Guid.NewGuid().ToString("N") + ".png");
         try {
            IndexedPng.Save(path, pixels, palette);
            var (loaded, loadedPalette) = IndexedPng.Load(path);
            for (int x = 0; x < 32; x++) for (int y = 0; y < 24; y++) Assert.Equal(pixels[x, y], loaded[x, y]);
            Assert.Equal(palette, loadedPalette.ToArray());
         } finally {
            File.Delete(path);
         }
      }

      [Fact]
      public void SavedFile256Colors_Load_SamePixelsAndPalette() {
         var palette = Palette(256).Select(Expected).ToArray();
         var pixels = Picture(64, 64, 256);
         var path = Path.Combine(Path.GetTempPath(), "hma_png_" + Guid.NewGuid().ToString("N") + ".png");
         try {
            IndexedPng.Save(path, pixels, palette);
            var (loaded, loadedPalette) = IndexedPng.Load(path);
            for (int x = 0; x < 64; x++) for (int y = 0; y < 64; y++) Assert.Equal(pixels[x, y], loaded[x, y]);
            Assert.Equal(palette, loadedPalette.ToArray());
         } finally {
            File.Delete(path);
         }
      }

      [Theory]
      [InlineData(1)]
      [InlineData(3)]
      [InlineData(7)]
      [InlineData(15)]
      public void SaveWithOddWidth_Load_SamePixels(int width) {
         var palette = Palette(16).Select(Expected).ToArray();
         var pixels = Picture(width, 5, 16);
         var path = Path.Combine(Path.GetTempPath(), "hma_png_" + Guid.NewGuid().ToString("N") + ".png");
         try {
            IndexedPng.Save(path, pixels, palette);
            var (loaded, _) = IndexedPng.Load(path);
            Assert.Equal(width, loaded.GetLength(0));
            for (int x = 0; x < width; x++) for (int y = 0; y < 5; y++) Assert.Equal(pixels[x, y], loaded[x, y]);
         } finally {
            File.Delete(path);
         }
      }

      [Fact]
      public void PixelOutsideThePalette_Save_Throws() {
         var path = Path.Combine(Path.GetTempPath(), "hma_png_" + Guid.NewGuid().ToString("N") + ".png");
         try {
            Assert.Throws<PngArgumentException>(() => IndexedPng.Save(path, Picture(8, 8, 16), new short[4]));
         } finally {
            File.Delete(path);
         }
      }

      [Fact]
      public void CorruptedChunk_Load_ThrowsPngArgumentException() {
         var file = BuildPng(Picture(16, 16, 16), 4, 16);
         file[file.Length - 20] ^= 0x55; // inside the IDAT data or its CRC
         Assert.Throws<PngArgumentException>(() => Load(file));
      }

      [Theory]
      [InlineData(8)]
      [InlineData(20)]
      [InlineData(50)]
      [InlineData(80)]
      public void TruncatedFile_Load_ThrowsPngArgumentException(int length) {
         var file = BuildPng(Picture(16, 16, 16), 4, 16);
         Assert.Throws<PngArgumentException>(() => Load(file.Take(length).ToArray()));
      }

      [Fact]
      public void NotAPng_Load_ThrowsPngArgumentException() {
         Assert.Throws<PngArgumentException>(() => Load(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }));
         Assert.Throws<PngArgumentException>(() => Load(new byte[0]));
      }

      [Fact]
      public void ImageDataForTheWrongSize_Load_ThrowsPngArgumentException() {
         // an IHDR that claims twice as many rows as there is image data for
         var file = BuildPng(Picture(16, 16, 16), 4, 16).ToList();
         var heightIndex = 8 + 8 + 4; // signature, IHDR chunk header, width
         file[heightIndex + 3] = 32;
         var crc = Int((int)Force.Crc32.Crc32Algorithm.Compute(file.Skip(8 + 4).Take(4 + 13).ToArray()));
         for (int i = 0; i < 4; i++) file[8 + 8 + 13 + i] = crc[i];
         Assert.Throws<PngArgumentException>(() => Load(file.ToArray()));
      }
   }
}
