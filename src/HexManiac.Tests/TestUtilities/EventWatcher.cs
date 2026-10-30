using System;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Tests {
   public class EventWatcher {
      public int Count { get; private set; }
      public EventWatcher(Action<EventHandler> handlerAction) => handlerAction((sender, e) => Count += 1);
   }
   public class CommandWatcher {
      public bool LastCanExecute { get; private set; }
      public CommandWatcher(ICommand command, object parameter = null) {
         LastCanExecute = command.CanExecute(parameter);
         command.CanExecuteChanged += (sender, e) => LastCanExecute = command.CanExecute(parameter);
      }
   }
}
