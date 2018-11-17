using HavenSoft.Gen3Hex.Model;
using HavenSoft.Gen3Hex.ViewModel;
using System.Windows.Input;
using Xunit;

namespace HavenSoft.HexTests {
   public class FindTests {

      [Fact]
      public void FindCanFindSingle() {
         var array = new byte[0x1000];
         new byte[] { 0x84, 0x23, 0xBB, 0x21 }.CopyTo(array, 0x240);
         var port = new ViewPort(new LoadedFile("test", array));

         var results = port.Find("84 23 BB 21");

         Assert.Single(results);
         Assert.Equal(0x240, results[0]);
      }

      [Fact]
      public void FindCanFindMultiple() {
         var array = new byte[0x1000];
         var searchFor = new byte[] { 0x84, 0x23, 0xBB, 0x21 };
         searchFor.CopyTo(array, 0x240);
         searchFor.CopyTo(array, 0xA70);
         var port = new ViewPort(new LoadedFile("test", array));

         var results = port.Find("84 23 BB 21");

         Assert.Equal(2, results.Count);
         Assert.Contains(0x240, results);
         Assert.Contains(0xA70, results);
      }

      [Fact]
      public void EditorShowsErrorIfNoneFound() {
         var tab = new StubViewPort();
         var editor = new EditorViewModel(new StubFileSystem()) { tab };

         tab.Find = str => new int[0];
         editor.Find.Execute("something");

         Assert.True(editor.ShowError);
         Assert.False(string.IsNullOrEmpty(editor.ErrorMessage));
      }

      [Fact]
      public void EditorJumpsToResultIfSingleResult() {
         var tab = new StubViewPort();
         var editor = new EditorViewModel(new StubFileSystem()) { tab };
         string gotoArg = string.Empty;

         tab.Find = str => new[] { 0x54 };
         tab.Goto = new StubCommand { CanExecute = arg => true, Execute = arg => gotoArg = (string)arg };
         editor.Find.Execute("something");

         Assert.False(editor.ShowError);
         Assert.Equal("54", gotoArg);
      }

      [Fact]
      public void EditorOpensHelperTabIfMultipleResult() {
         var tab = new StubViewPort();
         var editor = new EditorViewModel(new StubFileSystem()) { tab };
         var count = 0;

         tab.Find = str => new[] { 0x54, 0x154 };
         tab.CreateChildView = (int offset) => {
            var child = new ChildViewPort(new StubViewPort(), new byte[0x50]);
            count++;
            return child;
         };
         editor.Find.Execute("something");

         Assert.Equal(2, count); // since there were 2 results, editor should've asked for 2 child views
         Assert.Equal(2, editor.Count);
         Assert.Equal(1, editor.SelectedIndex);
         Assert.IsType<CompositeViewPort>(editor[1]);
      }

      [Fact]
      public void EditorHasShortcutsToGetPreviousOrNextFindResult() {
         var tab = new StubViewPort();
         var editor = new EditorViewModel(new StubFileSystem()) { tab };
         int gotoCount = 0;

         tab.Find = str => new[] { 0x54, 0x154 };
         tab.Goto = new StubCommand { CanExecute = arg => true, Execute = arg => gotoCount++ };
         editor.Find.Execute("something");
         editor.FindNext.Execute("something");
         editor.FindPrevious.Execute("something");

         Assert.Equal(2, gotoCount); // findNext / findPrevious use goto
      }
   }
}
