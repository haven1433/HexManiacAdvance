using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.HexManiac.Core.Models {
   /// <summary>
   /// Which move a TM or HM teaches. Emerald and the games made from it keep one list of moves (TM01 to TM50, then HM01 to HM08: 'data.pokemon.moves.tms').
   /// The pokeemerald-expansion of CUBE has 100 TMs and keeps the list twice, as two arrays of 108 moves that the game reads at different places (the item's move, and the check of what a Pokémon can learn):
   /// 'data.pokemon.moves.tmhm' and 'data.pokemon.moves.tmhm.copy'. Changing the move of a TM means changing all of them, so that the game agrees with itself.
   /// The old metadata of CUBE has a different table under the name 'data.pokemon.moves.tms' (items with moves, 59 rows) that the game does not read: it is left alone.
   /// </summary>
   public static class TmMoveTables {
      public const string Primary = "data.pokemon.moves.tmhm", Copy = "data.pokemon.moves.tmhm.copy", Classic = HardcodeTablesModel.TmMoves;
      public const string MoveFormat = "[move:data.pokemon.moves.stats]";

      // the moves of the eight HMs of every game: Cut, Fly, Surf, Strength, Flash, Rock Smash, Waterfall, Dive
      private static readonly int[] HmMoves = { 15, 19, 57, 70, 148, 249, 127, 291 };

      /// <summary>The tables that hold the moves (one move per element), the one the game reads for the item first.</summary>
      public static IReadOnlyList<ITableRun> Find(IDataModel model) {
         var result = new List<ITableRun>();
         foreach (var name in new[] { Primary, Copy, Classic }) {
            var address = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, name);
            if (address == Pointer.NULL || !(model.GetNextRun(address) is ITableRun table) || table.Start != address) continue;
            if (table.ElementContent.Count != 1 || table.ElementContent[0].Name != "move" || table.ElementLength != 2) continue; // not the old table of items with moves
            result.Add(table);
         }
         return result;
      }

      /// <summary>The first item of the TMs ('TM01'), or -1 if the item table has no such name.</summary>
      public static int FirstTmItem(IDataModel model) {
         var items = model.GetTableModel(HardcodeTablesModel.ItemsTableName);
         if (items == null) return -1;
         for (int i = 1; i < items.Count; i++) {
            string name;
            try { name = items[i].GetStringValue("name"); } catch (Exception) { return -1; }
            if (string.Equals(name?.Trim(), "TM01", StringComparison.OrdinalIgnoreCase)) return i;
         }
         return -1;
      }

      /// <summary>The place of an item in the lists: TM01 is 0, TM50 is 49, and the HMs follow the last TM.</summary>
      public static int IndexOf(IDataModel model, int item) {
         var first = FirstTmItem(model);
         return first < 0 || item < first ? -1 : item - first;
      }

      /// <summary>
      /// Gives the two arrays of CUBE their names (and the format of moves) if they are not named yet. They are found by the HMs at their end: the list is as long as the items between TM01 and HM01 and the HMs have their usual moves.
      /// Returns how many arrays were named.
      /// </summary>
      public static int NameCubeArrays(IDataModel model) {
         var noChange = new NoDataChangeDeltaModel();
         if (Find(model).Any(table => table.ElementCount > 58)) return 0;
         var first = FirstTmItem(model);
         if (first < 0) return 0;
         var items = model.GetTableModel(HardcodeTablesModel.ItemsTableName);
         int hm = -1;
         for (int i = first; i < items.Count; i++) {
            var name = items[i].GetStringValue("name")?.Trim();
            if (string.Equals(name, "HM01", StringComparison.OrdinalIgnoreCase)) { hm = i; break; }
         }
         var tmCount = hm - first;
         if (tmCount < 51 || tmCount > 120) return 0; // vanilla and small hacks keep the one list that the old tables know
         var total = tmCount + HmMoves.Length;
         var data = model.RawData;
         var found = new List<int>();
         for (int at = tmCount * 2; at + HmMoves.Length * 2 <= data.Length; at += 2) {
            bool match = true;
            for (int i = 0; i < HmMoves.Length && match; i++) match = data[at + i * 2] == (HmMoves[i] & 0xFF) && data[at + i * 2 + 1] == (HmMoves[i] >> 8);
            if (!match) continue;
            var start = at - tmCount * 2;
            if (start < 0x200) continue;
            // the first TMs are real moves
            bool plausible = true;
            for (int i = 0; i < 20 && plausible; i++) { var move = data[start + i * 2] | (data[start + i * 2 + 1] << 8); plausible = move > 0 && move < 1500; }
            if (plausible) found.Add(start);
            if (found.Count > 4) return 0; // not a place that can be told from others
         }
         if (found.Count == 0) return 0;
         int named = 0;
         for (int i = 0; i < found.Count && i < 2; i++) {
            var existing = model.GetNextRun(found[i]);
            if (existing.Start == found[i] && existing is ITableRun) continue; // it already has a format (a named table)
            var name = i == 0 ? Primary : Copy;
            if (model.GetAddressFromAnchor(noChange, -1, name) != Pointer.NULL) continue;
            PokemonModel.ApplyAnchor(model, noChange, found[i], BaseRun.AnchorStart + name + MoveFormat + total, allowAnchorOverwrite: false);
            named++;
         }
         return named;
      }

      /// <summary>Writes a move into every list at the place of an item. False if there is no list that holds it.</summary>
      public static bool Write(IDataModel model, ModelDelta token, int item, int move) {
         var index = IndexOf(model, item);
         if (index < 0) return false;
         var wrote = false;
         foreach (var table in Find(model)) {
            if (index >= table.ElementCount) continue;
            model.WriteMultiByteValue(table.Start + index * 2, 2, token, move);
            wrote = true;
         }
         return wrote;
      }
   }
}
