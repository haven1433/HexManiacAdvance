using System;
using System.Windows.Input;
using HavenSoft.HexManiac.Core.ViewModels;

namespace HavenSoft.HexManiac.WPF.Windows {
   public partial class ThemeSelector {
      public ThemeSelector() => InitializeComponent();

      private void ClearKeyboardFocus(object sender, EventArgs e) => Keyboard.ClearFocus();

      private void CloseWindow(object sender, System.Windows.RoutedEventArgs e) {
         this.Close();
      }

      private void ThemeReset(object sender, System.Windows.RoutedEventArgs e) {
         var viewModel = (Theme)DataContext;
         viewModel.Reset();
      }
   }
}
