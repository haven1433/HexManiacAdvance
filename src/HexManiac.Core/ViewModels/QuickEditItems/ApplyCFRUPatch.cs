using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HavenSoft.HexManiac.Core.ViewModels.QuickEditItems {
   public class ApplyCFRUPatch : IQuickEditItem {
      public const string PatchFileName = "resources/hubol_dpe_cfru.ups";

      public string Name => "Apply HUBOL DPE CFRU";

      public string Description => @"Haven's Unofficial Build Of Leon's DPE/CFRU Rombase.

Running this will apply Leon's base, Dynamic Pokemon Expansion, and the Complete FireRed Upgrade to the current file.

WARNING! This will save your file and make this ROM no longer vanilla!

Credits to Leon for putting together the rombase with additional recommended features over the DPE/CFRU.

Credits to Skeli / Ghoulslash for DPE / CFRU.

Please read the Wiki for information about how to use the included features.";

      public string WikiLink => "https://github.com/haven1433/HexManiacAdvance/wiki/Haven's-Unofficial-Build-Of-Leon's-Dynamic-Pokemon-Expansion---Complete-FireRed-Upgrade-Rombase";

      public event EventHandler CanRunChanged;

      public void TabChanged() => CanRunChanged.Raise(this);
   }
}
