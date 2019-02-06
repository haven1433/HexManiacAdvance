using System.ComponentModel;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public interface IToolViewModel : INotifyPropertyChanged {
      string Name { get; }
   }

   public class FillerTool : IToolViewModel {
      public string Name { get; }
      public FillerTool(string name) { Name = name; }

#pragma warning disable 0067 // it's ok if events are never used
      public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore 0067
   }
}
