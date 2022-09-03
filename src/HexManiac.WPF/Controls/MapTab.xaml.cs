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
         var p = e.GetPosition(element);
         element.CaptureMouse();
         withinMapInteraction = true;
         vm.LeftDown(p.X, p.Y);
      }

      private void LeftMove(object sender, MouseEventArgs e) {
         if (!withinMapInteraction) return;
         var element = (FrameworkElement)sender;
         var vm = (MapEditorViewModel)element.DataContext;
         var p = e.GetPosition(element);
         vm.LeftMove(p.X, p.Y);
      }

      private void LeftUp(object sender, MouseButtonEventArgs e) {
         var element = (FrameworkElement)sender;
         element.ReleaseMouseCapture();
         if (!withinMapInteraction) return;
         withinMapInteraction = false;
         var vm = (MapEditorViewModel)element.DataContext;
         var p = e.GetPosition(element);
         vm.LeftUp(p.X, p.Y);
      }

      #endregion
   }
}
