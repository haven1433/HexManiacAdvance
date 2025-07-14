using System;
using System.Collections.Generic;
using HavenSoft.AutoImplement.Delegation;
using HavenSoft.HexManiac.Core.Models;

// this file was created by AutoImplement
namespace HavenSoft.HexManiac.Core.ViewModels
{
    public class StubViewPort : IViewPort
    {
        public Func<HavenSoft.HexManiac.Core.Models.Point, bool> IsSelected { get; set; }
        
        public Func<HavenSoft.HexManiac.Core.Models.Point, bool> IsTable { get; set; }
        
        bool IViewPort.IsTable(HavenSoft.HexManiac.Core.Models.Point point)
        {
            if (this.IsTable != null)
            {
                return this.IsTable(point);
            }
            else
            {
                return default(bool);
            }
        }
        
        public Func<string, bool, System.Collections.Generic.IReadOnlyList<System.ValueTuple<int, int>>> Find { get; set; }
        
        public Action<HavenSoft.HexManiac.Core.Models.IFileSystem> FindFreeSpace { get; set; }
        
        public Func<int, int, IChildViewPort> CreateChildView { get; set; }
        
        public Action<int, int> FollowLink { get; set; }
        
        public Action<int, int> ExpandSelection { get; set; }
        
        public Action<HavenSoft.HexManiac.Core.Models.IFileSystem> ConsiderReload { get; set; }
        
        void IViewPort.ConsiderReload(HavenSoft.HexManiac.Core.Models.IFileSystem fileSystem)
        {
            if (this.ConsiderReload != null)
            {
                this.ConsiderReload(fileSystem);
            }
        }
        
        public Action<int, int> FindAllSources { get; set; }
        
        public Func<HavenSoft.HexManiac.Core.Models.Point, System.Collections.Generic.IReadOnlyList<Visitors.IContextItem>> GetContextMenuItems { get; set; }
        
        public PropertyImplementation<double> ToolPanelWidth = new();
        double IViewPort.ToolPanelWidth { get => ToolPanelWidth.get(); set => ToolPanelWidth.set(value); }

        public PropertyImplementation<string> FileName = new PropertyImplementation<string>();

        string IViewPort.FileName
        {
            get
            {
                return this.FileName.get();
            }
        }
        public PropertyImplementation<string> FullFileName = new PropertyImplementation<string>();
        
        string IViewPort.FullFileName
        {
            get
            {
                return this.FullFileName.get();
            }
        }
        public PropertyImplementation<int> Width = new PropertyImplementation<int>();

        public bool SpartanMode { get; set; }

        public PropertyImplementation<int> Height = new PropertyImplementation<int>();
        
        public PropertyImplementation<bool> AutoAdjustDataWidth = new PropertyImplementation<bool>();
        
        bool IViewPort.AutoAdjustDataWidth
        {
            get
            {
                return this.AutoAdjustDataWidth.get();
            }
            set
            {
                this.AutoAdjustDataWidth.set(value);
            }
        }
        public PropertyImplementation<bool> StretchData = new PropertyImplementation<bool>();
        
        bool IViewPort.StretchData
        {
            get
            {
                return this.StretchData.get();
            }
            set
            {
                this.StretchData.set(value);
            }
        }
        public PropertyImplementation<bool> AllowMultipleElementsPerLine = new PropertyImplementation<bool>();
        
        bool IViewPort.AllowMultipleElementsPerLine
        {
            get
            {
                return this.AllowMultipleElementsPerLine.get();
            }
            set
            {
                this.AllowMultipleElementsPerLine.set(value);
            }
        }
        public PropertyImplementation<bool> UseCustomHeaders = new PropertyImplementation<bool>();
        
        public PropertyImplementation<int> MinimumScroll = new PropertyImplementation<int>();
        
        int IViewPort.MinimumScroll
        {
            get
            {
                return this.MinimumScroll.get();
            }
        }
        public PropertyImplementation<int> ScrollValue = new PropertyImplementation<int>();
        
