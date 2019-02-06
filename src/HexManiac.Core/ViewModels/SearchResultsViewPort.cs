using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels {
   public class SearchResultsViewPort : ViewModelCore, IViewPort {
      private readonly StubCommand scroll, close;
      private readonly List<IChildViewPort> children = new List<IChildViewPort>();
      private int width, height, scrollValue, maxScrollValue;

      #region Implementing IViewPort

      public HexElement this[int x, int y] {
         get {
            var (childIndex, line) = GetChildLine(y);
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
      public int ScrollValue {
         get => scrollValue;
         set {
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
      public string FileName => string.Empty;
      public ICommand Save { get; } = new StubCommand();
      public ICommand SaveAs { get; } = new StubCommand();
      public ICommand Undo { get; } = new StubCommand();
      public ICommand Redo { get; } = new StubCommand();
      public ICommand Copy { get; } = new StubCommand();
      public ICommand Clear { get; } = new StubCommand();
      public ICommand Goto { get; } = new StubCommand();
      public ICommand Back { get; } = new StubCommand();
      public ICommand Forward { get; } = new StubCommand();
      public ICommand Close => close;

      public IDataModel Model => null;

      public bool HasTools => false;

      public IToolTrayViewModel Tools => null;

      public string AnchorText { get; set; }

      public bool AnchorTextVisible => false;

#pragma warning disable 0067 // it's ok if events are never used
      public event EventHandler<string> OnError;
      public event EventHandler<string> OnMessage;
      public event EventHandler ClearMessage;
      public event EventHandler Closed;
      public event NotifyCollectionChangedEventHandler CollectionChanged;
      public event EventHandler<ITabContent> RequestTabChange;
      public event EventHandler<Action> RequestDelayedWork;
#pragma warning restore 0067

      #endregion

      public SearchResultsViewPort(string searchTerm) {
         Name = $"Results for {searchTerm}";
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

      public void Add(IChildViewPort child) {
         children.Add(child);
         maxScrollValue += child.Height;
         if (children.Count > 1) maxScrollValue++;
         NotifyCollectionChanged();
      }

      public IChildViewPort CreateChildView(int offset) => throw new NotImplementedException();

      // if asked to search the search results... just don't
      public IReadOnlyList<int> Find(string search) => new int[0];

      public bool IsSelected(Point point) {
         var (x, y) = (point.X, point.Y);
         if (y < 0 || y > height || x < 0 || x > width) return false;
         var (childIndex, line) = GetChildLine(y);
         if (line == -1 || childIndex >= children.Count) return false;

         return children[childIndex].IsSelected(new Point(x, line));
      }

      public void FollowLink(int x, int y) {
         if (y < 0 || y > height || x < 0 || x > width) return;
         var (childIndex, line) = GetChildLine(y);
         if (line == -1 || childIndex >= children.Count) return;

         var child = children[childIndex];
         var parent = child.Parent;
         var dataOffset = Math.Max(0, child.DataOffset - (y - line) * child.Width);
         parent.Goto.Execute(dataOffset.ToString("X6"));
         RequestTabChange?.Invoke(this, parent);
      }

      public void ExpandSelection(int x, int y) => FollowLink(x, y);

      public void ConsiderReload(IFileSystem fileSystem) { }

      public void FindAllSources(int x, int y) {
         if (y < 0 || y > height || x < 0 || x > width) return;
         var (childIndex, line) = GetChildLine(y);
         if (line == -1 || childIndex >= children.Count) return;

         children[childIndex].FindAllSources(x, y);
      }

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
         int line = y + scrollValue; // 0 is the first line in the data
         int childIndex = 0;
         while (childIndex < children.Count && children[childIndex].Height <= line) {
            line -= children[childIndex].Height + 1; childIndex++;
         }
         return (childIndex, line);
      }

   }
}
