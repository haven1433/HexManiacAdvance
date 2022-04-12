using System.Collections.ObjectModel;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class TableGroupViewModel : ViewModelCore {
      private string groupName;
      public bool DisplayHeader => GroupName != "Other";
      public string GroupName { get => groupName; set => Set(ref groupName, value, old => NotifyPropertyChanged(nameof(DisplayHeader))); }
      public ObservableCollection<IArrayElementViewModel> Members { get; } = new();
      public TableGroupViewModel() { GroupName = "Other"; }
   }
}
