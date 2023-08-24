using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Code;
using HavenSoft.HexManiac.Core.Models.Map;
using HavenSoft.HexManiac.Core.Models.Runs;
using System;
using System.Collections.Generic;
using System.Linq;

/*
 * There are 0x500 flags, followed by 0x300 trainer flags (0x500 to 0x7FF), then system flags (0x800 to 0x8FF). Not all the system flags are used.
 * The system flags go from 0x860 to 0x95FF in Emerald, making room for more trainers, but still no more than 864 trainers (Emerald uses 856 by default).
 * The only restriction is that the first 0x20 flags are reset every time you enter a map.
 * That means that they can track things going on within a single map, but should not store information like "has an item been picked up".
 * Given the length of the trainer table, that means that trainers can be safely increase the table size by about 25 (FireRed).
 */

namespace HavenSoft.HexManiac.Core.ViewModels.Map {
   public record BerrySpot(int Address, int BerryID);

   public record FlagContext(int Address, int Bank, int Map);
   public record ObjectFlagContext(int Address, int Bank, int Map, int Object) : FlagContext(Address, Bank, Map);
   public record ScriptFlagContext(int Address, int Bank, int Map, int Script) : FlagContext(Address, Bank, Map);
   public record SignpostFlagContext(int Address, int Bank, int Map, int Signpost) : FlagContext(Address, Bank, Map);
   public record HeaderFlagContext(int Address, int Bank, int Map, int ScriptType) : FlagContext(Address, Bank, Map);

   public class Flags {
      private const int ScriptLengthLimit = 0x1000;
      private const int ScriptCountLimit = 100;

      /// <summary>
      /// Some vars are not used in scripts, but are used in thumb code.
      /// We need to remember these vars explicitly.
      /// </summary>
      public static HashSet<int>
         RS_ThumbVars = new() {
            0x4040, 0x4041, 0x4042, 0x4046, 0x4047, 0x404A, 0x404B, 0x404C, 0x404F,
            0x4054, 0x4097, 0x40C2,
         },
         FRLG_ThumbVars = new() {
            0x4035, 0x4037, 0x4038, 0x4039, 0x403B, 0x403C, 0x403D, 0x4040,
            0x4042, 0x4043, 0x4044, 0x4045, 0x4046, 0x4047, 0x4048, 0x404C, 0x404D, 0x404E, 0x404F,
            0x40AA, 0x40AB, 0x40AC, 0x40AD, 0x40AE,
            0x40B4, 0x40B5, 0x40B6, 0x40B7, 0x40B8, 0x40B9, 0x40BA, 0x40BB, 0x40BC,
            0x40CF,
            0x40E6, 0x40E7, 0x40E8, 0x40E9, 0x40EA, 0x40EB,
            0x40F1,
         },
         Emerald_ThumbVars = new() {
            0x4036, 0x4038, 0x403E,
            0x4040, 0x4041, 0x4042, 0x4046, 0x4047, 0x404A, 0x404B, 0x404C, 0x404F,
            0x4097, 0x40C2, 0x40CD,
            0x40D0, 0x40D4, 0x40DD, 0x40DE, 0x40DF,
            0x40E0, 0x40E1, 0x40E2, 0x40E3, 0x40E4, 0x40E6, 0x40E7, 0x40E8, 0x40E9, 0x40EA, 0x40EB, 0x40EC, 0x40EE, 0x40EF,
            0x40F0, 0x40F1, 0x40F5, 0x40F6,
         };

      /// <summary>
      /// IDs of every used flag
      /// </summary>
      public static HashSet<int> GetUsedItemFlags(IDataModel model, ScriptParser parser) {
         var usedFlags = new HashSet<int>();

         foreach (var element in GetAllEvents(model, "objects")) {
            if (!element.HasField("flag")) continue;
            usedFlags.Add(element.GetValue("flag"));
         }

         foreach (var spot in GetAllScriptSpots(model, parser, GetAllTopLevelScripts(model), 0x29, 0x2A, 0x2B)) {
            usedFlags.Add(model.ReadMultiByteValue(spot.Address + 1, 2));  
         }

         return usedFlags;
      }

