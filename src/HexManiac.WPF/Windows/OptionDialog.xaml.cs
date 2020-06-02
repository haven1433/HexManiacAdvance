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
         // TODO figure out which button index this is and set result
      }
   }
}
