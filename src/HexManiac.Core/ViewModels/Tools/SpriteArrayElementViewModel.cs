using HavenSoft.HexManiac.Core.Models.Runs;
using System;
using System.ComponentModel;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class SpriteArrayElementViewModel : ViewModelCore, IStreamArrayElementViewModel {
      private readonly ViewPort viewPort;
      private readonly SpriteFormat format;

      public event EventHandler<(int originalStart, int newStart)> DataMoved;
      public event EventHandler DataChanged;

      public string Name { get; }
      public int Start { get; }

      public bool IsInError => !string.IsNullOrEmpty(ErrorText);

      private string errorText;
      public string ErrorText {
         get => errorText;
         private set {
            if (TryUpdate(ref errorText, value)) NotifyPropertyChanged(nameof(IsInError));
         }
      }

      public SpriteArrayElementViewModel(ViewPort viewPort, SpriteFormat format, string name, int itemAddress) {
         this.viewPort = viewPort;
         this.format = format;
         Name = name;
         Start = itemAddress;
      }

      public bool TryCopy(IArrayElementViewModel other) {
         throw new NotImplementedException();
      }
   }
}