        public PropertyImplementation<int> MaximumScroll = new PropertyImplementation<int>();
        
        int IViewPort.MaximumScroll
        {
            get
            {
                return this.MaximumScroll.get();
            }
        }
        public PropertyImplementation<System.Collections.ObjectModel.ObservableCollection<RowHeader>> Headers = new PropertyImplementation<System.Collections.ObjectModel.ObservableCollection<RowHeader>>();
        
        System.Collections.ObjectModel.ObservableCollection<RowHeader> IViewPort.Headers
        {
            get
            {
                return this.Headers.get();
            }
        }
        public PropertyImplementation<System.Collections.ObjectModel.ObservableCollection<HavenSoft.HexManiac.Core.Models.Runs.ColumnHeaderRow>> ColumnHeaders = new PropertyImplementation<System.Collections.ObjectModel.ObservableCollection<HavenSoft.HexManiac.Core.Models.Runs.ColumnHeaderRow>>();
        
        System.Collections.ObjectModel.ObservableCollection<HavenSoft.HexManiac.Core.Models.Runs.ColumnHeaderRow> IViewPort.ColumnHeaders
        {
            get
            {
                return this.ColumnHeaders.get();
            }
        }
        public PropertyImplementation<int> DataOffset = new PropertyImplementation<int>();
        
        public PropertyImplementation<System.Windows.Input.ICommand> Scroll = new PropertyImplementation<System.Windows.Input.ICommand>();
        
        public PropertyImplementation<double> Progress = new PropertyImplementation<double>();
        
        double IViewPort.Progress
        {
            get
            {
                return this.Progress.get();
            }
        }
        public PropertyImplementation<bool> UpdateInProgress = new PropertyImplementation<bool>();
        
        bool IViewPort.UpdateInProgress
        {
            get
            {
                return this.UpdateInProgress.get();
            }
        }
        public PropertyImplementation<bool> CanFindFreeSpace = new PropertyImplementation<bool>();
        
        bool IViewPort.CanFindFreeSpace
        {
            get
            {
                return this.CanFindFreeSpace.get();
            }
        }
        public PropertyImplementation<string> SelectedAddress = new PropertyImplementation<string>();

        bool IViewPort.Base10Length {
           get => Base10Length.get();
           set => Base10Length.set(value);
        }

        public PropertyImplementation<bool> Base10Length = new PropertyImplementation<bool>();

        string IViewPort.SelectedAddress
        {
            get
            {
                return this.SelectedAddress.get();
            }
            set => SelectedAddress.set(value);
        }
        public PropertyImplementation<string> SelectedBytes = new PropertyImplementation<string>();
        
        string IViewPort.SelectedBytes
        {
            get
            {
                return this.SelectedBytes.get();
            }
        }
        public PropertyImplementation<string> AnchorText = new PropertyImplementation<string>();
        
        public PropertyImplementation<bool> AnchorTextVisible = new PropertyImplementation<bool>();
        
        bool IViewPort.AnchorTextVisible
        {
            get
            {
                return this.AnchorTextVisible.get();
            }
        }
        public PropertyImplementation<System.Byte[]> FindBytes = new PropertyImplementation<System.Byte[]>();
        
        public Func<int, int, HexElement> get_Item = (x, y) => default(HexElement);
        
        public PropertyImplementation<HavenSoft.HexManiac.Core.Models.IDataModel> Model = new PropertyImplementation<HavenSoft.HexManiac.Core.Models.IDataModel>();
        HavenSoft.HexManiac.Core.Models.IDataModel IViewPort.Model
        {
            get
            {
                return this.Model.get();
            }
        }

        public Func<Point,IDataModel> ModelFor;

        public PropertyImplementation<bool> HasTools = new PropertyImplementation<bool>();
        
        bool IViewPort.HasTools
        {
            get
            {
                return this.HasTools.get();
            }
        }
        public PropertyImplementation<ChangeHistory<HavenSoft.HexManiac.Core.Models.ModelDelta>> ChangeHistory = new PropertyImplementation<ChangeHistory<HavenSoft.HexManiac.Core.Models.ModelDelta>>();
        
