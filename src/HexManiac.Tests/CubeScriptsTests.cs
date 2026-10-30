using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// The CUBE scripts: the Goto entries Scripts > EXP Share / Scripts > Level Cap, finding the tables of a CUBE ROM whose .toml has no anchors for them,
   /// and naming the level cap flags. (The HMA scripts of resources/Scripts/CUBE are tested in CubeHmaScriptTests.)
   /// Synthetic models are small ROMs with the tables written by hand. The tests on a real CUBE run when HMA_INTRO_CUBE / HMA_INTRO_CUBE_TOML point at it
   /// (and HMA_INTRO_CUBE_OLD_TOML at a .toml without the anchors) and are skipped otherwise.
   /// </summary>
   public class CubeScriptsTests {
      private const int Config = 0x40, Caps = 0x80;      // anchored by hand (not next to each other)
      private const int FoundConfig = 0x100, FoundCaps = 0x110; // the layout of the real ROM: the caps right behind the config
      private readonly EditorViewModel editor = new EditorViewModel(new StubFileSystem(), InstantDispatch.Instance);

      private static readonly int[] DefaultLevels = { 15, 20, 25, 30, 38, 45, 52, 60, 75, 100 };

      internal static void WriteTables(byte[] data, int config, int[] levels = null, int padding = 0) {
         data[config + CubeScripts.ExpAllPercentIndex] = 20;
         data[config + CubeScripts.NoCharacterCustomizationIndex] = 1;
         data[config + CubeScripts.LevelCapIndex] = 1;
         var caps = config + CubeScripts.ConfigLength;
         for (int i = 0; i < 10; i++) {
            data[caps + i * 4] = (byte)i; data[caps + i * 4 + 1] = 9;
            data[caps + i * 4 + 2] = (byte)(levels ?? DefaultLevels)[i];
            data[caps + i * 4 + 3] = (byte)padding;
         }
      }

      private ViewPort OpenRom(bool withAnchors) {
         var data = new byte[0x200];
         data[Config + CubeScripts.ExpAllPercentIndex] = 20;
         data[Config + CubeScripts.NoCharacterCustomizationIndex] = 1;
         data[Config + CubeScripts.LevelCapIndex] = 1;
         for (int i = 0; i < 10; i++) { data[Caps + i * 4] = (byte)i; data[Caps + i * 4 + 1] = 9; data[Caps + i * 4 + 2] = (byte)(15 + i * 5); }
         var model = new PokemonModel(data, singletons: BaseViewModelTestClass.Singletons);
         var viewPort = new ViewPort("cube.gba", model, InstantDispatch.Instance, BaseViewModelTestClass.Singletons) { Width = 0x10, Height = 0x10 };
         if (withAnchors) {
            viewPort.Goto.Execute(Config.ToString("X2"));
            viewPort.Edit($"^{CubeScripts.ConfigAnchor}[on.]16 ");
            viewPort.Goto.Execute(Caps.ToString("X2"));
            viewPort.Edit($"^{CubeScripts.LevelCapAnchor}[flag:|h level. padding.]10 ");
         }
         editor.Add(viewPort);
         return viewPort;
      }

      // ---- Goto ----------------------------------------------------------------------------------------------------

      [Fact]
      public void LevelCapGotoExistsOnlyWhileTheLevelCapIsOnAndLeadsToTheTable() {
         var viewPort = OpenRom(true);
         Assert.Equal(Caps, GotoAliases.FindAddress(viewPort.Model, "scripts.levelcap"));
         Assert.Contains("scripts.levelcap", GotoAliases.Available(viewPort.Model));

         viewPort.Model.RawData[Config + CubeScripts.LevelCapIndex] = 0; // the level cap is off (the default of CUBE)
         Assert.Equal(Pointer.NULL, GotoAliases.FindAddress(viewPort.Model, "scripts.levelcap"));
         Assert.DoesNotContain("scripts.levelcap", GotoAliases.Available(viewPort.Model));

         viewPort.Model.RawData[Config + CubeScripts.LevelCapIndex] = 1;
         Assert.Equal(Caps, GotoAliases.FindAddress(viewPort.Model, "scripts.levelcap"));
      }

      [Fact]
      public void ExpShareGotoExistsOnlyWhileExpAllIsOn() {
         var viewPort = OpenRom(true);
         Assert.Equal(Pointer.NULL, GotoAliases.FindAddress(viewPort.Model, "scripts.expshare"));
         Assert.DoesNotContain("scripts.expshare", GotoAliases.Available(viewPort.Model));

         viewPort.Model.RawData[Config] = 1;
         Assert.Equal(Config + 1, GotoAliases.FindAddress(viewPort.Model, "scripts.expshare"));
         Assert.Contains("scripts.expshare", GotoAliases.Matching(viewPort.Model, "exp share"));

         viewPort.Model.RawData[Config] = 0;
         Assert.Equal(Pointer.NULL, GotoAliases.FindAddress(viewPort.Model, "scripts.expshare"));
      }

      [Fact]
      public void ExpShareGotoFollowsUndo() {
         var viewPort = OpenRom(true);
         viewPort.Edit($"@{CubeScripts.ConfigAnchor}+0 1 ");
         Assert.NotEqual(Pointer.NULL, GotoAliases.FindAddress(viewPort.Model, "scripts.expshare"));
         viewPort.ChangeHistory.Undo.Execute();
         Assert.Equal(Pointer.NULL, GotoAliases.FindAddress(viewPort.Model, "scripts.expshare"));
      }

      [Fact]
      public void OpeningTheGotoPanelRebuildsTheOptions() {
         var viewPort = OpenRom(true);
         editor.GotoViewModel.Text = "expshare";
         var before = editor.GotoViewModel.PrefixSelections.SelectMany(s => s.Tokens).Select(t => t.Content).ToList();
         viewPort.Model.RawData[Config] = 1; // changed behind its back (e.g. by a script, or an edit in the hex view)
         editor.GotoViewModel.ControlVisible = false; // it starts visible
         editor.GotoViewModel.ControlVisible = true;
         var after = editor.GotoViewModel.PrefixSelections.SelectMany(s => s.Tokens).Select(t => t.Content).ToList();
         Assert.DoesNotContain(before, t => t.Contains("expshare"));
         Assert.Contains(after, t => t.Contains("expshare"));
      }

      [Fact]
      public void GotoAliasesStayUnavailableWithoutAnchors() {
         var viewPort = OpenRom(false);
         viewPort.Model.RawData[Config] = 1;
         Assert.Equal(Pointer.NULL, GotoAliases.FindAddress(viewPort.Model, "scripts.expshare"));
         Assert.Equal(Pointer.NULL, GotoAliases.FindAddress(viewPort.Model, "scripts.levelcap"));
      }

      [Fact]
      public void RomWithoutAnchorsHasNoScripts() {
         var viewPort = OpenRom(false);
         Assert.False(CubeScripts.IsAvailable(viewPort.Model));
         Assert.False(CubeScripts.IsOn(viewPort.Model, CubeScripts.NoCharacterCustomizationIndex)); // the byte is 1 there, but it is no CUBE config
      }

      [Fact]
      public void NoModelMeansUnavailable() {
         Assert.False(CubeScripts.IsAvailable(null));
         Assert.Equal(Pointer.NULL, CubeScripts.LevelCapAddress(null));
      }

      [Fact]
      public void ConfigThatDoesNotFitIsUnavailable() {
         var model = new PokemonModel(new byte[0x20], singletons: BaseViewModelTestClass.Singletons);
         var viewPort = new ViewPort("small.gba", model, InstantDispatch.Instance, BaseViewModelTestClass.Singletons);
         viewPort.Edit($"@18 ^{CubeScripts.ConfigAnchor}[on.]16 ");
         Assert.False(CubeScripts.IsAvailable(model));
      }

      [Fact]
      public void LevelCapTableReadsFlagAndLevel() {
         var viewPort = OpenRom(true);
         var run = viewPort.Model.GetNextRun(Caps) as ITableRun;
         Assert.NotNull(run);
         Assert.Equal(10, run.ElementCount);
         Assert.Equal(4, run.ElementLength);
         Assert.Equal(CubeScripts.LevelCapAddress(viewPort.Model), Caps);
      }

      // ---- finding the tables by their content ---------------------------------------------------------------------------

      [Fact]
      public void FindLevelCaps_FindsTheTableWithTheConfigInFront() {
         var data = new byte[0x400];
         WriteTables(data, FoundConfig);
         Assert.Equal(FoundCaps, CubeScripts.FindLevelCaps(data));
      }

      [Fact]
      public void FindLevelCaps_ToleratesAnyLevelsPaddingAndSwitches() {
         var data = new byte[0x400];
         WriteTables(data, FoundConfig, new[] { 0, 1, 7, 100, 101, 255, 3, 99, 50, 2 }, padding: 0x55);
         // every switch on, a strange percentage, even a switch that holds a 2
         for (int i = 0; i < 6; i++) data[FoundConfig + i] = 1;
         data[FoundConfig + 1] = 77;
         data[FoundConfig + 3] = 2;
         data[FoundConfig + 6] = 1;
         Assert.Equal(FoundCaps, CubeScripts.FindLevelCaps(data));
      }

      [Fact]
      public void FindLevelCaps_NeedsAllTenFlagsInOrder() {
         var data = new byte[0x400];
         WriteTables(data, FoundConfig);
         data[FoundCaps + 4 * 7] = 0x0F; // the eighth flag is not 0x907
         Assert.Equal(Pointer.NULL, CubeScripts.FindLevelCaps(data));

         data = new byte[0x400];
         WriteTables(data, FoundConfig);
         (data[FoundCaps], data[FoundCaps + 4]) = (data[FoundCaps + 4], data[FoundCaps]); // two flags swapped
         Assert.Equal(Pointer.NULL, CubeScripts.FindLevelCaps(data));
      }

      [Fact]
      public void FindLevelCaps_ReservedConfigBytesMustBeZeroOrOne() {
         var data = new byte[0x400];
         WriteTables(data, FoundConfig);
         data[FoundConfig + 9] = 0x42;
         Assert.Equal(Pointer.NULL, CubeScripts.FindLevelCaps(data));
      }

      [Fact]
      public void FindLevelCaps_TwoCandidatesIsNoAnswer() {
         var data = new byte[0x400];
         WriteTables(data, FoundConfig);
         WriteTables(data, 0x200);
         Assert.Equal(Pointer.NULL, CubeScripts.FindLevelCaps(data));
      }

      [Fact]
      public void FindLevelCaps_DoesNotFindItInNothing() {
         Assert.Equal(Pointer.NULL, CubeScripts.FindLevelCaps(new byte[0x400]));
         Assert.Equal(Pointer.NULL, CubeScripts.FindLevelCaps(new byte[8]));
         Assert.Equal(Pointer.NULL, CubeScripts.FindLevelCaps(new byte[0]));
         var junk = Enumerable.Range(0, 0x10000).Select(i => (byte)((i * 2654435761L) >> 13)).ToArray();
         Assert.Equal(Pointer.NULL, CubeScripts.FindLevelCaps(junk));
      }

      [Fact]
      public void FindLevelCaps_NeedsRoomForTheConfigInFront() {
         var data = new byte[0x400];
         WriteTables(data, 0);
         Assert.Equal(CubeScripts.ConfigLength, CubeScripts.FindLevelCaps(data)); // the config is the first 16 bytes
         var noRoom = new byte[0x28];
         Array.Copy(data, CubeScripts.ConfigLength, noRoom, 0, 0x28); // the caps at address 0
         Assert.Equal(Pointer.NULL, CubeScripts.FindLevelCaps(noRoom));
      }

      [Fact]
      public void FindNewGameSpawn_FindsTheMagic() {
         var data = new byte[0x400];
         Encoding.ASCII.GetBytes(CubeScripts.NewGameSpawnMagic).CopyTo(data, 0x180);
         Assert.Equal(0x180, CubeScripts.FindNewGameSpawn(data));
         Encoding.ASCII.GetBytes(CubeScripts.NewGameSpawnMagic).CopyTo(data, 0x300);
         Assert.Equal(Pointer.NULL, CubeScripts.FindNewGameSpawn(data)); // two: no answer
         Assert.Equal(Pointer.NULL, CubeScripts.FindNewGameSpawn(new byte[0x400]));
         var atTheEnd = new byte[0x400];
         Encoding.ASCII.GetBytes(CubeScripts.NewGameSpawnMagic).CopyTo(atTheEnd, 0x400 - 12); // the struct would not fit
         Assert.Equal(Pointer.NULL, CubeScripts.FindNewGameSpawn(atTheEnd));
      }

      // ---- repairing a model that was loaded without the anchors ---------------------------------------------------------

      private static string[] Toml(string flagAliasNames = null, string namedFlagNames = null, string extra = "") {
         var lines = new List<string>();
         lines.Add("[[List]]");
         lines.Add("Name = '''scriptflagaliases'''");
         lines.Add("2300 = [");
         foreach (var name in (flagAliasNames ?? Unused(0x8FC, 0x910)).Split(' ')) lines.Add($"   '''{name}''',");
         lines.Add("]");
         lines.Add("");
         lines.Add("[[List]]");
         lines.Add("Name = '''scriptnamedflags'''");
         lines.Add("2144 = [");
         foreach (var name in (namedFlagNames ?? "FLAG_SYS_POKEMON_GET FLAG_SYS_POKEDEX_GET").Split(' ')) lines.Add($"   '''{name}''',");
         lines.Add("]");
         lines.Add("");
         if (extra != "") lines.Add(extra);
         return lines.SelectMany(line => line.Split('\n')).ToArray();
      }

      private static string Unused(int from, int to) => string.Join(" ", Enumerable.Range(from, to - from).Select(i => $"FLAG_UNUSED_0x{i:X3}"));

      internal static HardcodeTablesModel Load(byte[] data, string[] toml) {
         var model = new HardcodeTablesModel(BaseViewModelTestClass.Singletons, data, toml == null ? null : new StoredMetadata(toml));
         model.InitializationWorkload.Wait();
         return model;
      }

      private static int AnchorAddress(IDataModel model, string name) => model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, name);

      private static string ListEntry(IDataModel model, string list, int index) {
         Assert.True(model.TryGetList(list, out var names));
         return index < names.Count ? names[index] : null;
      }

      [Fact]
      public void Repair_AddsTheAnchorsToARomWhoseTomlHasNone() {
         var data = new byte[0x400];
         WriteTables(data, FoundConfig);
         var model = Load(data, Toml());

         Assert.Equal(FoundConfig, AnchorAddress(model, CubeScripts.ConfigAnchor));
         Assert.Equal(FoundCaps, AnchorAddress(model, CubeScripts.LevelCapAnchor));
         Assert.True(CubeScripts.IsAvailable(model));
         var config = Assert.IsAssignableFrom<ITableRun>(model.GetNextRun(FoundConfig));
         Assert.Equal((16, 1, "on"), (config.ElementCount, config.ElementLength, config.ElementContent[0].Name));
         var caps = Assert.IsAssignableFrom<ITableRun>(model.GetNextRun(FoundCaps));
         Assert.Equal((10, 4), (caps.ElementCount, caps.ElementLength));
         Assert.Equal(new[] { "capAppliesWhileFlagSet", "level", "padding" }, caps.ElementContent.Select(e => e.Name));
         Assert.Equal(FoundCaps, GotoAliases.FindAddress(model, "scripts.levelcap"));
      }

      [Fact]
      public void Repair_AnchorsLookLikeTheOnesOfTheTomlFormat() {
         // the same anchors, once from the repair and once typed in with the formats of CUBE.toml
         var data = new byte[0x400];
         WriteTables(data, FoundConfig);
         Encoding.ASCII.GetBytes(CubeScripts.NewGameSpawnMagic).CopyTo(data, 0x300);
         var repaired = Load((byte[])data.Clone(), Toml());
         var typed = Load((byte[])data.Clone(), Toml(extra: $"[[NamedAnchors]]\nName = '''{CubeScripts.ConfigAnchor}'''\nAddress = 0x{FoundConfig:X}\nFormat = '''[on.]16'''\n\n" +
            $"[[NamedAnchors]]\nName = '''{CubeScripts.LevelCapAnchor}'''\nAddress = 0x{FoundCaps:X}\nFormat = '''[capAppliesWhileFlagSet:scriptflagaliases level. padding.]10'''\n\n" +
            $"[[NamedAnchors]]\nName = '''{CubeScripts.NewGameSpawnAnchor}'''\nAddress = 0x300\nFormat = '''[magic\"\"8 mapGroup. mapNum. x:|z y:|z shakeTruck. padding.]1'''\n"));
         foreach (var (name, address) in new[] { (CubeScripts.ConfigAnchor, FoundConfig), (CubeScripts.LevelCapAnchor, FoundCaps), (CubeScripts.NewGameSpawnAnchor, 0x300) }) {
            Assert.Equal(address, AnchorAddress(repaired, name));
            Assert.Equal(address, AnchorAddress(typed, name));
            Assert.Equal(typed.GetNextRun(address).FormatString, repaired.GetNextRun(address).FormatString);
            Assert.Equal(typed.GetNextRun(address).Length, repaired.GetNextRun(address).Length);
         }
         var spawn = Assert.IsAssignableFrom<ITableRun>(repaired.GetNextRun(0x300));
         Assert.Equal(16, spawn.ElementLength); // the struct of cube_scripts.h
         Assert.Equal(new[] { "magic", "mapGroup", "mapNum", "x", "y", "shakeTruck", "padding" }, spawn.ElementContent.Select(e => e.Name));
      }

      [Fact]
      public void Repair_WritesNothingToTheRom() {
         var data = new byte[0x400];
         WriteTables(data, FoundConfig);
         Encoding.ASCII.GetBytes(CubeScripts.NewGameSpawnMagic).CopyTo(data, 0x300);
         var before = (byte[])data.Clone();
         var model = Load(data, Toml());
         Assert.Equal(before, model.RawData);
      }

      [Fact]
      public void Repair_FindsThemWhateverTheUserChanged() {
         var data = new byte[0x400];
         WriteTables(data, FoundConfig, new[] { 1, 99, 50, 3, 100, 2, 77, 8, 60, 33 }, padding: 7);
         for (int i = 0; i < 6; i++) data[FoundConfig + i] = (byte)(i % 2);
         data[FoundConfig + 1] = 55;
         var model = Load(data, Toml());
         Assert.Equal(FoundConfig, AnchorAddress(model, CubeScripts.ConfigAnchor));
         Assert.Equal(FoundCaps, AnchorAddress(model, CubeScripts.LevelCapAnchor));
      }

      [Fact]
      public void Repair_LeavesExistingAnchorsAlone() {
         var data = new byte[0x400];
         WriteTables(data, FoundConfig);
         var anchors = $"[[NamedAnchors]]\nName = '''{CubeScripts.ConfigAnchor}'''\nAddress = 0x{FoundConfig:X}\nFormat = '''[on.]16'''\n\n" +
            $"[[NamedAnchors]]\nName = '''{CubeScripts.LevelCapAnchor}'''\nAddress = 0x{FoundCaps:X}\nFormat = '''[flag:|h level. padding.]10'''\n";
         var model = Load(data, Toml(extra: anchors));
         Assert.Equal(new[] { "flag", "level", "padding" }, ((ITableRun)model.GetNextRun(FoundCaps)).ElementContent.Select(e => e.Name)); // the toml's format wins
      }

      [Fact]
      public void Repair_AnAnchorSomewhereElseIsNotContradicted() {
         var data = new byte[0x400];
         WriteTables(data, FoundConfig);
         var anchors = $"[[NamedAnchors]]\nName = '''{CubeScripts.ConfigAnchor}'''\nAddress = 0x200\nFormat = '''[on.]16'''\n";
         var model = Load(data, Toml(extra: anchors));
         Assert.Equal(0x200, AnchorAddress(model, CubeScripts.ConfigAnchor));
         Assert.Equal(Pointer.NULL, AnchorAddress(model, CubeScripts.LevelCapAnchor)); // no half-and-half
      }

      [Fact]
      public void Repair_SomethingElseInTheWayMeansNoAnchor() {
         var data = new byte[0x400];
         WriteTables(data, FoundConfig);
         var anchors = "[[NamedAnchors]]\nName = '''somebody.elses.table'''\nAddress = 0x118\nFormat = '''[x.]4'''\n";
         var model = Load(data, Toml(extra: anchors));
         Assert.Equal(Pointer.NULL, AnchorAddress(model, CubeScripts.LevelCapAnchor));
         Assert.Equal(0x118, AnchorAddress(model, "somebody.elses.table"));
      }

      [Fact]
      public void Repair_RomWithoutTablesStaysAsItWas() {
         var model = Load(new byte[0x400], Toml());
         Assert.Equal(Pointer.NULL, AnchorAddress(model, CubeScripts.ConfigAnchor));
         Assert.Equal(Pointer.NULL, AnchorAddress(model, CubeScripts.LevelCapAnchor));
         Assert.Equal(Pointer.NULL, AnchorAddress(model, CubeScripts.NewGameSpawnAnchor));
         Assert.Equal("FLAG_UNUSED_0x900", ListEntry(model, "scriptflagaliases", 0x900)); // not a CUBE: the flags keep their names
         Assert.Null(ListEntry(model, "scriptnamedflags", 0x900));
      }

      [Fact]
      public void Repair_NamesTheCapFlagsInBothFlagLists() {
         var data = new byte[0x400];
         WriteTables(data, FoundConfig);
         var model = Load(data, Toml());
         for (int i = 0; i < 10; i++) {
            Assert.Equal($"FLAG_CUBE_LEVEL_CAP_{i + 1}", ListEntry(model, "scriptflagaliases", 0x900 + i));
            Assert.Equal($"FLAG_CUBE_LEVEL_CAP_{i + 1}", ListEntry(model, "scriptnamedflags", 0x900 + i)); // the list was shorter than that
         }
         Assert.Equal("FLAG_UNUSED_0x8FF", ListEntry(model, "scriptflagaliases", 0x8FF));
         Assert.Equal("FLAG_UNUSED_0x90A", ListEntry(model, "scriptflagaliases", 0x90A));
         Assert.Null(ListEntry(model, "scriptnamedflags", 0x8FF));
         Assert.Equal("FLAG_SYS_POKEDEX_GET", ListEntry(model, "scriptnamedflags", 2145)); // the names that were there stay
      }

      [Fact]
      public void Repair_AFlagTheUserNamedKeepsItsName() {
         var names = Unused(0x8FC, 0x910).Replace("FLAG_UNUSED_0x902", "FLAG_MY_OWN_THING");
         var data = new byte[0x400];
         WriteTables(data, FoundConfig);
         var model = Load(data, Toml(flagAliasNames: names));
         Assert.Equal("FLAG_MY_OWN_THING", ListEntry(model, "scriptflagaliases", 0x902));
         Assert.Equal("FLAG_CUBE_LEVEL_CAP_1", ListEntry(model, "scriptflagaliases", 0x900));
         Assert.Equal("FLAG_CUBE_LEVEL_CAP_4", ListEntry(model, "scriptflagaliases", 0x903));
      }

      [Fact]
      public void Repair_CapFlagNamesWorkInTheScriptEditor() {
         var data = new byte[0x400];
         WriteTables(data, FoundConfig);
         var model = Load(data, Toml());
         var viewPort = new ViewPort("cube.gba", model, InstantDispatch.Instance, BaseViewModelTestClass.Singletons);
         var parser = viewPort.Tools.CodeTool.ScriptParser;
         var script = "setflag FLAG_CUBE_LEVEL_CAP_3\nclearflag FLAG_CUBE_LEVEL_CAP_10\ncheckflag 0x900\nend";
         var bytes = parser.Compile(new ModelDelta(), model, 0x200, ref script, out var _);
         Assert.Equal(new byte[] { 0x29, 0x02, 0x09, 0x2A, 0x09, 0x09, 0x2B, 0x00, 0x09, 0x02 }, bytes.Take(10).ToArray());
         // typing CUBELEVEL suggests the cap flags
         var options = parser.ReadOptions(model, "scriptnamedflags|h", "CUBELEVEL");
         Assert.NotEmpty(options);
         Assert.All(options.Where(option => option != "..."), option => Assert.StartsWith("FLAG_CUBE_LEVEL_CAP_", option));
         Assert.True(options.Count(option => option != "...") >= 9);
         // and the flags are shown by name when a script is read back
         new ModelDelta().ChangeData(model, 0x200, bytes.Take(10).ToArray());
         var text = parser.Parse(model, 0x200, 10);
         Assert.Contains("setflag FLAG_CUBE_LEVEL_CAP_3", text);
         Assert.Contains("checkflag FLAG_CUBE_LEVEL_CAP_1", text);
      }

      [Fact]
      public void Repair_TheRepairedMetadataIsAnOrdinaryTomlOfOlderVersions() {
         var data = new byte[0x400];
         WriteTables(data, FoundConfig);
         Encoding.ASCII.GetBytes(CubeScripts.NewGameSpawnMagic).CopyTo(data, 0x300);
         var model = Load(data, Toml());
         var text = model.ExportMetadata(null, BaseViewModelTestClass.Singletons.MetadataInfo).Serialize();
         var joined = string.Join("\n", text);
         Assert.Contains($"Name = '''{CubeScripts.ConfigAnchor}'''", joined);
         Assert.Contains("Format = '''[on.]16'''", joined);
         Assert.Contains("Format = '''[capAppliesWhileFlagSet:scriptflagaliases level. padding.]10'''", joined);
         Assert.Contains("Format = '''[magic\"\"8 mapGroup. mapNum. x:|z y:|z shakeTruck. padding.]1'''", joined);
         Assert.Contains("'''FLAG_CUBE_LEVEL_CAP_1'''", joined);
         // and it loads again, as the metadata of any other ROM, to the very same anchors
         var again = Load((byte[])data.Clone(), text);
         Assert.Equal(FoundConfig, AnchorAddress(again, CubeScripts.ConfigAnchor));
         Assert.Equal(FoundCaps, AnchorAddress(again, CubeScripts.LevelCapAnchor));
         Assert.Equal(0x300, AnchorAddress(again, CubeScripts.NewGameSpawnAnchor));
      }

      [Fact]
      public void Repair_TheGotoEntriesAndTheTableWorkAfterwards() {
         var data = new byte[0x400];
         WriteTables(data, FoundConfig);
         data[FoundConfig] = 1;
         var model = Load(data, Toml());
         Assert.Equal(FoundConfig + 1, GotoAliases.FindAddress(model, "scripts.expshare"));
         Assert.Equal(FoundCaps, GotoAliases.FindAddress(model, "scripts.levelcap"));
         // the table shows the names of the flags, the way the user wants to read them
         var caps = (ITableRun)model.GetNextRun(FoundCaps);
         Assert.Equal("FLAG_CUBE_LEVEL_CAP_1", caps.ElementContent[0].ToText(model, FoundCaps));
      }

      // ---- real ROMs ------------------------------------------------------------------------------------------------

      private static (byte[] rom, string[] toml)? RealCube(string tomlVariable = "HMA_INTRO_CUBE_TOML") {
         var path = Environment.GetEnvironmentVariable("HMA_INTRO_CUBE");
         var toml = Environment.GetEnvironmentVariable(tomlVariable);
         if (string.IsNullOrEmpty(path) || !File.Exists(path) || string.IsNullOrEmpty(toml) || !File.Exists(toml)) return null;
         return (File.ReadAllBytes(path), File.ReadAllLines(toml));
      }

      [SkippableFact]
      public void RealCube_WithItsOwnToml_HasTheTables() {
         var cube = RealCube();
         Skip.If(cube == null, "set HMA_INTRO_CUBE and HMA_INTRO_CUBE_TOML to run this test");
         var model = Load(cube.Value.rom, cube.Value.toml);
         Assert.True(CubeScripts.IsAvailable(model));
         Assert.Equal(CubeScripts.FindLevelCaps(model.RawData), CubeScripts.LevelCapAddress(model));
         Assert.Equal(CubeScripts.LevelCapAddress(model) - 16, CubeScripts.ConfigAddress(model));
         Assert.Equal("FLAG_CUBE_LEVEL_CAP_1", ListEntry(model, "scriptflagaliases", 0x900));
         Assert.Equal("FLAG_CUBE_LEVEL_CAP_10", ListEntry(model, "scriptnamedflags", 0x909));
         Assert.Equal(Pointer.NULL, GotoAliases.FindAddress(model, "scripts.levelcap")); // level caps are off by default, so no goto entry
         Console.WriteLine($"real CUBE: config {CubeScripts.ConfigAddress(model):X6}, caps {CubeScripts.LevelCapAddress(model):X6}, spawn {AnchorAddress(model, CubeScripts.NewGameSpawnAnchor):X6}");
      }

      [SkippableFact]
      public void RealCube_WithATomlWithoutTheAnchors_GetsThemFromTheRom() {
         var cube = RealCube("HMA_INTRO_CUBE_OLD_TOML");
         Skip.If(cube == null, "set HMA_INTRO_CUBE and HMA_INTRO_CUBE_OLD_TOML (a toml without cube.scripts.config) to run this test");
         Assert.DoesNotContain(cube.Value.toml, line => line.Contains(CubeScripts.ConfigAnchor));
         var before = (byte[])cube.Value.rom.Clone();
         var model = Load(cube.Value.rom, cube.Value.toml);
         Assert.Equal(before, model.RawData);
         Assert.Equal(CubeScripts.FindLevelCaps(cube.Value.rom) - 16, CubeScripts.ConfigAddress(model));
         Assert.Equal(CubeScripts.FindLevelCaps(cube.Value.rom), CubeScripts.LevelCapAddress(model));
         Assert.Equal(cube.Value.rom[CubeScripts.ConfigAddress(model) + CubeScripts.NoCharacterCustomizationIndex], model[CubeScripts.ConfigAddress(model) + CubeScripts.NoCharacterCustomizationIndex]); // whatever the ROM has (1 before round 14c, 0 after)
         var capsAt = CubeScripts.LevelCapAddress(model);
         Assert.Equal(15, model[capsAt + 2]);
         Assert.Equal(100, model[capsAt + 2 + 9 * 4]);
         var caps = (ITableRun)model.GetNextRun(capsAt);
         Assert.Equal("FLAG_CUBE_LEVEL_CAP_1", caps.ElementContent[0].ToText(model, capsAt));
         Assert.Equal("FLAG_CUBE_LEVEL_CAP_10", ListEntry(model, "scriptnamedflags", 0x909));
         Assert.Equal("FLAG_CUBE_LEVEL_CAP_2", ListEntry(model, "scriptflagaliases", 0x901));
         Assert.NotEqual(Pointer.NULL, GotoAliases.FindAddress(model, "scripts.levelcap"));
         Assert.NotEqual(Pointer.NULL, AnchorAddress(model, "data.pokemon.stats")); // the rest of the old toml is still there
      }

      [SkippableFact]
      public void RealCube_ChangedByTheUser_StillFound() {
         var cube = RealCube("HMA_INTRO_CUBE_OLD_TOML");
         Skip.If(cube == null, "set HMA_INTRO_CUBE and HMA_INTRO_CUBE_OLD_TOML to run this test");
         var rom = (byte[])cube.Value.rom.Clone();
         var random = new Random(14);
         var caps = CubeScripts.FindLevelCaps(cube.Value.rom); // wherever the build put the tables
         Assert.True(caps > 16);
         var config = caps - 16;
         for (int i = 0; i < 10; i++) { rom[caps + i * 4 + 2] = (byte)random.Next(0, 256); rom[caps + i * 4 + 3] = (byte)random.Next(0, 256); }
         for (int i = 0; i < 6; i++) rom[config + i] = (byte)random.Next(0, 2);
         rom[config + 1] = (byte)random.Next(0, 256);
         var model = Load(rom, cube.Value.toml);
         Assert.Equal(CubeScripts.FindLevelCaps(cube.Value.rom) - 16, CubeScripts.ConfigAddress(model));
         Assert.Equal(CubeScripts.FindLevelCaps(cube.Value.rom), CubeScripts.LevelCapAddress(model));
      }

      [SkippableFact]
      public void RealCube_WithNoTomlAtAll_GetsThemToo() {
         var cube = RealCube();
         Skip.If(cube == null, "set HMA_INTRO_CUBE and HMA_INTRO_CUBE_TOML to run this test");
         var model = Load((byte[])cube.Value.rom.Clone(), null);
         Assert.Equal(CubeScripts.FindLevelCaps(cube.Value.rom) - 16, CubeScripts.ConfigAddress(model));
         Assert.Equal(CubeScripts.FindLevelCaps(cube.Value.rom), CubeScripts.LevelCapAddress(model));
      }

      [SkippableTheory]
      [InlineData("HMA_INTRO_EMERALD", null)]
      [InlineData("HMA_INTRO_FIRERED", null)]
      [InlineData("HMA_INTRO_PREV_CUBE", "HMA_INTRO_PREV_CUBE_TOML")]
      public void RealRoms_WithoutTheTables_GetNoAnchorsAndNoNames(string romVariable, string tomlVariable) {
         var path = Environment.GetEnvironmentVariable(romVariable);
         Skip.If(string.IsNullOrEmpty(path) || !File.Exists(path), $"set {romVariable} to run this test");
         string[] toml = null;
         if (tomlVariable != null) {
            var tomlPath = Environment.GetEnvironmentVariable(tomlVariable);
            Skip.If(string.IsNullOrEmpty(tomlPath) || !File.Exists(tomlPath), $"set {tomlVariable} to run this test");
            toml = File.ReadAllLines(tomlPath);
         }
         var model = Load(File.ReadAllBytes(path), toml);
         Assert.Equal(Pointer.NULL, AnchorAddress(model, CubeScripts.ConfigAnchor));
         Assert.Equal(Pointer.NULL, AnchorAddress(model, CubeScripts.LevelCapAnchor));
         Assert.Equal(Pointer.NULL, AnchorAddress(model, CubeScripts.NewGameSpawnAnchor));
         Assert.False(CubeScripts.IsAvailable(model));
         foreach (var list in CubeScripts.FlagListNames) {
            if (!model.TryGetList(list, out var names)) continue;
            Assert.DoesNotContain(names, name => name != null && name.Contains("CUBE_LEVEL_CAP"));
         }
      }
   }
}
