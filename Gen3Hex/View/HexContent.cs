using HavenSoft.Gen3Hex.ViewModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Media;

namespace HavenSoft.Gen3Hex.View {
   public class HexContent : FrameworkElement {
      public const int CellWidth = 30, CellHeight = 20;

      #region ViewPort

      public ViewPort ViewPort {
         get { return (ViewPort)GetValue(ViewPortProperty); }
         set { SetValue(ViewPortProperty, value); }
      }

      public static readonly DependencyProperty ViewPortProperty = DependencyProperty.Register("ViewPort", typeof(ViewPort), typeof(HexContent), new FrameworkPropertyMetadata(null, ViewPortChanged));

      private static void ViewPortChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
         var self = (HexContent)d;
         self.OnViewPortChanged(e);
      }

      private void OnViewPortChanged(DependencyPropertyChangedEventArgs e) {
         if (e.OldValue is ViewPort oldViewPort) {
            oldViewPort.CollectionChanged -= OnViewPortContentChanged;
         }

         if (e.NewValue is ViewPort newViewPort) {
            newViewPort.CollectionChanged += OnViewPortContentChanged;
            UpdateViewPortSize();
         }

         InvalidateVisual();
      }

      #endregion

      protected override void OnRender(DrawingContext drawingContext) {
         base.OnRender(drawingContext);
         if (ViewPort == null) return;
         drawingContext.DrawRectangle(Solarized.Theme.Background, null, new Rect(0, 0, ActualWidth, ActualHeight));

         var visitor = new FormatDrawer(drawingContext);

         for (int x = 0; x < ViewPort.Width; x++) {
            for (int y = 0; y < ViewPort.Height; y++) {
               var element = ViewPort[x, y];
               drawingContext.PushTransform(new TranslateTransform(x * CellWidth, y * CellHeight));
               element.Format.Visit(visitor, element.Value);
               drawingContext.Pop();
            }
         }
      }

      protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo) {
         base.OnRenderSizeChanged(sizeInfo);
         if (ViewPort != null) UpdateViewPortSize();
      }

      private void UpdateViewPortSize() {
         ViewPort.Width = (int)ActualWidth / CellWidth;
         ViewPort.Height = (int)ActualHeight / CellHeight;
      }

      private void OnViewPortContentChanged(object sender, NotifyCollectionChangedEventArgs e) {
         InvalidateVisual();
      }
   }
}
