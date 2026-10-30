using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.WPF.Windows;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ModelPoint = HavenSoft.HexManiac.Core.Models.Point;

namespace HavenSoft.HexManiac.WPF.Controls {
   public partial class DexReorderView {
      private readonly Duration span = new Duration(TimeSpan.FromMilliseconds(100));

      private DexReorderTab ViewModel => DataContext as DexReorderTab;

      public DexReorderView() => InitializeComponent();

      private int ExpectedElementWidth => (int)(64 * ViewModel.SpriteScale) + 2;
      private int ExpectedElementHeight => (int)(64 * ViewModel.SpriteScale) + 2;

      private int ToTile(Point p) {
         var tileWidth = (int)(Container.ActualWidth / ExpectedElementWidth);
         var newTileX = (int)(p.X / ExpectedElementWidth);
         var newTileY = (int)(p.Y / ExpectedElementHeight);
         var tileIndex = newTileY * tileWidth + newTileX;
         return tileIndex.LimitToRange(0, Container.Items.Count);
      }

      private Point interactionPoint;
      private void StartElementMove(object sender, MouseButtonEventArgs e) {
         if (e.LeftButton == MouseButtonState.Released) return;
         interactionPoint = e.GetPosition(Container);
         var tileIndex = ToTile(interactionPoint);

         if (Keyboard.Modifiers == ModifierKeys.Shift) {
            ViewModel.SelectionEnd = tileIndex;
         } else {
            ViewModel.SelectionStart = tileIndex;
         }

         Container.CaptureMouse();
      }

      private void ElementMove(object sender, MouseEventArgs e) {
         if (!Container.IsMouseCaptured) return;
         var oldTileIndex = ToTile(interactionPoint);

         interactionPoint = e.GetPosition(Container);
         var newTileIndex = ToTile(interactionPoint);

         var tilesToAnimate = ViewModel.HandleMove(oldTileIndex, newTileIndex);

         foreach (var (index, direction) in tilesToAnimate) {
            var image = MainWindow.GetChild(Container, "PixelImage", ViewModel.Elements[index]);
            if (!(image.RenderTransform is TranslateTransform)) image.RenderTransform = new TranslateTransform();
            var transform = (TranslateTransform)image.RenderTransform;
            transform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(ExpectedElementWidth * direction, 0, span));
         }
      }

      private void EndElementMove(object sender, MouseButtonEventArgs e) {
         if (!Container.IsMouseCaptured) return;
         Container.ReleaseMouseCapture();
         ViewModel.CompleteCurrentInteraction();
      }

      private void ElementScroll(object sender, MouseWheelEventArgs e) {
         if (Keyboard.Modifiers != ModifierKeys.Control) return;
         ViewModel.SpriteScale *= Math.Sign(e.Delta) > 0 ? 2 : .5;
         e.Handled = true;
      }
   }
}
