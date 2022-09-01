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
        
        bool IViewPort.IsSelected(HavenSoft.HexManiac.Core.Models.Point point)
        {
            if (this.IsSelected != null)
            {
                return this.IsSelected(point);
            }
            else
            {
                return default(bool);
            }
        }
        
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
        
        System.Collections.Generic.IReadOnlyList<System.ValueTuple<int, int>> IViewPort.Find(string search, bool matchExactCase)
        {
            if (this.Find != null)
            {
                return this.Find(search, matchExactCase);
            }
            else
            {
                return default(System.Collections.Generic.IReadOnlyList<System.ValueTuple<int, int>>);
            }
        }
        
        public Action<HavenSoft.HexManiac.Core.Models.IFileSystem> FindFreeSpace { get; set; }
        
        void IViewPort.FindFreeSpace(HavenSoft.HexManiac.Core.Models.IFileSystem fileSystem)
        {
            if (this.FindFreeSpace != null)
            {
                this.FindFreeSpace(fileSystem);
            }
        }
        
        public Func<int, int, IChildViewPort> CreateChildView { get; set; }
        
        IChildViewPort IViewPort.CreateChildView(int startAddress, int endAddress)
        {
            if (this.CreateChildView != null)
            {
                return this.CreateChildView(startAddress, endAddress);
            }
            else
            {
                return default(IChildViewPort);
            }
        }
        
        public Action<int, int> FollowLink { get; set; }
        
        void IViewPort.FollowLink(int x, int y)
        {
            if (this.FollowLink != null)
            {
                this.FollowLink(x, y);
            }
        }
        
        public Action<int, int> ExpandSelection { get; set; }
        
        void IViewPort.ExpandSelection(int x, int y)
        {
            if (this.ExpandSelection != null)
            {
                this.ExpandSelection(x, y);
            }
        }
        
        public Action<HavenSoft.HexManiac.Core.Models.IFileSystem> ConsiderReload { get; set; }
        
        void IViewPort.ConsiderReload(HavenSoft.HexManiac.Core.Models.IFileSystem fileSystem)
        {
            if (this.ConsiderReload != null)
            {
                this.ConsiderReload(fileSystem);
            }
        }
        
        public Action<int, int> FindAllSources { get; set; }
        
        void IViewPort.FindAllSources(int x, int y)
        {
            if (this.FindAllSources != null)
            {
                this.FindAllSources(x, y);
            }
        }
        
        public Func<HavenSoft.HexManiac.Core.Models.Point, System.Collections.Generic.IReadOnlyList<Visitors.IContextItem>> GetContextMenuItems { get; set; }
        
        System.Collections.Generic.IReadOnlyList<Visitors.IContextItem> IViewPort.GetContextMenuItems(HavenSoft.HexManiac.Core.Models.Point point, IFileSystem fileSystem)
        {
            if (this.GetContextMenuItems != null)
            {
                return this.GetContextMenuItems(point);
            }
            else
            {
                return default(System.Collections.Generic.IReadOnlyList<Visitors.IContextItem>);
            }
        }

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
        
        int IViewPort.Width
        {
            get
            {
                return this.Width.get();
            }
            set
            {
                this.Width.set(value);
            }
        }
        public PropertyImplementation<int> Height = new PropertyImplementation<int>();
        
        int IViewPort.Height
        {
            get
            {
                return this.Height.get();
            }
            set
            {
                this.Height.set(value);
            }
        }
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
        
        bool IViewPort.UseCustomHeaders
        {
            get
            {
                return this.UseCustomHeaders.get();
            }
            set
            {
                this.UseCustomHeaders.set(value);
            }
        }
        public PropertyImplementation<int> MinimumScroll = new PropertyImplementation<int>();
        
        int IViewPort.MinimumScroll
        {
            get
            {
                return this.MinimumScroll.get();
            }
        }
        public PropertyImplementation<int> ScrollValue = new PropertyImplementation<int>();
        
        int IViewPort.ScrollValue
        {
            get
            {
                return this.ScrollValue.get();
            }
            set
            {
                this.ScrollValue.set(value);
            }
        }
        public PropertyImplementation<int> MaximumScroll = new PropertyImplementation<int>();
        
        int IViewPort.MaximumScroll
        {
            get
            {
                return this.MaximumScroll.get();
            }
        }
        public PropertyImplementation<System.Collections.ObjectModel.ObservableCollection<string>> Headers = new PropertyImplementation<System.Collections.ObjectModel.ObservableCollection<string>>();
        
        System.Collections.ObjectModel.ObservableCollection<string> IViewPort.Headers
        {
            get
            {
                return this.Headers.get();
            }
        }
        public PropertyImplementation<System.Collections.ObjectModel.ObservableCollection<HavenSoft.HexManiac.Core.Models.Runs.HeaderRow>> ColumnHeaders = new PropertyImplementation<System.Collections.ObjectModel.ObservableCollection<HavenSoft.HexManiac.Core.Models.Runs.HeaderRow>>();
        
        System.Collections.ObjectModel.ObservableCollection<HavenSoft.HexManiac.Core.Models.Runs.HeaderRow> IViewPort.ColumnHeaders
        {
            get
            {
                return this.ColumnHeaders.get();
            }
        }
        public PropertyImplementation<int> DataOffset = new PropertyImplementation<int>();
        
        int IViewPort.DataOffset
        {
            get
            {
                return this.DataOffset.get();
            }
        }
        public PropertyImplementation<System.Windows.Input.ICommand> Scroll = new PropertyImplementation<System.Windows.Input.ICommand>();
        
        System.Windows.Input.ICommand IViewPort.Scroll
        {
            get
            {
                return this.Scroll.get();
            }
        }
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
        
        string IViewPort.SelectedAddress
        {
            get
            {
                return this.SelectedAddress.get();
            }
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
        
        string IViewPort.AnchorText
        {
            get
            {
                return this.AnchorText.get();
            }
        }
        public PropertyImplementation<bool> AnchorTextVisible = new PropertyImplementation<bool>();
        
        bool IViewPort.AnchorTextVisible
        {
            get
            {
                return this.AnchorTextVisible.get();
            }
        }
        public PropertyImplementation<System.Byte[]> FindBytes = new PropertyImplementation<System.Byte[]>();
        
        System.Byte[] IViewPort.FindBytes
        {
            get
            {
                return this.FindBytes.get();
            }
            set
            {
                this.FindBytes.set(value);
            }
        }
        public Func<int, int, HexElement> get_Item = (x, y) => default(HexElement);
        
        HexElement IViewPort.this[int x, int y]
        {
            get
            {
                return get_Item(x, y);
            }
        }
        public PropertyImplementation<HavenSoft.HexManiac.Core.Models.IDataModel> Model = new PropertyImplementation<HavenSoft.HexManiac.Core.Models.IDataModel>();
        
        HavenSoft.HexManiac.Core.Models.IDataModel IViewPort.Model
        {
            get
            {
                return this.Model.get();
            }
        }
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
        
        void ITabContent.Duplicate()
        {
            if (this.Duplicate != null)
            {
                this.Duplicate();
            }
        }
        
        public Action Refresh { get; set; }
        
        void ITabContent.Refresh()
        {
            if (this.Refresh != null)
            {
                this.Refresh();
            }
        }

        public Func<HavenSoft.HexManiac.Core.Models.LoadedFile, HavenSoft.HexManiac.Core.Models.IFileSystem, bool> TryImport { get; set; }
        
        bool ITabContent.TryImport(HavenSoft.HexManiac.Core.Models.LoadedFile file, HavenSoft.HexManiac.Core.Models.IFileSystem fileSystem)
        {
            if (this.TryImport != null)
            {
                return this.TryImport(file, fileSystem);
            }
            else
            {
                return default(bool);
            }
        }
        
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
        
        System.Windows.Input.ICommand ITabContent.Save
        {
            get
            {
                return this.Save.get();
            }
        }
        public PropertyImplementation<System.Windows.Input.ICommand> SaveAs = new PropertyImplementation<System.Windows.Input.ICommand>();
        
        System.Windows.Input.ICommand ITabContent.SaveAs
        {
            get
            {
                return this.SaveAs.get();
            }
        }
        public PropertyImplementation<System.Windows.Input.ICommand> ExportBackup = new PropertyImplementation<System.Windows.Input.ICommand>();
        
        System.Windows.Input.ICommand ITabContent.ExportBackup
        {
            get
            {
                return this.ExportBackup.get();
            }
        }
        public PropertyImplementation<System.Windows.Input.ICommand> Undo = new PropertyImplementation<System.Windows.Input.ICommand>();
        
        System.Windows.Input.ICommand ITabContent.Undo
        {
            get
            {
                return this.Undo.get();
            }
        }
        public PropertyImplementation<System.Windows.Input.ICommand> Redo = new PropertyImplementation<System.Windows.Input.ICommand>();
        
        System.Windows.Input.ICommand ITabContent.Redo
        {
            get
            {
                return this.Redo.get();
            }
        }
        public PropertyImplementation<System.Windows.Input.ICommand> Copy = new PropertyImplementation<System.Windows.Input.ICommand>();
        
        System.Windows.Input.ICommand ITabContent.Copy
        {
            get
            {
                return this.Copy.get();
            }
        }
        public PropertyImplementation<System.Windows.Input.ICommand> DeepCopy = new PropertyImplementation<System.Windows.Input.ICommand>();
        
        System.Windows.Input.ICommand ITabContent.DeepCopy
        {
            get
            {
                return this.DeepCopy.get();
            }
        }
        public PropertyImplementation<System.Windows.Input.ICommand> Clear = new PropertyImplementation<System.Windows.Input.ICommand>();
        
        System.Windows.Input.ICommand ITabContent.Clear
        {
            get
            {
                return this.Clear.get();
            }
        }
        public PropertyImplementation<System.Windows.Input.ICommand> SelectAll = new PropertyImplementation<System.Windows.Input.ICommand>();
        
        System.Windows.Input.ICommand ITabContent.SelectAll
        {
            get
            {
                return this.SelectAll.get();
            }
        }
        public PropertyImplementation<System.Windows.Input.ICommand> Goto = new PropertyImplementation<System.Windows.Input.ICommand>();
        
        System.Windows.Input.ICommand ITabContent.Goto
        {
            get
            {
                return this.Goto.get();
            }
        }
        public PropertyImplementation<System.Windows.Input.ICommand> ResetAlignment = new PropertyImplementation<System.Windows.Input.ICommand>();
        
        System.Windows.Input.ICommand ITabContent.ResetAlignment
        {
            get
            {
                return this.ResetAlignment.get();
            }
        }
        public PropertyImplementation<System.Windows.Input.ICommand> Back = new PropertyImplementation<System.Windows.Input.ICommand>();
        
        System.Windows.Input.ICommand ITabContent.Back
        {
            get
            {
                return this.Back.get();
            }
        }
        public PropertyImplementation<System.Windows.Input.ICommand> Forward = new PropertyImplementation<System.Windows.Input.ICommand>();
        
        System.Windows.Input.ICommand ITabContent.Forward
        {
            get
            {
                return this.Forward.get();
            }
        }
        public PropertyImplementation<System.Windows.Input.ICommand> Close = new PropertyImplementation<System.Windows.Input.ICommand>();
        
        System.Windows.Input.ICommand ITabContent.Close
        {
            get
            {
                return this.Close.get();
            }
        }
        public PropertyImplementation<System.Windows.Input.ICommand> Diff = new PropertyImplementation<System.Windows.Input.ICommand>();
        
        System.Windows.Input.ICommand ITabContent.Diff
        {
            get
            {
                return this.Diff.get();
            }
        }
        public PropertyImplementation<System.Windows.Input.ICommand> DiffLeft = new PropertyImplementation<System.Windows.Input.ICommand>();
        
        System.Windows.Input.ICommand ITabContent.DiffLeft
        {
            get
            {
                return this.DiffLeft.get();
            }
        }
        public PropertyImplementation<System.Windows.Input.ICommand> DiffRight = new PropertyImplementation<System.Windows.Input.ICommand>();
        
        System.Windows.Input.ICommand ITabContent.DiffRight
        {
            get
            {
                return this.DiffRight.get();
            }
        }
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
        public EventImplementation<ITabContent> RequestTabChange = new EventImplementation<ITabContent>();
        
        event System.EventHandler<ITabContent> ITabContent.RequestTabChange
        {
            add
            {
                RequestTabChange.add(new EventHandler<ITabContent>(value));
            }
            remove
            {
                RequestTabChange.remove(new EventHandler<ITabContent>(value));
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
