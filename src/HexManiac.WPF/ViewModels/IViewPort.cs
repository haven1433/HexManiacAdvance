using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using HavenSoft.HexManiac.Core.ViewModels.Visitors;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading.Tasks;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels {
   public interface IViewPort : ITabContent, INotifyCollectionChanged {
      event EventHandler PreviewScrollChanged;
      event EventHandler<IDataModel> RequestCloseOtherViewports;

      double ToolPanelWidth { get; set; } // exists for binding, to track the left panel width when changing tabs

      string FileName { get; } // Name is dispayed in a tab. FileName lets us know when to call 'ConsiderReload'
      string FullFileName { get; } // FullFileName is displayed when hovering over the tab.

      int Width { get; set; }
      int Height { get; set; }

      bool AutoAdjustDataWidth { get; set; }
      bool StretchData { get; set; }
      bool AllowMultipleElementsPerLine { get; set; }
      bool Base10Length { get; set; }

      bool UseCustomHeaders { get; set; }
      int MinimumScroll { get; }
      int ScrollValue { get; set; }
      int MaximumScroll { get; }
      ObservableCollection<RowHeader> Headers { get; }
      ObservableCollection<ColumnHeaderRow> ColumnHeaders { get; }
      int DataOffset { get; }
      ICommand Scroll { get; } // parameter: Direction to scroll

      double Progress { get; }
      bool UpdateInProgress { get; }

      bool CanFindFreeSpace { get; }

      string SelectedAddress { get; set; }
      string SelectedBytes { get; }
      string AnchorText { get; }
      bool AnchorTextVisible { get; }

      byte[] FindBytes { get; set; }

      HexElement this[int x, int y] { get; }
      IDataModel Model { get; }
      IDataModel ModelFor(Point point);
      bool IsSelected(Point point);
      bool IsTable(Point point);

      IReadOnlyList<(int start, int end)> Find(string search, bool matchExactCase = false);
      void FindFreeSpace(IFileSystem fileSystem);
      IChildViewPort CreateChildView(int startAddress, int endAddress);
      void FollowLink(int x, int y);
      void ExpandSelection(int x, int y);
      void ConsiderReload(IFileSystem fileSystem);
      void FindAllSources(int x, int y);

      bool HasTools { get; }
      ChangeHistory<ModelDelta> ChangeHistory { get; }
      IToolTrayViewModel Tools { get; }

      IReadOnlyList<IContextItem> GetContextMenuItems(Point point, IFileSystem fileSystem);
   }

   public interface IEditableViewPort : IViewPort, IRaiseMessageTab, IRaiseErrorTab {
      Task InitializationWorkload { get; }
      MapEditorViewModel MapEditor { get; }
      bool AllowSingleTableMode { get; set; }
      bool IsFocused { get; set; }
      Point SelectionStart { get; }
      Point SelectionEnd { get; }
      new string AnchorText { get; set; }
      void Edit(string input);
      Task Edit(string input, double loadingPercentBeforeEdit, double loadingPercentAfterEdit);
      void Edit(ConsoleKey key);
      void RaiseRequestTabChange(ITabContent tab);
      InlineDispatch UpdateProgress(double value);
      void ClearProgress();
      IEditableViewPort CreateDuplicate();
      void OpenImageEditorTab(int address, int spritePage, int palettePage, int preferredTileWidth = -1);
      void GotoScript(int address);
   }
}
