using HavenSoft.HexManiac.Core.Models;
using System;
using System.Threading.Tasks;

namespace HavenSoft.HexManiac.Core.ViewModels.QuickEditItems {
   /// <summary>
   /// Opens the Character Customization editor: the skin tones and clothes colours the player can choose after picking a boy or a girl.
   /// </summary>
   public class OpenCharacterCustomization : IQuickEditItem {
      public string Name => "Character Customization";
      public string Description => "Open a tab for editing the skin tones and clothes colours the player can choose in the new game intro: rename, recolour, add or remove them and see the boy and the girl wearing them. Needs a ROM with the character customization tables (CUBE has them).";
      public string WikiLink => string.Empty;
      public event EventHandler CanRunChanged;

      public bool CanRun(IViewPort viewPortInterface) {
         if (viewPortInterface is not ViewPort viewPort) return false;
         return CharacterCustomizationTab.IsSupported(viewPort.Model);
      }

      public Task<ErrorInfo> Run(IViewPort viewPortInterface) {
         var viewPort = (ViewPort)viewPortInterface;
         viewPort.OpenCharacterCustomizationTab();
         return Task.FromResult(ErrorInfo.NoError);
      }

      public void TabChanged() => CanRunChanged?.Invoke(this, EventArgs.Empty);
   }
}
