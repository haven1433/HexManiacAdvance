using HavenSoft.Gen3Hex.ViewModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace HavenSoft.Gen3Hex.View {
   public class HexContent : FrameworkElement {
      public const int
         FontSize = 16,
         CellWidth = 30,
         CellHeight = 20;

      public static readonly Point CellTextOffset = new Point(4, 3);

      public const string Hex = "0123456789ABCDEF";

      private readonly List<FormattedText> byteVisualCache = new List<FormattedText>();

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
         if (e.OldValue is INotifyPropertyChanged oldContext) {
            oldContext.PropertyChanged -= OnViewPortPropertyChanged;
         }

         if (e.NewValue is INotifyPropertyChanged newContext) {
            newContext.PropertyChanged += OnViewPortPropertyChanged;
         }

         this.InvalidateVisual();
      }

      #endregion

      protected override void OnRender(DrawingContext drawingContext) {
         base.OnRender(drawingContext);

         VerifyByteVisualCache();

         for (int x = 0; x < ViewPort.Width; x++) {
            for (int y = 0; y < ViewPort.Height; y++) {
               var element = ViewPort[x, y];
               var origin = new Point(x * CellWidth + CellTextOffset.X, y * CellHeight + CellTextOffset.Y);
               drawingContext.DrawText(byteVisualCache[element.Value], origin);
            }
         }
      }

      protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo) {
         base.OnRenderSizeChanged(sizeInfo);

         ViewPort.Width = (int)sizeInfo.NewSize.Width / CellWidth;
         ViewPort.Height = (int)sizeInfo.NewSize.Height / CellHeight;
      }

      private void OnViewPortPropertyChanged(object sender, PropertyChangedEventArgs e) {
         if (e.PropertyName == nameof(ViewPort.Width) || e.PropertyName == nameof(ViewPort.Height)) {
            this.InvalidateVisual();
         }
      }

      private void VerifyByteVisualCache() {
         if (byteVisualCache.Count != 0) return;

         var bytesAsHex = Enumerable.Range(0, 255).Select(i => $"{Hex[i / 0x10]}{Hex[i % 0x10]}");

         var text = bytesAsHex.Select(hex => new FormattedText(
            hex,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Consolas"),
            FontSize,
            Brushes.Black,
            1.0));

         byteVisualCache.AddRange(text);
      }
   }
}
