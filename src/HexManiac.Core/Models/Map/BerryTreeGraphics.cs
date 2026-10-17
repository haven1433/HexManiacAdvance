using HavenSoft.HexManiac.Core.Models.Runs;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace HavenSoft.HexManiac.Core.Models.Map {
   /// <summary>
   /// Where a game keeps the pictures of its berry trees. The map editor needs two things for every berry:
   /// the table of animation frames (9 frames of 8 bytes: data pointer, size) and the list of palette slots (one byte per growth stage).
   ///
   /// pokeemerald-expansion (CUBE) lists both in the berry stats (the 'treePicTable' and 'treePaletteSlots' fields).
   /// The vanilla games (Ruby, Sapphire, Emerald) have no such fields: their berry stats only describe the berry itself.
   /// They keep two separate arrays of pointers, one entry per berry (gBerryTreePicTablePointers and gBerryTreePaletteSlotTablePointers),
   /// which no default metadata names, so they are found in the ROM by their shape (see <see cref="Locate"/>).
   /// </summary>
   public static class BerryTreeGraphics {
      /// <summary>Growth stages of the vanilla games: planted, sprouted, taller, flowering, berries.</summary>
      public const int VanillaStageCount = 5;

      /// <summary>The expansion has two more (a trunk and a budding stage that exist for save compatibility).</summary>
      public const int ExpansionStageCount = 7;

      /// <summary>The vanilla games have 43 berries, so both arrays have 43 pointers.</summary>
      public const int VanillaBerryCount = 43;

      /// <summary>Every berry has a table of 9 animation frames: planted (1), sprouted (2), the three tree stages (2 each).</summary>
      private const int FrameCount = 9;

      /// <summary>The arrays of the English Emerald (BPEE0), so that the most common game needs no search. Anything else is searched for.</summary>
      private static readonly IReadOnlyDictionary<string, (int pictures, int slots)> KnownLocations = new Dictionary<string, (int, int)> {
         [HardcodeTablesModel.Emerald] = (0x50C7B0, 0x50C85C),
      };

      private sealed class Locations {
         public bool Searched;
         public int Pictures = -1, Slots = -1;
      }

      private static readonly ConditionalWeakTable<IDataModel, Locations> locationCache = new();

      /// <summary>True if the berry stats list the tree tables themselves (pokeemerald-expansion), instead of the vanilla pointer arrays.</summary>
      public static bool UsesBerryStatsFields(IDataModel model) {
         var table = model.GetTableModel(HardcodeTablesModel.BerryTableName);
         return table != null && table.Count > 0 && table[0].HasField("treePicTable") && table[0].HasField("treePaletteSlots");
      }

      /// <summary>How many growth stages the game's tree tables describe (the number of palette slots every berry has).</summary>
      public static int StageCount(IDataModel model) => UsesBerryStatsFields(model) ? ExpansionStageCount : VanillaStageCount;

      /// <summary>
      /// Finds the table of animation frames and the palette slots of one berry's tree.
      /// berryIndex counts from the first berry of the berry stats (0 = Cheri in the vanilla games).
      /// Returns false if the game has no tree tables the editor understands (the object is then drawn from its own graphics).
      /// </summary>
      public static bool TryGetTables(IDataModel model, int berryIndex, out int pictureTable, out int paletteSlots) {
         pictureTable = paletteSlots = -1;
         if (berryIndex < 0) return false;
         var berries = model.GetTableModel(HardcodeTablesModel.BerryTableName);
         if (berries == null || berryIndex >= berries.Count) return false;

         if (berries[0].HasField("treePicTable") && berries[0].HasField("treePaletteSlots")) {
            var berry = berries[berryIndex];
            pictureTable = berry.GetAddress("treePicTable");
            paletteSlots = berry.GetAddress("treePaletteSlots");
            return pictureTable >= 0 && paletteSlots >= 0;
         }

         if (berryIndex >= VanillaBerryCount) return false;
         var locations = Locate(model);
         if (locations.Pictures < 0 || locations.Slots < 0) return false;
         var raw = model.RawData;
         pictureTable = ReadPointer(raw, locations.Pictures + berryIndex * 4);
         paletteSlots = ReadPointer(raw, locations.Slots + berryIndex * 4);
         return pictureTable >= 0 && paletteSlots >= 0;
      }

      /// <summary>
      /// The first berry whose tree uses the frame table at the given address, or -1.
      /// Only for games with the vanilla pointer arrays: the generic berry tree graphics (BERRY_TREE and its early/late stage variants)
      /// are drawn with one of those tables, which tells the map editor what such a graphic looks like.
      /// </summary>
      public static int FindBerryWithPictureTable(IDataModel model, int pictureTable) {
         if (pictureTable < 0 || UsesBerryStatsFields(model)) return -1;
         var berries = model.GetTableModel(HardcodeTablesModel.BerryTableName);
         if (berries == null) return -1;
         var locations = Locate(model);
         if (locations.Pictures < 0) return -1;
         var raw = model.RawData;
         for (int i = 0; i < Math.Min(VanillaBerryCount, berries.Count); i++) {
            if (ReadPointer(raw, locations.Pictures + i * 4) == pictureTable) return i;
         }
         return -1;
      }

      /// <summary>The place of the two vanilla arrays, found once per model (and found again if the data there stops matching).</summary>
      private static Locations Locate(IDataModel model) {
         var locations = locationCache.GetValue(model, _ => new Locations());
         lock (locations) {
            var raw = model.RawData;
            if (locations.Searched && locations.Pictures >= 0 && !(IsPictureArray(raw, locations.Pictures) && IsSlotArray(raw, locations.Slots))) {
               locations.Searched = false; // the tables moved: look again
            }
            if (!locations.Searched) {
               (locations.Pictures, locations.Slots) = Search(model, raw);
               locations.Searched = true;
            }
            return locations;
         }
      }

      private static (int pictures, int slots) Search(IDataModel model, byte[] raw) {
         if (KnownLocations.TryGetValue(model.GetGameCode(), out var known) && IsPictureArray(raw, known.pictures) && IsSlotArray(raw, known.slots)) {
            return known;
         }

         // the array of frame tables is easy to recognize: 43 pointers, every one to a table of 9 frames
         int arrayLength = VanillaBerryCount * 4;
         for (int address = 0; address + arrayLength <= raw.Length; address += 4) {
            if (raw[address + 3] != 0x08 && raw[address + 3] != 0x09) continue; // not a pointer into the ROM
            if (!IsPictureArray(raw, address)) continue;
            // the palette slots follow it in the vanilla games
            if (IsSlotArray(raw, address + arrayLength)) return (address, address + arrayLength);
            for (int slots = 0; slots + arrayLength <= raw.Length; slots += 4) {
               if (raw[slots + 3] != 0x08 && raw[slots + 3] != 0x09) continue;
               if (IsSlotArray(raw, slots)) return (address, slots);
            }
            return (-1, -1);
         }
         return (-1, -1);
      }

      /// <summary>43 pointers, every one to a table of animation frames.</summary>
      private static bool IsPictureArray(byte[] raw, int address) {
         if (address < 0 || address % 4 != 0 || address + VanillaBerryCount * 4 > raw.Length) return false;
         for (int i = 0; i < VanillaBerryCount; i++) {
            var table = ReadPointer(raw, address + i * 4);
            if (!IsPictureTable(raw, table)) return false;
         }
         return true;
      }

      /// <summary>
      /// 43 pointers, every one to a list of palette slots: hardware palette numbers, so under 16.
      /// The first two (the player and the player's reflection) are never used by a tree, which keeps tables of pointers to empty data from matching.
      /// </summary>
      private static bool IsSlotArray(byte[] raw, int address) {
         if (address < 0 || address % 4 != 0 || address + VanillaBerryCount * 4 > raw.Length) return false;
         for (int i = 0; i < VanillaBerryCount; i++) {
            var slots = ReadPointer(raw, address + i * 4);
            if (slots < 0 || slots + VanillaStageCount > raw.Length) return false;
            for (int stage = 0; stage < VanillaStageCount; stage++) if (raw[slots + stage] < 2 || raw[slots + stage] >= 16) return false;
         }
         return true;
      }

      /// <summary>9 frames of (pointer to the frame's tiles, size in bytes as a 16 bit value, 2 bytes of padding).</summary>
      private static bool IsPictureTable(byte[] raw, int address) {
         if (address < 0 || address % 4 != 0 || address + FrameCount * 8 > raw.Length) return false;
         for (int i = 0; i < FrameCount; i++) {
            if (ReadPointer(raw, address + i * 8) < 0) return false;
            var size = raw[address + i * 8 + 4] | raw[address + i * 8 + 5] << 8;
            if (size == 0 || size % 32 != 0 || size > 0x400) return false;
         }
         return true;
      }

      /// <summary>The ROM offset a pointer refers to, or -1 if the four bytes aren't a pointer into this ROM.</summary>
      private static int ReadPointer(byte[] raw, int address) {
         if (address < 0 || address + 4 > raw.Length) return -1;
         uint value = BitConverter.ToUInt32(raw, address);
         if (value < (uint)BaseModel.PointerOffset || value - (uint)BaseModel.PointerOffset >= (uint)raw.Length) return -1;
         return (int)(value - (uint)BaseModel.PointerOffset);
      }
   }
}
