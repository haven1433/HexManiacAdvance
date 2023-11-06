using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HavenSoft.HexManiac.Core.ViewModels.QuickEditItems {
   public class ApplyCFRUPatch : IQuickEditItem {
      private const string PatchFileName = "resources/hubol_dpe_cfru.ups";

      public EditorViewModel Editor { get; init; }

      public string Name => "Apply HUBOL DPE CFRU";

      public string Description => @"Haven's Unofficial Build Of Leon's DPE/CFRU Rombase.

Running this will apply Leon's base, Dynamic Pokemon Expansion, and the Complete FireRed Upgrade to the current file.

WARNING! This will save your file and make this ROM no longer vanilla!

Credits to Leon for putting together the rombase with additional recommended features over the DPE/CFRU.

Credits to Skeli / Ghoulslash for DPE / CFRU.

Please read the Wiki for information about how to use the included features.";

      public string WikiLink => "https://github.com/haven1433/HexManiacAdvance/wiki/Haven's-Unofficial-Build-Of-Leon's-Dynamic-Pokemon-Expansion---Complete-FireRed-Upgrade-Rombase";

      public event EventHandler CanRunChanged;

      public bool CanRun(IViewPort viewPort) => viewPort is ViewPort && (uint)Patcher.CalcCRC32(viewPort.Model.RawData) == 0xDD88761C;

      public async Task<ErrorInfo> Run(IViewPort viewPort1) {
         if (viewPort1 is not ViewPort viewPort) return new ErrorInfo("Selected tab is expected to be a FireRed data tab.");
         var model = viewPort.Model;
         var patch = File.ReadAllBytes(PatchFileName);
         Patcher.ApplyUPSPatch(model, patch, () => viewPort.CurrentChange, true, out var _);

         var fullFileName = viewPort.FullFileName;
         var fileName = viewPort.FileName;

         // close tab (and related tabs), delete the toml, and re-open
         viewPort.Save.Execute(Editor.FileSystem);
         viewPort.Close.Execute();
         foreach (var tab in Editor.ToList()) {
            if (tab is IViewPort vp && vp.Model == viewPort.Model) { tab.Save.Execute(); tab.Close.Execute(); }
            if (tab is MapEditorViewModel me && me.ViewPort.Model == viewPort.Model) { tab.Save.Execute(); tab.Close.Execute(); }
            if(tab is DexReorderTab dr && dr.Model == viewPort.Model) { tab.Close.Execute(); }
            // need to close diff tabs / search result tabs?
         }

         var toml = Path.ChangeExtension(fullFileName, "toml");
         File.Delete(toml);
         Editor.Open.Execute(Editor.FileSystem.LoadFile(fullFileName));

         return ErrorInfo.NoError;
      }

      public void TabChanged() => CanRunChanged.Raise(this);
   }
}
