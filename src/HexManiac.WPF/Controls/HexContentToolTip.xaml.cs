using System.Collections.ObjectModel;

namespace HavenSoft.HexManiac.WPF.Controls {
   public partial class HexContentToolTip {
      public HexContentToolTip(ObservableCollection<object> items) {
         InitializeComponent();
         ToolTipContent.ItemsSource = items;
      }
   }
}
