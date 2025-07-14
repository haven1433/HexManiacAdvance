using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace HavenSoft.HexManiac.Core.ViewModels {
   public delegate (Point start, Point end) GetSelectionSpan(Point p);

   public class JumpInfo {
      public int ViewStart { get; }
      public int SelectionStart { get; }
      public ITabContent Tab { get; }
      public JumpInfo(int viewStart, int selectionStart, ITabContent tab = null) {
         (ViewStart, SelectionStart) = (viewStart, selectionStart);
         Tab = tab;
      }
   }

   public record SelectionTabChangeArgs(ITabContent Tab, bool IsBackArrow);

   public class Selection : ViewModelCore {
      public const int DefaultPreferredWidth = 0x10;

      public readonly IDataModel model;
      public readonly ChangeHistory<ModelDelta> history;
      public readonly GetSelectionSpan getSpan;

      // these back/forward stacks are not encapsulated in a history object because we want to be able to change a remembered address each time we visit it.
      // if we navigate back, then scroll, then navigate forward, we want to remember the scroll if we go back again.
      public readonly Stack<JumpInfo> backStack = new Stack<JumpInfo>(), forwardStack = new Stack<JumpInfo>();

      public int preferredWidth = DefaultPreferredWidth, maxWidth = 4;

      public Point rawSelectionStart; // the actual click point
      public Point selectionStart;    // the calculated selection start, which may differ depending on the SelectionSpan
      public Point rawSelectionEnd;   // the actual release point
      public Point selectionEnd;      // the calculated selection end, which may differ depending on the SelectionSpan

      public Point SelectionStart {
         get => selectionStart;
         set {
            var index = Scroll.ViewPointToDataIndex(value);
            var max = Scroll.IsSingleTableMode ? Scroll.DataLength - 1 : Scroll.DataLength;
            value = Scroll.DataIndexToViewPoint(index.LimitToRange(Scroll.DataStart, max));

            if (selectionStart.Equals(value)) return;

            if (!Scroll.ScrollToPoint(ref value)) {
               PreviewSelectionStartChanged?.Invoke(this, selectionStart);
            }

            rawSelectionStart = value;
            rawSelectionEnd = value;
            var (start, end) = getSpan(rawSelectionStart);
            TryUpdate(ref selectionStart, start);
            TryUpdate(ref selectionEnd, end, nameof(SelectionEnd));
            UpdateHeaderSelection();
         }
      }

      public Point SelectionEnd {
         get => selectionEnd;
         set {
            var index = Scroll.ViewPointToDataIndex(value);
            var max = Scroll.IsSingleTableMode ? Scroll.DataLength - 1 : Scroll.DataLength;
            value = Scroll.DataIndexToViewPoint(index.LimitToRange(Scroll.DataStart, max));

            if (selectionEnd.Equals(value)) return;

            Scroll.ScrollToPoint(ref value);

            rawSelectionEnd = value;
            var startIndex = Scroll.ViewPointToDataIndex(rawSelectionStart);
            var endIndex = Scroll.ViewPointToDataIndex(rawSelectionEnd);

            UpdateHeaderSelection();

            // case 1: start/end are the same
            if (startIndex == endIndex) {
               var (start, end) = getSpan(rawSelectionStart);
               TryUpdate(ref selectionStart, start, nameof(SelectionStart));
               TryUpdate(ref selectionEnd, end);
               return;
            }

            // case 2: start < end
            if (startIndex < endIndex) {
               TryUpdate(ref selectionStart, getSpan(rawSelectionStart).start, nameof(SelectionStart));
               TryUpdate(ref selectionEnd, getSpan(rawSelectionEnd).end);
               return;
            }

            // case 3: start > end
            if (startIndex > endIndex) {
               TryUpdate(ref selectionEnd, getSpan(rawSelectionEnd).start);
               TryUpdate(ref selectionStart, getSpan(rawSelectionStart).end, nameof(SelectionStart));
               return;
            }
         }
      }

      public void UpdateHeaderSelection() {
         if (Scroll?.Headers == null) return;
         for (int i = 0; i < Scroll.Headers.Count; i++) {
            Scroll.Headers[i].IsSelected = i == rawSelectionStart.Y && i == rawSelectionEnd.Y;
         }
      }

      public int PreferredWidth {
         get => preferredWidth;
         set {
            if (TryUpdate(ref preferredWidth, value)) ChangeWidth(maxWidth);
         }
      }

      public bool autoAdjustDataWidth = true, allowMultipleElementsPerLine = false;
      public bool AutoAdjustDataWidth { get => autoAdjustDataWidth; set => Set(ref autoAdjustDataWidth, value); }
      public bool AllowMultipleElementsPerLine { get => allowMultipleElementsPerLine; set => Set(ref allowMultipleElementsPerLine, value); }

      public ScrollRegion Scroll { get; }

      public event EventHandler<SelectionTabChangeArgs> RequestTabChanged;
      public event EventHandler<string> OnError;

      /// <summary>
      /// The owner may have something special going on with the selected point.
      /// Warn the owner before the selection changes, in case they need to do cleanup.
      /// Note that this function is expected to pass the _old_ selection value
      /// </summary>
      public event EventHandler<Point> PreviewSelectionStartChanged;

      public bool IsSelected(Point point) {
         if (point.X < 0 || point.X >= Scroll.Width) return false;

         var selectionStart = Scroll.ViewPointToDataIndex(SelectionStart);
         var selectionEnd = Scroll.ViewPointToDataIndex(SelectionEnd);
         var middle = Scroll.ViewPointToDataIndex(point);

         var leftEdge = Math.Min(selectionStart, selectionEnd);
         var rightEdge = Math.Max(selectionStart, selectionEnd);

         return leftEdge <= middle && middle <= rightEdge;
      }

      /// <summary>
      /// Changing the scrollregion's width visibly moves the selection.
      /// But if we updated the selection using SelectionStart and SelectionEnd, it would auto-scroll.
      /// </summary>
      public void ChangeWidth(int newWidth) {
         maxWidth = newWidth;
         var rawStart = Scroll.ViewPointToDataIndex(rawSelectionStart);
         var rawEnd = Scroll.ViewPointToDataIndex(rawSelectionEnd);
         var start = Scroll.ViewPointToDataIndex(selectionStart);
         var end = Scroll.ViewPointToDataIndex(selectionEnd);

         Scroll.Width = CoerceWidth(newWidth);

         rawSelectionStart = Scroll.DataIndexToViewPoint(rawStart);
         rawSelectionEnd = Scroll.DataIndexToViewPoint(rawEnd);
         selectionStart = Scroll.DataIndexToViewPoint(start);
         selectionEnd = Scroll.DataIndexToViewPoint(end);
      }

      public void SetJumpBackPoint(int address) {
         if (backStack.Count > 0) backStack.Pop();
         backStack.Push(new JumpInfo(address, address));
      }

      public static (Point start, Point end) GetDefaultSelectionSpan(Point p) => (p, p);

      /// <summary>
      /// When the scrolling changes, the selection has to move as well.
      /// This is because the selection is in terms of the viewPort, not the overall data.
      /// Nothing in this method notifies because any amount of scrolling means we already need a complete redraw.
      /// </summary>
      public void ShiftSelectionFromScroll(int distance) {
         var rawStart = Scroll.ViewPointToDataIndex(rawSelectionStart);
         var rawEnd = Scroll.ViewPointToDataIndex(rawSelectionEnd);
         var start = Scroll.ViewPointToDataIndex(selectionStart);
         var end = Scroll.ViewPointToDataIndex(selectionEnd);

         rawStart -= distance;
         rawEnd -= distance;
         start -= distance;
         end -= distance;

         rawSelectionStart = Scroll.DataIndexToViewPoint(rawStart);
         rawSelectionEnd = Scroll.DataIndexToViewPoint(rawEnd);
         selectionStart = Scroll.DataIndexToViewPoint(start);
         selectionEnd = Scroll.DataIndexToViewPoint(end);
      }

      public void MoveSelectionStartExecuted(Direction direction) {
         Point dif;
         if (direction == Direction.PageUp) {
            dif = new Point(0, -Scroll.Height);
         } else if (direction == Direction.PageDown) {
            dif = new Point(0, Scroll.Height);
         } else if (direction == Direction.Home) {
            var currentStart = Scroll.ViewPointToDataIndex(selectionStart);
            var currentRun = model.GetNextRun(currentStart);
            if (currentRun.Start > currentStart) {
               dif = new Point(-selectionStart.X, 0); // no run -> jump to start of line
            } else {
               var newStart = currentRun.Start;
               dif = Scroll.DataIndexToViewPoint(newStart) - selectionStart;
            }
         } else if (direction == Direction.End) {
            var currentStart = Scroll.ViewPointToDataIndex(selectionStart);
            var currentRun = model.GetNextRun(currentStart);
            if (currentRun.Start > currentStart) {
               dif = new Point(Scroll.Width - 1 - selectionStart.X, 0); // no run -> jump to end of line
            } else {
               var newStart = currentRun.Start + currentRun.Length - 1;
               dif = Scroll.DataIndexToViewPoint(newStart) - selectionStart;
            }
         } else {
            dif = ScrollRegion.DirectionToDif[direction];
         }

         var (start, end) = getSpan(rawSelectionEnd);
         if (dif.X < 0 || dif.Y < 0 || direction == Direction.End) {
            // start from the _front_ of selectionEnd
            SelectionStart = start + dif;
         } else {
            // start from the _back_ of selectionEnd
            SelectionStart = end + dif;
         }
      }

      public void MoveSelectionEndExecuted(Direction direction) {
         Point dif;
         if (direction == Direction.PageUp) {
            dif = new Point(0, -Scroll.Height);
         } else if (direction == Direction.PageDown) {
            dif = new Point(0, Scroll.Height);
         } else if (direction == Direction.Home) {
            var currentStart = Scroll.ViewPointToDataIndex(selectionEnd);
            var currentRun = model.GetNextRun(currentStart);
            if (currentRun.Start > currentStart) {
               dif = new Point(-selectionEnd.X, 0); // no run -> jump to start of line
            } else {
               var newStart = currentRun.Start;
               dif = Scroll.DataIndexToViewPoint(newStart) - selectionEnd;
            }
         } else if (direction == Direction.End) {
            var currentStart = Scroll.ViewPointToDataIndex(selectionEnd);
            var currentRun = model.GetNextRun(currentStart);
            if (currentRun.Start > currentStart) {
               dif = new Point(Scroll.Width - 1 - selectionEnd.X, 0); // no run -> jump to end of line
            } else {
               var newStart = currentRun.Start + currentRun.Length - 1;
               dif = Scroll.DataIndexToViewPoint(newStart) - selectionEnd;
            }
         } else {
            dif = ScrollRegion.DirectionToDif[direction];
         }

         var (start, end) = getSpan(rawSelectionEnd);
         if (dif.X > 0 || dif.Y > 0 || direction == Direction.Home) {
            // start from the _back_ of selectionEnd
            SelectionEnd = end + dif;
         } else {
            // start from the _front_ of selectionEnd
            SelectionEnd = start + dif;
         }
      }

      public int CoerceWidth(int width) {
         var desiredWidth = preferredWidth.LimitToRange(1, 0x100);
         if (preferredWidth == -1 || preferredWidth == width) return width;
         if (!allowMultipleElementsPerLine) return desiredWidth;

         if (desiredWidth < width) {
            int multiple = 2;
            while (desiredWidth * multiple <= width) multiple++;
            return desiredWidth * (multiple - 1);
         }
         var divisors = GetDivisors(desiredWidth).Reverse();
         var newWidth = divisors.FirstOrDefault();
         if (newWidth < 4) return desiredWidth;
         return newWidth;
      }

      public static IEnumerable<int> GetDivisors(int number) {
         // only actually allow for divisors if the preferred width is 0x10
         if (number == 16) {
            for (int i = 1; i <= number / 2; i++) {
               if (number % i == 0) yield return i;
            }
         }
      }
   }
}
