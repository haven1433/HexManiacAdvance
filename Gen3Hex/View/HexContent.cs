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
         CellWidth = 30,
         CellHeight = 20;

      public const string Hex = "0123456789ABCDEF";

      private readonly List<FormattedText> byteVisualCache = new List<FormattedText>();

      private ViewPort ViewModel => (ViewPort)DataContext;

      public HexContent() {
         DataContextChanged += OnDataContextChanged;
      }

      protected override void OnRender(DrawingContext drawingContext) {
         base.OnRender(drawingContext);

         var viewPort = ViewModel;

         VerifyByteVisualCache();

         for (int x = 0; x < viewPort.Width; x++) {
            for (int y = 0; y < viewPort.Height; y++) {
               var element = viewPort[x, y];
               drawingContext.DrawText(byteVisualCache[element.Value], new Point(x * CellWidth, y * CellHeight));
            }
         }
      }

      protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo) {
         base.OnRenderSizeChanged(sizeInfo);

         ViewModel.Width = (int)sizeInfo.NewSize.Width / CellWidth;
         ViewModel.Height = (int)sizeInfo.NewSize.Height / CellHeight;
      }

      private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
         if (e.OldValue is INotifyPropertyChanged oldContext) {
            oldContext.PropertyChanged -= OnDataContextPropertyChanged;
         }

         if(e.NewValue is INotifyPropertyChanged newContext) {
            newContext.PropertyChanged += OnDataContextPropertyChanged;
         }

         this.InvalidateVisual();
      }

      private void OnDataContextPropertyChanged(object sender, PropertyChangedEventArgs e) {
         if (e.PropertyName == nameof(ViewModel.Width) || e.PropertyName == nameof(ViewModel.Height)) {
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
            16,
            Brushes.Black,
            1.0));

         byteVisualCache.AddRange(text);
      }
   }
}
