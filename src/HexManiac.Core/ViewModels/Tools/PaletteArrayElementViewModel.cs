using HavenSoft.HexManiac.Core.Models.Runs;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class PaletteArrayElementViewModel : ViewModelCore, IStreamArrayElementViewModel {
      private readonly ViewPort viewPort;
      private PaletteFormat format;

      public event EventHandler<(int originalStart, int newStart)> DataMoved;
      public event EventHandler DataChanged;

      public string Name { get; private set; }
      public int Start { get; private set; } // a pointer to the sprite's compressed data

      public bool IsInError => !string.IsNullOrEmpty(ErrorText);

      public ObservableCollection<short> Colors { get; } = new ObservableCollection<short>();

      public int ColorWidth => (int)Math.Ceiling(Math.Sqrt(Colors.Count));
      public int ColorHeight => (int)Math.Sqrt(Colors.Count);
      public int PixelWidth => 64;
      public int PixelHeight => 64;

      private string errorText;
      public string ErrorText {
         get => errorText;
         private set {
            if (TryUpdate(ref errorText, value)) NotifyPropertyChanged(nameof(IsInError));
         }
      }

      public PaletteArrayElementViewModel(ViewPort viewPort, PaletteFormat format, string name, int itemAddress) {
         this.viewPort = viewPort;
         this.format = format;
         Name = name;
         Start = itemAddress;
         UpdateColors();
      }

      public bool TryCopy(IArrayElementViewModel other) {
         if (!(other is PaletteArrayElementViewModel that)) return false;
         Name = that.Name;
         Start = that.Start;
         format = that.format;
         ErrorText = that.ErrorText;
         NotifyPropertyChanged(nameof(Name));
         NotifyPropertyChanged(nameof(Start));
         NotifyPropertyChanged(nameof(ErrorText));
         UpdateColors();
         return true;
      }

      private void UpdateColors() {
         var destination = viewPort.Model.ReadPointer(Start);
         var data = LZRun.Decompress(viewPort.Model, destination);
         int count = (int)Math.Pow(2, format.Bits);
         Debug.Assert(data.Length == count * 2);
         Colors.Clear();
         for (int i = 0; i < count; i++) {
            var color = (short)(data[i * 2] | (data[i * 2 + 1] << 8));
            Colors.Add(color);
         }
      }
   }
}
