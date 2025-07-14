using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using System;
using System.Linq;
using System.Threading.Tasks;
using static HavenSoft.HexManiac.Core.Models.HardcodeTablesModel;

namespace HavenSoft.HexManiac.Core.ViewModels.QuickEditItems {
   public class MakeItemsExpandable : IQuickEditItem {
      public string Name => "Make Items Expandable";

      public string Description => "The initial games have functions that do out-of-bounds checks on item IDs using a hard-coded number of items." +
         Environment.NewLine + "This change will allow HexManiac to update those functions as you to expand the number of items in the game.";

      public string WikiLink => throw new NotImplementedException();

      public event EventHandler CanRunChanged;

      public static int GetPrimaryEditAddress(string gameCode) {
         if (gameCode == FireRed) return 0x09A8A4;
         if (gameCode == LeafGreen) return 0x09A878;
         if (gameCode == Ruby || gameCode == Sapphire) return 0x0A98BC;
         if (gameCode == Emerald) return 0xAD745C;
         return -1;
      }

      public void TabChanged() => CanRunChanged?.Invoke(this, EventArgs.Empty);
   }
}
