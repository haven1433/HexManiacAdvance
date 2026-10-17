using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Map;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>The picture file of the PNG reader/writer: only plain PNG files, and a clear refusal for anything else.</summary>
   public class PlainPngTests {
      private static readonly uint[] crcTable = Enumerable.Range(0, 256).Select(n => {
         var c = (uint)n;
         for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
         return c;
      }).ToArray();

      private static uint Crc(byte[] data) {
         var c = 0xFFFFFFFFu;
         foreach (var b in data) c = crcTable[(c ^ b) & 0xFF] ^ (c >> 8);
         return c ^ 0xFFFFFFFFu;
      }

      private static void Chunk(Stream output, string type, byte[] body) {
         void Big(byte[] value) => output.Write(value, 0, value.Length);
         byte[] BigEndian(uint v) => new[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v };
         Big(BigEndian((uint)body.Length));
         var typeAndBody = System.Text.Encoding.ASCII.GetBytes(type).Concat(body).ToArray();
         Big(typeAndBody);
         Big(BigEndian(Crc(typeAndBody)));
      }

      /// <summary>A PNG file made by hand (every row is stored with filter 'none'): so these tests don't depend on how the program's own writer works.</summary>
      public static byte[] MakePng(int width, int height, int colorType, int bitDepth, byte[] rows, int bytesPerRow, byte[] palette = null, byte[] transparency = null) {
         var output = new MemoryStream();
         output.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, 0, 8);
         var header = new byte[13];
         header[3] = (byte)width; header[2] = (byte)(width >> 8);
         header[7] = (byte)height; header[6] = (byte)(height >> 8);
         header[8] = (byte)bitDepth;
         header[9] = (byte)colorType;
         Chunk(output, "IHDR", header);
         if (palette != null) Chunk(output, "PLTE", palette);
         if (transparency != null) Chunk(output, "tRNS", transparency);
         var raw = new MemoryStream();
         for (int y = 0; y < height; y++) {
            raw.WriteByte(0);
            raw.Write(rows, y * bytesPerRow, bytesPerRow);
         }
         var compressed = new MemoryStream();
         using (var z = new ZLibStream(compressed, CompressionLevel.Optimal, true)) z.Write(raw.ToArray(), 0, (int)raw.Length);
         Chunk(output, "IDAT", compressed.ToArray());
         Chunk(output, "IEND", new byte[0]);
         return output.ToArray();
      }

      public static byte[] MakeRgba(int width, int height, Func<int, int, (int r, int g, int b, int a)> pixel) {
         var rows = new byte[width * height * 4];
         for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
               var (r, g, b, a) = pixel(x, y);
               rows[(y * width + x) * 4 + 0] = (byte)r;
               rows[(y * width + x) * 4 + 1] = (byte)g;
               rows[(y * width + x) * 4 + 2] = (byte)b;
               rows[(y * width + x) * 4 + 3] = (byte)a;
            }
         }
         return MakePng(width, height, 6, 8, rows, width * 4);
      }

      [Fact]
      public void IndexedPicture_RoundTrips() {
         var pixels = new int[16, 8];
         for (int y = 0; y < 8; y++) for (int x = 0; x < 16; x++) pixels[x, y] = (x * 3 + y) % 16;
         var palette = Enumerable.Range(0, 16).Select(i => (i * 16 << 16) | ((255 - i * 16) << 8) | i).ToList();

         var file = PlainPng.EncodeIndexed(pixels, palette);

         Assert.True(PlainPng.HasPngSignature(file));
         Assert.True(PlainPng.TryReadHeader(file, out var header, out var error), error);
         Assert.Equal((16, 8, 4, PngColorType.Indexed, false), (header.Width, header.Height, header.BitDepth, header.ColorType, header.Interlaced));
         Assert.True(PlainPng.TryDecode(file, out var picture, out error), error);
         Assert.True(picture.IsIndexed);
         Assert.Equal(palette, picture.Palette.Take(16).ToList());
         for (int y = 0; y < 8; y++) for (int x = 0; x < 16; x++) Assert.Equal(pixels[x, y], picture.Indexes[x, y]);
      }

      [Fact]
      public void FullColourPicture_IsDecodedToRgba() {
         var file = MakeRgba(8, 8, (x, y) => (x * 30, y * 30, 7, x == 0 ? 0 : 255));

         Assert.True(PlainPng.TryDecode(file, out var picture, out var error), error);

         Assert.False(picture.IsIndexed);
         Assert.Equal(new byte[] { 60, 90, 7, 255 }, picture.Rgba.Skip((3 * 8 + 2) * 4).Take(4).ToArray());
         Assert.Equal(0, picture.Rgba[(3 * 8 + 0) * 4 + 3]);
      }

      [Fact]
      public void GrayPicture_IsDecodedToRgba() {
         var rows = Enumerable.Range(0, 16).Select(i => (byte)(i * 17)).ToArray();
         var file = MakePng(4, 4, 0, 8, rows, 4);

         Assert.True(PlainPng.TryDecode(file, out var picture, out var error), error);

         Assert.False(picture.IsIndexed);
         Assert.Equal(new byte[] { 34, 34, 34, 255 }, picture.Rgba.Skip(2 * 4).Take(4).ToArray());
      }

      [Fact]
      public void NotAPng_IsRefusedWithAMessage() {
         foreach (var junk in new[] { new byte[0], System.Text.Encoding.ASCII.GetBytes("this is not a picture"), new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46, 0 } }) {
            Assert.False(PlainPng.HasPngSignature(junk));
            Assert.False(PlainPng.TryReadHeader(junk, out var header, out var error));
            Assert.Null(header);
            Assert.NotEmpty(error);
            Assert.False(PlainPng.TryDecode(junk, out var picture, out error));
            Assert.Null(picture);
            Assert.NotEmpty(error);
         }
      }

      [Fact]
      public void TruncatedPng_IsRefused() {
         var file = PlainPng.EncodeIndexed(new int[32, 32], Enumerable.Range(0, 16).ToList());

         Assert.False(PlainPng.TryDecode(file.Take(file.Length / 2).ToArray(), out var picture, out var error));

         Assert.Null(picture);
         Assert.NotEmpty(error);
      }

      [Fact]
      public void PngWithADamagedChunk_IsRefused() {
         var pixels = new int[32, 32];
         for (int i = 0; i < 32; i++) pixels[i, i] = 5;
         var file = PlainPng.EncodeIndexed(pixels, Enumerable.Range(0, 16).ToList());
         file[file.Length / 2] ^= 0x40;

         Assert.False(PlainPng.TryDecode(file, out var picture, out var error));

         Assert.Null(picture);
         Assert.Contains("damaged", error);
      }

      [Fact]
      public void HugePicture_IsRefusedBeforeItIsDecoded() {
         var file = PlainPng.EncodeIndexed(new int[128, 128], Enumerable.Range(0, 16).ToList());

         Assert.False(PlainPng.TryDecode(file, out var picture, out var error, maxPixels: 1000));

         Assert.Null(picture);
         Assert.NotEmpty(error);
      }
   }

   /// <summary>The smallest LZ data: has to decompress to the same bytes, stay inside what the BIOS decompressor accepts, and be smaller than the older compression.</summary>
   public class TilesetLzTests {
      private static IEnumerable<(string name, byte[] data)> Samples() {
         var random = new Random(3);
         yield return ("one byte", new byte[] { 7 });
         yield return ("two bytes", new byte[] { 7, 7 });
         yield return ("zeros", new byte[256]);
         yield return ("zeros, 4 tiles", new byte[128]);
         yield return ("noise", Enumerable.Range(0, 2000).Select(i => (byte)random.Next(256)).ToArray());
         yield return ("tiles", Enumerable.Range(0, 32 * 40).Select(i => (byte)(((i / 32 + i % 32 / 4 + 1) % 15) | ((((i / 32) * 3 + i % 4 + 1) % 16) << 4))).ToArray());
         yield return ("pattern of 3 (distance 3: odd)", Enumerable.Range(0, 300).Select(i => (byte)(10 + i % 3)).ToArray());
         yield return ("pattern of 5 starting on an odd byte", new byte[] { 99 }.Concat(Enumerable.Range(0, 300).Select(i => (byte)(10 + i % 5))).ToArray());
         yield return ("runs of 17, 18, 19 and 20", new byte[] { 1 }.Concat(new byte[17]).Concat(new byte[] { 2 }).Concat(new byte[18]).Concat(new byte[] { 3 }).Concat(Enumerable.Repeat((byte)4, 19)).Concat(new byte[] { 5 }).Concat(Enumerable.Repeat((byte)6, 20)).ToArray());
         var block = Enumerable.Range(0, 4096).Select(i => (byte)random.Next(256)).ToArray();
         yield return ("copies from exactly 4096 bytes back, from 3139 back and from 4129 back (too far)", block.Concat(block.Take(40)).Concat(new byte[] { 1, 2, 3 }).Concat(block.Skip(1000).Take(40)).Concat(new byte[] { 4 }).Concat(block.Skip(10).Take(40)).ToArray());
         yield return ("16 KB of tiles", Enumerable.Range(0, 512 * 32).Select(i => (byte)((i / 64) % 3 == 0 ? random.Next(4) : (i * 7) & 0xFF)).ToArray());
      }

      [Fact]
      public void Compress_DecompressesToTheSameBytes() {
         foreach (var (name, data) in Samples()) {
            var lz = TilesetLz.Compress(data);

            Assert.Equal(data, LZRun.Decompress(lz, 0));
            Assert.True(LZRun.IsCompressedLzData(lz, 0) == lz.Length, name);
         }
      }

      [Fact]
      public void Compress_NeverCopiesFromTheByteJustWritten() {
         // a copy at distance 1 would be "write the same byte again": the BIOS decompressor for picture memory can't do that
         foreach (var (name, data) in Samples()) {
            var lz = TilesetLz.Compress(data);
            var at = 4;
            var written = 0;
            while (written < data.Length) {
               var flags = lz[at++];
               for (int i = 0; i < 8 && written < data.Length; i++, flags <<= 1) {
                  if ((flags & 0x80) == 0) { at++; written++; continue; }
                  var length = (lz[at] >> 4) + 3;
                  var distance = (((lz[at] & 0xF) << 8) | lz[at + 1]) + 1;
                  at += 2;
                  Assert.True(distance >= 2 && distance <= 4096, name + ": distance " + distance);
                  Assert.True(distance <= written, name + ": copies from before the start");
                  written += length;
               }
            }
            Assert.Equal(data.Length, written);
         }
      }

      [Fact]
      public void Compress_IsNeverBiggerThanTheOlderCompression() {
         foreach (var (name, data) in Samples()) {
            var older = LZRun.Compress(data, 0, data.Length).Count;

            Assert.True(TilesetLz.Compress(data).Length <= older, name);
         }
      }

      [Fact]
      public void Compress_OddDistancesAndPositions_AreUsed() {
         // the older compression only copies from an even distance at an even position: 3 bytes back is out of its reach
         var data = Enumerable.Range(0, 300).Select(i => (byte)(10 + i % 3)).ToArray();

         Assert.True(TilesetLz.Compress(data).Length < LZRun.Compress(data, 0, data.Length).Count);
      }

      [Fact]
      public void Compress_NoData_IsJustTheHeader() {
         Assert.Equal(new byte[] { 0x10, 0, 0, 0 }, TilesetLz.Compress(new byte[0]));
      }
   }

   /// <summary>
   /// Tileset pictures: one ordinary PNG, 128 pixels wide, 16 tiles per row. Importing one changes tiles only, never a palette.
   /// </summary>
   public class TilesetPictureTests : BaseViewModelTestClass {
      private const int Header = 0x20, Tiles = 0x1000, Palettes = 0x2000, Blocks = 0x2400, Attributes = 0x2800, FreeSpace = 0x3000;
      private const int RawHeader = 0x60, RawTiles = 0x4000, RawTileCount = 8;
      private const int Max = 512;

      public TilesetPictureTests() : base(0x20000) {
         SetFullModel(0xFF);
         // a tileset record: isCompressed, isSecondary, 2 flags, tiles, palettes, blocks, attributes, animation (null)
         Model[Header] = 1;
         Model[Header + 1] = 0;
         AddPointer(Header + 4, Tiles);
         AddPointer(Header + 8, Palettes);
         AddPointer(Header + 12, Blocks);
         AddPointer(Header + 16, Attributes);
         Model.WriteValue(Token, Header + 20, 0);
         // 16 palettes that all differ (as GBA colours: red is the low 5 bits)
         for (int p = 0; p < 16; p++) for (int c = 0; c < 16; c++) Model.WriteMultiByteValue(Palettes + p * 32 + c * 2, 2, Token, Native(p, c));
         var tiles = LZRun.Compress(OriginalTiles, 0, OriginalTiles.Length).ToArray();
         Token.ChangeData(Model, Tiles, tiles);
         Model.FreeSpaceStart = FreeSpace;

         // a second tileset record, stored without compression (a following anchor ends the tiles)
         Model[RawHeader] = 0;
         Model[RawHeader + 1] = 0;
         AddPointer(RawHeader + 4, RawTiles);
         AddPointer(RawHeader + 8, Palettes);
         Token.ChangeData(Model, RawTiles, RawTilesData);
         AddPointer(0x70, RawTiles + RawTileCount * 32);
      }

      private const int TileCount = 40;
      private static (int r, int g, int b) Color5(int palette, int color) => ((color * 2 + palette) % 32, (color * 7 + palette * 3) % 32, 31 - color);
      private static int Native(int palette, int color) { var (r, g, b) = Color5(palette, color); return r | (g << 5) | (b << 10); }
      private static int Rgb(int palette, int color) { var (r, g, b) = Color5(palette, color); return ((r * 255 / 31) << 16) | ((g * 255 / 31) << 8) | (b * 255 / 31); }

      private static byte[] MakeTiles(int count, int seed) => Enumerable.Range(0, count * 32).Select(i => (byte)(((i / 32 + i % 32 / 4 + seed) % 15) | ((((i / 32) * 3 + i % 4 + seed) % 16) << 4))).ToArray();
      private static readonly byte[] OriginalTiles = MakeTiles(TileCount, 1);
      private static readonly byte[] RawTilesData = MakeTiles(RawTileCount, 5);

      private TilesetPictureImporter Importer(int header = Header) => new TilesetPictureImporter(Model, header);
      private byte[] ReadTilesNow() => LZRun.Decompress(Model.RawData, Model.ReadPointer(Header + 4));
      private byte[] PaletteBytes() => Model.RawData.Skip(Palettes).Take(0x200).ToArray();
      private byte[] Snapshot() => (byte[])Model.RawData.Clone();

      private int[,] ExportedPixels(TilesetPictureImporter importer, out IReadOnlyList<int> palette) {
         Assert.True(importer.TryExport(out var png, out _, out var error), error);
         Assert.True(PlainPng.TryDecode(png, out var picture, out error), error);
         palette = picture.Palette;
         return picture.Indexes;
      }

      private static byte[] Picture(int[,] pixels, IReadOnlyList<int> palette) => PlainPng.EncodeIndexed(pixels, palette);

      private static (int, int) TileOf(int tile) => ((tile % 16) * 8, (tile / 16) * 8);

      #region the picture of a tileset

      [Fact]
      public void Export_IsOneIndexedPictureOfAllTilesInRowsOfSixteen() {
         Assert.True(Importer().TryExport(out var png, out var count, out var error), error);

         Assert.Equal(TileCount, count);
         Assert.True(PlainPng.TryReadHeader(png, out var header, out error), error);
         Assert.Equal((128, 24, 4, PngColorType.Indexed), (header.Width, header.Height, header.BitDepth, header.ColorType));
         Assert.True(PlainPng.TryDecode(png, out var picture, out error), error);
         for (int tile = 0; tile < TileCount; tile++) {
            var (left, top) = TileOf(tile);
            for (int y = 0; y < 8; y++) {
               for (int x = 0; x < 8; x++) {
                  var b = OriginalTiles[tile * 32 + y * 4 + x / 2];
                  Assert.Equal(x % 2 == 0 ? b & 0xF : b >> 4, picture.Indexes[left + x, top + y]);
               }
            }
         }
         // (the rest of the last row is blank)
         for (int x = 0; x < 128; x++) for (int y = 16 + 8; y < 24; y++) Assert.Equal(0, picture.Indexes[x, y]);
      }

      [Fact]
      public void Export_CarriesTheFirstPaletteTheTilesetUses() {
         var primary = Importer();
         ExportedPixels(primary, out var palette);
         Assert.Equal(Enumerable.Range(0, 16).Select(c => Rgb(0, c)).ToList(), palette.Take(16).ToList());

         Model[Header + 1] = 1; // secondary: palette 6 in Emerald
         var secondary = Importer();
         ExportedPixels(secondary, out palette);
         Assert.Equal(Enumerable.Range(0, 16).Select(c => Rgb(6, c)).ToList(), palette.Take(16).ToList());
         Assert.Equal(6, secondary.PaletteNumber);

         SetGameCode("BPRE0"); // FireRed: palette 7
         ExportedPixels(Importer(), out palette);
         Assert.Equal(Enumerable.Range(0, 16).Select(c => Rgb(7, c)).ToList(), palette.Take(16).ToList());
      }

      [Fact]
      public void Export_ChangesNothing() {
         var before = Snapshot();

         Importer().TryExport(out _, out _, out _);

         Assert.Equal(before, Model.RawData);
      }

      [Fact]
      public void Export_OfATilesetWithNoTiles_SaysSo() {
         for (int i = 0; i < 4; i++) Model[Header + 4 + i] = 0; // (not a pointer)

         Assert.False(Importer().TryExport(out var png, out _, out var error));

         Assert.Null(png);
         Assert.NotEmpty(error);
      }

      #endregion

      #region reading a picture

      [Fact]
      public void Import_TheExportAsItIs_ChangesNothing() {
         var importer = Importer();
         importer.TryExport(out var png, out _, out _);

         Assert.True(importer.TryRead(png, out var picture, out var error), error);

         Assert.True(picture.IsSameAsBefore);
         Assert.Empty(picture.ChangedTiles);
         Assert.Equal(OriginalTiles, picture.TileData);
      }

      [Theory]
      [InlineData(64, 24)]
      [InlineData(136, 24)]
      [InlineData(127, 24)]
      [InlineData(256, 24)]
      public void Import_WrongWidth_IsRefused(int width, int height) {
         var importer = Importer();
         ExportedPixels(importer, out var palette);
         var before = Snapshot();

         Assert.False(importer.TryRead(Picture(new int[width, height], palette), out var picture, out var error));

         Assert.Null(picture);
         Assert.Contains("128 pixels wide", error);
         Assert.Contains("left as it is", error);
         Assert.Equal(before, Model.RawData);
      }

      [Theory]
      [InlineData(25)]
      [InlineData(23)]
      [InlineData(4)]
      public void Import_HeightThatIsNotAMultipleOfEight_IsRefused(int height) {
         var importer = Importer();
         ExportedPixels(importer, out var palette);

         Assert.False(importer.TryRead(Picture(new int[128, height], palette), out var picture, out var error));

         Assert.Null(picture);
         Assert.Contains("multiple of 8", error);
      }

      [Fact]
      public void Import_MoreTilesThanTheTilesetCanHave_IsRefused() {
         var importer = Importer();
         ExportedPixels(importer, out var palette);
         var pixels = new int[128, (Max / 16 + 1) * 8];
         pixels[5, 5] = 3;

         Assert.False(importer.TryRead(Picture(pixels, palette), out var picture, out var error));

         Assert.Null(picture);
         Assert.Contains("can only have 512 tiles", error);
      }

      [Fact]
      public void Import_AllTheTilesOfTheLimit_IsAccepted() {
         var importer = Importer();
         ExportedPixels(importer, out var palette);
         var pixels = new int[128, Max / 16 * 8];
         for (int x = 0; x < 128; x++) for (int y = 0; y < pixels.GetLength(1); y++) pixels[x, y] = (x + y) % 16;

         Assert.True(importer.TryRead(Picture(pixels, palette), out var picture, out var error), error);

         Assert.Equal(Max, picture.TileCount);
      }

      [Fact]
      public void Import_FewerRowsThanTheTileset_IsRefused() {
         var importer = Importer();
         ExportedPixels(importer, out var palette);

         Assert.False(importer.TryRead(Picture(new int[128, 16], palette), out var picture, out var error));

         Assert.Null(picture);
         Assert.Contains("delete tiles", error);
      }

      [Fact]
      public void Import_ColourNumberAboveFifteen_IsRefused() {
         var importer = Importer();
         var pixels = ExportedPixels(importer, out _);
         var wide = Enumerable.Range(0, 32).Select(i => i * 8 * 65536 + i * 8).ToList();
         pixels[9, 3] = 20;

         Assert.False(importer.TryRead(Picture(pixels, wide), out var picture, out var error));

         Assert.Null(picture);
         Assert.Contains("colour number 20", error);
      }

      [Fact]
      public void Import_NotAPicture_IsRefused() {
         var importer = Importer();

         Assert.False(importer.TryRead(System.Text.Encoding.ASCII.GetBytes("this is not a picture"), out var picture, out var error));

         Assert.Null(picture);
         Assert.Contains("left as it is", error);
      }

      [Fact]
      public void Import_EditedPixels_AreTheOnlyChange() {
         var importer = Importer();
         var pixels = ExportedPixels(importer, out var palette);
         var edits = new[] { (x: 3, y: 2, color: 9), (x: 17, y: 4, color: 1), (x: 100, y: 9, color: 15), (x: 5, y: 20, color: 2) };
         foreach (var e in edits) pixels[e.x, e.y] = pixels[e.x, e.y] == e.color ? (e.color + 1) % 16 : e.color;

         Assert.True(importer.TryRead(Picture(pixels, palette), out var picture, out var error), error);

         Assert.False(picture.IsSameAsBefore);
         Assert.Equal(edits.Select(e => (e.y / 8) * 16 + e.x / 8).Distinct().OrderBy(t => t).ToList(), picture.ChangedTiles.ToList());
         Assert.Equal(TileCount, picture.TileCount);
         // every nibble is the old one, except the edited pixels
         var expected = (byte[])OriginalTiles.Clone();
         foreach (var e in edits) {
            var at = ((e.y / 8) * 16 + e.x / 8) * 32 + (e.y % 8) * 4 + (e.x % 8) / 2;
            var color = pixels[e.x, e.y];
            expected[at] = (byte)(e.x % 2 == 0 ? (expected[at] & 0xF0) | color : (expected[at] & 0x0F) | (color << 4));
         }
         Assert.Equal(expected, picture.TileData);
      }

      [Fact]
      public void Import_PictureThatLooksLikeAnotherPalette_UsesTheColourNumbers() {
         var importer = Importer();
         var pixels = ExportedPixels(importer, out _);
         pixels[0, 0] = (pixels[0, 0] + 5) % 16;
         var other = Enumerable.Range(0, 16).Select(i => i * 0x101010).ToList();

         Assert.True(importer.TryRead(Picture(pixels, other), out var picture, out var error), error);

         Assert.True(picture.PaletteDiffers);
         Assert.False(picture.ColorsMatched);
         Assert.Equal(new[] { 0 }, picture.ChangedTiles.ToArray());
         Assert.Equal(pixels[0, 0], picture.TileData[0] & 0xF);
      }

      [Fact]
      public void Import_FullColourPicture_UsesTheNearestColourOfThePalette() {
         var importer = Importer();
         var pixels = ExportedPixels(importer, out var palette);
         // the same picture in full colour, except tile 0 pixel (0,0): a colour close to colour 7 (not exactly it)
         var close = (r: ((palette[7] >> 16) & 0xFF) + 2, g: ((palette[7] >> 8) & 0xFF) - 2, b: (palette[7] & 0xFF) + 1);
         var file = PlainPngTests.MakeRgba(128, 24, (x, y) => (x, y) == (0, 0) ? (close.r, close.g, close.b, 255) : (((palette[pixels[x, y]] >> 16) & 0xFF), (palette[pixels[x, y]] >> 8) & 0xFF, palette[pixels[x, y]] & 0xFF, 255));

         Assert.True(importer.TryRead(file, out var picture, out var error), error);

         Assert.True(picture.ColorsMatched);
         Assert.Equal(7, picture.TileData[0] & 0xF);
         Assert.Equal(new[] { 0 }, picture.ChangedTiles.ToArray());
      }

      [Fact]
      public void Import_FullColourPicture_TransparentPixelsAreColourZero() {
         var importer = Importer();
         var pixels = ExportedPixels(importer, out var palette);
         // alpha 127 is see-through, alpha 128 is not
         var file = PlainPngTests.MakeRgba(128, 24, (x, y) => {
            var c = palette[pixels[x, y]];
            var (r, g, b) = ((c >> 16) & 0xFF, (c >> 8) & 0xFF, c & 0xFF);
            if (y == 0 && x < 3) return (0, 255, 0, x == 0 ? 0 : x == 1 ? 127 : 128); // bright green: not close to any colour of the palette
            return (r, g, b, 255);
         });

         Assert.True(importer.TryRead(file, out var picture, out var error), error);

         Assert.Equal(2, picture.TransparentPixels);
         Assert.Equal(0, picture.TileData[0] & 0xF); // alpha 0
         Assert.Equal(0, picture.TileData[0] >> 4); // alpha 127
         var nearestToGreen = Enumerable.Range(0, 16).OrderBy(i => (int)Math.Pow((palette[i] >> 16) & 0xFF, 2) + (int)Math.Pow(((palette[i] >> 8) & 0xFF) - 255, 2) + (int)Math.Pow(palette[i] & 0xFF, 2)).First();
         Assert.Equal(nearestToGreen, picture.TileData[1] & 0xF); // alpha 128: matched with the nearest colour
      }

      [Fact]
      public void Import_FullColourPicture_KeepsTheNumberOfAPixelWhoseColourDidNotChange() {
         // colours 3 and 4 of palette 0 are made the same colour. Pixel (0,0) has number 4 now and is still that colour, so it stays 4.
         // Pixel (1,0) has another number now (9) and gets that colour: the lowest number with it, 3.
         Model.WriteMultiByteValue(Palettes + 4 * 2, 2, Token, Native(0, 3));
         var edited = (byte[])OriginalTiles.Clone();
         edited[0] = 0x94;
         Token.ChangeData(Model, Tiles, LZRun.Compress(edited, 0, edited.Length).ToArray());
         var importer = Importer();
         var pixels = ExportedPixels(importer, out var palette);
         Assert.Equal(palette[3], palette[4]);
         Assert.Equal((4, 9), (pixels[0, 0], pixels[1, 0]));
         var file = PlainPngTests.MakeRgba(128, 24, (x, y) => {
            var c = (x, y) == (1, 0) ? palette[3] : palette[pixels[x, y]];
            return ((c >> 16) & 0xFF, (c >> 8) & 0xFF, c & 0xFF, 255);
         });

         Assert.True(importer.TryRead(file, out var picture, out var error), error);

         Assert.Equal(4, picture.TileData[0] & 0xF);
         Assert.Equal(3, picture.TileData[0] >> 4);
         Assert.Equal(new[] { 0 }, picture.ChangedTiles.ToArray());
      }

      #endregion

      #region growing

      [Fact]
      public void Import_NewRowWithSomethingInIt_AddsTilesUpToTheLastOne() {
         var importer = Importer();
         var pixels = ExportedPixels(importer, out var palette);
         var taller = new int[128, pixels.GetLength(1) + 16];
         for (int x = 0; x < 128; x++) for (int y = 0; y < pixels.GetLength(1); y++) taller[x, y] = pixels[x, y];
         taller[2 * 8 + 3, 24 + 8 + 1] = 6; // tile 48 + 16 + 2 = 66

         Assert.True(importer.TryRead(Picture(taller, palette), out var picture, out var error), error);

         Assert.Equal(67, picture.TileCount);
         Assert.Equal(TileCount, picture.PreviousTileCount);
         Assert.Equal(Enumerable.Range(TileCount, 67 - TileCount).ToList(), picture.ChangedTiles.ToList());
         Assert.Equal(67 * 32, picture.TileData.Length);
      }

      [Fact]
      public void Import_NewRowsThatAreBlank_AddNothing() {
         var importer = Importer();
         var pixels = ExportedPixels(importer, out var palette);
         var taller = new int[128, pixels.GetLength(1) + 16];
         for (int x = 0; x < 128; x++) for (int y = 0; y < pixels.GetLength(1); y++) taller[x, y] = pixels[x, y];

         Assert.True(importer.TryRead(Picture(taller, palette), out var picture, out var error), error);

         Assert.True(picture.IsSameAsBefore);
      }

      #endregion

      #region writing

      [Fact]
      public void Write_EditedPicture_ChangesTheTilesAndNoPalette() {
         var importer = Importer();
         var pixels = ExportedPixels(importer, out var palette);
         pixels[10, 10] = (pixels[10, 10] + 3) % 16;
         pixels[70, 3] = (pixels[70, 3] + 7) % 16;
         importer.TryRead(Picture(pixels, palette), out var picture, out _);
         var palettesBefore = PaletteBytes();
         var before = Snapshot();
         var palettePointer = Model.ReadPointer(Header + 8);
         var token = new ModelDelta();

         importer.Write(token, picture);

         Assert.Equal(picture.TileData, ReadTilesNow());
         Assert.Equal(palettesBefore, PaletteBytes());
         Assert.Equal(palettePointer, Model.ReadPointer(Header + 8));
         // nothing else of the header changed, and the changed pixels are exactly the two edits
         Assert.Equal(before.Skip(Header).Take(4).ToArray(), Model.RawData.Skip(Header).Take(4).ToArray());
         Assert.Equal(before.Skip(Header + 8).Take(16).ToArray(), Model.RawData.Skip(Header + 8).Take(16).ToArray());
         var newPixels = ExportedPixels(Importer(), out _);
         for (int y = 0; y < 24; y++) for (int x = 0; x < 128; x++) Assert.Equal(pixels[x, y], newPixels[x, y]);
      }

      [Fact]
      public void Write_SmallEdit_StaysWhereTheTilesetIs() {
         // the tiles in the ROM were made by the older (bigger) compression, as most of the game's data is bigger than what the import writes
         var importer = Importer();
         var pixels = ExportedPixels(importer, out var palette);
         pixels[10, 10] = (pixels[10, 10] + 3) % 16;
         importer.TryRead(Picture(pixels, palette), out var picture, out _);

         var newStart = importer.Write(new ModelDelta(), picture);

         Assert.Equal(Tiles, newStart);
         Assert.Equal(Tiles, Model.ReadPointer(Header + 4));
         Assert.Equal(picture.TileData, ReadTilesNow());
         Assert.True(Model.GetNextRun(Tiles) is LzTilesetRun);
      }

      [Fact]
      public void Compress_GivesDataThatDecompressesToTheTiles() {
         foreach (var tiles in new[] { OriginalTiles, MakeTiles(1, 4), MakeTiles(512, 9), new byte[64] }) {
            var lz = TilesetPictureImporter.Compress(tiles);

            Assert.Equal(tiles, LZRun.Decompress(lz, 0));
         }
      }

      [Fact]
      public void TilesetFile_PlainTiles_ArePackedAsSmallAsTheImportPacksPictures() {
         // the old Import .bin takes a file of plain tiles too: it is compressed the same way, so it stays where the tileset is as often as a picture does
         Assert.True(MapRepointer.TryReadTilesetFile(OriginalTiles, Max, out var data, out var error), error);

         Assert.Equal(OriginalTiles, LZRun.Decompress(data, 0));
         Assert.True(data.Length < LZRun.Compress(OriginalTiles, 0, OriginalTiles.Length).Count);
      }

      [Fact]
      public void Write_Undo_RestoresTheOldTiles() {
         var importer = Importer();
         var pixels = ExportedPixels(importer, out var palette);
         pixels[10, 10] = (pixels[10, 10] + 3) % 16;
         importer.TryRead(Picture(pixels, palette), out var picture, out _);
         var before = Snapshot();
         var token = new ModelDelta();
         importer.Write(token, picture);
         Assert.NotEqual(before, Model.RawData);

         var redo = token.Revert(Model);

         Assert.Equal(before, Model.RawData);
         Assert.Equal(OriginalTiles, ReadTilesNow());
         redo.Revert(Model);
         Assert.Equal(picture.TileData, ReadTilesNow());
      }

      [Fact]
      public void Write_BiggerTileset_MovesAndTheHeaderFollows() {
         var importer = Importer();
         ExportedPixels(importer, out var palette);
         var pixels = new int[128, Max / 16 * 8];
         var random = new Random(5);
         for (int x = 0; x < 128; x++) for (int y = 0; y < pixels.GetLength(1); y++) pixels[x, y] = random.Next(16);
         Assert.True(importer.TryRead(Picture(pixels, palette), out var picture, out var error), error);
         var palettesBefore = PaletteBytes();
         var token = new ModelDelta();
         var before = Snapshot();

         var newStart = importer.Write(token, picture);

         Assert.NotEqual(Tiles, newStart);
         Assert.Equal(newStart, Model.ReadPointer(Header + 4));
         Assert.Equal(picture.TileData, ReadTilesNow());
         Assert.Equal(Max, ReadTilesNow().Length / 32);
         Assert.Equal(palettesBefore, PaletteBytes());
         // the moved tiles are a tileset run the program can draw from
         Assert.True(Model.GetNextRun(newStart) is LzTilesetRun);
         // and undo puts everything back
         token.Revert(Model);
         Assert.Equal(before, Model.RawData);
         Assert.Equal(Tiles, Model.ReadPointer(Header + 4));
      }

      [Fact]
      public void Write_TilesThatShrink_LeaveTheRestOfTheOldDataBlank() {
         var importer = Importer();
         var pixels = ExportedPixels(importer, out var palette);
         for (int x = 0; x < 128; x++) for (int y = 0; y < pixels.GetLength(1); y++) pixels[x, y] = 0; // all blank: compresses to almost nothing
         importer.TryRead(Picture(pixels, palette), out var picture, out _);
         var oldRunLength = new LZRun(Model, Tiles).Length;

         importer.Write(new ModelDelta(), picture);

         Assert.Equal(Tiles, Model.ReadPointer(Header + 4));
         Assert.Equal(new byte[TileCount * 32], ReadTilesNow());
         var newLength = new LZRun(Model, Tiles).Length;
         Assert.True(newLength < oldRunLength);
         Assert.All(Model.RawData.Skip(Tiles + newLength).Take(oldRunLength - newLength).ToArray(), b => Assert.Equal(0xFF, b));
      }

      #endregion

      #region tiles stored without compression

      private static int[,] RawPixels(byte[] tiles, int count) => TilesetPicture.ToPixels(tiles, count);

      [Fact]
      public void Uncompressed_ExportIsTheTilesAsTheyAre() {
         var importer = Importer(RawHeader);
         Assert.False(importer.IsCompressed);

         Assert.True(importer.TryExport(out var png, out var count, out var error), error);

         Assert.Equal(RawTileCount, count);
         PlainPng.TryDecode(png, out var picture, out _);
         Assert.Equal(8, picture.Height);
         var expected = RawPixels(RawTilesData, RawTileCount);
         for (int y = 0; y < 8; y++) for (int x = 0; x < 128; x++) Assert.Equal(expected[x, y], picture.Indexes[x, y]);
      }

      [Fact]
      public void Uncompressed_EditedPicture_IsWrittenWhereTheTilesAre() {
         var importer = Importer(RawHeader);
         var pixels = ExportedPixels(importer, out var palette);
         pixels[8 * 3 + 1, 4] = (pixels[8 * 3 + 1, 4] + 1) % 16;
         Assert.True(importer.TryRead(Picture(pixels, palette), out var picture, out var error), error);
         var before = Snapshot();

         var start = importer.Write(new ModelDelta(), picture);

         Assert.Equal(RawTiles, start);
         Assert.Equal(RawTiles, Model.ReadPointer(RawHeader + 4));
         Assert.Equal(picture.TileData, Model.RawData.Skip(RawTiles).Take(RawTileCount * 32).ToArray());
         var changed = Enumerable.Range(0, Model.Count).Where(i => before[i] != Model.RawData[i]).ToList();
         Assert.Single(changed);
         Assert.InRange(changed[0], RawTiles + 3 * 32, RawTiles + 4 * 32 - 1);
      }

      [Fact]
      public void Uncompressed_AnotherSize_IsRefused() {
         var importer = Importer(RawHeader);
         var pixels = ExportedPixels(importer, out var palette);
         var taller = new int[128, 16];
         for (int x = 0; x < 128; x++) for (int y = 0; y < 8; y++) taller[x, y] = pixels[x, y];
         taller[3, 12] = 2;
         var before = Snapshot();

         Assert.False(importer.TryRead(Picture(taller, palette), out var picture, out var error));

         Assert.Null(picture);
         Assert.Contains("stored without compression", error);
         Assert.Equal(before, Model.RawData);
      }

      [Fact]
      public void Uncompressed_ContentBeyondTheLastTile_IsRefused() {
         var importer = Importer(RawHeader);
         var pixels = ExportedPixels(importer, out var palette);
         pixels[8 * 12 + 2, 2] = 5; // tile 12: the tileset has 8

         Assert.False(importer.TryRead(Picture(pixels, palette), out var picture, out var error));

         Assert.Null(picture);
         Assert.Contains("only has 8 tiles", error);
      }

      [Fact]
      public void Uncompressed_BytesThatLookedLikeAPointerAndChange_AreNoPointerAnyMore() {
         // tile 2 of the tileset happens to hold a pointer-looking value that the model marked as a pointer; tile 5 holds another one
         AddPointer(RawTiles + 2 * 32 + 8, 0x9000);
         AddPointer(RawTiles + 5 * 32 + 4, 0x9100);
         var importer = Importer(RawHeader);
         var pixels = ExportedPixels(importer, out var palette);
         // change a pixel in tile 2 only (the pixels of the pointer's own bytes: row 2 of the tile is bytes 8..11)
         pixels[2 * 8 + 3, 2] = (pixels[2 * 8 + 3, 2] + 1) % 16;
         Assert.True(importer.TryRead(Picture(pixels, palette), out var picture, out var error), error);

         importer.Write(new ModelDelta(), picture);

         Assert.False(Model.GetNextRun(RawTiles + 2 * 32 + 8) is PointerRun run && run.Start == RawTiles + 2 * 32 + 8, "the pointer whose bytes changed is gone");
         Assert.True(Model.GetNextRun(RawTiles + 5 * 32 + 4) is PointerRun kept && kept.Start == RawTiles + 5 * 32 + 4, "the pointer whose bytes are the same stays");
         Assert.False(Model.GetNextRun(0x9000).PointerSources?.Contains(RawTiles + 2 * 32 + 8) == true, "the destination no longer lists it as a source");
      }

      #endregion
   }
}
