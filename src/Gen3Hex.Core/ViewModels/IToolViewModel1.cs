using System.ComponentModel;

namespace HavenSoft.Gen3Hex.Core.ViewModels {
   public interface IToolViewModel : INotifyPropertyChanged {
      string Name { get; }
   }

   public class PCSTool : ViewModelCore, IToolViewModel {
      public string Name => "String";
   }
}
