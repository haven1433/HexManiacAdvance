using HavenSoft.HexManiac.Core.Models;
using System.Windows;

namespace HavenSoft.HexManiac.WPF.Windows {
   public partial class OptionDialog {
      public int Result { get; set; }

      public OptionDialog() {
         InitializeComponent();
         Result = -1;
      }

      private void OptionClicked(object sender, RoutedEventArgs e) {
         var option = (VisualOption)((FrameworkElement)sender).DataContext;
         Result = option.Index;
         DialogResult = true;
      }
   }
}
