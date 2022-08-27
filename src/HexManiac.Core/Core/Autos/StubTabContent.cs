using System;
using System.Collections.Generic;
using HavenSoft.AutoImplement.Delegation;

// this file was created by AutoImplement
namespace HavenSoft.HexManiac.Core.ViewModels
{
    public class StubTabContent : ITabContent
    {
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
    }
}
