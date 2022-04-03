using System.Collections.ObjectModel;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class TableGroupViewModel : ViewModelCore {
      private string groupName;
      public string GroupName { get => groupName; set => Set(ref groupName, value); }
      public ObservableCollection<IArrayElementViewModel> Members { get; } = new();
   }
}
