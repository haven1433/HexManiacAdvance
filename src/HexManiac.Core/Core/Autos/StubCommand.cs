using System;
using System.Collections.Generic;
using HavenSoft.AutoImplement.Delegation;

// this file was created by AutoImplement
namespace System.Windows.Input
{
    public class StubCommand : ICommand
    {
        public Func<object, bool> CanExecute { get; set; }
        
        bool ICommand.CanExecute(object parameter)
        {
            if (this.CanExecute != null)
            {
                return this.CanExecute(parameter);
            }
            else
            {
                return default(bool);
            }
        }
        
        public Action<object> Execute { get; set; }
        
        void ICommand.Execute(object parameter)
        {
            if (this.Execute != null)
            {
                this.Execute(parameter);
            }
        }
        
        public EventImplementation<System.EventArgs> CanExecuteChanged = new EventImplementation<System.EventArgs>();
        
        event System.EventHandler ICommand.CanExecuteChanged
        {
            add
            {
                CanExecuteChanged.add(new EventHandler<System.EventArgs>(value));
            }
            remove
            {
                CanExecuteChanged.remove(new EventHandler<System.EventArgs>(value));
            }
        }
    }
}
