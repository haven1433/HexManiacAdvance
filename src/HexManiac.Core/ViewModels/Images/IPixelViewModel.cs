using System.ComponentModel;
using System.Linq;

namespace HavenSoft.HexManiac.Core.ViewModels.Images {
   public interface IPixelViewModel : INotifyPropertyChanged {
      short Transparent { get; }
      int PixelWidth { get; }
      int PixelHeight { get; }
      double SpriteScale { get; }
   }

   public static class IPixelViewModelExtensions {
   }
}
