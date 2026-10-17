using HavenSoft.HexManiac.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace HavenSoft.HexManiac.WPF.Controls {
   /// <summary>
   /// Lays its children out left to right in rows, like a WrapPanel, with two differences:
   /// a row never holds more than <see cref="MaxPerRow"/> children (5 for the shortcut buttons of the Goto dialog), no matter how much room there is,
   /// and every row is centred, so a short last row sits under the middle of the full rows above it instead of at the left edge.
   /// The rows are broken by <see cref="ShortcutRows"/> (in Core, where it is tested) from the sizes the children really measured,
   /// so the big buttons and the small ones (SmallMode) are laid out by the same rule, and a window too narrow for 5 buttons gets fewer buttons per row instead of cutting one off.
   /// Collapsed children (the shortcuts hidden while the user types in the Goto box) take no room, so the others move together.
   /// The children are arranged in their own order, so Tab walks the buttons row by row, and the arrow keys follow the real positions.
   /// </summary>
   public class CenteredRowsPanel : Panel {
      public static readonly DependencyProperty MaxPerRowProperty = DependencyProperty.Register(
         nameof(MaxPerRow), typeof(int), typeof(CenteredRowsPanel),
         new FrameworkPropertyMetadata(ShortcutRows.DefaultMaxPerRow, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange),
         value => (int)value >= 1);

      /// <summary>The most children in a row (at least 1).</summary>
      public int MaxPerRow {
         get => (int)GetValue(MaxPerRowProperty);
         set => SetValue(MaxPerRowProperty, value);
      }

      // The layout pass is the only user of these; they are kept between passes so that a pass doesn't allocate.
      private readonly List<UIElement> shown = new();
      private readonly List<double> widths = new();
      private readonly List<double> heights = new();
      private readonly List<int> rows = new();

      protected override Size MeasureOverride(Size availableSize) {
         shown.Clear();
         widths.Clear();
         heights.Clear();
         var constraint = new Size(availableSize.Width, double.PositiveInfinity);
         foreach (UIElement child in InternalChildren) {
            if (child == null) continue;
            child.Measure(constraint);
            if (child.Visibility == Visibility.Collapsed) continue;
            if (child.DesiredSize.Width <= 0 && child.DesiredSize.Height <= 0) continue; // the empty item container of a collapsed button takes no room either
            shown.Add(child);
            widths.Add(child.DesiredSize.Width);
            heights.Add(child.DesiredSize.Height);
         }

         ShortcutRows.BreakInto(rows, widths, MaxPerRow, availableSize.Width);

         double widest = 0, total = 0;
         int start = 0;
         foreach (var count in rows) {
            widest = Math.Max(widest, ShortcutRows.RowWidth(widths, start, count));
            total += RowHeight(start, count);
            start += count;
         }
         return new Size(widest, total);
      }

      protected override Size ArrangeOverride(Size finalSize) {
         double y = 0;
         int start = 0;
         foreach (var count in rows) {
            var rowHeight = RowHeight(start, count);
            var x = ShortcutRows.CentreOffset(finalSize.Width, ShortcutRows.RowWidth(widths, start, count));
            for (int i = start; i < start + count && i < shown.Count; i++) {
               shown[i].Arrange(new Rect(x, y, widths[i], rowHeight));
               x += widths[i];
            }
            y += rowHeight;
            start += count;
         }
         return finalSize;
      }

      private double RowHeight(int start, int count) {
         double height = 0;
         for (int i = start; i < start + count && i < heights.Count; i++) height = Math.Max(height, heights[i]);
         return height;
      }
   }
}
