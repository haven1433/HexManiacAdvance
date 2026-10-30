// this file was created by AutoImplement
namespace System.Windows.Input
{
    public class CommandDecorator : ICommand
    {
        protected ICommand InnerCommand { get; set; }
        public virtual bool CanExecute(object parameter)
        {
            if (InnerCommand != null)
            {
                return InnerCommand.CanExecute(parameter);
            }
            return default(bool);
        }
        
        public virtual void Execute(object parameter)
        {
            if (InnerCommand != null)
            {
                InnerCommand.Execute(parameter);
            }
        }
        
        public virtual event System.EventHandler CanExecuteChanged
        {
            add
            {
                if (InnerCommand != null)
                {
                    InnerCommand.CanExecuteChanged += value;
                }
            }
            remove
            {
                if (InnerCommand != null)
                {
                    InnerCommand.CanExecuteChanged -= value;
                }
            }
        }
    }
}
