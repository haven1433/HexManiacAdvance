using System.ComponentModel;

namespace HavenSoft.Gen3Hex.ViewModel {
   public class ViewPort : INotifyPropertyChanged {
      public int Width { get; set; }

      public int Height { get; set; }

      public HexElement this[int x, int y] => new HexElement();

      public event PropertyChangedEventHandler PropertyChanged;
   }
}