        ChangeHistory<HavenSoft.HexManiac.Core.Models.ModelDelta> IViewPort.ChangeHistory
        {
            get
            {
                return this.ChangeHistory.get();
            }
        }
        public PropertyImplementation<Tools.IToolTrayViewModel> Tools = new PropertyImplementation<Tools.IToolTrayViewModel>();
        
        Tools.IToolTrayViewModel IViewPort.Tools
        {
            get
            {
                return this.Tools.get();
            }
        }
        public EventImplementation<System.EventArgs> PreviewScrollChanged = new EventImplementation<System.EventArgs>();
        
        event System.EventHandler IViewPort.PreviewScrollChanged
        {
            add
            {
                PreviewScrollChanged.add(new EventHandler<System.EventArgs>(value));
            }
            remove
            {
                PreviewScrollChanged.remove(new EventHandler<System.EventArgs>(value));
            }
        }
        public EventImplementation<HavenSoft.HexManiac.Core.Models.IDataModel> RequestCloseOtherViewports = new EventImplementation<HavenSoft.HexManiac.Core.Models.IDataModel>();
        
        event System.EventHandler<HavenSoft.HexManiac.Core.Models.IDataModel> IViewPort.RequestCloseOtherViewports
        {
            add
            {
                RequestCloseOtherViewports.add(new EventHandler<HavenSoft.HexManiac.Core.Models.IDataModel>(value));
            }
            remove
            {
                RequestCloseOtherViewports.remove(new EventHandler<HavenSoft.HexManiac.Core.Models.IDataModel>(value));
            }
        }
        public Action Duplicate { get; set; }
        
        public Action Refresh { get; set; }
        
        public Func<HavenSoft.HexManiac.Core.Models.LoadedFile, HavenSoft.HexManiac.Core.Models.IFileSystem, bool> TryImport { get; set; }
        
        public PropertyImplementation<string> Name = new PropertyImplementation<string>();
        
        string ITabContent.Name
        {
            get
            {
                return this.Name.get();
            }
        }
        public PropertyImplementation<bool> IsMetadataOnlyChange = new PropertyImplementation<bool>();
        
        bool ITabContent.IsMetadataOnlyChange
        {
            get
            {
                return this.IsMetadataOnlyChange.get();
            }
        }
        public PropertyImplementation<System.Windows.Input.ICommand> Save = new PropertyImplementation<System.Windows.Input.ICommand>();
        
        public PropertyImplementation<System.Windows.Input.ICommand> SaveAs = new PropertyImplementation<System.Windows.Input.ICommand>();
        
        public PropertyImplementation<System.Windows.Input.ICommand> ExportBackup = new PropertyImplementation<System.Windows.Input.ICommand>();
        
        public PropertyImplementation<System.Windows.Input.ICommand> Undo = new PropertyImplementation<System.Windows.Input.ICommand>();
        
        public PropertyImplementation<System.Windows.Input.ICommand> Redo = new PropertyImplementation<System.Windows.Input.ICommand>();
        
        public PropertyImplementation<System.Windows.Input.ICommand> Copy = new PropertyImplementation<System.Windows.Input.ICommand>();
        
        public PropertyImplementation<System.Windows.Input.ICommand> DeepCopy = new PropertyImplementation<System.Windows.Input.ICommand>();
        
        public PropertyImplementation<System.Windows.Input.ICommand> Clear = new PropertyImplementation<System.Windows.Input.ICommand>();
        
        public PropertyImplementation<System.Windows.Input.ICommand> SelectAll = new PropertyImplementation<System.Windows.Input.ICommand>();
        
        public PropertyImplementation<System.Windows.Input.ICommand> Goto = new PropertyImplementation<System.Windows.Input.ICommand>();
        
        public PropertyImplementation<System.Windows.Input.ICommand> ResetAlignment = new PropertyImplementation<System.Windows.Input.ICommand>();
        
