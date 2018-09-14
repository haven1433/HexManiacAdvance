using HavenSoft.Gen3Hex.ViewModel;
using HavenSoft.ViewModel.DataFormats;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
            oldViewPort.PropertyChanged -= OnViewPortPropertyChanged;
            oldViewPort.CollectionChanged -= OnViewPortContentChanged;
         }

         if (e.NewValue is ViewPort newViewPort) {
            newViewPort.PropertyChanged += OnViewPortPropertyChanged;
            newViewPort.CollectionChanged += OnViewPortContentChanged;
         }

         this.InvalidateVisual();
      }

      #endregion

      protected override void OnRender(DrawingContext drawingContext) {
         base.OnRender(drawingContext);

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

         ViewPort.Width = (int)sizeInfo.NewSize.Width / CellWidth;
         ViewPort.Height = (int)sizeInfo.NewSize.Height / CellHeight;
      }

      private void OnViewPortPropertyChanged(object sender, PropertyChangedEventArgs e) {
         if (e.PropertyName == nameof(ViewPort.Width) || e.PropertyName == nameof(ViewPort.Height)) {
            this.InvalidateVisual();
         }
      }

      private void OnViewPortContentChanged(object sender, NotifyCollectionChangedEventArgs e) {
         this.InvalidateVisual();
      }

      private class FormatDrawer : IDataFormatVisitor {
         public static readonly Point CellTextOffset = new Point(4, 3);

         private static readonly List<FormattedText> noneVisualCache = new List<FormattedText>();

         private readonly DrawingContext context;

         public FormatDrawer(DrawingContext drawingContext) => context = drawingContext;

         public void Visit(Undefined dataFormat, byte data) {
            // intentionally draw nothing
         }

         public void Visit(None dataFormat, byte data) {
            VerifyNoneVisualCache();
            context.DrawText(noneVisualCache[data], CellTextOffset);
         }

         private void VerifyNoneVisualCache() {
            if (noneVisualCache.Count != 0) return;

            var bytesAsHex = Enumerable.Range(0, 0x100).Select(i => i.ToString("X2"));

            var text = bytesAsHex.Select(hex => new FormattedText(
               hex,
               CultureInfo.CurrentCulture,
               FlowDirection.LeftToRight,
               new Typeface("Consolas"),
               FontSize,
               Brushes.Black,
               1.0));

            noneVisualCache.AddRange(text);
         }
      }
   }
}
