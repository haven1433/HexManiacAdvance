using HavenSoft.HexManiac.Core.ViewModels;
using System.Windows.Controls;
using System.Windows.Input;

namespace HavenSoft.HexManiac.WPF.Controls {
   public partial class ImageEditorView : UserControl {
      private ImageEditorViewModel ViewModel => (ImageEditorViewModel)DataContext;

      public ImageEditorView() => InitializeComponent();

      private Core.Models.Point Point(MouseEventArgs e) {
         var p = e.GetPosition(ImageContainer);
         p.X -= ImageContainer.ActualWidth / 2;
         p.Y -= ImageContainer.ActualHeight / 2;
         return new Core.Models.Point((int)p.X, (int)p.Y);
      }

      private void MousePrimaryDown(object sender, MouseButtonEventArgs e) {
         ImageContainer.CaptureMouse();
         ViewModel.ToolDown(Point(e));
      }
      private void MouseSecondaryDown(object sender, MouseButtonEventArgs e) {
         ImageContainer.CaptureMouse();
         ViewModel.EyeDropperDown(Point(e));
      }
      private void MoveMouse(object sender, MouseEventArgs e) => ViewModel.Hover(Point(e));
      private void MousePrimaryUp(object sender, MouseButtonEventArgs e) {
         if (!ImageContainer.IsMouseCaptured) return;
         ImageContainer.ReleaseMouseCapture();
         ViewModel.ToolUp(Point(e));
      }
      private void MouseSecondaryUp(object sender, MouseButtonEventArgs e) {
         if (!ImageContainer.IsMouseCaptured) return;
         ImageContainer.ReleaseMouseCapture();
         ViewModel.EyeDropperUp(Point(e));
      }
      private void WheelMouse(object sender, MouseWheelEventArgs e) {
         if (e.Delta > 0) ViewModel.ZoomIn(Point(e));
         if (e.Delta < 0) ViewModel.ZoomOut(Point(e));
      }
   }
}
