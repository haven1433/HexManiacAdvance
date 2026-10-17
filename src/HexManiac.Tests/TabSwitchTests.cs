using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// Selecting a tab is cheap when nothing was edited since the tab last looked (ModelEditStamp), and exactly as complete as before when something was.
   /// The table used here has two 2-byte fields and four elements, so element 1 starts at address 4 and element 2 at address 8.
   /// </summary>
   [Collection("TabSwitch")]
   public class TabSwitchTests : BaseViewModelTestClass {
      private const string TableFormat = "^table[a: b:]4 ";
      private readonly EditorViewModel editor;

      public TabSwitchTests() {
         editor = New.EditorViewModel();
      }

      private static ViewPort CreateOtherFile(string name = "other.gba") => new ViewPort(name, new PokemonModel(new byte[0x200], null, Singletons), InstantDispatch.Instance, Singletons) { AllowMultipleElementsPerLine = true, Width = 0x10, Height = 0x10 };

      /// <summary>Another tab on the same file. It shares the undo history of the original, and it sits at the same place.</summary>
      private static ViewPort CreateDuplicate(ViewPort viewPort) {
         var duplicate = viewPort.CreateDuplicate();
         (duplicate.AllowMultipleElementsPerLine, duplicate.Width, duplicate.Height) = (true, 0x10, 0x10);
         return duplicate;
      }

      private static FieldArrayElementViewModel FirstField(ViewPort viewPort) => viewPort.Tools.TableTool.Children.OfType<FieldArrayElementViewModel>().First();

      /// <summary>
      /// How many times the content of the table tool was built.
      /// (The view models are reused when a build finds the same fields, so comparing them can't tell a build from no build.)
      /// </summary>
      private class BuildCounter {
         public int Count { get; private set; }
         public BuildCounter(ViewPort viewPort) => viewPort.Tools.TableTool.PropertyChanged += (sender, e) => { if (e.PropertyName == nameof(TableTool.Children)) Count++; };
      }

      /// <summary>Open the tabs and visit every one of them twice, so every tab has caught up with every edit that opening the tabs made.</summary>
      private void AddAndVisitAll(params ITabContent[] tabs) {
         foreach (var tab in tabs) editor.Add(tab);
         for (int i = 0; i < tabs.Length; i++) editor.SelectedIndex = i;
         for (int i = tabs.Length - 1; i >= 0; i--) editor.SelectedIndex = i;
      }

      #region the edit counter

      [Fact]
      public void EditCounter_WriteData_Changes() {
         var before = ModelEditStamp.Current;
         Model[0x10] = 5;
         Assert.NotEqual(before, ModelEditStamp.Current);
      }

      [Fact]
      public void EditCounter_WriteTheSameValueThroughAToken_DoesNotChange() {
         Model[0x10] = 5;
         var before = ModelEditStamp.Current;
         Token.ChangeData(Model, 0x10, 5);
         Assert.Equal(before, ModelEditStamp.Current);
      }

      [Fact]
      public void EditCounter_ChangeFormat_Changes() {
         var before = ModelEditStamp.Current;
         ViewPort.Edit(TableFormat);
         Assert.NotEqual(before, ModelEditStamp.Current);
      }

      [Fact]
      public void EditCounter_AddRunWithoutTouchingData_Changes() {
         var before = ModelEditStamp.Current;
         Token.AddRun(new PointerRun(0x20));
         Assert.NotEqual(before, ModelEditStamp.Current);
      }

      [Fact]
      public void EditCounter_Undo_Changes() {
         ViewPort.Edit(TableFormat);
         ViewPort.Goto.Execute(4);
         ViewPort.Edit("10 ");
         var before = ModelEditStamp.Current;
         ViewPort.Undo.Execute();
         Assert.NotEqual(before, ModelEditStamp.Current);
      }

      [Fact]
      public void EditCounter_Redo_Changes() {
         ViewPort.Edit(TableFormat);
         ViewPort.Goto.Execute(4);
         ViewPort.Edit("10 ");
         ViewPort.Undo.Execute();
         var before = ModelEditStamp.Current;
         ViewPort.Redo.Execute();
         Assert.NotEqual(before, ModelEditStamp.Current);
      }

      [Fact]
      public void EditCounter_ResizeData_Changes() {
         var before = ModelEditStamp.Current;
         Model.ExpandData(Token, Model.Count + 0x10);
         Assert.NotEqual(before, ModelEditStamp.Current);
      }

      [Fact]
      public void EditCounter_JustLooking_DoesNotChange() {
         ViewPort.Edit(TableFormat);
         ViewPort.Goto.Execute(4);
         var before = ModelEditStamp.Current;

         ViewPort.Goto.Execute(8);
         ViewPort.Goto.Execute(4);
         _ = Model.GetNextRun(0);
         _ = Model.GetTable("table");
         _ = ViewPort.Tools.TableTool.Children.Count;
         ViewPort.Refresh();

         Assert.Equal(before, ModelEditStamp.Current);
      }

      #endregion

      #region nothing was edited: the tabs are left alone

      [Fact]
      public void SelectTab_NothingEdited_TableToolIsNotRebuilt() {
         ViewPort.Edit(TableFormat);
         ViewPort.Goto.Execute(4);
         AddAndVisitAll(ViewPort, CreateOtherFile());
         var builds = new BuildCounter(ViewPort);

         editor.SelectedIndex = 1;
         editor.SelectedIndex = 0;

         Assert.Equal(0, builds.Count);
         Assert.True(ViewPort.Tools.TableTool.IsUpToDate);
         Assert.Equal("table/1", ViewPort.Tools.TableTool.CurrentElementName);
      }

      [Fact]
      public void SelectTab_NothingEdited_ManyRoundsDoNotRebuildAnything() {
         ViewPort.Edit(TableFormat);
         ViewPort.Goto.Execute(4);
         var tabs = new ITabContent[] { ViewPort, CreateOtherFile("1.gba"), CreateOtherFile("2.gba"), CreateDuplicate(ViewPort), CreateOtherFile("3.gba") };
         AddAndVisitAll(tabs);
         var builds = new BuildCounter(ViewPort);
         var duplicateBuilds = new BuildCounter((ViewPort)tabs[3]);

         for (int round = 0; round < 5; round++) {
            for (int i = 0; i < tabs.Length; i++) {
               var target = (i * 3) % tabs.Length;
               editor.SelectedIndex = target;
               Assert.Same(tabs[target], editor.SelectedTab);
            }
         }

         Assert.Equal(0, builds.Count);
         Assert.Equal(0, duplicateBuilds.Count);
      }

      [Fact]
      public void SelectTab_NothingEdited_CursorAndSelectionStayWhereTheyWere() {
         ViewPort.Edit(TableFormat);
         ViewPort.Goto.Execute(8);
         var other = CreateOtherFile();
         other.Goto.Execute(0x30);
         AddAndVisitAll(ViewPort, other);
         var (selection, otherSelection) = (ViewPort.SelectionStart, other.SelectionStart);

         editor.SelectedIndex = 1;
         editor.SelectedIndex = 0;
         editor.SelectedIndex = 1;

         Assert.Equal(otherSelection, other.SelectionStart);
         Assert.Equal(selection, ViewPort.SelectionStart);
         Assert.Equal(8, ViewPort.ConvertViewPointToAddress(ViewPort.SelectionStart));
         Assert.Equal(0x30, other.ConvertViewPointToAddress(other.SelectionStart));
      }

      [Fact]
      public void SelectTab_NothingEdited_StillTellsTheViewToRedrawTheCells() {
         ViewPort.Edit(TableFormat);
         AddAndVisitAll(ViewPort, CreateOtherFile());
         editor.SelectedIndex = 1;
         var resets = 0;
         ViewPort.CollectionChanged += (sender, e) => resets += e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset ? 1 : 0;

         editor.SelectedIndex = 0;

         Assert.True(resets > 0);
      }

      #endregion

      #region something was edited: the tabs catch up, once

      [Fact]
      public void SelectTab_EditedFromAnotherTab_TableToolShowsTheNewValue() {
         ViewPort.Edit(TableFormat);
         ViewPort.Goto.Execute(4);
         var second = CreateDuplicate(ViewPort);
         AddAndVisitAll(ViewPort, second);
         Assert.Equal("0", FirstField(ViewPort).Content);

         editor.SelectedIndex = 1;
         second.Goto.Execute(4);
         second.Edit("10 ");
         editor.SelectedIndex = 0;

         Assert.Equal("10", FirstField(ViewPort).Content);
         Assert.Equal("table/1", ViewPort.Tools.TableTool.CurrentElementName);
      }

      [Fact]
      public void SelectTab_EditedFromAnotherTab_IsRebuiltOnce_ThenLeftAlone() {
         ViewPort.Edit(TableFormat);
         ViewPort.Goto.Execute(4);
         var second = CreateDuplicate(ViewPort);
         AddAndVisitAll(ViewPort, second);
         var builds = new BuildCounter(ViewPort);
         editor.SelectedIndex = 1;
         second.Goto.Execute(4);
         second.Edit("10 ");

         editor.SelectedIndex = 0; // catches up
         Assert.Equal(1, builds.Count);
         editor.SelectedIndex = 1;
         editor.SelectedIndex = 0; // nothing happened in between
         editor.SelectedIndex = 1;
         editor.SelectedIndex = 0;

         Assert.Equal(1, builds.Count);
      }

      [Fact]
      public void SelectTab_UndoneFromAnotherTab_TableToolShowsTheOldValue() {
         ViewPort.Edit(TableFormat);
         ViewPort.Goto.Execute(4);
         var second = CreateDuplicate(ViewPort); // tabs of one file share one undo history
         AddAndVisitAll(ViewPort, second);
         editor.SelectedIndex = 1;
         second.Goto.Execute(4);
         second.Edit("10 ");
         editor.SelectedIndex = 0;
         Assert.Equal("10", FirstField(ViewPort).Content);

         editor.SelectedIndex = 1;
         second.Undo.Execute();
         editor.SelectedIndex = 0;

         Assert.Equal("0", FirstField(ViewPort).Content);
      }

      [Fact]
      public void SelectTab_NewFormatFromAnotherTab_TableToolFollowsTheFormat() {
         ViewPort.Edit(TableFormat);
         ViewPort.Goto.Execute(4);
         var second = CreateDuplicate(ViewPort);
         AddAndVisitAll(ViewPort, second);
         Assert.Equal(2, ViewPort.Tools.TableTool.Children.OfType<FieldArrayElementViewModel>().Count());

         editor.SelectedIndex = 1;
         second.Goto.Execute(0);
         second.Edit("^table[a: b: c:]3 ");
         editor.SelectedIndex = 0;

         Assert.Equal(3, ViewPort.Tools.TableTool.Children.OfType<FieldArrayElementViewModel>().Count());
      }

      [Fact]
      public void SelectTab_OtherFileEdited_BothTabsStayCorrect() {
         ViewPort.Edit(TableFormat);
         ViewPort.Goto.Execute(4);
         var other = CreateOtherFile();
         AddAndVisitAll(ViewPort, other);

         editor.SelectedIndex = 1;
         other.Edit("^stuff[a: b:]4 ");
         other.Goto.Execute(4);
         other.Edit("7 ");
         editor.SelectedIndex = 0;

         Assert.Equal("0", FirstField(ViewPort).Content);
         editor.SelectedIndex = 1;
         Assert.Equal("7", FirstField(other).Content);
      }

      [Fact]
      public void SelectTab_CursorMovedWhileAwayFromTheTab_TableToolFollowsTheCursor() {
         ViewPort.Edit(TableFormat);
         ViewPort.Goto.Execute(4);
         AddAndVisitAll(ViewPort, CreateOtherFile());
         editor.SelectedIndex = 1;

         ViewPort.Goto.Execute(8);
         editor.SelectedIndex = 0;

         Assert.Equal("table/2", ViewPort.Tools.TableTool.CurrentElementName);
         Assert.True(ViewPort.Tools.TableTool.IsUpToDate);
      }

      [Fact]
      public void SelectTab_SpartanModeChanged_TableToolIsRebuilt() {
         ViewPort.Edit(TableFormat);
         ViewPort.Goto.Execute(4);
         AddAndVisitAll(ViewPort, CreateOtherFile());
         Assert.True(ViewPort.Tools.TableTool.IsUpToDate);

         ViewPort.SpartanMode = true;

         Assert.False(ViewPort.Tools.TableTool.IsUpToDate);
         editor.SelectedIndex = 1;
         editor.SelectedIndex = 0;
         Assert.True(ViewPort.Tools.TableTool.IsUpToDate);
      }

      [Fact]
      public void TableTool_NeverBuilt_IsNotUpToDate() {
         Assert.False(ViewPort.Tools.TableTool.IsUpToDate);
      }

      [Fact]
      public void ForcedRefresh_AlwaysRebuildsTheTableTool() {
         ViewPort.Edit(TableFormat);
         ViewPort.Goto.Execute(4);
         AddAndVisitAll(ViewPort, CreateOtherFile());
         editor.SelectedIndex = 0;
         var builds = new BuildCounter(ViewPort);

         ViewPort.Refresh();

         Assert.Equal(1, builds.Count);
      }

      #endregion

      #region what a tab switch must still get right

      [Fact]
      public void SelectTab_SelectedTabAndIndexFollow() {
         var tabs = new ITabContent[] { ViewPort, CreateOtherFile("1.gba"), CreateOtherFile("2.gba") };
         AddAndVisitAll(tabs);

         foreach (var index in new[] { 2, 0, 1, 1, 2, 0 }) {
            editor.SelectedIndex = index;
            Assert.Equal(index, editor.SelectedIndex);
            Assert.Same(tabs[index], editor.SelectedTab);
         }
      }

      [Fact]
      public void SelectTab_GotoShortcutsBelongToTheSelectedTab() {
         var withShortcut = new ViewPort("a.gba", new PokemonModel(new byte[0x200], null, Singletons), InstantDispatch.Instance, Singletons);
         var shortcuts = new[] { new StoredGotoShortcut("name", "image", "destination") };
         withShortcut.Model.LoadMetadata(new StoredMetadata(default, default, default, default, default, default, shortcuts, Singletons.MetadataInfo, default));
         withShortcut.Edit("^destination ");
         AddAndVisitAll(withShortcut, CreateOtherFile("b.gba"), ViewPort);

         for (int round = 0; round < 3; round++) {
            editor.SelectedIndex = 0;
            Assert.Single(editor.GotoViewModel.Shortcuts);
            editor.SelectedIndex = 1;
            Assert.Empty(editor.GotoViewModel.Shortcuts);
            editor.SelectedIndex = 2;
            Assert.Empty(editor.GotoViewModel.Shortcuts);
         }
      }

      [Fact]
      public void SelectTab_UndoStaysWithTheTabThatWasEdited() {
         ViewPort.Edit(TableFormat);
         var other = CreateOtherFile();
         AddAndVisitAll(ViewPort, other);

         editor.SelectedIndex = 0;
         ViewPort.Goto.Execute(4);
         ViewPort.Edit("10 ");
         Assert.Equal(10, Model.ReadMultiByteValue(4, 2));
         editor.SelectedIndex = 1;
         Assert.False(editor.Undo.CanExecute(null));
         editor.SelectedIndex = 0;
         Assert.True(editor.Undo.CanExecute(null));

         editor.Undo.Execute();

         Assert.Equal(0, Model.ReadMultiByteValue(4, 2));
      }

      [Fact]
      public void SelectTab_TabsThatAreNotViewPortsAreStillRefreshed() {
         var refreshes = 0;
         editor.Add(ViewPort);
         editor.Add(new StubTabContent { Refresh = () => refreshes++ });
         var before = refreshes;

         editor.SelectedIndex = 0;
         editor.SelectedIndex = 1;

         Assert.True(refreshes > before);
      }

      #endregion
   }

   /// <summary>
   /// The edit counter (ModelEditStamp) is shared by every model in the process, so a test that edits a model while these tests run would change it under their feet.
   /// The collection runs on its own, never in parallel with the other test classes.
   /// </summary>
   [CollectionDefinition("TabSwitch", DisableParallelization = true)]
   public class TabSwitchCollection { }
}
