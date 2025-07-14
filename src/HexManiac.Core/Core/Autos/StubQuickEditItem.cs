using System;
using System.Collections.Generic;
using HavenSoft.AutoImplement.Delegation;

// this file was created by AutoImplement
namespace HavenSoft.HexManiac.Core.ViewModels.QuickEditItems
{
    public class StubQuickEditItem : IQuickEditItem
    {
        public Func<HavenSoft.HexManiac.Core.ViewModels.IViewPort, bool> CanRun { get; set; }
        
        public Func<HavenSoft.HexManiac.Core.ViewModels.IViewPort, System.Threading.Tasks.Task<HavenSoft.HexManiac.Core.Models.ErrorInfo>> Run { get; set; }
        
        public Action TabChanged { get; set; }
        
        void IQuickEditItem.TabChanged()
        {
            if (this.TabChanged != null)
            {
                this.TabChanged();
            }
        }
        
        public PropertyImplementation<string> Name = new PropertyImplementation<string>();
        
        string IQuickEditItem.Name
        {
            get
            {
                return this.Name.get();
            }
        }
        public PropertyImplementation<string> Description = new PropertyImplementation<string>();
        
        public PropertyImplementation<string> WikiLink = new PropertyImplementation<string>();
        
        string IQuickEditItem.WikiLink
        {
            get
            {
                return this.WikiLink.get();
            }
        }
        public EventImplementation<System.EventArgs> CanRunChanged = new EventImplementation<System.EventArgs>();
        
        event System.EventHandler IQuickEditItem.CanRunChanged
        {
            add
            {
                CanRunChanged.add(new EventHandler<System.EventArgs>(value));
            }
            remove
            {
                CanRunChanged.remove(new EventHandler<System.EventArgs>(value));
            }
        }
    }
}
