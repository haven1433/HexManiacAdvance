using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.WPF.Windows;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace HavenSoft.HexManiac.WPF.Controls {
   public partial class StartScreen {
      public StartScreen() {
         InitializeComponent();
         Loaded += (sender, e) => {
            Usage.Text = AboutWindow.GetUsageText(((EditorViewModel)DataContext).Singletons.MetadataInfo.VersionNumber);
         };
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
