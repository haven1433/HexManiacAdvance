using System.Linq;

// this file was created by AutoImplement
namespace System.Windows.Input
{
    public class CompositeCommand : System.Collections.Generic.List<ICommand>, ICommand
    {
        public virtual bool CanExecute(object parameter)
        {
            var results = new System.Collections.Generic.List<bool>();
            for (int i = 0; i < base.Count; i++)
            {
                results.Add(base[i].CanExecute(parameter));
            }
            if (results.Count > 0 && results.All(result => result.Equals(results[0])))
            {
                return results[0];
            }
            return default(bool);
        }
        
        public virtual void Execute(object parameter)
        {
            for (int i = 0; i < base.Count; i++)
            {
                base[i].Execute(parameter);
            }
        }
        
        public virtual event System.EventHandler CanExecuteChanged
        {
            add
            {
                this.ForEach(listItem => listItem.CanExecuteChanged += value);
            }
            remove
            {
                this.ForEach(listItem => listItem.CanExecuteChanged -= value);
            }
        }
    }
}
