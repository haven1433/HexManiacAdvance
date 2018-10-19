using HavenSoft.Gen3Hex.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace HavenSoft.Gen3Hex.ViewModel {
   public class ScrollRegion : INotifyPropertyChanged {
      public static readonly IReadOnlyDictionary<Direction, Point> DirectionToDif = new Dictionary<Direction, Point> {
         { Direction.Up,    new Point( 0,-1) },
         { Direction.Down,  new Point( 0, 1) },
         { Direction.Left,  new Point(-1, 0) },
         { Direction.Right, new Point( 1, 0) },
      };

      private readonly StubCommand scroll;

      private int dataIndex, width, height, scrollValue, maximumScroll, dataLength;

      public ICommand Scroll => scroll;

      public int DataIndex {
         get => dataIndex;
         private set {
            var dif = value - dataIndex;
            if (TryUpdate(ref dataIndex, value)) ScrollChanged?.Invoke(this, dif);
         }
      }

      public int Width {
         get => width;
         set {
            if (TryUpdate(ref width, value)) UpdateScrollRange();
         }
      }

      public int Height {
         get => height;
         set {
            if (TryUpdate(ref height, value)) UpdateScrollRange();
         }
      }

      public int ScrollValue {
         get => scrollValue;
         set {
            value = value.LimitToRange(MinimumScroll, MaximumScroll);
            var dif = value - scrollValue;
            if (dif == 0) return;

            DataIndex += dif * width;
            TryUpdate(ref scrollValue, value);
         }
      }

      public int MinimumScroll => 0;

      public int MaximumScroll {
         get => maximumScroll;
         private set => TryUpdate(ref maximumScroll, value);
      }

      public int DataLength {
         get => dataLength;
         set {
            if (TryUpdate(ref dataLength, value)) UpdateScrollRange();
         }
      }

      public event PropertyChangedEventHandler PropertyChanged;

      public event EventHandler<int> ScrollChanged;

      public ScrollRegion() {
         scroll = new StubCommand {
            CanExecute = args => dataLength > 0,
            Execute = args => ScrollExecuted((Direction)args),
         };
      }

      public int ViewPointToDataIndex(Point p) => p.Y * width + p.X + dataIndex;

      public Point DataIndexToViewPoint(int index) {
         index -= dataIndex;
         if (index >= 0) {
            return new Point(index % width, index / width);
         } else {
            return new Point(width - ((-index) % width), index / width - 1);
         }
      }

      /// <summary>
      /// Scrolls the view, changing the ScrollValue and DataIndex, so that the provided point comes into view.
      /// If scrolling is required, the method returns true and the point argument is adjusted based on the scrolling.
      /// If scrolling is not required, the method returns false.
      /// </summary>
      public bool ScrollToPoint(ref Point point) {
         while (point.X < 0) point += new Point(width, -1);
         while (point.X >= width) point -= new Point(width, -1);

         if (point.Y < 0) {
            ScrollValue += point.Y;
            point = new Point(point.X, 0);
            return true;
         }

         if (point.Y >= height) {
            ScrollValue += point.Y + 1 - height;
            point = new Point(point.X, height - 1);
            return true;
         }

         return false;
      }

      private void ScrollExecuted(Direction direction) {
         var dif = DirectionToDif[direction];
         if (dif.Y != 0) {
            ScrollValue += dif.Y;
         } else {
            DataIndex = (dataIndex + dif.X).LimitToRange(1 - width, dataLength - 1);
            UpdateScrollRange();
         }
      }

      private void UpdateScrollRange() {
         if (width < 1 || height < 1 || dataLength < 1) return;
         int effectiveDataLength = CalculateEffectiveDataLength();
         var lineCount = (int)Math.Ceiling((double)effectiveDataLength / width);
         MaximumScroll = Math.Max(lineCount - 1, 0);
         var newCurrentScroll = (int)Math.Ceiling((double)dataIndex / width);

         // screen size changes while scrolled above the data can make the data scroll completely out of view
         if (newCurrentScroll < 0) {
            DataIndex += Width * -newCurrentScroll;
            newCurrentScroll = 0;
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
         int effectiveDataLength = dataLength;

         var columnOffset = DataIndex % width;
         if (columnOffset != 0) effectiveDataLength += width - columnOffset;

         return effectiveDataLength;
      }

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
