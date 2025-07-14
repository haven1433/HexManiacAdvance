// this file was created by AutoImplement
namespace HavenSoft.HexManiac.Core.ViewModels.QuickEditItems
{
    public class QuickEditItemDecorator : IQuickEditItem
    {
        protected IQuickEditItem InnerQuickEditItem { get; set; }
        
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
