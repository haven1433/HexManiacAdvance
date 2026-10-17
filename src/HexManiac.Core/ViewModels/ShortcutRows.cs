using System;
using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.ViewModels {
   /// <summary>
   /// The line breaking of the big shortcut buttons of the Goto dialog (Pokemon, Items, Maps...), kept apart from the WPF panel that uses it so it can be tested.
   /// A row holds at most <see cref="DefaultMaxPerRow"/> buttons, and never more than fit in the width the window gives the buttons.
   /// Every row is centred, so the last (shorter) row sits under the middle of the rows above it.
   /// </summary>
   public static class ShortcutRows {
      /// <summary>The most buttons in one row. Nine buttons in one long line looked too wide, so the 6th button starts the next row.</summary>
      public const int DefaultMaxPerRow = 5;

      // widths are sums of a few whole numbers of device independent pixels: this only absorbs the error of adding fractions
      private const double Tolerance = 0.001;

      /// <summary>
      /// How many buttons the row that starts at 'start' holds (0 only when there are no buttons left).
      /// A row has at least one button, even a button wider than the space: the panel can't make it smaller, and it must not loop forever on it.
      /// A width of infinity (or NaN) means the space is not limited, so only 'maxPerRow' counts.
      /// </summary>
      public static int CountInRow(IReadOnlyList<double> widths, int start, int maxPerRow, double availableWidth) {
         if (widths == null || start < 0 || start >= widths.Count) return 0;
         maxPerRow = Math.Max(1, maxPerRow);
         var limited = !double.IsNaN(availableWidth) && !double.IsInfinity(availableWidth);
         var count = 0;
         var used = 0.0;
         while (start + count < widths.Count && count < maxPerRow) {
            var width = ButtonWidth(widths[start + count]);
            if (count > 0 && limited && used + width > availableWidth + Tolerance) break;
            used += width;
            count++;
         }
         return count;
      }

      /// <summary>The number of buttons of every row, top to bottom.</summary>
      public static List<int> Break(IReadOnlyList<double> widths, int maxPerRow, double availableWidth) {
         var rows = new List<int>();
         BreakInto(rows, widths, maxPerRow, availableWidth);
         return rows;
      }

      /// <summary>Same as <see cref="Break"/>, but fills a list the caller keeps (the panel breaks its rows at every layout pass without making a new list).</summary>
      public static void BreakInto(List<int> rows, IReadOnlyList<double> widths, int maxPerRow, double availableWidth) {
         rows.Clear();
         if (widths == null) return;
         for (int start = 0; start < widths.Count;) {
            var count = CountInRow(widths, start, maxPerRow, availableWidth);
            if (count < 1) break; // can't happen (a row always holds one button), but never loop forever
            rows.Add(count);
            start += count;
         }
      }

      /// <summary>The sum of the widths of 'count' buttons starting at 'start'.</summary>
      public static double RowWidth(IReadOnlyList<double> widths, int start, int count) {
         var total = 0.0;
         for (int i = start; i < start + count && i < widths.Count; i++) total += ButtonWidth(widths[i]);
         return total;
      }

      /// <summary>A button without a usable width (negative or NaN) takes no space. Math.Max(0, NaN) is NaN, which would make every offset of the row NaN.</summary>
      private static double ButtonWidth(double width) => width > 0 ? width : 0;

      /// <summary>How far from the left edge a row of 'rowWidth' starts to sit in the middle of 'containerWidth'. A row wider than the container starts at the edge (it is clipped on the right, never on the left).</summary>
      public static double CentreOffset(double containerWidth, double rowWidth) {
         if (double.IsNaN(containerWidth) || double.IsInfinity(containerWidth)) return 0;
         return Math.Max(0, (containerWidth - rowWidth) / 2);
      }
   }
}
