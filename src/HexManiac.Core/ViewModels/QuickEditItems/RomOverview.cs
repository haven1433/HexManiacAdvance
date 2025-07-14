using System;

namespace HavenSoft.HexManiac.Core.ViewModels.QuickEditItems {
   public class RomOverview : IQuickEditItem {
      public string Name => "Render Rom Overview";

      public string Description => "Render an image that represents the raw bytes in the rom. Serves little useful purpose, but is pretty.";

      public string WikiLink => "https://github.com/haven1433/HexManiacAdvance/wiki/Rom-Overview-Explained";

      public event EventHandler EditSelected;

      public event EventHandler CanRunChanged;

      public void TabChanged() => CanRunChanged?.Invoke(this, EventArgs.Empty);
   }
}
