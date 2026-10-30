// this file was created by AutoImplement
namespace HavenSoft.HexManiac.Core.ViewModels.QuickEditItems
{
    public class QuickEditItemDecorator : IQuickEditItem
    {
        protected IQuickEditItem InnerQuickEditItem { get; set; }
        public virtual bool CanRun(HavenSoft.HexManiac.Core.ViewModels.IViewPort viewPort)
        {
            if (InnerQuickEditItem != null)
            {
                return InnerQuickEditItem.CanRun(viewPort);
            }
            return default(bool);
        }
        
        public virtual System.Threading.Tasks.Task<HavenSoft.HexManiac.Core.Models.ErrorInfo> Run(HavenSoft.HexManiac.Core.ViewModels.IViewPort viewPort)
        {
            if (InnerQuickEditItem != null)
            {
                return InnerQuickEditItem.Run(viewPort);
            }
            return default(System.Threading.Tasks.Task<HavenSoft.HexManiac.Core.Models.ErrorInfo>);
        }
        
        public virtual void TabChanged()
        {
            if (InnerQuickEditItem != null)
            {
                InnerQuickEditItem.TabChanged();
            }
        }
        
        public virtual string Name
        {
            get
            {
                if (InnerQuickEditItem != null)
                {
                    return InnerQuickEditItem.Name;
                }
                return default(string);
            }
        }
        public virtual string Description
        {
            get
            {
                if (InnerQuickEditItem != null)
                {
                    return InnerQuickEditItem.Description;
                }
                return default(string);
            }
        }
        public virtual string WikiLink
        {
            get
            {
                if (InnerQuickEditItem != null)
                {
                    return InnerQuickEditItem.WikiLink;
                }
                return default(string);
            }
        }
        public virtual event System.EventHandler CanRunChanged
        {
            add
            {
                if (InnerQuickEditItem != null)
                {
                    InnerQuickEditItem.CanRunChanged += value;
                }
            }
            remove
            {
                if (InnerQuickEditItem != null)
                {
                    InnerQuickEditItem.CanRunChanged -= value;
                }
            }
        }
    }
}
