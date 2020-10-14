using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace HavenSoft.HexManiac.WPF.Controls {
   public partial class StartScreen {
      public StartScreen() {
         InitializeComponent();

#if STABLE
         Usage.Text = "This is a preview release. Please report any bugs via GitHub or Discord.";
# endif
      }

      private void Navigate(object sender, RequestNavigateEventArgs e) {
         Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
         e.Handled = true;
      }
   }

   /// <summary>
   /// Causes the element within the decorator to not take part in the Measure step of WPF's Measure-Arrange loop.
   /// </summary>
   public class MeasureEmptyDecorator : Decorator {
      protected override Size MeasureOverride(Size constraint) {
         Child.Measure(default);
         return default;
      }
   }
}
