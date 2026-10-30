using HavenSoft.HexManiac.Core.ViewModels;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace HavenSoft.HexManiac.WPF.Controls {
   public partial class TilesetAnimationView {
      private TilesetAnimationTab ViewModel => DataContext as TilesetAnimationTab;

      private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(1000.0 / 60) };

      public TilesetAnimationView() {
         InitializeComponent();
         timer.Tick += (sender, e) => {
            // a bad frame must never take the whole program down: stop the preview instead
            try { ViewModel?.Tick(); } catch (Exception) { timer.Stop(); }
         };
         Loaded += (sender, e) => timer.Start();
         Unloaded += (sender, e) => timer.Stop();
      }

      /// <summary>The pane (primary or secondary tileset) a tileset picture belongs to: the picture sits in a grid that carries the pane.</summary>
      private static TilesetPane PaneOf(object sender) => (sender as FrameworkElement)?.Parent is FrameworkElement parent ? parent.DataContext as TilesetPane : null;

      private void TilesetClicked(object sender, MouseButtonEventArgs e) => PickTile(sender);

      private void TilesetDragged(object sender, MouseEventArgs e) {
         if (e.LeftButton != MouseButtonState.Pressed) return;
         PickTile(sender);
      }

      private void PickTile(object sender) {
         if (sender is not IInputElement image) return;
         var pane = PaneOf(sender);
         if (pane == null || ViewModel == null) return;
         var position = Mouse.GetPosition(image); // the image's own (unscaled) pixel coordinates
         ViewModel.PickTile(pane, (int)position.X, (int)position.Y);
      }

      private void EntryClicked(object sender, MouseButtonEventArgs e) {
         if (sender is FrameworkElement element && element.DataContext is TilesetAnimationItem item && ViewModel != null) ViewModel.SelectedEntry = item;
      }

      private void DoorClicked(object sender, MouseButtonEventArgs e) {
         if (sender is FrameworkElement element && element.DataContext is DoorItem item && ViewModel != null) ViewModel.SelectedDoor = item;
      }

      private void DoorFrameClicked(object sender, MouseButtonEventArgs e) {
         if (sender is FrameworkElement element && element.DataContext is TilesetAnimationFrame frame && frame.Door != null && ViewModel != null) {
            ViewModel.SelectedDoor = frame.Door;
            frame.Door.EditFrame(frame);
            e.Handled = true;
         }
      }

      private void FrameClicked(object sender, MouseButtonEventArgs e) {
         if (sender is FrameworkElement element && element.DataContext is TilesetAnimationFrame frame && ViewModel != null) {
            ViewModel.SelectedEntry = frame.Owner;
            frame.Owner.EditFrame(frame);
            e.Handled = true;
         }
      }
   }
}
