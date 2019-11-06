using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels.QuickEditItems;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using static HavenSoft.HexManiac.Core.ICommandExtensions;

namespace HavenSoft.HexManiac.Core.ViewModels {
   public class EditorViewModel : ViewModelCore, IEnumerable<ITabContent>, INotifyCollectionChanged {
      public const string ApplicationName = "HexManiac";
      private const int MaxReasonableResults = 400; // limit for performance reasons

      private readonly IFileSystem fileSystem;
      private readonly bool allowLoadingMetadata;
      private readonly List<ITabContent> tabs;

      private readonly List<StubCommand> commandsToRefreshOnTabChange = new List<StubCommand>();
      private readonly StubCommand
         newCommand = new StubCommand(),
         open = new StubCommand(),
         save = new StubCommand(),
         saveAs = new StubCommand(),
         saveAll = new StubCommand(),
         close = new StubCommand(),
         closeAll = new StubCommand(),

         undo = new StubCommand(),
         redo = new StubCommand(),
         cut = new StubCommand(),
         copy = new StubCommand(),
         paste = new StubCommand(),
         delete = new StubCommand(),

         back = new StubCommand(),
         forward = new StubCommand(),
         find = new StubCommand(),
         findPrevious = new StubCommand(),
         findNext = new StubCommand(),
         showFind = new StubCommand(),
         hideSearchControls = new StubCommand(),
         resetZoom = new StubCommand(),
         resetAlignment = new StubCommand(),
         resetTheme = new StubCommand(),
         clearError = new StubCommand(),
         clearMessage = new StubCommand(),
         toggleMatrix = new StubCommand(),
         toggleTableHeaders = new StubCommand();

      private readonly Dictionary<Func<ITabContent, ICommand>, EventHandler> forwardExecuteChangeNotifications;
      private (IViewPort tab, int start, int end)[] recentFindResults = new (IViewPort, int start, int end)[0];
      private int currentFindResultIndex;

      public ICommand New => newCommand;
      public ICommand Open => open;                // parameter: file to open (or null)
      public ICommand Save => save;
      public ICommand SaveAs => saveAs;
      public ICommand SaveAll => saveAll;
      public ICommand Close => close;
      public ICommand CloseAll => closeAll;
      public ICommand Undo => undo;
      public ICommand Redo => redo;
      public ICommand Cut => cut;
      public ICommand Copy => copy;
      public ICommand Paste => paste;
      public ICommand Delete => delete;
      public ICommand Back => back;
      public ICommand Forward => forward;
      public ICommand Find => find;                 // parameter: target string to search
      public ICommand FindPrevious => findPrevious; // parameter: target string to search
      public ICommand FindNext => findNext;         // parameter: target string to search
      public ICommand ShowFind => showFind;         // parameter: true for show, false for hide
      public ICommand HideSearchControls => hideSearchControls;
      public ICommand ResetZoom => resetZoom;
      public ICommand ResetAlignment => resetAlignment;
      public ICommand ResetTheme => resetTheme;
      public ICommand ClearError => clearError;
      public ICommand ClearMessage => clearMessage;
      public ICommand ToggleMatrix => toggleMatrix;
      public ICommand ToggleTableHeaders => toggleTableHeaders;

      private GotoControlViewModel gotoViewModel = new GotoControlViewModel(null);
      public GotoControlViewModel GotoViewModel {
         get => gotoViewModel;
         private set {
            var old = gotoViewModel;
            gotoViewModel = value;
            NotifyPropertyChanged(old, nameof(GotoViewModel));
         }
      }

      private bool findControlVisible;
      public bool FindControlVisible {
         get => findControlVisible;
         private set {
            if (value) {
               ClearError.Execute();
               ClearMessage.Execute();
               gotoViewModel.ControlVisible = false;
            }
            TryUpdate(ref findControlVisible, value);
            if (value) MoveFocusToFind?.Invoke(this, EventArgs.Empty);
         }
      }

      private int zoomLevel = 16;
      public int ZoomLevel {
         get => zoomLevel;
         set => TryUpdate(ref zoomLevel, value);
      }

