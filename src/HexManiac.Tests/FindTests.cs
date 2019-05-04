using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class FindTests {
      [Fact]
      public void FindCanFindSingle() {
         var array = new byte[0x1000];
         new byte[] { 0x84, 0x23, 0xBB, 0x21 }.CopyTo(array, 0x240);
         var port = new ViewPort(new LoadedFile("test", array));

         var results = port.Find("84 23 BB 21");

         Assert.Single(results);
         Assert.Equal(0x240, results[0].start);
      }

      [Fact]
      public void FindCanFindMultiple() {
         var array = new byte[0x1000];
         var searchFor = new byte[] { 0x84, 0x23, 0xBB, 0x21 };
         searchFor.CopyTo(array, 0x240);
         searchFor.CopyTo(array, 0xA70);
         var port = new ViewPort(new LoadedFile("test", array));

         var results = port.Find("84 23 BB 21").Select(result => result.start).ToList();

         Assert.Equal(2, results.Count);
         Assert.Contains(0x240, results);
         Assert.Contains(0xA70, results);
      }

      [Fact]
      public void EditorShowsErrorIfNoneFound() {
         var tab = new StubViewPort();
         var editor = new EditorViewModel(new StubFileSystem()) { tab };

         tab.Find = str => new (int, int)[0];
         editor.Find.Execute("something");

         Assert.True(editor.ShowError);
         Assert.False(string.IsNullOrEmpty(editor.ErrorMessage));
      }

      [Fact]
      public void EditorJumpsToResultIfSingleResult() {
         var tab = new StubViewPort();
         var editor = new EditorViewModel(new StubFileSystem()) { tab };
         string gotoArg = string.Empty;

         tab.Find = str => new[] { (0x54, 0x54) };
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

         tab.Find = str => new[] { (0x54, 0x54), (0x154, 0x154) };
         tab.Model = new BasicModel(new byte[0x200]);
         tab.CreateChildView = (int startAddress, int endAddress) => {
            var child = new ChildViewPort(tab);
            count++;
            return child;
         };
         editor.Find.Execute("something");

         Assert.Equal(2, count); // since there were 2 results, editor should've asked for 2 child views
         Assert.Equal(2, editor.Count);
         Assert.Equal(1, editor.SelectedIndex);
         Assert.IsType<SearchResultsViewPort>(editor[1]);
      }

      [Fact]
      public void EditorHasShortcutsToGetPreviousOrNextFindResult() {
         int gotoCount = 0;
         StubViewPort tab = null;
         tab = new StubViewPort {
            Find = str => new[] { (0x54, 0x54), (0x154, 0x154) },
            Model = new BasicModel(new byte[0x200]),
            Goto = new StubCommand { CanExecute = arg => true, Execute = arg => gotoCount++ },
            CreateChildView = (start, end) => new ChildViewPort(tab),
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
            Find = query => new[] { (0x60, 0x60) },
            Goto = new StubCommand(),
            Model = new BasicModel(new byte[0x200]),
            CreateChildView = (start, end) => new ChildViewPort(tab1),
            Headers = new ObservableCollection<string> { "00", "01", "02", "03" },
            Width = 4,
            Height = 4,
         };
         StubViewPort tab2 = null;
         tab2 = new StubViewPort {
            Find = query => new[] { (0x50, 0x50), (0x70, 0x70) },
            Goto = new StubCommand(),
            Model = new BasicModel(new byte[0x200]),
            CreateChildView = (start, end) => new ChildViewPort(tab2),
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
            Find = query => new[] { (0x50, 0x50), (0x70, 0x70) },
            Goto = new StubCommand(),
            Model = new BasicModel(new byte[0x200]),
            CreateChildView = (start, end) => new ChildViewPort(tab),
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
            Find = query => new[] { (0x50, 0x50), (0x70, 0x70) },
            Goto = new StubCommand(),
            Model = new BasicModel(new byte[0x200]),
            CreateChildView = (start, end) => new ChildViewPort(tab),
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
         var composite = new SearchResultsViewPort("search") { Height = 0x10 };
         var parentData = new byte[0x100];
         var parent = new StubViewPort { Width = 0x10, Height = 0x10, Model = new BasicModel(parentData) };
         composite.Add(new ChildViewPort(parent), 0, 0);
         composite.Add(new ChildViewPort(parent), 0, 0);

         var bodyChanged = false;
         var headerChanged = false;
         composite.Headers.CollectionChanged += (sender, e) => headerChanged = true;
         composite.CollectionChanged += (sender, e) => bodyChanged = true;

         composite.ScrollValue = 4;

         Assert.True(bodyChanged);
         Assert.True(headerChanged);
      }

      [Fact]
      public void FindOrderShouldBeBasedOnCurrentSelection() {
         var data = new byte[0x100];
         var dataToFind = new byte[] { 0x52, 0xDC, 0xFF, 0x79 };
         dataToFind.CopyTo(data, 0x2);
         dataToFind.CopyTo(data, 0x62);
         dataToFind.CopyTo(data, 0xA8);
         dataToFind.CopyTo(data, 0xCC);
         var viewPort = new ViewPort(new LoadedFile("test", data)) { Width = 8, Height = 8 };
         viewPort.SelectionStart = new Point(0, 2); // select byte 0x10

         var results = viewPort.Find("52 DC FF 79").Select(result => result.start).ToList(); ;

         // the earliest match is at the end because the search started where the cursor was and looped around.
         Assert.True(results.SequenceEqual(new[] { 0x62, 0xA8, 0xCC, 0x02 }));
      }

      /// <summary>
      /// editor starts with most recently added tab selected.
      /// if a single result is found, the editor should switch to the tab that contains it.
      /// </summary>
      [Fact]
      public void FindSwitchesTabIfSingleResultIsOnAnotherTab() {
         var tab0 = new StubViewPort { Goto = new StubCommand() };
         var tab1 = new StubTabContent();
         var editor = new EditorViewModel(new StubFileSystem()) { tab0, tab1 };

         tab0.Find = query => new[] { (0x50, 0x50) };
         editor.Find.Execute("search");

         Assert.Equal(0, editor.SelectedIndex);
      }

      [Fact]
      public void FindNextFindPreviousDisabledIfNoResults() {
         var editor = new EditorViewModel(new StubFileSystem());

         Assert.False(editor.FindPrevious.CanExecute("text"));
         Assert.False(editor.FindNext.CanExecute("text"));
      }

      [Fact]
      public void FindNextAndPreviousBecomePossibleAfterFindSingleResult() {
         var editor = new EditorViewModel(new StubFileSystem());
         StubViewPort tab = null;
         tab = new StubViewPort {
            Find = query => new[] { (0x50, 0x50) },
            Goto = new StubCommand(),
            CreateChildView = (start, end) => new ChildViewPort(tab),
            Headers = new ObservableCollection<string> { "00", "01", "02", "03" },
            Width = 4,
            Height = 4,
         };
         editor.Add(tab);

         editor.Find.Execute("text");

         Assert.True(editor.FindPrevious.CanExecute("text"));
         Assert.True(editor.FindNext.CanExecute("text"));
      }

      [Fact]
      public void FindNextAndPreviousBecomePossibleAfterTabSwitchAfterFindMultiResult() {
         var editor = new EditorViewModel(new StubFileSystem());
         StubViewPort tab = null;
         tab = new StubViewPort {
            Find = query => new[] { (0x50, 0x50), (0x70, 0x70) },
            Goto = new StubCommand(),
            Model = new BasicModel(new byte[0x200]),
            CreateChildView = (start, end) => new ChildViewPort(tab),
            Headers = new ObservableCollection<string> { "00", "01", "02", "03" },
            Width = 4,
            Height = 4,
         };
         editor.Add(tab);

         editor.Find.Execute("text");
         Assert.False(editor.FindPrevious.CanExecute("text"));
         Assert.False(editor.FindNext.CanExecute("text"));

         editor.SelectedIndex = 0;
         Assert.True(editor.FindPrevious.CanExecute("text"));
         Assert.True(editor.FindNext.CanExecute("text"));
      }

      [Fact]
      public void FindRaisesMessageForGoodResults() {
         var editor = new EditorViewModel(new StubFileSystem());
         var data = new byte[0x100];
         var dataToFind = new byte[] { 0x52, 0xDC, 0xFF, 0x79 };
         dataToFind.CopyTo(data, 0x2);
         var viewPort = new ViewPort(new LoadedFile("test", data)) { Width = 8, Height = 8 };
         editor.Add(viewPort);
         editor.Find.Execute("52 DC FF 79");

         Assert.True(editor.ShowMessage);
      }

      [Fact]
      public void SearchResultsHaveFullResultsSelected() {
         var text = "This is the song that never ends.";
         var bytes = PCSString.Convert(text).ToArray();
         var buffer = new byte[0x200];
         Array.Copy(bytes, 0, buffer, 0x30, bytes.Length);                // two copies of the data
         Array.Copy(bytes, 0, buffer, 0x60, bytes.Length);                // at reasonable locations
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort("test.gba", model) { Width = 0x10, Height = 0x10 };
         var editor = new EditorViewModel(new StubFileSystem());
         editor.Add(viewPort);

         editor.Find.Execute("This is the song");
         var resultsTab = (IViewPort)editor[editor.SelectedIndex];
         resultsTab.Width = 0x10;
         resultsTab.Height = 0x10;

         int selectedCellCount = 0;
         for (int x = 0; x < resultsTab.Width; x++) {
            selectedCellCount +=
               Enumerable.Range(0, resultsTab.Height)
               .Select(y => new Point(x, y))
               .Count(resultsTab.IsSelected);
         }

         Assert.InRange(selectedCellCount, 30, 40);
      }

      [Fact]
      public void FollowingASearchResultSelectsEntireResult() {
         var text = "This is the song that never ends.";
         var bytes = PCSString.Convert(text).ToArray();
         var buffer = new byte[0x200];
         Array.Copy(bytes, 0, buffer, 0x30, bytes.Length);                // two copies of the data
         Array.Copy(bytes, 0, buffer, 0x60, bytes.Length);                // at reasonable locations
         var model = new PokemonModel(buffer);
         var viewPort = new ViewPort("test.gba", model);
         var editor = new EditorViewModel(new StubFileSystem());
         editor.Add(viewPort);

         editor.Find.Execute("This is the song");
         var resultsTab = (IViewPort)editor[editor.SelectedIndex];
         resultsTab.FollowLink(0, 1);

         Assert.NotEqual(viewPort.SelectionStart, viewPort.SelectionEnd);
      }

      [Fact]
      public void SearchCanFindTableRows() {
         // Arrange two tables, one that depends on the other
         var data = Enumerable.Range(0, 0x200).Select(i => (byte)0xFF).ToArray();
         var model = new PokemonModel(data);
         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };
         viewPort.Edit("^names[entry\"\"8] +\"bob\" +\"sam\" +\"carl\" +\"steve\" ");
         viewPort.Edit("@50 ^table[x: y:]names ");

         // Act: do a search that should return a table entry as a result
         var editor = new EditorViewModel(new StubFileSystem());
         editor.Add(viewPort);
         editor.Find.Execute("carl");

         // Assert: there are 2 results, one for the text and one for the table
         Assert.Contains("2", editor.InformationMessage);
      }

      [Fact]
      public void SearchCanFindEnumUsages() {
         // Arrange two tables, one that depends on the other
         var data = Enumerable.Range(0, 0x200).Select(i => (byte)0xFF).ToArray();
         var model = new PokemonModel(data);
         var viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };
         viewPort.Edit("^names[entry\"\"8] +\"bob\" +\"sam\" +\"carl\" +\"steve\" ");
         viewPort.Edit("@50 ^table[x: y:names]2 12 sam 100 steve ");

         // Act: do a search that should return a table entry as a result
         var editor = new EditorViewModel(new StubFileSystem());
         editor.Add(viewPort);
         editor.Find.Execute("steve");

         // Assert: there are 2 results, one for the text and one for the enum
         Assert.Contains("2", editor.InformationMessage);
      }

      [Fact]
      public void FindingSingleResultHighlightsEntireResult() {
         var data = Enumerable.Range(0, 0x100).Select(i => (byte)i).ToArray();
         var model = new PokemonModel(data);
         var viewPort = new ViewPort("name.txt", model) { Height = 0x10, Width = 0x10 };
         var editor = new EditorViewModel(new StubFileSystem());
         editor.Add(viewPort);

         editor.Find.Execute("05 06 07 08");

         Assert.NotEqual(viewPort.SelectionStart, viewPort.SelectionEnd);
      }
   }
}
