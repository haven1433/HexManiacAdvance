using System.Collections.Generic;
using System.ComponentModel;

namespace HavenSoft.HexManiac.Tests {
   public class StubView {
      public List<string> Notifications { get; } = new List<string>();
      public StubView(INotifyPropertyChanged viewModel) {
         viewModel.PropertyChanged += Collect;
      }

      private void Collect(object sender, PropertyChangedEventArgs e) {
         Notifications.Add(e.PropertyName);
      }
   }
}
