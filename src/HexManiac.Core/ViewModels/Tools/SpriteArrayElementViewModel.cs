using HavenSoft.HexManiac.Core.Models.Runs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class SpriteArrayElementViewModel : ViewModelCore, IStreamArrayElementViewModel {
      private readonly ViewPort viewPort;
      private SpriteFormat format;

      public event EventHandler<(int originalStart, int newStart)> DataMoved;
      public event EventHandler DataChanged;

      public string Name { get; private set; }
      public int Start { get; private set; } // a pointer to the sprite's compressed data

      public bool IsInError => !string.IsNullOrEmpty(ErrorText);

      public int PixelWidth => format.TileWidth * 8;
      public int PixelHeight => format.TileHeight * 8;
      public int CurrentPage => 0; // TODO

      public ObservableCollection<TileViewModel> Tiles { get; } = new ObservableCollection<TileViewModel>();

      private string errorText;
      public string ErrorText {
         get => errorText;
         private set {
            if (TryUpdate(ref errorText, value)) NotifyPropertyChanged(nameof(IsInError));
         }
      }

      public SpriteArrayElementViewModel(ViewPort viewPort, SpriteFormat format, string name, int itemAddress) {
         this.viewPort = viewPort;
         this.format = format;
         Name = name;
         Start = itemAddress;
         UpdateTiles();
      }

      public bool TryCopy(IArrayElementViewModel other) {
         if (!(other is SpriteArrayElementViewModel that)) return false;
         Name = that.Name;
         Start = that.Start;
         format = that.format;
         ErrorText = that.ErrorText;
         NotifyPropertyChanged(nameof(Name));
         NotifyPropertyChanged(nameof(Start));
         NotifyPropertyChanged(nameof(ErrorText));
         NotifyPropertyChanged(nameof(PixelWidth));
         NotifyPropertyChanged(nameof(PixelHeight));
         UpdateTiles();
         return true;
      }

      private void UpdateTiles() {
         // TODO support multiple layers
         var destination = viewPort.Model.ReadPointer(Start);
         var data = LZRun.Decompress(viewPort.Model, destination);
         if (format.BitsPerPixel == 4) FixPixelByteOrder(data);
         var tileSize = 8 * format.BitsPerPixel;
         Debug.Assert(data.Length == format.ExpectedByteLength);
         Tiles.Clear();
         for (int y = 0; y < format.TileHeight; y++) {
            for (int x = 0; x < format.TileWidth; x++) {
               var tileIndex = y * format.TileWidth + x;
               Tiles.Add(new TileViewModel(data, tileIndex * tileSize, tileSize));
            }
         }
      }

      // the gba expects the high bits to be the first pixel. WPF expects the low bits to be the first pixel.
      private static void FixPixelByteOrder(byte[] data) {
         for (int i = 0; i < data.Length; i++) {
            data[i] = (byte)((data[i] >> 4) | (data[i] << 4));
         }
      }
   }

   public class TileViewModel : ViewModelCore {
      public byte[] DataStore { get; }
      public int Start { get; }

      /// <summary>
      /// Encoded as 5,6,5 bits for r,g,b
      /// </summary>
      public IReadOnlyList<short> Palette { get; private set; }

      public TileViewModel(byte[] data, int start, int byteLength) {
         DataStore = data;
         Start = start;
         // TODO figure out default palette based on byteLength / 8 = bitsPerPixel
         var palette = new List<short>();
         for (int i = 0; i <= 15; i++) {
            var (r, g, b) = (0b11111 * i / 15, 0b111111 * i / 15, 0b11111 * i / 15);
            var color = (r << 11) | (g << 5) | b;
            palette.Add((short)color);
         }
         Palette = palette;
      }

      // TODO include horizontal/vertical flip information
   }
}
