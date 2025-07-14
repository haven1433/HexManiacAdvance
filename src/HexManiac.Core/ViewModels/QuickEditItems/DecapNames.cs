using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HavenSoft.HexManiac.Core.ViewModels.QuickEditItems {
   // TODO 3A72A0, 3A72A2 -> addresses in FireRed for making things plural -> BerrIES, ItemS
   public class DecapNames : IQuickEditItem {
      public string Name => "Decapitalize Names";

      public string Description => "Decapitalize names of Pokemon, Species, Moves, Abilities, Items, Trainers, Types, and Natures";

      public string WikiLink => "https://github.com/haven1433/HexManiacAdvance/wiki/Decapitalization-Explained";

      public event EventHandler CanRunChanged;

      public void TabChanged() => CanRunChanged?.Invoke(this, EventArgs.Empty);

      public bool IsCap(string c) {
         if (c.StartsWith("\"")) c = c.Substring(1);
         if (c.Length != 1) return false;
         return 'A' <= c[0] && c[0] <= 'Z';
      }

      public bool IsSpecial(IDataModel model, PCS pcs) {
         var address = pcs.Source + pcs.Position;
         if (model[address] == 0x1B) return true; // é
         if (model[address] == 0xB4) return true; // '
         return false;
      }
   }
}
