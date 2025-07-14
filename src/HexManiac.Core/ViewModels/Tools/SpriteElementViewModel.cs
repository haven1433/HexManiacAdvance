using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;


namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class SpriteIndicatorElementViewModel : ViewModelCore, IArrayElementViewModel {
      public IPixelViewModel Image { get; set; }

      public bool visible;
      public bool Visible { get => visible; set => Set(ref visible, value); }

      public string theme; public string Theme { get => theme; set => Set(ref theme, value); }
      public bool IsInError => false;

      public string ErrorText => string.Empty;

      public int ZIndex => 1;

      public event EventHandler DataChanged;
      public event EventHandler DataSelected;

      public SpriteIndicatorElementViewModel(IPixelViewModel image) => Image = image;

      public bool TryCopy(IArrayElementViewModel other) {
         if (other is not SpriteIndicatorElementViewModel indicator) return false;
         Image = indicator.Image;
         NotifyPropertyChanged(nameof(Image));
         return true;
      }
   }

   public class SpriteElementViewModel : PagedElementViewModel, IPixelViewModel {
      public SpriteFormat format;

      public short[] PixelData { get; set; }
      public short Transparent => -1;
      public int PixelWidth => format.TileWidth * 8;
      public int PixelHeight => format.TileHeight * 8;
      public double SpriteScale { get; set; }

      public bool HasMultiplePalettes => MaxPalette > 0;
      public int currentPalette;
      public int MaxPalette { get; set; }

      #region Import / Export Commands

      public bool CanExecuteExportAllImages(object arg) {
         return Model.GetNextRun(Start) is ITableRun table && table.ElementCount > 1;
      }

      #endregion

      public ObservableCollection<SelectionViewModel> PaletteSelection { get; } = new ObservableCollection<SelectionViewModel>();
      public void UpdatePaletteSelection() {
         for (int i = 0; i <= MaxPalette; i++) PaletteSelection[i].Selected = i == currentPalette;
      }

      public bool needsUpdate = false;

      public int[,] lastPixels;
      public IReadOnlyList<short> lastColors;
   }

   public class TileViewModel : ViewModelCore {
      public byte[] DataStore { get; }
      public int Start { get; }

      /// <summary>
      /// Encoded as 5,6,5 bits for r,g,b
      /// </summary>
      public IReadOnlyList<short> Palette { get; set; }

      public TileViewModel(byte[] data, int start, int byteLength, IReadOnlyList<short> palette) {
         DataStore = data;
         Start = start;
         Palette = palette;

         if (palette != null) return;

         int desiredCount = (int)Math.Pow(2, byteLength / 8);
         Palette = CreateDefaultPalette(desiredCount);
      }

      // TODO include horizontal/vertical flip information

      public static short[] CreateDefaultPalette(int desiredCount) {
         var palette = new short[desiredCount];
         for (int i = 0; i < desiredCount; i++) {
            var shade = 0b11111 * i / (desiredCount - 1);
            var color = (shade << 10) | (shade << 5) | shade;
            palette[i] = (short)color;
         }
         return palette;
      }
   }
}
