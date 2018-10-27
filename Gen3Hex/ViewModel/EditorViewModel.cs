using HavenSoft.Gen3Hex.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows.Input;

namespace HavenSoft.Gen3Hex.ViewModel {
   public class EditorViewModel : ViewModelCore, IEnumerable<ITabContent>, INotifyCollectionChanged {

      private readonly IFileSystem fileSystem;
      private readonly List<ITabContent> tabs;
      private readonly StubCommand saveAll, closeAll;

      private int selectedIndex;

      public ICommand Undo => SelectedTab?.Undo;
      public ICommand Redo => SelectedTab?.Redo;
      public ICommand Save => SelectedTab?.Save;
      public ICommand SaveAs => SelectedTab?.SaveAs;
      public ICommand Close => SelectedTab?.Close;
      public ICommand SaveAll => saveAll;
      public ICommand CloseAll => closeAll;

      #region Collection Properties

      public event NotifyCollectionChangedEventHandler CollectionChanged;

      public ITabContent this[int index] => tabs[index];

      public int Count => tabs.Count;

      public int SelectedIndex {
         get => selectedIndex;
         set {
            if (!TryUpdate(ref selectedIndex, value)) return;
            var commandsToRefresh = new List<string> {
               nameof(Undo),
               nameof(Redo),
               nameof(Save),
               nameof(SaveAs),
               nameof(Close),
            };
            commandsToRefresh.ForEach(NotifyPropertyChanged);
         }
      }

      private ITabContent SelectedTab => SelectedIndex < 0 ? null : tabs[SelectedIndex];

      #endregion

      public EditorViewModel(IFileSystem fileSystem) {
         this.fileSystem = fileSystem;
         tabs = new List<ITabContent>();
         selectedIndex = -1;

         saveAll = new StubCommand {
            CanExecute = arg => tabs.Any(tab => tab.Save.CanExecute(fileSystem)),
            Execute = arg => tabs.ForEach(tab => {
               if (tab.Save.CanExecute(fileSystem)) {
                  tab.Save.Execute(fileSystem);
               }
            }),
         };
         closeAll = new StubCommand {
            CanExecute = arg => tabs.Any(tab => tab.Close.CanExecute(fileSystem)),
            Execute = arg => tabs.ForEach(tab => tab.Close.Execute(fileSystem)),
         };
      }

      public void Add(ITabContent content) {
         tabs.Add(content);
         SelectedIndex = tabs.Count - 1;
         CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, content));
         content.Closed += RemoveTab;
      }

      public void SwapTabs(int a, int b) {
         var temp = tabs[a];
         tabs[a] = tabs[b];
         tabs[b] = temp;
         CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
      }

      public IEnumerator<ITabContent> GetEnumerator() => tabs.GetEnumerator();

      IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

      private void RemoveTab(object sender, EventArgs e) {
         var tab = (ITabContent)sender;
         if (!tabs.Contains(tab)) throw new InvalidOperationException("Cannot remove tab, because tab is not currently in editor.");
         var index = tabs.IndexOf(tab);
         tabs.Remove(tab);
         tab.Closed -= RemoveTab;
         CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, tab));
         if (index < SelectedIndex) {
            selectedIndex--;
         } else if (index == SelectedIndex) {
            SelectedIndex = Math.Max(0, selectedIndex - 1);
         }

         if (tabs.Count == 0) SelectedIndex = -1;
      }
   }
}
