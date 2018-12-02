using System.Diagnostics;
using System.Reflection;
using System.Windows.Navigation;

namespace HavenSoft.Gen3Hex.View {
   public partial class AboutWindow {
      public AboutWindow() {
         InitializeComponent();

         var assembly = Assembly.GetExecutingAssembly();
         var fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
         Version.Text = $"Version {fvi.FileMajorPart}.{fvi.FileMinorPart}.{fvi.FileBuildPart}";
      }

      private void Navigate(object sender, RequestNavigateEventArgs e) {
         Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
         e.Handled = true;
      }
   }
}
