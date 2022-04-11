using HavenSoft.HexManiac.Core;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace HavenSoft.HexManiac.WPF.Controls {
   /// <summary>
   /// A cross between a WrapPanel and a StackPanel.
   /// Like a WrapPanel, new elements are added left-to-right, and further elements appear below the first row.
   /// However, each column calculates its height separately:
   ///   in a 3-column layout, the 4th element will appear in the leftmost column,
   ///   directly after the end of the 1st element, ignoring the size of the 2nd and 3rd elemenst.
   /// </summary>
   public class ColumnStackPanel : Panel {
      /// <summary>
      /// During Measure, this grows based on the number of Headers found.
      /// During Arrange, this limits the maximum number of columns.
      /// </summary>
      private int expectedHeaderCount = 0;

      #region IsHeader

      /// <summary>
      /// Attached property for children within the panel. Set to 'true' in order to move to the next column.
      /// </summary>
      public static readonly DependencyProperty IsHeaderProperty = DependencyProperty.RegisterAttached("IsHeader", typeof(bool), typeof(ColumnStackPanel), new PropertyMetadata(false));

      public static bool GetIsHeader(DependencyObject depObj) => (bool)depObj.GetValue(IsHeaderProperty);
      public static void SetIsHeader(DependencyObject depObj, bool value) => depObj.SetValue(IsHeaderProperty, value);

      #endregion

      #region MinimumColumnWidth

      /// <summary>
      /// The minimum width needed for a column. The number of columns will scale based on available width.
      /// </summary>
      public static readonly DependencyProperty MinimumColumnWidthProperty = DependencyProperty.Register(nameof(MinimumColumnWidth), typeof(double), typeof(ColumnStackPanel), new FrameworkPropertyMetadata(32d, MinimumColumnWidthChanged));

      public double MinimumColumnWidth {
         get => (double)GetValue(MinimumColumnWidthProperty);
         set => SetValue(MinimumColumnWidthProperty, value);
      }

      private static void MinimumColumnWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
         var self = (ColumnStackPanel)d;
         self.OnMinimumColumnWidthChanged(e);
      }

      protected virtual void OnMinimumColumnWidthChanged(DependencyPropertyChangedEventArgs e) => InvalidateMeasure();

      #endregion

      #region ColumnMargin

      /// <summary>
      /// The space between columns. Should be left empty.
      /// </summary>
      public static readonly DependencyProperty ColumnMarginProperty = DependencyProperty.Register(nameof(ColumnMargin), typeof(double), typeof(ColumnStackPanel), new FrameworkPropertyMetadata(0d, ColumnMarginChanged));

      public double ColumnMargin {
         get => (double)GetValue(ColumnMarginProperty);
         set => SetValue(ColumnMarginProperty, value);
      }

      private static void ColumnMarginChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
         var self = (ColumnStackPanel)d;
         self.OnColumnMarginChanged(e);
      }

      protected virtual void OnColumnMarginChanged(DependencyPropertyChangedEventArgs e) { }

      #endregion

      #region HeaderMargin

      /// <summary>
      /// The space between the end of one section and the start of a new section below it within the same column.
      /// </summary>
      public static readonly DependencyProperty HeaderMarginProperty = DependencyProperty.Register(nameof(HeaderMargin), typeof(double), typeof(ColumnStackPanel), new FrameworkPropertyMetadata(0d, HeaderMarginChanged));

      public double HeaderMargin {
         get => (double)GetValue(HeaderMarginProperty);
         set => SetValue(HeaderMarginProperty, value);
      }

      private static void HeaderMarginChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
         var self = (ColumnStackPanel)d;
         self.OnHeaderMarginChanged(e);
      }

      protected virtual void OnHeaderMarginChanged(DependencyPropertyChangedEventArgs e) { }

      #endregion

      protected override Size MeasureOverride(Size availableSize) {
         // count header elements among children
         expectedHeaderCount = 0;
         foreach (UIElement child in InternalChildren) { if (GetContentIsHeader(child)) expectedHeaderCount += 1; }

         var (widthPerColumn, desiredColumnCount) = CalculateColumnWidth(availableSize, expectedHeaderCount);
         widthPerColumn = Math.Max(widthPerColumn, MinimumColumnWidth);
         var offerSize = new Size(widthPerColumn, availableSize.Height);

         // calculate the desired height of each column
         var desiredHeights = new double[desiredColumnCount];
         var activeColumn = -1;
         foreach (UIElement child in InternalChildren) {
            if (GetContentIsHeader(child)) {
               activeColumn = (activeColumn + 1) % desiredColumnCount; // desiredHeights.IndexOf(desiredHeights.Min());
               if (desiredHeights[activeColumn] > 0) desiredHeights[activeColumn] += HeaderMargin;
            }
            if (activeColumn < 0) activeColumn = 0;
            child.Measure(offerSize);
            desiredHeights[activeColumn] += child.DesiredSize.Height;
         }

         return new Size(widthPerColumn * desiredColumnCount + ColumnMargin * (desiredColumnCount - 1), desiredHeights.Max());
      }

      protected override Size ArrangeOverride(Size finalSize) {
         var (widthPerColumn, desiredColumnCount) = CalculateColumnWidth(finalSize, expectedHeaderCount);

         // calculate the desired height of each column
         var usedHeight = new double[desiredColumnCount];
         var activeColumn = -1;
         foreach (UIElement child in InternalChildren) {
            if (GetContentIsHeader(child)) {
               activeColumn = (activeColumn + 1) % desiredColumnCount; // usedHeight.IndexOf(usedHeight.Min());
               if (usedHeight[activeColumn] > 0) usedHeight[activeColumn] += HeaderMargin;
            }
            if (activeColumn < 0) activeColumn = 0;
            child.Arrange(new Rect(activeColumn * (widthPerColumn + ColumnMargin), usedHeight[activeColumn], widthPerColumn, child.DesiredSize.Height));
            usedHeight[activeColumn] += child.RenderSize.Height;
         }

         return new Size(widthPerColumn * desiredColumnCount + ColumnMargin * (desiredColumnCount - 1), usedHeight.Max());
      }

      private bool GetContentIsHeader(UIElement child) {
         return true;
         //while (
         //   child is ContentPresenter contentPresenter &&
         //   !GetIsHeader(child) &&
         //   VisualTreeHelper.GetChildrenCount(child) > 0
         //) {
         //   child = VisualTreeHelper.GetChild(child, 0) as UIElement;
         //}

         //return GetIsHeader(child);
      }

      private (double columnWidth, int columnCount) CalculateColumnWidth(Size offer, int maxColumns = int.MaxValue) {
         // make sure we have a reasonable size to work with
         var columnWidth = Math.Max(MinimumColumnWidth, 16);
         var availableWidth = offer.Width;
         if (double.IsNaN(availableWidth) || double.IsPositiveInfinity(availableWidth)) availableWidth = columnWidth * 2;
         var desiredColumnCount = (int)((availableWidth + ColumnMargin) / (columnWidth + ColumnMargin));
         if (maxColumns < 1) maxColumns = 1;
         desiredColumnCount = desiredColumnCount.LimitToRange(1, maxColumns);
         var widthPerColumn = (availableWidth + ColumnMargin) / desiredColumnCount - ColumnMargin;
         return (widthPerColumn, desiredColumnCount);
      }
   }
}