      /// <returns>Every address where this flag is used</returns>
      public static HashSet<(int, int)> FindFlagUsages(IDataModel model, ScriptParser parser, int flag) {
         var usages = new HashSet<(int, int)>();

         foreach (var element in GetAllEvents(model, "objects")) {
            if (element.GetValue("flag") == flag) usages.Add((element.Start, element.Start + element.Length - 1));
         }

         foreach (var spot in GetAllScriptSpots(model, parser, GetAllTopLevelScripts(model), 0x29, 0x2A, 0x2B)) {
            var address = spot.Address;
            if (model.ReadMultiByteValue(address + 1, 2) == flag) {
               usages.Add((address, address + spot.Line.CompiledByteLength(model, address, null) - 1));
            }
         }

         return usages;
      }

      public static HashSet<int> GetUsedVariables(IDataModel model, ScriptParser parser) {
         var usedVariables = new HashSet<int>();
         if (model.IsFRLG()) {
            usedVariables.AddRange(FRLG_ThumbVars);
         } else if (model.IsEmerald()) {
            usedVariables.AddRange(Emerald_ThumbVars);
         } else {
            usedVariables.AddRange(RS_ThumbVars);
         }

         foreach (var element in GetAllEvents(model, "scripts")) {
            if (!element.HasField("trigger")) continue;
            usedVariables.Add(element.GetValue("trigger"));
         }

         foreach (var spot in GetAllScriptSpots(model, parser, GetAllTopLevelScripts(model), 0x16, 0x17, 0x18, 0x19, 0x1A, 0x21, 0x22, 0x26)) { // setvar, addvar, subvar, copyvar, setorcopyvar, compare, comparevars, special2
            usedVariables.Add(model.ReadMultiByteValue(spot.Address + 1, 2));
         }

         return usedVariables;
      }

      public static HashSet<(int, int)> FindVarUsages(IDataModel model, ScriptParser parser, int variable) {
         var usages = new HashSet<(int, int)>();

         foreach (var element in GetAllEvents(model, "scripts")) {
            if (!element.HasField("trigger")) continue;
            if (element.GetValue("trigger") == variable) usages.Add((element.Start, element.Start + element.Length - 1));
         }

         foreach (var spot in GetAllScriptSpots(model, parser, GetAllTopLevelScripts(model), 0x16, 0x17, 0x18, 0x19, 0x1A, 0x21, 0x22, 0x26)) { // setvar, addvar, subvar, copyvar, setorcopyvar, compare, comparevars, special2
            var address = spot.Address;
            if (model.ReadMultiByteValue(address + 1, 2) == variable) {
               usages.Add((address, address + spot.Line.CompiledByteLength(model, address, null) - 1));
            }
         }

         return usages;
      }

