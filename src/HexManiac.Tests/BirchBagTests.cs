using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// The Intro Manager button shows Professor Birch's bag, found through the code of the starter-choosing screen (BirchBag). A small ROM with the same shapes of code
   /// (the one the compiler of the original game makes, and the one of a newer compiler that shares a base register for the file's tables) tests the finder without a real ROM;
   /// the real games are tested when their files are around.
   /// </summary>
   public class BirchBagTests {
      private const int Code = 0x1000, Pool = Code + 0x100, Tiles = 0x4000, Map = 0x5000, Grass = 0x5800, Palette = 0x6000;
      private const short Transparent = ShortcutFallbackIcons.Transparent;

      private static void Half(byte[] data, int address, int half) { data[address] = (byte)half; data[address + 1] = (byte)(half >> 8); }
      private static void Word(byte[] data, int address, int word) { for (int i = 0; i < 4; i++) data[address + i] = (byte)(word >> (8 * i)); }
      private static void Pointer(byte[] data, int literal, int target) => Word(data, literal, 0x08000000 + target);

      // ldr rX, [pc, #imm] that loads the literal at an address
      private static void Ldr(byte[] data, int address, int register, int literal) => Half(data, address, 0x4800 | (register << 8) | ((literal - ((address + 4) & ~3)) / 4));

      // an LZ77 stream that only has literals: valid, just not smaller
      private static void Lz(byte[] data, int address, byte[] plain) {
         data[address] = 0x10; data[address + 1] = (byte)plain.Length; data[address + 2] = (byte)(plain.Length >> 8); data[address + 3] = (byte)(plain.Length >> 16);
         int at = address + 4;
         for (int i = 0; i < plain.Length; i += 8) {
            data[at++] = 0;
            for (int j = 0; j < 8; j++) data[at++] = plain[i + j];
         }
      }

      private static byte[] CreateRom(bool sharedBase) {
         var data = new byte[0x8000];
         for (int i = 0; i < Code; i++) data[i] = 0xFF;
         for (int i = Code; i < 0x2000; i += 2) Half(data, i, 0x46C0); // nops
         // the picture: tile 0 is see-through, tiles 1+ are colour 1; the tilemap (32x20) has a block of 10x8 tiles of tile 1
         var tiles = new byte[0x800];
         for (int i = 32; i < tiles.Length; i++) tiles[i] = 0x11;
         var map = new byte[0x500];
         for (int y = 2; y < 10; y++) for (int x = 4; x < 14; x++) map[(y * 32 + x) * 2] = 1;
         Lz(data, Tiles, tiles);
         Lz(data, Map, map);
         Lz(data, Grass, new byte[0x500]);
         Word(data, Palette + 2, 0x001F); // colour 1 is red
         // the code: tiles, tilemap, grass tilemap, then the palette
         var pool = Pool;
         Pointer(data, pool, Tiles); Pointer(data, pool + 4, Map); Word(data, pool + 8, 0x06003000); Pointer(data, pool + 12, Grass); Word(data, pool + 16, 0x06003800);
         Ldr(data, Code, 0, pool); Ldr(data, Code + 6, 0, pool + 4); Ldr(data, Code + 8, 1, pool + 8);
         Ldr(data, Code + 12, 0, pool + 12); Ldr(data, Code + 14, 1, pool + 16);
         if (!sharedBase) {
            Pointer(data, pool + 20, Palette);
            Ldr(data, Code + 0x40, 0, pool + 20); Half(data, Code + 0x42, 0x2100); Half(data, Code + 0x44, 0x2240);
         } else {
            // a base register (r4) = the start of the file's tables, and the palette is 0x20 after it: mov r0, r4 ; movs r2, #0x40 ; movs r1, #0 ; adds r0, #0x20
            Pointer(data, pool + 20, Palette - 0x20);
            Ldr(data, Code + 0x20, 4, pool + 20);
            Half(data, Code + 0x40, 0x0020); Half(data, Code + 0x42, 0x2240); Half(data, Code + 0x44, 0x2100); Half(data, Code + 0x46, 0x3020);
         }
         return data;
      }

      [Theory]
      [InlineData(false)]
      [InlineData(true)]
      public void TheBag_IsFoundThroughTheCode_WithEitherWayOfLoadingThePalette(bool sharedBase) {
         var data = CreateRom(sharedBase);

         Assert.True(BirchBag.TryFind(data, out var location));
         Assert.Equal((Tiles, Map, Palette), (location.Tiles, location.Tilemap, location.Palette));
      }

      [Fact]
      public void TheBag_IsCutOutAndHalved_AndTheGrassIsSeeThrough() {
         var data = CreateRom(false);

         var pixels = BirchBag.Draw(data, Transparent, out var width, out var height);

         Assert.NotNull(pixels);
         Assert.Equal((40, 32), (width, height)); // 80x64 pixels of tiles, halved
         Assert.Equal(width * height, pixels.Length);
         Assert.All(pixels, pixel => Assert.Equal(0x7C00, pixel)); // red, in the program's pixel format (red in the high bits)
      }

      [Fact]
      public void TheBag_MovedToAnotherPlace_IsStillFound() {
         var data = CreateRom(false);
         var moved = new byte[0x10000];
         Array.Copy(data, moved, data.Length);
         Array.Copy(data, Tiles, moved, 0x9000, 0x2100); // a second copy of the pieces; the code points at the new one
         Pointer(moved, Pool, 0x9000);
         Pointer(moved, Pool + 4, 0x9000 + (Map - Tiles));
         Pointer(moved, Pool + 12, 0x9000 + (Grass - Tiles));
         Pointer(moved, Pool + 20, 0x9000 + (Palette - Tiles));

         Assert.True(BirchBag.TryFind(moved, out var location));
         Assert.Equal(0x9000, location.Tiles);
         Assert.NotNull(BirchBag.Draw(moved, Transparent, out _, out _));
      }

      [Fact]
      public void ARomWithoutTheScreen_HasNoBag_AndNothingThrows() {
         Assert.False(BirchBag.TryFind(new byte[0x8000], out _));
         Assert.False(BirchBag.TryFind(new byte[10], out _));
         Assert.False(BirchBag.TryFind(null, out _));
         Assert.Null(BirchBag.Draw(new byte[0x8000], Transparent, out var width, out var height));
         Assert.Equal((0, 0), (width, height));
      }

      [Fact]
      public void ADamagedPicture_HasNoBag() {
         var data = CreateRom(false);
         data[Tiles] = 0x11; // not LZ any more

         Assert.False(BirchBag.TryFind(data, out _));
      }

      [Fact]
      public void ThePicture_IsNeverTheSeeThroughColour() {
         var data = CreateRom(false);
         Word(data, Palette + 2, 0x7C1F >> 0); // colour 1 becomes the colour that stands for 'see through' once its red and blue are swapped back

         var pixels = BirchBag.Draw(data, Transparent, out _, out _);

         Assert.NotNull(pixels);
         Assert.DoesNotContain(pixels, pixel => pixel == Transparent);
         Assert.True(pixels.Length > 0);
      }
   }

   /// <summary>The real games, when their files are around (HMA_INTRO_EMERALD, HMA_INTRO_CUBE). Skipped otherwise, like the other real ROM tests.</summary>
   public class BirchBagRealRomTests {
      private static byte[] Load(string variable) {
         var path = Environment.GetEnvironmentVariable(variable);
         Skip.If(string.IsNullOrEmpty(path) || !File.Exists(path), $"set {variable} to a ROM to run this test");
         return File.ReadAllBytes(path);
      }

      [SkippableTheory]
      [InlineData("HMA_INTRO_EMERALD")]
      [InlineData("HMA_INTRO_CUBE")]
      public void TheRealBag_IsFound(string variable) {
         var data = Load(variable);

         var pixels = BirchBag.Draw(data, ShortcutFallbackIcons.Transparent, out var width, out var height);

         Assert.NotNull(pixels);
         Assert.InRange(width, 50, 60);
         Assert.InRange(height, 28, 36);
         Assert.True(pixels.Count(pixel => pixel != ShortcutFallbackIcons.Transparent) > width * height / 3);
      }
   }
}
