using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.WPF.Windows;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace HavenSoft.HexManiac.WPF.Controls {
   public partial class DexReorderView {
      private readonly Duration span = new Duration(TimeSpan.FromMilliseconds(100));

      public DexReorderView() => InitializeComponent();

      private const int ExpectedElementWidth = 66, ExpectedElementHeight = 66;

      private Point interactionPoint;
      private void StartElementMove(object sender, MouseButtonEventArgs e) {
         if (e.LeftButton == MouseButtonState.Released) return;
         interactionPoint = e.GetPosition(Container);
         Container.CaptureMouse();
      }

      private void ElementMove(object sender, MouseEventArgs e) {
         if (!Container.IsMouseCaptured) return;
         var tileWidth = (int)(Container.ActualWidth / ExpectedElementWidth);

         var oldTileX = (int)(interactionPoint.X / ExpectedElementWidth);
         var oldTileY = (int)(interactionPoint.Y / ExpectedElementHeight);
         var oldTileIndex = oldTileY * tileWidth + oldTileX;

         interactionPoint = e.GetPosition(Container);
         var newTileX = (int)(interactionPoint.X / ExpectedElementWidth);
         var newTileY = (int)(interactionPoint.Y / ExpectedElementHeight);
         var newTileIndex = newTileY * tileWidth + newTileX;

         var viewModel = (DexReorderTab)DataContext;
         oldTileIndex = Math.Min(Math.Max(0, oldTileIndex), Container.Items.Count - 1);
         newTileIndex = Math.Min(Math.Max(0, newTileIndex), Container.Items.Count - 1);
         var tilesToAnimate = viewModel.HandleMove(oldTileIndex, newTileIndex);

         foreach(var tile in tilesToAnimate) {
            var image = MainWindow.GetChild(Container, "PixelImage", viewModel.Elements[tile.index]);
            if (!(image.RenderTransform is TranslateTransform)) image.RenderTransform = new TranslateTransform();
            var transform = (TranslateTransform)image.RenderTransform;
            transform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(ExpectedElementWidth * tile.direction, 0, span));
         }
      }

      private void EndElementMove(object sender, MouseButtonEventArgs e) {
         if (!Container.IsMouseCaptured) return;
         Container.ReleaseMouseCapture();
         var viewModel = (DexReorderTab)DataContext;
         viewModel.CompleteCurrentInteraction();
      }
   }
}
