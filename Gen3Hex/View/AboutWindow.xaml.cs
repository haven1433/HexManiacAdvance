using System.Diagnostics;
using System.Windows.Navigation;

namespace HavenSoft.Gen3Hex.View {
   public partial class AboutWindow {
      public AboutWindow() => InitializeComponent();

      private void Navigate(object sender, RequestNavigateEventArgs e) {
         Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
         e.Handled = true;
      }
   }
}