      /// <summary>
      /// Instead of just finding out which flags are used,
      /// this method attempts to find all the context for each flag.
      /// Any flag in this dictionary is used at least once by a header, object, script, or signpost.
      /// You can check the flag to see info about what calls it.
      /// * The exact address that uses the flag
      /// * the Bank/Map that uses the flag, as well as the event type and index
      /// * For header scripts, the type of script that uses it
      /// </summary>
      public static Dictionary<int, List<FlagContext>> GetAllFlagContext(IDataModel model, ScriptParser parser) {
         var usedFlags = new Dictionary<int, List<FlagContext>>();
         var filter = new byte[] { 0x29, 0x2A, 0x2B }; // setflag, clearflag, checkflag

         void Add(int scriptAddress, Func<ScriptSpot, FlagContext> predicate) {
            foreach (var spot in GetAllScriptSpots(model, parser, new[] { scriptAddress }, filter)) {
               var flag = model.ReadMultiByteValue(spot.Address + 1, 2);
               if (!usedFlags.ContainsKey(flag)) usedFlags.Add(flag, new());
               usedFlags[flag].Add(predicate(spot));
            }
         }

         var banks = AllMapsModel.Create(model, default);
         for (int bankIndex = 0; bankIndex < banks.Count; bankIndex++) {
            var bank = banks[bankIndex];
            if (bank == null) continue;
            for (int mapIndex = 0; mapIndex < bank.Count; mapIndex++) {
               var map = bank[mapIndex];
               if (map == null) continue;

               // map header scripts
               var headerScripts = map.MapScripts;
               if (headerScripts != null) {
                  for (int scriptIndex = 0; scriptIndex < headerScripts.Count; scriptIndex++) {
                     var script = headerScripts[scriptIndex];
                     if (script == null) continue;
                     if (script.GetValue("type").IsAny(2, 4)) {
                        var start = script.GetAddress("pointer");
                        if (start >= model.Count || start < 0) continue;
                        int initialStart = start;
                        while (!model.ReadMultiByteValue(start, 2).IsAny(0, 0xFFFF)) {
                           Add(model.ReadPointer(start + 4), spot => new HeaderFlagContext(spot.Address, bankIndex, mapIndex, script.GetValue("type")));
                           start += 8;
                           if (start - initialStart > 100) break; // sanity check
                        }
                     } else {
                        Add(script.GetAddress("pointer"), spot => new HeaderFlagContext(spot.Address, bankIndex, mapIndex, script.GetValue("type")));
                     }
                  }
               }

               // objects
               var objects = map.Events.Objects;
               for (int objectIndex = 0; objectIndex < objects.Count; objectIndex++) {
                  var obj = objects[objectIndex];
                  if (obj == null) continue;
                  var objFlag = obj.Flag;
                  if (objFlag != 0) {
                     if (!usedFlags.ContainsKey(objFlag)) usedFlags.Add(objFlag, new());
                     usedFlags[objFlag].Add(new ObjectFlagContext(obj.Element.Start, bankIndex, mapIndex, objectIndex));
                  }
                  Add(obj.ScriptAddress, spot => new ObjectFlagContext(spot.Address, bankIndex, mapIndex, objectIndex));
               }

               // scripts
               var scripts = map.Events.Scripts;
               for (int scriptIndex = 0; scriptIndex < scripts.Count; scriptIndex++) {
                  var script = scripts[scriptIndex];
                  if (script == null) continue;
                  Add(script.ScriptAddress, spot => new ScriptFlagContext(spot.Address, bankIndex, mapIndex, scriptIndex));
               }

               // signposts
               var signposts = map.Events.Signposts;
               for (int signpostIndex = 0; signpostIndex < signposts.Count; signpostIndex++) {
                  var signpost = signposts[signpostIndex];
                  if (signpost == null || !signpost.HasScript) continue;
                  Add(signpost.ScriptAddress, spot => new SignpostFlagContext(spot.Address, bankIndex, mapIndex, signpostIndex));
               }
            }
         }

         return usedFlags;
      }

      public static Dictionary<int, BerrySpot> GetBerrySpots(IDataModel model, ScriptParser parser) {
         var code = model.GetGameCode();
         if (!code.StartsWith("AXPE") && !code.StartsWith("AXVE") && !code.StartsWith("BPEE")) return new();

         var start = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, "scripts.newgame.setflags");
         if (start < 0 || start >= model.Count) return new();

         var results = new Dictionary<int, BerrySpot>();
         foreach (var spot in GetAllScriptSpots(model, parser, new[] { start }, 0x8A)) {
            var plantID = model[spot.Address + 1];
            var berryID = model[spot.Address + 2];
            results[plantID] =  new(spot.Address, berryID - 1);
         }
         return results;
      }

      public static ISet<int> GetUsedTrainerFlags(IDataModel model, ScriptParser parser) {
         var trainerFlags = new HashSet<int>();

         // check all scripts
         foreach (var spot in GetAllScriptSpots(model, parser, GetAllTopLevelScripts(model), 0x5C)) {
            var trainerFlag = model.ReadMultiByteValue(spot.Address + 2, 2);
            trainerFlags.Add(trainerFlag);
         }

         // check rematch table
         var rematches = model.GetTableModel(HardcodeTablesModel.RematchTable);
         if (rematches != null) {
            foreach(var rematch in rematches) {
               if (!trainerFlags.Contains(rematch.GetValue("match1"))) continue;
               foreach (var match in new[] { "match2", "match3", "match4", "match5", "match6" }.Select(rematch.GetValue)) trainerFlags.Add(match);
            }
         }

         return trainerFlags;
      }

      public static IReadOnlyDictionary<int, int> GetMinimumLevelForPokemon(IDataModel model) {
         var evolutions = model.GetTableModel(HardcodeTablesModel.EvolutionTableName);
         var levelMethods = new[] { 4, 8, 9, 10, 11, 12, 13, 14 };
         var results = new Dictionary<int, int>();
         foreach (var evo in evolutions) {
            // method1:evolutionmethods arg1:|s=method1(6=data.items.stats|7=data.items.stats) species1:data.pokemon.names unused1:
            for (int i = 0; i < evo.Length; i += 8) {
               var method = model.ReadMultiByteValue(evo.Start + i, 2);
               if (!levelMethods.Contains(method)) continue;
               var level = model.ReadMultiByteValue(evo.Start + i + 2, 2);
               var species = model.ReadMultiByteValue(evo.Start + i + 4, 2);
               results[species] = level;
            }
         }
         return results;
      }

