using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Runtime.CompilerServices;

namespace HavenSoft.HexManiac.Core.Models {
   /// <summary>Where the player appears after the new game speech (gCubeNewGameSpawn of CUBE). The truck is map 25-40 at -1,-1.</summary>
   public record CubeNewGameSpawn(int MapGroup, int MapNum, int X, int Y, bool ShakeTruck) {
      public const int TruckGroup = 25, TruckMap = 40;
      public static CubeNewGameSpawn Original { get; } = new(TruckGroup, TruckMap, -1, -1, true);
      public bool IsTruck => MapGroup == TruckGroup && MapNum == TruckMap;
   }

   /// <summary>
   /// The 16 byte table of CUBE that says where a new game starts: char magic[8]="CUBESPWN"; u8 mapGroup; u8 mapNum; s16 x; s16 y; u8 shakeTruck; u8 padding.
   /// Found by the anchor 'cube.newgame.spawn' first; a ROM whose metadata has no such anchor (an older .toml) is searched for the magic once, and the answer is kept.
   /// Nothing is ever written to the metadata here. ROMs without the table (vanilla, FireRed, an older CUBE) answer "not found" (-1) and every other member is tolerant.
   /// </summary>
   public static class CubeNewGameSpawnTable {
      public const string Anchor = "cube.newgame.spawn";
      public const string Magic = "CUBESPWN";
      public const int Length = 16;
      private const int MapGroupOffset = 8, MapNumOffset = 9, XOffset = 10, YOffset = 12, ShakeOffset = 14;

      private sealed class Box { public int Address; }
      private static readonly ConditionalWeakTable<IDataModel, Box> searched = new();

      /// <summary>The address of the table, or -1 if this ROM does not have it.</summary>
      public static int Find(IDataModel model) {
         if (model == null) return -1;
         try {
            var anchored = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, Anchor);
            if (anchored != Pointer.NULL && anchored >= 0 && HasMagic(model.RawData, anchored)) return anchored;
         } catch (Exception) {
            // an odd metadata file must never break the tab: fall through to the search
         }
         var box = searched.GetValue(model, key => new Box { Address = Search(key.RawData) });
         return box.Address != -1 && HasMagic(model.RawData, box.Address) ? box.Address : -1;
      }

      public static bool IsAvailable(IDataModel model) => Find(model) != -1;

      /// <summary>The first place in the data where the magic is followed by room for the rest of the table, or -1.</summary>
      public static int Search(byte[] data) {
         if (data == null) return -1;
         var first = (byte)Magic[0];
         for (int i = 0; i + Length <= data.Length; i++) {
            if (data[i] == first && HasMagic(data, i)) return i;
         }
         return -1;
      }

      private static bool HasMagic(byte[] data, int address) {
         if (address < 0 || address + Length > data.Length) return false;
         for (int i = 0; i < Magic.Length; i++) if (data[address + i] != Magic[i]) return false;
         return true;
      }

      /// <summary>The spawn the ROM has, or null if there is no table.</summary>
      public static CubeNewGameSpawn Read(IDataModel model) {
         var address = Find(model);
         if (address == -1) return null;
         var data = model.RawData;
         return new CubeNewGameSpawn(data[address + MapGroupOffset], data[address + MapNumOffset], (short)(data[address + XOffset] | (data[address + XOffset + 1] << 8)),
            (short)(data[address + YOffset] | (data[address + YOffset + 1] << 8)), data[address + ShakeOffset] != 0);
      }

      /// <summary>Writes the whole spawn (the caller closes the undo step). Returns false, changing nothing, if there is no table.</summary>
      public static bool Write(IDataModel model, ModelDelta token, CubeNewGameSpawn spawn) {
         var address = Find(model);
         if (address == -1) return false;
         if (spawn.MapGroup is < 0 or > 255 || spawn.MapNum is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(spawn), "The map group and number must fit in a byte.");
         if (spawn.X is < short.MinValue or > short.MaxValue || spawn.Y is < short.MinValue or > short.MaxValue) throw new ArgumentOutOfRangeException(nameof(spawn), "X and Y must fit in a signed 16 bit number.");
         void Put(int offset, int value) {
            if (model.RawData[address + offset] != (byte)value) token.ChangeData(model, address + offset, (byte)value);
         }
         Put(MapGroupOffset, spawn.MapGroup);
         Put(MapNumOffset, spawn.MapNum);
         Put(XOffset, spawn.X);
         Put(XOffset + 1, spawn.X >> 8);
         Put(YOffset, spawn.Y);
         Put(YOffset + 1, spawn.Y >> 8);
         Put(ShakeOffset, spawn.ShakeTruck ? 1 : 0);
         return true;
      }
   }
}
