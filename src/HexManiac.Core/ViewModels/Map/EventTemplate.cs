using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Code;
using HavenSoft.HexManiac.Core.Models.Runs;
using System;
using System.Collections.Generic;

// example for making a bug trainer: templates.CreateTrainer(objectEvent, history.CurrentChange, 20 /* bug catcher */, 30, 9, 6 /*bug*/, true);


namespace HavenSoft.HexManiac.Core.ViewModels.Map {
   public class EventTemplate {
      private readonly Random rnd = new();
      private readonly IDataModel model;
      private readonly ScriptParser parser;
      private ISet<int> usedFlags;
      private ISet<int> usedTrainerFlags;
      private IReadOnlyDictionary<int, TrainerPreference> trainerPreferences;
      private IReadOnlyDictionary<int, int> minLevel;

      private ISet<int> UsedFlags {
         get {
            if (usedFlags == null) usedFlags = Flags.GetUsedItemFlags(model, parser);
            return usedFlags;
         }
      }

      private ISet<int> UsedTrainerFlags {
         get {
            if (usedTrainerFlags == null) usedTrainerFlags = Flags.GetUsedTrainerFlags(model, parser);
            return usedTrainerFlags;
         }
      }

      private IReadOnlyDictionary<int, TrainerPreference> TrainerPreferences {
         get {
            if (trainerPreferences == null) trainerPreferences = Flags.GetTrainerPreference(model, parser);
            return trainerPreferences;
         }
      }

      private IReadOnlyDictionary<int, int> MinLevel {
         get {
            if (minLevel == null) minLevel = Flags.GetMinimumLevelForPokemon(model);
            return minLevel;
         }
      }

      public EventTemplate(IDataModel model, ScriptParser parser) => (this.model, this.parser) = (model, parser);

      // TODO use all-caps name or mixed-caps name depending on other trainers in the table
      // TODO use reference file to get names and before/win/after text
      public void CreateTrainer(ObjectEventModel objectEventModel, ModelDelta token, int graphics, int maxPokedex, int maxLevel, int preferredType, bool useNational) {
         // part 1: the team
         var availablePokemon = new List<int>();
         var dexName = HardcodeTablesModel.RegionalDexTableName;
         if (useNational) dexName = HardcodeTablesModel.NationalDexTableName;
         var pokedex = model.GetTableModel(dexName, token);
         var pokestats = model.GetTableModel(HardcodeTablesModel.PokemonStatsTable, token);
         for (int i = 1; i < pokedex.Count; i++) {
            if (pokedex[i - 1].GetValue(0) > maxPokedex) continue;
            if (MinLevel.TryGetValue(i, out var level) && level > maxLevel) continue;
            availablePokemon.Add(i);
            if (pokestats[i].GetValue("type1") == preferredType || pokestats[i].GetValue("type2") == preferredType) {
               for (int j = 0; j < 9; j++) availablePokemon.Add(i);
            }
         }

         var teamSize = rnd.Next(3) + 1;
         var teamStart = model.FindFreeSpace(model.FreeSpaceStart, 8 * teamSize);
         for (int i = 0; i < teamSize; i++) {
            // ivSpread: level: mon: padding:
            var pokemon = availablePokemon[rnd.Next(availablePokemon.Count)];
            var level = maxLevel;
            while (level > maxLevel - 5 && rnd.Next(2) == 1) level--;
            model.WriteMultiByteValue(teamStart + i * 8 + 0, 2, token, 0);
            model.WriteMultiByteValue(teamStart + i * 8 + 2, 2, token, level);
            model.WriteMultiByteValue(teamStart + i * 8 + 4, 2, token, pokemon);
            model.WriteMultiByteValue(teamStart + i * 8 + 6, 2, token, 0);
         }

         // part 2: the trainer
         var trainerFlag = 1;
         while (UsedTrainerFlags.Contains(trainerFlag)) trainerFlag++;
         usedTrainerFlags.Add(trainerFlag);

         var trainers = model.GetTableModel(HardcodeTablesModel.TrainerTableName, token);
         var trainer = trainers[trainerFlag];
         // structType. class. introMusicAndGender. sprite. name""12 item1: item2: item3: item4: doubleBattle:: ai:: pokemonCount:: pokemon<>
         trainer.SetValue("structType", 0);
         trainer.SetStringValue("name", "Francis");
         trainer.SetValue("item1", 0);
         trainer.SetValue("item2", 0);
         trainer.SetValue("item3", 0);
         trainer.SetValue("item4", 0);
         trainer.SetValue("doubleBattle", 0);
         trainer.SetValue("ai", 0);
         trainer.SetValue("pokemonCount", teamSize);
         trainer.SetAddress("pokemon", teamStart);
         if (!TrainerPreferences.TryGetValue(graphics, out var pref)) pref = new(0, 0, 0);
         trainer.SetValue("class", pref.TrainerClass);
         trainer.SetValue("introMusicAndGender", pref.MusicAndGender);
         trainer.SetValue("sprite", pref.Sprite);

         // part 3: the script
         /*
              trainerbattle 00 102 0 <before> <during>
              loadpointer 0 <after>
              callstd 6
              end
          */
         //       2                  6       10          16
         // 5C 00 trainerFlag: 00 00 <before> <win> 0F 00 <after> 09 06 02
         var before = WriteText(token, "Before!");
         var win = WriteText(token, "You Win!");
         var after = WriteText(token, "After!");
         var scriptStart = model.FindFreeSpace(model.FreeSpaceStart, 24);
         token.ChangeData(model, scriptStart, "5C 00 00 00 00 00 00 00 00 00 00 00 00 00 0F 00 00 00 00 00 09 06 02 00".ToByteArray());
         model.WriteMultiByteValue(scriptStart + 2, 2, token, trainerFlag);
         model.WritePointer(token, scriptStart + 6, before);
         model.WritePointer(token, scriptStart + 10, win);
         model.WritePointer(token, scriptStart + 16, after);

         // part 4: the event
         objectEventModel.Graphics = graphics;
         objectEventModel.ScriptAddress = scriptStart;
         objectEventModel.TrainerType = 1;
         objectEventModel.TrainerRangeOrBerryID = 5;
         objectEventModel.RangeXY = "(1,1)";
         objectEventModel.MoveType = 9;
         objectEventModel.Flag = 0;
         objectEventModel.Elevation = 3;
      }

      // TODO create NPC
      // TODO create item

      private int WriteText(ModelDelta token, string text) {
         var bytes = model.TextConverter.Convert(text, out var _);
         var start = model.FindFreeSpace(model.FreeSpaceStart, bytes.Count);
         token.ChangeData(model, start, bytes);
         return start;
      }
   }
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
