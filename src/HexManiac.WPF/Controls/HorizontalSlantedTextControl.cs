using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
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

      public static readonly DependencyProperty ColumnWidthProperty = DependencyProperty.Register(nameof(ColumnWidth), typeof(double), typeof(HorizontalSlantedTextControl), new FrameworkPropertyMetadata(0.0, ColumnWidthChanged));

      public double ColumnWidth {
         get { return (double)GetValue(ColumnWidthProperty); }
         set { SetValue(ColumnWidthProperty, value); }
      }

      private static void ColumnWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
         var self = (HorizontalSlantedTextControl)d;
         self.OnColumnWidthChanged(e);
      }

      private void OnColumnWidthChanged(DependencyPropertyChangedEventArgs e) {
         InvalidateVisual();
      }

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

      #region FontSize

      public static readonly DependencyProperty FontSizeProperty = DependencyProperty.Register(nameof(FontSize), typeof(int), typeof(HorizontalSlantedTextControl), new FrameworkPropertyMetadata(0, FontSizeChanged));

      public int FontSize {
         get => (int)GetValue(FontSizeProperty);
         set => SetValue(FontSizeProperty, value);
      }

      private static void FontSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
         var self = (HorizontalSlantedTextControl)d;
         self.OnFontSizeChanged(e);
      }

      private void OnFontSizeChanged(DependencyPropertyChangedEventArgs e) {
         UpdateDesiredHeight();
         InvalidateVisual();
      }

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
         var angle = SlantAngle * Math.PI / 180;
         var heightPerRow = Math.Max(sampleFormat.Width * Math.Sin(angle) + sampleFormat.Height * Math.Cos(angle), sampleFormat.Height);
         var angledTextHeight = sampleFormat.Height * Math.Cos(SlantAngle * Math.PI / 180);
         var pen = new Pen(Brush(nameof(Theme.Backlight)), 1);

         double yOffset = heightPerRow;

         for (var i = 0; i < HeaderRows.Count; i++) {
            var row = HeaderRows[i];
            double xOffset = 70.0;
            var height = ActualHeight - (HeaderRows.Count - i - 1) * heightPerRow;
            foreach (var header in row.ColumnHeaders) {
               var theta = (90 - SlantAngle) * Math.PI / 180;
               var separatorLength = sampleFormat.Width * Math.Cos(SlantAngle * Math.PI / 180) * 2 / 3;
               drawingContext.DrawLine(pen, new Point(xOffset, height), new Point(xOffset + Math.Sin(theta) * separatorLength, height - Math.Cos(theta) * separatorLength));

               xOffset += header.ByteWidth * ColumnWidth / 2;

               var text = Format(header.ColumnTitle);
               var additionalXOffsetForTilt = header.ColumnTitle.Length > 1 ? 3 : -2;
               var additionalYOffsetForTilt = header.ColumnTitle.Length > 1 ? angledTextHeight : sampleFormat.Height;

               drawingContext.PushTransform(new TranslateTransform(xOffset + additionalXOffsetForTilt, yOffset - additionalYOffsetForTilt));
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

      private static SolidColorBrush Brush(string name) {
         return (SolidColorBrush)Application.Current.Resources.MergedDictionaries[0][name];
      }

      private void UpdateDesiredHeight() {
         var maxLength = (HeaderRows?.Count ?? 0) == 0 ? 1 : HeaderRows.Max(row => row.ColumnHeaders.Max(header => header.ColumnTitle.Length));
         var formattedText = Format(new string('0', maxLength));
         var rows = HeaderRows?.Count ?? 1;
         var angle = SlantAngle * Math.PI / 180;
         var heightPerRow = Math.Max(formattedText.Width * Math.Sin(angle) + formattedText.Height * Math.Cos(angle), formattedText.Height);
         var finalHeight = Math.Ceiling(heightPerRow * rows);
         Height = finalHeight;
      }

      private FormattedText Format(string text) {
         var typeface = new Typeface("Consolas");
         return new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            FontSize * 3.0 / 4,
            Brush(nameof(Theme.Secondary)),
            1.0);
      }
   }
}
