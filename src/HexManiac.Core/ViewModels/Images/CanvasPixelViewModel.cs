using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;

namespace HavenSoft.HexManiac.Core.ViewModels.Images {
   public class CanvasPixelViewModel : ViewModelCore, IPixelViewModel {
      public short Transparent { get; init; } = -1;
      public int PixelWidth { get; }
      public int PixelHeight { get; }
      public short[] PixelData { get; private set; }

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

      public void Fill(short[] pixelData) {
         if (pixelData.Length != PixelData.Length) throw new NotSupportedException($"Need {PixelData.Length} pixels to fill, but was given {pixelData.Length} pixels.");
         PixelData = pixelData;
         NotifyPropertyChanged(nameof(PixelData));
      }

      public void Draw(IPixelViewModel foreground, int x, int y) {
         if (foreground == null) return;
         if (x >= PixelWidth || y >= PixelHeight) return;
         for (int yy = 0; yy < foreground.PixelHeight; yy++) {
            if (y + yy < 0 || y + yy >= PixelHeight) continue;
            if (foreground.Transparent == -1) {
               // copy one row at a time, to account for gaps
               var start = Math.Max(x, 0);
               var end = Math.Min(x + foreground.PixelWidth, PixelWidth);
               Array.Copy(foreground.PixelData, foreground.PixelWidth * yy + start - x, PixelData, PixelWidth * (y + yy) + x, end - start);
            } else {
               // go through each pixel to look for transparency
               for (int xx = 0; xx < foreground.PixelWidth; xx++) {
                  var pixel = foreground.PixelData[foreground.PixelWidth * yy + xx];
                  if (pixel == foreground.Transparent) continue;
                  if (x + xx >= PixelWidth) continue;
                  if (x + xx < 0) continue;
                  int offset = PixelWidth * (y + yy) + (x + xx);
                  PixelData[offset] = pixel;
               }
            }
         }
         NotifyPropertyChanged(nameof(PixelData));
      }

      public void DrawBox(int x, int y, int size, short color) => DrawRect(x, y, size, size, color);

      public void DrawRect(int x, int y, int width, int height, short color) {
         if (x + y * PixelWidth >= PixelData.Length) return;
         for (int i = 0; i < width - 1; i++) {
            PixelData[x + i + y * PixelWidth] = color;
            PixelData[x + width - 1 - i + (y + height - 1) * PixelWidth] = color;
         }
         for (int i = 0; i < height - 1; i++) {
            PixelData[x + (y + height - 1 - i) * PixelWidth] = color;
            PixelData[x + width - 1 + (y + i) * PixelWidth] = color;
         }
      }

      public void DarkenRect(int x, int y, int width, int height, int darkness) {
         if (x + y * PixelWidth >= PixelData.Length) return;
         for (int i = 0; i < width - 1; i++) {
            var (p1, p2) = (x + i + y * PixelWidth, x + width - 1 - i + (y + height - 1) * PixelWidth);
            if (p1 < PixelData.Length) PixelData[p1] = Darken(PixelData[p1], darkness);
            if (p2 < PixelData.Length) PixelData[p2] = Darken(PixelData[p2], darkness);
         }
         for (int i = 0; i < height - 1; i++) {
            var (p1, p2) = (x + (y + height - 1 - i) * PixelWidth, x + width - 1 + (y + i) * PixelWidth);
            if (p1 < PixelData.Length) PixelData[p1] = Darken(PixelData[p1], darkness);
            if (p2 < PixelData.Length) PixelData[p2] = Darken(PixelData[p2], darkness);
         }
      }

      public static short Darken(short color, int amount) {
         // it's faster to do this inline rather than calling UncompressedPaletteColor.ToRGB
         // this method needs to be fast, since it can be called for many pixels on many maps
         int r = (color >> 10) & 0x1F;
         int g = (color >> 5) & 0x1F;
         int b = (color >> 0) & 0x1F;

         r = Math.Max(0, r - amount);
         g = Math.Max(0, g - amount);
         b = Math.Max(0, b - amount);
         return UncompressedPaletteColor.Pack(r, g, b);
      }
      public static short ShiftTowards(short color, (int r, int g, int b) targetColor, int amount) {
         var rgb = UncompressedPaletteColor.ToRGB(color);
         var dr = Math.Sign(targetColor.r - rgb.r);
         var dg = Math.Sign(targetColor.g - rgb.g);
         var db = Math.Sign(targetColor.b - rgb.b);

         rgb.r = (rgb.r + amount * dr).LimitToRange(0, 31);
         rgb.g = (rgb.g + amount * dg).LimitToRange(0, 31);
         rgb.b = (rgb.b + amount * db).LimitToRange(0, 31);
         return UncompressedPaletteColor.Pack(rgb.r, rgb.g, rgb.b);
      }
   }
}
