using HavenSoft.Gen3Hex.Model;
using HavenSoft.Gen3Hex.ViewModel;
using System.Linq;
using System.Windows.Input;
using Xunit;

namespace HavenSoft.HexTests {
   public class GeneralAppTests {
      private readonly EditorViewModel editor;

      public GeneralAppTests() {
         editor = new EditorViewModel(new StubFileSystem());
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
         Assert.Null(editor.Undo);
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

      // TODO write test for closing current tab
   }
}
