#nullable enable
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using HavenSoft.HexManiac.Core.ViewModels.Visitors;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.Models;

public record CrossTableRow(int Address, IDataFormat Format); // use UnderEdit(null, ...) for non-data items, like separators and headers

// we want editing to work the same as for normal ViewPorts...
// maybe we don't need a full ViewPort here, but can just use a custom Scroll engine to specify that the data is different?
// but we still need the headers to be different

public class CrossTableDataViewPort : ViewModelCore, IViewPort {
   private readonly ViewPort parent;
   private List<CrossTableRow> rows = new();
   private int height;

   public CrossTableDataViewPort(ViewPort parent) {
      this.parent = parent;
   }

   public HexElement this[int x, int y] {
      get {
         y += scrollValue;
         if (y >= rows.Count) return new HexElement(0, false, Undefined.Instance);
         var row = rows[y];
         return new HexElement(Model[row.Address], false, row.Format);
      }
      set { }
   }

   #region IViewPort

   public double ToolPanelWidth { get => 0; set { } }

   public string FileName => parent.FileName;

   public string FullFileName => parent.FullFileName;

   public int Width { get => 4; set { } }
   public int Height { get => height; set => Set(ref height, value); }
   public bool AutoAdjustDataWidth { get => false; set { } }
   public bool StretchData { get => false; set { } }
   public bool AllowMultipleElementsPerLine { get => false; set { } }
   public bool Base10Length { get => parent.Base10Length; set { } }
   public bool UseCustomHeaders { get => true; set { } }

   public int MinimumScroll => 0;

   private int scrollValue;
   public int ScrollValue { get => scrollValue; set => Set(ref scrollValue, value); }

   public int MaximumScroll => rows.Count - 1;

   public ObservableCollection<RowHeader> Headers { get; private set; } = new();
   private void UpdateRowHeaders() {
      var headers = new ObservableCollection<RowHeader>();
      for (int i = 0; i < Height; i++) {
         int y = i + scrollValue;
         if (rows[y].Address < 0 || Model.GetNextRun(rows[y].Address) is not ITableRun table) {
            headers.Add(new() { Content = string.Empty });
            continue;
         }
         var offset = table.ConvertByteOffsetToArrayOffset(rows[i].Address - table.Start);
         headers.Add(new() { Content = table.ElementContent[offset.SegmentIndex].Name });
      }
      Headers = headers;
      NotifyPropertyChanged(nameof(Headers));
   }

   public ObservableCollection<ColumnHeaderRow> ColumnHeaders { get; } = new() { new ColumnHeaderRow("data", 4) };

   public int DataOffset => 0;

   private StubCommand? scrollCommand;
   public ICommand Scroll => scrollCommand ??= new() { CanExecute = CanScroll, Execute = ScrollExecute };

   public double Progress => 0;

   public bool UpdateInProgress => false;

   public bool CanFindFreeSpace => false;

   public string SelectedAddress { get => string.Empty; set { } }

   public string SelectedBytes => string.Empty;

   public string AnchorText => string.Empty;

   public bool AnchorTextVisible => false;

   public byte[] FindBytes { get => Array.Empty<byte>(); set { } }

   public IDataModel Model => parent.Model;

   public bool HasTools => false;

   public ChangeHistory<ModelDelta> ChangeHistory => parent.ChangeHistory;

   public IToolTrayViewModel Tools { get; } = new EmptyToolTray();

   public string Name => parent.Name;

   public bool IsMetadataOnlyChange => parent.IsMetadataOnlyChange;

   public ICommand Save => parent.Save;

   public ICommand SaveAs => parent.SaveAs;

   public ICommand ExportBackup => parent.ExportBackup;

   public ICommand Undo => parent.Undo;

   public ICommand Redo => parent.Redo;

   private StubCommand? copy, deepCopy, clear, selectAll;
   public ICommand Copy => copy ??= new() { CanExecute = CanCopy, Execute = CopyExecute };

   public ICommand DeepCopy => deepCopy ??= new() { CanExecute = CanDeepCopy, Execute = DeepCopyExecute };

   public ICommand Clear => clear ??= new() { CanExecute = CanClear, Execute = ClearExecute };

   public ICommand SelectAll => new StubCommand();

   public ICommand Goto => new StubCommand();

   public ICommand ResetAlignment => new StubCommand();

   public ICommand Back => new StubCommand();

   public ICommand Forward => new StubCommand();

   public ICommand Close => new StubCommand();

   public ICommand Diff => new StubCommand();

   public ICommand DiffLeft => new StubCommand();

   public ICommand DiffRight => new StubCommand();

   public bool SpartanMode { get => false; set { } }

   public bool CanIpsPatchRight => false;

   public bool CanUpsPatchRight => false;

   public bool CanDuplicate => false;

