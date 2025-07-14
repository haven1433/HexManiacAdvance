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

namespace HavenSoft.HexManiac.Core.ViewModels {
   public interface IViewPort : ITabContent, INotifyCollectionChanged {
      event EventHandler PreviewScrollChanged;
      event EventHandler<IDataModel> RequestCloseOtherViewports;

      double ToolPanelWidth { get; set; } // exists for binding, to track the left panel width when changing tabs

      string FileName { get; } // Name is dispayed in a tab. FileName lets us know when to call 'ConsiderReload'
      string FullFileName { get; } // FullFileName is displayed when hovering over the tab.

      bool AutoAdjustDataWidth { get; set; }
      bool StretchData { get; set; }
      bool AllowMultipleElementsPerLine { get; set; }
      bool Base10Length { get; set; }

      int MinimumScroll { get; }
      int MaximumScroll { get; }
      ObservableCollection<RowHeader> Headers { get; }
      ObservableCollection<ColumnHeaderRow> ColumnHeaders { get; }

      double Progress { get; }
      bool UpdateInProgress { get; }

      bool CanFindFreeSpace { get; }

      string SelectedAddress { get; set; }
      string SelectedBytes { get; }
      bool AnchorTextVisible { get; }

      IDataModel Model { get; }
      bool IsTable(Point point);

      void ConsiderReload(IFileSystem fileSystem);

      bool HasTools { get; }
      ChangeHistory<ModelDelta> ChangeHistory { get; }
      IToolTrayViewModel Tools { get; }
   }

   public interface IEditableViewPort : IViewPort, IRaiseMessageTab, IRaiseErrorTab {
      Task InitializationWorkload { get; }
      MapEditorViewModel MapEditor { get; }
      bool AllowSingleTableMode { get; set; }
      bool IsFocused { get; set; }
      Point SelectionStart { get; }
      Point SelectionEnd { get; }
      void RaiseRequestTabChange(ITabContent tab);
      InlineDispatch UpdateProgress(double value);
      void ClearProgress();
   }
}
