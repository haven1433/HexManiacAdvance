using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.CubeUpdate;
using HavenSoft.HexManiac.Core.Models.Runs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// "Update CUBE" on real ROMs: an old CUBE (UPD_OLD + UPD_OLD_TOML) that gets hacked in a few ways, and the new CUBE (UPD_NEW + UPD_NEW_TOML).
   /// </summary>
   public class UpdateCubeScenarioTests {
      private static string Env(string name) => Environment.GetEnvironmentVariable(name);

      private static (byte[] rom, string[] toml)? Read(string rom, string toml) {
         if (string.IsNullOrEmpty(Env(rom)) || string.IsNullOrEmpty(Env(toml))) return null;
         return (File.ReadAllBytes(Env(rom)), File.ReadAllLines(Env(toml)));
      }

      private static IDataModel Load((byte[] rom, string[] toml) cube) => CubeScriptsTests.Load((byte[])cube.rom.Clone(), cube.toml);

      private static string[] Export(IDataModel model) {
         var references = BaseViewModelTestClass.Singletons.GameReferenceTables.TryGetValue(model.GetGameCode(), out var refTable) ? refTable : null;
         return model.ExportMetadata(references, BaseViewModelTestClass.Singletons.MetadataInfo).Serialize();
      }

      private static void OnBigStack(Action action) {
         Exception error = null;
         var thread = new Thread(() => { try { action(); } catch (Exception ex) { error = ex; } }, 512 * 1024 * 1024);
         thread.Start(); thread.Join();
         if (error != null) throw new Exception(error.Message, error);
      }

      /// <summary>Hacks a stock old CUBE with the edit, saves it the way HMA would (rom + exported metadata), and updates it.</summary>
      private static (IDataModel result, CubeUpdateReport report, string[] toml) Update(Action<IDataModel, ModelDelta> hack) {
         var old = Read("UPD_OLD", "UPD_OLD_TOML").Value;
         var cube = Read("UPD_NEW", "UPD_NEW_TOML").Value;
         (IDataModel result, CubeUpdateReport report, string[] toml) output = default;
         OnBigStack(() => {
            var oldModel = Load(old);
            var oldFingerprint = CubeFingerprint.Compute(new RomGraph(oldModel));
            var hacked = Load(old);
            hack?.Invoke(hacked, new ModelDelta());
            var userModel = CubeScriptsTests.Load((byte[])hacked.RawData.Clone(), Export(hacked));
            var newModel = Load(cube);
            var updater = new CubeUpdater(userModel, newModel, oldFingerprint);
            updater.Run();
            var toml = Export(updater.Result);
            var reloaded = CubeScriptsTests.Load((byte[])updater.Result.RawData.Clone(), toml);
            output = (reloaded, updater.Report, toml);
         });
         return output;
      }

      private static ModelTable Table(IDataModel model, string anchor, ModelDelta token) {
         var address = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, anchor);
         return new ModelTable(model, address, () => token);
      }

      private static Dictionary<ulong, ulong> Fingerprint(IDataModel model) {
         Dictionary<ulong, ulong> result = null;
         OnBigStack(() => result = CubeFingerprint.Compute(new RomGraph(model)));
         return result;
      }

      [SkippableFact]
      public void NoEdits_GivesTheNewCube() {
         Skip.If(Read("UPD_OLD", "UPD_OLD_TOML") == null || Read("UPD_NEW", "UPD_NEW_TOML") == null, "set UPD_OLD, UPD_OLD_TOML, UPD_NEW, UPD_NEW_TOML");
         var (result, report, _) = Update(null);
         Console.WriteLine(report);
         var expected = Fingerprint(Load(Read("UPD_NEW", "UPD_NEW_TOML").Value));
         var actual = Fingerprint(result);
         var different = expected.Count(pair => !actual.TryGetValue(pair.Key, out var hash) || hash != pair.Value);
         Console.WriteLine($"nodes that differ from the new CUBE: {different} of {expected.Count}");
         Assert.Equal(0, report.NodesEdited);
         Assert.Equal(0, different);
      }

      [SkippableFact]
      public void AStatEdit_IsCarriedOver_AndNothingElseChanges() {
         Skip.If(Read("UPD_OLD", "UPD_OLD_TOML") == null || Read("UPD_NEW", "UPD_NEW_TOML") == null, "set UPD_OLD, UPD_OLD_TOML, UPD_NEW, UPD_NEW_TOML");
         var (result, report, _) = Update((model, token) => {
            var stats = Table(model, "data.pokemon.stats", token);
            stats[25].SetValue("hp", 99);
         });
         Console.WriteLine(report);
         Assert.Equal(99, Table(result, "data.pokemon.stats", new ModelDelta())[25].GetValue("hp"));
         var expected = Fingerprint(Load(Read("UPD_NEW", "UPD_NEW_TOML").Value));
         var actual = Fingerprint(result);
         var different = expected.Count(pair => !actual.TryGetValue(pair.Key, out var hash) || hash != pair.Value);
         Console.WriteLine($"nodes that differ from the new CUBE: {different}");
         Assert.Equal(1, different);
      }

      [SkippableFact]
      public void ALongerMoveName_IsCarriedOverToFreeSpace() {
         Skip.If(Read("UPD_OLD", "UPD_OLD_TOML") == null || Read("UPD_NEW", "UPD_NEW_TOML") == null, "set UPD_OLD, UPD_OLD_TOML, UPD_NEW, UPD_NEW_TOML");
         var longName = "MEGA TACKLE OF DOOM";
         var (result, report, _) = Update((model, token) => {
            var moves = Table(model, "data.pokemon.moves.stats", token);
            moves[33].SetStringValue("name", longName);
         });
         Console.WriteLine(report);
         Assert.Equal(longName, Table(result, "data.pokemon.moves.stats", new ModelDelta())[33].GetStringValue("name"));
         Assert.Equal("Scratch", Table(result, "data.pokemon.moves.stats", new ModelDelta())[10].GetStringValue("name"));
      }

      private static ITableRun Grow(IDataModel model, ModelDelta token, string anchor, int rows) {
         var address = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, anchor);
         var run = (ITableRun)model.GetNextRun(address);
         var grown = model.RelocateForExpansion(token, run, run.Length + rows * run.ElementLength);
         grown = grown.Append(token, rows);
         model.ObserveRunWritten(token, grown);
         return grown;
      }

      [SkippableFact]
      public void TablesTheAuthorMadeLonger_GrowInTheNewCube_WithTheirRows() {
         Skip.If(Read("UPD_OLD", "UPD_OLD_TOML") == null || Read("UPD_NEW", "UPD_NEW_TOML") == null, "set UPD_OLD, UPD_OLD_TOML, UPD_NEW, UPD_NEW_TOML");
         int trainerCount = 0, moveCount = 0;
         var (result, report, _) = Update((model, token) => {
            var trainers = Grow(model, token, "data.trainers.stats", 3);
            trainerCount = trainers.ElementCount;
            var table = new ModelTable(model, trainers.Start, () => token, trainers);
            table[trainerCount - 1].SetValue("item1", 13);
            var moves = Grow(model, token, "data.pokemon.moves.stats", 1);
            moveCount = moves.ElementCount;
            var moveTable = new ModelTable(model, moves.Start, () => token, moves);
            moveTable[moveCount - 1].SetStringValue("name", "BRAND NEW MOVE");
         });
         Console.WriteLine(report);
         var newTrainers = Table(result, "data.trainers.stats", new ModelDelta());
         Assert.Equal(trainerCount, newTrainers.Count);
         Assert.Equal(13, newTrainers[trainerCount - 1].GetValue("item1"));
         var newMoves = Table(result, "data.pokemon.moves.stats", new ModelDelta());
         Assert.Equal(moveCount, newMoves.Count);
         Assert.Equal("BRAND NEW MOVE", newMoves[moveCount - 1].GetStringValue("name"));
         Assert.Equal("Scratch", newMoves[10].GetStringValue("name"));
         Assert.Equal(2, report.TablesGrown);
      }

      [SkippableFact]
      public void NestedTablesOfASpecies_AreCarriedOver() {
         Skip.If(Read("UPD_OLD", "UPD_OLD_TOML") == null || Read("UPD_NEW", "UPD_NEW_TOML") == null, "set UPD_OLD, UPD_OLD_TOML, UPD_NEW, UPD_NEW_TOML");
         var (result, report, _) = Update((model, token) => {
            var stats = Table(model, "data.pokemon.stats", token);
            var species = stats[4];
            species.SetValue("catchRate", 111);
            species.SetValue("ability1", 7);
            var moves = species.GetSubTable("levelUpMoves");
            moves[0].SetValue("level", 5);
            species.SetStringValue("name", "LONGISH NAME");
         });
         Console.WriteLine(report);
         var stock = Table(Load(Read("UPD_NEW", "UPD_NEW_TOML").Value), "data.pokemon.stats", new ModelDelta());
         var after = Table(result, "data.pokemon.stats", new ModelDelta());
         Assert.Equal(111, after[4].GetValue("catchRate"));
         Assert.Equal(7, after[4].GetValue("ability1"));
         Assert.Equal(5, after[4].GetSubTable("levelUpMoves")[0].GetValue("level"));
         Assert.Equal("LONGISH NAME", after[4].GetStringValue("name"));
         Assert.Equal(stock[5].GetValue("catchRate"), after[5].GetValue("catchRate"));
         Assert.Equal(stock[4].GetValue("baseExp"), after[4].GetValue("baseExp"));
      }
   }

   /// <summary>Writes cube_versions.fp (what each older CUBE looks like, compared to the current one). Run when a new CUBE round is made.</summary>
   public class UpdateCubeVersionFileGenerator {
      [SkippableFact]
      public void WriteVersionFile() {
         var versions = Environment.GetEnvironmentVariable("UPD_VERSIONS");
         var newRom = Environment.GetEnvironmentVariable("UPD_NEW"); var newToml = Environment.GetEnvironmentVariable("UPD_NEW_TOML");
         var output = Environment.GetEnvironmentVariable("UPD_FP_OUT");
         Skip.If(string.IsNullOrEmpty(versions) || string.IsNullOrEmpty(newRom) || string.IsNullOrEmpty(output), "set UPD_VERSIONS (rom|toml;rom|toml), UPD_NEW, UPD_NEW_TOML, UPD_FP_OUT");
         var singletons = BaseViewModelTestClass.Singletons;
         CubeUpdateService.OnBigStack(() => {
            var current = CubeUpdateService.Fingerprint(CubeUpdateService.Load(singletons, File.ReadAllBytes(newRom), File.ReadAllLines(newToml)));
            var deltas = new List<CubeFingerprint.VersionDelta>();
            foreach (var version in versions.Split(';')) {
               var parts = version.Split('|');
               var rom = File.ReadAllBytes(parts[0]);
               var stock = CubeUpdateService.Load(singletons, (byte[])rom.Clone(), File.ReadAllLines(parts[1]));
               // what an author's copy looks like after HMA saved it: the exported metadata of the stock ROM
               var saved = CubeUpdateService.Load(singletons, (byte[])rom.Clone(), CubeUpdateService.Export(singletons, stock));
               var name = "CUBE " + Path.GetFileName(parts[0]).Replace("prev_", "").Replace("_CUBE.gba", "");
               var delta = CubeFingerprint.Diff(name, CubeUpdateService.Fingerprint(saved), current);
               Console.WriteLine($"{name}: {delta.Changes.Count} nodes differ from the current CUBE");
               deltas.Add(delta);
            }
            File.WriteAllBytes(output, CubeFingerprint.Write(deltas));
            Console.WriteLine($"wrote {output}: {new FileInfo(output).Length:N0} bytes");
         });
      }
   }

   public class UpdateCubeServiceTests {
      [SkippableFact]
      public void EveryKnownVersion_IsDetected_AndKeepsItsEdits() {
         var versions = Environment.GetEnvironmentVariable("UPD_VERSIONS");
         var newRom = Environment.GetEnvironmentVariable("UPD_NEW"); var newToml = Environment.GetEnvironmentVariable("UPD_NEW_TOML");
         var fp = Environment.GetEnvironmentVariable("UPD_FP_OUT");
         Skip.If(string.IsNullOrEmpty(versions) || string.IsNullOrEmpty(newRom) || string.IsNullOrEmpty(fp) || !File.Exists(fp), "set UPD_VERSIONS, UPD_NEW, UPD_NEW_TOML, UPD_FP_OUT (existing file)");
         var singletons = BaseViewModelTestClass.Singletons;
         var known = CubeFingerprint.Read(File.ReadAllBytes(fp));
         var cubeRom = File.ReadAllBytes(newRom); var cubeToml = File.ReadAllLines(newToml);
         foreach (var version in versions.Split(';')) {
            var parts = version.Split('|');
            var name = "CUBE " + Path.GetFileName(parts[0]).Replace("prev_", "").Replace("_CUBE.gba", "");
            var rom = File.ReadAllBytes(parts[0]);
            var hacked = CubeUpdateService.Load(singletons, (byte[])rom.Clone(), File.ReadAllLines(parts[1]));
            var token = new ModelDelta();
            var address = hacked.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, "data.pokemon.moves.stats");
            var moves = new ModelTable(hacked, address, () => token);
            moves[10].SetValue("pp", 37);
            moves[11].SetStringValue("name", "MY OWN MOVE NAME");
            var toml = CubeUpdateService.Export(singletons, hacked);
            var result = CubeUpdateService.Update(singletons, (byte[])hacked.RawData.Clone(), toml, cubeRom, cubeToml, known);
            Console.WriteLine($"{name}: detected {result.Report.DetectedVersion}; {result.Report.ToString().Replace(Environment.NewLine, " | ")}");
            Assert.True(result.Report.NodesEdited <= 3, $"{name}: {result.Report.NodesEdited} edits were found, 2 were made: " + string.Join(", ", result.Report.EditsPerAnchor.Keys.Take(10)));
            Assert.Equal(name, result.Report.DetectedVersion);
            var dump = Environment.GetEnvironmentVariable("UPD_DUMP_DIR");
            if (!string.IsNullOrEmpty(dump)) { File.WriteAllBytes(Path.Combine(dump, name.Replace(' ', '_') + ".gba"), result.Rom); File.WriteAllLines(Path.Combine(dump, name.Replace(' ', '_') + ".toml"), result.Toml); }
            var updated = CubeUpdateService.Load(singletons, result.Rom, result.Toml);
            var table = new ModelTable(updated, updated.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, "data.pokemon.moves.stats"), () => new ModelDelta());
            Assert.Equal(37, table[10].GetValue("pp"));
            Assert.Equal("MY OWN MOVE NAME", table[11].GetStringValue("name"));
            var fresh = new ModelTable(CubeUpdateService.Load(singletons, (byte[])cubeRom.Clone(), cubeToml), updated.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, "data.pokemon.moves.stats"), () => new ModelDelta());
            Assert.Equal(fresh[12].GetValue("pp"), table[12].GetValue("pp"));
         }
      }
   }

   public class UpdateCubeRunTests {
      /// <summary>The whole command on a real old CUBE: the file dialog, the update, the new tab.</summary>
      [SkippableFact]
      public void Run_OnAnOldCube_ReplacesTheTabWithTheUpdatedRom() {
         var oldRom = Environment.GetEnvironmentVariable("UPD_OLD"); var oldToml = Environment.GetEnvironmentVariable("UPD_OLD_TOML");
         var vanilla = Environment.GetEnvironmentVariable("UPD_VANILLA"); var resources = Environment.GetEnvironmentVariable("UPD_RESOURCES");
         Skip.If(string.IsNullOrEmpty(oldRom) || string.IsNullOrEmpty(vanilla) || string.IsNullOrEmpty(resources), "set UPD_OLD, UPD_OLD_TOML, UPD_VANILLA, UPD_RESOURCES (folder with the shipped cube_* files)");
         var fs = new StubFileSystem { OpenFile = (_, _) => new LoadedFile("Emerald.gba", File.ReadAllBytes(vanilla)) };
         var editor = new HavenSoft.HexManiac.Core.ViewModels.EditorViewModel(fs, InstantDispatch.Instance);
         var model = CubeUpdateService.Load(editor.Singletons, File.ReadAllBytes(oldRom), File.ReadAllLines(oldToml));
         var tab = new HavenSoft.HexManiac.Core.ViewModels.ViewPort("Hack.gba", model, InstantDispatch.Instance, editor.Singletons);
         editor.Add(tab);
         var item = new HavenSoft.HexManiac.Core.ViewModels.QuickEditItems.UpdateCube(editor, fs, new HavenSoft.HexManiac.Core.ViewModels.QuickEditItems.ShippedCubeResources(resources));
         Assert.True(item.CanRun(tab));
         var result = item.Run(tab).Result;
         Assert.True(result.IsWarning, result.ErrorMessage);
         Assert.Contains("Updated Hack.gba", result.ErrorMessage);
         Assert.Equal(1, editor.Count);
      }
   }
}
