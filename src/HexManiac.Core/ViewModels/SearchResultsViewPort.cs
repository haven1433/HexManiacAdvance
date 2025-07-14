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

namespace HavenSoft.HexManiac.Core.ViewModels {
   public class SelectionRange { public virtual int Start { get; init; } public virtual int End { get; init; } }
   public class SelectionRangeGroup : SelectionRange, IEnumerable<SelectionRange> {
      public readonly List<SelectionRange> children = new();
      public override int Start { get => throw new NotImplementedException(); init => base.Start = value; }
      public override int End { get => throw new NotImplementedException(); init => base.End = value; }
      public void Add(SelectionRange range) => children.Add(range);
      public SelectionRange this[int i] => children[i];

      public IEnumerator<SelectionRange> GetEnumerator() => children.GetEnumerator();
      IEnumerator IEnumerable.GetEnumerator() => children.GetEnumerator();
   }

   public class SearchResultsViewPort : ViewModelCore, IViewPort {
      public readonly List<IChildViewPort> children = new List<IChildViewPort>();
      public readonly Dictionary<IViewPort, int> firstChildToUseParent = new Dictionary<IViewPort, int>();
      public readonly List<SelectionRange> childrenSelection = new List<SelectionRange>();
      public int width, height, scrollValue, maxScrollValue;

      public int ChildViewCount => children.Count;

      #region Implementing IViewPort

      public int MinimumScroll => 0;
      public event EventHandler PreviewScrollChanged;
      public int MaximumScroll { get => maxScrollValue; set => TryUpdate(ref maxScrollValue, value); }
      public ObservableCollection<RowHeader> Headers { get; } = new ObservableCollection<RowHeader>();
      public double ToolPanelWidth { get; set; } = 500;
      public string Name { get; }
      public string FullFileName { get; }
      public string FileName => string.Empty;
      public bool SpartanMode { get; set; }
      public bool IsMetadataOnlyChange => false;
      public byte[] FindBytes { get; set; }
      public bool CanDuplicate => false;
      public bool Base10Length { get; set; }
      public void Duplicate() { }

      public bool UpdateInProgress => false;
      public double Progress => 0;

      public IDataModel Model => null;
      public bool HasTools => false;
      public bool AutoAdjustDataWidth { get; set; }
      public bool StretchData { get; set; }
      public bool AllowMultipleElementsPerLine { get; set; }

      public ChangeHistory<ModelDelta> ChangeHistory { get; }
      public IToolTrayViewModel Tools { get; } = new SearchResultsTools();

      public string SelectedAddress { get => string.Empty; set { } }
      public string SelectedBytes => string.Empty;

      public string AnchorText { get; set; }

      public bool AnchorTextVisible => false;

      public ObservableCollection<ColumnHeaderRow> ColumnHeaders { get; }

#pragma warning disable 0067 // it's ok if events are never used
      public event EventHandler<string> OnError;
      public event EventHandler<string> OnMessage;
      public event EventHandler ClearMessage;
      public event EventHandler Closed;
      public event NotifyCollectionChangedEventHandler CollectionChanged;
      public event EventHandler<TabChangeRequestedEventArgs> RequestTabChange;
      public event EventHandler<IDataModel> RequestCloseOtherViewports;
      public event EventHandler<Action> RequestDelayedWork;
      public event EventHandler RequestMenuClose;
      public event EventHandler<Direction> RequestDiff;
      event EventHandler<CanPatchEventArgs> ITabContent.RequestCanCreatePatch { add { } remove { } }
      event EventHandler<CanPatchEventArgs> ITabContent.RequestCreatePatch { add { } remove { } }
      public event EventHandler<CanDiffEventArgs> RequestCanDiff;
      event EventHandler ITabContent.RequestRefreshGotoShortcuts { add { } remove { } }
#pragma warning restore 0067

      public bool CanIpsPatchRight => false;
      public bool CanUpsPatchRight => false;
      public void IpsPatchRight() { }
      public void UpsPatchRight() { }

      #endregion

      public SearchResultsViewPort(string searchTerm) {
         FullFileName = $"Results for {searchTerm}";
         Name = searchTerm.Length > 24 ? searchTerm.Substring(0, 23) + "…" : searchTerm;

         width = 4;
         height = 4;
      }

      public IChildViewPort CreateChildView(int startAddress, int endAddress) => throw new NotImplementedException();

      // if asked to search the search results... just don't
      public IReadOnlyList<(int, int)> Find(string search, bool matchExactCase = false) => new (int, int)[0];

      public bool CanFindFreeSpace => false;
      public void FindFreeSpace(IFileSystem fs) { }

      public bool IsTable(Point point) => false;

      public void ConsiderReload(IFileSystem fileSystem) { }

      public bool TryImport(LoadedFile file, IFileSystem fileSystem) => false;
   }

   public class SearchResultsTools : IToolTrayViewModel {
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
      public IToolViewModel SelectedTool { get => null; set { } }
      public int Count => 0;
      public IEnumerator<IToolViewModel> GetEnumerator() => new List<IToolViewModel>().GetEnumerator();
      IEnumerator IEnumerable.GetEnumerator() => new List<IToolViewModel>().GetEnumerator();
   }
}