   public event EventHandler? PreviewScrollChanged;
   public event EventHandler<IDataModel>? RequestCloseOtherViewports;
   public event EventHandler<string>? OnError;
   public event EventHandler<string>? OnMessage;
   public event EventHandler? ClearMessage;
   public event EventHandler? Closed;
   public event EventHandler<TabChangeRequestedEventArgs>? RequestTabChange;
   public event EventHandler<Action>? RequestDelayedWork;
   public event EventHandler? RequestMenuClose;
   public event EventHandler<Direction>? RequestDiff;
   public event EventHandler<CanDiffEventArgs>? RequestCanDiff;
   public event EventHandler<CanPatchEventArgs>? RequestCanCreatePatch;
   public event EventHandler<CanPatchEventArgs>? RequestCreatePatch;
   public event EventHandler? RequestRefreshGotoShortcuts;
   public event PropertyChangedEventHandler? PropertyChanged;
   public event NotifyCollectionChangedEventHandler? CollectionChanged;

   public void ConsiderReload(IFileSystem fileSystem) => parent.ConsiderReload(fileSystem);

   public IChildViewPort CreateChildView(int startAddress, int endAddress) => throw new NotImplementedException();

   public void Duplicate() { }

   public void ExpandSelection(int x, int y) { }

   public IReadOnlyList<(int start, int end)> Find(string search, bool matchExactCase = false) => parent.Find(search, matchExactCase);

   public void FindAllSources(int x, int y) => parent.FindAllSources(x, y);

   public void FindFreeSpace(IFileSystem fileSystem) => parent.FindFreeSpace(fileSystem);

   public void FollowLink(int x, int y) => parent.FollowLink(x, y);

   public void IpsPatchRight() { }

   public bool IsTable(Point point) => true;

   public IDataModel ModelFor(Point point) => parent.Model;

   public bool TryImport(LoadedFile file, IFileSystem fileSystem) => false;

   public void UpsPatchRight() { }

   #endregion

   public IReadOnlyList<IContextItem> GetContextMenuItems(Point point, IFileSystem fileSystem) {
      // TODO
      return new List<IContextItem>();
   }

   public bool IsSelected(Point point) {
      return false;
   }

   private bool visible;
   public bool Visible { get => visible; set => Set(ref visible, value, oldValue => VisibleChanged()); }
   private void VisibleChanged() {
      if (visible && needsRefresh) Refresh();
   }

   private bool needsRefresh;
   public void Refresh() {
      needsRefresh = true;
      if (!visible) return;
      var selectedAddress = parent.ConvertViewPointToAddress(parent.SelectionStart);
      if (Model.GetNextRun(selectedAddress) is not ArrayRun arrayRun) return;

      var basename = Model.GetAnchorFromAddress(-1, arrayRun.Start);
      var baseElementIndex = (selectedAddress - arrayRun.Start) / arrayRun.ElementLength;
      var originalTableName = basename;
      if (!string.IsNullOrEmpty(arrayRun.LengthFromAnchor) && Model.GetMatchedWords(arrayRun.LengthFromAnchor).Count == 0) basename = arrayRun.LengthFromAnchor; // basename is now a 'parent table' name, if there is one
      IReadOnlyList<TableGroup> groups = Model.GetTableGroups(basename) ?? new[] { new TableGroup(TableGroupViewModel.DefaultName, new[] { originalTableName }) };

      // now we have to get fields from all the tables and use them to fill the rows, including blank rows
      rows.Clear();
      foreach (var group in groups) {
         foreach (var table in group.Tables) {
            var tableRun = Model.GetTable(table);
            var tableIndex = baseElementIndex;
            if (tableRun is ArrayRun array) tableIndex -= array.ParentOffset.BeginningMargin;
            var start = tableRun.Start + tableIndex * tableRun.ElementLength;
            rows.Add(new CrossTableRow(-1, new UnderEdit(Undefined.Instance, table)));
            foreach (var field in tableRun.ElementContent) {
               var format = tableRun.CreateDataFormat(Model, start);
               rows.Add(new CrossTableRow(start, format));
            }
         }
         rows.Add(new(-1, new UnderEdit(Undefined.Instance, string.Empty))); // separator between groups
      }

      needsRefresh = false;
   }

   private bool CanScroll(object? parameter) {
      if (parameter is not Direction direction) return false;
      if (!direction.IsAny(Direction.Up, Direction.Down)) return false;
      var dy = direction == Direction.Down ? 1 : -1;
      return (scrollValue + dy).InRange(MinimumScroll, MaximumScroll + 1);
   }

   private void ScrollExecute(object? parameter) {
      if (parameter is not Direction direction || !direction.IsAny(Direction.Up, Direction.Down)) return;
      var dy = direction == Direction.Down ? 1 : -1;
      ScrollValue += dy;
      UpdateRowHeaders();
   }

   private bool CanCopy(object? parameter) {
      if (parameter is not IFileSystem fs) return false;
      // Copying is not yet supported on Cross Table Viewports.
      return false;
   }

   private void CopyExecute(object? parameter) {
      if (parameter is not IFileSystem fs) return;
   }

   private bool CanDeepCopy(object? parameter) {
      if (parameter is not IFileSystem fs) return false;
      // Copying is not yet supported on Cross Table Viewports.
      return false;
   }

   private void DeepCopyExecute(object? parameter) {
      if (parameter is not IFileSystem fs) return;
   }

   private bool CanClear(object? parameter) {
      if (parameter is not IFileSystem fs) return false;
      // Clearing is not yet supported on Cross Table Viewports.
      return false;
   }

   private void ClearExecute(object? parameter) {
      if (parameter is not IFileSystem fs) return;
   }
}
