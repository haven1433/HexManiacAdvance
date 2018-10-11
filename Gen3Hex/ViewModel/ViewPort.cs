using HavenSoft.Gen3Hex.Model;
using HavenSoft.ViewModel.DataFormats;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace HavenSoft.Gen3Hex.ViewModel {
   /// <summary>
   /// A range of visible data that should be displayed.
   /// </summary>
   public class ViewPort : INotifyPropertyChanged, INotifyCollectionChanged {
      private static readonly NotifyCollectionChangedEventArgs ResetArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);

      private static readonly Dictionary<Direction, Point> directionToDif = new Dictionary<Direction, Point> {
         { Direction.Up,    new Point( 0,-1) },
         { Direction.Down,  new Point( 0, 1) },
         { Direction.Left,  new Point(-1, 0) },
         { Direction.Right, new Point( 1, 0) },
      };

      private readonly byte[] data;

      private int dataIndex;

      #region Name

      private string name;

      public string Name {
         get => name;
         private set => TryUpdate(ref name, value);
      }

      #endregion

      #region Width

      private int width;

      public int Width {
         get => width;
         set {
            if (TryUpdate(ref width, value) && width > 0 && height > 0) {
               UpdateScrollRange();
               NotifyCollectionChanged(ResetArgs);
            }
         }
      }

      #endregion

      #region Height

      private int height;

      public int Height {
         get => height;
         set {
            if (TryUpdate(ref height, value) && width > 0 && height > 0) {
               UpdateScrollRange();
               NotifyCollectionChanged(ResetArgs);
            }
         }
      }

      #endregion

      #region MinimumScroll

      public int MinimumScroll => 0;

      #endregion

      #region ScrollValue

      private int scrollValue;

      public int ScrollValue {
         get => scrollValue;
         set {
            value = Math.Min(Math.Max(MinimumScroll, value), maximumScroll);
            var dif = value - scrollValue;
            if (dif == 0) return;

            dataIndex += dif * width;
            ShiftSelectionFromScroll(dif * width);
            if (TryUpdate(ref scrollValue, value)) {
               NotifyCollectionChanged(ResetArgs);
            }
         }
      }

      #endregion

      #region MaximumScroll

      private int maximumScroll;

      public int MaximumScroll {
         get => maximumScroll;
         private set => TryUpdate(ref maximumScroll, value);
      }

      #endregion

      #region Scroll

      private readonly StubCommand scroll = new StubCommand();

      public ICommand Scroll => scroll;

      private void ScrollExecuted(Direction direction) {
         var dif = directionToDif[direction];
         if (dif.Y != 0) {
            ScrollValue += dif.Y;
         } else {
            dataIndex += dif.X;
            ShiftSelectionFromScroll(dif.X);
            dataIndex = Math.Min(Math.Max(1 - width, dataIndex), data.Length - 1);
            UpdateScrollRange();
            NotifyCollectionChanged(ResetArgs);
         }
      }

      #endregion

      #region SelectionStart

      private Point selectionStart;

      public Point SelectionStart {
         get => selectionStart;
         set {
            value = ScrollToPoint(value);
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
            value = ScrollToPoint(value);
            TryUpdate(ref selectionEnd, value);
         }
      }

      #endregion

      #region MoveSelectionStart

      private readonly StubCommand moveSelectionStart = new StubCommand();

      public ICommand MoveSelectionStart => moveSelectionStart;

      private void MoveSelectionStartExecuted(Direction direction) {
         var dif = directionToDif[direction];
         SelectionStart = SelectionEnd + dif;
      }

      #endregion

      #region MoveSelectionEnd

      private readonly StubCommand moveSelectionEnd = new StubCommand();

      public ICommand MoveSelectionEnd => moveSelectionEnd;

      private void MoveSelectionEndExecuted(Direction direction) {
         var dif = directionToDif[direction];
         SelectionEnd += dif;
      }

      #endregion

      public HexElement this[int x, int y] {
         get {
            if (x < 0 || x >= Width) return HexElement.Undefined;
            if (y < 0 || y >= Height) return HexElement.Undefined;

            var index = y * Width + x + dataIndex;
            if (index < 0 || index >= data.Length) return HexElement.Undefined;

            return new HexElement {
               Format = None.Instance,
               Value = data[index],
            };
         }
      }

      public event PropertyChangedEventHandler PropertyChanged;

      public event NotifyCollectionChangedEventHandler CollectionChanged;

      public ViewPort() : this(new LoadedFile(string.Empty, new byte[0])) { }

      public ViewPort(LoadedFile file) {
         name = file.Name;
         data = file.Contents;

         moveSelectionStart.CanExecute = args => true;
         moveSelectionStart.Execute = args => MoveSelectionStartExecuted((Direction)args);
         moveSelectionEnd.CanExecute = args => true;
         moveSelectionEnd.Execute = args => MoveSelectionEndExecuted((Direction)args);
         scroll.CanExecute = args => true;
         scroll.Execute = args => ScrollExecuted((Direction)args);
      }

      public bool IsSelected(Point point) {
         if (point.X < 0 || point.X >= width) return false;

         int linearize(Point p) => p.Y * width + p.X;
         var selectionStart = linearize(SelectionStart);
         var selectionEnd = linearize(SelectionEnd);
         var middle = linearize(point);

         var leftEdge = Math.Min(selectionStart, selectionEnd);
         var rightEdge = Math.Max(selectionStart, selectionEnd);

         return leftEdge <= middle && middle <= rightEdge;
      }

      private void UpdateScrollRange() {
         int effectiveDataLength = CalculateEffectiveDataLength();
         var lineCount = (int)Math.Ceiling((double)effectiveDataLength / width);
         MaximumScroll = lineCount - 1;
         var newCurrentScroll = (int)Math.Ceiling((double)dataIndex / width);

         // screen size changes while scrolled above the data can make the data scroll completely out of view
         while (newCurrentScroll < 0) {
            newCurrentScroll++;
            dataIndex += Width;
         }

         // Call Update instead of ScrollValue.set to avoid changing the dataIndex.
         TryUpdate(ref scrollValue, newCurrentScroll, nameof(ScrollValue));
      }

      /// <summary>
      /// If the data is offset in a strange way, there may be some blank spaces we have
      /// to display at the start of the data. The 'effective data length' is the length
      /// of whatever actual data we have, plus the extra blank space on the first row.
      /// </summary>
      private int CalculateEffectiveDataLength() {
         int effectiveDataLength = data.Length;

         var columnOffset = dataIndex % width;
         if (columnOffset != 0) effectiveDataLength += width - columnOffset;

         return effectiveDataLength;
      }

      /// <param name="initial">The point to scroll to, using screen coordinates</param>
      /// <returns>The same point, adjusted based on scrolling.</returns>
      private Point ScrollToPoint(Point target) {
         while (target.X < 0) target += new Point(width, -1);
         while (target.X >= width) target -= new Point(width, -1);

         if (target.Y < 0) {
            ScrollValue += target.Y;
            target = new Point(target.X, 0);
         }
         if (target.Y >= height) {
            ScrollValue += target.Y + 1 - height;
            target = new Point(target.X, height - 1);
         }

         return target;
      }

      /// <summary>
      /// When the scrolling changes, the selection has to move as well.
      /// This is because the selection is in terms of the viewPort, not the overall data.
      /// Nothing in this method notifies because any amount of scrolling means we already need a complete redraw.
      /// </summary>
      private void ShiftSelectionFromScroll(int distance) {
         var dif = new Point(distance, 0);
         var line = new Point(width, -1);

         selectionStart -= dif;
         while (selectionStart.X < 0) selectionStart += line;
         while (selectionStart.X >= width) selectionStart -= line;

         selectionEnd -= dif;
         while (selectionEnd.X < 0) selectionEnd += line;
         while (selectionEnd.X >= width) selectionEnd -= line;
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
