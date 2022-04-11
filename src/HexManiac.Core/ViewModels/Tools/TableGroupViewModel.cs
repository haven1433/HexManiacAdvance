using System.Collections.ObjectModel;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class TableGroupViewModel : ViewModelCore {
      private string groupName;
      public bool DisplayHeader => GroupName != "Other";
      public string GroupName { get => groupName; set => Set(ref groupName, value); }
      public ObservableCollection<IArrayElementViewModel> Members { get; } = new();
   }
}
