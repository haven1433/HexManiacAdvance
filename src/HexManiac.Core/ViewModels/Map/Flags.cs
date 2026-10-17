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
   /// <param name="Address">Where the setberrytree command is.</param>
   /// <param name="BerryID">Index into the berry table (data.items.berry.stats).</param>
   /// <param name="Stage">The growth stage argument of the command (1 = planted ... 5 = berries).</param>
   public record BerrySpot(int Address, int BerryID, int Stage);

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

      // script commands that reference flags (setflag, clearflag, checkflag), trainer flags (trainerbattle), and variables
      private static readonly byte[] FlagCommands = { 0x29, 0x2A, 0x2B };
      private static readonly byte[] TrainerBattleCommands = { 0x5C };
      private static readonly byte[] VariableCommands = { 0x16, 0x17, 0x18, 0x19, 0x1A, 0x21, 0x22, 0x26 }; // setvar, addvar, subvar, copyvar, setorcopyvar, compare, comparevars, special2

      public record ScriptUsage(HashSet<int> ItemFlags, HashSet<int> TrainerFlags, HashSet<int> Variables);

      /// <summary>
      /// Finds every used flag, trainer flag, and variable with a single pass over every script in the game.
      /// This is equivalent to calling GetUsedItemFlags, GetUsedTrainerFlags, and GetUsedVariables,
      /// but walks the scripts only once instead of three times.
      /// </summary>
      public static ScriptUsage GetScriptUsage(IDataModel model, ScriptParser parser) {
         var flagSpots = new List<ScriptSpot>();
         var trainerSpots = new List<ScriptSpot>();
         var variableSpots = new List<ScriptSpot>();
         var filter = FlagCommands.Concat(TrainerBattleCommands).Concat(VariableCommands).ToArray();
         foreach (var spot in GetAllScriptSpots(model, parser, GetAllTopLevelScripts(model), filter)) {
            var command = model[spot.Address];
            if (FlagCommands.Contains(command)) flagSpots.Add(spot);
            else if (TrainerBattleCommands.Contains(command)) trainerSpots.Add(spot);
            else variableSpots.Add(spot);
         }
         return new(GetUsedItemFlags(model, flagSpots), GetUsedTrainerFlags(model, trainerSpots), GetUsedVariables(model, variableSpots));
      }

      /// <summary>
      /// IDs of every used flag
      /// </summary>
      public static HashSet<int> GetUsedItemFlags(IDataModel model, ScriptParser parser) {
         return GetUsedItemFlags(model, GetAllScriptSpots(model, parser, GetAllTopLevelScripts(model), FlagCommands));
      }

      public const string FlagListName = "scriptflagaliases", VariableListName = "scriptvariablealiases";

      /// <summary>
      /// Scripts aren't the only thing that uses flags and variables: the game's own code does too (badges, system flags, story progress...).
      /// Every flag/variable the ROM's constants give a real name to is treated as spoken for. Names that say UNUSED, and the
      /// CUSTOM_ pool that CUBE adds for hackers, are the ones that are free to hand out.
      /// </summary>
      public static HashSet<int> GetReservedFlags(IDataModel model) => GetReserved(model, FlagListName);
      public static HashSet<int> GetReservedVariables(IDataModel model) => GetReserved(model, VariableListName);

      private static HashSet<int> GetReserved(IDataModel model, string listName) {
         var result = new HashSet<int>();
         if (!model.TryGetList(listName, out var list)) return result;
         for (int i = 0; i < list.Count; i++) {
            var name = list[i];
            if (string.IsNullOrEmpty(name) || name == i.ToString()) continue;
            if (IsFreeName(name)) continue;
            result.Add(i);
         }
         return result;
      }

      public static bool IsFreeName(string name) => name.Contains("UNUSED", StringComparison.OrdinalIgnoreCase) || name.Contains("CUSTOM", StringComparison.OrdinalIgnoreCase);

      /// <summary>The first flag of the spare pool (FLAG_CUSTOM_000 in CUBE), or -1 if the ROM doesn't have one.</summary>
      public static int CustomFlagStart(IDataModel model) => FirstWithName(model, FlagListName, "_CUSTOM_");
      /// <summary>The first variable of the spare pool (VAR_CUSTOM_000 in CUBE), or -1 if the ROM doesn't have one.</summary>
      public static int CustomVariableStart(IDataModel model) => FirstWithName(model, VariableListName, "_CUSTOM_");

      /// <summary>
      /// The first flag that isn't in 'used' (scripts, map events and the game's named flags) and whose name, if it has one, says it's free.
      /// ROMs with a spare pool (CUBE's FLAG_CUSTOM_000...) hand out pool flags first.
      /// </summary>
      public static int NextFreeFlag(IDataModel model, ISet<int> used) => NextFree(model, used, FlagListName, CustomFlagStart(model), 0x21, 0xFFFF);
      public static int NextFreeVariable(IDataModel model, ISet<int> used) => NextFree(model, used, VariableListName, CustomVariableStart(model), 0x4034, 0x7FFF);

      private static int NextFree(IDataModel model, ISet<int> used, string listName, int poolStart, int classicStart, int limit) {
         model.TryGetList(listName, out var list);
         bool IsFree(int value) => !used.Contains(value) && (list == null || value >= list.Count || string.IsNullOrEmpty(list[value]) || list[value] == value.ToString() || IsFreeName(list[value]));
         var start = poolStart >= 0 ? poolStart : classicStart;
         for (int value = start; value <= limit; value++) if (IsFree(value)) return value;
         // nothing free after the start (the pool is full): take the first unused one anywhere, ignoring names
         for (int value = classicStart; value <= limit; value++) if (!used.Contains(value)) return value;
         return classicStart;
      }

      private static int FirstWithName(IDataModel model, string listName, string marker) {
         if (!model.TryGetList(listName, out var list)) return -1;
         for (int i = 0; i < list.Count; i++) {
            if (list[i] != null && list[i].Contains(marker, StringComparison.OrdinalIgnoreCase)) return i;
         }
         return -1;
      }

      /// <param name="flagSpots">Every spot in every script that uses a flag command (setflag, clearflag, checkflag).</param>
      private static HashSet<int> GetUsedItemFlags(IDataModel model, IEnumerable<ScriptSpot> flagSpots) {
         var usedFlags = GetReservedFlags(model);

         var hiddenItemFlagStart = 600;
         if (model.IsEmerald()) {
            for (int i = 0x15C; i <= 0x1A9; i++) usedFlags.Add(i);        // Emerald Match Call flags
            hiddenItemFlagStart = 500;
         } else if (model.IsFRLG()) {
            hiddenItemFlagStart = 1000;
         }

         // object flags
         foreach (var element in GetAllEvents(model, "objects")) {
            if (!element.HasField("flag")) continue;
            usedFlags.Add(element.GetValue("flag"));
         }

         // hidden item flags
         foreach (var element in GetAllEvents(model, "signposts")) {
            var signpost = new SignpostEventModel(element);
            if (!signpost.IsHiddenItem) continue;
            usedFlags.Add(hiddenItemFlagStart + signpost.HiddenItemFlag);
         }

         foreach (var spot in flagSpots) {
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
         return GetUsedVariables(model, GetAllScriptSpots(model, parser, GetAllTopLevelScripts(model), VariableCommands));
      }

      /// <param name="variableSpots">Every spot in every script that uses a variable command (setvar, addvar, subvar, copyvar, setorcopyvar, compare, comparevars, special2).</param>
      private static HashSet<int> GetUsedVariables(IDataModel model, IEnumerable<ScriptSpot> variableSpots) {
         var usedVariables = GetReservedVariables(model);
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

         foreach (var spot in variableSpots) {
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
         var offset = BerryIdOffset(model);
         foreach (var spot in GetAllScriptSpots(model, parser, new[] { start }, 0x8A)) {
            var plantID = model[spot.Address + 1];
            var berryID = model[spot.Address + 2];
            results[plantID] = new(spot.Address, berryID - offset, model[spot.Address + 3]);
         }
         return results;
      }

      /// <summary>
      /// setberrytree's berry argument is 1-based. The vanilla berry table starts at Cheri (so subtract 1),
      /// but decomp hacks such as pokeemerald-expansion keep an empty entry 0 so the table is indexed by the raw id.
      /// </summary>
      public static int BerryIdOffset(IDataModel model) {
         var berries = model.GetTableModel(HardcodeTablesModel.BerryTableName);
         if (berries == null || berries.Count < 2) return 1;
         var first = berries[0].GetStringValue("name") ?? string.Empty;
         var second = berries[1].GetStringValue("name") ?? string.Empty;
         if (second.Trim().Equals("Cheri", StringComparison.OrdinalIgnoreCase) && !first.Trim().Equals("Cheri", StringComparison.OrdinalIgnoreCase)) return 0;
         if (string.IsNullOrWhiteSpace(first) || first.Trim().All(c => c == '?')) return 0;
         return 1;
      }

      /// <summary>
      /// True for pokeemerald-expansion ROMs. They differ from the vanilla games in ways the event templates care about:
      /// trainers have a 52-byte record and a unified trainerbattle, move tutors are given a move id (not a tutor-table index),
      /// and the in-game trade specials read the trade number from script variable 0x8005.
      /// </summary>
      public static bool IsExpansion(IDataModel model) => IsUnifiedTrainerBattle(model);

      /// <summary>True for ROMs (pokeemerald-expansion 1.17+) whose trainerbattle command is the unified 41-byte form.</summary>
      public static bool IsUnifiedTrainerBattle(IDataModel model) {
         var trainers = model.GetTableModel(HardcodeTablesModel.TrainerTableName, null);
         return trainers != null && TrainerLayout.For(trainers.Run) != TrainerLayout.Vanilla;
      }

      public static ISet<int> GetUsedTrainerFlags(IDataModel model, ScriptParser parser) {
         return GetUsedTrainerFlags(model, GetAllScriptSpots(model, parser, GetAllTopLevelScripts(model), TrainerBattleCommands));
      }

      /// <param name="trainerBattleSpots">Every spot in every script that uses the trainerbattle command.</param>
      private static HashSet<int> GetUsedTrainerFlags(IDataModel model, IEnumerable<ScriptSpot> trainerBattleSpots) {
         var trainerFlags = new HashSet<int>();

         // check all scripts
         var expansionLayout = IsUnifiedTrainerBattle(model);
         foreach (var spot in trainerBattleSpots) {
            if (expansionLayout) {
               // 5C flags localIdA trainerA: ... localIdB trainerB: ... (trainer B only matters for two-trainer battles)
               foreach (var id in new[] { model.ReadMultiByteValue(spot.Address + 3, 2), model.ReadMultiByteValue(spot.Address + 18, 2) }) if (id > 0) trainerFlags.Add(id);
            } else {
               trainerFlags.Add(model.ReadMultiByteValue(spot.Address + 2, 2));
            }
         }

         // check the Ruby/Sapphire/Emerald rematch table: base trainer and its rematches (base:data.trainers.stats rematch1:... rematch4:...)
         var rematchesRSE = model.GetTableModel(HardcodeTablesModel.RematchTableRSE);
         if (rematchesRSE != null) {
            foreach (var rematch in rematchesRSE) {
               if (!rematch.TryGetValue("base", out var baseTrainer) || !trainerFlags.Contains(baseTrainer)) continue;
               for (int i = 1; i <= 4; i++) if (rematch.TryGetValue("rematch" + i, out var later) && later > 0) trainerFlags.Add(later);
            }
         }

         // check rematch table
         var rematches = model.GetTableModel(HardcodeTablesModel.RematchTable);
         if (rematches != null) {
            foreach (var rematch in rematches) {
               if (!trainerFlags.Contains(rematch.GetValue("match1"))) continue;
               foreach (var match in new[] { "match2", "match3", "match4", "match5", "match6" }.Select(rematch.GetValue)) trainerFlags.Add(match);
            }
         }

         return trainerFlags;
      }

      #region Derived Pokemon Data

      public static IReadOnlyDictionary<int, int> GetMinimumLevelForPokemon(IDataModel model) {
         var evolutions = model.GetTableModel(HardcodeTablesModel.EvolutionTableName);
         if (evolutions == null) return null; // callers treat null as "no data" (some hacks don't have a recognizable evolution table)
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

      /// <summary>
      /// For a given pokemon, find what pokemon (if any evolves into it)
      /// </summary>
      public static IReadOnlyDictionary<int, int> GetPokemonDevolutions(IDataModel model) {
         var evolutions = model.GetTableModel(HardcodeTablesModel.EvolutionTableName);
         if (evolutions == null) return null;
         var levelMethods = new[] { 4, 8, 9, 10, 11, 12, 13, 14 };
         var results = new Dictionary<int, int>();
         foreach (var evo in evolutions) {
            // method1:evolutionmethods arg1:|s=method1(6=data.items.stats|7=data.items.stats) species1:data.pokemon.names unused1:
            for (int i = 0; i < evo.Length; i += 8) {
               var species = model.ReadMultiByteValue(evo.Start + i + 4, 2);
               results[species] = evo.ArrayIndex;
            }
         }
         return results;
      }

      public static IReadOnlyDictionary<int, IReadOnlyList<int>> GetPokemonEggMoves(IDataModel model) {
         var start = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, HardcodeTablesModel.EggMovesTableName);
         if (!start.InRange(0, model.Count)) return null;
         if (model.GetNextRun(start) is not EggMoveRun eggRun) return null;
         var table = new EggTable(model, null, eggRun);
         int currentPokemon = -1;
         var results = new AutoDictionary<int, List<int>>(i => new());
         foreach (var element in table) {
            var value = model.ReadMultiByteValue(element.Address, 2);
            if (element.is_pokemon) {
               value -= EggMoveRun.MagicNumber;
               currentPokemon = value;
            } else {
               results[currentPokemon].Add(value);
            }
         }
         return new Dictionary<int, IReadOnlyList<int>>(results.Select(kvp => new KeyValuePair<int, IReadOnlyList<int>>(kvp.Key, kvp.Value)));
      }

      public static IReadOnlyDictionary<int, IReadOnlyList<int>> GetPokemonLevelupMoves(IDataModel model) {
         var start = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, HardcodeTablesModel.LevelMovesTableName);
         if (!start.InRange(0, model.Count)) return null;
         if (model.GetNextRun(start) is not ArrayRun array) return null;
         var table = new ModelTable(model, array);
         var results = new AutoDictionary<int, List<int>>(i => new());
         foreach (var element in table) {
            var sub = element.GetSubTable("movesFromLevel");
            foreach (var move in sub) {
               if (sub.Run.ElementContent.Count == 1) {
                  results[element.ArrayIndex].Add(model.ReadMultiByteValue(move.Start, 2) & 0x1FF);
               } else if (move.TryGetValue("move", out int moveIndex)) {
                  results[element.ArrayIndex].Add(moveIndex);
               }
            }
         }
         return new Dictionary<int, IReadOnlyList<int>>(results.Select(kvp => new KeyValuePair<int, IReadOnlyList<int>>(kvp.Key, kvp.Value)));
      }

      public static IReadOnlyDictionary<int, IReadOnlyList<int>> GetPokemonTutorMoves(IDataModel model) {
         var start = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, HardcodeTablesModel.TutorCompatibility);
         if (!start.InRange(0, model.Count)) return null;
         if (model.GetNextRun(start) is not ArrayRun array) return null;
         var table = new ModelTable(model, array);
         var options = model.GetTableModel(HardcodeTablesModel.MoveTutors);
         if (options == null) return null;
         var results = new AutoDictionary<int, List<int>>(i => new());
         foreach (var element in table) {
            if (!element.HasField("moves")) break;
            var move = element.GetTuple("moves");
            for (int i = 0; i < move.FieldCount; i++) {
               if ((model[element.Start + i / 8] & (1 << i)) == 0) continue;
               results[element.ArrayIndex].Add(options[i].GetValue(0));
            }
         }
         return new Dictionary<int, IReadOnlyList<int>>(results.Select(kvp => new KeyValuePair<int, IReadOnlyList<int>>(kvp.Key, kvp.Value)));
      }

      public static IReadOnlyDictionary<int, IReadOnlyList<int>> GetPokemonTmMoves(IDataModel model) {
         var start = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, HardcodeTablesModel.TmCompatibility);
         if (!start.InRange(0, model.Count)) return null;
         if (model.GetNextRun(start) is not ArrayRun array) return null;
         var table = new ModelTable(model, array);
         var options = model.GetTableModel(HardcodeTablesModel.TmMoves);
         if (options == null) return null;
         var results = new AutoDictionary<int, List<int>>(i => new());
         foreach (var element in table) {
            if (!element.HasField("moves")) break;
            var move = element.GetTuple("moves");
            for (int i = 0; i < move.FieldCount; i++) {
               if ((model[element.Start + i / 8] & (1 << i)) == 0) continue;
               results[element.ArrayIndex].Add(options[i].GetValue(0));
            }
         }
         return new Dictionary<int, IReadOnlyList<int>>(results.Select(kvp => new KeyValuePair<int, IReadOnlyList<int>>(kvp.Key, kvp.Value)));
      }

      #endregion

      public static ISet<int> GetTrainerFlagUsages(IDataModel model, ScriptParser parser, int flag) {
         var flagUsages = new HashSet<int>();
         var trainerOffset = IsUnifiedTrainerBattle(model) ? 3 : 2; // the unified command has a flags byte and an object id before the trainer
         foreach (var spot in GetAllScriptSpots(model, parser, GetAllTopLevelScripts(model), 0x5C)) {
            var trainerFlag = model.ReadMultiByteValue(spot.Address + trainerOffset, 2);
            if (trainerFlag == flag) flagUsages.Add(spot.Address + trainerOffset);
         }
         // TODO need to check the rematch table
         return flagUsages;
      }

      public static IReadOnlyDictionary<int, TrainerPreference> GetTrainerPreference(IDataModel model, ScriptParser parser) {
         var trainers = model.GetTableModel(HardcodeTablesModel.TrainerTableName);
         var trainerOffset = IsUnifiedTrainerBattle(model) ? 3 : 2;

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
               var trainerFlag = model.ReadMultiByteValue(spot.Address + trainerOffset, 2);
               if (trainerFlag <= 0 || trainerFlag >= trainers.Count) continue;
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
            var scriptsSeen = new HashSet<int> { scriptStart }; // same content as scriptsToCheck, but with a fast Contains
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
                        if (destination >= 0 && destination < model.Count && scriptsToCheck.Count < ScriptCountLimit && scriptsSeen.Add(destination)) scriptsToCheck.Add(destination);
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

      /// <summary>
      /// Finds the start address of every script referenced by a map (object, script, and signpost events, plus map header scripts).
      /// Walking every map is slow, so the result is cached until the model's data changes.
      /// The returned set is shared: callers must not modify it.
      /// </summary>
      public static ISet<int> GetAllTopLevelScripts(IDataModel model) {
         return model.CurrentCacheScope.GetOrAdd("flags.top-level-scripts", () => FindAllTopLevelScripts(model));
      }

      private static HashSet<int> FindAllTopLevelScripts(IDataModel model) {
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

      /// <summary>
      /// Collects the events of the requested type from every map in the game.
      /// Walking every map is slow (and this gets called from several different scans), so the result is cached until the model's data changes.
      /// </summary>
      /// <param name="type">Expects "objects" "scripts" or "signposts"</param>
      /// <returns>A read-only list of every event of that type.</returns>
      public static IList<ModelArrayElement> GetAllEvents(IDataModel model, string type) {
         return model.CurrentCacheScope.GetOrAdd("flags.all-events:" + type, () => FindAllEvents(model, type).AsReadOnly());
      }

      private static List<ModelArrayElement> FindAllEvents(IDataModel model, string type) {
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
