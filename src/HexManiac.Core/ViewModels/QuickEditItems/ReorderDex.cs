using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using System;

namespace HavenSoft.HexManiac.Core.ViewModels.QuickEditItems {
   public class ReorderDex : IQuickEditItem {
      public string Name => "Reorder Dex";

      public string Description => "Open a special UI for interactively reordering the national pokedex.";

      public string WikiLink => string.Empty;

      public event EventHandler CanRunChanged;

      public bool CanRun(IViewPort viewPortInterface) {
         if (!(viewPortInterface is ViewPort viewPort)) return false;
         var model = viewPort.Model;
         var dexOrder = GetTable(model, HardcodeTablesModel.NationalDexTableName);
         var dexInfo = GetTable(model, HardcodeTablesModel.DexInfoTableName);
         return dexOrder != null && dexInfo != null;
      }

      public ErrorInfo Run(IViewPort viewPortInterface) {
         var viewPort = (ViewPort)viewPortInterface;
         viewPort.OpenDexReorderTab();
         return ErrorInfo.NoError;
      }

      public void TabChanged() => CanRunChanged?.Invoke(this, EventArgs.Empty);

      public static ITableRun GetTable(IDataModel model, string name) {
         var address = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, name);
         return model.GetNextRun(address) as ITableRun;
      }
   }
}
