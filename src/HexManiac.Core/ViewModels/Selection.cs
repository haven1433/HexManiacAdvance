using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels {
   public delegate (Point start, Point end) GetSelectionSpan(Point p);

   public class JumpInfo {
      public int ViewStart { get; }
      public int SelectionStart { get; }
      public JumpInfo(int viewStart, int selectionStart) => (ViewStart, SelectionStart) = (viewStart, selectionStart);
   }

   public class Selection : ViewModelCore {
      private const int DefaultPreferredWidth = 0x10;

      private readonly IDataModel model;
      private readonly ChangeHistory<ModelDelta> history;
      private readonly GetSelectionSpan getSpan;
      private readonly StubCommand
         moveSelectionStart = new StubCommand(),
         moveSelectionEnd = new StubCommand(),
         gotoCommand = new StubCommand(),
         resetAlignmentCommand = new StubCommand(),
         forward = new StubCommand(),
         backward = new StubCommand();

      // these back/forward stacks are not encapsulated in a history object because we want to be able to change a remembered address each time we visit it.
      // if we navigate back, then scroll, then navigate forward, we want to remember the scroll if we go back again.
      private readonly Stack<JumpInfo> backStack = new Stack<JumpInfo>(), forwardStack = new Stack<JumpInfo>();

      private int preferredWidth = DefaultPreferredWidth, maxWidth = 4;

      private Point rawSelectionStart; // the actual click point
      private Point selectionStart;    // the calculated selection start, which may differ depending on the SelectionSpan
      private Point rawSelectionEnd;   // the actual release point
      private Point selectionEnd;      // the calculated selection end, which may differ depending on the SelectionSpan

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

      public int PreferredWidth {
         get => preferredWidth;
         set {
            if (TryUpdate(ref preferredWidth, value)) ChangeWidth(maxWidth);
         }
      }

      private bool autoAdjustDataWidth = true, allowMultipleElementsPerLine = true;
      public bool AutoAdjustDataWidth { get => autoAdjustDataWidth; set => Set(ref autoAdjustDataWidth, value); }
      public bool AllowMultipleElementsPerLine { get => allowMultipleElementsPerLine; set => Set(ref allowMultipleElementsPerLine, value); }

      public ICommand MoveSelectionStart => moveSelectionStart;
      public ICommand MoveSelectionEnd => moveSelectionEnd;
      public ICommand Goto => gotoCommand;
      public ICommand ResetAlignment => resetAlignmentCommand;
      public ICommand Forward => forward;
      public ICommand Back => backward;

      public ScrollRegion Scroll { get; }

      public event EventHandler<string> OnError;

      /// <summary>
      /// The owner may have something special going on with the selected point.
      /// Warn the owner before the selection changes, in case they need to do cleanup.
      /// Note that this function is expected to pass the _old_ selection value
      /// </summary>
      public event EventHandler<Point> PreviewSelectionStartChanged;

      public Selection(ScrollRegion scrollRegion, IDataModel model, ChangeHistory<ModelDelta> history, GetSelectionSpan getSpan = null) {
         this.model = model;
         this.history = history;
         this.getSpan = getSpan ?? GetDefaultSelectionSpan;
         Scroll = scrollRegion;
         Scroll.ScrollChanged += (sender, e) => ShiftSelectionFromScroll(e);

         moveSelectionStart.CanExecute = args => true;
         moveSelectionStart.Execute = args => MoveSelectionStartExecuted((Direction)args);
         moveSelectionEnd.CanExecute = args => true;
         moveSelectionEnd.Execute = args => MoveSelectionEndExecuted((Direction)args);
         resetAlignmentCommand.CanExecute = args => true;
         resetAlignmentCommand.Execute = args => GotoAddressAndAlign(Scroll.DataIndex, 0x10);

         gotoCommand = new StubCommand {
            CanExecute = args => {
               if (args is string str) {
                  str = str.Replace(PointerRun.PointerStart.ToString(), string.Empty).Replace(PointerRun.PointerEnd.ToString(), string.Empty).ToLower();
                  if (str == "null") return false;
               }
               return true;
            },
            Execute = args => {
               if (args is int intArgs) args = intArgs.ToString("X6");
               var address = args.ToString().Trim();
               if (address.StartsWith(ViewPort.GotoMarker.ToString())) address = address.Substring(1);
               if (address.StartsWith(PointerRun.PointerStart.ToString())) address = address.Substring(1);
               if (address.EndsWith(PointerRun.PointerEnd.ToString())) address = address.Substring(0, address.Length - 1);
               if (address.StartsWith("0x")) address = address.Substring(2);
               var offset = 0;
               if (address.Split("+") is string[] addParts && addParts.Length == 2) {
                  address = addParts[0];
                  int.TryParse(addParts[1], NumberStyles.HexNumber, CultureInfo.CurrentCulture, out offset);
               } else if (address.Split("-") is string[] subtractParts && subtractParts.Length == 2) {
                  if (int.TryParse(subtractParts[1], NumberStyles.HexNumber, CultureInfo.CurrentCulture, out offset)) {
                     address = subtractParts[0];
                     offset = -offset;
                  }
               }
               using (ModelCacheScope.CreateScope(this.model)) {
                  var minSize = -1;
                  if (address.Contains("(") && address.Contains(")")) {
                     var addressParts = address.Split(new[] { '(', ')' }, StringSplitOptions.RemoveEmptyEntries);
                     address = addressParts[0];
                     if (addressParts.Length == 2) int.TryParse(addressParts[1], NumberStyles.HexNumber, CultureInfo.CurrentCulture, out minSize);
                  }
                  var anchor = this.model.GetAddressFromAnchor(new ModelDelta(), -1, address);
                  if (anchor == Pointer.NULL && minSize > 0) {
                     var newAnchorStart = model.FindFreeSpace(this.model.FreeSpaceStart, minSize);
                     this.model.ObserveAnchorWritten(history.CurrentChange, address, new NoInfoRun(newAnchorStart));
                     GotoAddress(newAnchorStart + offset);
                  } else if (anchor != Pointer.NULL) {
                     GotoAddress(anchor + offset);
                  } else if (int.TryParse(address, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out int result)) {
                     if (result >= BaseModel.PointerOffset) result -= BaseModel.PointerOffset;
                     GotoAddress(result + offset);
                  } else {
                     address = address.ToLower().Trim();
                     var options = model.GetAutoCompleteAnchorNameOptions(address).ToList();
                     if (!address.Contains("/") && options.All(option => !option.ToLower().Contains(address))) {
                        options = options.Concat(model.GetAutoCompleteAnchorNameOptions("/" + address)).ToList();
                     }
                     if (options.Count == 1) {
                        anchor = this.model.GetAddressFromAnchor(new ModelDelta(), -1, options[0]);
                        GotoAddress(anchor + offset);
                     } else if (options.Count > 1) {
                        var bestMatches = options.Where(option => option.ToLower().Contains(address)).ToList();
                        if (bestMatches.Count > 0) {
                           var shortestMatch = bestMatches.OrderBy(match => match.Split("/").Last().Length).First();
                           anchor = this.model.GetAddressFromAnchor(new ModelDelta(), -1, shortestMatch);
                        } else {
                           anchor = this.model.GetAddressFromAnchor(new ModelDelta(), -1, options[0]);
                        }
                        GotoAddress(anchor + offset);
                     } else {
                        OnError?.Invoke(this, $"Unable to goto address '{address}'");
                     }
                  }
               }
            },
         };
         backward = new StubCommand {
            CanExecute = args => backStack.Count > 0,
            Execute = args => {
               if (backStack.Count == 0) return;
               var selectionPoint = Scroll.ViewPointToDataIndex(SelectionStart);
               if (selectionPoint < Scroll.DataIndex || selectionPoint > Scroll.DataIndex + Scroll.Width * Scroll.Height) selectionPoint = Scroll.DataIndex;
               forwardStack.Push(new JumpInfo(Scroll.DataIndex, selectionPoint));
               if (forwardStack.Count == 1) forward.CanExecuteChanged.Invoke(forward, EventArgs.Empty);
               var backInfo = backStack.Pop();
               GotoAddressHelper(backInfo.ViewStart);
               SelectionStart = Scroll.DataIndexToViewPoint(backInfo.SelectionStart);
               if (backStack.Count == 0) backward.CanExecuteChanged.Invoke(backward, EventArgs.Empty);
            },
         };
         forward = new StubCommand {
            CanExecute = args => forwardStack.Count > 0,
            Execute = args => {
               if (forwardStack.Count == 0) return;
               var selectionPoint = Scroll.ViewPointToDataIndex(SelectionStart);
               if (selectionPoint < Scroll.DataIndex || selectionPoint > Scroll.DataIndex + Scroll.Width * Scroll.Height) selectionPoint = Scroll.DataIndex;
               backStack.Push(new JumpInfo(Scroll.DataIndex, selectionPoint));
               if (backStack.Count == 1) backward.CanExecuteChanged.Invoke(backward, EventArgs.Empty);
               var forwardInfo = forwardStack.Pop();
               GotoAddressHelper(forwardInfo.ViewStart);
               SelectionStart = Scroll.DataIndexToViewPoint(forwardInfo.SelectionStart);
               if (forwardStack.Count == 0) forward.CanExecuteChanged.Invoke(forward, EventArgs.Empty);
            },
         };
      }

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

      public void GotoAddress(int address) {
         if (address >= model.Count || address < 0) {
            OnError?.Invoke(this, $"Address {address:X2} is not within the size of the data.");
            return;
         }

         var selectionPoint = Scroll.ViewPointToDataIndex(SelectionStart);
         if (selectionPoint < Scroll.DataIndex || selectionPoint > Scroll.DataIndex + Scroll.Width * Scroll.Height) selectionPoint = Scroll.DataIndex;
         backStack.Push(new JumpInfo(Scroll.DataIndex, selectionPoint));
         if (backStack.Count == 1) backward.CanExecuteChanged.Invoke(backward, EventArgs.Empty);
         if (forwardStack.Count > 0) {
            forwardStack.Clear();
            forward.CanExecuteChanged.Invoke(forward, EventArgs.Empty);
         }
         GotoAddressHelper(address);

         // if we didn't actually move anything, don't put the jump point in the back-stack
         var jump = backStack.Peek();
         if (Scroll.DataIndex == jump.ViewStart && Scroll.ViewPointToDataIndex(SelectionStart) == jump.SelectionStart) {
            backStack.Pop();
         }
      }

      public void SetJumpBackPoint(int address) {
         if (backStack.Count > 0) backStack.Pop();
         backStack.Push(new JumpInfo(address, address));
      }

      private static (Point start, Point end) GetDefaultSelectionSpan(Point p) => (p, p);

      private void GotoAddressHelper(int address) {
         var destinationRun = model.GetNextRun(address) as ITableRun;
         var destinationIsArray = destinationRun != null && destinationRun.Start <= address;
         int preferredWidth = destinationIsArray ? destinationRun.ElementLength : DefaultPreferredWidth;
         GotoAddressAndAlign(address, preferredWidth, destinationIsArray ? destinationRun.Start : 0);
      }

      private void GotoAddressAndAlign(int address, int preferredWidth, int tableStart = 0) {
         if (address < Scroll.DataStart || Scroll.DataLength < address) {
            Scroll.ClearTableMode();
            Debug.Assert(Scroll.DataLength == model.Count, "I forgot to update the Scroll.DataLength after expanding the data!");
         }

         var startAddress = address;
         if (preferredWidth > 1) address -= (address - tableStart) % preferredWidth;

         // first, change the scroll to view the actual requested address
         Scroll.ScrollValue += Scroll.DataIndexToViewPoint(startAddress).Y;

         // then, scroll left/right as needed to align everything
         while (Scroll.DataIndex < address) Scroll.Scroll.Execute(Direction.Right);
         while (Scroll.DataIndex > address) Scroll.Scroll.Execute(Direction.Left);

         // update the width
         if (autoAdjustDataWidth) PreferredWidth = preferredWidth;

         // finally, update the selection
         SelectionStart = Scroll.DataIndexToViewPoint(startAddress);

         if (model.GetNextRun(startAddress) is ITableRun tableRun && tableRun.Start <= startAddress) {
            Scroll.SetTableMode(tableRun.Start, tableRun.Length);
         } else {
            Scroll.ClearTableMode();
         }
      }

      /// <summary>
      /// When the scrolling changes, the selection has to move as well.
      /// This is because the selection is in terms of the viewPort, not the overall data.
      /// Nothing in this method notifies because any amount of scrolling means we already need a complete redraw.
      /// </summary>
      private void ShiftSelectionFromScroll(int distance) {
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

      private void MoveSelectionStartExecuted(Direction direction) {
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

      private void MoveSelectionEndExecuted(Direction direction) {
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

      private int CoerceWidth(int width) {
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

      private static IEnumerable<int> GetDivisors(int number) {
         // only actually allow for divisors if the preferred width is 0x10
         if (number == 16) {
            for (int i = 1; i <= number / 2; i++) {
               if (number % i == 0) yield return i;
            }
         }
      }
   }
}
