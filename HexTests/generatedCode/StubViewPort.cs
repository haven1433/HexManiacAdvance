// this file was created by AutoImplement
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Delegation;

namespace HavenSoft.Gen3Hex.ViewModel
{
    public class StubViewPort : IViewPort
    {
        public Func<HavenSoft.Gen3Hex.Model.Point, bool> IsSelected { get; set; }
        
        bool IViewPort.IsSelected(HavenSoft.Gen3Hex.Model.Point point)
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
        
        public Func<string, System.Collections.Generic.IReadOnlyList<int>> Find { get; set; }
        
        System.Collections.Generic.IReadOnlyList<int> IViewPort.Find(string search)
        {
            if (this.Find != null)
            {
                return this.Find(search);
            }
            else
            {
                return default(System.Collections.Generic.IReadOnlyList<int>);
            }
        }
        
        public Func<int, ChildViewPort> CreateChildView { get; set; }
        
        ChildViewPort IViewPort.CreateChildView(int offset)
        {
            if (this.CreateChildView != null)
            {
                return this.CreateChildView(offset);
            }
            else
            {
                return default(ChildViewPort);
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
            set {
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
        public PropertyImplementation<System.Windows.Input.ICommand> Scroll = new PropertyImplementation<System.Windows.Input.ICommand>();
        
        System.Windows.Input.ICommand IViewPort.Scroll
        {
            get
            {
                return this.Scroll.get();
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
        public PropertyImplementation<string> Name = new PropertyImplementation<string>();
        
        string ITabContent.Name
        {
            get
            {
                return this.Name.get();
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
        public PropertyImplementation<System.Windows.Input.ICommand> Clear = new PropertyImplementation<System.Windows.Input.ICommand>();
        
        System.Windows.Input.ICommand ITabContent.Clear
        {
            get
            {
                return this.Clear.get();
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
        public EventImplementation<NotifyCollectionChangedEventArgs> CollectionChanged = new EventImplementation<NotifyCollectionChangedEventArgs>();
        
        event NotifyCollectionChangedEventHandler INotifyCollectionChanged.CollectionChanged
        {
            add
            {
                CollectionChanged.add(new EventHandler<NotifyCollectionChangedEventArgs>(value));
            }
            remove
            {
                CollectionChanged.remove(new EventHandler<NotifyCollectionChangedEventArgs>(value));
            }
        }
        public EventImplementation<PropertyChangedEventArgs> PropertyChanged = new EventImplementation<PropertyChangedEventArgs>();
        
        event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged
        {
            add
            {
                PropertyChanged.add(new EventHandler<PropertyChangedEventArgs>(value));
            }
            remove
            {
                PropertyChanged.remove(new EventHandler<PropertyChangedEventArgs>(value));
            }
        }
    }
}
