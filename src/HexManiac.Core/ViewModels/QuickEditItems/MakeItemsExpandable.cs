using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using System;
using System.Linq;

using static HavenSoft.HexManiac.Core.Models.AutoSearchModel;

namespace HavenSoft.HexManiac.Core.ViewModels.QuickEditItems {
   public class MakeItemsExpandable : IQuickEditItem {
      public string Name => "Make Items Expandable";

      public string Description => "The initial games have functions that do out-of-bounds checks on item IDs using a hard-coded number of items." +
         Environment.NewLine + "This change will allow HexManiac to update those functions as you to expand the number of items in the game.";

      public event EventHandler CanRunChanged;

      public static int GetPrimaryEditAddress(string gameCode) {
         if (gameCode == FireRed) return 0x09A8A4;
         if (gameCode == LeafGreen) return 0x09A878;
         if (gameCode == Ruby || gameCode == Sapphire) return 0x0A98BC;
         if (gameCode == Emerald) return 0xAD745C;
         return -1;
      }

      public bool CanRun(IViewPort viewPortInterface) {
         var viewPort = viewPortInterface as ViewPort;
         if (viewPort == null) return false;
         var model = viewPortInterface.Model;
         var gameCode = new string(Enumerable.Range(0xAC, 4).Select(i => ((char)model[i])).ToArray());

         var start = GetPrimaryEditAddress(gameCode);
         if (start == -1) return false;
         var run = model.GetNextRun(start);
         return !(run is WordRun);
      }

      public ErrorInfo Run(IViewPort viewPortInterface) {
         var viewPort = (ViewPort)viewPortInterface;
         var model = viewPortInterface.Model;
         var gameCode = new string(Enumerable.Range(0xAC, 4).Select(i => ((char)model[i])).ToArray());
         var start = GetPrimaryEditAddress(gameCode);

         // IsItemIDValid(itemID)
         viewPort.Edit($"@{start:X6} 00 B5 00 04 00 0C 03 49 08 45 00 DB 00 20 02 BC 08 47 00 00 ::items ");

         if (gameCode == FireRed) {
            // DB: comparison was 'less or same'. Make it 'less than'.
            //     then update the constant after the code to just be the number of items.
            viewPort.Edit("@098983 DB @098998 ::items ");
         } else if (gameCode == LeafGreen) {
            // DB: comparison was 'less or same'. Make it 'less than'.
            //     then update the constant after the code to just be the number of items.
            viewPort.Edit("@098967 DB @09896C ::items ");
         } else if (gameCode == Emerald) {
            // Emerald code already uses the number of items specifically. Just add the
            //    format so we can update the constant whenever the user adds new items.
            viewPort.Edit("@1B0014 ::items ");
         }
         // note that we make no updates for Ruby/Sapphire... that's because I don't
         //    know where the item image tables are stored in those games :(

         CanRunChanged?.Invoke(this, EventArgs.Empty);

         return ErrorInfo.NoError;
      }

      public void TabChanged() => CanRunChanged?.Invoke(this, EventArgs.Empty);
   }
}
