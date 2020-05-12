using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using HavenSoft.HexManiac.Core.ViewModels.Visitors;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels {
   public class SearchResultsViewPort : ViewModelCore, IViewPort {
      private readonly StubCommand scroll, close;
      private readonly List<IChildViewPort> children = new List<IChildViewPort>();
      private readonly Dictionary<IViewPort, int> firstChildToUseParent = new Dictionary<IViewPort, int>();
      private readonly List<(int start, int end)> childrenSelection = new List<(int, int)>();
      private int width, height, scrollValue, maxScrollValue;

      #region Implementing IViewPort

      public HexElement this[int x, int y] {
         get {
            var (childIndex, line) = GetChildLine(y);
            if (line < 0 && x == 3 && childIndex < children.Count && firstChildToUseParent[children[childIndex].Parent] == childIndex) {
               return new HexElement(0, false, new UnderEdit(null, "Results from " + children[childIndex].FileName, width));
            }
            if (line < 0 || childIndex >= children.Count) return HexElement.Undefined;

            return children[childIndex][x, line];
         }
      }

      public int Width {
         get => width;
         set {
            if (TryUpdate(ref width, value)) NotifyCollectionChanged();
         }
      }
      public int Height {
         get => height;
         set {
            if (TryUpdate(ref height, value)) NotifyCollectionChanged();
         }
      }
      public int MinimumScroll => 0;
      public event EventHandler PreviewScrollChanged;
      public int ScrollValue {
         get => scrollValue;
         set {
            PreviewScrollChanged?.Invoke(this, EventArgs.Empty);
            value = value.LimitToRange(0, MaximumScroll);
            if (TryUpdate(ref scrollValue, value)) NotifyCollectionChanged();
         }
      }
      public int MaximumScroll { get => maxScrollValue; private set => TryUpdate(ref maxScrollValue, value); }
      public ObservableCollection<string> Headers { get; } = new ObservableCollection<string>();
      public ICommand Scroll => scroll;
      public int DataOffset {
         get {
            var (childIndex, line) = GetChildLine(0);
            var child = children[childIndex];
            return child.Width * line + child.DataOffset;
         }
      }
      public string Name { get; }
      public string FullFileName { get; }
      public string FileName => string.Empty;
      public ICommand Save { get; } = new StubCommand();
      public ICommand SaveAs { get; } = new StubCommand();
      public ICommand Undo { get; } = new StubCommand();
      public ICommand Redo { get; } = new StubCommand();
      public ICommand Copy { get; } = new StubCommand();
      public ICommand DeepCopy { get; } = new StubCommand();
      public ICommand Clear { get; } = new StubCommand();
      public ICommand SelectAll { get; } = new StubCommand();
      public ICommand Goto { get; } = new StubCommand();
      public ICommand ResetAlignment { get; } = new StubCommand();
      public ICommand Back { get; } = new StubCommand();
      public ICommand Forward { get; } = new StubCommand();
      public ICommand Close => close;

      public IDataModel Model => null;

      public bool HasTools => false;

      public IToolTrayViewModel Tools { get; } = new SearchResultsTools();

      public string SelectedAddress => string.Empty;
      public string SelectedBytes => string.Empty;

      public string AnchorText { get; set; }

      public bool AnchorTextVisible => false;

      public ObservableCollection<HeaderRow> ColumnHeaders { get; }

#pragma warning disable 0067 // it's ok if events are never used
      public event EventHandler<string> OnError;
      public event EventHandler<string> OnMessage;
      public event EventHandler ClearMessage;
      public event EventHandler Closed;
      public event NotifyCollectionChangedEventHandler CollectionChanged;
      public event EventHandler<ITabContent> RequestTabChange;
      public event EventHandler<Action> RequestDelayedWork;
      public event EventHandler RequestMenuClose;
#pragma warning restore 0067

      #endregion

      public SearchResultsViewPort(string searchTerm) {
         FullFileName = $"Results for {searchTerm}";
         Name = FullFileName.Length > 24 ? FullFileName.Substring(0, 23) + "…" : FullFileName;

         width = 4;
         height = 4;

         scroll = new StubCommand {
            CanExecute = arg => (Direction)arg != Direction.Left && (Direction)arg != Direction.Right,
            Execute = arg => {
               var direction = (Direction)arg;
               if (direction == Direction.Up) ScrollValue--;
               if (direction == Direction.Down) ScrollValue++;
            },
         };

         close = new StubCommand {
            CanExecute = arg => true,
            Execute = arg => Closed?.Invoke(this, EventArgs.Empty),
         };
      }

      public void Add(IChildViewPort child, int start, int end) {
         children.Add(child);
         childrenSelection.Add((start, end));
         maxScrollValue += child.Height;
         if (children.Count > 1) maxScrollValue++;
         if (!firstChildToUseParent.ContainsKey(child.Parent)) {
            firstChildToUseParent.Add(child.Parent, children.Count - 1);
         }
         NotifyCollectionChanged();
      }

      public IChildViewPort CreateChildView(int startAddress, int endAddress) => throw new NotImplementedException();

      // if asked to search the search results... just don't
      public IReadOnlyList<(int, int)> Find(string search) => new (int, int)[0];

      public bool UseCustomHeaders {
         get => children.FirstOrDefault()?.UseCustomHeaders ?? false;
         set {
            children.ForEach(child => child.UseCustomHeaders = value);
            UpdateHeaders();
         }
      }

      public bool IsSelected(Point point) {
         var (x, y) = (point.X, point.Y);
         if (y < 0 || y > height || x < 0 || x > width) return false;
         var (childIndex, line) = GetChildLine(y);
         if (line == -1 || childIndex >= children.Count) return false;

         return children[childIndex].IsSelected(new Point(x, line));
      }

      public bool IsTable(Point point) => false;

      public void FollowLink(int x, int y) {
         if (y < 0 || y > height || x < 0 || x > width) return;
         var (childIndex, line) = GetChildLine(y);
         if (line == -1 || childIndex >= children.Count) return;

         var child = children[childIndex];
         var parent = child.Parent;
         if (child.Model.GetNextRun(child.DataOffset) is ArrayRun array) {
            parent.Goto.Execute(child.DataOffset.ToString("X6"));
            parent.ScrollValue += line - y + Height - parent.Height;
            // heuristic: if the parent height matches the search results height, then the parent
            // probably doesn't have labels yet but is about to get them. We don't know how big the
            // labels will be, but they will probably push all the data down quite a bit.
            // compensate by scrolling slightly
            if (parent.Height == Height) parent.ScrollValue += 2;
         } else {
            var dataOffset = Math.Max(0, child.DataOffset - (y - line) * child.Width);
            parent.Goto.Execute(dataOffset.ToString("X6"));
         }

         if (parent is ViewPort viewPort) {
            SelectRange(viewPort, childrenSelection[childIndex]);
         }

         RequestTabChange?.Invoke(this, parent);
      }

      public static void SelectRange(ViewPort viewPort, (int start, int end) range) {
         viewPort.SelectionStart = viewPort.ConvertAddressToViewPoint(range.start);
         viewPort.SelectionEnd = viewPort.ConvertAddressToViewPoint(range.end);
      }

      public void ExpandSelection(int x, int y) => FollowLink(x, y);

      public void ConsiderReload(IFileSystem fileSystem) { }

      public void FindAllSources(int x, int y) {
         if (y < 0 || y > height || x < 0 || x > width) return;
         var (childIndex, line) = GetChildLine(y);
         if (line == -1 || childIndex >= children.Count) return;

         children[childIndex].FindAllSources(x, y);
      }

      public void ValidateMatchedWords() { }

      public IReadOnlyList<IContextItem> GetContextMenuItems(Point selectionPoint) {
         return new[] { new ContextItem("Open in Main Tab", arg => {
            FollowLink(selectionPoint.X, selectionPoint.Y);
            RequestMenuClose?.Invoke(this, EventArgs.Empty);
         }) { ShortcutText = "Ctrl+Click" } };
      }

      public void Refresh() { }

      private void NotifyCollectionChanged() {
         if (children.Count == 0) return;
         UpdateHeaders();
         CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
      }

      private void UpdateHeaders() {
         Headers.Clear();
         for (int i = 0; i < height; i++) {
            var (childIndex, line) = GetChildLine(i);
            if (line == -1 || childIndex >= children.Count) { // blank line between results / after results
               Headers.Add(string.Empty);
            } else {
               Headers.Add(children[childIndex].Headers[line]);
            }
         }
      }

      /// <param name="y">0 is the first line in the view</param>
      /// <returns>
      /// Which child (and which line of that child) should be used to represent the y-line in the view,
      /// based on scrolling in the overall search results and within each individual result.
      /// </returns>
      private (int childIndex, int childLineNumber) GetChildLine(int y) {
         int line = y + scrollValue - 1; // 0 is the first line in the data. Include one empty line at the top for the file name
         int childIndex = 0;
         while (childIndex < children.Count && children[childIndex].Height <= line) {
            line -= children[childIndex].Height + 1; childIndex++;
         }
         return (childIndex, line);
      }
   }

   public class SearchResultsTools : IToolTrayViewModel {
      public ICommand HideCommand { get; } = new StubCommand();
      public ICommand StringToolCommand { get; } = new StubCommand();
      public ICommand TableToolCommand { get; } = new StubCommand();
      public ICommand SpriteToolCommand { get; } = new StubCommand();
      public ICommand CodeToolCommand { get; } = new StubCommand();

      public PCSTool StringTool => null;
      public TableTool TableTool => null;
      public SpriteTool SpriteTool => null;
      public CodeTool CodeTool => null;

      public IDisposable DeferUpdates => new StubDisposable();

#pragma warning disable 0067 // it's ok if events are never used after implementing an interface
      public event EventHandler<string> OnError;
      public event EventHandler<string> OnMessage;
      public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore 0067

      public void Schedule(Action action) => action();
      public void RefreshContent() { }

      public IToolViewModel this[int index] => null;
      public int SelectedIndex { get => -1; set { } }
      public IToolViewModel SelectedTool => null;
      public int Count => 0;
      public IEnumerator<IToolViewModel> GetEnumerator() => new List<IToolViewModel>().GetEnumerator();
      IEnumerator IEnumerable.GetEnumerator() => new List<IToolViewModel>().GetEnumerator();
   }
}
