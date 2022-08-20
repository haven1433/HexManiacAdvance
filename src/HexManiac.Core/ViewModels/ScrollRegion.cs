using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels {
   public delegate bool TryGetUsefulHeader(int address, out string header);

   public class ScrollRegion : ViewModelCore {
      public static readonly IReadOnlyDictionary<Direction, Point> DirectionToDif = new Dictionary<Direction, Point> {
         { Direction.Up,    new Point( 0,-1) },
         { Direction.Down,  new Point( 0, 1) },
         { Direction.Left,  new Point(-1, 0) },
         { Direction.Right, new Point( 1, 0) },
      };

      private readonly StubCommand scroll;

      private readonly TryGetUsefulHeader tryGetUsefulHeader;

      private int dataIndex, width, height, scrollValue, maximumScroll, dataLength;

      private int tableStart, tableLength;
      private bool allowTableMode;
      public bool AllowSingleTableMode { get => allowTableMode; set => Set(ref allowTableMode, value, arg => ClearTableMode()); }

      public IToolTrayViewModel Scheduler { get; set; }

      public ICommand Scroll => scroll;

      public int DataIndex {
         get => dataIndex;
         private set {
            var dif = value - dataIndex;
            if (TryUpdate(ref dataIndex, value)) {
               ScrollChanged?.Invoke(this, dif);
               UpdateScrollRange();
            }
         }
      }

      public int Width {
         get => width;
         set {
            // do the update manually so that we don't notify value change until after calling UpdateScrollRange
            value = value.LimitToRange(4, int.MaxValue);
            if (value == width) return;
            var oldWidth = width;
            width = value;
            UpdateScrollRange();
            NotifyPropertyChanged(oldWidth);
         }
      }

      public int Height {
         get => height;
         set {
            // do the update manually so that we don't notify value change until after calling UpdateScrollRange
            value = value.LimitToRange(1, int.MaxValue);
            if (value == height) return;
            var oldHeight = height;
            height = value;
            UpdateScrollRange();
            NotifyPropertyChanged(oldHeight);
         }
      }

      public int ScrollValue {
         get => scrollValue;
         set {
            value = value.LimitToRange(MinimumScroll, MaximumScroll);
            var dif = value - scrollValue;
            if (dif == 0) return;

            DataIndex += dif * width;
            NotifyPropertyChanged(value - dif);
         }
      }

      public bool IsSingleTableMode => tableLength != 0;

      public int MinimumScroll {
         get {
            if (tableLength == 0) return 0;
            int effectiveDataStart = CalculateEffectiveDataLength(tableStart);
            return effectiveDataStart / width;
         }
      }

      public int MaximumScroll {
         get {
            if (tableLength == 0) return maximumScroll;
            int effectiveDataLength = CalculateEffectiveDataLength(tableStart + tableLength);
            var lineCount = (int)Math.Ceiling((double)effectiveDataLength / width);
            return Math.Max(lineCount - 1, 0);
         }
         private set => TryUpdate(ref maximumScroll, value);
      }

      private bool useCustomHeaders;
      public bool UseCustomHeaders {
         get => useCustomHeaders;
         set {
            if (TryUpdate(ref useCustomHeaders, value)) UpdateHeaders();
         }
      }

      public int DataStart => tableLength == 0 ? 0 : tableStart;

      public int DataLength {
         get {
            if (tableLength == 0) return dataLength;
            return tableStart + tableLength;
         }
         set {
            if (TryUpdate(ref dataLength, value)) UpdateScrollRange();
         }
      }

      public ObservableCollection<string> Headers { get; } = new ObservableCollection<string>();

      public event EventHandler<int> ScrollChanged;

      public static bool DefaultHeaderStrategy(int address, out string header) { header = null; return false; }

      public ScrollRegion(TryGetUsefulHeader headerStratey = null) {
         tryGetUsefulHeader = headerStratey ?? DefaultHeaderStrategy;
         width = 4;
         height = 4;
         scroll = new StubCommand {
            CanExecute = args => dataLength > 0,
            Execute = args => ScrollExecuted((Direction)args),
         };
         UpdateHeaders();
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

      public void SetTableMode(int start, int length) {
         if (tableStart == start && tableLength == length) return;
         if (!allowTableMode && length != 0) return;
         tableStart = start;
         tableLength = length;
         NotifyPropertyChanged(nameof(MinimumScroll));
         NotifyPropertyChanged(nameof(MaximumScroll));
         NotifyPropertyChanged(nameof(DataLength));
         UpdateHeaders();
      }

      public void ClearTableMode() => SetTableMode(0, 0);

      private void ScrollExecuted(Direction direction) {
         var dif = DirectionToDif[direction];
         if (dif.Y != 0) {
            ScrollValue += dif.Y;
         } else {
            DataIndex = (dataIndex + dif.X).LimitToRange(1 - width, dataLength - 1);
            ClearTableMode();
         }
      }

      private void UpdateScrollRange() {
         if (width < 1 || height < 1 || dataLength < 1) return;
         int effectiveDataLength = CalculateEffectiveDataLength();
         var lineCount = (int)Math.Ceiling((double)effectiveDataLength / width);
         if (tableLength != 0) NotifyPropertyChanged(nameof(MinimumScroll));
         MaximumScroll = Math.Max(lineCount - 1, 0);
         if (MinimumScroll > MaximumScroll) throw new InvalidOperationException($"MinimumScroll({MinimumScroll}) is bigger than MaximumScroll({MaximumScroll}): TableStart={tableStart}, TableLength={tableLength}, Width={width}");
         var newCurrentScroll = (int)Math.Ceiling((double)dataIndex / width);

         // screen size changes while scrolled above the data can make the data scroll completely out of view
         if (newCurrentScroll < 0) {
            DataIndex += Width * -newCurrentScroll;
            newCurrentScroll = 0;
         }

         // don't notify: the caller will notify if desired.
         scrollValue = newCurrentScroll;

         if (Scheduler != null) {
            Scheduler.Schedule(UpdateHeaders);
         } else {
            UpdateHeaders();
         }
      }

      public void UpdateHeaders() {
         while (Headers.Count > Height) Headers.RemoveAt(Headers.Count - 1);
         for (int i = 0; i < Height; i++) {
            var address = dataIndex + i * Width;

            if (!useCustomHeaders || !tryGetUsefulHeader(address, out string hexAddress)) {
               hexAddress = address.ToString("X6");
               if (address < 0) hexAddress = string.Empty;
            }

            if (address >= DataLength) hexAddress = string.Empty;

            if (Headers.Count > i) {
               Headers[i] = hexAddress;
            } else {
               Headers.Add(hexAddress);
            }
         }
      }

      /// <summary>
      /// If the data is offset in a strange way, there may be some blank spaces we have
      /// to display at the start of the data. The 'effective data length' is the length
      /// of whatever actual data we have, plus the extra blank space on the first row.
      /// </summary>
      private int CalculateEffectiveDataLength(int virtualDataLength = -1) {
         if (virtualDataLength == -1) virtualDataLength = dataLength;
         int effectiveDataLength = virtualDataLength;

         var columnOffset = DataIndex % width;
         if (columnOffset != 0) effectiveDataLength += width - columnOffset;

         return effectiveDataLength;
      }
   }
}
