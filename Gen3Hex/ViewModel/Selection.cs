using HavenSoft.Gen3Hex.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace HavenSoft.Gen3Hex.ViewModel {
   public class Selection : ViewModelCore {

      private readonly StubCommand
         moveSelectionStart = new StubCommand(),
         moveSelectionEnd = new StubCommand(),
         gotoCommand = new StubCommand(),
         forward = new StubCommand(),
         backward = new StubCommand();

      private readonly ScrollRegion scroll;

      // these back/forward stacks are not encapsulated in a history object because we want to be able to change a remembered address each time we visit it.
      // if we navigate back, then scroll, then navigate forward, we want to remember the scroll if we go back again.
      private readonly Stack<int> backStack = new Stack<int>(), forwardStack = new Stack<int>();

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
      public ICommand Goto => gotoCommand;
      public ICommand Forward => forward;
      public ICommand Back => backward;

      public event EventHandler<string> OnError;

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

         gotoCommand = new StubCommand {
            CanExecute = args => true,
            Execute = args => {
               var address = args.ToString();
               if (int.TryParse(address, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out int result)) {
                  backStack.Push(scroll.DataIndex);
                  forwardStack.Clear();
                  SelectionStart = scroll.DataIndexToViewPoint(result);
                  scroll.ScrollValue += selectionStart.Y;
               } else {
                  OnError?.Invoke(this, $"Unable to goto address '{address}'");
               }
            },
         };
         backward = new StubCommand {
            CanExecute = args => backStack.Count > 0,
            Execute = args => {
               if (backStack.Count == 0) return;
               forwardStack.Push(scroll.DataIndex);
               SelectionStart = scroll.DataIndexToViewPoint(backStack.Pop());
               scroll.ScrollValue += selectionStart.Y;
            },
         };
         forward = new StubCommand {
            CanExecute = args => forwardStack.Count > 0,
            Execute = args => {
               if (forwardStack.Count == 0) return;
               backStack.Push(scroll.DataIndex);
               SelectionStart = scroll.DataIndexToViewPoint(forwardStack.Pop());
               scroll.ScrollValue += selectionStart.Y;
            },
         };

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
