using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Tests {
   public class StubView {
      public List<string> PropertyNotifications { get; } = new List<string>();
      public List<string> CommandCanExecuteChangedNotifications { get; } = new List<string>();
      public StubView(INotifyPropertyChanged viewModel) {
         viewModel.PropertyChanged += Collect;

         foreach (var commandProperty in viewModel.GetType().GetProperties().Where(prop => typeof(ICommand).IsAssignableFrom(prop.PropertyType))) {
            var command = (ICommand)commandProperty.GetValue(viewModel);
            command.CanExecuteChanged += (sender, e) => CommandCanExecuteChangedNotifications.Add(commandProperty.Name);
         }
      }

      private void Collect(object sender, PropertyChangedEventArgs e) {
         PropertyNotifications.Add(e.PropertyName);
      }
   }
}