      public static ISet<int> GetTrainerFlagUsages(IDataModel model, ScriptParser parser, int flag) {
         var flagUsages = new HashSet<int>();
         foreach (var spot in GetAllScriptSpots(model, parser, GetAllTopLevelScripts(model), 0x5C)) {
            var trainerFlag = model.ReadMultiByteValue(spot.Address + 2, 2);
            if (trainerFlag == flag) flagUsages.Add(spot.Address + 2);
         }
         // TODO need to check the rematch table
         return flagUsages;
      }

      public static IReadOnlyDictionary<int, TrainerPreference> GetTrainerPreference(IDataModel model, ScriptParser parser) {
         var trainers = model.GetTableModel(HardcodeTablesModel.TrainerTableName);

         var classHistogram = new Dictionary<int, Dictionary<int, int>>();
         var musicHistogram = new Dictionary<int, Dictionary<int, int>>();
         var spriteHistogram = new Dictionary<int, Dictionary<int, int>>();

         // for each OW that is a trainer, count up how many times it uses each trainer sprite and music
         // class.data.trainers.classes.names introMusicAndGender.|t|music:::.|female. sprite.graphics.trainers.sprites.front
         foreach (var element in GetAllEvents(model, "objects")) {
            if (!element.HasField("graphics")) continue;
            var graphics = element.GetValue("graphics");
            if (!classHistogram.TryGetValue(graphics, out var desiredClass)) classHistogram[graphics] = desiredClass = new();
            if (!musicHistogram.TryGetValue(graphics, out var desiredMusic)) musicHistogram[graphics] = desiredMusic = new();
            if (!spriteHistogram.TryGetValue(graphics, out var desiredSprite)) spriteHistogram[graphics] = desiredSprite = new();
            foreach (var spot in GetAllScriptSpots(model, parser, new[] { element.GetAddress("script") }, 0x5C)) {
               var trainerFlag = model.ReadMultiByteValue(spot.Address + 2, 2);
               if (trainerFlag >= trainers.Count) continue;
               var trainer = trainers[trainerFlag];
               var pref = new TrainerPreference(trainer.GetValue("class"), trainer.GetValue("introMusicAndGender"), trainer.GetValue("sprite"));

               if (!desiredClass.TryGetValue(pref.TrainerClass, out var count)) desiredClass[pref.TrainerClass] = 0;
               desiredClass[pref.TrainerClass]++;

               if (!desiredMusic.TryGetValue(pref.MusicAndGender, out count)) desiredMusic[pref.MusicAndGender] = 0;
               desiredMusic[pref.MusicAndGender]++;

               if (!desiredSprite.TryGetValue(pref.Sprite, out count)) desiredSprite[pref.Sprite] = 0;
               desiredSprite[pref.Sprite]++;
            }
         }

         var results = new Dictionary<int, TrainerPreference>();
         foreach (var key in classHistogram.Keys) {
            results[key] = new(GetKeyWithHighestValue(classHistogram[key]), GetKeyWithHighestValue(musicHistogram[key]), GetKeyWithHighestValue(spriteHistogram[key]));
         }
         return results;
      }

      private static T GetKeyWithHighestValue<T>(IReadOnlyDictionary<T, int> dict) {
         T bestKey = default;
         int bestValue = -1;
         foreach (var (k, v) in dict) {
            if (v > bestValue) (bestKey, bestValue) = (k, v);
         }
         return bestKey;
      }

      public static IEnumerable<ScriptSpot> GetAllScriptSpots(IDataModel model, ScriptParser parser, IEnumerable<int> initialAddresses, params byte[] filter) {
         return GetAllScriptSpots(model, parser, initialAddresses, true, filter);
      }

