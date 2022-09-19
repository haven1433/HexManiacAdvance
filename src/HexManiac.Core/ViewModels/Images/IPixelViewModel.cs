using System.ComponentModel;
using System.Linq;

namespace HavenSoft.HexManiac.Core.ViewModels.Images {
   public interface IPixelViewModel : INotifyPropertyChanged {
      short Transparent { get; }
      int PixelWidth { get; }
      int PixelHeight { get; }
      short[] PixelData { get; }
      double SpriteScale { get; }
   }

   public static class IPixelViewModelExtensions {
      // only crops height, not width
      public static IPixelViewModel AutoCrop(this IPixelViewModel image) {
         int top = 0, bottom = image.PixelHeight;
         while (top < image.PixelHeight && image.PixelWidth.Range().All(i => image.PixelData[top * image.PixelWidth + i] == image.Transparent)) top++;
         while (bottom > 0 && image.PixelWidth.Range().All(i => image.PixelData[bottom * image.PixelWidth - i - 1] == image.Transparent)) bottom--;
         var height = bottom - top;
         while (height % 8 != 0) {
            height++;
            top = (top - 1).LimitToRange(0, image.PixelHeight);
         }
         var data = new short[image.PixelWidth * height];
         for (int y = 0; y < height; y++) {
            for (int x = 0; x < image.PixelWidth; x++) {
               data[y * image.PixelWidth + x] = image.PixelData[(top + y) * image.PixelWidth + x];
            }
         }
         return new ReadonlyPixelViewModel(image.PixelWidth, height, data, image.Transparent);
      }

      public static IPixelViewModel ReflectX(this IPixelViewModel image) {
         var data = new short[image.PixelWidth * image.PixelHeight];
         for (int y = 0; y < image.PixelHeight; y++) {
            for (int x = 0; x < image.PixelWidth; x++) {
               var xx = image.PixelWidth - x - 1;
               data[y * image.PixelWidth + x] = image.PixelData[y * image.PixelWidth + xx];
            }
         }
         return new ReadonlyPixelViewModel(image.PixelWidth, image.PixelHeight, data, image.Transparent);
      }
   }
}
