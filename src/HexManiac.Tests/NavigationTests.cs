using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class NavigationTests {
      [Fact]
      public void AddressesAreCorrect() {
         var viewPort = new ViewPort(new LoadedFile("test.txt", new byte[0x200])) { Width = 0x10, Height = 0x10 };

         Assert.Equal("000000", viewPort.Headers[0]);
         Assert.Equal("000010", viewPort.Headers[1]);
         Assert.Equal(viewPort.Height, viewPort.Headers.Count);
      }

      [Fact]
      public void AddressUpdateOnWidthChanged() {
         var viewPort = new ViewPort(new LoadedFile("test.txt", new byte[0x200])) { PreferredWidth = -1, Width = 0x10, Height = 0x10 };
         var changeCount = 0;
         viewPort.Headers.CollectionChanged += (sender, e) => changeCount += 1;

         viewPort.Width++;

         Assert.InRange(changeCount, 1, viewPort.Height);
         Assert.Equal("000011", viewPort.Headers[1]);
      }

      [Fact]
      public void AddressUpdateOnScroll() {
         var viewPort = new ViewPort(new LoadedFile("test.txt", new byte[0x200])) { Width = 0x10, Height = 0x10 };
         var changeCount = 0;
         viewPort.Headers.CollectionChanged += (sender, e) => changeCount += 1;

         viewPort.ScrollValue++;

         Assert.InRange(changeCount, 1, viewPort.Height);
         Assert.Equal("000020", viewPort.Headers[1]);
      }

      [Fact]
      public void AddressExtendOnHeightChanged() {
         var viewPort = new ViewPort(new LoadedFile("test.txt", new byte[0x200])) { Width = 0x10, Height = 0x10 };
         var changeCount = 0;
         viewPort.Headers.CollectionChanged += (sender, e) => changeCount += 1;

         viewPort.Height++;

         Assert.InRange(changeCount, 1, viewPort.Height);
         Assert.Equal(viewPort.Height, viewPort.Headers.Count);
      }

      [Fact]
      public void AddressMissingIfBeforeFile() {
         var viewPort = new ViewPort(new LoadedFile("test.txt", new byte[0x200])) { Width = 0x10, Height = 0x10 };
         var changeCount = 0;
         viewPort.Headers.CollectionChanged += (sender, e) => changeCount += 1;

         viewPort.Scroll.Execute(Direction.Left);

         Assert.InRange(changeCount, 1, viewPort.Height);
         Assert.Equal(string.Empty, viewPort.Headers[0]);
         Assert.Equal("00000F", viewPort.Headers[1]);
      }

      [Fact]
      public void AddressMissingIfAfterEndOfFile() {
         var viewPort = new ViewPort(new LoadedFile("test.txt", new byte[0x200])) { Width = 0x10, Height = 0x10 };
         var changeCount = 0;
         viewPort.Headers.CollectionChanged += (sender, e) => changeCount += 1;

         viewPort.ScrollValue = viewPort.MaximumScroll;

         Assert.InRange(changeCount, 1, viewPort.Height);
         Assert.Equal("0001F0", viewPort.Headers[0]);
         Assert.Equal(string.Empty, viewPort.Headers[1]);
      }

      [Fact]
      public void AddressAddedIfFileGrows() {
         var viewPort = new ViewPort(new LoadedFile("test.txt", new byte[0x200])) { Width = 0x10, Height = 0x10 };
         var changeCount = 0;
         viewPort.Headers.CollectionChanged += (sender, e) => changeCount += 1;

         viewPort.ScrollValue = viewPort.MaximumScroll;
         viewPort.SelectionStart = new Point(0, 1);
         viewPort.Edit("FF");

         Assert.Equal("000200", viewPort.Headers[1]);
      }

      [Fact]
      public void GotoWorks() {
         var viewPort = new ViewPort(new LoadedFile("test.txt", new byte[0x1000])) { Width = 0x10, Height = 0x10 };

         viewPort.Goto.Execute("000300");

         Assert.Equal("000300", viewPort.Headers[0]);
      }

      [Fact]
      public void GotoIsNotCaseSensitive() {
         var viewPort = new ViewPort(new LoadedFile("test.txt", new byte[0x1000])) { Width = 0x10, Height = 0x10 };

         viewPort.Goto.Execute("000a00");

         Assert.Equal("000A00", viewPort.Headers[0]);
      }

      [Fact]
      public void GotoIsReversable() {
         var viewPort = new ViewPort(new LoadedFile("test.txt", new byte[0x1000])) { Width = 0x10, Height = 0x10 };

         var changeCount = 0;
         viewPort.Forward.CanExecuteChanged += (sender, e) => changeCount++;

         viewPort.Goto.Execute("000A00");
         Assert.Equal(0, changeCount);

         viewPort.Back.Execute();
         Assert.Equal(1, changeCount);

         Assert.Equal("000000", viewPort.Headers[0]);
      }

      [Fact]
      public void BackIsReversable() {
         var viewPort = new ViewPort(new LoadedFile("test.txt", new byte[0x1000])) { Width = 0x10, Height = 0x10 };

         var changeCount = 0;
         viewPort.Back.CanExecuteChanged += (sender, e) => changeCount++;

         viewPort.Goto.Execute("000A00");
         Assert.Equal(1, changeCount);

         viewPort.Back.Execute();
         Assert.Equal(2, changeCount);

         viewPort.Forward.Execute();
         Assert.Equal(3, changeCount);

         Assert.Equal("000A00", viewPort.Headers[0]);
      }

      [Fact]
      public void CannotBackFromSearchTab() {
         var searchTab = new SearchResultsViewPort("bob");
         Assert.False(searchTab.Back.CanExecute(null));
         Assert.False(searchTab.Forward.CanExecute(null));
      }

      [Fact]
      public void BackRecallsYourLastScrollPosition() {
         var viewPort = new ViewPort(new LoadedFile("test.txt", new byte[0x1000])) { Width = 0x10, Height = 0x10 };

         viewPort.ScrollValue++;
         viewPort.Goto.Execute("000A00");
         viewPort.ScrollValue++;
         viewPort.Back.Execute();
         Assert.Equal("000010", viewPort.Headers[0]);

         viewPort.Forward.Execute();
         Assert.Equal("000A10", viewPort.Headers[0]);
      }

      [Fact]
      public void GotoMovesScroll() {
         var viewPort = new ViewPort(new LoadedFile("test.txt", new byte[0x1000])) { Width = 0x10, Height = 0x10 };

         viewPort.Goto.Execute("000A00");

         Assert.NotEqual(0, viewPort.ScrollValue);
      }

      [Fact]
      public void GotoErrorsOnBadAddress() {
         var viewPort = new ViewPort(new LoadedFile("test.txt", new byte[0x1000])) { Width = 0x10, Height = 0x10 };
         int errorCount = 0;
         viewPort.OnError += (sender, e) => errorCount += 1;

         viewPort.Goto.Execute("BadAddress");

         Assert.Equal(1, errorCount);
      }

      [Fact]
      public void GotoMovesSelection() {
         var viewPort = new ViewPort(new LoadedFile("test.txt", new byte[0x1000])) { Width = 0x10, Height = 0x10 };

         viewPort.Goto.Execute("000C00");

         Assert.Equal(new Point(0, 0), viewPort.SelectionStart);
      }

      [Fact]
      public void SearchFor6BytesAlsoFindsPointers() {
         var data = new byte[0x1200];
         var model = new PokemonModel(data);
         var viewPort = new ViewPort("file.txt", model);

         viewPort.Edit("<001060>");
         var results = viewPort.Find("001060");

         Assert.Single(results);
      }

      [Fact]
      public void GotoAutoCompleteNavigationCommandsWork() {
         var tab = new StubViewPort {
            Goto = new StubCommand { CanExecute = ICommandExtensions.CanAlwaysExecute },
            Model = new StubDataModel {
               GetAutoCompleteAnchorNameOptions = str => new List<string> {
                  "Option 1",
                  "Option 2",
                  "Option 3",
               },
               Equals = arg => arg is StubDataModel,
            },
         };
         var viewModel = new GotoControlViewModel(tab);

         Assert.False(viewModel.ControlVisible);
         viewModel.ShowGoto.Execute(true);
         Assert.True(viewModel.ControlVisible);

         Assert.False(viewModel.ShowAutoCompleteOptions);
         viewModel.Text = "Something";
         Assert.True(viewModel.ShowAutoCompleteOptions);
         Assert.Equal(3, viewModel.AutoCompleteOptions.Count);
         Assert.All(viewModel.AutoCompleteOptions, option => Assert.False(option.IsSelected));

         viewModel.MoveAutoCompleteSelectionDown.Execute();
         Assert.True(viewModel.AutoCompleteOptions[0].IsSelected);

         viewModel.MoveAutoCompleteSelectionDown.Execute();
         Assert.True(viewModel.AutoCompleteOptions[1].IsSelected);

         viewModel.MoveAutoCompleteSelectionUp.Execute();
         Assert.True(viewModel.AutoCompleteOptions[0].IsSelected);
      }

      [Fact]
      public void CanGotoUsingAtSymbol() {
         StandardSetup(out var data, out var model, out var viewPort);

         viewPort.Edit("@100 ");

         Assert.Equal("000100", viewPort.Headers[0]);
      }

      [Fact]
      public void CanGotoAddressWith08Prepended() {
         StandardSetup(out var data, out var model, out var viewPort);
         var errors = new List<string>();
         viewPort.OnError += (sender, e) => errors.Add(e);

         viewPort.Goto.Execute("8000100");

         Assert.Empty(errors);
         Assert.Equal(0x100, viewPort.DataOffset);
      }

      [Fact]
      public void CanGotoAddressWithAngleBraces() {
         StandardSetup(out var data, out var model, out var viewPort);
         var errors = new List<string>();
         viewPort.OnError += (sender, e) => errors.Add(e);

         viewPort.Goto.Execute("<000100>");

         Assert.Empty(errors);
         Assert.Equal(0x100, viewPort.DataOffset);
      }

      [Fact]
      public void GotoBadPointerErrors() {
         StandardSetup(out var data, out var model, out var viewPort);
         var errors = new List<string>();
         viewPort.OnError += (sender, e) => errors.Add(e);

         viewPort.Edit("^table[p<>]2 ");
         viewPort.FollowLink(1, 0);

         Assert.Single(errors);
      }

      [Fact]
      public void TableLengthChangeShouldNotCauseGoto() {
         StandardSetup(out var data, out var model, out var viewPort);

         viewPort.SelectionStart = new Point(0, 2);
         viewPort.Edit("^bob[data.]10 ");             // auto scroll (table format change)
         viewPort.Goto.Execute("000000");
         viewPort.SelectionStart = new Point(0, 2);
         viewPort.AnchorText = "^bob[data.]12";       // no auto scroll (table length change)

         Assert.Equal("000000", viewPort.Headers[0]);
      }

      [Fact]
      public void CanResetAlignment() {
         var fileSystem = new StubFileSystem();
         StandardSetup(out var data, out var model, out var viewPort);
         var editor = new EditorViewModel(fileSystem);
         editor.Add(viewPort);

         viewPort.Goto.Execute("10");
         viewPort.Edit("^moves[name\"\"8]8 Adam\" Bob\" Carl\" Dave\" Elen\" Fred\" Gary\" Horton\"");
         viewPort.Width = 25;
         Assert.Equal(24, viewPort.Width); // closest smaller multiple of 8

         editor.ResetAlignment.Execute();
         Assert.Equal(16, viewPort.Width); // closest smaller multiple of 16
      }

      [Fact]
      public void GotoAutocompleteIncludesTableFields() {
         var test = new BaseViewModelTestClass();
         test.ViewPort.Edit("^table[field1:: field2<>]2 ");

         using (ModelCacheScope.CreateScope(test.Model)) {
            var options = test.Model.GetAutoCompleteAnchorNameOptions("table/0/");

            Assert.Contains("table/0/field1", options);
            Assert.Contains("table/0/field2", options);

            options = test.Model.GetAutoCompleteAnchorNameOptions("table//field2/");

            Assert.Contains("table/0/field2/", options);
            Assert.Contains("table/1/field2/", options);
         }
      }

      private void StandardSetup(out byte[] data, out PokemonModel model, out ViewPort viewPort) {
         data = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         model = new PokemonModel(data);
         viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };
      }
   }
}
