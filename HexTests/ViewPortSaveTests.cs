using HavenSoft.Gen3Hex.Model;
using HavenSoft.Gen3Hex.ViewModel;
using Xunit;

namespace HavenSoft.HexTests {
   public class ViewPortSaveTests {

      private readonly string name = string.Empty;
      private readonly StubFileSystem fileSystem;

      public ViewPortSaveTests() {
         var fileSystem = new StubFileSystem {
            RequestNewName = extension => $"file.txt",
            WriteFile = (LoadedFile loadedFile) => name = loadedFile.Name,
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
         viewPort.Save.Execute(fileSystem);

         Assert.Equal("file.txt", name);
      }

      [Fact]
      public void SaveDoesNotRequestNewNameIfFileIsNotNew() {
         var viewPort = new ViewPort(new LoadedFile("input.txt", new byte[0]));
         viewPort.Save.Execute(fileSystem);

         Assert.Equal("input.txt", name);
      }

      [Fact]
      public void SaveDoesNotWriteFileIfNoChanges() {
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

         Assert.Equal(string.Empty, name); // none should have called WriteFile
      }

      [Fact]
      public void EditedFilePromptsForSaveOnExit() {
         bool triedToSave = false;
         var viewPort = new ViewPort();
         viewPort.Edit("ab cd ef");
         fileSystem.TrySave = loadedFile => { triedToSave = true; return true; };

         viewPort.Close.Execute(fileSystem);

         Assert.True(triedToSave);
      }

      [Fact]
      public void NonEditedFileDoesNotPromptForSaveOnExit() {
         bool triedToSave = false;
         var viewPort = new ViewPort(new LoadedFile("input.txt", new byte[10]));
         fileSystem.TrySave = loadedFile => { triedToSave = true; return true; };

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
         fileSystem.TrySave = loadedFile => true;

         viewPort.Edit("12");
         viewPort.Close.Execute(fileSystem);

         Assert.Equal(1, closed);
      }

      [Fact]
      public void FileRaisesCloseEventIfNotSavedAfterEdit() {
         int closed = 0;
         var viewPort = new ViewPort();
         viewPort.Closed += (sender, e) => closed++;
         fileSystem.TrySave = loadedFile => false;

         viewPort.Edit("12");
         viewPort.Close.Execute(fileSystem);

         Assert.Equal(1, closed);
      }

      [Fact]
      public void FileDoesNotRaiseCloseEventIfSaveCanceled() {
         int closed = 0;
         var viewPort = new ViewPort();
         viewPort.Closed += (sender, e) => closed++;
         fileSystem.TrySave = loadedFile => null;

         viewPort.Edit("12");
         viewPort.Close.Execute(fileSystem);

         Assert.Equal(0, closed);
      }

      [Fact]
      public void CallingSaveMultipleTimesOnlySavesOnce() {
         int count = 0;
         var viewPort = new ViewPort();
         fileSystem.WriteFile = loadedFile => count++;

         viewPort.Edit("00 01 02");
         viewPort.Save.Execute(fileSystem);
         viewPort.Save.Execute(fileSystem);

         Assert.Equal(1, count);
      }

      [Fact]
      public void SaveCanExecuteChangesOnEdit() {
         int canExecuteChangedCount = 0;
         var viewPort = new ViewPort(new LoadedFile("input.txt", new byte[20]));
         viewPort.Save.CanExecuteChanged += canExecuteChangedCount++;

         viewPort.Edit("aa bb cc");

         Assert.Equal(1, canExecuteChangedCount);
      }

      [Fact]
      public void SaveCanExecuteChangesOnUndo() {
         int canExecuteChangedCount = 0;
         var viewPort = new ViewPort(new LoadedFile("input.txt", new byte[20]));
         viewPort.Save.CanExecuteChanged += canExecuteChangedCount++;

         viewPort.Edit("aa bb cc");
         viewPort.Undo.Execute();

         Assert.Equal(2, canExecuteChangedCount);
      }
   }
}
