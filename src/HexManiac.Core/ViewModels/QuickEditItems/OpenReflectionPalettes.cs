using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Threading.Tasks;

namespace HavenSoft.HexManiac.Core.ViewModels.QuickEditItems {
   /// <summary>
   /// Shows the Reflection Palettes editor: the palettes of the reflections in water, on ice and under bridges, with the sprites each one affects. It is the second section of the panel
   /// on the right side of the overworld sprites view (the first is Field Effects).
   /// </summary>
   public class OpenReflectionPalettes : IQuickEditItem {
      public string Name => "Reflection Palettes";
      public string Description => "Show, next to the overworld sprites, the editor for finding and changing the palettes that reflections are drawn with (water, ice, bridges), with a preview of the sprites each one affects: the player, the other people and the Pokemon. "
         + "A game that works its reflections out while it runs (pokeemerald-expansion, CUBE) shows the rules it uses instead.";
      public string WikiLink => string.Empty;
      public event EventHandler CanRunChanged;

      public bool CanRun(IViewPort viewPortInterface) {
         if (viewPortInterface is not ViewPort viewPort) return false;
         return ReflectionPaletteTab.IsSupported(viewPort.Model);
      }

      public Task<ErrorInfo> Run(IViewPort viewPortInterface) {
         var viewPort = (ViewPort)viewPortInterface;
         viewPort.OpenOverworldSpritesEditor(OverworldSecondarySection.Reflections);
         return Task.FromResult(ErrorInfo.NoError);
      }

      public void TabChanged() => CanRunChanged?.Invoke(this, EventArgs.Empty);
   }
}
