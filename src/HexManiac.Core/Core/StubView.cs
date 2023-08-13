using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core {
   public class StubView {
      public List<string> PropertyNotifications { get; } = new();
      public List<string> CommandCanExecuteChangedNotifications { get; } = new();
      public List<string> EventNotifications { get; } = new();
      public StubView(INotifyPropertyChanged viewModel) {
         viewModel.PropertyChanged += Collect;

         foreach (var commandProperty in viewModel.GetType().GetProperties().Where(prop => typeof(ICommand).IsAssignableFrom(prop.PropertyType))) {
            var command = (ICommand)commandProperty.GetValue(viewModel);
            if (command == null) continue;
            command.CanExecuteChanged += (sender, e) => CommandCanExecuteChangedNotifications.Add(commandProperty.Name);
         }

         // collect calls to any event of type EventHandler / EventHandler<T>
         foreach (var ev in viewModel.GetType().GetEvents()) {
            var closureCapture = ev;
            try {
               if (ev.EventHandlerType == typeof(EventHandler)) {
                  ev.AddEventHandler(viewModel, (EventHandler)((sender, e) => {
                     EventNotifications.Add(closureCapture.Name);
                  }));
                  continue;
               } else if (ev.EventHandlerType == typeof(PropertyChangedEventHandler)) {
                  ev.AddEventHandler(viewModel, (PropertyChangedEventHandler)((sender, e) => {
                     EventNotifications.Add(closureCapture.Name);
                  }));
                  continue;
               } else if (ev.EventHandlerType == typeof(NotifyCollectionChangedEventHandler)) {
                  ev.AddEventHandler(viewModel, (NotifyCollectionChangedEventHandler)((sender, e) => {
                     EventNotifications.Add(closureCapture.Name);
                  }));
                  continue;
               }
               var eventParams = ev.EventHandlerType
                  .GetMethod("Invoke")
                  .GetParameters()[1].ParameterType;
               var genericCreate = GetType()
                  .GetMethod("CreateNotifier", BindingFlags.NonPublic | BindingFlags.Instance);
               var create = genericCreate.MakeGenericMethod(eventParams);
               var name = new object[] { closureCapture.Name };
               ev.AddEventHandler(viewModel, (Delegate)create.Invoke(this, name));
            } catch {
               // failed to add event handler, that's ok
               Debugger.Break();
            }
         }
      }

      private EventHandler<T2> CreateNotifier<T2>(string eventName) {
         return (object sender, T2 e) => {
            EventNotifications.Add(eventName);
         };
      }

      private void Collect(object sender, PropertyChangedEventArgs e) {
         PropertyNotifications.Add(e.PropertyName);
      }
   }
}
