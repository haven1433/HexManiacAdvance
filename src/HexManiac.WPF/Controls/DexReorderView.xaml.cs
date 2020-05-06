using HavenSoft.HexManiac.Core.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace HavenSoft.HexManiac.WPF.Controls {
   public partial class DexReorderView {
      public DexReorderView() => InitializeComponent();

      private const int ExpectedElementWidth = 64, ExpectedElementHeight = 64;

      private Point interactionPoint;
      private void StartElementMove(object sender, MouseButtonEventArgs e) {
         if (e.LeftButton == MouseButtonState.Released) return;
         interactionPoint = e.GetPosition(Container);
         Container.CaptureMouse();
      }

      private void ElementMove(object sender, MouseEventArgs e) {
         if (!Container.IsMouseCaptured) return;
         var tileWidth = (int)(ActualWidth / ExpectedElementWidth);

         var oldTileX = (int)(interactionPoint.X / ExpectedElementWidth);
         var oldTileY = (int)(interactionPoint.Y / ExpectedElementHeight);
         var oldTileIndex = oldTileY * tileWidth + oldTileX;

         interactionPoint = e.GetPosition(Container);
         var newTileX = (int)(interactionPoint.X / ExpectedElementWidth);
         var newTileY = (int)(interactionPoint.Y / ExpectedElementHeight);
         var newTileIndex = newTileY * tileWidth + newTileX;

         var viewModel = (DexReorderTab)DataContext;
         viewModel.HandleMove(oldTileIndex, newTileIndex);
      }

      private void EndElementMove(object sender, MouseButtonEventArgs e) {
         if (!Container.IsMouseCaptured) return;
         Container.ReleaseMouseCapture();
         var viewModel = (DexReorderTab)DataContext;
         viewModel.CompleteCurrentInteraction();
      }
   }
}
