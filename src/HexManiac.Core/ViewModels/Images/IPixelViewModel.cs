using System.ComponentModel;

namespace HavenSoft.HexManiac.Core.ViewModels.Images {
   public interface IPixelViewModel : INotifyPropertyChanged {
      short Transparent { get; }
      int PixelWidth { get; }
      int PixelHeight { get; }
      short[] PixelData { get; }
      double SpriteScale { get; }
   }
}
