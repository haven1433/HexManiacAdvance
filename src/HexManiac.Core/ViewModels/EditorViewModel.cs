using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.QuickEditItems;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Input;
using static HavenSoft.HexManiac.Core.ICommandExtensions;

namespace HavenSoft.HexManiac.Core.ViewModels {
   public class EditorViewModel : ViewModelCore, IEnumerable<ITabContent>, INotifyCollectionChanged {
      public const string ApplicationName = "HexManiacAdvance";
      private const int MaxReasonableResults = 400; // limit for performance reasons

      private readonly IFileSystem fileSystem;
      private readonly IWorkDispatcher workDispatcher;
      private readonly bool allowLoadingMetadata;
      private readonly List<ITabContent> tabs;

      private readonly List<StubCommand> commandsToRefreshOnTabChange = new List<StubCommand>();
      private readonly StubCommand
         newCommand = new StubCommand(),
         open = new StubCommand(),
         save = new StubCommand(),
         saveAs = new StubCommand(),
         exportBackup = new StubCommand(),
         saveAll = new StubCommand(),
         close = new StubCommand(),
         closeAll = new StubCommand(),

         undo = new StubCommand(),
         redo = new StubCommand(),
         cut = new StubCommand(),
         copy = new StubCommand(),
         deepCopy = new StubCommand(),
         paste = new StubCommand(),
         delete = new StubCommand(),
         selectAll = new StubCommand(),

         back = new StubCommand(),
         forward = new StubCommand(),
         find = new StubCommand(),
         findPrevious = new StubCommand(),
         findNext = new StubCommand(),
         showFind = new StubCommand(),
         showHexConverter = new StubCommand(),
         hideSearchControls = new StubCommand(),
         resetZoom = new StubCommand(),
         resetAlignment = new StubCommand(),
         resetTheme = new StubCommand(),
         clearError = new StubCommand(),
         clearMessage = new StubCommand(),
         toggleMatrix = new StubCommand(),
         toggleScrollAnimation = new StubCommand(),
         toggleTableHeaders = new StubCommand();

      private readonly Dictionary<Func<ITabContent, ICommand>, EventHandler> forwardExecuteChangeNotifications;
      private (IViewPort tab, int start, int end)[] recentFindResults = new (IViewPort, int start, int end)[0];
      private int currentFindResultIndex;

      private bool showDevMenu;
      public bool ShowDeveloperMenu { get => showDevMenu; set => Set(ref showDevMenu, value); }

      public ICommand New => newCommand;
      public ICommand Open => open;                // parameter: file to open (or null)
      public ICommand Save => save;
      public ICommand SaveAs => saveAs;
      public ICommand SaveAll => saveAll;
      public ICommand ExportBackup => exportBackup;
      public ICommand Close => close;
      public ICommand CloseAll => closeAll;
      public ICommand Undo => undo;
      public ICommand Redo => redo;
      public ICommand Cut => cut;
      public ICommand Copy => copy;
      public ICommand DeepCopy => deepCopy;
      public ICommand Paste => paste;
      public ICommand Delete => delete;
      public ICommand SelectAll => selectAll;
      public ICommand Back => back;
      public ICommand Forward => forward;
      public ICommand Find => find;                         // parameter: target string to search
      public ICommand FindPrevious => findPrevious;         // parameter: target string to search
      public ICommand FindNext => findNext;                 // parameter: target string to search
      public ICommand ShowFind => showFind;                 // parameter: true for show, false for hide
      public ICommand ShowHexConverter => showHexConverter; // parameter: true for show, false for hide
      public ICommand HideSearchControls => hideSearchControls;
      public ICommand ResetZoom => resetZoom;
      public ICommand ResetAlignment => resetAlignment;
      public ICommand ResetTheme => resetTheme;
      public ICommand ClearError => clearError;
      public ICommand ClearMessage => clearMessage;
      public ICommand ToggleMatrix => toggleMatrix;
      public ICommand ToggleScrollAnimation => toggleScrollAnimation;
      public ICommand ToggleTableHeaders => toggleTableHeaders;

