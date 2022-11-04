using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using System;
using System.Windows;
using System.Windows.Input;

namespace HavenSoft.HexManiac.WPF.Controls {
   public partial class SelectedBlockEditor {
      private BlockEditor ViewModel => (BlockEditor)DataContext;
      public SelectedBlockEditor() {
         InitializeComponent();
      }

      private void WheelOverImage(object sender, MouseWheelEventArgs e) {
         // ViewModel.LayerMode = (ViewModel.LayerMode + 1) % 3;
         e.Handled = true;
      }

      private void MouseEnterTile(object sender, MouseEventArgs e) {
         var element = (FrameworkElement)sender;
         ViewModel.EnterTile((IPixelViewModel)element.DataContext);
         e.Handled = true;
      }

      private void MouseClickTile(object sender, MouseButtonEventArgs e) {
         var element = (FrameworkElement)sender;
         ViewModel.DrawOnTile((IPixelViewModel)element.DataContext);
         e.Handled = true;
      }

      private void MouseGrabTile(object sender, MouseButtonEventArgs e) {
         var element = (FrameworkElement)sender;
         ViewModel.GetSelectionFromTile((IPixelViewModel)element.DataContext);
         e.Handled = true;
      }

      private void MouseExitTiles(object sender, MouseEventArgs e) => ViewModel.ExitTiles();
   }
}
