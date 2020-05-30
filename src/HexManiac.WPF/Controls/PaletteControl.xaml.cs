using HavenSoft.HexManiac.Core.ViewModels.Tools;
using HavenSoft.HexManiac.WPF.Windows;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace HavenSoft.HexManiac.WPF.Controls {
   public partial class PaletteControl : UserControl {
      private const int ExpectedElementWidth = 16, ExpectedElementHeight = 16;
      private readonly Duration span = new Duration(TimeSpan.FromMilliseconds(100));
      private Point interactionPoint;

      private PaletteCollection ViewModel => (PaletteCollection)DataContext;

      private int InteractionTileIndex {
         get {
            var x = (int)(interactionPoint.X / ExpectedElementWidth);
            var y = (int)(interactionPoint.Y / ExpectedElementHeight);
            var index = y * ViewModel.ColorWidth + x;
            index = Math.Min(Math.Max(0, index), ViewModel.Elements.Count - 1);
            return index;
         }
      }

      public PaletteControl() => InitializeComponent();

      private void StartPaletteColorMove(object sender, MouseButtonEventArgs e) {
         var view = (FrameworkElement)sender;
         if (e.LeftButton == MouseButtonState.Released) return;
         view.Focus();

         interactionPoint = e.GetPosition(view);
         var tileIndex = InteractionTileIndex;

         if (Keyboard.Modifiers == ModifierKeys.Shift) {
            ViewModel.SelectionEnd = tileIndex;
         } else {
            ViewModel.SelectionStart = tileIndex;
         }

         view.CaptureMouse();
         e.Handled = true;
      }

      private void PaletteColorMove(object sender, MouseEventArgs e) {
         var view = (FrameworkElement)sender;
         if (!view.IsMouseCaptured) return;

         var oldTileIndex = InteractionTileIndex;
         interactionPoint = e.GetPosition(view);
         var newTileIndex = InteractionTileIndex;

         var tilesToAnimate = ViewModel.HandleMove(oldTileIndex, newTileIndex);

         foreach (var (index, direction) in tilesToAnimate) {
            var image = MainWindow.GetChild(view, "PaletteColor", ViewModel.Elements[index]);
            if (!(image.RenderTransform is TranslateTransform)) image.RenderTransform = new TranslateTransform();
            var transform = (TranslateTransform)image.RenderTransform;
            transform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(ExpectedElementWidth * direction, 0, span));
         }
      }

      private void EndPaletteColorMove(object sender, MouseButtonEventArgs e) {
         var view = (FrameworkElement)sender;
         if (!view.IsMouseCaptured) return;
         view.ReleaseMouseCapture();

         ViewModel.CompleteCurrentInteraction();
      }
   }
}
