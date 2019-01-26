using HavenSoft.Gen3Hex.Core;
using HavenSoft.Gen3Hex.Core.Models;
using HavenSoft.Gen3Hex.Core.ViewModels;
using HavenSoft.Gen3Hex.Core.ViewModels.DataFormats;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace HavenSoft.Gen3Hex.Tests {
   public class ViewPortSaveTests {

      private readonly StubFileSystem fileSystem;
      private string name = string.Empty;

      public ViewPortSaveTests() {
         fileSystem = new StubFileSystem {
            RequestNewName = (previousName, description, extensions) => { name = $"file.txt"; return name; },
            TrySavePrompt = (loadedFile, md) => { name = loadedFile.Name; return true; },
         };
      }

      [Fact]
      public void SaveAsRequestsNewName() {
         var viewPort = new ViewPort();
         viewPort.SaveAs.Execute(fileSystem);

         Assert.Equal("file.txt", name);
      }

      [Fact]
      public void SaveRequestsNewNameIfFileIsNew() {
         var viewPort = new ViewPort();

         viewPort.Edit("01 23 45");
         viewPort.Save.Execute(fileSystem);

         Assert.Equal("file.txt", name);
      }

      [Fact]
      public void SaveDoesNotRequestNewNameIfFileIsNotNew() {
         var viewPort = new ViewPort(new LoadedFile("input.txt", new byte[0]));
         fileSystem.Save = (loadedFile, md) => { name = loadedFile.Name; return true; };

         viewPort.Edit("01 23 45");
         viewPort.Save.Execute(fileSystem);

         Assert.Equal("input.txt", name);
      }

      [Fact]
      public void SaveDoesNotCallFileSystemTrySavePromptIfNoChanges() {
         var viewPort1 = new ViewPort();
         var viewPort2 = new ViewPort(new LoadedFile("input1.txt", new byte[10]));
         var viewPort3 = new ViewPort(new LoadedFile("input2.txt", new byte[10]));

         Assert.False(viewPort1.Save.CanExecute(fileSystem));
         viewPort1.Save.Execute(fileSystem);

         Assert.False(viewPort2.Save.CanExecute(fileSystem));
         viewPort2.Save.Execute(fileSystem);

         viewPort3.Edit("01 23 45 67 89");
         viewPort3.Undo.Execute();
         Assert.False(viewPort3.Save.CanExecute(fileSystem));
         viewPort3.Save.Execute(fileSystem);

         Assert.Equal(string.Empty, name); // none should have called TrySave
      }

      [Fact]
      public void EditedFilePromptsForSaveOnExit() {
         bool triedToSave = false;
         var viewPort = new ViewPort();
         viewPort.Edit("ab cd ef");
         fileSystem.TrySavePrompt = (loadedFile, md) => { triedToSave = true; return true; };

         viewPort.Close.Execute(fileSystem);

         Assert.True(triedToSave);
      }

      [Fact]
      public void NonEditedFileDoesNotPromptForSaveOnExit() {
         bool triedToSave = false;
         var viewPort = new ViewPort(new LoadedFile("input.txt", new byte[10]));
         fileSystem.TrySavePrompt = (loadedFile, md) => { triedToSave = true; return true; };

         Assert.False(viewPort.Save.CanExecute(fileSystem));
         viewPort.Close.Execute(fileSystem);

         Assert.False(triedToSave);
      }

      [Fact]
      public void FileRaisesCloseEventIfClosedWithoutEdit() {
         int closed = 0;
         var viewPort = new ViewPort();
         viewPort.Closed += (sender, e) => closed++;

         viewPort.Close.Execute(fileSystem);

         Assert.Equal(1, closed);
      }

      [Fact]
      public void FileRaisesCloseEventIfSavedAfterEdit() {
         int closed = 0;
         var viewPort = new ViewPort();
         viewPort.Closed += (sender, e) => closed++;
         fileSystem.TrySavePrompt = (loadedFile, md) => true;

         viewPort.Edit("12");
         viewPort.Close.Execute(fileSystem);

         Assert.Equal(1, closed);
      }

      [Fact]
      public void FileRaisesCloseEventIfNotSavedAfterEdit() {
         int closed = 0;
         var viewPort = new ViewPort();
         viewPort.Closed += (sender, e) => closed++;
         fileSystem.TrySavePrompt = (loadedFile, md) => false;

         viewPort.Edit("12");
         viewPort.Close.Execute(fileSystem);

         Assert.Equal(1, closed);
      }

      [Fact]
      public void FileDoesNotRaiseCloseEventIfSaveCanceled() {
         int closed = 0;
         var viewPort = new ViewPort();
         viewPort.Closed += (sender, e) => closed++;
         fileSystem.TrySavePrompt = (loadedFile, md) => null;

         viewPort.Edit("12");
         viewPort.Close.Execute(fileSystem);

         Assert.Equal(0, closed);
      }

      [Fact]
      public void CallingSaveMultipleTimesOnlySavesOnce() {
         int count = 0;
         var viewPort = new ViewPort();
         fileSystem.Save = (loadedFile, md) => { count++; return true; };

         viewPort.Edit("00 01 02");
         viewPort.Save.Execute(fileSystem);
         viewPort.Save.Execute(fileSystem);

         Assert.Equal(1, count);
      }

      [Fact]
      public void SaveCanExecuteChangesOnEdit() {
         int canExecuteChangedCount = 0;
         var viewPort = new ViewPort(new LoadedFile("input.txt", new byte[20]));
         viewPort.Save.CanExecuteChanged += (sender, e) => canExecuteChangedCount++;

         viewPort.Edit("aa bb cc");

         Assert.Equal(1, canExecuteChangedCount);
      }

      [Fact]
      public void SaveCanExecuteChangesOnUndo() {
         int canExecuteChangedCount = 0;
         var viewPort = new ViewPort(new LoadedFile("input.txt", new byte[20]));
         viewPort.Save.CanExecuteChanged += (sender, e) => canExecuteChangedCount++;

         viewPort.Edit("aa bb cc");
         viewPort.Undo.Execute();

         Assert.Equal(2, canExecuteChangedCount);
      }

      [Fact]
      public void ViewPortNameDoesNotContainPathOrExension() {
         var viewPort = new ViewPort(new LoadedFile("path/to/myfile.txt", new byte[10]));

         Assert.Equal("myfile", viewPort.Name);
      }

      [Fact]
      public void ViewPortNameEndsWithStarIfNeedsSave() {
         var viewPort = new ViewPort(new LoadedFile("path/to/myfile.txt", new byte[10]));
         int nameChangedCount = 0;
         viewPort.PropertyChanged += (sender, e) => { if (e.PropertyName == nameof(viewPort.Name)) nameChangedCount++; };

         viewPort.Edit("12 34 56");

         Assert.EndsWith("*", viewPort.Name);
         Assert.Equal(1, nameChangedCount);
      }

      [Fact]
      public void ViewPortHasDefaultNameBeforeFirstSave() {
         var viewPort = new ViewPort();

         Assert.NotEqual(string.Empty, viewPort.Name);
         Assert.NotNull(viewPort.Name);
      }

      [Fact]
      public void EditDefaultFileStillShowsStar() {
         var viewPort = new ViewPort();
         var name = viewPort.Name;

         viewPort.Edit("11 22 33");

         Assert.Equal($"{name}*", viewPort.Name);
      }

      [Fact]
      public void ViewPortTakesNewNameOnSave() {
         var fileSystem = new StubFileSystem { RequestNewName = (originalName, description, extensions) => "path/to/newfile.txt", Save = (loadedFile, md) => true };
         var viewPort = new ViewPort();
         int nameChangedCount = 0;
         viewPort.PropertyChanged += (sender, e) => { if (e.PropertyName == nameof(viewPort.Name)) nameChangedCount++; };

         viewPort.Edit("012345");
         viewPort.Save.Execute(fileSystem);

         Assert.Equal("newfile", viewPort.Name);
         Assert.Equal(2, nameChangedCount);
      }

      [Fact]
      public void ViewPortRequestsDelayedReloadIfReloadFails() {
         var viewPort = new ViewPort(new LoadedFile("file.txt", new byte[50]));
         var fileSystem = new StubFileSystem { LoadFile = fileName => throw new IOException() };

         var retryCount = 0;
         viewPort.RequestDelayedWork += (sender, e) => retryCount++;

         viewPort.ConsiderReload(fileSystem);

         Assert.Equal(1, retryCount);
      }

      [Fact]
      public void ViewPortAdjustsSelectionWhenLoadingAShorterFile() {
         var viewPort = new ViewPort(new LoadedFile("file.txt", new byte[12]));
         viewPort.SelectionStart = new Point(3, 3);
         Assert.Equal(4, viewPort.Width);
         Assert.Equal(4, viewPort.Height);
         Assert.Equal(new Point(0, 3), viewPort.SelectionStart);

         var fileSystem = new StubFileSystem { LoadFile = filename => new LoadedFile("file.txt", new byte[10]) };
         viewPort.ConsiderReload(fileSystem);

         Assert.Equal(new Point(2, 2), viewPort.SelectionStart);
      }

      [Fact]
      public void ViewPortNotifiesOnFileNameChange() {
         var properties = new List<string>();

         var fileSystem = new StubFileSystem {
            RequestNewName = (currentName, description, extensionOptions) => "file.txt",
            Save = (file, md) => true,
         };
         var viewPort = new ViewPort();
         viewPort.PropertyChanged += (sender, e) => properties.Add(e.PropertyName);

         viewPort.Edit("01 23 45 67");
         viewPort.SaveAs.Execute(fileSystem);

         Assert.Contains("FileName", properties);
         Assert.Equal("file.txt", viewPort.FileName);
      }

      [Fact]
      public void EditorUpdatesFileSystemWatchesWhenViewPortFileNameChanges() {
         var fileSystem = new StubFileSystem();
         int addCalls = 0, removeCalls = 0;
         fileSystem.AddListenerToFile = (fileName, action) => addCalls++;
         fileSystem.RemoveListenerForFile = (fileName, action) => removeCalls++;
         var editor = new EditorViewModel(fileSystem);
         var tab = new StubViewPort();

         editor.Add(tab);
         Assert.Equal(0, addCalls);
         Assert.Equal(0, removeCalls);

         tab.FileName = "file.txt";
         tab.PropertyChanged.Invoke(tab, new ExtendedPropertyChangedEventArgs(null, nameof(tab.FileName)));
         Assert.Equal(1, addCalls);
         Assert.Equal(0, removeCalls);

         tab.FileName = "file2.txt";
         tab.PropertyChanged.Invoke(tab, new ExtendedPropertyChangedEventArgs("file.txt", nameof(tab.FileName)));
         Assert.Equal(2, addCalls);
         Assert.Equal(1, removeCalls);
      }

      [Fact]
      public void CanSaveAndLoadNamesAndFormats() {
         var buffer = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         var model = new PointerAndStringModel(buffer);
         var viewPort = new ViewPort(new LoadedFile("test.txt", buffer), model) { Width = 0x10, Height = 0x10 };
         StoredMetadata metadata = null;
         var fileSystem = new StubFileSystem { Save = (file, md) => { metadata = md; return true; } };

         viewPort.Edit("^bob\"\" \"Hello\"");
         viewPort.Save.Execute(fileSystem);

         var model2 = new PointerAndStringModel(buffer, metadata);
         var viewPort2 = new ViewPort(new LoadedFile("test.txt", buffer), model2) { Width = 0x10, Height = 0x10 };

         Assert.Equal("bob", ((Anchor)viewPort2[0, 0].Format).Name);
      }
   }
}
