using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using HavenSoft.HexManiac.Core.ViewModels.Visitors;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels {
   public class DiffViewPort : ViewModelCore, IViewPort {
      private readonly IChildViewPort[] left, right;
      private readonly int leftWidth, rightWidth;
      private readonly List<int> startOfNextSegment;

      private HexElement[,] cell;

      public DiffViewPort(IEnumerable<IChildViewPort> leftChildren, IEnumerable<IChildViewPort> rightChildren) {
         left = leftChildren.ToArray();
         right = rightChildren.ToArray();
         Debug.Assert(left.Length == right.Length, "Diff views must have the same number of diff elements on each side!");
         leftWidth = left.Length == 0 ? 16 : left.Max(child => child.Width);
         rightWidth = right.Length == 0 ? 16 : right.Max(child => child.Width);
         startOfNextSegment = new List<int>();
         startOfNextSegment.Add(0);
         for (int i = 0; i < Math.Min(left.Length, right.Length); i++) {
            startOfNextSegment.Add(startOfNextSegment[i] + 1 + Math.Max(left[i].Height, right[i].Height));
         }
      }

      #region IViewPort

      public HexElement this[int x, int y] {
         get {
            if (x < 0 || y < 0 || x >= cell.GetLength(0) || y >= cell.GetLength(1)) return new HexElement(default, default, DataFormats.Undefined.Instance);
            return cell[x, y];
         }
      }

      public string FileName => string.Empty;

      public string FullFileName => string.Empty;

      public int Width { get => leftWidth + rightWidth + 1; set { } }

      private int height;
      public int Height { get => height; set => Set(ref height, value, Refresh); }

      public bool AutoAdjustDataWidth { get; set; }
      public bool StretchData { get; set; }
      public bool AllowMultipleElementsPerLine { get; set; }
      public bool UseCustomHeaders { get; set; }

      public int MinimumScroll => 0;

      private int scrollValue;
      public int ScrollValue { get => scrollValue; set => Set(ref scrollValue, value.LimitToRange(MinimumScroll, MaximumScroll), Refresh); }

      public int MaximumScroll => startOfNextSegment[startOfNextSegment.Count - 1] - 1;

      public ObservableCollection<string> Headers { get; } = new ObservableCollection<string>();

      public ObservableCollection<HeaderRow> ColumnHeaders => null;

      public int DataOffset => 0;

      private StubCommand scrollCommand;
      public ICommand Scroll => StubCommand<Direction>(ref scrollCommand, ExecuteScroll);
      private void ExecuteScroll(Direction direction) {
         if (direction == Direction.Up) ScrollValue -= 1;
         if (direction == Direction.Up) ScrollValue += 1;
         if (direction == Direction.PageUp) ScrollValue -= height;
         if (direction == Direction.PageDown) ScrollValue += height;
      }

      public double Progress => 0;

      public bool UpdateInProgress => false;

      public string SelectedAddress => string.Empty;

      public string SelectedBytes => string.Empty;

      public string AnchorText { get; set; }

      public bool AnchorTextVisible => false;

      public IDataModel Model => null;

      public bool HasTools => false;

      public ChangeHistory<ModelDelta> ChangeHistory { get; }

      public IToolTrayViewModel Tools => null;

      public string Name => left[0].Parent.Name.Trim('*') + " -> " + right[0].Parent.Name.Trim('*');
      public ICommand Save => null;
      public ICommand SaveAs => null;
      public ICommand ExportBackup => null;
      public ICommand Undo => null;
      public ICommand Redo => null;
      public ICommand Copy => null;
      public ICommand DeepCopy => null;
      public ICommand Clear => null;
      public ICommand SelectAll => null;
      public ICommand Goto => null;
      public ICommand ResetAlignment => null;
      public ICommand Back => null;
      public ICommand Forward => null;
      public ICommand Close => null;
      public ICommand Diff => null;

      public event EventHandler PreviewScrollChanged;
      public event EventHandler<string> OnError;
      public event EventHandler<string> OnMessage;
      public event EventHandler ClearMessage;
      public event EventHandler Closed;
      public event EventHandler<ITabContent> RequestTabChange;
      public event EventHandler<Action> RequestDelayedWork;
      public event EventHandler RequestMenuClose;
      public event PropertyChangedEventHandler PropertyChanged;
      public event NotifyCollectionChangedEventHandler CollectionChanged;

      public void ConsiderReload(IFileSystem fileSystem) { }

      public IChildViewPort CreateChildView(int startAddress, int endAddress) {
         throw new NotImplementedException();
      }

      public void ExpandSelection(int x, int y) { }

      public IReadOnlyList<(int start, int end)> Find(string search) => throw new NotImplementedException();

      public void FindAllSources(int x, int y) { }

      public void FollowLink(int x, int y) { }

      public IReadOnlyList<IContextItem> GetContextMenuItems(Point point) {
         throw new NotImplementedException();
      }

      public bool IsSelected(Point point) {
         var (childIndex, childLine) = ConvertLine(point.Y);
         if (childIndex >= left.Length) return false;
         if (point.X < leftWidth) return left[childIndex].IsSelected(new Point(point.X, childLine));
         return right[childIndex].IsSelected(new Point(point.X - leftWidth - 1, childLine));
      }

      public bool IsTable(Point point) => false;

      private void Refresh(int unused) => FillCells();
      public void Refresh() => FillCells();

      public bool TryImport(LoadedFile file, IFileSystem fileSystem) {
         throw new NotImplementedException();
      }

      public void ValidateMatchedWords() { }

      #endregion

      private (int childIndex, int childLine) ConvertLine(int parentLine) {
         var scrollLine = parentLine + scrollValue;
         if (scrollLine < 0) return (0, scrollLine);
         int index = startOfNextSegment.BinarySearch(scrollLine);
         if (index >= 0) return (index, 0);
         index = ~index - 1;
         return (index, scrollLine - startOfNextSegment[index]);
      }

      private void FillCells() {
         Headers.Clear();
         cell = new HexElement[Width, Height];
         var defaultCell = new HexElement(default, default, Undefined.Instance);

         var (childIndex, childLine) = ConvertLine(0);
         for (int y = 0; y < Height; y++) {
            var childIsValid = childIndex < left.Length;
            for (int x = 0; x < leftWidth; x++) {
               cell[x, y] = childIsValid ? left[childIndex][x, childLine] : defaultCell;
            }
            cell[leftWidth, y] = defaultCell;
            for (int x = 0; x < rightWidth; x++) {
               cell[leftWidth + 1 + x, y] = childIsValid ? right[childIndex][x, childLine] : defaultCell;
            }

            var hasHeader = childIsValid && left[childIndex].Headers.Count > childLine;
            Headers.Add(hasHeader ? left[childIndex].Headers[childLine] : string.Empty);
            childLine += 1;
            if (childIsValid && y + scrollValue == startOfNextSegment[childIndex + 1] - 1) {
               (childIndex, childLine) = (childIndex + 1, 0);
            }
         }
         CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
      }
   }
}
