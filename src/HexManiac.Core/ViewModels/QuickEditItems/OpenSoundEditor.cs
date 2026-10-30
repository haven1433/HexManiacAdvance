using HavenSoft.HexManiac.Core.Models;
using System;
using System.Threading.Tasks;

namespace HavenSoft.HexManiac.Core.ViewModels.QuickEditItems {
   /// <summary>
   /// Opens the Sound tab: Pokémon cry editor (play / export / import WAV) and music tool (export / insert Sappy .s songs and MIDI files).
   /// </summary>
   public class OpenSoundEditor : IQuickEditItem {
      public string Name => "Sound Editor (Cries & Music)";
      public string Description => "Open a tab for editing Pokémon cries (play, export to WAV, import from WAV) and songs (view, export to .s, insert mid2agb/Sappy .s files or MIDI files).";
      public string WikiLink => string.Empty;
      public event EventHandler CanRunChanged;

      public bool CanRun(IViewPort viewPortInterface) {
         if (viewPortInterface is not ViewPort viewPort) return false;
         return SoundTab.IsSupported(viewPort.Model);
      }

      public Task<ErrorInfo> Run(IViewPort viewPortInterface) {
         ((ViewPort)viewPortInterface).OpenSoundTab();
         return Task.FromResult(ErrorInfo.NoError);
      }

      public void TabChanged() => CanRunChanged?.Invoke(this, EventArgs.Empty);
   }
}
