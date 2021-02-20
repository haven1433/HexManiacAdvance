using HavenSoft.HexManiac.Core.ViewModels;
using System.Windows.Controls;
using System.Windows.Input;

namespace HavenSoft.HexManiac.WPF.Controls {
   public partial class ImageEditorView : UserControl {
      private ImageEditorViewModel ViewModel => (ImageEditorViewModel)DataContext;

      public ImageEditorView() {
         InitializeComponent();
         Loaded += (sender, e) => Focus();
         Unloaded += (sender, e) => ClearPopups(default, default);
      }

      private Core.Models.Point Point(MouseEventArgs e) {
         var p = e.GetPosition(ImageContainer);
         p.X -= ImageContainer.ActualWidth / 2;
         p.Y -= ImageContainer.ActualHeight / 2;
         return new Core.Models.Point((int)p.X, (int)p.Y);
      }

      private void MousePrimaryDown(object sender, MouseButtonEventArgs e) {
         PaletteControl.ClosePopup();
         ImageContainer.CaptureMouse();
         ViewModel.ToolDown(Point(e), Keyboard.Modifiers == ModifierKeys.Control);
         Focus();
      }
      private void MouseMiddleDown(object sender, MouseButtonEventArgs e) {
         if (e.ChangedButton != MouseButton.Middle) return;
         ImageContainer.CaptureMouse();
         ViewModel.PanDown(Point(e));
      }
      private void MouseSecondaryDown(object sender, MouseButtonEventArgs e) {
         ImageContainer.CaptureMouse();
         ViewModel.EyeDropperDown(Point(e));
      }
      private void MoveMouse(object sender, MouseEventArgs e) => ViewModel.Hover(Point(e));
      private void MousePrimaryUp(object sender, MouseButtonEventArgs e) {
         if (!ImageContainer.IsMouseCaptured) return;
         ViewModel.ToolUp(Point(e));
         ImageContainer.ReleaseMouseCapture();
      }
      private void MouseMiddleUp(object sender, MouseButtonEventArgs e) {
         if (!ImageContainer.IsMouseCaptured) return;
         if (e.ChangedButton != MouseButton.Middle) return;
         ViewModel.PanUp(Point(e));
         ImageContainer.ReleaseMouseCapture();
      }
      private void MouseSecondaryUp(object sender, MouseButtonEventArgs e) {
         if (!ImageContainer.IsMouseCaptured) return;
         ViewModel.EyeDropperUp(Point(e));
         ImageContainer.ReleaseMouseCapture();
      }
      private void WheelMouse(object sender, MouseWheelEventArgs e) {
         if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) {
            if (e.Delta > 0) ViewModel.SpritePage = (ViewModel.SpritePage + 1) % ViewModel.SpritePages;
            if (e.Delta < 0) ViewModel.SpritePage = ViewModel.SpritePage == 0 ? ViewModel.SpritePages - 1 : ViewModel.SpritePage - 1;
         } else {
            if (e.Delta > 0) ViewModel.ZoomIn(Point(e));
            if (e.Delta < 0) ViewModel.ZoomOut(Point(e));
         }
      }

      private void ClearPopups(object sender, MouseButtonEventArgs e) {
         PaletteControl.ClosePopup();
         PaletteControl.SingleSelect();
         PaletteMixer.ClosePopup();
         PaletteMixer.SingleSelect();
      }
   }
}
