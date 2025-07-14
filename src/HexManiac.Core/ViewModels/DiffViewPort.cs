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

namespace HavenSoft.HexManiac.Core.ViewModels {
   public class DiffViewPort : ViewModelCore, IViewPort {
      public const int MaxInnerTabWidth = 32;
      public readonly IList<IChildViewPort> left, right;
      public readonly int leftWidth, rightWidth;
      public readonly List<int> startOfNextSegment;

      public HexElement[,] cell;

      public int ChildCount => left.Count;

      #region IViewPort

      public HexElement this[int x, int y] {
         get {
            if (x < 0 || y < 0 || x >= cell.GetLength(0) || y >= cell.GetLength(1)) return new HexElement(default, default, DataFormats.Undefined.Instance);
            return cell[x, y];
         }
      }

      public double ToolPanelWidth { get; set; } = 500;

      public string FileName => string.Empty;

      public string FullFileName => Name;

      public bool SpartanMode { get; set; }

      public int Width { get => leftWidth + rightWidth + 1; set { } }

      public int height;

      public bool AutoAdjustDataWidth { get; set; }
      public bool StretchData { get; set; }
      public bool AllowMultipleElementsPerLine { get; set; }
      public bool UseCustomHeaders { get; set; }

      public int MinimumScroll => 0;

      public int scrollValue;

      public int MaximumScroll => startOfNextSegment[startOfNextSegment.Count - 1] - 1;

      public ObservableCollection<RowHeader> Headers { get; } = new ObservableCollection<RowHeader>();

      public ObservableCollection<ColumnHeaderRow> ColumnHeaders => null;

      public int DataOffset => 0;

      public double Progress => 0;

      public bool UpdateInProgress => false;

      public string SelectedAddress { get => string.Empty; set { } }

      public string SelectedBytes => string.Empty;

      public string AnchorText { get; set; }

      public bool AnchorTextVisible => false;

      public IDataModel Model => null;

      public bool HasTools => false;

      public bool Base10Length { get; set; }

      public ChangeHistory<ModelDelta> ChangeHistory { get; }

      public IToolTrayViewModel Tools => null;

      public string Name => left[0].Parent.Name.Trim('*') + " -> " + right[0].Parent.Name.Trim('*');
      public bool IsMetadataOnlyChange => false;
      public bool CanDuplicate => false;
      public void Duplicate() { }

      public event EventHandler PreviewScrollChanged;
      public event EventHandler<string> OnError;
      public event EventHandler<string> OnMessage;
      public event EventHandler ClearMessage;
      public event EventHandler Closed;
      public event EventHandler<TabChangeRequestedEventArgs> RequestTabChange;
      public event EventHandler<IDataModel> RequestCloseOtherViewports;
      public event EventHandler<Action> RequestDelayedWork;
      public event EventHandler RequestMenuClose;
      public event EventHandler<Direction> RequestDiff;
      public event EventHandler<CanDiffEventArgs> RequestCanDiff;
      event EventHandler<CanPatchEventArgs> ITabContent.RequestCanCreatePatch { add { } remove { } }
      event EventHandler<CanPatchEventArgs> ITabContent.RequestCreatePatch { add { } remove { } }
      public event PropertyChangedEventHandler PropertyChanged;
      public event NotifyCollectionChangedEventHandler CollectionChanged;
      event EventHandler ITabContent.RequestRefreshGotoShortcuts { add { } remove { } }

      public bool CanIpsPatchRight => false;
      public bool CanUpsPatchRight => false;
      public void IpsPatchRight() { }
      public void UpsPatchRight() { }

      public void ConsiderReload(IFileSystem fileSystem) => throw new NotImplementedException();

      public IChildViewPort CreateChildView(int startAddress, int endAddress) {
         throw new NotImplementedException();
      }

      public IReadOnlyList<(int start, int end)> Find(string search, bool matchExactCase = false) => Array.Empty<(int, int)>();

      public bool CanFindFreeSpace => false;
      public void FindFreeSpace(IFileSystem fs) { }

      public void FindAllSources(int x, int y) { }

      public void FollowLink(int x, int y) { }

      public bool IsTable(Point point) => false;

      public bool TryImport(LoadedFile file, IFileSystem fileSystem) => false;

      #endregion

      public (int childIndex, int childLine) ConvertLine(int parentLine) {
         var scrollLine = parentLine + scrollValue;
         if (scrollLine < 0) return (0, scrollLine);
         int index = startOfNextSegment.BinarySearch(scrollLine);
         if (index >= 0) return (index, 0);
         index = ~index - 1;
         return (index, scrollLine - startOfNextSegment[index]);
      }
   }
}
