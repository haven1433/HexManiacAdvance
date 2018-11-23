using HavenSoft.Gen3Hex.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;

namespace HavenSoft.Gen3Hex.ViewModel {
   public class CompositeViewPort : ViewModelCore, IViewPort {
      private readonly StubCommand scroll, close;
      private readonly List<IChildViewPort> children = new List<IChildViewPort>();
      private int width, height, scrollValue, maxScrollValue;

      #region Implementing IViewPort

      public HexElement this[int x, int y] {
         get {
            if (y < 0 || y > height || x < 0 || x > width) return HexElement.Undefined;

            int line = scrollValue + y;
            int childIndex = 0;
            while (childIndex < children.Count && children[childIndex].Height < line) {
               line -= children[childIndex].Height + 1; childIndex++;
            }

            if (line == -1 || childIndex >= children.Count) return HexElement.Undefined;
            return children[childIndex][x, line];
         }
      }

      public int Width {
         get => width; set {
            if (TryUpdate(ref width, value)) NotifyCollectionChanged();
         }
      }
      public int Height {
         get => height; set {
            if (TryUpdate(ref height, value)) NotifyCollectionChanged();
         }
      }
      public int MinimumScroll => 0;
      public int ScrollValue {
         get => scrollValue; set {
            value = value.LimitToRange(0, MaximumScroll);
            if (TryUpdate(ref scrollValue, value)) NotifyCollectionChanged();
         }
      }
      public int MaximumScroll { get => maxScrollValue; private set => TryUpdate(ref maxScrollValue, value); }
      public ObservableCollection<string> Headers { get; } = new ObservableCollection<string>();
      public ICommand Scroll => scroll;
      public string Name { get; }
      public ICommand Save => null;
      public ICommand SaveAs => null;
      public ICommand Undo => null;
      public ICommand Redo => null;
      public ICommand Copy => null;
      public ICommand Clear => null;
      public ICommand Goto => null;
      public ICommand Back => null;
      public ICommand Forward => null;
      public ICommand Close => close;

      public event EventHandler<string> OnError;
      public event EventHandler Closed;
      public event NotifyCollectionChangedEventHandler CollectionChanged;
      public event EventHandler<ITabContent> RequestTabChange;

      #endregion

      public CompositeViewPort(string searchTerm) {
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

      public IReadOnlyList<int> Find(string search) => new int[0];

      public bool IsSelected(Point point) {
         var (x, y) = (point.X, point.Y);
         if (y < 0 || y > height || x < 0 || x > width) return false;

         int line = scrollValue + y;
         int childIndex = 0;
         while (childIndex < children.Count && children[childIndex].Height <= line) {
            line -= children[childIndex].Height + 1; childIndex++;
         }

         if (line == -1 || childIndex >= children.Count) return false;
         return children[childIndex].IsSelected(new Point(x, line));
      }

      public void FollowLink(int x, int y) {
         if (y < 0 || y > height || x < 0 || x > width) return;

         int line = scrollValue + y;
         int childIndex = 0;
         while (childIndex < children.Count && children[childIndex].Height <= line) {
            line -= children[childIndex].Height + 1; childIndex++;
         }

         if (line == -1 || childIndex >= children.Count) return;
         var child = children[childIndex];
         var parent = child.Parent;
         parent.ScrollValue = child.ScrollValue - (y - line);
         RequestTabChange?.Invoke(this, parent);
      }

      private void NotifyCollectionChanged() {
         if (children.Count == 0) return;
         Headers.Clear();
         for (int i = 0; i < height; i++) {
            int line = scrollValue + i;
            int childIndex = 0;
            while (childIndex < children.Count && children[childIndex].Height <= line) {
               line -= children[childIndex].Height + 1; childIndex++;
            }
            if (line == -1 || childIndex >= children.Count) { // blank line between results / after results
               Headers.Add(string.Empty);
            } else {
               Headers.Add(children[childIndex].Headers[line]);
            }
         }
         CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
      }
   }
}
