using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.WPF.Windows;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace HavenSoft.HexManiac.WPF.Controls {
   public partial class StartScreen {
      public StartScreen() {
         InitializeComponent();
         Loaded += (sender, e) => {
            var version = ((EditorViewModel)DataContext)?.Singletons.MetadataInfo;
            Usage.Text = AboutWindow.GetUsageText(version);
         };
      }

      private void Navigate(object sender, RequestNavigateEventArgs e) {
         NativeProcess.Start(e.Uri.AbsoluteUri);
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
