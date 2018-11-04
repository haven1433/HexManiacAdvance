using HavenSoft.Gen3Hex.Model;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace HavenSoft.Gen3Hex.ViewModel {
   public class Selection : ViewModelCore {

      private readonly StubCommand
         moveSelectionStart = new StubCommand(),
         moveSelectionEnd = new StubCommand();

      private readonly ScrollRegion scroll;

      private Point selectionStart, selectionEnd;

      public Point SelectionStart {
         get => selectionStart;
         set {
            var index = scroll.ViewPointToDataIndex(value);
            value = scroll.DataIndexToViewPoint(index.LimitToRange(0, scroll.DataLength));

            if (selectionStart.Equals(value)) return;

            if (!scroll.ScrollToPoint(ref value)) {
               PreviewSelectionStartChanged?.Invoke(this, selectionStart);
            }

            if (TryUpdate(ref selectionStart, value)) {
               SelectionEnd = selectionStart;
            }
         }
      }

      public Point SelectionEnd {
         get => selectionEnd;
         set {
            var index = scroll.ViewPointToDataIndex(value);
            value = scroll.DataIndexToViewPoint(index.LimitToRange(0, scroll.DataLength));

            scroll.ScrollToPoint(ref value);
            TryUpdate(ref selectionEnd, value);
         }
      }

      public ICommand MoveSelectionStart => moveSelectionStart;

      public ICommand MoveSelectionEnd => moveSelectionEnd;

      /// <summary>
      /// The owner may have something special going on with the selected point.
      /// Warn the owner before the selection changes, in case they need to do cleanup.
      /// </summary>
      public event EventHandler<Point> PreviewSelectionStartChanged;

      public Selection(ScrollRegion scrollRegion) {
         scroll = scrollRegion;
         scroll.ScrollChanged += (sender, e) => ShiftSelectionFromScroll(e);

         moveSelectionStart.CanExecute = args => true;
         moveSelectionStart.Execute = args => MoveSelectionStartExecuted((Direction)args);
         moveSelectionEnd.CanExecute = args => true;
         moveSelectionEnd.Execute = args => MoveSelectionEndExecuted((Direction)args);
      }

      public bool IsSelected(Point point) {
         if (point.X < 0 || point.X >= scroll.Width) return false;

         var selectionStart = scroll.ViewPointToDataIndex(SelectionStart);
         var selectionEnd = scroll.ViewPointToDataIndex(SelectionEnd);
         var middle = scroll.ViewPointToDataIndex(point);

         var leftEdge = Math.Min(selectionStart, selectionEnd);
         var rightEdge = Math.Max(selectionStart, selectionEnd);

         return leftEdge <= middle && middle <= rightEdge;
      }

      /// <summary>
      /// Changing the scrollregion's width visibly moves the selection.
      /// But if we updated the selection using SelectionStart and SelectionEnd, it would auto-scroll.
      /// </summary>
      public void ChangeWidth(int newWidth) {
         var start = scroll.ViewPointToDataIndex(selectionStart);
         var end = scroll.ViewPointToDataIndex(selectionEnd);

         scroll.Width = newWidth;

         TryUpdate(ref selectionStart, scroll.DataIndexToViewPoint(start));
         TryUpdate(ref selectionEnd, scroll.DataIndexToViewPoint(end));
      }

      /// <summary>
      /// When the scrolling changes, the selection has to move as well.
      /// This is because the selection is in terms of the viewPort, not the overall data.
      /// Nothing in this method notifies because any amount of scrolling means we already need a complete redraw.
      /// </summary>
      private void ShiftSelectionFromScroll(int distance) {
         var start = scroll.ViewPointToDataIndex(selectionStart);
         var end = scroll.ViewPointToDataIndex(selectionEnd);

         start -= distance;
         end -= distance;

         selectionStart = scroll.DataIndexToViewPoint(start);
         selectionEnd = scroll.DataIndexToViewPoint(end);
      }

      private void MoveSelectionStartExecuted(Direction direction) {
         var dif = ScrollRegion.DirectionToDif[direction];
         SelectionStart = SelectionEnd + dif;
      }

      private void MoveSelectionEndExecuted(Direction direction) {
         var dif = ScrollRegion.DirectionToDif[direction];
         SelectionEnd += dif;
      }
   }
}
