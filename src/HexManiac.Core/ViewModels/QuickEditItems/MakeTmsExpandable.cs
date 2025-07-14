using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static HavenSoft.HexManiac.Core.Models.HardcodeTablesModel;

namespace HavenSoft.HexManiac.Core.ViewModels.QuickEditItems {
   // TODO for Emerald, update CanSpeciesLearnTmHm also
   /// <summary>
   /// The default implementation of converting a TM item ID to a TM move ID is to use an item offset.
   /// This means that all the TM and HM moves are stored in a single list, and all the TM/HM items must be sequential.
   ///
   /// MakingTmsExpandable will change the implementation to be based on item names instead.
   /// So 'TM30' will use the 30th move in the tmmove table, no matter which item number it is.
   /// To make this work for arbitrary numbers of TMs and HMs, HM moves/compatibility are split out into a separate table.
   /// This requires updating 2 tables and 6 functions.
   /// </summary>
   public class MakeTmsExpandable : IQuickEditItem {
      public const string HmCompatibility = "hmcompatibility";

      #region FunctionLocations

      // helper function used by Special0x196
      private readonly Dictionary<string, int> SetText = new Dictionary<string, int> {
         [FireRed] = 0x008D84,
      };

      // helper function used by CanPokemonLearnTmOrHmMove
      private readonly Dictionary<string, int> ReadPokeData = new Dictionary<string, int> {
         [FireRed] = 0x03FBE8,
         [LeafGreen] = 0x03FBE8,
         [Ruby] = 0x03CB60,
         [Sapphire] = 0x03CB60,
         [Emerald] = 0x06A518,
      };

      // magic strings needed for doing menu text buffering
      private readonly Dictionary<string, int[]> MagicBufferStrings = new Dictionary<string, int[]> {
         [FireRed] = new[] { 0x4166FF, 0x463178, 0x416226, 0x46317C, 0x416703 },
      };

      // 0x58
      private readonly Dictionary<string, int> CanPokemonLearnTmOrHmMove = new Dictionary<string, int> {
         [FireRed] = 0x043C2C,
         [LeafGreen] = 0x043C2C,
         [Ruby] = 0x040374,
         [Sapphire] = 0x040374,
         [Emerald] = 0x06E00C,
      };

      // 0x3C
      private readonly Dictionary<string, int> IsMoveHmMove2 = new Dictionary<string, int> {
         [FireRed] = 0x0441B8,
         [Emerald] = 0x06E804,
      };

      // 0x4C
      private readonly Dictionary<string, int> Special0x196 = new Dictionary<string, int> {
         [FireRed] = 0x0CC8CC,
         [Emerald] = 0x1398C0,
      };

      // 0x18
      private readonly Dictionary<string, int> GetBattleMoveFromItemNumber = new Dictionary<string, int> {
         [FireRed] = 0x125A78,
         [Emerald] = 0x1B6CFC,
      };

      // 0x30
      private readonly Dictionary<string, int> IsMoveHmMove1 = new Dictionary<string, int> {
         [FireRed] = 0x125A90,
         [Emerald] = 0x1B6D14,
      };

      // 0xD0
      private readonly Dictionary<string, int> BufferTmHmNameForMenu = new Dictionary<string, int> {
         [FireRed] = 0x131D48,
         // [Emerald] = null,
      };

      // newly created functions
      private readonly Dictionary<string, int> ConvertItemPointerToTmHmBattleMoveId, IsItemTmHm, ParseNumber, ReadBitArray, IsItemTmHm2;

      #endregion

      public string Name => "Make TMs Expandable";

      public string Description => "The initial games are limited to have no more than 64 TMs+HMs." +
         Environment.NewLine + "This change will allow you to freely add new TMs, up to 256." +
         Environment.NewLine + "It will also split TMs and HMs into separate lists, making them easier to manage." +
         Environment.NewLine + "After this change, TM moves are based on the name given to the TM/HM instead of the item index." +
         Environment.NewLine + "For example, an item named 'TM30' will use the 30th move in the 'data.pokemon.moves.tms' table.";

      public string WikiLink => throw new NotImplementedException();

      public event EventHandler CanRunChanged;

      public MakeTmsExpandable() {
         ConvertItemPointerToTmHmBattleMoveId = new Dictionary<string, int>();
         IsItemTmHm = new Dictionary<string, int>();
         ParseNumber = new Dictionary<string, int>();
         ReadBitArray = new Dictionary<string, int>();
         IsItemTmHm2 = new Dictionary<string, int>();

         // ReadBitArray fits after IsMoveHmMove2
         foreach (var pair in IsMoveHmMove2) ReadBitArray.Add(pair.Key, pair.Value + 0x24);

         // ParseNumber goes after Special196
         foreach (var pair in Special0x196) ParseNumber.Add(pair.Key, pair.Value + 0x30);

         // IsItemTmHm goes after IsMoveHmMove1
         foreach (var pair in IsMoveHmMove1) IsItemTmHm.Add(pair.Key, pair.Value + 0x8);

         // ConvertItemPointerToTmHmBattleMoveId goes after BufferTmHmNameForMenu
         foreach (var pair in BufferTmHmNameForMenu) ConvertItemPointerToTmHmBattleMoveId.Add(pair.Key, pair.Value + 0x6C);

         // IsItemTmHm2 goes after ConvertItemPointerToTmHmBattleMoveId
         foreach (var pair in ConvertItemPointerToTmHmBattleMoveId) IsItemTmHm2.Add(pair.Key, pair.Value + 0x24);
      }

      public void TabChanged() => CanRunChanged?.Invoke(this, EventArgs.Empty);
   }
}
