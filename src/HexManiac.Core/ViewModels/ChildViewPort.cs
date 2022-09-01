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
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels {
   /// <summary>
   ///  exactly replaces a ViewPort, except that it also has a known parent view
   /// </summary>
   public class ChildViewPort : ViewPort, IChildViewPort {
      public IViewPort Parent { get; }

      public ChildViewPort(IViewPort viewPort, IWorkDispatcher dispatcher, Singletons singletons) : base(viewPort.FileName, viewPort.Model, dispatcher, singletons, null, viewPort.ChangeHistory) {
         Parent = viewPort;
         Width = Parent.Width;
      }
   }

   public class CompositeChildViewPort : List<IChildViewPort>, IChildViewPort {
      public static bool TryCombine(IChildViewPort a, IChildViewPort b, out CompositeChildViewPort result) {
         result = null;
         if (b is CompositeChildViewPort) return false;
         result = a is CompositeChildViewPort composite ? composite : new CompositeChildViewPort { a };
         if (a.Parent != b.Parent) return false;
         if (a.Width != b.Width) return false;
         if (b.DataOffset < a.DataOffset) return false;
         if ((b.DataOffset - a.DataOffset) % b.Width != 0) return false;
         var bSelectionLine = (GetFirstVisibleSelectedAddress(b) - a.DataOffset) / b.Width;
         var aSelectionLine = (GetLastVisibleSelectedAddress(a) - a.DataOffset) / b.Width;
         var lineDif = bSelectionLine - aSelectionLine;
         if (lineDif > 3) return false;
         a.Height += lineDif;
         b.ScrollValue -= (b.DataOffset - a.DataOffset) / b.Width;
         b.Height = a.Height;
         Debug.Assert(a.DataOffset == b.DataOffset, "Data Offsets should be the same at this point!");
         result.Add(b);
         return true;
      }

      public static int GetFirstVisibleSelectedAddress(IViewPort viewPort) {
         for (int y = 0; y < viewPort.Height; y++) {
            for (int x = 0; x < viewPort.Width; x++) {
               if (!viewPort.IsSelected(new Point(x, y))) continue;
               return viewPort.DataOffset + y * viewPort.Width + x;
            }
         }

         return -1;
      }

      public static int GetLastVisibleSelectedAddress(IViewPort viewPort) {
         for (int y = viewPort.Height - 1; y >= 0; y--) {
            for (int x = viewPort.Width - 1; x >= 0; x--) {
               if (!viewPort.IsSelected(new Point(x, y))) continue;
               return viewPort.DataOffset + y * viewPort.Width + x;
            }
         }

         return -1;
      }

      public HexElement this[int x, int y] => this[0][x, y];

      public IViewPort Parent => this[0].Parent;

      public double ToolPanelWidth { get; set; } = 500;

      public string FileName => this[0].FileName;

      public string FullFileName => this[0].FullFileName;

      public int PreferredWidth { get => this[0].PreferredWidth; set => ForEach(child => child.PreferredWidth = value); }
      public int Width { get => this[0].Width; set => ForEach(child => child.Width = value); }
      public int Height { get => this[0].Height; set => ForEach(child => child.Height = value); }
      public bool AutoAdjustDataWidth { get => this[0].AutoAdjustDataWidth; set => ForEach(child => child.AutoAdjustDataWidth = value); }
      public bool StretchData { get => this[0].StretchData; set => ForEach(child => child.StretchData = value); }
      public bool AllowMultipleElementsPerLine { get => this[0].AllowMultipleElementsPerLine; set => ForEach(child => child.AllowMultipleElementsPerLine = value); }
      public bool UseCustomHeaders { get => this[0].UseCustomHeaders; set => ForEach(child => child.UseCustomHeaders = value); }

      public int MinimumScroll => this[0].MinimumScroll;

      public int ScrollValue { get => this[0].ScrollValue; set => ForEach(child => child.ScrollValue = value); }

      public int MaximumScroll => this[0].MaximumScroll;

      public ObservableCollection<string> Headers => this[0].Headers;

      public ObservableCollection<HeaderRow> ColumnHeaders => this[0].ColumnHeaders;

      public int DataOffset => this[0].DataOffset;

      public ICommand Scroll => this[0].Scroll;

      public double Progress => this[0].Progress;

      public bool UpdateInProgress => this[0].UpdateInProgress;

      public string SelectedAddress => this[0].SelectedAddress;

      public string SelectedBytes => this[0].SelectedBytes;

      public string AnchorText => this[0].AnchorText;

      public bool AnchorTextVisible => this[0].AnchorTextVisible;

      public IDataModel Model => this[0].Model;

      public bool HasTools => this[0].HasTools;

      public ChangeHistory<ModelDelta> ChangeHistory => this[0].ChangeHistory;

      public IToolTrayViewModel Tools => this[0].Tools;

      public string Name => this[0].Name;

      public bool IsMetadataOnlyChange => false;

      public byte[] FindBytes { get; set; }

      public ICommand Save => this[0].Save;

      public ICommand SaveAs => this[0].SaveAs;

      public ICommand ExportBackup => this[0].ExportBackup;

      public ICommand Undo => this[0].Undo;

      public ICommand Redo => this[0].Redo;

      public ICommand Copy => this[0].Copy;

      public ICommand DeepCopy => this[0].DeepCopy;

      public ICommand Diff => this[0].Diff;
      public ICommand DiffLeft => this[0].DiffLeft;
      public ICommand DiffRight => this[0].DiffRight;

      public ICommand SelectAll => this[0].SelectAll;

      public ICommand Goto => this[0].Goto;

      public ICommand ResetAlignment => this[0].ResetAlignment;

      public ICommand Back => this[0].Back;

      public ICommand Forward => this[0].Forward;

      public ICommand Close => this[0].Close;

      ICommand ITabContent.Clear => this[0].Clear;
      public bool CanDuplicate => false;
      public void Duplicate() { }

      public event EventHandler PreviewScrollChanged { add => ForEach(child => child.PreviewScrollChanged += value); remove => ForEach(child => child.PreviewScrollChanged -= value); }
      public event EventHandler<string> OnError { add => ForEach(child => child.OnError += value); remove => ForEach(child => child.OnError -= value); }
      public event EventHandler<string> OnMessage { add => ForEach(child => child.OnMessage += value); remove => ForEach(child => child.OnMessage -= value); }
      public event EventHandler ClearMessage { add => ForEach(child => child.ClearMessage += value); remove => ForEach(child => child.ClearMessage -= value); }
      public event EventHandler Closed { add => ForEach(child => child.Closed += value); remove => ForEach(child => child.Closed -= value); }
      public event EventHandler<ITabContent> RequestTabChange { add => ForEach(child => child.RequestTabChange += value); remove => ForEach(child => child.RequestTabChange -= value); }
      public event EventHandler<IDataModel> RequestCloseOtherViewports { add => ForEach(child => child.RequestCloseOtherViewports += value); remove => ForEach(child => child.RequestCloseOtherViewports -= value); }
      public event EventHandler<Action> RequestDelayedWork { add => ForEach(child => child.RequestDelayedWork += value); remove => ForEach(child => child.RequestDelayedWork -= value); }
      public event EventHandler RequestMenuClose { add => ForEach(child => child.RequestMenuClose += value); remove => ForEach(child => child.RequestMenuClose -= value); }
      public event EventHandler<Direction> RequestDiff { add => ForEach(child => child.RequestDiff += value); remove => ForEach(child => child.RequestDiff -= value); }
      public event EventHandler<CanDiffEventArgs> RequestCanDiff { add => ForEach(child => child.RequestCanDiff += value); remove => ForEach(child => child.RequestCanDiff -= value); }
      event EventHandler<CanPatchEventArgs> ITabContent.RequestCanCreatePatch { add { } remove { } }
      event EventHandler<CanPatchEventArgs> ITabContent.RequestCreatePatch { add { } remove { } }
      public event PropertyChangedEventHandler PropertyChanged { add => ForEach(child => child.PropertyChanged += value); remove => ForEach(child => child.PropertyChanged -= value); }
      public event NotifyCollectionChangedEventHandler CollectionChanged { add => ForEach(child => child.CollectionChanged += value); remove => ForEach(child => child.CollectionChanged -= value); }

      public void ConsiderReload(IFileSystem fileSystem) => throw new NotImplementedException();

      public IChildViewPort CreateChildView(int startAddress, int endAddress) {
         throw new NotImplementedException();
      }

      public void ExpandSelection(int x, int y) => ForEach(child => child.ExpandSelection(x, y));

      public IReadOnlyList<(int start, int end)> Find(string search, bool matchExactCase = false) => new (int, int)[0];

      public bool CanFindFreeSpace => false;
      public void FindFreeSpace(IFileSystem fs) { }

      public void FindAllSources(int x, int y) { }

      public void FollowLink(int x, int y) { }

      public IReadOnlyList<IContextItem> GetContextMenuItems(Point point, IFileSystem fileSystem) => new IContextItem[0];

      public bool IsSelected(Point point) => this.Any(child => child.IsSelected(point));

      public bool IsTable(Point point) => this[0].IsTable(point);

      public void Refresh() => ForEach(child => child.Refresh());

      public bool TryImport(LoadedFile file, IFileSystem fileSystem) => false;
   }
}
