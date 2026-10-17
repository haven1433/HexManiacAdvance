using HavenSoft.HexManiac.Core.Models;
using System;
using System.Threading.Tasks;

namespace HavenSoft.HexManiac.Core.ViewModels.QuickEditItems {
   /// <summary>
   /// Opens the Intro Manager: the Pokémon, the professor's picture and the music of the new game intro.
   /// </summary>
   public class OpenIntroManager : IQuickEditItem {
      public string Name => "Intro Manager";
      public string Description => "Open a tab for changing the start of a new game: the Pokémon that the professor shows, the picture of the professor, the music of the title screen and of the speech (and the title screen cry in FireRed). Choose from lists with names instead of typing numbers. Works on Emerald, FireRed and games made from them.";
      public string WikiLink => string.Empty;
      public event EventHandler CanRunChanged;

      public bool CanRun(IViewPort viewPortInterface) {
         if (viewPortInterface is not ViewPort viewPort) return false;
         return IntroManagerTab.IsSupported(viewPort.Model);
      }

      public Task<ErrorInfo> Run(IViewPort viewPortInterface) {
         var viewPort = (ViewPort)viewPortInterface;
         viewPort.OpenIntroManagerTab();
         return Task.FromResult(ErrorInfo.NoError);
      }

      public void TabChanged() => CanRunChanged?.Invoke(this, EventArgs.Empty);
   }
}
