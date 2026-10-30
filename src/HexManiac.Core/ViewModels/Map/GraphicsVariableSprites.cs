using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Code;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace HavenSoft.HexManiac.Core.ViewModels.Map {
   /// <summary>How the map editor draws an object event whose graphics is one of the 16 graphics variables (VAR_OBJ_GFX_ID_0 ... F). View menu choice, saved with the settings.</summary>
   public enum GraphicsVariableDisplay {
      /// <summary>The first frame of a sprite that a script gives the variable (setvar VAR_OBJ_GFX_ID_x sprite). If no script does, the blacked-out sprite with a question mark.</summary>
      ScriptSprite = 0,
      /// <summary>A blacked-out regular NPC with a small question mark on it.</summary>
      BlackedOutQuestionMark = 1,
      /// <summary>The question mark picture the original HexManiacAdvance draws for sprites it does not know (the icon of the first species, "?????").</summary>
      QuestionMark = 2,
      /// <summary>The first frame of the icon of the first Pokemon of the Pokedex.</summary>
      FirstPokemonIcon = 3,
   }

   /// <summary>
   /// Finds the sprites that scripts give to the graphics variables: 'setvar VAR_OBJ_GFX_ID_x sprite' (command 0x16, variable 0x4010 + x).
   /// Which sprite a variable really holds is decided at runtime, so this is only a good guess for the editor: the first assignment that is found.
   /// </summary>
   public static class GraphicsVariableSprites {
      public const int FirstVariable = 0x4010;
      private const int SetVarCommand = 0x16;

      /// <summary>The variable-to-sprite assignments of some scripts, in the order the scripts were walked.</summary>
      public record Assignments(IReadOnlyList<(int Variable, int Sprite)> All) {
         /// <summary>The sprites given to graphics variable number 'variable' (0 to 15), first one first.</summary>
         public IEnumerable<int> For(int variable) => All.Where(a => a.Variable == variable).Select(a => a.Sprite);
         public IEnumerable<int> Any() => All.Select(a => a.Sprite);
      }

      private static readonly Assignments None = new(Array.Empty<(int, int)>());

      // The script parser belongs to the open ROM's code tool. The static drawing code has none, so the map editor registers it here whenever it draws an event of the ROM.
      private static readonly ConditionalWeakTable<IDataModel, ScriptParser> parsers = new();
      public static void RegisterParser(IDataModel model, ScriptParser parser) {
         if (model == null || parser == null) return;
         lock (parsers) { parsers.Remove(model); parsers.Add(model, parser); }
      }
      private static ScriptParser GetParser(IDataModel model) {
         lock (parsers) return parsers.TryGetValue(model, out var parser) ? parser : null;
      }

      /// <summary>The assignments in the scripts that start at these addresses (and the scripts they call).</summary>
      public static Assignments Scan(IDataModel model, ScriptParser parser, IEnumerable<int> scripts) {
         if (parser == null) return None;
         var list = new List<(int, int)>();
         try {
            foreach (var spot in Flags.GetAllScriptSpots(model, parser, scripts, (byte)SetVarCommand)) {
               var variable = model.ReadMultiByteValue(spot.Address + 1, 2) - FirstVariable;
               if (variable < 0 || variable >= 16) continue;
               var sprite = model.ReadMultiByteValue(spot.Address + 3, 2);
               if (sprite >= 0x4000) continue; // the sprite comes from another variable: unknown
               list.Add((variable, sprite));
            }
         } catch (Exception) {
            // an odd script must never stop the map from being drawn
         }
         return list.Count == 0 ? None : new Assignments(list);
      }

      /// <summary>Every start address of a script of this map: the map scripts, and the scripts of its object, script and signpost events.</summary>
      public static IReadOnlyList<int> GetMapScripts(IDataModel model, ModelArrayElement map) {
         var result = new List<int>();
         void Add(int address) { if (address >= 0 && address < model.Count && !result.Contains(address)) result.Add(address); }
         if (map == null) return result;

         if (map.HasField("mapscripts")) {
            var start = map.GetAddress("mapscripts");
            for (int count = 0; start >= 0 && start < model.Count && model[start] != 0 && count < 20; count++, start += 5) {
               var type = model[start];
               var pointer = model.ReadPointer(start + 1);
               if (type is 2 or 4) {
                  // a list of (variable, value, script)
                  if (pointer < 0 || pointer >= model.Count) continue;
                  for (int child = 0; child < 100 && pointer + 8 <= model.Count && !model.ReadMultiByteValue(pointer, 2).IsAny(0, 0xFFFF); child++, pointer += 8) Add(model.ReadPointer(pointer + 4));
               } else {
                  Add(pointer);
               }
            }
         }

         var eventsTable = map.HasField("events") ? map.GetSubTable("events") : null;
         var events = eventsTable == null || eventsTable.Count == 0 ? null : eventsTable[0];
         if (events != null) {
            foreach (var (type, field) in new[] { ("objects", "script"), ("scripts", "script"), ("signposts", "arg") }) {
               var table = events.GetSubTable(type);
               if (table == null) continue;
               foreach (var element in table) {
                  if (element.HasField(field)) Add(element.GetAddress(field));
               }
            }
         }
         return result;
      }

      /// <summary>The graphics variable assignments in the scripts of one map. Remembered until the ROM data changes.</summary>
      public static Assignments InMap(IDataModel model, ScriptParser parser, ModelArrayElement map) {
         if (model == null || map == null) return None;
         parser ??= GetParser(model);
         if (parser == null) return None;
         return model.CurrentCacheScope.GetOrAdd("graphics-variable-sprites:map:" + map.Start, () => Scan(model, parser, GetMapScripts(model, map)));
      }

      private class Snapshot { public Assignments Result; }
      private static readonly ConditionalWeakTable<IDataModel, Snapshot> everywhere = new();

      /// <summary>
      /// The assignments in the scripts of every map of the game (the other scripts a map can use: shared scripts, other maps).
      /// Walking every script takes a moment, so it is done once per opened ROM, the first time an event needs it,
      /// and again after <see cref="Invalidate"/> (the editor calls it after it changes such an assignment itself).
      /// </summary>
      public static Assignments Everywhere(IDataModel model, ScriptParser parser = null, Func<IEnumerable<int>> scripts = null) {
         if (model == null) return None;
         Snapshot snapshot;
         lock (everywhere) {
            if (!everywhere.TryGetValue(model, out snapshot)) everywhere.Add(model, snapshot = new Snapshot());
         }
         if (snapshot.Result != null) return snapshot.Result;
         parser ??= GetParser(model);
         if (parser == null) return None; // not remembered: the parser may be registered later
         var result = Scan(model, parser, scripts?.Invoke() ?? Flags.GetAllTopLevelScripts(model));
         snapshot.Result = result;
         return result;
      }

      public static void Invalidate(IDataModel model) {
         if (model == null) return;
         lock (everywhere) everywhere.Remove(model);
      }
   }
}
