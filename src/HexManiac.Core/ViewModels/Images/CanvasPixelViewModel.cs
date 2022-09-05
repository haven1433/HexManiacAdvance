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

      public CanvasPixelViewModel(int width, int height) {
         (PixelWidth, PixelHeight) = (width, height);
         PixelData = new short[width * height];
      }

      public void Draw(IPixelViewModel foreground, int x, int y) {
         for (int yy = 0; yy < foreground.PixelHeight; yy++) {
            for (int xx = 0; xx < foreground.PixelWidth; xx++) {
               var pixel = foreground.PixelData[foreground.PixelWidth * yy + xx];
               if (pixel == foreground.Transparent) continue;
               if (x + xx >= PixelWidth || y + yy >= PixelHeight) continue;
               int offset = PixelWidth * (y + yy) + (x + xx);
               PixelData[offset] = pixel;
            }
         }
         NotifyPropertyChanged(nameof(PixelData));
      }
   }
}
