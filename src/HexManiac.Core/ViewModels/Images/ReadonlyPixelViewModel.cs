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
      public double SpriteScale { get; init; } = 1;

      public ReadonlyPixelViewModel(SpriteFormat sf, short[] data, short transparent = -1) {
         (PixelWidth, PixelHeight, PixelData) = (sf.TileWidth * 8, sf.TileHeight * 8, data);
         Transparent = transparent;
      }

      public ReadonlyPixelViewModel(int width, int height, short[] data = null, short transparent = -1) {
         (PixelWidth, PixelHeight, PixelData) = (width, height, data);
         if (data == null) PixelData = new short[width * height];
         Transparent = transparent;
      }

      public static ReadonlyPixelViewModel Create(IDataModel model, ISpriteRun sprite, bool useTransparency = false, double scale = 1) {
         return SpriteDecorator.BuildSprite(model, sprite, useTransparency, scale);
      }

      public static ReadonlyPixelViewModel Create(IDataModel model, ISpriteRun sprite, int tableElementAddress, bool useTransparency = false, double scale = 1) {
         return SpriteDecorator.BuildSprite(model, sprite, tableElementAddress, useTransparency, scale);
      }

      public static IPixelViewModel Create(IDataModel model, ISpriteRun sprite, IPaletteRun palette, bool useTransparency = false) {
         return SpriteDecorator.BuildSprite(model, sprite, palette, useTransparency);
      }

      public static ReadonlyPixelViewModel Crop(IPixelViewModel pixels, int x, int y, int width, int height) {
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
               if (x + xx < 0 || y + yy < 0) continue;
               int offset = background.PixelWidth * (y + yy) + x + xx;
               data[offset] = pixel;
            }
         }

         return new ReadonlyPixelViewModel(background.PixelWidth, background.PixelHeight, data, background.Transparent);
      }
   }
}
