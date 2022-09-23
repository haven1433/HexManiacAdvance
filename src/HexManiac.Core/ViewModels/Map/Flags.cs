using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Code;
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
   public class Flags {
      private const int ScriptLengthLimit = 0x1000;
      private const int ScriptCountLimit = 100;

      public static HashSet<int> GetUsedItemFlags(IDataModel model, ScriptParser parser) {
         var usedFlags = new HashSet<int>();

         foreach (var element in GetAllEvents(model, "objects")) {
            usedFlags.Add(element.GetValue("flag"));
         }

         foreach (var spot in GetAllScriptSpots(model, parser, GetAllTopLevelScripts(model), 0x29, 0x2A, 0x2B)) {
            usedFlags.Add(model.ReadMultiByteValue(spot.Address + 1, 2));
         }

         return usedFlags;
      }

      public static HashSet<int> FindFlagUsages(IDataModel model, ScriptParser parser, int flag) {
         var usages = new HashSet<int>();

         foreach (var element in GetAllEvents(model, "objects")) {
            if (element.GetValue("flag") == flag) usages.Add(element.Start + 20);
         }

         foreach (var spot in GetAllScriptSpots(model, parser, GetAllTopLevelScripts(model), 0x29, 0x2A, 0x2B)) {
            if (model.ReadMultiByteValue(spot.Address + 1, 2) == flag) usages.Add(spot.Address + 1);
         }

         return usages;
      }

      public static ISet<int> GetUsedTrainerFlags(IDataModel model, ScriptParser parser) {
         var trainerFlags = new HashSet<int>();
         foreach (var spot in GetAllScriptSpots(model, parser, GetAllTopLevelScripts(model), 0x5C)) {
            var trainerFlag = model.ReadMultiByteValue(spot.Address + 2, 2);
            trainerFlags.Add(trainerFlag);
         }
         // TODO need to check the rematch table
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
         foreach (var scriptStart in initialAddresses) {
            if (scriptStart < 0 || scriptStart >= model.Count) continue;
            var scriptsToCheck = new List<int> { scriptStart };
            for (int i = 0; i < scriptsToCheck.Count; i++) {
               var address = scriptsToCheck[i];
               int currentScriptLength = 0;
               while (currentScriptLength < ScriptLengthLimit) {
                  var line = parser.GetLine(model, address);
                  if (line == null) break;
                  var length = line.CompiledByteLength(model, address);
                  if (filter.Contains(model[address])) yield return new(address, line);
                  if (line.PointsToNextScript) {
                     var destination = model.ReadPointer(address + length - 4);
                     if (destination >= 0 && destination < model.Count && !scriptsToCheck.Contains(destination) && scriptsToCheck.Count < ScriptCountLimit) scriptsToCheck.Add(destination);
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

         return scriptAddresses; // takes about <1s
      }

      /// <param name="type">Expects "objects" "scripts" or "signposts"</param>
      /// <returns></returns>
      public static IList<ModelArrayElement> GetAllEvents(IDataModel model, string type) {
         var elements = new List<ModelArrayElement>();
         var banks = model.GetTableModel(HardcodeTablesModel.MapBankTable);
         foreach (var bank in banks) {
            var maps = bank.GetSubTable("maps");
            foreach (var map in maps) {
               var events = map.GetSubTable("map")[0].GetSubTable("events")[0];

               // id. graphics. unused: x:500 y:500 elevation. moveType. range: trainerType: trainerRangeOrBerryID: script<`xse`> flag: unused:
               var objects = events.GetSubTable(type);
               if (objects != null) elements.AddRange(objects);
            }
         }
         return elements;
      }
   }

   public record ScriptSpot(int Address, ScriptLine Line);
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
