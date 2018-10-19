using HavenSoft.Gen3Hex.Model;
using HavenSoft.ViewModel.DataFormats;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace HavenSoft.Gen3Hex.ViewModel {
   /// <summary>
   /// A range of visible data that should be displayed.
   /// </summary>
   public class ViewPort : INotifyPropertyChanged, INotifyCollectionChanged {
      private static readonly NotifyCollectionChangedEventArgs ResetArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);

      private readonly byte[] data;

      private readonly ChangeHistory<Dictionary<int, HexElement>> history;

      private readonly ScrollRegion scroll;

      private HexElement[,] currentView;

      #region Name

      private string name;

      public string Name {
         get => name;
         private set => TryUpdate(ref name, value);
      }

      #endregion

      public int Width {
         get => scroll.Width;
         set {
            var start = scroll.ViewPointToDataIndex(selectionStart);
            var end = scroll.ViewPointToDataIndex(selectionEnd);
            scroll.Width = value;
            TryUpdate(ref selectionStart, scroll.DataIndexToViewPoint(start), nameof(SelectionStart));
            TryUpdate(ref selectionEnd, scroll.DataIndexToViewPoint(end), nameof(SelectionEnd));
         }
      }

      public int Height {
         get => scroll.Height;
         set => scroll.Height = value;
      }

      public int MinimumScroll => scroll.MinimumScroll;

      public int ScrollValue {
         get => scroll.ScrollValue;
         set => scroll.ScrollValue = value;
      }

      public int MaximumScroll => scroll.MaximumScroll;

      public ICommand Scroll => scroll.Scroll;

      #region SelectionStart

      private Point selectionStart;

      public Point SelectionStart {
         get => selectionStart;
         set {
            if (selectionStart.Equals(value)) return;
            var originalSelectionStart = selectionStart;

            scroll.ScrollToPoint(ref value);
            if (selectionStart.X >= 0 && selectionStart.X < Width && selectionStart.Y >= 0 && selectionStart.Y < Height) {
               var element = currentView[selectionStart.X, selectionStart.Y];
               if (element.Format is UnderEdit underEdit) {
                  currentView[selectionStart.X, selectionStart.Y] = new HexElement(element.Value, underEdit.OriginalFormat);
                  NotifyCollectionChanged(ResetArgs);
               }
            }

            if (TryUpdate(ref selectionStart, value)) {
               SelectionEnd = selectionStart;
            }
         }
      }

      #endregion

      #region SelectionEnd

      private Point selectionEnd;

      public Point SelectionEnd {
         get => selectionEnd;
         set {
            scroll.ScrollToPoint(ref value);

            if (TryUpdate(ref selectionEnd, value)) {
               history.ChangeCompleted();
            }
         }
      }

      #endregion

      #region MoveSelectionStart

      private readonly StubCommand moveSelectionStart = new StubCommand();

      public ICommand MoveSelectionStart => moveSelectionStart;

      private void MoveSelectionStartExecuted(Direction direction) {
         var dif = ScrollRegion.DirectionToDif[direction];
         SelectionStart = SelectionEnd + dif;
      }

      #endregion

      #region MoveSelectionEnd

      private readonly StubCommand moveSelectionEnd = new StubCommand();

      public ICommand MoveSelectionEnd => moveSelectionEnd;

      private void MoveSelectionEndExecuted(Direction direction) {
         var dif = ScrollRegion.DirectionToDif[direction];
         SelectionEnd += dif;
      }

      #endregion

      #region Undo / Redo

      public ICommand Undo => history.Undo;

      public ICommand Redo => history.Redo;

      private Dictionary<int, HexElement> RevertChanges(Dictionary<int, HexElement> changes) {
         var opposite = new Dictionary<int, HexElement>();

         foreach (var change in changes) {
            var (index, element) = (change.Key, change.Value);
            var point = scroll.DataIndexToViewPoint(index);
            scroll.ScrollToPoint(ref point);

            opposite[index] = currentView[point.X, point.Y];
            data[index] = element.Value;
            currentView[point.X, point.Y] = element;
         }

         if (changes.Count > 0) NotifyCollectionChanged(ResetArgs);
         return opposite;
      }

      #endregion

      public HexElement this[int x, int y] {
         get {
            if (x < 0 || x >= Width) return HexElement.Undefined;
            if (y < 0 || y >= Height) return HexElement.Undefined;

            return currentView[x, y];
         }
      }

      public event PropertyChangedEventHandler PropertyChanged;

      public event NotifyCollectionChangedEventHandler CollectionChanged;

      public ViewPort() : this(new LoadedFile(string.Empty, new byte[0])) { }

      public ViewPort(LoadedFile file) {
         name = file.Name;
         data = file.Contents;
         scroll = new ScrollRegion { DataLength = data.Length };
         scroll.PropertyChanged += ScrollPropertyChanged;
         scroll.ScrollChanged += (sender, e) => ShiftSelectionFromScroll(e);

         moveSelectionStart.CanExecute = args => true;
         moveSelectionStart.Execute = args => MoveSelectionStartExecuted((Direction)args);
         moveSelectionEnd.CanExecute = args => true;
         moveSelectionEnd.Execute = args => MoveSelectionEndExecuted((Direction)args);

         history = new ChangeHistory<Dictionary<int, HexElement>>(RevertChanges);
      }

      public bool IsSelected(Point point) {
         if (point.X < 0 || point.X >= Width) return false;

         var selectionStart = scroll.ViewPointToDataIndex(SelectionStart);
         var selectionEnd = scroll.ViewPointToDataIndex(SelectionEnd);
         var middle = scroll.ViewPointToDataIndex(point);

         var leftEdge = Math.Min(selectionStart, selectionEnd);
         var rightEdge = Math.Max(selectionStart, selectionEnd);

         return leftEdge <= middle && middle <= rightEdge;
      }

      public void Edit(string input) {
         for (int i = 0; i < input.Length; i++) Edit(input[i]);
      }

      private void Edit(char input) {
         var selectionStart = scroll.ViewPointToDataIndex(SelectionStart);
         var selectionEnd = scroll.ViewPointToDataIndex(SelectionEnd);
         var leftEdge = Math.Min(selectionStart, selectionEnd);
         var point = scroll.DataIndexToViewPoint(Math.Min(selectionStart, selectionEnd));
         scroll.ScrollToPoint(ref point);
         var element = currentView[point.X, point.Y];
         var underEdit = element.Format as UnderEdit;

         if (!"0123456789ABCDEFabcdef".Contains(input)) {
            if (underEdit != null) {
               currentView[point.X, point.Y] = new HexElement(element.Value, underEdit.OriginalFormat);
               NotifyCollectionChanged(ResetArgs);
            }
            return;
         }

         SelectionStart = point;

         if (underEdit == null) {
            currentView[point.X, point.Y] = new HexElement(element.Value, new UnderEdit(element.Format, input.ToString()));
            NotifyCollectionChanged(ResetArgs);
         } else {
            currentView[point.X, point.Y] = new HexElement(element.Value, new UnderEdit(underEdit.OriginalFormat, underEdit.CurrentText + input));
            underEdit = (UnderEdit)currentView[point.X, point.Y].Format;
            if (underEdit.CurrentText.Length == 2) {
               var byteValue = byte.Parse(underEdit.CurrentText, NumberStyles.HexNumber);
               var memoryLocation = scroll.ViewPointToDataIndex(point);
               history.CurrentChange[memoryLocation] = new HexElement(element.Value, underEdit.OriginalFormat);
               data[memoryLocation] = byteValue;
               var nextPoint = point + new Point(1, 0);
               if (nextPoint.X == Width) nextPoint += new Point(-Width, 1);
               if (!scroll.ScrollToPoint(ref nextPoint)) {
                  // didn't scroll: update manually
                  if (currentView[point.X, point.Y].Format is UnderEdit) {
                     currentView[point.X, point.Y] = new HexElement(byteValue, underEdit.OriginalFormat);
                  }
                  NotifyCollectionChanged(ResetArgs);
               }

               TryUpdate(ref this.selectionStart, nextPoint, nameof(SelectionStart));
               TryUpdate(ref this.selectionEnd, nextPoint, nameof(SelectionEnd));
            } else {
               NotifyCollectionChanged(ResetArgs);
            }
         }
      }

      private void RefreshBackingData() {
         currentView = new HexElement[Width, Height];
         for (int y = 0; y < Height; y++) {
            for (int x = 0; x < Width; x++) {
               var index = scroll.ViewPointToDataIndex(new Point(x, y));
               if (index < 0 || index >= data.Length) {
                  currentView[x, y] = HexElement.Undefined;
               } else {
                  currentView[x, y] = new HexElement(data[index], None.Instance);
               }
            }
         }

         NotifyCollectionChanged(ResetArgs);
      }

      private void ScrollPropertyChanged(object sender, PropertyChangedEventArgs e) {
         if (e.PropertyName == nameof(scroll.DataIndex)) {
            RefreshBackingData();
         } else if (e.PropertyName != nameof(scroll.DataLength)) {
            PropertyChanged?.Invoke(this, e);
         }

         if (e.PropertyName == nameof(Width) || e.PropertyName == nameof(Height)) {
            RefreshBackingData();
         }
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

      private void NotifyCollectionChanged(NotifyCollectionChangedEventArgs args) => CollectionChanged?.Invoke(this, args);

      /// <summary>
      /// Utility function to make writing property updates easier.
      /// If the backing field's value does not match the new value, the backing field is updated and PropertyChanged gets called.
      /// </summary>
      /// <typeparam name="T">The type of the property being updated.</typeparam>
      /// <param name="backingField">A reference to the backing field of the property being changed.</param>
      /// <param name="newValue">The new value for the property.</param>
      /// <param name="propertyName">The name of the property to notify on. If the property is the caller, the compiler will figure this parameter out automatically.</param>
      /// <returns>false if the data did not need to be updated, true if it did.</returns>
      private bool TryUpdate<T>(ref T backingField, T newValue, [CallerMemberName]string propertyName = null) where T : IEquatable<T> {
         if (backingField == null && newValue == null) return false;
         if (backingField != null && backingField.Equals(newValue)) return false;
         backingField = newValue;
         PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
         return true;
      }
   }
}
