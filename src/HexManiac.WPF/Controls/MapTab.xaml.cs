using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace HavenSoft.HexManiac.WPF.Controls {
   public partial class MapTab {
      public MapTab() {
         InitializeComponent();
      }

      protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo) {
         base.OnRenderSizeChanged(sizeInfo);
         var partial = ActualWidth / 2 - (int)(ActualWidth / 2);
         var transform = (TranslateTransform)MapView.RenderTransform;
         transform.X = -partial;
      }

      #region Map Interaction

      private MouseButton withinMapInteraction = MouseButton.XButton1; // track which button is being used. Set to XButton1 when not in use.

      private void ButtonDown(object sender, MouseButtonEventArgs e) {
         if (withinMapInteraction != MouseButton.XButton1) return;
         var element = (FrameworkElement)sender;
         var vm = (MapEditorViewModel)element.DataContext;
         var p = GetCoordinates(element, e);
         element.CaptureMouse();
         if (e.LeftButton == MouseButtonState.Pressed) {
            withinMapInteraction = MouseButton.Left;
            vm.DrawDown(p.X, p.Y);
         } else if (e.MiddleButton == MouseButtonState.Pressed) {
            withinMapInteraction = MouseButton.Middle;
            vm.DragDown(p.X, p.Y);
         }
      }

      private void ButtonMove(object sender, MouseEventArgs e) {
         if (withinMapInteraction == MouseButton.XButton1) return;
         var element = (FrameworkElement)sender;
         var vm = (MapEditorViewModel)element.DataContext;
         var p = GetCoordinates(element, e);
         if (withinMapInteraction == MouseButton.Left) {
            vm.DrawMove(p.X, p.Y);
         } else if (withinMapInteraction == MouseButton.Middle) {
            vm.DragMove(p.X, p.Y);
         }
      }

      private void ButtonUp(object sender, MouseButtonEventArgs e) {
         var element = (FrameworkElement)sender;
         element.ReleaseMouseCapture();
         if (withinMapInteraction == MouseButton.XButton1) return;
         if (e.ChangedButton != withinMapInteraction) return;
         var vm = (MapEditorViewModel)element.DataContext;
         var p = GetCoordinates(element, e);
         if (withinMapInteraction == MouseButton.Left) {
            vm.DrawUp(p.X, p.Y);
         } else if (withinMapInteraction == MouseButton.Middle) {
            vm.DragUp(p.X, p.Y);
         }
         withinMapInteraction = MouseButton.XButton1;
      }

      private void Wheel(object sender, MouseWheelEventArgs e) {
         var element = (FrameworkElement)sender;
         var vm = (MapEditorViewModel)element.DataContext;
         var p = GetCoordinates(element, e);
         vm.Zoom(p.X, p.Y, e.Delta > 0);
      }

      private void BlocksDown(object sender, MouseButtonEventArgs e) {
         var element = (FrameworkElement)sender;
         var mainModel = (MapEditorViewModel)DataContext;
         var imageModel = (IPixelViewModel)element.DataContext;
         var p = e.GetPosition(element);
         p.X /= 16;
         p.Y /= 16;
         mainModel.SelectBlock((int)p.X, (int)p.Y);

         // couldn't get bindings to update correctly for this Rectangle for some reason
         // just do it manually
         Canvas.SetLeft(BlockSelectionRect, mainModel.HighlightBlockX);
         Canvas.SetTop(BlockSelectionRect, mainModel.HighlightBlockY);
         BlockSelectionRect.Width = mainModel.HighlightBlockSize;
         BlockSelectionRect.Height = mainModel.HighlightBlockSize;
      }

      private Point GetCoordinates(FrameworkElement element, MouseEventArgs e) {
         var p = e.GetPosition(element);
         return new(p.X - element.ActualWidth / 2, p.Y - element.ActualHeight / 2);
      }

      #endregion
   }
}