      private bool showError;
      public bool ShowError {
         get => showError;
         private set {
            if (value) {
               gotoViewModel.ControlVisible = false;
               FindControlVisible = false;
               ShowMessage = false;
            }
            if (TryUpdate(ref showError, value)) clearError.CanExecuteChanged.Invoke(clearError, EventArgs.Empty);
         }
      }

      private string errorMessage;
      public string ErrorMessage {
         get => errorMessage;
         private set {
            TryUpdate(ref errorMessage, value);
            ShowError = !string.IsNullOrEmpty(ErrorMessage);
         }
      }

      private bool showMessage;
      public bool ShowMessage {
         get => showMessage;
         private set {
            if (value) {
               gotoViewModel.ControlVisible = false;
               FindControlVisible = false;
               ShowError = false;
            } else {
               infoMessage = string.Empty;
            }
            if (TryUpdate(ref showMessage, value)) clearMessage.CanExecuteChanged.Invoke(clearMessage, EventArgs.Empty);
         }
      }

      private bool useTableEntryHeaders = true;
      public bool UseTableEntryHeaders {
         get => useTableEntryHeaders;
         set {
            if (!TryUpdate(ref useTableEntryHeaders, value)) return;
            foreach (var tab in tabs) {
               if (tab is ViewPort viewModel) viewModel.UseCustomHeaders = useTableEntryHeaders;
            }
         }
      }

      private bool showMatrix = true;
      public bool ShowMatrix {
         get => showMatrix;
         set => TryUpdate(ref showMatrix, value);
      }

      public Theme Theme { get; }

      private string infoMessage;
      public string InformationMessage {
         get => infoMessage;
         private set {
            if (TryUpdate(ref infoMessage, value)) ShowMessage = !string.IsNullOrEmpty(InformationMessage);
         }
      }

      public IToolTrayViewModel Tools => (SelectedTab as IViewPort)?.Tools;

      public IReadOnlyList<IQuickEditItem> QuickEdits { get; } = new List<IQuickEditItem> {
         new MakeTutorsExpandable(),
         // new MakeTmsExpandable(),   // expanding TMs requires further research.
         // new MakeItemsExpandable(),
      };

      public event EventHandler<Action> RequestDelayedWork;

      public event EventHandler MoveFocusToFind;

      #region Collection Properties

      public event NotifyCollectionChangedEventHandler CollectionChanged;

      public ITabContent this[int index] => tabs[index];

      public int Count => tabs.Count;

      private int selectedIndex;

      public int SelectedIndex {
         get => selectedIndex;
         set {
            using (WorkWithoutListeningToCommandsFromCurrentTab()) {
               if (TryUpdate(ref selectedIndex, value)) {
                  findPrevious.CanExecuteChanged.Invoke(findPrevious, EventArgs.Empty);
                  findNext.CanExecuteChanged.Invoke(findNext, EventArgs.Empty);
                  UpdateGotoViewModel();
                  foreach (var edit in QuickEdits) edit.TabChanged();
               }
            }
         }
      }

      private ITabContent SelectedTab => SelectedIndex < 0 ? null : tabs[SelectedIndex];

      #endregion

