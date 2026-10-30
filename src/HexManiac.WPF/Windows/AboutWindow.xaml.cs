using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using System.Windows.Navigation;

namespace HavenSoft.HexManiac.WPF.Windows {
   partial class AboutWindow {
      public AboutWindow(IMetadataInfo metadata) {
         InitializeComponent();
         Version.Text = $"Version {metadata.VersionNumber}";
         Usage.Text = GetUsageText(metadata);
      }

      public static string GetUsageText(IMetadataInfo metadata) {
         if (metadata != null && metadata.IsPublicRelease) {
            return "This is a preview release. Please report bugs via GitHub / Discord.";
         } else {
            return "This is an unstable version: use at your own risk!";
         }
      }

      private void Navigate(object sender, RequestNavigateEventArgs e) {
         NativeProcess.Start(e.Uri.AbsoluteUri);
         e.Handled = true;
      }
   }
}