      private StubCommand copyAlignedAddress, copyAnchorReference;
      public ICommand CopyAlignedAddress => StubCommand<IFileSystem>(ref copyAlignedAddress, ExecuteCopyAlignedAddress);
      public ICommand CopyAnchorReference => StubCommand<IFileSystem>(ref copyAnchorReference, ExecuteCopyAnchorReference);

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
               HexConverterVisible = false;
            }
            TryUpdate(ref findControlVisible, value);
            if (value) MoveFocusToFind?.Invoke(this, EventArgs.Empty);
         }
      }

      private bool hexConverterVisible;
      public bool HexConverterVisible {
         get => hexConverterVisible;
         private set {
            if (value) {
               ClearError.Execute();
               ClearMessage.Execute();
               gotoViewModel.ControlVisible = false;
               FindControlVisible = false;
            }
            TryUpdate(ref hexConverterVisible, value);
            if (value) MoveFocusToHexConverter?.Invoke(this, EventArgs.Empty);
         }
      }

      private string hexText;
      public string HexText {
         get => hexText;
         set {
            if (!TryUpdate(ref hexText, value)) return;
            var parseHex = hexText.Where(ViewPort.AllHexCharacters.Contains).Select(c => c.ToString()).Aggregate(string.Empty, string.Concat);
            if (!int.TryParse(parseHex, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out int result)) return;
            TryUpdate(ref decText, result.ToString(), nameof(DecText));
         }
      }

      private string decText;
      public string DecText {
         get => decText;
         set {
            if (!TryUpdate(ref decText, value)) return;
            if (!int.TryParse(decText, out int result)) return;
            TryUpdate(ref hexText, result.ToString("X2"), nameof(HexText));
         }
      }

      public void RunQuickEdit(IQuickEditItem edit) {
         var error = edit.Run(tabs[SelectedIndex] as IViewPort);
         if (!error.HasError) return;
         if (error.IsWarning) {
            InformationMessage = error.ErrorMessage;
         } else {
            ErrorMessage = error.ErrorMessage;
         }
      }

      private int searchByte = -1;
      public int SearchByte {
         get => searchByte;
         set => Set(ref searchByte, value);
      }

      private string findText;
      public string FindText {
         get => findText;
         set => Set(ref findText, value, FindTextChanged);
      }
      private void FindTextChanged(string oldValue) {
         if (findText.Length == 2 && int.TryParse(findText, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out var result)) {
            SearchByte = result;
         } else {
            SearchByte = -1;
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
               HexConverterVisible = false;
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
               HexConverterVisible = false;
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

      public bool IsNewVersionAvailable { get; set; }
      public DateTime LastUpdateCheck { get; set; }

      private bool showMatrix = true;
      public bool ShowMatrix { get => showMatrix; set => Set(ref showMatrix, value); }

      private bool animateScroll = true;
      public bool AnimateScroll { get => animateScroll; set => Set(ref animateScroll, value); }

      private bool autoAdjustDataWidth = true;
      public bool AutoAdjustDataWidth { get => autoAdjustDataWidth; set => Set(ref autoAdjustDataWidth, value); }

      private bool stretchData = true;
      public bool StretchData { get => stretchData; set => Set(ref stretchData, value); }

      private bool allowMultipleElementsPerLine = true;
      public bool AllowMultipleElementsPerLine { get => allowMultipleElementsPerLine; set => Set(ref allowMultipleElementsPerLine, value); }

      public Theme Theme { get; }

      private string infoMessage;
      public string InformationMessage {
         get => infoMessage;
         private set {
            if (TryUpdate(ref infoMessage, value)) ShowMessage = !string.IsNullOrEmpty(InformationMessage);
         }
      }

      public IToolTrayViewModel Tools => (SelectedTab as IViewPort)?.Tools;

      public IReadOnlyList<IQuickEditItem> QuickEdits { get; }

      public Singletons Singletons { get; }

      public event EventHandler<Action> RequestDelayedWork;

      public event EventHandler MoveFocusToFind;
      public event EventHandler MoveFocusToHexConverter;

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
                  if (selectedIndex >= 0 && selectedIndex < tabs.Count) tabs[selectedIndex].Refresh();
                  UpdateGotoViewModel();
                  foreach (var edit in QuickEdits) edit.TabChanged();
               }
            }
         }
      }

      private ITabContent SelectedTab => SelectedIndex < 0 ? null : tabs[SelectedIndex];

      public ObservableCollection<string> RecentFiles { get; }
      public ObservableCollection<RecentFileViewModel> RecentFileViewModels { get; } = new ObservableCollection<RecentFileViewModel>();

      #endregion

      public EditorViewModel(IFileSystem fileSystem, IWorkDispatcher workDispatcher = null, bool allowLoadingMetadata = true, IReadOnlyList<IQuickEditItem> utilities = null) {
         this.fileSystem = fileSystem;
         Singletons = new Singletons(workDispatcher);
         this.workDispatcher = workDispatcher ?? InstantDispatch.Instance;
         this.allowLoadingMetadata = allowLoadingMetadata;
         QuickEdits = utilities ?? new List<IQuickEditItem> {
            new MakeTutorsExpandable(),
            new MakeMovesExpandable(),
            new UpdateDexConversionTable(),
            new ReorderDex("National", HardcodeTablesModel.NationalDexTableName),
            new ReorderDex("Regional", HardcodeTablesModel.RegionalDexTableName),
            // new MakeTmsExpandable(),   // expanding TMs requires further research.
            // new MakeItemsExpandable(),
         }.Select(edit => new EditItemWrapper(edit)).ToList();

         tabs = new List<ITabContent>();
         selectedIndex = -1;

         ImplementCommands();

         copy = CreateWrapperForSelected(tab => tab.Copy);
         deepCopy = CreateWrapperForSelected(tab => tab.DeepCopy);
         delete = CreateWrapperForSelected(tab => tab.Clear);
         selectAll = CreateWrapperForSelected(tab => tab.SelectAll);
         save = CreateWrapperForSelected(tab => tab.Save);
         saveAs = CreateWrapperForSelected(tab => tab.SaveAs);
         exportBackup = CreateWrapperForSelected(tab => tab.ExportBackup);
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
            { tab => tab.DeepCopy, (sender, e) => deepCopy.CanExecuteChanged.Invoke(this, e) },
            { tab => tab.Clear, (sender, e) => delete.CanExecuteChanged.Invoke(this, e) },
            { tab => tab.SelectAll, (sender, e) => selectAll.CanExecuteChanged.Invoke(this,e) },
            { tab => tab.Save, (sender, e) => save.CanExecuteChanged.Invoke(this, e) },
            { tab => tab.SaveAs, (sender, e) => saveAs.CanExecuteChanged.Invoke(this, e) },
            { tab => tab.ExportBackup, (sender, e) => exportBackup.CanExecuteChanged.Invoke(this, e) },
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
         AnimateScroll = !metadata.Contains("AnimateScroll = False");
         AutoAdjustDataWidth = !metadata.Contains("AutoAdjustDataWidth = False");
         StretchData = !metadata.Contains("StretchData = False");
         IsNewVersionAvailable = metadata.Contains("IsNewVersionAvailable = True");
         AllowMultipleElementsPerLine = !metadata.Contains("AllowMultipleElementsPerLine = False");
         var lastUpdateCheckLine = metadata.FirstOrDefault(line => line.StartsWith("LastUpdateCheck = "));
         if (lastUpdateCheckLine != null && DateTime.TryParse(lastUpdateCheckLine.Split('=').Last().Trim(), out var lastUpdateCheck)) LastUpdateCheck = lastUpdateCheck;
         var zoomLine = metadata.FirstOrDefault(line => line.StartsWith("ZoomLevel ="));
         if (zoomLine != null && int.TryParse(zoomLine.Split('=').Last().Trim(), out var zoomLevel)) ZoomLevel = zoomLevel;

         var recentFilesLine = metadata.FirstOrDefault(line => line.StartsWith("RecentFiles = ["));
         if (recentFilesLine != null) {
            RecentFiles = ParseRecentFiles(recentFilesLine);
         } else {
            RecentFiles = new ObservableCollection<string>();
         }
         PopulateRecentFilesViewModel();
      }

      public static ICommand Wrap(IQuickEditItem quickEdit) => new StubCommand {
         CanExecute = arg => quickEdit.CanRun((IViewPort)arg),
         Execute = arg => quickEdit.Run((IViewPort)arg),
      };

      public void WriteAppLevelMetadata() {
         var metadata = new List<string> {
            "[GeneralSettings]",
            $"ShowMatrixGrid = {ShowMatrix}",
            $"ZoomLevel = {ZoomLevel}",
            $"AnimateScroll = {AnimateScroll}",
            $"AutoAdjustDataWidth = {AutoAdjustDataWidth}",
            $"StretchData = {StretchData}",
            $"AllowMultipleElementsPerLine = {AllowMultipleElementsPerLine}",
            $"IsNewVersionAvailable = {IsNewVersionAvailable}",
            $"LastUpdateCheck = {LastUpdateCheck}",
            SerializeRecentFiles(),
            string.Empty
         };
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
               UpdateRecentFiles(file.Name);
               string[] metadataText = new string[0];
               if (allowLoadingMetadata) {
                  metadataText = fileSystem.MetadataFor(file.Name) ?? new string[0];
               }
               var metadata = new StoredMetadata(metadataText);
               var model = new HardcodeTablesModel(Singletons, file.Contents, metadata);
               var viewPort = new ViewPort(file.Name, model, Singletons);
               if (metadata.IsEmpty) {
                  var createdMetadata = viewPort.Model.ExportMetadata(Singletons.MetadataInfo).Serialize();
                  fileSystem.SaveMetadata(file.Name, createdMetadata);
               }
               Add(viewPort);
            } catch (IOException ex) {
               ErrorMessage = ex.Message;
            }
         };

         ImplementFindCommands();

         showHexConverter.CanExecute = CanAlwaysExecute;
         showHexConverter.Execute = arg => HexConverterVisible = (bool)arg;

         hideSearchControls.CanExecute = CanAlwaysExecute;
         hideSearchControls.Execute = arg => {
            gotoViewModel.ControlVisible = false;
            FindControlVisible = false;
            HexConverterVisible = false;
            ShowError = false;
            ShowMessage = false;
         };

         clearError.CanExecute = arg => showError;
         clearError.Execute = arg => {
            ErrorMessage = string.Empty;
         };

         clearMessage.CanExecute = arg => showMessage;
         clearMessage.Execute = arg => InformationMessage = string.Empty;

         toggleMatrix.CanExecute = CanAlwaysExecute;
         toggleMatrix.Execute = arg => ShowMatrix = !ShowMatrix;

         toggleScrollAnimation.CanExecute = CanAlwaysExecute;
         toggleScrollAnimation.Execute = arg => AnimateScroll = !AnimateScroll;

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
            var copyText = fileSystem.CopyText;
            // if the paste is long, add whitespace to complete any pasted elements
            if (copyText.Contains(Environment.NewLine) || copyText.Contains(BaseRun.AnchorStart)) {
               copyText += " ";
               copyText = copyText.Replace(Environment.NewLine, "\n"); // normalize newline inputs
            }

            (SelectedTab as ViewPort)?.Edit(copyText, workDispatcher);
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
         AddContentListeners(content);
         CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, content));
         if (content is IViewPort viewModel) {
            viewModel.Model.InitializeComplete += (sender, e) => Singletons.WorkDispatcher.DispatchWork(()=> {
               viewModel.Refresh();
               gotoViewModel.RefreshOptions();
            });
            viewModel.UseCustomHeaders = useTableEntryHeaders;
            viewModel.AutoAdjustDataWidth = AutoAdjustDataWidth;
            viewModel.AllowMultipleElementsPerLine = AllowMultipleElementsPerLine;
            viewModel.StretchData = StretchData;
            if (Singletons.WorkDispatcher is InstantDispatch) {
               viewModel.ValidateMatchedWords();
            } else {
               viewModel.Model.InitializeComplete += (sender, e) => Singletons.WorkDispatcher.DispatchWork(viewModel.ValidateMatchedWords);
            }
            if (content is ViewPort viewPort) {
               bool anyTabsHaveMatchingViewModel = false;
               foreach (var tab in tabs) {
                  if (!(tab is ViewPort vp)) continue;
                  if (tab == viewPort) continue;
                  anyTabsHaveMatchingViewModel |= vp.Model == viewPort.Model;
               }
               if (!anyTabsHaveMatchingViewModel && viewPort.FileName.ToLower().EndsWith(".gba")) {
                  ErrorMessage = string.Empty;
                  InformationMessage = string.Empty;
                  gotoViewModel.ControlVisible = true;
               }
            }
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
         if (search == null) search = findText;
         var results = new List<(IViewPort, int start, int end)>();

         var searchedModels = new List<IDataModel>();
         foreach (var tab in tabs) {
            if (tab is IViewPort viewPort && searchedModels.All(model => model != viewPort.Model)) {
               results.AddRange(viewPort.Find(search).Select(offset => (viewPort, offset.start, offset.end)));
               searchedModels.Add(viewPort.Model);
            }
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
               if (selectedIndex == tabs.Count) SelectedIndex = tabs.Count - 1;
               CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, tab, index));
            }
            UpdateGotoViewModel();
            return;
         }

         // if the removed tab was left of the selected tab, we need to adjust the selected index based on the removal
         if (index < SelectedIndex) selectedIndex--;

         RemoveContentListeners(tab);
         tabs.Remove(tab);
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
            var args = (ExtendedPropertyChangedEventArgs<string>)e;
            var oldName = args.OldValue;

            if (!string.IsNullOrEmpty(oldName)) fileSystem.RemoveListenerForFile(oldName, viewPort.ConsiderReload);
            if (!string.IsNullOrEmpty(viewPort.FileName)) fileSystem.AddListenerToFile(viewPort.FileName, viewPort.ConsiderReload);
         }

         // when one tab's height updates, update other tabs by the same amount.
         // this isn't perfect, since tabs shouldn't nessisarily change height at the same pixel.
         // but it'll keep the tabs that are out of view from getting totally out of sync.
         if (sender is IViewPort viewPort2) {
            if (e.PropertyName == nameof(IViewPort.AutoAdjustDataWidth)) AutoAdjustDataWidth = viewPort2.AutoAdjustDataWidth;
            if (e.PropertyName == nameof(IViewPort.StretchData)) StretchData = viewPort2.StretchData;
            if (e.PropertyName == nameof(IViewPort.AllowMultipleElementsPerLine)) AllowMultipleElementsPerLine = viewPort2.AllowMultipleElementsPerLine;
            foreach (var tab in this) {
               if (tab == viewPort2) continue;
               if (!(tab is IViewPort viewPort3)) continue;
               var syncedProperties = new[] { nameof(IViewPort.Height), nameof(IViewPort.AutoAdjustDataWidth), nameof(StretchData), nameof(AllowMultipleElementsPerLine) };
               if (!e.PropertyName.IsAny(syncedProperties)) continue; // filter to only the properties wo care about so we don't Remove/Add Content Listeners unless we need to.
               RemoveContentListeners(tab);
               if (e.PropertyName == nameof(IViewPort.Height)) {
                  var args = (ExtendedPropertyChangedEventArgs<int>)e;
                  var oldHeight = args.OldValue;
                  viewPort3.Height += viewPort2.Height - oldHeight;
               } else if (e.PropertyName == nameof(IViewPort.AutoAdjustDataWidth)) {
                  viewPort3.AutoAdjustDataWidth = AutoAdjustDataWidth;
               } else if (e.PropertyName == nameof(IViewPort.StretchData)) {
                  viewPort3.StretchData = StretchData;
               } else if (e.PropertyName == nameof(IViewPort.AllowMultipleElementsPerLine)) {
                  viewPort3.AllowMultipleElementsPerLine = AllowMultipleElementsPerLine;
               }
               AddContentListeners(tab);
            }
         }
      }

      private void GotoPropertyChanged(object sender, PropertyChangedEventArgs e) {
         if (e.PropertyName == nameof(gotoViewModel.ControlVisible) && gotoViewModel.ControlVisible) {
            ClearError.Execute();
            ClearMessage.Execute();
            FindControlVisible = false;
            HexConverterVisible = false;
         }
      }

      private void AcceptError(object sender, string message) => ErrorMessage = message;

      private void AcceptMessage(object sender, string message) => InformationMessage = message;

      private void AcceptMessageClear(object sender, EventArgs e) => HideSearchControls.Execute();

      private ObservableCollection<string> ParseRecentFiles(string recentFilesLine) {
         var list = new ObservableCollection<string>();
         var content = recentFilesLine.Split('[')[1];
         content = content.Split(']')[0];

         foreach (var segment in content.Split(',')) {
            var element = segment.Trim(' ', '"', '\'');
            if (!fileSystem.Exists(element)) continue;
            list.Add(element);
         }

         return list;
      }

      private string SerializeRecentFiles() {
         var content = string.Join(", ", RecentFiles.Select(file => $"\"{file}\""));
         return $"RecentFiles = [{content}]";
      }

      private void UpdateRecentFiles(string filename) {
         var index = RecentFiles.IndexOf(filename);
         if (index == 0) return;
         if (index != -1) RecentFiles.RemoveAt(index);
         RecentFiles.Insert(0, filename);
         while (RecentFiles.Count > 5) RecentFiles.RemoveAt(RecentFiles.Count - 1);

         PopulateRecentFilesViewModel();
      }

      private void PopulateRecentFilesViewModel() {
         RecentFileViewModels.Clear();
         foreach (var file in RecentFiles) {
            var closureFile = file;
            RecentFileViewModels.Add(new RecentFileViewModel(file, new StubCommand {
               CanExecute = arg => true, Execute = arg => {
                  var loadedFile = fileSystem.LoadFile(closureFile);
                  Open.Execute(loadedFile);
               }
            }));
         }

         RecentFileViewModels.Add(new RecentFileViewModel(new StubCommand {
            CanExecute = arg => true,
            Execute = arg => Open.Execute()
         }));
      }

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
         if (selectedIndex < 0 || selectedIndex >= tabs.Count) return;
         var tab = tabs[selectedIndex];
         foreach (var kvp in forwardExecuteChangeNotifications) {
            var getCommand = kvp.Key;
            var notify = kvp.Value;
            var command = getCommand(tab);
            if (command != null) adjust(command, notify);
         }
      }

      bool withinNotListeningScope = false;
      private IDisposable WorkWithoutListeningToCommandsFromCurrentTab() {
         if (withinNotListeningScope) return new StubDisposable();
         void add(ICommand command, EventHandler notify) => command.CanExecuteChanged += notify;
         void remove(ICommand command, EventHandler notify) => command.CanExecuteChanged -= notify;
         withinNotListeningScope = true;
         AdjustNotificationsFromCurrentTab(remove);
         return new StubDisposable {
            Dispose = () => {
               commandsToRefreshOnTabChange.ForEach(command => command.CanExecuteChanged.Invoke(command, EventArgs.Empty));
               AdjustNotificationsFromCurrentTab(add);
               withinNotListeningScope = false;
            }
         };
      }

      private void RaiseSaveAllCanExecuteChanged(object sender, EventArgs e) => saveAll.CanExecuteChanged.Invoke(this, e);

      private void ExecuteCopyAlignedAddress(IFileSystem fileSystem) {
         if (!(SelectedTab is ViewPort currentTab)) {
            ErrorMessage = "Selected tab must be a ViewPort.";
            return;
         }

         var addresses = new List<string>();

         foreach (var tab in tabs) {
            if (!(tab is ViewPort viewPort)) continue;
            addresses.Add(viewPort.ConvertViewPointToAddress(currentTab.SelectionStart).ToString("X6"));
         }

         fileSystem.CopyText = string.Join(", ", addresses);
         InformationMessage = $"Copied {addresses.Count} addresses.";
      }

      private void ExecuteCopyAnchorReference(IFileSystem fileSystem) {
         if (!(SelectedTab is ViewPort viewport)) {
            ErrorMessage = "Selected tab must be a ViewPort.";
            return;
         }

         var address = viewport.ConvertViewPointToAddress(viewport.SelectionStart);
         var run = viewport.Model.GetNextRun(address);
         var anchor = viewport.Model.GetAnchorFromAddress(-1, address);
         if (string.IsNullOrEmpty(anchor) || (run.PointerSources?.Count ?? 0) == 0) {
            ErrorMessage = "Must select a named anchor with at least one pointer.";
            return;
         }

         var gameCode = viewport.Model.GetGameCode();
         var addresses = ", ".Join(Singletons.GameReferenceTables.GuessSources(gameCode, run.PointerSources[0]));

         var content = new StringBuilder();
         content.AppendLine("```");
         content.AppendLine($"{anchor}, {addresses}, {run.FormatString} // {gameCode}");
         content.AppendLine("```");
         fileSystem.CopyText = content.ToString();
         InformationMessage = $"Copied {anchor} reference.";
      }
   }

   /// <summary>
   /// Gives all QuickEditItem's an extra check at the end to make sure that they didn't leave the model in a bad state.
   /// </summary>
   public class EditItemWrapper : QuickEditItemDecorator {
      public EditItemWrapper(IQuickEditItem core) => InnerQuickEditItem = core;
      public override ErrorInfo Run(IViewPort viewPort) {
         var result = base.Run(viewPort);
         (viewPort.Model as PokemonModel)?.ResolveConflicts();
         return result;
      }
   }

   public class RecentFileViewModel : ViewModelCore {
      public string ShortName { get; }
      public string LongName { get; }
      public ICommand Open { get; }

      public RecentFileViewModel(ICommand open) {
         LongName = null;
         ShortName = "Open file...";
         Open = open;
      }

      public RecentFileViewModel(string filename, ICommand open) {
         LongName = filename;
         ShortName = Path.GetFileNameWithoutExtension(filename);
         Open = open;
      }
   }
}