      public EditorViewModel(IFileSystem fileSystem, bool allowLoadingMetadata = true) {
         this.fileSystem = fileSystem;
         this.allowLoadingMetadata = allowLoadingMetadata;
         tabs = new List<ITabContent>();
         selectedIndex = -1;

         ImplementCommands();

         copy = CreateWrapperForSelected(tab => tab.Copy);
         delete = CreateWrapperForSelected(tab => tab.Clear);
         save = CreateWrapperForSelected(tab => tab.Save);
         saveAs = CreateWrapperForSelected(tab => tab.SaveAs);
         close = CreateWrapperForSelected(tab => tab.Close);
         undo = CreateWrapperForSelected(tab => tab.Undo);
         redo = CreateWrapperForSelected(tab => tab.Redo);
         back = CreateWrapperForSelected(tab => tab.Back);
         forward = CreateWrapperForSelected(tab => tab.Forward);
         resetAlignment = CreateWrapperForSelected(tab => tab.ResetAlignment);

         saveAll = CreateWrapperForAll(tab => tab.Save);
         closeAll = CreateWrapperForAll(tab => tab.Close);

         forwardExecuteChangeNotifications = new Dictionary<Func<ITabContent, ICommand>, EventHandler> {
            { tab => tab.Copy, (sender, e) => copy.CanExecuteChanged.Invoke(this, e) },
            { tab => tab.Clear, (sender, e) => delete.CanExecuteChanged.Invoke(this, e) },
            { tab => tab.Save, (sender, e) => save.CanExecuteChanged.Invoke(this, e) },
            { tab => tab.SaveAs, (sender, e) => saveAs.CanExecuteChanged.Invoke(this, e) },
            { tab => tab.Close, (sender, e) => close.CanExecuteChanged.Invoke(this, e) },
            { tab => tab.Undo, (sender, e) => undo.CanExecuteChanged.Invoke(this, e) },
            { tab => tab.Redo, (sender, e) => redo.CanExecuteChanged.Invoke(this, e) },
            { tab => tab.Back, (sender, e) => back.CanExecuteChanged.Invoke(this, e) },
            { tab => tab.Forward, (sender, e) => forward.CanExecuteChanged.Invoke(this, e) },
            { tab => tab.ResetAlignment, (sender, e) => resetAlignment.CanExecuteChanged.Invoke(this, e) },
         };

         var metadata = fileSystem.MetadataFor(ApplicationName) ?? new string[0];
         Theme = new Theme(metadata);
         ShowMatrix = !metadata.Contains("ShowMatrixGrid = False");
         var zoomLine = metadata.FirstOrDefault(line => line.StartsWith("ZoomLevel ="));
         if (zoomLine != null && int.TryParse(zoomLine.Split('=').Last().Trim(), out var zoomLevel)) ZoomLevel = zoomLevel;
      }

      public static ICommand Wrap(IQuickEditItem quickEdit) => new StubCommand {
         CanExecute = arg => quickEdit.CanRun((IViewPort)arg),
         Execute = arg => quickEdit.Run((IViewPort)arg),
      };

      public void WriteAppLevelMetadata() {
         var metadata = new List<string>();
         metadata.Add("[GeneralSettings]");
         metadata.Add($"ShowMatrixGrid = {ShowMatrix}");
         metadata.Add($"ZoomLevel = {ZoomLevel}");
         metadata.Add(string.Empty);
         metadata.AddRange(Theme.Serialize());
         fileSystem.SaveMetadata(ApplicationName, metadata.ToArray());
      }

      private void ImplementCommands() {
         newCommand.CanExecute = CanAlwaysExecute;
         newCommand.Execute = arg => Add(new ViewPort());

         open.CanExecute = CanAlwaysExecute;
         open.Execute = arg => {
            try {
               var file = arg as LoadedFile ?? fileSystem.OpenFile("GameBoy Advanced", "gba");
               if (file == null) return;
               string[] metadataText = new string[0];
               if (allowLoadingMetadata) {
                  metadataText = fileSystem.MetadataFor(file.Name) ?? new string[0];
               }
               var metadata = new StoredMetadata(metadataText);
               var viewPort = new ViewPort(file.Name, new HardcodeTablesModel(file.Contents, metadata, trackChanges: true));
               if (metadata.IsEmpty) {
                  var createdMetadata = viewPort.Model.ExportMetadata().Serialize();
                  fileSystem.SaveMetadata(file.Name, createdMetadata);
               }
               Add(viewPort);
            } catch (IOException ex) {
               ErrorMessage = ex.Message;
            }
         };

         ImplementFindCommands();

         hideSearchControls.CanExecute = CanAlwaysExecute;
         hideSearchControls.Execute = arg => {
            gotoViewModel.ControlVisible = false;
            FindControlVisible = false;
            ShowError = false;
            ShowMessage = false;
         };

         clearError.CanExecute = arg => showError;
         clearError.Execute = arg => ErrorMessage = string.Empty;

         clearMessage.CanExecute = arg => showMessage;
         clearMessage.Execute = arg => InformationMessage = string.Empty;

         toggleMatrix.CanExecute = CanAlwaysExecute;
         toggleMatrix.Execute = arg => ShowMatrix = !ShowMatrix;

         resetZoom.CanExecute = CanAlwaysExecute;
         resetZoom.Execute = arg => ZoomLevel = 16;

         resetTheme.CanExecute = CanAlwaysExecute;
         resetTheme.Execute = arg => Theme.Reset();

         toggleTableHeaders.CanExecute = CanAlwaysExecute;
         toggleTableHeaders.Execute = arg => UseTableEntryHeaders = !UseTableEntryHeaders;

         cut.CanExecute = arg => SelectedTab?.Copy?.CanExecute(arg) ?? false;
         cut.Execute = arg => {
            if (SelectedTab != null && SelectedTab.Copy != null && SelectedTab.Clear != null) {
               SelectedTab.Copy.Execute(fileSystem);
               SelectedTab.Clear.Execute();
            }
         };

         paste.CanExecute = arg => SelectedTab is ViewPort;
         paste.Execute = arg => {
            (SelectedTab as ViewPort)?.Edit(fileSystem.CopyText);
         };
      }

