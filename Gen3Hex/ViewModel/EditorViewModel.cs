using HavenSoft.Gen3Hex.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace HavenSoft.Gen3Hex.ViewModel {
   public class EditorViewModel : ViewModelCore, IEnumerable<ITabContent>, INotifyCollectionChanged {

      public ICommand Undo { get; }
      public ICommand Redo { get; }
      public ICommand Save { get; }
      public ICommand SaveAs { get; }
      public ICommand SaveAll { get; }
      public ICommand Close { get; }
      public ICommand CloseAll { get; }

      #region Collection Properties

      public event NotifyCollectionChangedEventHandler CollectionChanged;

      public ITabContent this[int index] => throw new NotImplementedException();

      public int Count => throw new NotImplementedException();

      public int SelectedIndex { get; set; }

      #endregion

      public EditorViewModel(IFileSystem fileSystem) { }

      public void Add(ITabContent content) { }

      public void SwapTabs(int a, int b) { }

      public IEnumerator<ITabContent> GetEnumerator() => throw new NotImplementedException();

      IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
   }
}
