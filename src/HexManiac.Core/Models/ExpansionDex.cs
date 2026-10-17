using HavenSoft.HexManiac.Core.Models.Runs;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.HexManiac.Core.Models {
   /// <summary>
   /// The pokedex order of a pokeemerald-expansion ROM (CUBE), which keeps it in different places than the vanilla games:
   /// * the national dex number of every species is the 'dexNumber' field of the species table (gSpeciesInfo[].natDexNum).
   ///   All the forms of a species share their number, and the pokedex shows the species whose number is N in slot N.
   /// * the regional (Hoenn) dex is 'data.pokedex.hoennToNational' (sHoennToNationalOrder): the national number in each Hoenn slot.
   ///   Its entries are 4 bytes (enum NationalDexOrder); tomls written before this knew it as '[national:]428', 2-byte halves, which is read the same way.
   /// * the search orders 'data.pokedex.search.alpha/weight/size' (gPokedexOrder_*) are lists of national numbers.
   /// The dex entries themselves (height, weight, description) belong to the species, so they follow it with no copying.
   /// </summary>
   public static class ExpansionDex {
      public static readonly IReadOnlyList<string> NationalFieldNames = new[] { "dexNumber", "natDexNum", "nationalDexNum" };
      public static readonly IReadOnlyList<string> SearchTables = new[] { "data.pokedex.search.alpha", "data.pokedex.search.weight", "data.pokedex.search.size" };

      /// <summary>True when the species table carries the national dex number itself (the expansion layout) and there is no vanilla dex order table.</summary>
      public static bool IsExpansionLayout(IDataModel model) {
         if (model.GetTable(HardcodeTablesModel.NationalDexTableName) != null) return false;
         return NationalField(model) != null;
      }

      /// <summary>Null when the expansion dex can be reordered, else why not (for the message the user sees).</summary>
      public static string WhyUnavailable(IDataModel model) {
         if (!IsExpansionLayout(model)) return "This ROM's pokedex tables were not found.";
         var hoenn = HoennTable(model);
         if (hoenn.count == 0) return $"The regional dex table '{HardcodeTablesModel.ConversionDexTableName}' (sHoennToNationalOrder) is missing from this ROM's metadata, so the pokedex can't be reordered safely. Add the anchor to the .toml (or load a newer CUBE .toml) to use this.";
         if (NationalCount(model) < 1) return "No species in this ROM has a national dex number.";
         return null;
      }

      public static ArrayRunElementSegment NationalField(IDataModel model) {
         var stats = model.GetTable(HardcodeTablesModel.PokemonStatsTable);
         if (stats == null) return null;
         foreach (var name in NationalFieldNames) {
            var seg = stats.ElementContent.FirstOrDefault(segment => segment.Name == name);
            if (seg != null && seg.Type == ElementContentType.Integer && seg.Length == 2) return seg;
         }
         return null;
      }

      private static int NationalFieldAddress(ITableRun stats, ArrayRunElementSegment field, int species) {
         var offset = 0;
         foreach (var seg in stats.ElementContent) {
            if (seg == field) break;
            offset += seg.Length;
         }
         return stats.Start + stats.ElementLength * species + offset;
      }

      /// <summary>The national dex number of every species (index 0 is SPECIES_NONE).</summary>
      public static int[] ReadNationalNumbers(IDataModel model) {
         var stats = model.GetTable(HardcodeTablesModel.PokemonStatsTable);
         var field = NationalField(model);
         if (stats == null || field == null) return new int[0];
         var result = new int[stats.ElementCount];
         for (int i = 0; i < result.Length; i++) result[i] = model.ReadMultiByteValue(NationalFieldAddress(stats, field, i), 2);
         return result;
      }

      /// <summary>How many slots the national dex has: the highest national number of any species.</summary>
      public static int NationalCount(IDataModel model) {
         var numbers = ReadNationalNumbers(model);
         return numbers.Length < 2 ? 0 : numbers.Skip(1).Max();
      }

      /// <summary>For each national number, the species that have it, lowest species id first (the base form comes first in gSpeciesInfo).</summary>
      public static Dictionary<int, List<int>> SpeciesByNational(IDataModel model) {
         var numbers = ReadNationalNumbers(model);
         var result = new Dictionary<int, List<int>>();
         for (int species = 1; species < numbers.Length; species++) {
            var national = numbers[species];
            if (national == 0) continue;
            if (!result.TryGetValue(national, out var list)) result[national] = list = new List<int>();
            list.Add(species);
         }
         return result;
      }

      /// <summary>The species the game shows for a national number (NationalPokedexNumToSpecies: the first species with that number), or 0.</summary>
      public static int SpeciesForNational(IReadOnlyDictionary<int, List<int>> speciesByNational, int national) {
         return speciesByNational.TryGetValue(national, out var list) && list.Count > 0 ? list[0] : 0;
      }

      /// <summary>Where the regional table is and how wide its entries are. A 2-byte table whose every second half is 0 holds 4-byte entries.</summary>
      private static (int start, int width, int count) HoennTable(IDataModel model) {
         var table = model.GetTable(HardcodeTablesModel.ConversionDexTableName);
         if (table == null || table.ElementCount == 0) return (-1, 0, 0);
         if (table.ElementLength == 4) return (table.Start, 4, table.ElementCount);
         if (table.ElementLength != 2) return (-1, 0, 0);
         var count = table.ElementCount;
         if (count % 2 == 0) {
            var wide = true;
            for (int i = 1; i < count && wide; i += 2) wide = model.ReadMultiByteValue(table.Start + i * 2, 2) == 0;
            if (wide) return (table.Start, 4, count / 2);
         }
         return (table.Start, 2, count);
      }

      /// <summary>The national number in each regional (Hoenn) slot, slot 1 first.</summary>
      public static int[] ReadRegional(IDataModel model) {
         var (start, width, count) = HoennTable(model);
         var result = new int[count];
         for (int i = 0; i < count; i++) result[i] = model.ReadMultiByteValue(start + i * width, width);
         return result;
      }

      public static void WriteRegional(IDataModel model, ModelDelta token, IReadOnlyList<int> nationals) {
         var (start, width, count) = HoennTable(model);
         if (nationals.Count != count) throw new ArgumentException($"The regional dex has {count} slots, not {nationals.Count}.");
         for (int i = 0; i < count; i++) {
            if (model.ReadMultiByteValue(start + i * width, width) == nationals[i]) continue;
            model.WriteMultiByteValue(start + i * width, width, token, nationals[i]);
         }
      }

      /// <summary>
      /// Renumbers the national dex. newOrder[slot - 1] is the old national number that should now be in that slot (0 for an empty slot).
      /// Every species (with all its forms) gets its new number, and the regional dex and search orders, which store national numbers, follow.
      /// </summary>
      public static void ApplyNationalOrder(IDataModel model, ModelDelta token, IReadOnlyList<int> newOrder) {
         var renumber = new Dictionary<int, int>();
         for (int slot = 1; slot <= newOrder.Count; slot++) {
            var old = newOrder[slot - 1];
            if (old == 0) continue;
            if (renumber.ContainsKey(old)) throw new ArgumentException($"National dex number {old} is in the new order twice.");
            renumber[old] = slot;
         }
         if (renumber.All(pair => pair.Key == pair.Value)) return;
         int Map(int old) => renumber.TryGetValue(old, out var value) ? value : old;

         // species
         var stats = model.GetTable(HardcodeTablesModel.PokemonStatsTable);
         var field = NationalField(model);
         for (int species = 1; species < stats.ElementCount; species++) {
            var address = NationalFieldAddress(stats, field, species);
            var old = model.ReadMultiByteValue(address, 2);
            var updated = Map(old);
            if (updated != old) model.WriteMultiByteValue(address, 2, token, updated);
         }

         // regional dex
         var regional = ReadRegional(model);
         if (regional.Length > 0) WriteRegional(model, token, regional.Select(Map).ToList());

         // search orders: lists of national numbers, sorted by the species' name / weight / height, which didn't change
         foreach (var name in SearchTables) {
            var table = model.GetTable(name);
            if (table == null || table.ElementLength != 2 || table.ElementContent.Count != 1) continue;
            for (int i = 0; i < table.ElementCount; i++) {
               var address = table.Start + i * 2;
               var old = model.ReadMultiByteValue(address, 2);
               var updated = Map(old);
               if (updated != old) model.WriteMultiByteValue(address, 2, token, updated);
            }
         }
      }
   }
}