      private void ImplementFindCommands() {
         find.CanExecute = CanAlwaysExecute;
         find.Execute = arg => FindExecuted((string)arg);

         findPrevious.CanExecute = arg => recentFindResults.Any(pair => pair.tab == SelectedTab);
         findPrevious.Execute = arg => {
            int attemptCount = 0;
            while (attemptCount < recentFindResults.Length) {
               attemptCount++;
               currentFindResultIndex--;
               if (currentFindResultIndex < 0) currentFindResultIndex += recentFindResults.Length;
               var (tab, start, end) = recentFindResults[currentFindResultIndex];
               if (tab != SelectedTab) continue;
               JumpTo(tab, start, end);
               break;
            }
         };

         findNext.CanExecute = arg => recentFindResults.Any(pair => pair.tab == SelectedTab);
         findNext.Execute = arg => {
            int attemptCount = 0;
            while (attemptCount < recentFindResults.Length) {
               attemptCount++;
               currentFindResultIndex++;
               if (currentFindResultIndex >= recentFindResults.Length) currentFindResultIndex -= recentFindResults.Length;
               var (tab, start, end) = recentFindResults[currentFindResultIndex];
               if (tab != SelectedTab) continue;
               JumpTo(tab, start, end);
               break;
            }
         };

         showFind.CanExecute = CanAlwaysExecute;
         showFind.Execute = arg => FindControlVisible = (bool)arg;
      }

      private static void JumpTo(IViewPort tab, int start, int end) {
         tab.Goto.Execute(start.ToString("X2"));
         if (tab is ViewPort viewPort) {
            viewPort.SelectionEnd = viewPort.ConvertAddressToViewPoint(end);
         }
      }

