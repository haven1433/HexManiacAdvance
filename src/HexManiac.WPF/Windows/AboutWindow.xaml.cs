using HavenSoft.HexManiac.Core.Models;
using System.Diagnostics;
using System.Windows.Navigation;

namespace HavenSoft.HexManiac.WPF.Windows {
   partial class AboutWindow {
      public AboutWindow(IMetadataInfo metadata) {
         InitializeComponent();
         Version.Text = $"Version {metadata.VersionNumber}";
      }

      private void Navigate(object sender, RequestNavigateEventArgs e) {
         Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
         e.Handled = true;
      }
   }
}
