using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using HavenSoft.HexManiac.Core.ViewModels.Visitors;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;

namespace HavenSoft.HexManiac.Core.ViewModels {
   /// <summary>
   ///  exactly replaces a ViewPort, except that it also has a known parent view
   /// </summary>
   public class ChildViewPort : ViewPort, IChildViewPort {
      public IViewPort Parent { get; }

      public void RefreshHeaders() => ScrollRegion.UpdateDeferedHeader();
   }

   public class CompositeChildViewPort : List<IChildViewPort>, IChildViewPort {

      public void RefreshHeaders() => this[0].RefreshHeaders();

      public IViewPort Parent => this[0].Parent;

      public double ToolPanelWidth { get; set; } = 500;

      public string FileName => this[0].FileName;

      public string FullFileName => this[0].FullFileName;

      public bool SpartanMode { get; set; }

      public int PreferredWidth { get => this[0].PreferredWidth; set => ForEach(child => child.PreferredWidth = value); }
      public bool AutoAdjustDataWidth { get => this[0].AutoAdjustDataWidth; set => ForEach(child => child.AutoAdjustDataWidth = value); }
      public bool StretchData { get => this[0].StretchData; set => ForEach(child => child.StretchData = value); }
      public bool AllowMultipleElementsPerLine { get => this[0].AllowMultipleElementsPerLine; set => ForEach(child => child.AllowMultipleElementsPerLine = value); }
      public bool Base10Length { get => this[0].Base10Length; set => ForEach(child => child.Base10Length = value); }

      public int MinimumScroll => this[0].MinimumScroll;

      public int MaximumScroll => this[0].MaximumScroll;

      public ObservableCollection<RowHeader> Headers => this[0].Headers;

      public ObservableCollection<ColumnHeaderRow> ColumnHeaders => this[0].ColumnHeaders;

      public double Progress => this[0].Progress;

      public bool UpdateInProgress => this[0].UpdateInProgress;

      public string SelectedAddress { get => this[0].SelectedAddress; set => this[0].SelectedAddress = value; }

      public string SelectedBytes => this[0].SelectedBytes;

      public bool AnchorTextVisible => this[0].AnchorTextVisible;

      public IDataModel Model => this[0].Model;

      public bool HasTools => this[0].HasTools;

      public ChangeHistory<ModelDelta> ChangeHistory => this[0].ChangeHistory;

      public IToolTrayViewModel Tools => this[0].Tools;

      public string Name => this[0].Name;

      public bool IsMetadataOnlyChange => false;

      public byte[] FindBytes { get; set; }
      public bool CanDuplicate => false;
      public void Duplicate() { }

      public event EventHandler PreviewScrollChanged { add => ForEach(child => child.PreviewScrollChanged += value); remove => ForEach(child => child.PreviewScrollChanged -= value); }
      public event EventHandler<string> OnError { add => ForEach(child => child.OnError += value); remove => ForEach(child => child.OnError -= value); }
      public event EventHandler<string> OnMessage { add => ForEach(child => child.OnMessage += value); remove => ForEach(child => child.OnMessage -= value); }
      public event EventHandler ClearMessage { add => ForEach(child => child.ClearMessage += value); remove => ForEach(child => child.ClearMessage -= value); }
      public event EventHandler Closed { add => ForEach(child => child.Closed += value); remove => ForEach(child => child.Closed -= value); }
      public event EventHandler<TabChangeRequestedEventArgs> RequestTabChange { add => ForEach(child => child.RequestTabChange += value); remove => ForEach(child => child.RequestTabChange -= value); }
      public event EventHandler<IDataModel> RequestCloseOtherViewports { add => ForEach(child => child.RequestCloseOtherViewports += value); remove => ForEach(child => child.RequestCloseOtherViewports -= value); }
      public event EventHandler<Action> RequestDelayedWork { add => ForEach(child => child.RequestDelayedWork += value); remove => ForEach(child => child.RequestDelayedWork -= value); }
      public event EventHandler RequestMenuClose { add => ForEach(child => child.RequestMenuClose += value); remove => ForEach(child => child.RequestMenuClose -= value); }
      public event EventHandler<Direction> RequestDiff { add => ForEach(child => child.RequestDiff += value); remove => ForEach(child => child.RequestDiff -= value); }
      public event EventHandler<CanDiffEventArgs> RequestCanDiff { add => ForEach(child => child.RequestCanDiff += value); remove => ForEach(child => child.RequestCanDiff -= value); }
      event EventHandler<CanPatchEventArgs> ITabContent.RequestCanCreatePatch { add { } remove { } }
      event EventHandler<CanPatchEventArgs> ITabContent.RequestCreatePatch { add { } remove { } }
      public event PropertyChangedEventHandler PropertyChanged { add => ForEach(child => child.PropertyChanged += value); remove => ForEach(child => child.PropertyChanged -= value); }
      public event NotifyCollectionChangedEventHandler CollectionChanged { add => ForEach(child => child.CollectionChanged += value); remove => ForEach(child => child.CollectionChanged -= value); }
      public event EventHandler RequestRefreshGotoShortcuts { add => ForEach(child => child.RequestRefreshGotoShortcuts += value);remove => ForEach(child => child.RequestRefreshGotoShortcuts -= value); }

      public bool CanIpsPatchRight => false;
      public bool CanUpsPatchRight => false;
      public void IpsPatchRight() { }
      public void UpsPatchRight() { }

      public void ConsiderReload(IFileSystem fileSystem) => throw new NotImplementedException();

      public IChildViewPort CreateChildView(int startAddress, int endAddress) {
         throw new NotImplementedException();
      }

      public IReadOnlyList<(int start, int end)> Find(string search, bool matchExactCase = false) => new (int, int)[0];

      public bool CanFindFreeSpace => false;
      public void FindFreeSpace(IFileSystem fs) { }

      public void FindAllSources(int x, int y) { }

      public void FollowLink(int x, int y) { }

      public IReadOnlyList<IContextItem> GetContextMenuItems(Point point, IFileSystem fileSystem) => new IContextItem[0];

      public bool IsTable(Point point) => this[0].IsTable(point);

      public bool TryImport(LoadedFile file, IFileSystem fileSystem) => false;
   }
}
