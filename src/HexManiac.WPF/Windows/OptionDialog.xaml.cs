using HavenSoft.HexManiac.Core.Models;
using System.Windows;

namespace HavenSoft.HexManiac.WPF.Windows {
   /// <summary>
   /// Interaction logic for OptionDialog.xaml
   /// </summary>
   public partial class OptionDialog : Window {
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
