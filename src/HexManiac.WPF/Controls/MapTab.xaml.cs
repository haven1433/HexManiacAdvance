using HavenSoft.HexManiac.Core.ViewModels.Map;
using System.Windows;
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

      private bool withinMapInteraction;

      private void LeftDown(object sender, MouseButtonEventArgs e) {
         var element = (FrameworkElement)sender;
         var vm = (MapEditorViewModel)element.DataContext;
         var p = GetCoordinates(element, e);
         element.CaptureMouse();
         withinMapInteraction = true;
         vm.LeftDown(p.X, p.Y);
      }

      private void LeftMove(object sender, MouseEventArgs e) {
         if (!withinMapInteraction) return;
         var element = (FrameworkElement)sender;
         var vm = (MapEditorViewModel)element.DataContext;
         var p = GetCoordinates(element, e);
         vm.LeftMove(p.X, p.Y);
      }

      private void LeftUp(object sender, MouseButtonEventArgs e) {
         var element = (FrameworkElement)sender;
         element.ReleaseMouseCapture();
         if (!withinMapInteraction) return;
         withinMapInteraction = false;
         var vm = (MapEditorViewModel)element.DataContext;
         var p = GetCoordinates(element, e);
         vm.LeftUp(p.X, p.Y);
      }

      private void Wheel(object sender, MouseWheelEventArgs e) {
         var element = (FrameworkElement)sender;
         var vm = (MapEditorViewModel)element.DataContext;
         var p = GetCoordinates(element, e);
         vm.Zoom(p.X, p.Y, e.Delta > 0);
      }

      private Point GetCoordinates(FrameworkElement element, MouseEventArgs e) {
         var p = e.GetPosition(element);
         return new(p.X - element.ActualWidth / 2, p.Y - element.ActualHeight / 2);
      }

      #endregion
   }
}
