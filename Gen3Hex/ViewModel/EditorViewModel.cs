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
      private readonly StubCommand newCommand, open, save, saveAs, saveAll, close, closeAll, undo, redo;

      private int selectedIndex;

      public ICommand New => newCommand;
      public ICommand Open => open;
      public ICommand Save => save;
      public ICommand SaveAs => saveAs;
      public ICommand SaveAll => saveAll;
      public ICommand Close => close;
      public ICommand CloseAll => closeAll;
      public ICommand Undo => undo;
      public ICommand Redo => redo;

      #region Collection Properties

      public event NotifyCollectionChangedEventHandler CollectionChanged;

      public ITabContent this[int index] => tabs[index];

      public int Count => tabs.Count;

      public int SelectedIndex {
         get => selectedIndex;
         set {
            StopListeningToCommandsFromCurrentTab();
            bool updated = TryUpdate(ref selectedIndex, value);
            StartListeningToCommandsFromCurrentTab();
            if (!updated) return;
         }
      }

      private ITabContent SelectedTab => SelectedIndex < 0 ? null : tabs[SelectedIndex];

      #endregion

      public EditorViewModel(IFileSystem fileSystem) {
         this.fileSystem = fileSystem;
         tabs = new List<ITabContent>();
         selectedIndex = -1;

         newCommand = new StubCommand {
            CanExecute = arg => true,
            Execute = arg => Add(new ViewPort()),
         };
         open = new StubCommand {
            CanExecute = arg => true,
            Execute = arg => {
               var fileName = arg as string;
               var file = arg as LoadedFile;
               if (file == null) file = fileSystem.OpenFile();
               if (file == null) return;
               var tab = new ViewPort(file);
               Add(tab);
            },
         };
         save = CreateWrapperFor(tab => tab.Save);
         saveAs = CreateWrapperFor(tab => tab.SaveAs);
         saveAll = new StubCommand {
            CanExecute = arg => tabs.Any(tab => tab.Save.CanExecute(fileSystem)),
            Execute = arg => tabs.ForEach(tab => {
               if (tab.Save.CanExecute(fileSystem)) {
                  tab.Save.Execute(fileSystem);
               }
            }),
         };
         close = CreateWrapperFor(tab => tab.Close);
         closeAll = new StubCommand {
            CanExecute = arg => tabs.Any(tab => tab.Close.CanExecute(fileSystem)),
            Execute = arg => tabs.ToList().ForEach(tab => { // ToList -> because closing a tab modifies the original list
               if (tab.Close.CanExecute(fileSystem)) {
                  tab.Close.Execute(fileSystem);
               }
            }),
         };
         undo = CreateWrapperFor(tab => tab.Undo);
         redo = CreateWrapperFor(tab => tab.Redo);
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

         // if one of the items to swap is selected, swap the selection too
         if (selectedIndex == a || selectedIndex == b) {
            selectedIndex ^= a;
            selectedIndex ^= b;
         }

         var args = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, tabs[a], a, b);
         CollectionChanged?.Invoke(this, args);
      }

      public IEnumerator<ITabContent> GetEnumerator() => tabs.GetEnumerator();

      IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

      private StubCommand CreateWrapperFor(Func<ITabContent, ICommand> commandGetter) {
         var command = new StubCommand {
            CanExecute = arg => {
               if (SelectedIndex < 0) return false;
               var innerCommand = commandGetter(tabs[SelectedIndex]);
               if (innerCommand == null) return false;
               return innerCommand.CanExecute(fileSystem);
            },
            Execute = arg => {
               var innerCommand = commandGetter(tabs[SelectedIndex]);
               if (innerCommand == null) return;
               innerCommand.Execute(fileSystem);
            }
         };

         return command;
      }

      private void RemoveTab(object sender, EventArgs e) {
         var tab = (ITabContent)sender;
         if (!tabs.Contains(tab)) throw new InvalidOperationException("Cannot remove tab, because tab is not currently in editor.");
         var index = tabs.IndexOf(tab);

         if (index == SelectedIndex) {
            StopListeningToCommandsFromCurrentTab();
            tabs.Remove(tab);
            tab.Closed -= RemoveTab;
            if (selectedIndex == tabs.Count) TryUpdate(ref selectedIndex, tabs.Count - 1, nameof(SelectedIndex));
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, tab, index));
            StartListeningToCommandsFromCurrentTab();
            return;
         }

         if (index < SelectedIndex) selectedIndex--;

         tabs.Remove(tab);
         tab.Closed -= RemoveTab;
         CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, tab, index));
      }

      private void StartListeningToCommandsFromCurrentTab() {
         var commandsToRefresh = new List<StubCommand> {
            undo,
            redo,
            save,
            saveAs,
            close,
         };
         commandsToRefresh.ForEach(command => command.CanExecuteChanged.Invoke(command, EventArgs.Empty));

         if (selectedIndex == -1) return;
         var tab = tabs[selectedIndex];

         if (tab.Save != null) tab.Save.CanExecuteChanged += RaiseSaveCanExecutedChanged;
         if (tab.SaveAs != null) tab.SaveAs.CanExecuteChanged += RaiseSaveAsCanExecutedChanged;
         if (tab.Close != null) tab.Close.CanExecuteChanged += RaiseCloseCanExecutedChanged;
         if (tab.Undo != null) tab.Undo.CanExecuteChanged += RaiseUndoCanExecutedChanged;
         if (tab.Redo != null) tab.Redo.CanExecuteChanged += RaiseRedoCanExecutedChanged;
      }

      private void StopListeningToCommandsFromCurrentTab() {
         if (selectedIndex == -1) return;
         var tab = tabs[selectedIndex];

         if (tab.Save != null) tab.Save.CanExecuteChanged -= RaiseSaveCanExecutedChanged;
         if (tab.SaveAs != null) tab.SaveAs.CanExecuteChanged -= RaiseSaveAsCanExecutedChanged;
         if (tab.Close != null) tab.Close.CanExecuteChanged -= RaiseCloseCanExecutedChanged;
         if (tab.Undo != null) tab.Undo.CanExecuteChanged -= RaiseUndoCanExecutedChanged;
         if (tab.Redo != null) tab.Redo.CanExecuteChanged -= RaiseRedoCanExecutedChanged;
      }

      private void RaiseSaveCanExecutedChanged(object sender, EventArgs e) => save.CanExecuteChanged.Invoke(this, e);
      private void RaiseSaveAsCanExecutedChanged(object sender, EventArgs e) => saveAs.CanExecuteChanged.Invoke(this, e);
      private void RaiseCloseCanExecutedChanged(object sender, EventArgs e) => close.CanExecuteChanged.Invoke(this, e);
      private void RaiseUndoCanExecutedChanged(object sender, EventArgs e) => undo.CanExecuteChanged.Invoke(this, e);
      private void RaiseRedoCanExecutedChanged(object sender, EventArgs e) => redo.CanExecuteChanged.Invoke(this, e);
   }
}
