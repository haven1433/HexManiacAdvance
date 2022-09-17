using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Code;
using HavenSoft.HexManiac.Core.Models.Runs;
using IronPython.Compiler;
using Microsoft.Scripting.Hosting;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;

/*
 * There are 0x500 flags, followed by 0x300 trainer flags (0x500 to 0x7FF), then system flags (0x800 to 0x8FF). Not all the system flags are used.
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

         foreach (var spot in GetAllScriptSpots(model, parser, 0x29, 0x2A, 0x2B)) {
            usedFlags.Add(model.ReadMultiByteValue(spot.Address + 1, 2));
         }

         return usedFlags;
      }

      public static HashSet<int> FindFlagUsages(IDataModel model, ScriptParser parser, int flag) {
         var usages = new HashSet<int>();

         foreach (var element in GetAllEvents(model, "objects")) {
            if (element.GetValue("flag") == flag) usages.Add(element.Start + 20);
         }

         foreach (var spot in GetAllScriptSpots(model, parser, 0x29, 0x2A, 0x2B)) {
            if (model.ReadMultiByteValue(spot.Address + 1, 2) == flag) usages.Add(spot.Address + 1);
         }

         return usages;
      }

      public static ISet<int> GetUsedTrainerFlags(IDataModel model, ScriptParser parser) {
         var trainerFlags = new HashSet<int>();
         foreach (var spot in GetAllScriptSpots(model, parser, 0x5C)) {
            var trainerFlag = model.ReadMultiByteValue(spot.Address + 2, 2);
            trainerFlags.Add(trainerFlag);
         }
         // TODO need to check the rematch table
         return trainerFlags;
      }

      public static ISet<int> GetTrainerFlagUsages(IDataModel model, ScriptParser parser, int flag) {
         var flagUsages = new HashSet<int>();
         foreach (var spot in GetAllScriptSpots(model, parser, 0x5C)) {
            var trainerFlag = model.ReadMultiByteValue(spot.Address + 2, 2);
            if (trainerFlag == flag) flagUsages.Add(spot.Address + 2);
         }
         // TODO need to check the rematch table
         return flagUsages;
      }

      public static IEnumerable<ScriptSpot> GetAllScriptSpots(IDataModel model, ScriptParser parser, params byte[] filter) {
         foreach (var scriptStart in GetAllTopLevelScripts(model)) {
            if (scriptStart < 0 || scriptStart >= model.Count) continue;
            var scriptsToCheck = new List<int> { scriptStart };
            for (int i = 0; i < scriptsToCheck.Count; i++) {
               var address = scriptsToCheck[i];
               int currentScriptLength = 0;
               while (currentScriptLength < ScriptLengthLimit) {
                  var line = parser.GetLine(model, address);
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
         var banks = model.GetTableModel(HardcodeTablesModel.MapBankTable, new());
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