      public void Add(ITabContent content) {
         tabs.Add(content);
         SelectedIndex = tabs.Count - 1;
         CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, content));
         AddContentListeners(content);
         if (content is IViewPort viewModel) {
            viewModel.UseCustomHeaders = useTableEntryHeaders;
            viewModel.ValidateMatchedWords();
         }
      }

      public void SwapTabs(int a, int b) {
         var temp = tabs[a];
         tabs[a] = tabs[b];
         tabs[b] = temp;

         // if one of the items to swap is selected, swap the selection too
         if (selectedIndex == a) {
            selectedIndex = b;
         } else if (selectedIndex == b) {
            selectedIndex = a;
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
               var tab = tabs[SelectedIndex];
               var innerCommand = commandGetter(tab);
               if (innerCommand == null) return;

               // special case: for save/save as, remove the file listener
               // otherwise the filesystem will notify us of our own change
               if ((innerCommand == tab.Save || innerCommand == tab.SaveAs) && tab is IViewPort viewPort && !string.IsNullOrEmpty(viewPort.FileName)) {
                  fileSystem.RemoveListenerForFile(viewPort.FileName, viewPort.ConsiderReload);
                  using (new StubDisposable { Dispose = () => fileSystem.AddListenerToFile(viewPort.FileName, viewPort.ConsiderReload) }) {
                     innerCommand.Execute(fileSystem);
                  }
               } else {
                  innerCommand.Execute(fileSystem);
               }
            }
         };

         commandsToRefreshOnTabChange.Add(command);

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

      private void FindExecuted(string search) {
         var results = new List<(IViewPort, int start, int end)>();
         foreach (var tab in tabs) {
            if (tab is IViewPort viewPort) results.AddRange(viewPort.Find(search).Select(offset => (viewPort, offset.start, offset.end)));
         }

         FindControlVisible = false;

         if (results.Count == 0) {
            ErrorMessage = $"Could not find {search}.";
            return;
         }

         recentFindResults = results.ToArray();
         findPrevious.CanExecuteChanged.Invoke(findPrevious, EventArgs.Empty);
         findNext.CanExecuteChanged.Invoke(findNext, EventArgs.Empty);

         if (results.Count == 1) {
            var (tab, start, end) = results[0];
            SelectedIndex = tabs.IndexOf(tab);
            tab.Goto.Execute(start.ToString("X2"));
            if (tab is ViewPort viewPort) SearchResultsViewPort.SelectRange(viewPort, (start, end));
            return;
         }

         if (results.Count > MaxReasonableResults) {
            ErrorMessage = $"Found {results.Count} results: please refine your search.";
            return;
         }

         var newTab = new SearchResultsViewPort(search);
         foreach (var (tab, start, end) in results) {
            newTab.Add(tab.CreateChildView(start, end), start, end);
         }

         Add(newTab);
      }

      private void RemoveTab(object sender, EventArgs e) {
         var tab = (ITabContent)sender;
         if (!tabs.Contains(tab)) throw new InvalidOperationException("Cannot remove tab, because tab is not currently in editor.");
         var index = tabs.IndexOf(tab);

         // if the tab to remove is the selected tab, select the next tab (or the previous if there is no next)
         if (index == SelectedIndex) {
            using (WorkWithoutListeningToCommandsFromCurrentTab()) {
               tabs.Remove(tab);
               RemoveContentListeners(tab);
               if (selectedIndex == tabs.Count) TryUpdate(ref selectedIndex, tabs.Count - 1, nameof(SelectedIndex));
               CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, tab, index));
            }
            UpdateGotoViewModel();
            return;
         }

         // if the removed tab was left of the selected tab, we need to adjust the selected index based on the removal
         if (index < SelectedIndex) selectedIndex--;

         tabs.Remove(tab);
         RemoveContentListeners(tab);
         CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, tab, index));
      }

      private void AddContentListeners(ITabContent content) {
         content.Closed += RemoveTab;
         content.OnError += AcceptError;
         content.OnMessage += AcceptMessage;
         content.ClearMessage += AcceptMessageClear;
         content.RequestTabChange += TabChangeRequested;
         content.RequestDelayedWork += ForwardDelayedWork;
         content.PropertyChanged += TabPropertyChanged;
         if (content.Save != null) content.Save.CanExecuteChanged += RaiseSaveAllCanExecuteChanged;

         if (content is IViewPort viewPort && !string.IsNullOrEmpty(viewPort.FileName)) {
            fileSystem.AddListenerToFile(viewPort.FileName, viewPort.ConsiderReload);
         }
      }

      private void RemoveContentListeners(ITabContent content) {
         content.Closed -= RemoveTab;
         content.OnError -= AcceptError;
         content.OnMessage -= AcceptMessage;
         content.ClearMessage -= AcceptMessageClear;
         content.RequestTabChange -= TabChangeRequested;
         content.RequestDelayedWork -= ForwardDelayedWork;
         content.PropertyChanged -= TabPropertyChanged;
         if (content.Save != null) content.Save.CanExecuteChanged -= RaiseSaveAllCanExecuteChanged;

         if (content is IViewPort viewPort && !string.IsNullOrEmpty(viewPort.FileName)) {
            fileSystem.RemoveListenerForFile(viewPort.FileName, viewPort.ConsiderReload);
         }
      }

      private void UpdateGotoViewModel() {
         GotoViewModel.PropertyChanged -= GotoPropertyChanged;
         GotoViewModel = new GotoControlViewModel(SelectedTab);
         GotoViewModel.PropertyChanged += GotoPropertyChanged;
         NotifyPropertyChanged(nameof(Tools));
      }

      private void ForwardDelayedWork(object sender, Action e) => RequestDelayedWork?.Invoke(this, e);

      private void TabPropertyChanged(object sender, PropertyChangedEventArgs e) {
         if (e.PropertyName == nameof(IViewPort.FileName) && sender is IViewPort viewPort) {
            var args = (ExtendedPropertyChangedEventArgs)e;
            var oldName = (string)args.OldValue;

            if (!string.IsNullOrEmpty(oldName)) fileSystem.RemoveListenerForFile(oldName, viewPort.ConsiderReload);
            if (!string.IsNullOrEmpty(viewPort.FileName)) fileSystem.AddListenerToFile(viewPort.FileName, viewPort.ConsiderReload);
         }

         // when one tab's height updates, update other tabs by the same amount.
         // this isn't perfect, since tabs shouldn't nessisarily change height at the same pixel.
         // but it'll keep the tabs that are out of view from getting totally out of sync.
         if (e.PropertyName == nameof(IViewPort.Height) && sender is IViewPort viewPort2) {
            var args = (ExtendedPropertyChangedEventArgs)e;
            var oldHeight = (int)args.OldValue;
            var height = viewPort2.Height;
            foreach (var tab in this) {
               if (tab == viewPort2) continue;
               if (!(tab is IViewPort viewPort3)) continue;
               RemoveContentListeners(tab);
               viewPort3.Height += height - oldHeight;
               AddContentListeners(tab);
            }
         }
      }

      private void GotoPropertyChanged(object sender, PropertyChangedEventArgs e) {
         if (e.PropertyName == nameof(gotoViewModel.ControlVisible) && gotoViewModel.ControlVisible) {
            ClearError.Execute();
            ClearMessage.Execute();
            FindControlVisible = false;
         }
      }

      private void AcceptError(object sender, string message) => ErrorMessage = message;

      private void AcceptMessage(object sender, string message) => InformationMessage = message;

      private void AcceptMessageClear(object sender, EventArgs e) => HideSearchControls.Execute();

      private void TabChangeRequested(object sender, ITabContent newTab) {
         if (sender != SelectedTab) return;
         var index = tabs.IndexOf(newTab);
         if (index == -1) {
            Add(newTab);
         } else {
            SelectedIndex = index;
         }
      }

      private void AdjustNotificationsFromCurrentTab(Action<ICommand, EventHandler> adjust) {
         if (selectedIndex == -1) return;
         var tab = tabs[selectedIndex];
         foreach (var kvp in forwardExecuteChangeNotifications) {
            var getCommand = kvp.Key;
            var notify = kvp.Value;
            var command = getCommand(tab);
            if (command != null) adjust(command, notify);
         }
      }

      private IDisposable WorkWithoutListeningToCommandsFromCurrentTab() {
         void add(ICommand command, EventHandler notify) => command.CanExecuteChanged += notify;
         void remove(ICommand command, EventHandler notify) => command.CanExecuteChanged -= notify;
         AdjustNotificationsFromCurrentTab(remove);
         return new StubDisposable {
            Dispose = () => {
               commandsToRefreshOnTabChange.ForEach(command => command.CanExecuteChanged.Invoke(command, EventArgs.Empty));
               AdjustNotificationsFromCurrentTab(add);
            }
         };
      }

      private void RaiseSaveAllCanExecuteChanged(object sender, EventArgs e) => saveAll.CanExecuteChanged.Invoke(this, e);
   }
}
