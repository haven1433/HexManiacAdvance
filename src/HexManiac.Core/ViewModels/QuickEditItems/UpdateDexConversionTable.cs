using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using System;
using System.Threading.Tasks;

namespace HavenSoft.HexManiac.Core.ViewModels.QuickEditItems {
   public class UpdateDexConversionTable : IQuickEditItem {
      public string Name => "Update Dex Conversion Table";

      public string WikiLink => "https://github.com/haven1433/HexManiacAdvance/wiki/Pokedex-Conversion-Explained";

      public event EventHandler CanRunChanged;

      public static void Run(IDataModel model, ModelDelta token) {
         ArrayRun get(string name) => model.GetNextRun(model.GetAddressFromAnchor(token, -1, name)) as ArrayRun;
         var regional = get(HardcodeTablesModel.RegionalDexTableName);
         var national = get(HardcodeTablesModel.NationalDexTableName);
         var convert = get(HardcodeTablesModel.ConversionDexTableName);
         if (convert == null) return;

         for (int i = 0; i < regional.ElementCount; i++) {
            var regionalIndex = model.ReadMultiByteValue(regional.Start + i * 2, 2);
            var nationalIndex = model.ReadMultiByteValue(national.Start + i * 2, 2);
            var conversionIndex = model.ReadMultiByteValue(convert.Start + (regionalIndex - 1) * 2, 2);
            if (nationalIndex != conversionIndex) {
               model.WriteMultiByteValue(convert.Start + (regionalIndex - 1) * 2, 2, token, nationalIndex);
            }
         }
      }

      public void TabChanged() => CanRunChanged?.Invoke(this, EventArgs.Empty);
   }
}
