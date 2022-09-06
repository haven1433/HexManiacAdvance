using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace HavenSoft.HexManiac.WPF.Controls {
   public partial class MapTab {
      private MapEditorViewModel ViewModel => (MapEditorViewModel)DataContext;

      public MapTab() {
         InitializeComponent();
         DataContextChanged += UpdateDataContext;
      }

      private void UpdateDataContext(object sender, DependencyPropertyChangedEventArgs e) {
         var oldContext = e.OldValue as MapEditorViewModel;
         if (oldContext != null) {
            oldContext.PropertyChanged -= HandleContextPropertyChanged;
            oldContext.AutoscrollBlocks -= AutoscrollBlocks;
         }
         var newContext = e.NewValue as MapEditorViewModel;
         if (newContext != null) {
            newContext.PropertyChanged += HandleContextPropertyChanged;
            newContext.AutoscrollBlocks += AutoscrollBlocks;
         }
      }

      private void HandleContextPropertyChanged(object sender, PropertyChangedEventArgs e) {
         // TODO any custom property logic here
      }

      private void AutoscrollBlocks(object sender, EventArgs e) {
         var scrollRange = BlockViewer.ExtentHeight - BlockViewer.ViewportHeight;
         var scrollPercent = ((ViewModel.DrawBlockIndex - 8) / 1008.0).LimitToRange(0, 1);
         BlockViewer.ScrollToVerticalOffset(scrollRange * scrollPercent);
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
         var vm = ViewModel;
         var p = GetCoordinates(element, e);
         element.CaptureMouse();
         if (e.LeftButton == MouseButtonState.Pressed) {
            withinMapInteraction = MouseButton.Left;
            vm.DrawDown(p.X, p.Y);
         } else if (e.MiddleButton == MouseButtonState.Pressed) {
            withinMapInteraction = MouseButton.Middle;
            vm.DragDown(p.X, p.Y);
         } else if (e.RightButton == MouseButtonState.Pressed) {
            vm.SelectDown(p.X, p.Y);
         }
      }

      private void ButtonMove(object sender, MouseEventArgs e) {
         var element = (FrameworkElement)sender;
         var vm = ViewModel;
         var p = GetCoordinates(element, e);
         if (withinMapInteraction == MouseButton.XButton1) {
            vm.Hover(p.X, p.Y);
            return;
         }
         if (withinMapInteraction == MouseButton.Left) {
            vm.DrawMove(p.X, p.Y);
         } else if (withinMapInteraction == MouseButton.Middle) {
            vm.DragMove(p.X, p.Y);
         } else if (withinMapInteraction == MouseButton.Right) {
            vm.SelectMove(p.X, p.Y);
         }
      }

      private void ButtonUp(object sender, MouseButtonEventArgs e) {
         var element = (FrameworkElement)sender;
         element.ReleaseMouseCapture();
         if (withinMapInteraction == MouseButton.XButton1) return;
         if (e.ChangedButton != withinMapInteraction) return;
         var vm = ViewModel;
         var p = GetCoordinates(element, e);
         if (withinMapInteraction == MouseButton.Left) {
            vm.DrawUp(p.X, p.Y);
         } else if (withinMapInteraction == MouseButton.Middle) {
            vm.DragUp(p.X, p.Y);
         } else if (withinMapInteraction == MouseButton.Right) {
            vm.SelectUp(p.X, p.Y);
         }
         withinMapInteraction = MouseButton.XButton1;
      }

      private void Wheel(object sender, MouseWheelEventArgs e) {
         var element = (FrameworkElement)sender;
         var vm = ViewModel;
         var p = GetCoordinates(element, e);
         vm.Zoom(p.X, p.Y, e.Delta > 0);
      }

      private void BlocksDown(object sender, MouseButtonEventArgs e) {
         var element = (FrameworkElement)sender;
         var mainModel = ViewModel;
         var imageModel = (IPixelViewModel)element.DataContext;
         var p = e.GetPosition(element);
         p.X /= 16;
         p.Y /= 16;
         mainModel.SelectBlock((int)p.X, (int)p.Y);
      }

      private Point GetCoordinates(FrameworkElement element, MouseEventArgs e) {
         var p = e.GetPosition(element);
         return new(p.X - element.ActualWidth / 2, p.Y - element.ActualHeight / 2);
      }

      #endregion
   }
}
