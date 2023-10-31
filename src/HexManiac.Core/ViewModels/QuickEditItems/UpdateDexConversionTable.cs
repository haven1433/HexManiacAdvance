using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using System;
using System.Threading.Tasks;

namespace HavenSoft.HexManiac.Core.ViewModels.QuickEditItems {
   public class UpdateDexConversionTable : IQuickEditItem {
      public string Name => "Update Dex Conversion Table";

      public string Description => "The games contain a table that converts regional dex entries to national dex entries." + Environment.NewLine +
         EditorViewModel.ApplicationName + " can update this automatically." + Environment.NewLine +
         "If you're using FireRed or LeafGreen, this probably doesn't matter for you.";

      public string WikiLink => "https://github.com/haven1433/HexManiacAdvance/wiki/Pokedex-Conversion-Explained";

      public event EventHandler CanRunChanged;

      public bool CanRun(IViewPort viewPortInterface) {
         var noChange = new NoDataChangeDeltaModel();
         var viewPort = viewPortInterface as ViewPort;
         if (viewPort == null) return false;
         var model = viewPort.Model;
         ArrayRun get(string name) => model.GetNextRun(model.GetAddressFromAnchor(noChange, -1, name)) as ArrayRun;
         var regional = get(HardcodeTablesModel.RegionalDexTableName);
         var national = get(HardcodeTablesModel.NationalDexTableName);
         var convert = get(HardcodeTablesModel.ConversionDexTableName);
         if (regional is null || national is null || convert is null) return false;
         if (regional.ElementContent.Count != 1 || national.ElementContent.Count != 1 || convert.ElementContent.Count != 1) return false;
         if (regional.ElementCount != national.ElementCount || regional.ElementCount != convert.ElementCount) return false;
         if (regional.ElementLength != 2 || national.ElementLength != 2 || convert.ElementLength != 2) return false;

         for (int i = 0; i < regional.ElementCount; i++) {
            var regionalIndex = model.ReadMultiByteValue(regional.Start + i * 2, 2);
            var nationalIndex = model.ReadMultiByteValue(national.Start + i * 2, 2);
            var conversionIndex = model.ReadMultiByteValue(convert.Start + (regionalIndex - 1) * 2, 2);
            if (nationalIndex != conversionIndex) return true;
         }

         return false;
      }

      public Task<ErrorInfo> Run(IViewPort viewPortInterface) {
         var noChange = new NoDataChangeDeltaModel();
         var viewPort = (ViewPort)viewPortInterface;
         var model = viewPort.Model;
         var token = viewPort.CurrentChange;

         Run(model, token);

         return Task.FromResult(ErrorInfo.NoError);
      }

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