      public static IEnumerable<ScriptSpot> GetAllScriptSpots(IDataModel model, ScriptParser parser, IEnumerable<int> initialAddresses, bool includeMacros, params byte[] filter) {
         foreach (var scriptStart in initialAddresses) {
            if (scriptStart < 0 || scriptStart >= model.Count) continue;
            var scriptsToCheck = new List<int> { scriptStart };
            for (int i = 0; i < scriptsToCheck.Count; i++) {
               var address = scriptsToCheck[i];
               int currentScriptLength = 0;
               while (currentScriptLength < ScriptLengthLimit) {
                  IScriptLine line = includeMacros ? parser.GetMacro(model, address) : null;
                  if (line == null) line = parser.GetLine(model, address);
                  if (line == null) break;
                  var length = line.CompiledByteLength(model, address, null);
                  if (filter.Contains(model[address])) yield return new(address, line);

                  var commandOffset = line.LineCode.Count;
                  foreach (var arg in line.Args) {
                     if (arg.PointerType == ExpectedPointerType.Script) {
                        var destination = model.ReadPointer(address + commandOffset);
                        if (destination >= 0 && destination < model.Count && !scriptsToCheck.Contains(destination) && scriptsToCheck.Count < ScriptCountLimit) scriptsToCheck.Add(destination);
                     }
                     commandOffset += arg.Length(model, -1);
                  }

                  if (line.IsEndingCommand) break;
                  address += length;
                  currentScriptLength += length;
               }
            }
         }
      }

      public static ISet<int> GetAllTopLevelScripts(IDataModel model) {
         var scriptAddresses = new HashSet<int>();

         foreach (var element in GetAllEvents(model, "objects")) scriptAddresses.Add(element.GetAddress("script"));
         foreach (var element in GetAllEvents(model, "scripts")) scriptAddresses.Add(element.GetAddress("script"));
         foreach (var element in GetAllEvents(model, "signposts")) scriptAddresses.Add(element.GetAddress("arg"));

         // header scripts
         foreach (var bank in AllMapsModel.Create(model, default)) {
            if (bank == null) continue;
            foreach (var map in bank) {
               if (map == null) continue;
               var scripts = map.MapScripts;
               if (scripts == null) continue;
               foreach (var script in scripts) {
                  if (script == null) continue;
                  if (script.GetValue("type").IsAny(2, 4)) {
                     var start = script.GetAddress("pointer");
                     int childCount = 0;
                     if (start >= model.Count || start < 0) continue;
                     while (!model.ReadMultiByteValue(start, 2).IsAny(0, 0xFFFF)) {
                        scriptAddresses.Add(model.ReadPointer(start + 4));
                        start += 8;
                        childCount += 1;
                        if (childCount > 100) break; // sanity check
                     }
                  } else {
                     scriptAddresses.Add(script.GetAddress("pointer"));
                  }
               }
            }
         }

         return scriptAddresses; // takes about <1s
      }

      /// <param name="type">Expects "objects" "scripts" or "signposts"</param>
      /// <returns></returns>
      public static IList<ModelArrayElement> GetAllEvents(IDataModel model, string type) {
         var elements = new List<ModelArrayElement>();
         var banks = model.GetTableModel(HardcodeTablesModel.MapBankTable);
         if (banks == null) return elements;
         foreach (var bank in banks) {
            var maps = bank.GetSubTable("maps");
            if (maps == null) continue;
            foreach (var map in maps) {
               var mapTable = map.GetSubTable("map");
               if (mapTable == null) continue;
               var mapElement = mapTable[0];
               if (mapElement == null || !mapElement.HasField("events")) continue;
               var eventTable = mapElement.GetSubTable("events");
               if (eventTable == null) continue;
               var events = eventTable[0];
               if (events == null) continue;

               // id. graphics. unused: x:500 y:500 elevation. moveType. range: trainerType: trainerRangeOrBerryID: script<`xse`> flag: unused:
               var objects = events.GetSubTable(type);
               if (objects != null) elements.AddRange(objects);
            }
         }
         return elements;
      }
   }

   public record ScriptSpot(int Address, IScriptLine Line);
   public record TrainerPreference(int TrainerClass, int MusicAndGender, int Sprite);
}

/*
 * FireRed flags that get missed by the current algorithm:
// 2A2 visited sevii island 2
// 2A7 -> aurora ticket
// 2A8 -> mystic ticket
// 2CF -> visited Oak's Lab
// 2D2/2D3 -> seafoam B3F/B4F current
// 2DE -> tutor frezy plant?
// 2DF -> tutor blast burn?
// 2E0 -> tutor hydro cannon?
// missing 3E8 to 4A6 (hidden items)
// missing 4BC -> defeat champ

 * known gaps in the current algorithm:
 * -> doesn't check map header scripts
 * -> doesn't understand flags that are set using variables
 */
