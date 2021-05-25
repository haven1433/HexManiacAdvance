using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using HavenSoft.HexManiac.Core.ViewModels.Visitors;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels {
   public class DiffViewPort : ViewModelCore, IViewPort {
      private readonly IViewPort left, right;

      private HexElement[,] cell;
      private void FillCells() {
         cell = new HexElement[Width, Height];
         for (int y = 0; y < Height; y++) {
            for (int x = 0; x < left.Width; x++) {
               cell[x, y] = left[x, y];
            }
            cell[left.Width, y] = new HexElement(default, default, DataFormats.Undefined.Instance);
            for (int x = 0; x < right.Width; x++) {
               cell[left.Width + 1 + x, y] = right[x, y];
            }
         }
         CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
      }

      public DiffViewPort(IViewPort left, IViewPort right) {
         (this.left, this.right) = (left, right);
      }

      public HexElement this[int x, int y] {
         get {
            if (x < 0 || y < 0 || x >= cell.GetLength(0) || y >= cell.GetLength(1)) return new HexElement(default, default, DataFormats.Undefined.Instance);
            return cell[x, y];
         }
      }

      public string FileName => string.Empty;

      public string FullFileName => string.Empty;

      public int Width { get => left.Width + right.Width + 1; set { } }
      public int Height { get => left.Height; set {
            left.Height = value;
            right.Height = value;
      } }
      public bool AutoAdjustDataWidth { get; set; }
      public bool StretchData { get; set; }
      public bool AllowMultipleElementsPerLine { get; set; }
      public bool UseCustomHeaders { get; set; }

      public int MinimumScroll => 0;

      private int scrollValue;
      public int ScrollValue { get => scrollValue; set => Set(ref scrollValue, value.LimitToRange(MinimumScroll, MaximumScroll), ScrollValueChanged); }
      private void ScrollValueChanged(int oldValue) {
         left.ScrollValue = scrollValue;
         right.ScrollValue = scrollValue;
         FillCells();
      }

      public int MaximumScroll => left.MaximumScroll;

      public ObservableCollection<string> Headers => left.Headers;

      public ObservableCollection<HeaderRow> ColumnHeaders => null;

      public int DataOffset => 0;

      private StubCommand scrollCommand;
      public ICommand Scroll => StubCommand<Direction>(ref scrollCommand, ExecuteScroll);
      private void ExecuteScroll(Direction direction) {
         left.Scroll.Execute(direction);
         right.Scroll.Execute(direction);
         FillCells();
      }

      public double Progress => 0;

      public bool UpdateInProgress => false;

      public string SelectedAddress => string.Empty;

      public string SelectedBytes => string.Empty;

      public string AnchorText { get; set; }

      public bool AnchorTextVisible => false;

      public IDataModel Model => left.Model;

      public bool HasTools => false;

      public ChangeHistory<ModelDelta> ChangeHistory => left.ChangeHistory;

      public IToolTrayViewModel Tools => null;

      public string Name => left.FileName + " -> " + right.FileName;
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

      public bool IsSelected(Point point) => false;

      public bool IsTable(Point point) => false;

      public void Refresh() {
         FillCells();
      }

      public bool TryImport(LoadedFile file, IFileSystem fileSystem) {
         throw new NotImplementedException();
      }

      public void ValidateMatchedWords() { }
   }
}
