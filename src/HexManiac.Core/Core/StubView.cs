using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;


namespace HavenSoft.HexManiac.Core {
   public class StubView {
      public List<string> PropertyNotifications { get; } = new();
      public List<string> CommandCanExecuteChangedNotifications { get; } = new();
      public List<string> EventNotifications { get; } = new();

      public EventHandler<T2> CreateNotifier<T2>(string eventName) {
         return (object sender, T2 e) => {
            EventNotifications.Add(eventName);
         };
      }

      public void Collect(object sender, PropertyChangedEventArgs e) {
         PropertyNotifications.Add(e.PropertyName);
      }
   }
}
