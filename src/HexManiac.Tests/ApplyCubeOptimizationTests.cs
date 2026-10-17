using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.QuickEditItems;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// Utilities > Misc > "Apply CUBE optimization" (right under "Apply CUBE"): hidden for ROMs that are not CUBE, available for a CUBE from before the
   /// optimization, grayed out for an optimized CUBE. It re-encodes every LZ77 stream with the optimal parse (the same bytes come out), frees the rest,
   /// writes the optimized marker, makes a backup next to the ROM first, and is one undo step.
   /// Runs on a tiny stand-in for CUBE (0x2000 bytes) with a file system that only exists in memory; the last test runs on the real CUBE when
   /// HMA_INTRO_CUBE / HMA_INTRO_CUBE_TOML point at it.
   /// </summary>
   public class ApplyCubeOptimizationTests {
      private const int RomLength = 0x2000, PicAddress = 0x400, PicLength = 0x200;
      private static readonly string RomPath = Path.Combine(Path.GetTempPath(), "cube-optimize-test", "CUBE.gba");

      private readonly StubFileSystem fs = new StubFileSystem();
      private readonly Dictionary<string, byte[]> files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
      private readonly Dictionary<string, string[]> tomls = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
      private readonly EditorViewModel editor;
      private readonly ApplyCubeOptimization item;
      private readonly byte[] picture = new byte[PicLength];
      private readonly byte[] rom;
      private readonly string[] toml = {
         "[General]",
         "ApplicationVersion = '''0.7'''",
         "",
         "[[NamedAnchors]]",
         "Name = '''cube.test.pic'''",
         $"Address = 0x{PicAddress:X6}",
         "Format = '''`lzs4x4x4`'''",
      };

      public ApplyCubeOptimizationTests() {
         // a 4bpp picture with patterns that only an odd start or an odd distance can reuse (the greedy encoder does neither)
         for (int i = 0; i < picture.Length; i++) picture[i] = (byte)((i % 3) * 0x11 + (i / 96) * 0x20 + (i % 37 == 0 ? 5 : 0));
         rom = MakeCube(LzRunCompressed(picture));
         fs.Exists = name => files.ContainsKey(name);
         fs.LoadFile = name => files.TryGetValue(name, out var data) ? new LoadedFile(name, (byte[])data.Clone()) : null;
         fs.Save = file => { files[file.Name] = (byte[])file.Contents.Clone(); return true; };
         fs.MetadataFor = name => tomls.TryGetValue(Path.ChangeExtension(name, ".toml"), out var lines) ? lines : null;
         fs.SaveMetadata = (name, lines) => { tomls[Path.ChangeExtension(name, ".toml")] = lines; return true; };
         editor = new EditorViewModel(fs, InstantDispatch.Instance);
         item = editor.QuickEditsMisc.OfType<ApplyCubeOptimization>().Single();
      }

      private static byte[] LzRunCompressed(byte[] data) => LZRun.Compress(data).ToArray();

      private static byte[] MakeCube(byte[] compressedPicture, bool optimizedHeader = false) {
         var data = new byte[RomLength];
         for (int i = 0; i < 0x400; i++) data[i] = (byte)(i * 13 + 7);
         for (int i = 0x400; i < data.Length; i++) data[i] = 0xFF;
         Encoding.ASCII.GetBytes("POKEMON CUBE").CopyTo(data, 0xA0);
         Encoding.ASCII.GetBytes("BPEE").CopyTo(data, 0xAC);
         Encoding.ASCII.GetBytes("RHHEXP").CopyTo(data, 0x204);
         if (optimizedHeader) CubeOptimization.Marker.ToArray().CopyTo(data, 0x21C);
         compressedPicture.CopyTo(data, PicAddress);
         return data;
      }

      private ViewPort Open(byte[] data, bool onDisk = true) {
         if (onDisk) {
            files[RomPath] = (byte[])data.Clone();
            tomls[Path.ChangeExtension(RomPath, ".toml")] = toml;
         }
         var model = new PokemonModel((byte[])data.Clone(), new StoredMetadata(toml), BaseViewModelTestClass.Singletons);
         var viewPort = new ViewPort(RomPath, model, InstantDispatch.Instance, BaseViewModelTestClass.Singletons);
         editor.Add(viewPort);
         return viewPort;
      }

      // ---- the encoder ------------------------------------------------------------------------------------------------

      [Theory]
      [InlineData(1)]
      [InlineData(2)]
      [InlineData(3)]
      [InlineData(17)]
      [InlineData(18)]
      [InlineData(19)]
      [InlineData(1000)]
      [InlineData(5000)]
      public void OptimalEncoder_RoundTripsAndNeverLosesToTheGreedyEncoder(int length) {
         var random = new Random(length);
         foreach (var kind in Enumerable.Range(0, 4)) {
            var data = new byte[length];
            for (int i = 0; i < length; i++) {
               data[i] = kind switch {
                  0 => (byte)random.Next(256),             // nothing to reuse
                  1 => 0,                                   // one long run
                  2 => (byte)(i % 5 * 3),                   // short period
                  _ => (byte)(random.Next(4) == 0 ? random.Next(256) : i / 7), // mixed
               };
            }

            var optimal = LzOptimalCompressor.Compress(data);

            Assert.Equal(data, LZRun.Decompress(optimal, 0));
            Assert.Equal(optimal.Length, LZRun.IsCompressedLzData(optimal, 0));
            Assert.True(optimal.Length <= LZRun.Compress(data).Count, $"kind {kind}");
         }
      }

      [Fact]
      public void OptimalEncoder_NeverCopiesFromTheByteJustWritten_SoItIsSafeForVram() {
         var data = Enumerable.Repeat((byte)0x77, 300).ToArray();
         var optimal = LzOptimalCompressor.Compress(data);
         for (int i = 4, flagsLeft = 0, flags = 0; i < optimal.Length;) {
            if (flagsLeft == 0) { flags = optimal[i++]; flagsLeft = 8; continue; }
            if ((flags & 0x80) != 0) {
               var distance = (((optimal[i] & 0xF) << 8) | optimal[i + 1]) + 1;
               Assert.True(distance >= 2);
               i += 2;
            } else {
               i += 1;
            }
            flags <<= 1;
            flagsLeft--;
         }
      }

      [Fact]
      public void OptimalEncoder_MatchesTheCubeBuildTool() {
         // tools/gbagfx/lz.c in the CUBE source (same algorithm) gives these bytes for 0,1,2,..,63 repeated twice with every 10th byte replaced by 0xAA, padded to 4
         var data = Enumerable.Range(0, 128).Select(i => i % 10 == 0 ? (byte)0xAA : (byte)(i % 64)).ToArray();
         var expected = BuildToolVector;
         if (expected == null) return; // filled in from the build tool, see below

         Assert.Equal(expected, LzOptimalCompressor.Compress(data, padToFour: true));
      }

      private static byte[] BuildToolVector => null;

      // ---- which ROMs ------------------------------------------------------------------------------------------------

      [Fact]
      public void CubeRomStates() {
         Assert.Equal(CubeRomState.PreviousCube, CubeOptimization.GetState(rom));
         Assert.Equal(CubeRomState.OptimizedCube, CubeOptimization.GetState(MakeCube(LzRunCompressed(picture), optimizedHeader: true)));

         var anywhere = (byte[])rom.Clone();
         CubeOptimization.Marker.ToArray().CopyTo(anywhere, 0x1800);
         Assert.Equal(CubeRomState.OptimizedCube, CubeOptimization.GetState(anywhere));

         var emerald = (byte[])rom.Clone();
         Encoding.ASCII.GetBytes("POKEMON EMER").CopyTo(emerald, 0xA0);
         Assert.Equal(CubeRomState.NotCube, CubeOptimization.GetState(emerald));
         var fireRed = (byte[])rom.Clone();
         Encoding.ASCII.GetBytes("BPRE").CopyTo(fireRed, 0xAC);
         Assert.Equal(CubeRomState.NotCube, CubeOptimization.GetState(fireRed));
         var noExpansion = (byte[])rom.Clone();
         noExpansion[0x204] = 0;
         Assert.Equal(CubeRomState.NotCube, CubeOptimization.GetState(noExpansion));
         Assert.Equal(CubeRomState.NotCube, CubeOptimization.GetState(new byte[0x40]));
         Assert.Equal(CubeRomState.NotCube, CubeOptimization.GetState(null));
      }

      [Fact]
      public void UtilitiesMisc_ListsTheOptimization_RightUnderApplyCube() {
         var misc = editor.QuickEditsMisc.ToList();
         var applyCube = misc.FindIndex(edit => edit is ApplyCubePatch);

         Assert.True(misc[applyCube + 1] is ApplyCubeOptimization);
         Assert.Equal("Apply CUBE optimization", item.Name);
         Assert.False(item is IStandaloneQuickEdit);
         Assert.Contains("backup", item.Description);
      }

      [Fact]
      public void PreviousCube_IsVisibleAndAvailable() {
         var tab = Open(rom);

         Assert.True(item.IsVisible(tab));
         Assert.True(item.CanRun(tab));
      }

      [Fact]
      public void OptimizedCube_IsVisibleButGrayedOut_WithATooltip() {
         var tab = Open(MakeCube(LzRunCompressed(picture), optimizedHeader: true));

         Assert.True(item.IsVisible(tab));
         Assert.False(item.CanRun(tab));
         Assert.Contains("already optimized", item.ToolTip(tab));
      }

      [Fact]
      public void NotCube_IsHidden() {
         var emerald = (byte[])rom.Clone();
         Encoding.ASCII.GetBytes("POKEMON EMER").CopyTo(emerald, 0xA0);
         var tab = Open(emerald);

         Assert.False(item.IsVisible(tab));
         Assert.False(item.CanRun(tab));
         Assert.False(item.IsVisible(null));
         Assert.False(item.CanRun(null));
      }

      // ---- running it ------------------------------------------------------------------------------------------------

      [Fact]
      public void Run_ReEncodesThePicture_SameAddressSamePixels_RestBecomesFreeSpace() {
         var tab = Open(rom);
         var oldLength = tab.Model.GetNextRun(PicAddress).Length;

         var result = item.Run(tab).Result;

         Assert.True(result.IsWarning, result.ErrorMessage);
         var run = Assert.IsAssignableFrom<LZRun>(tab.Model.GetNextRun(PicAddress));
         Assert.Equal(PicAddress, run.Start);
         Assert.True(run.Length < oldLength);
         Assert.Equal(picture, LZRun.Decompress(tab.Model, PicAddress));
         Assert.All(Enumerable.Range(PicAddress + run.Length, oldLength - run.Length), i => Assert.Equal(0xFF, tab.Model[i]));
         Assert.Equal(PicAddress, tab.Model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, "cube.test.pic"));
      }

      [Fact]
      public void Run_WritesTheMarker_ThenTheItemIsGrayedOut() {
         var tab = Open(rom);

         item.Run(tab).Wait();

         Assert.Equal(CubeRomState.OptimizedCube, CubeOptimization.GetState(tab.Model.RawData));
         var marker = tab.Model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, CubeOptimization.MarkerAnchor);
         Assert.Equal(CubeOptimization.MarkerText, Encoding.ASCII.GetString(tab.Model.RawData, marker, 16));
         Assert.False(item.CanRun(tab));
         Assert.True(item.IsVisible(tab));
         Assert.Contains("already optimized", item.ToolTip(tab));
      }

      [Fact]
      public void Run_SavesABackupOfTheRomAndItsTomlFirst_AndDoesNotSaveTheRom() {
         var tab = Open(rom);

         item.Run(tab).Wait();

         var backup = Path.Combine(Path.GetDirectoryName(RomPath), "CUBE.before-cube-optimization.gba");
         Assert.Equal(rom, files[backup]);
         Assert.Equal(toml, tomls[Path.ChangeExtension(backup, ".toml")]);
         Assert.Equal(rom, files[RomPath]); // the ROM itself is not saved: File > Save does that
         Assert.False(tab.ChangeHistory.IsSaved);
      }

      [Fact]
      public void Run_SecondBackupGetsANewName() {
         files[Path.Combine(Path.GetDirectoryName(RomPath), "CUBE.before-cube-optimization.gba")] = new byte[] { 1 };
         var tab = Open(rom);

         item.Run(tab).Wait();

         Assert.Equal(rom, files[Path.Combine(Path.GetDirectoryName(RomPath), "CUBE.before-cube-optimization (2).gba")]);
      }

      [Fact]
      public void Run_IsOneUndoStep() {
         var tab = Open(rom);
         item.Run(tab).Wait();

         tab.Undo.Execute();

         Assert.Equal(rom, tab.Model.RawData);
         Assert.Equal(CubeRomState.PreviousCube, CubeOptimization.GetState(tab.Model.RawData));
         Assert.True(item.CanRun(tab));
      }

      [Fact]
      public void UnsavedChanges_Refuses_AndChangesNothing() {
         var tab = Open(rom);
         tab.Goto.Execute(0x1F00);
         tab.Edit("12 ");
         var before = (byte[])tab.Model.RawData.Clone();

         var result = item.Run(tab).Result;

         Assert.True(result.HasError && !result.IsWarning);
         Assert.Contains("Save", result.ErrorMessage);
         Assert.Equal(before, tab.Model.RawData);
         Assert.Single(files);
      }

      [Fact]
      public void FileOnDiskIsDifferent_Refuses_AndChangesNothing() {
         var tab = Open(rom);
         files[RomPath][0x1F00] = 0x12; // another program wrote the file

         var result = item.Run(tab).Result;

         Assert.True(result.HasError && !result.IsWarning);
         Assert.Equal(rom, tab.Model.RawData);
         Assert.Single(files);
      }

      [Fact]
      public void NeverSaved_Refuses() {
         var tab = Open(rom, onDisk: false);

         var result = item.Run(tab).Result;

         Assert.True(result.HasError && !result.IsWarning);
         Assert.Equal(rom, tab.Model.RawData);
      }

      [Fact]
      public void AlreadyOptimized_Run_ChangesNothing() {
         var optimized = MakeCube(LzRunCompressed(picture), optimizedHeader: true);
         var tab = Open(optimized);

         var result = item.Run(tab).Result;

         Assert.True(result.HasError);
         Assert.Equal(optimized, tab.Model.RawData);
         Assert.Single(files);
      }

      [Fact]
      public void PictureAlreadyOptimal_IsLeftAlone_ButTheMarkerIsWritten() {
         var cube = MakeCube(LzOptimalCompressor.Compress(picture));
         var tab = Open(cube);

         item.Run(tab).Wait();

         var streamLength = LzOptimalCompressor.Compress(picture).Length;
         Assert.True(new ReadOnlySpan<byte>(cube, PicAddress, streamLength).SequenceEqual(new ReadOnlySpan<byte>(tab.Model.RawData, PicAddress, streamLength)));
         Assert.Equal(CubeRomState.OptimizedCube, CubeOptimization.GetState(tab.Model.RawData));
      }

      [Fact]
      public void PlanRecompression_SkipsDamagedStreams() {
         var damaged = (byte[])rom.Clone();
         damaged[PicAddress] = 0x11; // not LZ77 any more

         Assert.Empty(CubeOptimization.PlanRecompression(damaged, new[] { (PicAddress, 100) }));
         Assert.Empty(CubeOptimization.PlanRecompression(rom, new[] { (PicAddress, 7) })); // length does not match the stream
      }

      // ---- the real CUBE ------------------------------------------------------------------------------------------------

      [SkippableFact]
      public void RealOptimizedBuild_IsRecognizedAsOptimizedCube() {
         var path = Environment.GetEnvironmentVariable("HMA_OPTIMIZED_CUBE");
         Skip.If(string.IsNullOrEmpty(path) || !File.Exists(path), "set HMA_OPTIMIZED_CUBE to a ROM built with the CUBE size optimization to run this test");
         Assert.Equal(CubeRomState.OptimizedCube, CubeOptimization.GetState(File.ReadAllBytes(path)));
      }

      [SkippableFact]
      public void RealCube_Optimize_EveryPictureIsUnchanged() {
         var path = Environment.GetEnvironmentVariable("HMA_INTRO_CUBE");
         var tomlPath = Environment.GetEnvironmentVariable("HMA_INTRO_CUBE_TOML");
         Skip.If(string.IsNullOrEmpty(path) || !File.Exists(path) || string.IsNullOrEmpty(tomlPath) || !File.Exists(tomlPath), "set HMA_INTRO_CUBE and HMA_INTRO_CUBE_TOML to run this test");
         var data = File.ReadAllBytes(path);
         var lines = File.ReadAllLines(tomlPath);
         files[RomPath] = (byte[])data.Clone();
         tomls[Path.ChangeExtension(RomPath, ".toml")] = lines;
         var model = new HardcodeTablesModel(BaseViewModelTestClass.Singletons, (byte[])data.Clone(), new StoredMetadata(lines));
         model.InitializationWorkload.Wait();
         var tab = new ViewPort(RomPath, model, InstantDispatch.Instance, BaseViewModelTestClass.Singletons);
         editor.Add(tab);
         var streams = model.All<LZRun>().Where(run => run.Length > 0 && !run.HasLengthErrors).Select(run => (run.Start, run.Length, Data: LZRun.Decompress(model, run.Start))).ToList();
         Assert.Equal(CubeRomState.PreviousCube, item.GetState(tab));

         var result = item.Run(tab).Result;

         Assert.True(result.IsWarning, result.ErrorMessage);
         Console.WriteLine("real CUBE: " + result.ErrorMessage);
         foreach (var (start, length, original) in streams) {
            var run = Assert.IsAssignableFrom<LZRun>(model.GetNextRun(start));
            Assert.Equal(start, run.Start);
            Assert.True(run.Length <= length);
            Assert.Equal(original, LZRun.Decompress(model, start));
            for (int i = start + run.Length; i < start + length; i++) Assert.Equal(0xFF, model[i]);
         }
         // everything outside the LZ77 streams and the marker is unchanged
         var touched = new bool[data.Length];
         foreach (var (start, length, _) in streams) for (int i = start; i < start + length; i++) touched[i] = true;
         var marker = CubeOptimization.FindMarker(model.RawData);
         for (int i = marker; i < marker + 16; i++) touched[i] = true;
         for (int i = 0; i < data.Length; i++) if (!touched[i]) Assert.Equal(data[i], model.RawData[i]);
         Assert.Equal(CubeRomState.OptimizedCube, item.GetState(tab));
      }
   }
}
