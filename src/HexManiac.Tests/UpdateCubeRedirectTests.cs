using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.CubeUpdate;
using System;
using System.Threading;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// A layout of a map that the author pointed to another tileset: after "Update CUBE" it still points there,
   /// and the tileset that it used to point to (shared with other maps) is unchanged.
   /// </summary>
   public class UpdateCubeRedirectTests {
      private const int Layouts = 0x100, HeaderA = 0x200, HeaderB = 0x210, GraphicsA = 0x300, GraphicsB = 0x310;
      private static readonly string[] Toml = {
         "[[NamedAnchors]]",
         "Name = '''layouts'''",
         $"Address = 0x{Layouts:X}",
         "Format = '''[tileset<[flags:: gfx<[v::]1>]1>]3'''",
         "",
      };

      private static void Pointer(byte[] rom, int at, int destination) {
         var value = destination + 0x08000000;
         for (int i = 0; i < 4; i++) rom[at + i] = (byte)(value >> (8 * i));
      }

      private static byte[] Rom(int secondLayoutUses) {
         var rom = new byte[0x1000];
         Pointer(rom, Layouts, HeaderA);
         Pointer(rom, Layouts + 4, secondLayoutUses);
         Pointer(rom, Layouts + 8, HeaderB);
         rom[HeaderA] = 5; Pointer(rom, HeaderA + 4, GraphicsA);
         rom[HeaderB] = 5; Pointer(rom, HeaderB + 4, GraphicsB);
         rom[GraphicsA] = 0x11; rom[GraphicsB] = 0x22;
         return rom;
      }

      private static HardcodeTablesModel Load(byte[] data, string[] toml) {
         var model = new HardcodeTablesModel(BaseViewModelTestClass.Singletons, data, new StoredMetadata(toml));
         model.InitializationWorkload.Wait();
         return model;
      }

      private static int Read(byte[] rom, int at) => (rom[at] | (rom[at + 1] << 8) | (rom[at + 2] << 16) | (rom[at + 3] << 24)) & 0x1FFFFFF;

      [Fact]
      public void AnAuthorsOtherTileset_IsKept_AndTheSharedOneIsNotChanged() {
         byte[] result = null;
         Exception error = null;
         var thread = new Thread(() => {
            try {
               var stock = Rom(HeaderA);
               var oldModel = Load((byte[])stock.Clone(), Toml);
               var fingerprint = CubeFingerprint.Compute(new RomGraph(oldModel));
               var user = Load(Rom(HeaderB), Toml);
               var target = Load((byte[])stock.Clone(), Toml);
               var updater = new CubeUpdater(user, target, fingerprint);
               updater.Run();
               result = updater.Result.RawData;
            } catch (Exception ex) { error = ex; }
         }, 64 * 1024 * 1024);
         thread.Start(); thread.Join();
         if (error != null) throw error;

         Assert.Equal(HeaderA, Read(result, Layouts));
         Assert.Equal(HeaderB, Read(result, Layouts + 4));
         Assert.Equal(HeaderB, Read(result, Layouts + 8));
         Assert.Equal(GraphicsA, Read(result, HeaderA + 4));  // the tileset the other maps use still has its own graphics
         Assert.Equal(GraphicsB, Read(result, HeaderB + 4));
      }
   }
}