        public PropertyImplementation<System.Windows.Input.ICommand> Back = new PropertyImplementation<System.Windows.Input.ICommand>();
        
        public PropertyImplementation<System.Windows.Input.ICommand> Forward = new PropertyImplementation<System.Windows.Input.ICommand>();
        
        public PropertyImplementation<System.Windows.Input.ICommand> Close = new PropertyImplementation<System.Windows.Input.ICommand>();
        
        public PropertyImplementation<System.Windows.Input.ICommand> Diff = new PropertyImplementation<System.Windows.Input.ICommand>();
        
        public PropertyImplementation<System.Windows.Input.ICommand> DiffLeft = new PropertyImplementation<System.Windows.Input.ICommand>();
        
        public PropertyImplementation<System.Windows.Input.ICommand> DiffRight = new PropertyImplementation<System.Windows.Input.ICommand>();
        
        public PropertyImplementation<bool> CanDuplicate = new PropertyImplementation<bool>();
        
        bool ITabContent.CanDuplicate
        {
            get
            {
                return this.CanDuplicate.get();
            }
        }
        public EventImplementation<string> OnError = new EventImplementation<string>();
        
        event System.EventHandler<string> ITabContent.OnError
        {
            add
            {
                OnError.add(new EventHandler<string>(value));
            }
            remove
            {
                OnError.remove(new EventHandler<string>(value));
            }
        }
        public EventImplementation<string> OnMessage = new EventImplementation<string>();
        
        event System.EventHandler<string> ITabContent.OnMessage
        {
            add
            {
                OnMessage.add(new EventHandler<string>(value));
            }
            remove
            {
                OnMessage.remove(new EventHandler<string>(value));
            }
        }
        public EventImplementation<System.EventArgs> ClearMessage = new EventImplementation<System.EventArgs>();
        
        event System.EventHandler ITabContent.ClearMessage
        {
            add
            {
                ClearMessage.add(new EventHandler<System.EventArgs>(value));
            }
            remove
            {
                ClearMessage.remove(new EventHandler<System.EventArgs>(value));
            }
        }
        public EventImplementation<System.EventArgs> Closed = new EventImplementation<System.EventArgs>();
        
        event System.EventHandler ITabContent.Closed
        {
            add
            {
                Closed.add(new EventHandler<System.EventArgs>(value));
            }
            remove
            {
                Closed.remove(new EventHandler<System.EventArgs>(value));
            }
        }
        public EventImplementation<TabChangeRequestedEventArgs> RequestTabChange = new EventImplementation<TabChangeRequestedEventArgs>();
        
        event System.EventHandler<TabChangeRequestedEventArgs> ITabContent.RequestTabChange
        {
            add
            {
                RequestTabChange.add(new EventHandler<TabChangeRequestedEventArgs>(value));
            }
            remove
            {
                RequestTabChange.remove(new EventHandler<TabChangeRequestedEventArgs>(value));
            }
        }
        public EventImplementation<System.Action> RequestDelayedWork = new EventImplementation<System.Action>();
        
        event System.EventHandler<System.Action> ITabContent.RequestDelayedWork
        {
            add
            {
                RequestDelayedWork.add(new EventHandler<System.Action>(value));
            }
            remove
            {
                RequestDelayedWork.remove(new EventHandler<System.Action>(value));
            }
        }
        public EventImplementation<System.EventArgs> RequestMenuClose = new EventImplementation<System.EventArgs>();
        
        event System.EventHandler ITabContent.RequestMenuClose
        {
            add
            {
                RequestMenuClose.add(new EventHandler<System.EventArgs>(value));
            }
            remove
            {
                RequestMenuClose.remove(new EventHandler<System.EventArgs>(value));
            }
        }
        public EventImplementation<HavenSoft.HexManiac.Core.Models.Direction> RequestDiff = new EventImplementation<HavenSoft.HexManiac.Core.Models.Direction>();
        
