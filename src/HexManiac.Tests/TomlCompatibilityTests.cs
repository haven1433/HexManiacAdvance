using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// A TOML written by any older CUBHMA (0.6.1-perf, 0.7, 0.7.1, ...) or an older HMA must keep working in the current build:
   /// no moves turning into numbers, no trainer panel losing its fields, no named anchors / table groups disappearing.
   ///
   /// The tests without a ROM always run. The real-ROM tests run when these are set (skipped otherwise):
   /// - HMA_INTRO_CUBE / HMA_INTRO_CUBE_TOML: the CUBE ROM and its current TOML,
   /// - HMA_COMPAT_TOMLS: a folder with older CUBE TOMLs in 'cube/' and other games' TOMLs in 'foreign/',
   /// - HMA_COMPAT_FOREIGN_ROM: the ROM for the foreign TOMLs (an Emerald-based hack), HMA_INTRO_EMERALD: vanilla Emerald (for foreign TOMLs with 'Emerald' in the name),
   /// - HMA_COMPAT_REPORT (optional): a file that receives one summary line per TOML.
   /// </summary>
   public class TomlCompatibilityTests {
      private static Singletons Singletons => BaseViewModelTestClass.Singletons;
      private static string Env(string name) => Environment.GetEnvironmentVariable(name);
      private static readonly string[] CoreTables = { "data.pokemon.stats", "data.pokemon.moves.stats", "data.items.stats", HardcodeTablesModel.TrainerTableName };
      private static readonly string[] VanillaCoreTables = { "data.pokemon.stats", "data.pokemon.moves.stats.battle", "data.items.stats", HardcodeTablesModel.TrainerTableName, "data.pokemon.names", "data.pokemon.moves.names" }; // (CUBE has the names inside the stats tables, and one moves table)

      #region helpers

      private static string[] WithVersion(string[] lines, string version) {
         var result = new List<string>();
         var replaced = false;
         foreach (var line in lines) {
            if (line.StartsWith("ApplicationVersion = ")) {
               if (version != null) result.Add($"ApplicationVersion = '''{version}'''");
               replaced = true;
            } else {
               result.Add(line);
            }
         }
         if (!replaced && version != null) result.Insert(1, $"ApplicationVersion = '''{version}'''");
         return result.ToArray();
      }

      private static HardcodeTablesModel Load(byte[] rom, string[] toml) {
         var model = new HardcodeTablesModel(Singletons, rom, new StoredMetadata(toml));
         model.InitializationWorkload.Wait();
         return model;
      }

      private static void Report(string line) {
         var path = Env("HMA_COMPAT_REPORT");
         if (string.IsNullOrEmpty(path)) return;
         lock (typeof(TomlCompatibilityTests)) File.AppendAllLines(path, new[] { line });
      }

      private static IEnumerable<string> TomlsIn(string subfolder) {
         var root = Env("HMA_COMPAT_TOMLS");
         if (string.IsNullOrEmpty(root) || !Directory.Exists(Path.Combine(root, subfolder))) return Enumerable.Empty<string>();
         return Directory.GetFiles(Path.Combine(root, subfolder), "*.toml").OrderBy(name => name);
      }

      private static int Address(IDataModel model, string anchor) => model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, anchor);

      /// <summary>Every named anchor of the TOML is either loaded, or reported (Logs tab) as missing. Returns how many are missing.</summary>
      private static int CheckAnchorsSurvive(string label, StoredMetadata stored, HardcodeTablesModel model, List<string> failures) {
         var missing = 0;
         foreach (var anchor in stored.NamedAnchors) {
            if (Address(model, anchor.Name) != Pointer.NULL) continue;
            missing++;
            if (!model.MissingStoredAnchors.Contains(anchor)) failures.Add($"{label}: anchor {anchor.Name} vanished without a log message");
            else if (!model.LoadingMessages.Any(message => message.Contains($"'{anchor.Name}'"))) failures.Add($"{label}: anchor {anchor.Name} is missing but not in the Logs messages");
         }
         return missing;
      }

      private static IReadOnlyList<TableGroup> Groups(IDataModel model) => model.ExportMetadata(Singletons.MetadataInfo).TableGroups;

      private static bool SameGroup(TableGroup a, TableGroup b) => a.GroupName == b.GroupName && a.Tables.SequenceEqual(b.Tables);

      /// <summary>All the checks for a CUBE TOML loaded on the CUBE ROM. Returns the number of anchors that could not be loaded (all of them logged).</summary>
      private static int CheckCube(string label, byte[] rom, string[] lines, List<string> failures, bool checkTrainerTeams = true, bool checkTableTool = false) {
         var stored = new StoredMetadata(lines);
         var model = Load(rom, lines);
         var current = Singletons.MetadataInfo.VersionNumber;

         if (model.RanMetadataUpgrade) failures.Add($"{label}: the vanilla metadata upgrade ran on a CUBE TOML");
         var missing = CheckAnchorsSurvive(label, stored, model, failures);

         // the formats are the TOML's (never replaced by the vanilla ones)
         var formatChanged = 0;
         foreach (var anchor in stored.NamedAnchors) {
            var address = Address(model, anchor.Name);
            if (address == Pointer.NULL || address != anchor.Address) continue;
            var run = model.GetNextRun(address);
            if (run.Start != address) { failures.Add($"{label}: {anchor.Name} has no run at its address"); continue; }
            if (run.FormatString != anchor.Format) formatChanged++;
            if (run is NoInfoRun && !string.IsNullOrEmpty(anchor.Format) && !model.LoadingMessages.Any(message => message.Contains($"'{anchor.Name}'"))) failures.Add($"{label}: {anchor.Name} lost its format without a log message ({anchor.Format})");
         }

         // every table group of the TOML is there, unchanged
         var loadedGroups = Groups(model);
         foreach (var group in stored.TableGroups) {
            if (!loadedGroups.Any(loaded => SameGroup(loaded, group))) failures.Add($"{label}: table group {group.GroupName} was lost or changed");
         }
         if (loadedGroups.Count != stored.TableGroups.Count) failures.Add($"{label}: {stored.TableGroups.Count} groups in the TOML, {loadedGroups.Count} after loading");

         // the pokemon / moves / items / trainer tables are there
         // (a TOML saved for another build of the ROM can have anchors that don't match this ROM's data: those are logged as missing (checked above), and the data checks skip them)
         bool NotLoadedForThisRom(string name) => stored.NamedAnchors.Any(anchor => anchor.Name == name) && !stored.NamedAnchors.Any(anchor => anchor.Name == name && Address(model, name) == anchor.Address);
         foreach (var table in CoreTables) {
            if (NotLoadedForThisRom(table)) continue;
            if (model.GetNextRun(Address(model, table)) is not ITableRun) failures.Add($"{label}: table {table} is missing");
         }
         var trainersLoaded = !NotLoadedForThisRom(HardcodeTablesModel.TrainerTableName);

         // the trainer panel shows all its field groups
         var trainerGroups = stored.TableGroups.Where(group => group.Tables.Any(table => table.Split('|')[0] == HardcodeTablesModel.TrainerTableName)).Select(group => group.GroupName).ToList();
         var loadedTrainerGroups = model.GetTableGroups(HardcodeTablesModel.TrainerTableName)?.Select(group => group.GroupName).ToList() ?? new List<string>();
         foreach (var name in trainerGroups.Where(_ => trainersLoaded)) {
            if (!loadedTrainerGroups.Contains(name)) failures.Add($"{label}: the trainer table lost its '{name}' group");
         }

         // trainer teams show move names (not numbers)
         var numericMoves = 0;
         if (checkTrainerTeams && trainersLoaded && model.GetTableModel(HardcodeTablesModel.TrainerTableName) is { } trainers) {
            for (int i = 1; i < Math.Min(trainers.Count, 120); i++) {
               if (model.GetNextRun(trainers[i].GetAddress("pokemon")) is not ExpansionTrainerTeamRun team) continue;
               var text = team.SerializeRun();
               numericMoves += text.Split('\n').Count(line => Regex.IsMatch(line.Trim(), @"^- \d+$"));
               if (Regex.IsMatch(text, @"^\d+ \d+", RegexOptions.Multiline)) failures.Add($"{label}: trainer {i} shows a species number: {text.Split('\n')[0]}");
            }
            if (numericMoves > 0) failures.Add($"{label}: {numericMoves} trainer moves show as numbers");
         } else if (checkTrainerTeams && trainersLoaded) {
            failures.Add($"{label}: no trainer table");
         }

         // the table tool (trainer panel) shows the TOML's groups
         if (checkTableTool) {
            var viewPort = new ViewPort("CUBE.gba", model, InstantDispatch.Instance, Singletons);
            viewPort.Goto.Execute(model.GetTableModel(HardcodeTablesModel.TrainerTableName)[117].Start);
            var shown = viewPort.Tools.TableTool.Groups.Select(group => group.GroupName).ToList();
            foreach (var name in trainerGroups) {
               if (!shown.Contains(name)) failures.Add($"{label}: the table tool doesn't show the '{name}' group (shows {string.Join(", ", shown)})");
            }
         }

         // saving writes the current version and the layout marker, and loads back the same
         var exported = model.ExportMetadata(Singletons.MetadataInfo);
         if (exported.Version != current) failures.Add($"{label}: saved with version {exported.Version} instead of {current}");
         var serialized = exported.Serialize();
         if (!serialized.Contains("MetadataLayout = '''expansion'''")) failures.Add($"{label}: the saved TOML has no expansion layout marker");
         var reread = new StoredMetadata(serialized);
         if (!reread.DescribesExpansionLayout) failures.Add($"{label}: the saved TOML doesn't describe an expansion layout");
         foreach (var group in stored.TableGroups) {
            if (!reread.TableGroups.Any(saved => SameGroup(saved, group))) failures.Add($"{label}: the saved TOML lost table group {group.GroupName}");
         }

         Report($"{label}: version={stored.Version ?? "(none)"} anchors={stored.NamedAnchors.Count} missing={missing} formatsNormalized={formatChanged} groups={stored.TableGroups.Count}/{loadedGroups.Count} upgrade={model.RanMetadataUpgrade} log={model.LoadingMessages.Count}");
         return missing;
      }

      #endregion

      #region no ROM needed

      [Fact]
      public void LayoutMarker_IsReadWrittenAndRoundTrips() {
         var metadata = new StoredMetadata(new[] { "[General]", "ApplicationVersion = '''0.7.2'''", "MetadataLayout = '''expansion'''" });
         Assert.Equal(StoredMetadata.ExpansionLayout, metadata.MetadataLayout);
         Assert.True(metadata.DescribesExpansionLayout);
         var lines = metadata.Serialize();
         Assert.Contains("MetadataLayout = '''expansion'''", lines);
         Assert.Equal(StoredMetadata.ExpansionLayout, new StoredMetadata(lines).MetadataLayout);
      }

      [Fact]
      public void LayoutMarker_UnknownValue_IsKept_AndTomlWithoutIt_HasNone() {
         var metadata = new StoredMetadata(new[] { "[General]", "MetadataLayout = '''something-new'''" });
         Assert.Equal("something-new", metadata.MetadataLayout);
         Assert.False(metadata.DescribesExpansionLayout);
         Assert.Contains("MetadataLayout = '''something-new'''", metadata.Serialize());

         var old = new StoredMetadata(new[] { "[General]", "ApplicationVersion = '''0.7.1'''" });
         Assert.Null(old.MetadataLayout);
         Assert.DoesNotContain(old.Serialize(), line => line.StartsWith("MetadataLayout"));
      }

      [Fact]
      public void LayoutMarker_DoesNotDisturbTheOtherGeneralSettings() {
         // an older reader skips the line: it is not a known key, and it isn't a numbered list entry (the "N = [...]" form)
         var metadata = new StoredMetadata(new[] { "[General]", "ApplicationVersion = '''0.7.2'''", "FreeSpaceSearch = '''1BD9000'''", "MetadataLayout = '''expansion'''", "FreeSpaceBuffer = '''040'''", "NextExportID = '''3'''" });
         Assert.Equal("0.7.2", metadata.Version);
         Assert.Equal(0x1BD9000, metadata.FreeSpaceSearch);
         Assert.Equal(0x40, metadata.FreeSpaceBuffer);
         Assert.Equal(3, metadata.NextExportID);
         Assert.True(metadata.IsEmpty);
      }

      private static readonly StoredMetadata VanillaDefaults = new(tableGroups: new[] { new TableGroup("Pokemon", new[] { "data.pokemon.stats|0" }), new TableGroup("Trainer", new[] { "data.trainers.stats|0" }) });

      [Fact]
      public void ReasonToKeepAsIs_VanillaGroups_AllowsTheUpgrade() {
         var metadata = new StoredMetadata(tableGroups: new[] { new TableGroup("Pokemon", new[] { "data.pokemon.stats|0", "data.pokemon.stats|1" }) });
         Assert.Null(metadata.ReasonToKeepAsIs(new[] { VanillaDefaults }));
      }

      [Fact]
      public void ReasonToKeepAsIs_UserGroupName_KeepsTheToml() {
         var metadata = new StoredMetadata(tableGroups: new[] { new TableGroup("Pokemon", new[] { "data.pokemon.stats|0" }), new TableGroup("Starting Status", new[] { "data.trainers.stats|4" }) });
         Assert.Contains("Starting Status", metadata.ReasonToKeepAsIs(new[] { VanillaDefaults }));
      }

      [Fact]
      public void ReasonToKeepAsIs_EditedGroup_KeepsTheToml() {
         var edited = new TableGroup("Trainer", new[] { "data.trainers.stats|name,class" }, "00000000"); // stored hash doesn't match the tables: edited by hand
         var metadata = new StoredMetadata(tableGroups: new[] { edited });
         Assert.NotNull(metadata.ReasonToKeepAsIs(new[] { VanillaDefaults }));
      }

      [Fact]
      public void ReasonToKeepAsIs_ExpansionAnchorOrMarker_KeepsTheToml() {
         var tpte = new StoredMetadata(anchors: new[] { new StoredAnchor(0x100, "data.trainers.stats", "[pokemon<`tpte`>]10", null) });
         Assert.NotNull(tpte.ReasonToKeepAsIs(new[] { VanillaDefaults }));
         var marked = new StoredMetadata(new[] { "[General]", "MetadataLayout = '''expansion'''" });
         Assert.NotNull(marked.ReasonToKeepAsIs(new[] { VanillaDefaults }));
      }

      [Fact]
      public void NeedVersionUpdate_HandlesMissingAndUnusualVersions() {
         Assert.True(StoredMetadata.NeedVersionUpdate(null, "0.7.2"));
         Assert.False(StoredMetadata.NeedVersionUpdate(null, null));
         Assert.False(StoredMetadata.NeedVersionUpdate("0.7.2", null));
         Assert.True(StoredMetadata.NeedVersionUpdate("0.6.1", "0.7.2"));
         Assert.True(StoredMetadata.NeedVersionUpdate("0.7", "0.7.2"));
         Assert.False(StoredMetadata.NeedVersionUpdate("0.7.2", "0.7.2"));
         Assert.False(StoredMetadata.NeedVersionUpdate("0.8", "0.7.2"));          // a newer TOML in an older build: never "upgraded" backwards
      }

      [Fact]
      public void MetadataBackupName_SitsNextToTheRom() {
         Assert.Equal("C:/roms/CUBE.backup-v0.7.1", EditorViewModel.MetadataBackupName("C:/roms/CUBE.gba", "0.7.1"));
         Assert.Equal("C:/roms/CUBE.backup-vold", EditorViewModel.MetadataBackupName("C:/roms/CUBE.GBA", null));
      }

      #endregion

      #region CUBE ROM

      private static (byte[] rom, string[] toml) Cube() {
         var path = Env("HMA_INTRO_CUBE");
         var toml = Env("HMA_INTRO_CUBE_TOML");
         Skip.If(string.IsNullOrEmpty(path) || !File.Exists(path) || string.IsNullOrEmpty(toml) || !File.Exists(toml), "set HMA_INTRO_CUBE and HMA_INTRO_CUBE_TOML to run this test");
         return (File.ReadAllBytes(path), File.ReadAllLines(toml));
      }

      [SkippableFact]
      public void CurrentCubeToml_LoadsCompletely_WithTheTableTool() {
         var (rom, toml) = Cube();
         var failures = new List<string>();
         var missing = CheckCube("current", rom, toml, failures, checkTableTool: true);
         Assert.True(missing == 0, $"{missing} anchors of the current TOML didn't load");
         Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
      }

      /// <summary>The current CUBE TOML, as if an older CUBHMA (or HMA) had saved it: every version must load it the same way.</summary>
      [SkippableFact]
      public void CurrentCubeToml_SavedByAnyOlderVersion_LoadsTheSame() {
         var (rom, toml) = Cube();
         var failures = new List<string>();
         foreach (var version in new[] { "0.1", "0.5", "0.5.6.1", "0.6.1", "0.7", "0.7.1", null }) {
            var missing = CheckCube("as " + (version ?? "no version"), rom, WithVersion(toml, version), failures, checkTrainerTeams: version == "0.1" || version == "0.7.1");
            if (missing > 0) failures.Add($"as {version}: {missing} anchors didn't load");
         }
         Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
      }

      /// <summary>Without its `tpte` marker, a CUBE TOML is still recognized by its own table groups: the vanilla upgrade doesn't touch it.</summary>
      [SkippableFact]
      public void CubeTomlWithoutExpansionMarker_IsStillKept_BecauseOfItsTableGroups() {
         var (rom, toml) = Cube();
         var lines = WithVersion(toml, "0.6.1").Select(line => line.Replace("`tpte`", "`tpt`")).ToArray();
         var stored = new StoredMetadata(lines);
         Assert.False(stored.DescribesExpansionLayout);
         var model = Load(rom, lines);
         Assert.False(model.RanMetadataUpgrade);
         Assert.Contains(model.LoadingMessages, message => message.Contains("user-defined table groups"));
         foreach (var group in stored.TableGroups) Assert.Contains(Groups(model), loaded => SameGroup(loaded, group));
         Assert.NotEqual(Pointer.NULL, Address(model, ExpansionTrainerTeamRun.MovesTable));
         Assert.NotEqual(Pointer.NULL, Address(model, ExpansionTrainerTeamRun.SpeciesTable));
         Assert.Equal(stored.NamedAnchors.Single(anchor => anchor.Name == ExpansionTrainerTeamRun.MovesTable).Format, model.GetNextRun(Address(model, ExpansionTrainerTeamRun.MovesTable)).FormatString);
      }

      /// <summary>An anchor that can't be placed is not dropped silently: it is in the Logs messages.</summary>
      [SkippableFact]
      public void TomlAnchorThatCannotLoad_IsLogged() {
         var (rom, toml) = Cube();
         var lines = toml.ToList();
         var insertAt = lines.FindIndex(line => line.StartsWith("[[NamedAnchors]]"));
         // a table of pointers to pokemon names at the very start of the ROM header: there are no pointers there, so it can't be placed
         lines.InsertRange(insertAt, new[] { "[[NamedAnchors]]", "Name = '''user.compat.broken'''", "Address = 0x000004", "Format = '''[a<\"\">]40'''", "" });
         var model = Load(rom, lines.ToArray());
         Assert.NotEqual(Pointer.NULL, Address(model, ExpansionTrainerTeamRun.MovesTable));
         if (Address(model, "user.compat.broken") == Pointer.NULL) {
            Assert.Contains(model.MissingStoredAnchors, anchor => anchor.Name == "user.compat.broken");
            Assert.Contains(model.LoadingMessages, message => message.Contains("'user.compat.broken'") && message.Contains("000004"));
         }
         Assert.All(model.MissingStoredAnchors, anchor => Assert.Contains(model.LoadingMessages, message => message.Contains($"'{anchor.Name}'")));
      }

      /// <summary>Every CUBE TOML we have from earlier CUBHMA builds (0.6.1-perf, round 7..12, ...) loads on the current ROM with its groups, formats and move names.</summary>
      [SkippableFact]
      public void HistoricalCubeTomls_KeepTheirGroupsAnchorsAndMoveNames() {
         var (rom, _) = Cube();
         var files = TomlsIn("cube").ToList();
         Skip.If(files.Count == 0, "set HMA_COMPAT_TOMLS to a folder with a 'cube' subfolder to run this test");
         var failures = new List<string>();
         foreach (var file in files) CheckCube(Path.GetFileName(file), rom, File.ReadAllLines(file), failures);
         Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures.Take(60)));
      }

      #endregion

      #region other games (vanilla layout: the upgrade may run, but only adds)

      private static byte[] RomFor(string tomlFile) {
         var name = Path.GetFileName(tomlFile);
         var path = name.Contains("Emerald") ? Env("HMA_INTRO_EMERALD") : Env("HMA_COMPAT_FOREIGN_ROM");
         if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
         return File.ReadAllBytes(path);
      }

      /// <summary>
      /// Other games' TOMLs (an Emerald hack, vanilla Emerald, saved by HMA 0.5.6.1): the upgrade runs, but everything the TOML defines survives,
      /// every table is at least as long as without the upgrade, and the user's table groups stay.
      /// </summary>
      [SkippableFact]
      public void ForeignTomls_UpgradeOnlyAdds() {
         var files = TomlsIn("foreign").ToList();
         Skip.If(files.Count == 0, "set HMA_COMPAT_TOMLS to a folder with a 'foreign' subfolder to run this test");
         var max = int.TryParse(Env("HMA_COMPAT_FOREIGN_MAX"), out var m) ? m : int.MaxValue;
         var failures = new List<string>();
         var ran = 0;
         foreach (var file in files) {
            if (ran >= max) break;
            var rom = RomFor(file);
            if (rom == null) continue;
            ran++;
            var label = Path.GetFileName(file);
            var lines = File.ReadAllLines(file);
            var stored = new StoredMetadata(lines);
            var upgraded = Load(rom, lines);
            var plain = Load(rom, WithVersion(lines, Singletons.MetadataInfo.VersionNumber)); // the same TOML, without the upgrade

            var missing = CheckAnchorsSurvive(label, stored, upgraded, failures);
            var plainMissing = stored.NamedAnchors.Count(anchor => Address(plain, anchor.Name) == Pointer.NULL);
            var renamed = 0;
            foreach (var anchor in stored.NamedAnchors) {
               var address = Address(plain, anchor.Name);
               if (address == Pointer.NULL) continue;
               if (Address(upgraded, anchor.Name) == Pointer.NULL) { renamed++; continue; } // checked above: renamed by the upgrade or logged
               if (plain.GetNextRun(address) is ITableRun before && upgraded.GetNextRun(Address(upgraded, anchor.Name)) is ITableRun after && after.ElementCount < before.ElementCount) {
                  failures.Add($"{label}: the upgrade made {anchor.Name} shorter ({before.ElementCount} -> {after.ElementCount})");
               }
            }
            var upgradedGroups = Groups(upgraded);
            foreach (var group in stored.TableGroups) {
               var kept = upgradedGroups.Any(loaded => SameGroup(loaded, group));
               var replacedBySuperset = upgradedGroups.Any(loaded => loaded.GroupName == group.GroupName && group.Tables.All(loaded.Tables.Contains));
               if (!kept && !replacedBySuperset) failures.Add($"{label}: table group {group.GroupName} was lost");
            }
            foreach (var list in stored.Lists) {
               if (upgraded.TryGetList(list.Name, out var loaded) && loaded.Count < list.Contents.Count) failures.Add($"{label}: list {list.Name} lost entries");
            }
            foreach (var table in VanillaCoreTables) {
               if (upgraded.GetNextRun(Address(upgraded, table)) is not ITableRun) failures.Add($"{label}: table {table} is missing after the upgrade");
            }
            if (upgraded.FreeSpaceBuffer != plain.FreeSpaceBuffer) failures.Add($"{label}: the upgrade changed FreeSpaceBuffer");
            var exported = upgraded.ExportMetadata(Singletons.MetadataInfo);
            if (exported.Version != Singletons.MetadataInfo.VersionNumber) failures.Add($"{label}: saved with version {exported.Version}");
            Report($"{label}: version={stored.Version} anchors={stored.NamedAnchors.Count} missing={missing} (without upgrade {plainMissing}) renamed={renamed} groups={stored.TableGroups.Count}/{upgradedGroups.Count} upgrade={upgraded.RanMetadataUpgrade} log={upgraded.LoadingMessages.Count}");
         }
         Skip.If(ran == 0, "set HMA_COMPAT_FOREIGN_ROM / HMA_INTRO_EMERALD to run this test");
         Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures.Take(60)));
      }

      /// <summary>
      /// A vanilla Emerald TOML from HMA 0.5.6.1 with the user's own edits: the upgrade runs (no expansion marker, only vanilla groups),
      /// and the user's own name for a vanilla table and the user's edited format of a reference table both survive it.
      /// </summary>
      [SkippableFact]
      public void VanillaEmeraldToml_WithUserEdits_UpgradeKeepsThem() {
         var file = TomlsIn("foreign").FirstOrDefault(name => Path.GetFileName(name).Contains("Emerald"));
         var romPath = Env("HMA_INTRO_EMERALD");
         Skip.If(file == null || string.IsNullOrEmpty(romPath) || !File.Exists(romPath), "set HMA_COMPAT_TOMLS (with a foreign Emerald TOML) and HMA_INTRO_EMERALD to run this test");
         var rom = File.ReadAllBytes(romPath);
         var lines = File.ReadAllLines(file).ToList();
         var original = new StoredMetadata(lines.ToArray());
         var gameCode = rom.GetGameCode();
         var defaults = BaseModel.GetDefaultMetadatas(gameCode.Substring(0, 4), gameCode).SelectMany(md => md.NamedAnchors).ToList();
         var referenceNames = Singletons.GameReferenceTables[gameCode].Select(reference => reference.Name).ToHashSet();

         // 1. the user gave a vanilla (default metadata) table their own name
         var vanillaTable = original.NamedAnchors.First(anchor => anchor.Format != null && anchor.Format.StartsWith("[") && !referenceNames.Contains(anchor.Name) &&
            defaults.Any(d => d.Name == anchor.Name && d.Address == anchor.Address));
         var nameLine = lines.IndexOf($"Name = '''{vanillaTable.Name}'''");
         lines[nameLine] = "Name = '''user.my.table'''";
         // 2. the user renamed a field of a reference table (its stored DefaultHash is the vanilla one, so the edit is visible)
         var stats = original.NamedAnchors.Single(anchor => anchor.Name == "data.pokemon.moves.stats.battle");
         var editedFormat = stats.Format.Replace("power.", "basePower.");
         Assert.NotEqual(stats.Format, editedFormat);
         var index = lines.IndexOf("Name = '''data.pokemon.moves.stats.battle'''");
         var formatLine = lines.FindIndex(index, line => line.StartsWith("Format = "));
         lines[formatLine] = $"Format = '''{editedFormat}'''";

         var toml = WithVersion(lines.ToArray(), "0.5.6.1");
         var stored = new StoredMetadata(toml);
         var model = Load(rom, toml);
         Assert.True(model.RanMetadataUpgrade);
         Assert.Equal(editedFormat, model.GetNextRun(Address(model, "data.pokemon.moves.stats.battle")).FormatString);
         Assert.Equal(vanillaTable.Address, Address(model, "user.my.table"));
         Assert.Equal(Pointer.NULL, Address(model, vanillaTable.Name));
         foreach (var group in original.TableGroups) {
            Assert.Contains(Groups(model), loaded => loaded.GroupName == group.GroupName && group.Tables.All(loaded.Tables.Contains));
         }
         var failures = new List<string>();
         CheckAnchorsSurvive("emerald", stored, model, failures);
         Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
      }

      #endregion
   }
}
