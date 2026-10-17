using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using System;
using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.ViewModels {
   /// <summary>
   /// The CUBE "scripts": optional gameplay systems that are always compiled into CUBE and switched on or off by one byte each.
   /// The bytes live in gCubeScriptConfig (anchor cube.scripts.config, format [on.]16), the level caps in gCubeLevelCaps (anchor cube.scripts.levelcaps).
   /// The HMA scripts in resources/Scripts/CUBE write those bytes; the Goto entries Scripts > EXP Share / Level Cap lead to them.
   ///
   /// A CUBE that was modified and saved with an older metadata file (.toml) does not have those anchors. RepairMissingTables finds the tables by their content
   /// when the ROM is loaded, so everything above works for it too. A ROM without the tables (not a CUBE, or an older CUBE) has no scripts:
   /// every lookup here is tolerant and answers "unavailable".
   /// </summary>
   public static class CubeScripts {
      public const string ConfigAnchor = "cube.scripts.config", LevelCapAnchor = "cube.scripts.levelcaps", NewGameSpawnAnchor = "cube.newgame.spawn";

      // The formats are the ones of CUBE.toml. A ROM that gets its anchors from RepairMissingTables must look the same as one that got them from the toml.
      public const string ConfigFormat = "[on.]16";
      public const string LevelCapFormat = "[capAppliesWhileFlagSet:scriptflagaliases level. padding.]10";
      // x and y are s16, 2 bytes each ('x::' would be 4 bytes): the struct has to add up to the 16 bytes of NewGameSpawnLength
      public const string NewGameSpawnFormat = "[magic\"\"8 mapGroup. mapNum. x:|z y:|z shakeTruck. padding.]1";

      public const int ExpAllIndex = 0, ExpAllPercentIndex = 1, EvoItemsIndex = 2, NoCharacterCustomizationIndex = 3, RandomizerMenuIndex = 4, LevelCapIndex = 5, ReusableTmsIndex = 6;
      public const int ConfigLength = 16, DefaultExpAllPercent = 20;
      public const int LevelCapCount = 10, LevelCapLength = 4, FirstLevelCapFlag = 0x900;
      public const int NewGameSpawnLength = 16;
      public const string NewGameSpawnMagic = "CUBESPWN";

      /// <summary>The name of cap flag 1-10 (flag 0x900 + number - 1) in the flag lists, so scripts can say 'setflag FLAG_CUBE_LEVEL_CAP_3'.</summary>
      public static string LevelCapFlagName(int number) => $"FLAG_CUBE_LEVEL_CAP_{number}";

      /// <summary>Flag lists that name flags: the one of the script editor (setflag, checkflag...) and the one of the tables (such as the level cap table).</summary>
      public static readonly string[] FlagListNames = { "scriptnamedflags", "scriptflagaliases" };

      /// <summary>Address of gCubeScriptConfig, or Pointer.NULL if this ROM has no such anchor.</summary>
      public static int ConfigAddress(IDataModel model) {
         if (model == null) return Pointer.NULL;
         try {
            var address = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, ConfigAnchor);
            if (address == Pointer.NULL || address < 0 || address + ConfigLength > model.Count) return Pointer.NULL;
            return address;
         } catch (Exception) {
            return Pointer.NULL; // an odd metadata file must never break the goto list
         }
      }

      public static bool IsAvailable(IDataModel model) => ConfigAddress(model) != Pointer.NULL;

      /// <summary>The raw byte of a switch (0 if the ROM has no scripts).</summary>
      public static int GetByte(IDataModel model, int index) {
         var address = ConfigAddress(model);
         return address == Pointer.NULL ? 0 : model[address + index];
      }

      public static bool IsOn(IDataModel model, int index) => GetByte(model, index) != 0;

      /// <summary>Address of the level cap table, or Pointer.NULL.</summary>
      public static int LevelCapAddress(IDataModel model) {
         if (model == null) return Pointer.NULL;
         try { return model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, LevelCapAnchor); } catch (Exception) { return Pointer.NULL; }
      }

      // ---- finding the tables by their content ---------------------------------------------------------------------------------------------

      /// <summary>
      /// True if a gCubeLevelCaps table starts at 'address': ten entries of {u16 flag, u8 level, u8 padding} whose flags are 0x900, 0x901 ... 0x909 in order.
      /// The levels and the padding are not looked at: they are the part that users edit, and the flags are specific enough.
      /// </summary>
      public static bool LooksLikeLevelCaps(IReadOnlyList<byte> data, int address) {
         if (address < ConfigLength || address + LevelCapCount * LevelCapLength > data.Count) return false;
         for (int i = 0; i < LevelCapCount; i++) {
            var flag = FirstLevelCapFlag + i;
            if (data[address + i * LevelCapLength] != (flag & 0xFF) || data[address + i * LevelCapLength + 1] != flag >> 8) return false;
         }
         return true;
      }

      /// <summary>
      /// True if the 16 bytes at 'address' can be gCubeScriptConfig: switches (any value, nonzero means on), the EXP percentage, and reserved bytes that are 0 or 1.
      /// </summary>
      public static bool LooksLikeConfig(IReadOnlyList<byte> data, int address) {
         if (address < 0 || address + ConfigLength > data.Count) return false;
         for (int i = 6; i < ConfigLength; i++) if (data[address + i] > 1) return false;
         return true;
      }

      /// <summary>
      /// Finds gCubeLevelCaps by content. gCubeScriptConfig is the 16 bytes immediately in front of it, and has to look like one.
      /// Returns the address of the level caps (the config is 16 bytes before it), or Pointer.NULL if there is no table or more than one candidate.
      /// </summary>
      public static int FindLevelCaps(byte[] data) {
         var found = Pointer.NULL;
         for (int address = ConfigLength; address + LevelCapCount * LevelCapLength <= data.Length; address += 2) {
            // cheap test of the first flag before the real check: this loop runs over the whole ROM
            if (data[address] != 0 || data[address + 1] != 9) continue;
            if (!LooksLikeLevelCaps(data, address) || !LooksLikeConfig(data, address - ConfigLength)) continue;
            if (found != Pointer.NULL) return Pointer.NULL; // ambiguous: better no anchor than the wrong one
            found = address;
         }
         return found;
      }

      /// <summary>Finds gCubeNewGameSpawn by its 8 byte magic. Pointer.NULL if the magic is missing, or is there more than once.</summary>
      public static int FindNewGameSpawn(byte[] data) {
         if (data == null) return Pointer.NULL;
         var magic = System.Text.Encoding.ASCII.GetBytes(NewGameSpawnMagic);
         var found = Pointer.NULL;
         var start = 0;
         while (start + NewGameSpawnLength <= data.Length) {
            var at = ((ReadOnlySpan<byte>)data).Slice(start).IndexOf(magic);
            if (at < 0) break;
            var address = start + at;
            start = address + 1;
            if (address + NewGameSpawnLength > data.Length) break;
            if (found != Pointer.NULL) return Pointer.NULL;
            found = address;
         }
         return found;
      }

      // ---- repairing a model that was loaded without the anchors ---------------------------------------------------------------------------

      /// <summary>
      /// Called when a ROM is loaded. If the metadata has no anchor for the CUBE script tables (a CUBE that was modified and saved with an older .toml),
      /// finds the tables by their content and gives them the anchors, and names the flags of the level caps (FLAG_CUBE_LEVEL_CAP_1 - 10) in the flag lists.
      /// Nothing is written to the ROM and no metadata format is added: an older HMA loads a metadata file with these anchors like any other.
      /// An anchor that is already there is never touched, and an odd ROM must never fail to load because of this convenience.
      /// </summary>
      public static void RepairMissingTables(IDataModel model) {
         try {
            var noChange = new NoDataChangeDeltaModel();
            var data = model.RawData;
            if (data == null) return;
            bool Has(string anchor) => model.GetAddressFromAnchor(noChange, -1, anchor) != Pointer.NULL;

            if (!Has(ConfigAnchor) || !Has(LevelCapAnchor)) {
               var caps = FindLevelCaps(data);
               if (caps != Pointer.NULL) {
                  var config = caps - ConfigLength;
                  // an address is only handed out if the other anchor, when it exists, agrees: a half-and-half result would be wrong
                  var configAnchorAt = model.GetAddressFromAnchor(noChange, -1, ConfigAnchor);
                  var capsAnchorAt = model.GetAddressFromAnchor(noChange, -1, LevelCapAnchor);
                  var consistent = (configAnchorAt == Pointer.NULL || configAnchorAt == config) && (capsAnchorAt == Pointer.NULL || capsAnchorAt == caps);
                  if (consistent) {
                     if (configAnchorAt == Pointer.NULL) AddAnchor(model, ConfigAnchor, config, ConfigFormat, ConfigLength);
                     if (capsAnchorAt == Pointer.NULL) AddAnchor(model, LevelCapAnchor, caps, LevelCapFormat, LevelCapCount * LevelCapLength);
                  }
               }
            }

            if (!Has(NewGameSpawnAnchor)) {
               var spawn = FindNewGameSpawn(data);
               if (spawn != Pointer.NULL) AddAnchor(model, NewGameSpawnAnchor, spawn, NewGameSpawnFormat, NewGameSpawnLength);
            }

            // the cap flags get their names (only in a ROM that has the cap table: the same flags mean something else in other ROMs)
            var capTable = LevelCapAddress(model);
            if (capTable != Pointer.NULL && LooksLikeLevelCaps(data, capTable)) {
               foreach (var listName in FlagListNames) NameLevelCapFlags(model, listName);
            }
         } catch (Exception) {
            // see above
         }
      }

      private static bool AddAnchor(IDataModel model, string name, int address, string format, int length) {
         var noChange = new NoDataChangeDeltaModel();
         if (address < 0 || address + length > model.Count) return false;

         // whatever formatted data is in the way (a name, a table, a pointer...) belongs to somebody, so it stays
         for (var run = model.GetNextRun(address); run.Start < address + length; run = model.GetNextRun(Math.Max(run.Start + run.Length, run.Start + 1))) {
            if (run.Start + run.Length <= address) continue;
            if (!(run is NoInfoRun) || !string.IsNullOrEmpty(model.GetAnchorFromAddress(-1, run.Start))) return false;
         }

         // ApplyAnchor does the same checks as typing '^name[format]' in the hex view
         var error = PokemonModel.ApplyAnchor(model, noChange, address, BaseRun.AnchorStart + name + format, allowAnchorOverwrite: true);
         if (error.HasError && !error.IsWarning) return false;
         return model.GetAddressFromAnchor(noChange, -1, name) == address;
      }

      private static void NameLevelCapFlags(IDataModel model, string listName) {
         if (!model.TryGetList(listName, out var list)) return;
         var names = new List<string>(list);
         var changed = false;
         for (int i = 0; i < LevelCapCount; i++) {
            var flag = FirstLevelCapFlag + i;
            // sparse lists (scriptnamedflags) end before the flag: the gap has no names
            while (names.Count <= flag) names.Add(null);
            var current = names[flag];
            var wanted = LevelCapFlagName(i + 1);
            if (current == wanted) continue;
            var isFree = string.IsNullOrWhiteSpace(current) || current == flag.ToString() || Flags.IsFreeName(current);
            if (!isFree) continue; // somebody named it already: that name stays
            names[flag] = wanted;
            changed = true;
         }
         if (changed) model.SetList(new NoDataChangeDeltaModel(), listName, names, list.Comments ?? new Dictionary<int, string>(), list.StoredHash);
      }
   }
}
