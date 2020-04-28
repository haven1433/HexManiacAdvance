using HavenSoft.HexManiac.Core.Models;
using System.Diagnostics;
using System.Windows.Navigation;

namespace HavenSoft.HexManiac.WPF.Windows {
   partial class AboutWindow {
      public AboutWindow(IMetadataInfo metadata) {
         InitializeComponent();
         Version.Text = $"Version {metadata.VersionNumber}";
#if STABLE
         Usage.Text = "This is a preview release. Please report any bugs via GitHub or Discord.";
#endif
      }

      private void Navigate(object sender, RequestNavigateEventArgs e) {
         Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
         e.Handled = true;
      }
   }
}