        event System.EventHandler<HavenSoft.HexManiac.Core.Models.Direction> ITabContent.RequestDiff
        {
            add
            {
                RequestDiff.add(new EventHandler<HavenSoft.HexManiac.Core.Models.Direction>(value));
            }
            remove
            {
                RequestDiff.remove(new EventHandler<HavenSoft.HexManiac.Core.Models.Direction>(value));
            }
        }
        public EventImplementation<CanDiffEventArgs> RequestCanDiff = new EventImplementation<CanDiffEventArgs>();
        
        event System.EventHandler<CanDiffEventArgs> ITabContent.RequestCanDiff
        {
            add
            {
                RequestCanDiff.add(new EventHandler<CanDiffEventArgs>(value));
            }
            remove
            {
                RequestCanDiff.remove(new EventHandler<CanDiffEventArgs>(value));
            }
        }
        public EventImplementation<CanPatchEventArgs> RequestCanCreatePatch = new EventImplementation<CanPatchEventArgs>();
        
        event System.EventHandler<CanPatchEventArgs> ITabContent.RequestCanCreatePatch
        {
            add
            {
                RequestCanCreatePatch.add(new EventHandler<CanPatchEventArgs>(value));
            }
            remove
            {
                RequestCanCreatePatch.remove(new EventHandler<CanPatchEventArgs>(value));
            }
        }
        public EventImplementation<CanPatchEventArgs> RequestCreatePatch = new EventImplementation<CanPatchEventArgs>();
        
        event System.EventHandler<CanPatchEventArgs> ITabContent.RequestCreatePatch
        {
            add
            {
                RequestCreatePatch.add(new EventHandler<CanPatchEventArgs>(value));
            }
            remove
            {
                RequestCreatePatch.remove(new EventHandler<CanPatchEventArgs>(value));
            }
        }
        public EventImplementation<System.ComponentModel.PropertyChangedEventArgs> PropertyChanged = new EventImplementation<System.ComponentModel.PropertyChangedEventArgs>();
        
        event System.ComponentModel.PropertyChangedEventHandler System.ComponentModel.INotifyPropertyChanged.PropertyChanged
        {
            add
            {
                PropertyChanged.add(new EventHandler<System.ComponentModel.PropertyChangedEventArgs>(value));
            }
            remove
            {
                PropertyChanged.remove(new EventHandler<System.ComponentModel.PropertyChangedEventArgs>(value));
            }
        }
        public EventImplementation<System.Collections.Specialized.NotifyCollectionChangedEventArgs> CollectionChanged = new EventImplementation<System.Collections.Specialized.NotifyCollectionChangedEventArgs>();

      public EventImplementation<EventArgs> RequestRefreshGotoShortcuts = new();
      event EventHandler ITabContent.RequestRefreshGotoShortcuts {
         add => RequestRefreshGotoShortcuts.add(new(value));
         remove => RequestRefreshGotoShortcuts.remove(new(value));
      }

      public PropertyImplementation<bool> CanIpsPatchRight { get; } = new();
      bool ITabContent.CanIpsPatchRight => CanIpsPatchRight.value;
      public PropertyImplementation<bool> CanUpsPatchRight { get; } = new();
      bool ITabContent.CanUpsPatchRight => CanUpsPatchRight.value;
      public Action IpsPatchRight;
      void ITabContent.IpsPatchRight() => IpsPatchRight?.Invoke();
      public Action UpsPatchRight;
      void ITabContent.UpsPatchRight() => UpsPatchRight?.Invoke();

      event System.Collections.Specialized.NotifyCollectionChangedEventHandler System.Collections.Specialized.INotifyCollectionChanged.CollectionChanged
        {
            add
            {
                CollectionChanged.add(new EventHandler<System.Collections.Specialized.NotifyCollectionChangedEventArgs>(value));
            }
            remove
            {
                CollectionChanged.remove(new EventHandler<System.Collections.Specialized.NotifyCollectionChangedEventArgs>(value));
            }
        }
    }
}
