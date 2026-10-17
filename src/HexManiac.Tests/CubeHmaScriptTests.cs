using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// The HMA scripts in Scripts/CUBE (copied to resources/Scripts/CUBE): one ON/OFF pair for each CUBE option, and the level cap script with its editable lines.
   /// They are run the way the user runs them: the text of the file goes through ViewPort.TryImport.
   /// The python block that starts each script (the check 'is this a CUBE ROM') needs IronPython and the PythonTool of the editor, so these tests
   /// run the scripts without it. That block is tested for what it must look like, the rest of the script is tested for what it does.
   /// </summary>
   public class CubeHmaScriptTests {
      private const int Config = 0x100, Caps = 0x110;
      private static readonly string[] ScriptNames = {
         "CUBE_ExpAll_ON", "CUBE_ExpAll_OFF", "CUBE_EvolutionUseItems_ON", "CUBE_EvolutionUseItems_OFF", "CUBE_CharacterCustomization_Enable", "CUBE_CharacterCustomization_Disable",
         "CUBE_RandomizerMenu_ON", "CUBE_RandomizerMenu_OFF", "CUBE_LevelCap_ON", "CUBE_LevelCap_OFF", "CUBE_ReusableTMs_ON", "CUBE_ReusableTMs_OFF",
      };

      private static string ScriptText(string name) {
         var candidates = new List<string> { $"resources/Scripts/CUBE/{name}.hma", $"Scripts/CUBE/{name}.hma" };
         for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir != null; dir = dir.Parent) candidates.Add(Path.Combine(dir.FullName, "src", "HexManiac.WPF", "Scripts", "CUBE", name + ".hma"));
         var path = candidates.FirstOrDefault(File.Exists);
         Assert.True(path != null, $"{name}.hma was not found");
         return File.ReadAllText(path);
      }

      /// <summary>The script without its python blocks (the harness has no IronPython).</summary>
      private static string WithoutPython(string text) {
         var result = new StringBuilder();
         var inPython = false;
         foreach (var line in text.Replace("\r\n", "\n").Split('\n')) {
            if (!inPython && line.Trim() == ".python") inPython = true;
            else if (inPython && line.Trim() == ".end") inPython = false;
            else if (!inPython) result.Append(line).Append('\n');
         }
         return result.ToString();
      }

      private static IEnumerable<string> PythonBlocks(string text) {
         var block = new List<string>();
         var inPython = false;
         foreach (var line in text.Replace("\r\n", "\n").Split('\n')) {
            if (!inPython && line.Trim() == ".python") { inPython = true; block.Clear(); } else if (inPython && line.Trim() == ".end") { inPython = false; yield return string.Join("\n", block); } else if (inPython) block.Add(line);
         }
      }

      private static ViewPort OpenCube(byte[] data, string[] toml = null) {
         var model = CubeScriptsTests.Load(data, toml ?? new string[0]);
         return new ViewPort("cube.gba", model, InstantDispatch.Instance, BaseViewModelTestClass.Singletons) { Width = 0x10, Height = 0x10 };
      }

      private static byte[] SyntheticRom(int[] levels = null, int padding = 0) {
         var data = new byte[0x400];
         CubeScriptsTests.WriteTables(data, Config, levels, padding);
         return data;
      }

      private static string[] Toml(params string[] extra) {
         var names = string.Join("\n", Enumerable.Range(0x8FC, 0x14).Select(i => $"   '''FLAG_UNUSED_0x{i:X3}''',"));
         return ("[[List]]\nName = '''scriptflagaliases'''\n2300 = [\n" + names + "\n]\n\n" + string.Join("\n", extra)).Split('\n');
      }

      private static void Run(ViewPort viewPort, string text) {
         Assert.True(viewPort.TryImport(new LoadedFile("script.hma", Encoding.ASCII.GetBytes(WithoutPython(text))), null));
      }

      private static List<int> Differences(byte[] before, byte[] after) => Enumerable.Range(0, before.Length).Where(i => before[i] != after[i]).ToList();

      // ---- what the files look like ----------------------------------------------------------------------------------

      [Fact]
      public void EveryScriptExists_IsPlainAscii_AndExplainsItself() {
         foreach (var name in ScriptNames) {
            var text = ScriptText(name);
            Assert.True(text.All(c => c < 128), $"{name}: only ASCII, the importer reads the file with the system code page");
            Assert.StartsWith("# CUBE script: ", text);
            Assert.Contains("# What it does", text);
            Assert.Contains("# How to undo", text);
            Assert.Contains("# How to use", text);
         }
      }

      [Fact]
      public void EveryScript_StartsWithTheCheckBeforeItWritesAnything() {
         foreach (var name in ScriptNames) {
            var lines = ScriptText(name).Replace("\r\n", "\n").Split('\n');
            var firstContent = lines.First(line => !line.StartsWith("#") && line.Trim() != "");
            Assert.Equal(".python", firstContent.Trim());
            var firstWrite = Array.FindIndex(lines, line => line.StartsWith("@cube."));
            var checkEnd = Array.FindLastIndex(lines, firstWrite, line => line.Trim() == ".end");
            Assert.True(checkEnd >= 0 && checkEnd < firstWrite, $"{name}: the check comes before the first write");

            // the check has to stop the script when the table that the script writes to is missing
            var guards = PythonBlocks(ScriptText(name)).Take(2).ToList();
            var anchors = lines.Where(line => line.StartsWith("@cube.")).Select(line => line.Substring(1).Split('+', ' ')[0]).Distinct().ToList();
            foreach (var anchor in anchors) {
               var guard = guards.FirstOrDefault(g => g.Contains($"GetAddressFromAnchor(tab.CurrentChange, -1, '{anchor}') < 0"));
               Assert.True(guard != null, $"{name}: no check for {anchor}");
               Assert.Contains("tab.RaiseError(", guard);
               Assert.Contains("tab.AbortScript()", guard);
            }
         }
      }

      [Fact]
      public void EveryScript_OnlyTouchesTheCubeTables_AndNothingElseIsWritten() {
         foreach (var name in ScriptNames) {
            var script = WithoutPython(ScriptText(name));
            foreach (var line in script.Split('\n').Where(line => !line.StartsWith("#") && line.Trim() != "")) {
               Assert.StartsWith("@cube.scripts.", line); // no other command: no hex bytes at the current cursor, no thumb code
            }
         }
      }

      [Fact]
      public void TheScriptTextDoesNotTriggerTheImporterInComments() {
         // the importer looks for '.thumb' and '.python' at the start of a word, even inside a comment, and '@!game' anywhere
         foreach (var name in ScriptNames) {
            var comments = ScriptText(name).Replace("\r\n", "\n").Split('\n').Where(line => line.StartsWith("#")).ToList();
            Assert.DoesNotContain(comments, line => line.Contains(" .python") || line.Contains(" .thumb") || line.Contains("@!"));
            Assert.DoesNotContain(comments, line => line.Skip(1).Contains('#'));
         }
      }

      // ---- what they do ----------------------------------------------------------------------------------------------

      [Theory]
      [InlineData("CUBE_EvolutionUseItems", CubeScripts.EvoItemsIndex)]
      [InlineData("CUBE_RandomizerMenu", CubeScripts.RandomizerMenuIndex)]
      [InlineData("CUBE_ExpAll", CubeScripts.ExpAllIndex)]
      [InlineData("CUBE_LevelCap", CubeScripts.LevelCapIndex)]
      [InlineData("CUBE_ReusableTMs", CubeScripts.ReusableTmsIndex)]
      public void OnAndOff_ChangeOnlyTheirOwnByte_AndUndoPutsItBack(string name, int index) {
         var rom = SyntheticRom();
         var viewPort = OpenCube((byte[])rom.Clone(), Toml());
         Assert.True(CubeScripts.IsAvailable(viewPort.Model));
         var before = viewPort.Model.RawData.ToArray();

         Run(viewPort, ScriptText(name + "_OFF"));
         var afterOff = viewPort.Model.RawData.ToArray();
         Assert.Equal(0, afterOff[Config + index]);
         Assert.Empty(Differences(before, afterOff).Where(i => i != Config + index));

         Run(viewPort, ScriptText(name + "_ON"));
         var afterOn = viewPort.Model.RawData.ToArray();
         Assert.Equal(1, afterOn[Config + index]);
         // ON writes its byte (and for EXP All and the level cap the lines that the script has for the percentage and the caps), and nothing else
         var expected = new HashSet<int> { Config + index };
         if (name == "CUBE_ExpAll") expected.Add(Config + CubeScripts.ExpAllPercentIndex);
         Assert.Empty(Differences(afterOff, afterOn).Where(i => !expected.Contains(i)));

         viewPort.ChangeHistory.Undo.Execute(); // each script is its own undo step
         Assert.Empty(Differences(afterOff, viewPort.Model.RawData));
         viewPort.ChangeHistory.Undo.Execute();
         Assert.Empty(Differences(before, viewPort.Model.RawData));
      }

      [Fact]
      public void CharacterCustomization_EnableWritesZero_DisableWritesOne_AndNothingElseChanges() {
         const int index = CubeScripts.NoCharacterCustomizationIndex;
         var viewPort = OpenCube(SyntheticRom(), Toml());
         var start = viewPort.Model.RawData.ToArray();
         Run(viewPort, ScriptText("CUBE_CharacterCustomization_Disable"));
         var disabled = viewPort.Model.RawData.ToArray();
         Assert.Equal(1, disabled[Config + index]);
         Assert.Empty(Differences(start, disabled).Where(i => i != Config + index));
         Run(viewPort, ScriptText("CUBE_CharacterCustomization_Enable"));
         var enabled = viewPort.Model.RawData.ToArray();
         Assert.Equal(0, enabled[Config + index]);
         Assert.Empty(Differences(disabled, enabled).Where(i => i != Config + index));
         Run(viewPort, ScriptText("CUBE_CharacterCustomization_Enable")); // twice is the same as once
         Assert.Empty(Differences(enabled, viewPort.Model.RawData));
         viewPort.ChangeHistory.Undo.Execute();
         viewPort.ChangeHistory.Undo.Execute();
         Assert.Equal(1, viewPort.Model.RawData[Config + index]);
      }

      [Fact]
      public void TheOldCharacterCustomizationScriptsAreGone_AndEveryHeaderSaysWhatOnAndOffMean() {
         var gone = false;
         try { ScriptText("CUBE_DisableCharacterCustomization_ON"); } catch (Exception) { gone = true; }
         Assert.True(gone);
         foreach (var name in ScriptNames) {
            var text = ScriptText(name);
            if (name.EndsWith("_ON") || name.EndsWith("_OFF")) Assert.Contains("# ON means", text);
            if (name.StartsWith("CUBE_CharacterCustomization")) {
               Assert.Contains("Ctrl+Z", text);
               Assert.Contains(name.EndsWith("Enable") ? "CUBE_CharacterCustomization_Disable.hma" : "CUBE_CharacterCustomization_Enable.hma", text);
            }
         }
      }

      [Fact]
      public void RunningAScriptTwice_IsTheSameAsOnce() {
         var viewPort = OpenCube(SyntheticRom(), Toml());
         Run(viewPort, ScriptText("CUBE_EvolutionUseItems_ON"));
         var once = viewPort.Model.RawData.ToArray();
         Run(viewPort, ScriptText("CUBE_EvolutionUseItems_ON"));
         Assert.Empty(Differences(once, viewPort.Model.RawData));
      }

      [Fact]
      public void ExpAllOn_WritesThePercentageOfTheScript_Default20() {
         var rom = SyntheticRom();
         rom[Config + CubeScripts.ExpAllPercentIndex] = 0;
         var viewPort = OpenCube(rom, Toml());
         Run(viewPort, ScriptText("CUBE_ExpAll_ON"));
         Assert.Equal(1, viewPort.Model[Config + CubeScripts.ExpAllIndex]);
         Assert.Equal(20, viewPort.Model[Config + CubeScripts.ExpAllPercentIndex]);
      }

      [Theory]
      [InlineData(0)]
      [InlineData(5)]
      [InlineData(33)]
      [InlineData(100)]
      public void ExpAllOn_WritesAPercentageThatTheUserTyped(int percent) {
         var viewPort = OpenCube(SyntheticRom(), Toml());
         var text = ScriptText("CUBE_ExpAll_ON");
         Assert.Contains("@cube.scripts.config+1 20", text);
         Run(viewPort, text.Replace("@cube.scripts.config+1 20", $"@cube.scripts.config+1 {percent}"));
         Assert.Equal(percent, viewPort.Model[Config + CubeScripts.ExpAllPercentIndex]);
         Assert.Equal(1, viewPort.Model[Config + CubeScripts.ExpAllIndex]);
      }

      [Fact]
      public void ExpAllOff_KeepsThePercentage() {
         var rom = SyntheticRom();
         rom[Config + CubeScripts.ExpAllIndex] = 1;
         rom[Config + CubeScripts.ExpAllPercentIndex] = 45;
         var viewPort = OpenCube(rom, Toml());
         Run(viewPort, ScriptText("CUBE_ExpAll_OFF"));
         Assert.Equal(0, viewPort.Model[Config + CubeScripts.ExpAllIndex]);
         Assert.Equal(45, viewPort.Model[Config + CubeScripts.ExpAllPercentIndex]);
      }

      [Fact]
      public void LevelCapOn_WithTheDefaultLines_ChangesNothingInARomWithTheDefaults() {
         var rom = SyntheticRom(padding: 0x21);
         var viewPort = OpenCube((byte[])rom.Clone(), Toml());
         Run(viewPort, ScriptText("CUBE_LevelCap_ON"));
         Assert.Empty(Differences(rom, viewPort.Model.RawData));
      }

      [Fact]
      public void LevelCapOn_WritesTheLevelsOfTheScript_AndKeepsThePadding() {
         var rom = SyntheticRom(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, padding: 0x21);
         rom[Config + CubeScripts.LevelCapIndex] = 0;
         var viewPort = OpenCube((byte[])rom.Clone(), Toml());
         Run(viewPort, ScriptText("CUBE_LevelCap_ON"));
         var data = viewPort.Model.RawData;
         var defaults = new[] { 15, 20, 25, 30, 38, 45, 52, 60, 75, 100 };
         for (int i = 0; i < 10; i++) {
            Assert.Equal(i, data[Caps + i * 4]);
            Assert.Equal(9, data[Caps + i * 4 + 1]);
            Assert.Equal(defaults[i], data[Caps + i * 4 + 2]);
            Assert.Equal(0x21, data[Caps + i * 4 + 3]);
         }
         Assert.Equal(1, data[Config + CubeScripts.LevelCapIndex]);
         Assert.Equal(20, data[Config + CubeScripts.ExpAllPercentIndex]); // the rest of the config is not touched
      }

      /// <summary>Changes the flag and/or the level of cap 'number' (1-10) in the text of CUBE_LevelCap_ON.hma.</summary>
      private static string EditCap(string text, int number, string flag, int? level) {
         var offset = (number - 1) * 4;
         var flagLine = $"@cube.scripts.levelcaps+{offset:X} FLAG_CUBE_LEVEL_CAP_{number}";
         var levelLine = $"@cube.scripts.levelcaps+{offset + 2:X} {DefaultLevels[number - 1]}";
         Assert.Contains(flagLine, text);
         Assert.Contains(levelLine, text);
         if (flag != null) text = text.Replace(flagLine, $"@cube.scripts.levelcaps+{offset:X} {flag}");
         if (level != null) text = text.Replace(levelLine, $"@cube.scripts.levelcaps+{offset + 2:X} {level}");
         return text;
      }

      private static readonly int[] DefaultLevels = { 15, 20, 25, 30, 38, 45, 52, 60, 75, 100 };

      [Fact]
      public void LevelCapOn_TheUserEditsFlagsAndLevels_ByNameOrByNumber() {
         var viewPort = OpenCube(SyntheticRom(), Toml());
         var text = ScriptText("CUBE_LevelCap_ON");
         text = EditCap(text, 1, "FLAG_CUBE_LEVEL_CAP_7", 12);   // another flag, by name
         text = EditCap(text, 2, "2309", 99);                    // by number (decimal, 0x905)
         text = EditCap(text, 3, "CUBELEVELCAP9", 1);            // the spelling without underscores
         text = EditCap(text, 4, "cube_level_cap_10", null);     // lower case
         text = EditCap(text, 10, null, 37);
         Run(viewPort, text);
         var data = viewPort.Model.RawData;
         Assert.Equal((0x06, 9, 12), (data[Caps], data[Caps + 1], data[Caps + 2]));
         Assert.Equal((0x05, 9, 99), (data[Caps + 4], data[Caps + 5], data[Caps + 6]));
         Assert.Equal((0x08, 9, 1), (data[Caps + 8], data[Caps + 9], data[Caps + 10]));
         Assert.Equal((0x09, 9, 30), (data[Caps + 12], data[Caps + 13], data[Caps + 14]));
         Assert.Equal((0x09, 9, 37), (data[Caps + 36], data[Caps + 37], data[Caps + 38]));
         Assert.Equal(38, data[Caps + 4 * 4 + 2]); // the caps that were not edited
      }

      [Fact]
      public void LevelCapOn_ALineThatIsNotUnderstood_LeavesThatCapAsItWas_AndDoesNotShiftTheOthers() {
         var rom = SyntheticRom(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
         var viewPort = OpenCube((byte[])rom.Clone(), Toml());
         var errors = new List<string>();
         viewPort.OnError += (sender, e) => errors.Add(e);
         var text = EditCap(ScriptText("CUBE_LevelCap_ON"), 2, "0x905", null); // hexadecimal is not understood there
         text = EditCap(text, 3, "FLAG_NOT_A_FLAG", 55);
         Run(viewPort, text);
         var data = viewPort.Model.RawData;
         Assert.Equal(2, errors.Count);
         Assert.Equal((1, 9), (data[Caps + 4], data[Caps + 5]));        // cap 2 keeps its flag
         Assert.Equal(20, data[Caps + 6]);                              // and gets its level
         Assert.Equal((2, 9, 55), (data[Caps + 8], data[Caps + 9], data[Caps + 10])); // cap 3 keeps its flag, gets the level
         Assert.Equal(30, data[Caps + 12 + 2]);                         // the next cap is not shifted
      }

      [Fact]
      public void LevelCapOff_KeepsTheCaps() {
         var rom = SyntheticRom(new[] { 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 });
         var viewPort = OpenCube((byte[])rom.Clone(), Toml());
         Run(viewPort, ScriptText("CUBE_LevelCap_OFF"));
         Assert.Equal(0, viewPort.Model[Config + CubeScripts.LevelCapIndex]);
         Assert.Empty(Differences(rom, viewPort.Model.RawData).Where(i => i != Config + CubeScripts.LevelCapIndex));
      }

      [Fact]
      public void TheScripts_AlsoWorkOnARomWhoseTomlHasNoAnchors_ThanksToTheRepair() {
         // Toml() has no anchors for the tables: they are found in the ROM
         var viewPort = OpenCube(SyntheticRom(), Toml());
         Assert.Equal(Config, CubeScripts.ConfigAddress(viewPort.Model));
         Run(viewPort, ScriptText("CUBE_RandomizerMenu_ON"));
         Assert.Equal(1, viewPort.Model[Config + CubeScripts.RandomizerMenuIndex]);
      }

      [Fact]
      public void TheScriptsAreOneUndoStep() {
         var viewPort = OpenCube(SyntheticRom(), Toml());
         var before = viewPort.Model.RawData.ToArray();
         Run(viewPort, ScriptText("CUBE_ExpAll_ON"));
         Assert.False(viewPort.ChangeHistory.IsSaved);
         viewPort.ChangeHistory.Undo.Execute(); // both lines of the script
         Assert.Empty(Differences(before, viewPort.Model.RawData));
      }

      [Fact]
      public void AScriptIsItsOwnUndoStep_EvenAfterAnEditThatWasNotCompleted() {
         var viewPort = OpenCube(SyntheticRom(), Toml());
         viewPort.Edit($"@{CubeScripts.ConfigAnchor}+6 1 "); // an edit of the user
         Run(viewPort, ScriptText("CUBE_RandomizerMenu_ON"));
         viewPort.ChangeHistory.Undo.Execute();
         Assert.Equal(0, viewPort.Model[Config + CubeScripts.RandomizerMenuIndex]); // the script is gone
         Assert.Equal(1, viewPort.Model[Config + 6]);                               // the edit before it is not
      }

      // ---- the real ROM ------------------------------------------------------------------------------------------------

      private static (byte[] rom, string[] toml)? RealCube(string tomlVariable) {
         var path = Environment.GetEnvironmentVariable("HMA_INTRO_CUBE");
         var toml = Environment.GetEnvironmentVariable(tomlVariable);
         if (string.IsNullOrEmpty(path) || !File.Exists(path) || string.IsNullOrEmpty(toml) || !File.Exists(toml)) return null;
         return (File.ReadAllBytes(path), File.ReadAllLines(toml));
      }

      [SkippableTheory]
      [InlineData("HMA_INTRO_CUBE_TOML")]
      [InlineData("HMA_INTRO_CUBE_OLD_TOML")]
      public void RealCube_EveryScriptDoesItsOneJob(string tomlVariable) {
         var cube = RealCube(tomlVariable);
         Skip.If(cube == null, $"set HMA_INTRO_CUBE and {tomlVariable} to run this test");
         var viewPort = OpenCube((byte[])cube.Value.rom.Clone(), cube.Value.toml);
         var config = CubeScripts.ConfigAddress(viewPort.Model);
         Assert.Equal(CubeScripts.FindLevelCaps(cube.Value.rom) - 16, config);
         foreach (var (name, index) in new[] { ("CUBE_EvolutionUseItems", 2), ("CUBE_RandomizerMenu", 4), ("CUBE_LevelCap", 5), ("CUBE_ExpAll", 0) }) {
            foreach (var state in new[] { "ON", "OFF" }) {
               var before = viewPort.Model.RawData.ToArray();
               Run(viewPort, ScriptText($"{name}_{state}"));
               var changed = Differences(before, viewPort.Model.RawData);
               Assert.Equal(state == "ON" ? 1 : 0, viewPort.Model.RawData[config + index]);
               Assert.All(changed, i => Assert.True(i >= config && i < config + 16, $"{name}_{state} wrote {i:X6}"));
            }
         }
         // character customization: Enable = 0, Disable = 1, whatever the ROM has as its default (the CUBE default changed from 1 to 0)
         foreach (var (state, value) in new[] { ("Disable", 1), ("Enable", 0), ("Disable", 1), ("Enable", 0) }) {
            var before = viewPort.Model.RawData.ToArray();
            Run(viewPort, ScriptText($"CUBE_CharacterCustomization_{state}"));
            var changed = Differences(before, viewPort.Model.RawData);
            Assert.Equal(value, viewPort.Model.RawData[config + 3]);
            Assert.All(changed, i => Assert.Equal(config + 3, i));
         }
      }

      [SkippableTheory]
      [InlineData("HMA_INTRO_CUBE_TOML")]
      [InlineData("HMA_INTRO_CUBE_OLD_TOML")]
      public void RealCube_LevelCapScriptWithItsDefaultLines_IsTheRomAsItIs(string tomlVariable) {
         var cube = RealCube(tomlVariable);
         Skip.If(cube == null, $"set HMA_INTRO_CUBE and {tomlVariable} to run this test");
         var viewPort = OpenCube((byte[])cube.Value.rom.Clone(), cube.Value.toml);
         Run(viewPort, ScriptText("CUBE_LevelCap_ON"));
         // the ROM defaults are the defaults of the script; the only change is the switch itself (level caps are off by default since round 14d)
         Assert.Equal(new List<int> { CubeScripts.FindLevelCaps(cube.Value.rom) - 16 + CubeScripts.LevelCapIndex }, Differences(cube.Value.rom, viewPort.Model.RawData));
      }

      [SkippableFact]
      public void RealCube_LevelCapScript_ChangesOneCap() {
         var cube = RealCube("HMA_INTRO_CUBE_OLD_TOML");
         Skip.If(cube == null, "set HMA_INTRO_CUBE and HMA_INTRO_CUBE_OLD_TOML to run this test");
         var viewPort = OpenCube((byte[])cube.Value.rom.Clone(), cube.Value.toml);
         var caps = CubeScripts.FindLevelCaps(cube.Value.rom);
         Run(viewPort, EditCap(ScriptText("CUBE_LevelCap_ON"), 5, null, 41));
         Assert.Equal(new List<int> { caps + 4 * 4 + 2 }, Differences(cube.Value.rom, viewPort.Model.RawData));
         Assert.Equal(41, viewPort.Model.RawData[caps + 4 * 4 + 2]);
      }

      [SkippableFact]
      public void RealCube_EvolutionItemsOnWritesTheByteTheRomReads() {
         var cube = RealCube("HMA_INTRO_CUBE_OLD_TOML");
         Skip.If(cube == null, "set HMA_INTRO_CUBE and HMA_INTRO_CUBE_OLD_TOML to run this test");
         var viewPort = OpenCube((byte[])cube.Value.rom.Clone(), cube.Value.toml);
         Run(viewPort, ScriptText("CUBE_EvolutionUseItems_ON"));
         // gCubeScriptConfig[CUBE_SCRIPT_EVO_ITEMS_USE]: the byte 2 of the config table
         Assert.Equal(new List<int> { CubeScripts.FindLevelCaps(cube.Value.rom) - 16 + 2 }, Differences(cube.Value.rom, viewPort.Model.RawData));
      }
   }
}
