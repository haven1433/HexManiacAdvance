using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using System;
using System.Threading.Tasks;

namespace HavenSoft.HexManiac.Core.ViewModels.QuickEditItems {
   public class ReorderDex : IQuickEditItem {
      private readonly string displayName, dexTableName;

      public string Name => $"Reorder {displayName} Dex";

      public string Description => $"Open a special UI for interactively reordering the {displayName} pokedex.";

      public string WikiLink => string.Empty;

      public event EventHandler CanRunChanged;

      public ReorderDex(string displayName, string dexTableName) => (this.displayName, this.dexTableName) = (displayName, dexTableName);

      public bool CanRun(IViewPort viewPortInterface) {
         if (!(viewPortInterface is ViewPort viewPort)) return false;
         var model = viewPort.Model;
         var dexOrder = GetTable(model, dexTableName);
         var dexInfo = GetTable(model, HardcodeTablesModel.DexInfoTableName);
         var pokenames = GetTable(model, HardcodeTablesModel.PokemonNameTable);
         var frontSprites = GetTable(model, HardcodeTablesModel.FrontSpritesTable);
         var pokepalettes = GetTable(model, HardcodeTablesModel.PokePalettesTable);
         if (dexOrder != null && pokenames != null && dexInfo != null && frontSprites != null && pokepalettes != null) return true;
         // pokeemerald-expansion (CUBE): the order lives in the species table and the hoenn->national table.
         // Stays enabled when that layout is recognized, so that Run can explain anything missing instead of the item just being grayed out.
         return ExpansionDex.IsExpansionLayout(model);
      }

      public Task<ErrorInfo> Run(IViewPort viewPortInterface) {
         var viewPort = (ViewPort)viewPortInterface;
         var model = viewPort.Model;
         if (GetTable(model, dexTableName) == null && ExpansionDex.IsExpansionLayout(model)) {
            var reason = ExpansionDex.WhyUnavailable(model);
            if (reason != null) return Task.FromResult(new ErrorInfo(reason));
         }
         viewPort.OpenDexReorderTab(dexTableName);
         return Task.FromResult(ErrorInfo.NoError);
      }

      public void TabChanged() => CanRunChanged?.Invoke(this, EventArgs.Empty);

      public static ITableRun GetTable(IDataModel model, string name) {
         var address = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, name);
         return model.GetNextRun(address) as ITableRun;
      }
   }
}
