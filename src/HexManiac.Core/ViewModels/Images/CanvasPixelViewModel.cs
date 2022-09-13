namespace HavenSoft.HexManiac.Core.ViewModels.Images {
   public class CanvasPixelViewModel : ViewModelCore, IPixelViewModel {
      public short Transparent => -1;
      public int PixelWidth { get; }
      public int PixelHeight { get; }
      public short[] PixelData { get; }

      private double spriteScale = 1;
      public double SpriteScale {
         get => spriteScale;
         set => Set(ref spriteScale, value, old => {
            NotifyPropertyChanged(nameof(ScaledWidth));
            NotifyPropertyChanged(nameof(ScaledHeight));
         });
      }

      public double ScaledWidth => PixelWidth * SpriteScale;
      public double ScaledHeight => PixelHeight * SpriteScale;

      public CanvasPixelViewModel(int width, int height, short[] data = null) {
         (PixelWidth, PixelHeight) = (width, height);
         PixelData = data ?? new short[width * height];
      }

      public void Draw(IPixelViewModel foreground, int x, int y) {
         for (int yy = 0; yy < foreground.PixelHeight; yy++) {
            for (int xx = 0; xx < foreground.PixelWidth; xx++) {
               var pixel = foreground.PixelData[foreground.PixelWidth * yy + xx];
               if (pixel == foreground.Transparent) continue;
               if (x + xx >= PixelWidth || y + yy >= PixelHeight) continue;
               if (x + yy < 0 || y + yy < 0) continue;
               int offset = PixelWidth * (y + yy) + (x + xx);
               PixelData[offset] = pixel;
            }
         }
         NotifyPropertyChanged(nameof(PixelData));
      }

      public void DrawBox(int x, int y, int size, short color) {
         for (int i = 0; i < size - 1; i++) {
            PixelData[x + i + y * PixelWidth] = color;
            PixelData[x + size - 1 - i + (y + size - 1) * PixelWidth] = color;
            PixelData[x + (y + size - 1 - i) * PixelWidth] = color;
            PixelData[x + size - 1 + (y + i) * PixelWidth] = color;
         }
      }
   }
}
