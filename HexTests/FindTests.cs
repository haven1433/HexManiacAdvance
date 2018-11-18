using HavenSoft.Gen3Hex.Model;
using HavenSoft.Gen3Hex.ViewModel;
using System.Collections.ObjectModel;
using System.Linq;
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
         int gotoCount = 0;
         StubViewPort tab = null;
         tab = new StubViewPort {
            Find = str => new[] { 0x54, 0x154 },
            Goto = new StubCommand { CanExecute = arg => true, Execute = arg => gotoCount++ },
            CreateChildView = offset => new ChildViewPort(tab, new byte[100]),
            Headers = new ObservableCollection<string> { "00", "01", "02", "03" },
            Width = 4,
            Height = 4,
         };
         var editor = new EditorViewModel(new StubFileSystem()) { tab };

         editor.Find.Execute("something");
         editor.SelectedIndex = 0;
         editor.FindNext.Execute("something");
         editor.FindPrevious.Execute("something");

         Assert.Equal(2, gotoCount); // findNext / findPrevious use goto
      }

      [Fact]
      public void EditorFindNextDoesNotSwitchTabs() {
         StubViewPort tab1 = null;
         tab1 = new StubViewPort {
            Find = query => new[] { 0x60 },
            Goto = new StubCommand(),
            CreateChildView = offset => new ChildViewPort(tab1, new byte[100]),
            Headers = new ObservableCollection<string> { "00", "01", "02", "03" },
            Width = 4,
            Height = 4,
         };
         StubViewPort tab2 = null;
         tab2 = new StubViewPort {
            Find = query => new[] { 0x50, 0x70 },
            Goto = new StubCommand(),
            CreateChildView = offset => new ChildViewPort(tab2, new byte[100]),
            Headers = new ObservableCollection<string> { "00", "01", "02", "03" },
            Width = 4,
            Height = 4,
         };
         var editor = new EditorViewModel(new StubFileSystem()) { tab1, tab2 };

         editor.Find.Execute("something");

         editor.FindNext.Execute("something");
         Assert.Equal(2, editor.SelectedIndex); // results still selected

         editor.SelectedIndex = 0;
         editor.FindNext.Execute("something");
         Assert.Equal(0, editor.SelectedIndex); // results in first tab selected

         editor.SelectedIndex = 1;
         editor.FindNext.Execute("something");
         Assert.Equal(1, editor.SelectedIndex); // results in second tab selected
      }

      [Fact]
      public void FindResultsHasHeadersAndGaps() {
         StubViewPort tab = null;
         tab = new StubViewPort {
            Find = query => new[] { 0x50, 0x70 },
            Goto = new StubCommand(),
            CreateChildView = offset => new ChildViewPort(tab, new byte[100]),
            Headers = new ObservableCollection<string> { "00", "01", "02", "03" },
            Width = 4,
            Height = 4,
         };
         var editor = new EditorViewModel(new StubFileSystem()) { tab };

         editor.Find.Execute("something");
         var results = (IViewPort)editor[1];
         results.Height = 9; // both children are size 4, one space inbetween
         Assert.False(results.Headers.All(string.IsNullOrEmpty)); // not all the headers are blank
         Assert.Contains(results.Headers, string.IsNullOrEmpty);  // blank lines have blank headers
      }

      [Fact]
      public void FindClosesAfterRun() {
         StubViewPort tab = null;
         tab = new StubViewPort {
            Find = query => new[] { 0x50, 0x70 },
            Goto = new StubCommand(),
            CreateChildView = offset => new ChildViewPort(tab, new byte[100]),
            Headers = new ObservableCollection<string> { "00", "01", "02", "03" },
            Width = 4,
            Height = 4,
         };
         var editor = new EditorViewModel(new StubFileSystem()) { tab };

         editor.ShowFind.Execute(true);
         editor.Find.Execute("something");

         Assert.False(editor.FindControlVisible);
      }

      [Fact]
      public void CompositeCanScroll() {
         var composite = new CompositeViewPort("search") { Height = 0x10 };
         var parent = new StubViewPort { Width = 0x10, Height = 0x10 };
         var parentData = new byte[0x100];
         composite.Add(new ChildViewPort(parent, parentData));
         composite.Add(new ChildViewPort(parent, parentData));

         var bodyChanged = false;
         var headerChanged = false;
         composite.Headers.CollectionChanged += (sender, e) => headerChanged = true;
         composite.CollectionChanged += (sender, e) => bodyChanged = true;

         composite.ScrollValue = 4;

         Assert.True(bodyChanged);
         Assert.True(headerChanged);
      }
   }
}
