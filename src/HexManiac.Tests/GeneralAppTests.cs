using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.QuickEditItems;
using HavenSoft.HexManiac.Core.ViewModels.Visitors;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class GeneralAppTests {
      private readonly EditorViewModel editor;
      private readonly StubFileSystem fileSystem;

      public GeneralAppTests() {
         fileSystem = new StubFileSystem();
         editor = new EditorViewModel(fileSystem);
      }

      [Fact]
      public void ApplicationOpensWithNoTabs() {
         Assert.Equal(0, editor.Count);
         Assert.Equal(-1, editor.SelectedIndex);
      }

      [Fact]
      public void CanAddTabs() {
         var viewPort = new ViewPort();
         editor.Add(viewPort);
         Assert.Equal(1, editor.Count);
         Assert.Equal(0, editor.SelectedIndex);
      }

      [Fact]
      public void CanReorderTabs() {
         var viewPort1 = new ViewPort(new LoadedFile("file1", new byte[0]));
         var viewPort2 = new ViewPort(new LoadedFile("file2", new byte[0]));
         editor.Add(viewPort1);
         editor.Add(viewPort2);
         Assert.Equal(2, editor.Count);
         Assert.Equal(1, editor.SelectedIndex);

         editor.SwapTabs(0, 1);

         Assert.Equal("file2", editor[0].Name);
         Assert.Equal("file1", editor[1].Name);
         Assert.Equal(0, editor.SelectedIndex);
      }

      [Fact]
      public void UndoRunsForCurrentTab() {
         int executeCounter = 0;
         editor.Add(new StubTabContent());
         editor.Add(new StubTabContent {
            Undo = new StubCommand {
               CanExecute = arg => true,
               Execute = arg => executeCounter++
            },
         });

         editor.Undo.Execute();
         Assert.Equal(1, executeCounter);

         editor.SelectedIndex = 0;
         Assert.False(editor.Undo.CanExecute(null));
      }

      [Fact]
      public void SaveRunsForCurrentTab() {
         int executeCount = 0;
         editor.Add(new StubTabContent {
            Save = new StubCommand {
               CanExecute = arg => true,
               Execute = arg => executeCount += 1,
            },
         });
         editor.Add(new StubTabContent {
            Save = new StubCommand {
               CanExecute = arg => true,
               Execute = arg => executeCount += 10,
            },
         });

         editor.Save.Execute();
         Assert.Equal(10, executeCount);

         executeCount = 0;
         editor.SaveAll.Execute();
         Assert.Equal(11, executeCount);
      }

      [Fact]
      public void SaveAsRunsForCurrentTab() {
         int executeCount = 0;
         editor.Add(new StubTabContent {
            SaveAs = new StubCommand {
               CanExecute = arg => true,
               Execute = arg => executeCount += 1,
            },
         });
         editor.Add(new StubTabContent {
            SaveAs = new StubCommand {
               CanExecute = arg => true,
               Execute = arg => executeCount += 10,
            },
         });

         editor.SaveAs.Execute();
         Assert.Equal(10, executeCount);
      }

      [Fact]
      public void SaveAllSavesAllDocumentsThatNeedSaving() {
         int executeCount = 0;

         var save = new StubCommand { CanExecute = arg => true, Execute = arg => executeCount++ };
         var noSave = new StubCommand { CanExecute = arg => false, Execute = arg => executeCount++ };
         editor.Add(new StubTabContent { Save = save });
         editor.Add(new StubTabContent { Save = noSave });
         editor.Add(new StubTabContent { Save = save });

         editor.SaveAll.Execute();
         Assert.Equal(2, executeCount);
      }

      [Fact]
      public void ClosingCurrentTabSelectsAnotherTab() {
         editor.Add(CreateClosableTab());
         editor.Add(CreateClosableTab());
         editor.Add(CreateClosableTab());

         editor.Close.Execute();

         Assert.Equal(1, editor.SelectedIndex);
      }

      [Fact]
      public void ClosingAllTabsWorks() {
         editor.Add(CreateClosableTab());
         editor.Add(CreateClosableTab());
         editor.Add(CreateClosableTab());

         editor.CloseAll.Execute();

         Assert.Equal(-1, editor.SelectedIndex);
         Assert.Equal(0, editor.Count);
      }

      [Fact]
      public void NewAddsATab() {
         editor.New.Execute();

         Assert.Equal(1, editor.Count);
      }

      [Fact]
      public void OpenCanAddTab() {
         fileSystem.OpenFile = (description, extensions) => new LoadedFile("chosenFile.txt", new byte[0x200]);

         editor.Open.Execute();

         Assert.Equal(1, editor.Count);
      }

      [Fact]
      public void OpenDoesNotAddTabIfUserCancels() {
         fileSystem.OpenFile = (description, extensions) => null;

         editor.Open.Execute();

         Assert.Equal(0, editor.Count);
      }

      [Fact]
      public void SaveCommandsPassInFileSystemAsParameter() {
         int count = 0;
         void checkIfArgIsFileSystem(object arg) { if (arg is IFileSystem) count++; }
         bool canExecuteWrapper(object arg) { checkIfArgIsFileSystem(arg); return true; }
         var save = new StubCommand { CanExecute = canExecuteWrapper, Execute = checkIfArgIsFileSystem };

         editor.Add(new StubTabContent { Save = save, SaveAs = save });
         editor.Add(new StubTabContent { Save = save, SaveAs = save });
         editor.Add(new StubTabContent { Save = save, SaveAs = save });

         editor.Save.Execute(); // once
         editor.SaveAs.Execute(); // once
         editor.SaveAll.Execute(); // 6 times, since SaveAll should also check CanExecute

         Assert.Equal(8, count);
      }

      [Fact]
      public void CloseCommandsPassInFileSystemAsParameter() {
         int count = 0;
         void checkIfArgIsFileSystem(object arg) { if (arg is IFileSystem) count++; }
         bool canExecuteWrapper(object arg) { checkIfArgIsFileSystem(arg); return true; }
         var close = new StubCommand { CanExecute = canExecuteWrapper, Execute = checkIfArgIsFileSystem };

         editor.Add(new StubTabContent { Close = close });
         editor.Add(new StubTabContent { Close = close });
         editor.Add(new StubTabContent { Close = close });

         editor.Close.Execute(); // once
         editor.CloseAll.Execute(); // 6 times, since SaveAll should also check CanExecute

         Assert.Equal(7, count);
      }

      [Theory]
      [InlineData(nameof(EditorViewModel.Copy))]
      [InlineData(nameof(EditorViewModel.Delete))]
      [InlineData(nameof(EditorViewModel.Save))]
      [InlineData(nameof(EditorViewModel.SaveAs))]
      [InlineData(nameof(EditorViewModel.Close))]
      [InlineData(nameof(EditorViewModel.Undo))]
      [InlineData(nameof(EditorViewModel.Redo))]
      [InlineData(nameof(EditorViewModel.Back))]
      [InlineData(nameof(EditorViewModel.Forward))]
      public void EditorNotifiesCanExecuteChangedOnTabChange(string commandName) {
         int count = 0;
         var command = (ICommand)editor.GetType().GetProperty(commandName).GetValue(editor);
         command.CanExecuteChanged += (sender, e) => count++;
         var tab = new StubTabContent();
         tab.Close = new StubCommand { CanExecute = arg => true, Execute = arg => tab.Closed.Invoke(tab, EventArgs.Empty) };

         editor.Add(tab);
         Assert.Equal(1, count);

         count = 0;
         editor.Close.Execute();
         Assert.Equal(1, count);
      }

      [Fact]
      public void EditorNotifiesWhenUndoCanExecuteChange() {
         int count = 0;
         var undo = new StubCommand();
         var tab = new StubTabContent { Undo = undo };
         editor.Add(tab);
         editor.Undo.CanExecuteChanged += (sender, e) => count++;

         undo.CanExecute = arg => true;
         undo.CanExecuteChanged.Invoke(undo, EventArgs.Empty);

         Assert.Equal(1, count);
      }

      [Fact]
      public void SaveAllChangesWhenCurrentFileChanges() {
         int count = 0;
         var save = new StubCommand();
         var tab = new StubTabContent { Save = save };
         editor.Add(tab);
         editor.SaveAll.CanExecuteChanged += (sender, e) => count++;

         save.CanExecute = arg => true;
         save.CanExecuteChanged.Invoke(save, EventArgs.Empty);

         Assert.Equal(1, count);
      }

      [Fact]
      public void EditorShowsErrors() {
         var tab = new StubTabContent();
         editor.Add(tab);
         var clearErrorChangedNotifications = 0;
         editor.ClearError.CanExecuteChanged += (sender, e) => clearErrorChangedNotifications += 1;

         tab.OnError.Invoke(tab, "Some Message");

         Assert.True(editor.ShowError);
         Assert.Equal("Some Message", editor.ErrorMessage);
         Assert.Equal(1, clearErrorChangedNotifications);
      }

      [Fact]
      public void EditorCanClearErrors() {
         var tab = new StubTabContent();
         editor.Add(tab);
         var clearErrorChangedNotifications = 0;
         editor.ClearError.CanExecuteChanged += (sender, e) => clearErrorChangedNotifications += 1;

         tab.OnError.Invoke(tab, "Some Message");
         editor.ClearError.Execute();

         Assert.False(editor.ShowError);
         Assert.Equal(editor.ErrorMessage, string.Empty);
         Assert.Equal(2, clearErrorChangedNotifications);
      }

      [Fact]
      public void ShowingGotoClearsErrors() {
         var tab = new StubTabContent();
         editor.Add(tab);

         tab.OnError.Invoke(tab, "Some Message");
         editor.GotoViewModel.ShowGoto.Execute(true);

         Assert.True(editor.GotoViewModel.ControlVisible);
         Assert.False(editor.ShowError);
      }

      [Fact]
      public void ActiveTabCanTellEditorToSwitch() {
         var tab0 = new StubTabContent();
         var tab1 = new StubTabContent();
         editor.Add(tab0);
         editor.Add(tab1);

         tab1.RequestTabChange.Invoke(tab1, tab0); // tab 1 is requesting that the editor switch to tab 0

         Assert.Equal(0, editor.SelectedIndex);
      }

      [Fact]
      public void NonActiveTabSwitchesAreIgnored() {
         var tab0 = new StubTabContent();
         var tab1 = new StubTabContent();
         editor.Add(tab0);
         editor.Add(tab1);

         tab1.RequestTabChange.Invoke(tab0, tab0); // tab 0 is trying to force itself to be focused

         Assert.Equal(1, editor.SelectedIndex);
      }

      [Fact]
      public void EditorAddsOpenedFilesToFileSystemWatch() {
         var fileSystem = new StubFileSystem();
         string name = null;
         fileSystem.AddListenerToFile = (fileName, action) => name = fileName;
         var editor = new EditorViewModel(fileSystem);

         editor.Open.Execute(new LoadedFile("InputFile.txt", new byte[0x200]));

         Assert.Equal("InputFile.txt", name);
      }

      [Fact]
      public void EditorRemovesFileSystemWatchWhenTabsClose() {
         var fileSystem = new StubFileSystem();
         string name = null;
         fileSystem.RemoveListenerForFile = (fileName, listener) => name = fileName;
         var editor = new EditorViewModel(fileSystem);
         editor.Open.Execute(new LoadedFile("InputFile.txt", new byte[0x200]));

         editor.Close.Execute();

         Assert.Equal("InputFile.txt", name);
      }

      [Fact]
      public void ViewPortReloadsIfNoLocalChangesWhenFileChanges() {
         var fileSystem = new StubFileSystem();
         string file = null;
         fileSystem.LoadFile = input => { file = input; return new LoadedFile(input, new byte[] { 0x10, 0x20 }); };
         var viewPort = new ViewPort(new LoadedFile("file.txt", new byte[] { 0x00, 0x00 }));

         viewPort.ConsiderReload(fileSystem);

         Assert.Equal("file.txt", file);
         Assert.Equal(0x10, viewPort[0, 0].Value);
      }

      [Fact]
      public void ViewPortDoesNotReloadIfLocalChangesWhenFileChanges() {
         var fileSystem = new StubFileSystem();
         string file = null;
         fileSystem.LoadFile = input => { file = input; return new LoadedFile(input, new byte[] { 0x10, 0x20 }); };
         var viewPort = new ViewPort(new LoadedFile("file.txt", new byte[] { 0x00, 0x00 }));

         viewPort.Edit("05");
         viewPort.ConsiderReload(fileSystem);

         Assert.Null(file);
         Assert.Equal(0x05, viewPort[0, 0].Value);
      }

      [Fact]
      public void EditorForwardsTabDelayedWork() {
         void SomeAction() { }
         Action work = null;
         var editor = new EditorViewModel(new StubFileSystem());
         var tab = new StubTabContent();
         editor.Add(tab);

         editor.RequestDelayedWork += (sender, e) => work = e;
         tab.RequestDelayedWork.Invoke(tab, SomeAction);

         Assert.Equal(SomeAction, work);
      }

      /// <summary>
      /// The first time we open any given file, we have to parse the file
      /// to see what data it contains. This process takes time.
      /// This test shows that we save that metadata immediately, even if the user doesn't
      /// save the file. This makes the second load much faster, even if the user never saved any changes.
      /// </summary>
      [Fact]
      public void FirstFileOpenAutomaticallySavesItsMetadata() {
         var fileSystem = new StubFileSystem();
         var editor = new EditorViewModel(fileSystem);
         var saveCount = 0;
         fileSystem.SaveMetadata = (fileName, data) => { saveCount += 1; return true; };

         fileSystem.OpenFile = (type, exensions) => new LoadedFile("file.gba", new byte[0x200]);
         editor.Open.Execute();

         Assert.Equal(1, saveCount);
      }

      [Fact]
      public void ZoomCanReset() {
         var fileSystem = new StubFileSystem();
         var editor = new EditorViewModel(fileSystem) {
            ZoomLevel = 24
         };

         editor.ResetZoom.Execute();

         Assert.Equal(16, editor.ZoomLevel);
      }

      [Fact]
      public void ThemeCanReset() {
         var editor = new EditorViewModel(new StubFileSystem());
         var defaultValue = editor.Theme.AccentValue;

         editor.Theme.AccentValue += 1;
         Assert.Equal(defaultValue + 1, editor.Theme.AccentValue);

         editor.ResetTheme.Execute();
         Assert.Equal(defaultValue, editor.Theme.AccentValue);
      }

      [Fact]
      public void CanOpenSecondTabWithSameModel() {
         var test = new BaseViewModelTestClass();
         editor.Add(test.ViewPort);
         test.ViewPort.Edit("<000100> @00 "); // create a pointer for us to follow

         var group = (ContextItemGroup)test.ViewPort.GetContextMenuItems(new Point()).Single(menuItem => menuItem.Text == "Pointer Operations");
         var item = group.Single(menuItem => menuItem.Text == "Open in New Tab");
         item.Command.Execute();

         var newTab = (ViewPort)editor[1];
         Assert.Equal(test.ViewPort.CurrentChange, newTab.CurrentChange); // they have the same change history
         Assert.Equal(test.ViewPort.Model, newTab.Model);                 // they have the same model
         Assert.Equal(0x100, newTab.DataOffset);                          // the new tab is scrolled to the desired location
      }

      [Fact]
      public void TabGetsRefreshedWhenSwitchedIn() {
         int count = 0;
         var tab1 = new StubTabContent { Refresh = () => count++ };
         var tab2 = new StubTabContent();
         editor.Add(tab1);
         count = 0;

         tab1.RequestTabChange?.Invoke(tab1, tab2);
         editor.SelectedIndex = 0;

         Assert.Equal(1, count);
      }

      [Fact]
      public void CanLoadVersionFromToml() {
         var file = @"
[General]
ApplicationVersion = '''0.1.0'''
";
         var metadata = new StoredMetadata(file.Split(Environment.NewLine));
         Assert.Equal("0.1.0", metadata.Version);
      }

      [Fact]
      public void CanSaveVersionToToml() {
         var metadata = new StoredMetadata(null, null, null, null, null, null, new StubMetadataInfo { VersionNumber = "0.1.0" }, default, default, default);
         var lines = metadata.Serialize();
         Assert.Contains("[General]", lines);
         Assert.Contains("ApplicationVersion = '''0.1.0'''", lines);
      }

      [Fact]
      public void NullVersionIsNotSavedToToml() {
         var metadata = new StoredMetadata(null, null, null, null, null, null, new StubMetadataInfo(), default, default, default);
         var lines = metadata.Serialize();
         Assert.All(lines, line => Assert.DoesNotContain("ApplicationVersion = '''", line));
      }

      [Fact]
      public void ClosingTabUpdatesQuickEdits() {
         int tabChangedCalls = 0;
         var qItem = new StubQuickEditItem { TabChanged = () => tabChangedCalls += 1 };
         var editor = new EditorViewModel(new StubFileSystem(), utilities: new[] { qItem });
         var tabToClose = new StubTabContent();
         editor.Add(new StubTabContent());
         editor.Add(tabToClose);

         tabChangedCalls = 0;
         tabToClose.Closed.Invoke(tabToClose, EventArgs.Empty);

         Assert.Equal(1, tabChangedCalls);
      }

      /// <summary>
      /// This test warns me when an anchor name starts with another anchor name
      /// </summary>
      [Fact]
      public void TableReference_Load_NoNamesPrefixOtherNames() {
         var tableNames = BaseViewModelTestClass.Singletons.GameReferenceTables.Values.SelectMany(tables => tables.Select(table => table.Name.ToLower()))
            .Concat(BaseViewModelTestClass.Singletons.GameReferenceConstants.Values.SelectMany(constants => constants.Select(constant => constant.Name.ToLower())))
            .Distinct().ToArray();
         for (int i = 0; i < tableNames.Length; i++) {
            Assert.All(tableNames.Length.Range(), j => {
               if (i == j) return;
               Assert.False(tableNames[i].StartsWith(tableNames[j] + "."), $"{tableNames[i]} contains name {tableNames[j]}!");
            });
         }
      }

      [Fact]
      public void NewTab_Backspace_DoNothing() {
         editor.New.Execute();

         var viewPort = (ViewPort)editor[0];
         for (int i = 0; i < 5; i++)
            viewPort.Edit(ConsoleKey.Backspace);

         // no asserts: if it doesn't crash, we pass
      }

      [Fact]
      public void Tab_Edit_CanRunChanged() {
         var test = new BaseViewModelTestClass();
         editor.Add(test.ViewPort);
         Assert.True(editor.RunFile.CanExecute(default));

         var view = new StubView(editor);
         test.ViewPort.Edit("aa ");

         Assert.Contains(nameof(editor.RunFile), view.CommandCanExecuteChangedNotifications);
         Assert.False(editor.RunFile.CanExecute(default));
      }

      [Fact]
      public void Tab_Duplicate_Duplicated() {
         var tab = new StubTabContent { CanDuplicate = true };
         tab.Duplicate = () => tab.RequestTabChange.Invoke(tab, new StubTabContent());

         editor.Add(tab);
         editor.DuplicateCurrentTab.Execute();

         Assert.Equal(2, editor.Count);
         Assert.True(editor.GotoViewModel.ControlVisible);
      }

      [Fact]
      public void TabWithoutDuplicate_SwitchToTabWithDuplicate_Notify() {
         editor.Add(new StubTabContent { CanDuplicate = false });
         editor.Add(new StubTabContent { CanDuplicate = true });
         editor.SelectedIndex = 0;
         var view = new StubView(editor);

         editor.SelectedIndex = 1;

         Assert.Contains(nameof(editor.DuplicateCurrentTab), view.CommandCanExecuteChangedNotifications);
      }

      [Fact]
      public void CleanTab_MetadataOnlyEdit_EditorPropertiesMatch() {
         var tab = new StubTabContent();
         var save = new StubCommand();
         tab.Save = save;
         editor.Add(tab);
         var view = new StubView(editor);

         tab.IsMetadataOnlyChange = true;
         save.CanExecute = arg => true;
         tab.PropertyChanged.Invoke(tab, new System.ComponentModel.PropertyChangedEventArgs(nameof(tab.IsMetadataOnlyChange)));
         save.CanExecuteChanged.Invoke(save, EventArgs.Empty);

         Assert.True(editor.IsMetadataOnlyChange);
         Assert.Contains(nameof(editor.IsMetadataOnlyChange), view.PropertyNotifications);
      }

      [Fact]
      public void Editor_SwapTabs_GotoHasRightShortcuts() {
         var singletons = BaseViewModelTestClass.Singletons;
         var viewPort = new ViewPort("file.gba", new PokemonModel(new byte[0x200], null, singletons), InstantDispatch.Instance, singletons);
         var shortcuts = new[] { new StoredGotoShortcut("name", "image", "destination") };
         viewPort.Model.LoadMetadata(new StoredMetadata(default, default, default, default, default, default, shortcuts, singletons.MetadataInfo, default));
         viewPort.Edit("^destination ");

         editor.Add(viewPort);
         editor.Add(new StubTabContent());
         editor.SelectedIndex = 0;

         Assert.False(editor.GotoViewModel.Loading);
         Assert.Single(editor.GotoViewModel.Shortcuts);
      }

      [Fact]
      public void TabWithMetadataChange_SwitchTabs_SaveIconUpdates() {
         editor.Add(new StubTabContent { IsMetadataOnlyChange = true });
         editor.Add(new StubTabContent { IsMetadataOnlyChange = false });
         editor.SelectedIndex = 0;
         var view = new StubView(editor);

         editor.SelectedIndex = 1;

         Assert.False(editor.IsMetadataOnlyChange);
         Assert.Contains(nameof(editor.IsMetadataOnlyChange), view.PropertyNotifications);
      }

      [Fact]
      public void Editor_OpenFile_NotifyRecentFileMenuEnabled() {
         var view = new StubView(editor);

         editor.Open.Execute(new LoadedFile("TestRom.gba", new byte[0x200]));

         Assert.Contains(nameof(editor.RecentFileMenuEnabled), view.PropertyNotifications);
      }

      [Fact]
      public void Editor_New_PokemonModel() {
         editor.New.Execute();
         var viewPort = (IViewPort)editor.SelectedTab;
         Assert.IsType<PokemonModel>(viewPort.Model);
      }

      [Fact]
      public void Tab_SaveAs_UpdatesRecentFiles() {
         var filename = "Test.gba";

         editor.Add(new StubViewPort {
            FileName = filename,
            SaveAs = new StubCommand {
               CanExecute = arg => true,
            },
         });

         editor.SaveAs.Execute();
         Assert.Contains(filename, editor.RecentFiles);
      }

      private StubTabContent CreateClosableTab() {
         var tab = new StubTabContent();
         var close = new StubCommand { CanExecute = arg => true };
         close.Execute = arg => tab.Closed.Invoke(tab, EventArgs.Empty);
         tab.Close = close;
         return tab;
      }
   }
}
