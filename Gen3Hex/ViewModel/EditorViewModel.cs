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
      private readonly StubCommand newCommand, open, save, saveAs, saveAll, close, closeAll, undo, redo, back, forward, gotoCommand, showGoto, clearError;
      private readonly Dictionary<Func<ITabContent, ICommand>, EventHandler> forwardExecuteChangeNotifications;

      public ICommand New => newCommand;
      public ICommand Open => open;
      public ICommand Save => save;
      public ICommand SaveAs => saveAs;
      public ICommand SaveAll => saveAll;
      public ICommand Close => close;
      public ICommand CloseAll => closeAll;
      public ICommand Undo => undo;
      public ICommand Redo => redo;
      public ICommand Back => back;
      public ICommand Forward => forward;
      public ICommand Goto => gotoCommand;
      public ICommand ShowGoto => showGoto;
      public ICommand ClearError => clearError;

      private bool gotoControlVisible;
      public bool GotoControlVisible {
         get => gotoControlVisible;
         private set {
            TryUpdate(ref gotoControlVisible, value);
            if (value) ClearError.Execute();
         }
      }

      private bool showError;
      public bool ShowError {
         get => showError;
         private set {
            if (TryUpdate(ref showError, value)) clearError.CanExecuteChanged.Invoke(clearError, EventArgs.Empty);
         }
      }

      private string errorMessage;
      public string ErrorMessage {
         get => errorMessage;
         private set {
            if (TryUpdate(ref errorMessage, value)) ShowError = !string.IsNullOrEmpty(ErrorMessage);
         }
      }

      #region Collection Properties

      public event NotifyCollectionChangedEventHandler CollectionChanged;

      public ITabContent this[int index] => tabs[index];

      public int Count => tabs.Count;

      private int selectedIndex;
      public int SelectedIndex {
         get => selectedIndex;
         set {
            StopListeningToCommandsFromCurrentTab();
            TryUpdate(ref selectedIndex, value);
            StartListeningToCommandsFromCurrentTab();
         }
      }

      private ITabContent SelectedTab => SelectedIndex < 0 ? null : tabs[SelectedIndex];

      #endregion

      public EditorViewModel(IFileSystem fileSystem) {
         this.fileSystem = fileSystem;
         tabs = new List<ITabContent>();
         selectedIndex = -1;

         bool CanAlwaysExecute(object arg) => true;

         newCommand = new StubCommand {
            CanExecute = CanAlwaysExecute,
            Execute = arg => Add(new ViewPort()),
         };
         open = new StubCommand {
            CanExecute = CanAlwaysExecute,
            Execute = arg => {
               var file = arg as LoadedFile ?? fileSystem.OpenFile();
               if (file == null) return;
               Add(new ViewPort(file));
            },
         };
         gotoCommand = new StubCommand {
            CanExecute = arg => SelectedTab?.Goto?.CanExecute(arg) ?? false,
            Execute = arg => {
               SelectedTab?.Goto?.Execute(arg);
               GotoControlVisible = false;
            },
         };
         showGoto = new StubCommand {
            CanExecute = CanAlwaysExecute,
            Execute = arg => GotoControlVisible = (bool)arg,
         };
         clearError = new StubCommand {
            CanExecute = arg => showError,
            Execute = arg => ErrorMessage = string.Empty,
         };
         save = CreateWrapperForSelected(tab => tab.Save);
         saveAs = CreateWrapperForSelected(tab => tab.SaveAs);
         saveAll = CreateWrapperForAll(tab => tab.Save);
         close = CreateWrapperForSelected(tab => tab.Close);
         closeAll = CreateWrapperForAll(tab => tab.Close);
         undo = CreateWrapperForSelected(tab => tab.Undo);
         redo = CreateWrapperForSelected(tab => tab.Redo);
         back = CreateWrapperForSelected(tab => tab.Back);
         forward = CreateWrapperForSelected(tab => tab.Forward);

         forwardExecuteChangeNotifications = new Dictionary<Func<ITabContent, ICommand>, EventHandler> {
            { tab => tab.Save, (sender, e) => save.CanExecuteChanged.Invoke(this, e) },
            { tab => tab.SaveAs, (sender, e) => saveAs.CanExecuteChanged.Invoke(this, e) },
            { tab => tab.Close, (sender, e) => close.CanExecuteChanged.Invoke(this, e) },
            { tab => tab.Undo, (sender, e) => undo.CanExecuteChanged.Invoke(this, e) },
            { tab => tab.Redo, (sender, e) => redo.CanExecuteChanged.Invoke(this, e) },
            { tab => tab.Back, (sender, e) => back.CanExecuteChanged.Invoke(this, e) },
            { tab => tab.Forward, (sender, e) => forward.CanExecuteChanged.Invoke(this, e) },
         };
      }

      public void Add(ITabContent content) {
         tabs.Add(content);
         SelectedIndex = tabs.Count - 1;
         CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, content));
         content.Closed += RemoveTab;
         content.OnError += AcceptError;
         if (content.Save != null) content.Save.CanExecuteChanged += RaiseSaveAllCanExecuteChanged;
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

      private StubCommand CreateWrapperForSelected(Func<ITabContent, ICommand> commandGetter) {
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

      private StubCommand CreateWrapperForAll(Func<ITabContent, ICommand> commandGetter) {
         var command = new StubCommand {
            CanExecute = arg => tabs.Any(tab => commandGetter(tab)?.CanExecute(fileSystem) ?? false),
            Execute = arg => tabs.ToList().ForEach(tab => { // some commands may modify the tabs. Make a copy of the list before running a foreach.
               if (commandGetter(tab).CanExecute(fileSystem)) {
                  commandGetter(tab).Execute(fileSystem);
               }
            }),
         };

         return command;
      }

      private void RemoveTab(object sender, EventArgs e) {
         var tab = (ITabContent)sender;
         if (!tabs.Contains(tab)) throw new InvalidOperationException("Cannot remove tab, because tab is not currently in editor.");
         if (tab.Save != null) tab.Save.CanExecuteChanged -= RaiseSaveAllCanExecuteChanged;
         var index = tabs.IndexOf(tab);

         // if the tab to remove is the selected tab, select the next tab (or the previous if there is no next)
         if (index == SelectedIndex) {
            StopListeningToCommandsFromCurrentTab();
            tabs.Remove(tab);
            tab.Closed -= RemoveTab;
            if (selectedIndex == tabs.Count) TryUpdate(ref selectedIndex, tabs.Count - 1, nameof(SelectedIndex));
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, tab, index));
            StartListeningToCommandsFromCurrentTab();
            return;
         }

         // if the removed tab was left of the selected tab, we need to adjust the selected index based on the removal
         if (index < SelectedIndex) selectedIndex--;

         tabs.Remove(tab);
         tab.Closed -= RemoveTab;
         tab.OnError -= AcceptError;
         CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, tab, index));
      }

      private void AcceptError(object sender, string message) => ErrorMessage = message;

      private void StartListeningToCommandsFromCurrentTab() {
         var commandsToRefresh = new List<StubCommand> {
            undo,
            redo,
            save,
            saveAs,
            close,
            back,
            forward,
         };
         commandsToRefresh.ForEach(command => command.CanExecuteChanged.Invoke(command, EventArgs.Empty));

         if (selectedIndex == -1) return;

         var tab = tabs[selectedIndex];
         foreach (var kvp in forwardExecuteChangeNotifications) {
            var getCommand = kvp.Key;
            var notify = kvp.Value;
            var command = getCommand(tab);
            if (command != null) command.CanExecuteChanged += notify;
         }
      }

      private void StopListeningToCommandsFromCurrentTab() {
         if (selectedIndex == -1) return;
         var tab = tabs[selectedIndex];
         foreach (var kvp in forwardExecuteChangeNotifications) {
            var getCommand = kvp.Key;
            var notify = kvp.Value;
            var command = getCommand(tab);
            if (command != null) command.CanExecuteChanged -= notify;
         }
      }

      private void RaiseSaveAllCanExecuteChanged(object sender, EventArgs e) => saveAll.CanExecuteChanged.Invoke(this, e);
   }
}
