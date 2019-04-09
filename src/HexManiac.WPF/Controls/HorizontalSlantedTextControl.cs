using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.WPF.Implementations;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace HavenSoft.HexManiac.WPF.Controls {
   public class HorizontalSlantedTextControl : FrameworkElement {

      #region HeaderRows

      public ObservableCollection<HeaderRow> HeaderRows {
         get { return (ObservableCollection<HeaderRow>)GetValue(HeaderRowsProperty); }
         set { SetValue(HeaderRowsProperty, value); }
      }

      public static readonly DependencyProperty HeaderRowsProperty = DependencyProperty.Register(nameof(HeaderRows), typeof(ObservableCollection<HeaderRow>), typeof(HorizontalSlantedTextControl), new FrameworkPropertyMetadata(null, HeaderRowsChanged));

      private static void HeaderRowsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
         var self = (HorizontalSlantedTextControl)d;
         self.OnHeaderRowsChanged(e);
      }

      private void OnHeaderRowsChanged(DependencyPropertyChangedEventArgs e) {
         var oldCollection = (ObservableCollection<HeaderRow>)e.OldValue;
         if (oldCollection != null) oldCollection.CollectionChanged -= HeaderRowsCollectionChanged;
         var newCollection = (ObservableCollection<HeaderRow>)e.NewValue;
         if (newCollection != null) newCollection.CollectionChanged += HeaderRowsCollectionChanged;
         UpdateDesiredHeight();
         InvalidateVisual();
      }

      private void HeaderRowsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
         UpdateDesiredHeight();
         InvalidateVisual();
      }

      #endregion

      #region ColumnWidth

      public double ColumnWidth {
         get { return (double)GetValue(ColumnWidthProperty); }
         set { SetValue(ColumnWidthProperty, value); }
      }

      public static readonly DependencyProperty ColumnWidthProperty = DependencyProperty.Register(nameof(ColumnWidth), typeof(double), typeof(HorizontalSlantedTextControl), new PropertyMetadata(0.0));

      #endregion

      #region HorizontalOffset

      public double HorizontalOffset {
         get { return (double)GetValue(HorizontalOffsetProperty); }
         set { SetValue(HorizontalOffsetProperty, value); }
      }

      public static readonly DependencyProperty HorizontalOffsetProperty = DependencyProperty.Register("HorizontalOffset", typeof(double), typeof(HorizontalSlantedTextControl), new FrameworkPropertyMetadata(0.0, HorizontalOffsetChanged));

      private static void HorizontalOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
         var self = (HorizontalSlantedTextControl)d;
         self.OnHorizontalOffsetChanged(e);
      }

      private void OnHorizontalOffsetChanged(DependencyPropertyChangedEventArgs e) {
         InvalidateVisual();
      }

      #endregion

      #region SlantAngle

      public double SlantAngle {
         get { return (double)GetValue(SlantAngleProperty); }
         set { SetValue(SlantAngleProperty, value); }
      }

      public static readonly DependencyProperty SlantAngleProperty = DependencyProperty.Register(nameof(SlantAngle), typeof(double), typeof(HorizontalSlantedTextControl), new PropertyMetadata(30.0));

      #endregion

      public HorizontalSlantedTextControl() => ClipToBounds = true;

      protected override void OnRender(DrawingContext drawingContext) {
         base.OnRender(drawingContext);
         if (HeaderRows == null) return;

         // handle horizontal scrolling
         drawingContext.PushTransform(new TranslateTransform(-HorizontalOffset, 0));

         int maxLength = 1;
         if (HeaderRows.Count > 0) maxLength = HeaderRows.Max(row => row.ColumnHeaders.Max(header => header.ColumnTitle.Length));
         var sampleFormat = Format(new string('0', maxLength));
         var heightPerRow = Math.Max(sampleFormat.Width * Math.Sin(SlantAngle * Math.PI / 180), sampleFormat.Height);
         var pen = new Pen(Solarized.Theme.Backlight, 1);

         double yOffset = heightPerRow;

         for (var i = 0; i < HeaderRows.Count; i++) {
            var row = HeaderRows[i];
            double xOffset = 70.0;
            foreach (var header in row.ColumnHeaders) {
               var theta = (90 - SlantAngle) * Math.PI / 180;
               var separatorLength = sampleFormat.Width * Math.Cos(SlantAngle * Math.PI / 180) * 2 / 3;
               var height = ActualHeight - (HeaderRows.Count - i - 1) * heightPerRow;
               drawingContext.DrawLine(pen, new Point(xOffset, height), new Point(xOffset + Math.Sin(theta) * separatorLength, height - Math.Cos(theta) * separatorLength));

               xOffset += header.ByteWidth * ColumnWidth / 2;

               var text = Format(header.ColumnTitle);
               var additionalOffsetForTilt = header.ColumnTitle.Length > 1 ? 3 : -2;

               drawingContext.PushTransform(new TranslateTransform(xOffset + additionalOffsetForTilt, yOffset));
               if (header.ColumnTitle.Length > 1) drawingContext.PushTransform(new RotateTransform(-SlantAngle));
               drawingContext.DrawText(text, new Point());
               if (header.ColumnTitle.Length > 1) drawingContext.Pop();
               drawingContext.Pop();

               xOffset += header.ByteWidth * ColumnWidth / 2;
            }
            yOffset += heightPerRow;
         }

         drawingContext.Pop();
      }

      private void UpdateDesiredHeight() {
         var maxLength = (HeaderRows?.Count ?? 0) == 0 ? 1 : HeaderRows.Max(row => row.ColumnHeaders.Max(header => header.ColumnTitle.Length));
         var formattedText = Format(new string('0', maxLength));
         var rows = HeaderRows?.Count ?? 0;
         var heightPerRow = Math.Max(formattedText.Width * Math.Sin(SlantAngle * Math.PI / 180), formattedText.Height);
         var finalHeight = Math.Ceiling(heightPerRow * rows) + 10;
         Height = finalHeight;
      }

      private FormattedText Format(string text) {
         var typeface = new Typeface("Consolas");
         return new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            FormatDrawer.FontSize * 3 / 4,
            Solarized.Theme.Secondary,
            1.0);
      }
   }
}
