using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// The map editor's Import buttons for tilesets and palettes take a file from the user: a wrong file must not damage the ROM.
   /// </summary>
   public class MapFileImportTests {
      private const int MaxTiles = 512;

      private static byte[] Tiles(int count) => (count * 0x20).Range().Select(i => (byte)(i % 7 == 0 ? 0x21 : 0x33)).ToArray();

      [Fact]
      public void TilesetFile_Empty_IsRefused() {
         Assert.False(MapRepointer.TryReadTilesetFile(new byte[0], MaxTiles, out var data, out var error));
         Assert.Null(data);
         Assert.NotEmpty(error);
      }

      [Fact]
      public void TilesetFile_RandomBytes_IsRefused() {
         var junk = 200.Range().Select(i => (byte)(i * 37 + 11)).ToArray();
         Assert.False(MapRepointer.TryReadTilesetFile(junk, MaxTiles, out var data, out var error));
         Assert.Null(data);
         Assert.NotEmpty(error);
      }

      [Fact]
      public void TilesetFile_LzData_IsUsedAsItIs() {
         var raw = Tiles(8);
         var lz = LZRun.Compress(raw, 0, raw.Length).ToArray();

         Assert.True(MapRepointer.TryReadTilesetFile(lz, MaxTiles, out var data, out var error));

         Assert.Equal(lz, data);
         Assert.Null(error);
      }

      [Fact]
      public void TilesetFile_LzDataWithTooManyTiles_IsRefused() {
         var raw = Tiles(MaxTiles + 1);
         var lz = LZRun.Compress(raw, 0, raw.Length).ToArray();

         Assert.False(MapRepointer.TryReadTilesetFile(lz, MaxTiles, out var data, out var error));

         Assert.Null(data);
         Assert.Contains(MaxTiles.ToString(), error);
      }

      [Fact]
      public void TilesetFile_PlainTiles_AreCompressed() {
         var raw = Tiles(8);

         Assert.True(MapRepointer.TryReadTilesetFile(raw, MaxTiles, out var data, out var error));

         Assert.Equal(raw, LZRun.Decompress(data, 0));
      }

      [Fact]
      public void TilesetFile_PlainTilesThatStartLikeLzData_AreStillPlainTiles() {
         var raw = Tiles(4);
         raw[0] = 0x10; // the marker of LZ data, but the rest of the file is not LZ data

         Assert.True(MapRepointer.TryReadTilesetFile(raw, MaxTiles, out var data, out var error));

         Assert.Equal(raw, LZRun.Decompress(data, 0));
      }

      [Fact]
      public void TilesetFile_MoreTilesThanTheMapCanUse_IsRefused() {
         Assert.False(MapRepointer.TryReadTilesetFile(Tiles(MaxTiles + 1), MaxTiles, out var data, out var error));
         Assert.Null(data);
         Assert.NotEmpty(error);
      }

      private static string PaletteFile(int colors) => "JASC-PAL\n0100\n" + colors + "\n" + string.Join("\n", colors.Range().Select(i => $"{i % 32 * 8} {(i * 3) % 32 * 8} {(i * 5) % 32 * 8}")) + "\n";

      [Fact]
      public void PaletteFile_SixteenColors_AllAreRead() {
         var colors = MapRepointer.ReadPaletteFile(PaletteFile(16));
         Assert.Equal(16, colors.Count);
      }

      [Fact]
      public void PaletteFile_TwoHundredFiftySixColors_OnlyTheFirstSixteenAreRead() {
         // writing all of them would spill 480 bytes past the 16 palettes of a tileset
         var colors = MapRepointer.ReadPaletteFile(PaletteFile(256));
         Assert.Equal(16, colors.Count);
         Assert.Equal(MapRepointer.ReadPaletteFile(PaletteFile(16)), colors);
      }

      [Fact]
      public void PaletteFile_FewerColorsAndJunkLines_OnlyTheColorsAreRead() {
         var colors = MapRepointer.ReadPaletteFile("JASC-PAL\n0100\n3\n8 16 24\nnot a color\n248 248 248\n1 2\n0 0 0\n");
         Assert.Equal(3, colors.Count);
         Assert.Equal(UncompressedPaletteColor.Pack(1, 2, 3), colors[0]);
         Assert.Equal(UncompressedPaletteColor.Pack(31, 31, 31), colors[1]);
      }

      [Fact]
      public void PaletteFile_ExportedColorComesBackTheSame() {
         for (int value = 0; value < 32; value++) {
            var exported = value * 255 / 31; // what ExportPal writes for one channel
            var colors = MapRepointer.ReadPaletteFile($"{exported} {exported} {exported}");
            Assert.Equal(UncompressedPaletteColor.Pack(value, value, value), colors.Single());
         }
      }
   }
}
