using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;

namespace HavenSoft.HexManiac.Core.ViewModels.Images {
   public class ReadonlyPixelViewModel : ViewModelCore, IPixelViewModel {
      public short Transparent { get; }
      public int PixelWidth { get; }
      public int PixelHeight { get; }
      public short[] PixelData { get; }
      public double SpriteScale => 1;

      public ReadonlyPixelViewModel(SpriteFormat sf, short[] data, short transparent = -1) {
         (PixelWidth, PixelHeight, PixelData) = (sf.TileWidth * 8, sf.TileHeight * 8, data);
         Transparent = transparent;
      }
      private ReadonlyPixelViewModel(int width, int height, short[] data) {
         (PixelWidth, PixelHeight, PixelData) = (width, height, data);
         Transparent = -1;
      }

      public static IPixelViewModel Create(IDataModel model, ISpriteRun sprite, bool useTransparency = false) {
         return SpriteDecorator.BuildSprite(model, sprite, useTransparency);
      }

      public static IPixelViewModel Create(IDataModel model, ISpriteRun sprite, IPaletteRun palette, bool useTransparency = false) {
         return SpriteDecorator.BuildSprite(model, sprite, palette, useTransparency);
      }

      public static IPixelViewModel Crop(IPixelViewModel pixels, int x, int y, int width, int height) {
         return TilemapTableRun.Crop(pixels, x, y, Math.Max(0, pixels.PixelWidth - width - x), Math.Max(0, pixels.PixelHeight - height - y));
      }

      public static IPixelViewModel Render(IPixelViewModel background, IPixelViewModel foreground, int x, int y) {
         var data = new short[background.PixelData.Length];
         Array.Copy(background.PixelData, data, background.PixelData.Length);

         for (int yy = 0; yy < foreground.PixelHeight; yy++) {
            for (int xx = 0; xx < foreground.PixelWidth; xx++) {
               var pixel = foreground.PixelData[foreground.PixelWidth * yy + xx];
               if (pixel == foreground.Transparent) continue;
               if (x + xx >= background.PixelWidth || y + yy >= background.PixelHeight) continue;
               int offset = background.PixelWidth * (y + yy) + x + xx;
               data[offset] = pixel;
            }
         }

         return new ReadonlyPixelViewModel(background.PixelWidth, background.PixelHeight, data);
      }
   }
}
