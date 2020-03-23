using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class SpriteTool : ViewModelCore, IToolViewModel {
      public string Name => "Image";

      private int spriteAddress;
      public int SpriteAddress {
         get => spriteAddress;
         set {
            if (!TryUpdate(ref spriteAddress, value)) return;
         }
      }

      private int paletteAddress;
      public int PaletteAddress {
         get => paletteAddress;
         set {
            if (!TryUpdate(ref paletteAddress, value)) return;
         }
      }

      public event EventHandler<string> OnMessage;

      public SpriteTool() {
         spriteAddress = Pointer.NULL;
         paletteAddress = Pointer.NULL;
      }
   }
}
