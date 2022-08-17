using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using System;
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
         viewPort.SelectionStart = new Point(0, 4);

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
         var test = new BaseViewModelTestClass(0x1200);

         test.ViewPort.Edit("<001060>");
         var results = test.ViewPort.Find("001060");

         Assert.Single(results);
      }

      [Fact]
      public void CanGotoUsingAtSymbol() {
         StandardSetup(out _, out _, out var viewPort);

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

      [Fact]
      public void CanSearchForBranchLink() {
         var test = new BaseViewModelTestClass();
         var command1 = test.ViewPort.Tools.CodeTool.Parser.Compile(test.Model, 0x0010, "bl <000060>").ToArray();
         var command2 = test.ViewPort.Tools.CodeTool.Parser.Compile(test.Model, 0x0100, "bl <000060>").ToArray();
         Array.Copy(command1, 0, test.Model.RawData, 0x0010, command1.Length);
         Array.Copy(command2, 0, test.Model.RawData, 0x0100, command2.Length);

         var results = test.ViewPort.Find("bl <000060>").ToList();

         Assert.Equal(2, results.Count);
         Assert.Equal(0x010, results[0].start);
         Assert.Equal(0x100, results[1].start);
      }

      [Fact]
      public void SelectionLengthIsPositiveWhenSelectingLeft() {
         var test = new BaseViewModelTestClass();

         test.ViewPort.SelectionStart = new Point(3, 0);
         test.ViewPort.SelectionEnd = new Point(0, 0);

         Assert.Contains("4", test.ViewPort.SelectedLength);
      }

      [Fact]
      public void CanPrefixAddressWith0xDuringGoto() {
         var editor = new EditorViewModel(new StubFileSystem());
         var test = new BaseViewModelTestClass();
         editor.Add(test.ViewPort);

         editor.GotoViewModel.Goto.Execute("0x0030");

         Assert.Equal(0x30, test.ViewPort.DataOffset);
      }

      [Fact]
      public void CanGotoNamedElements() {
         var test = new BaseViewModelTestClass();
         test.CreateTextTable("names", 0x100, "Adam Bob Carl Dave Eric Fred Greg Hal Jim".Split(" "));
         test.ViewPort.Edit("@00 ^table[content.]names ");

         test.ViewPort.Goto.Execute("table/Eric");

         Assert.Equal(4, test.ViewPort.DataOffset);
      }

      [Fact]
      public void CanGotoSecondNamedElementWithSameName() {
         var test = new BaseViewModelTestClass();
         test.CreateTextTable("names", 0x100, "Adam Bob Carl Dave Eric Dave Fred Greg Hal Jim".Split(" "));
         test.ViewPort.Edit("@00 ^table[content.]names ");

         test.ViewPort.Goto.Execute("table/Dav~2");

         Assert.Equal(5, test.ViewPort.DataOffset);
      }

      [Fact]
      public void CanFollowPointerInline() {
         var test = new BaseViewModelTestClass();
         test.ViewPort.Edit("00 01 02 03 <000100> @04 @{ 80 @} ");
         Assert.Equal(0, test.ViewPort.DataOffset);
         Assert.Equal(new Point(8, 0), test.ViewPort.SelectionStart);
         Assert.Equal(0x80, test.Model[0x100]);
      }

      [Fact]
      public void CanCreatePointerInline() {
         var test = new BaseViewModelTestClass();

         test.ViewPort.Edit("^table[content<\"\">]2 @{ Hello!\" @}");
         var matches = test.ViewPort.Find("Hello!");

         Assert.Equal(4, test.ViewPort.DataOffset);
         Assert.Equal(new Point(0, 0), test.ViewPort.SelectionStart);
         Assert.Single(matches);
      }

      [Fact]
      public void CanGotoSingleOptionEvenWithoutPerfectMatch() {
         var test = new BaseViewModelTestClass();
         test.ViewPort.Edit("^pizza @piza ");
         Assert.Empty(test.Errors);

         test.ViewPort.Edit("@20 ^candy ");
         var editor = new EditorViewModel(new StubFileSystem());
         editor.Add(test.ViewPort);
         editor.GotoViewModel.Text = "cady";
         editor.GotoViewModel.Goto.Execute();

         Assert.Empty(test.Errors);
      }

      [Fact]
      public void NoNamespaceHasMoreThanNineElements() {
         var singletons = BaseViewModelTestClass.Singletons;
         foreach (var game in singletons.GameReferenceTables.Keys) {
            var gameTables = singletons.GameReferenceTables[game];
            var namespaces = new Dictionary<string, int>();
            foreach (var table in gameTables) {
               var namespaceLength = table.Name.LastIndexOf('.');
               if (namespaceLength < 0) continue;
               var currentNamespace = table.Name.Substring(0, namespaceLength);
               if (!namespaces.ContainsKey(currentNamespace)) namespaces[currentNamespace] = 0;
               namespaces[currentNamespace] += 1;
            }
            foreach (var currentNamespace in namespaces.Keys) {
               var elements = namespaces[currentNamespace];
               Assert.InRange(elements, 0, 9); // we shouldn't have found more than 7
            }
         }
      }

      [Fact]
      public void NamedConstant_PasteScriptAtEquals_ValueChange() {
         StandardSetup(out var data, out var model, out var viewPort);
         viewPort.Edit("@00 .some.number @10 .some.number @20 ");

         viewPort.Edit("@some.number=7 ");

         Assert.Equal(7, model[0x00]);
         Assert.Equal(7, model[0x10]);
      }

      [Fact]
      public void NamedConstant_PasteScriptGotoSpecifyIndex2_GotoSecondInstance() {
         StandardSetup(out var data, out var model, out var viewPort);
         viewPort.Edit("@00 .some.number @10 .some.number @20 ");

         viewPort.Edit("@some.number~2 ");

         Assert.Equal(0x10, viewPort.ConvertViewPointToAddress(viewPort.SelectionStart));
      }

      [Fact]
      public void FreeSpaceStart_GotoFreeSpaceStart() {
         var test = new BaseViewModelTestClass();

         test.ViewPort.FreeSpaceStart = 0x100;
         test.ViewPort.GotoFreeSpaceStart.Execute();

         Assert.Equal(0x100, test.ViewPort.DataOffset);
      }

      private void StandardSetup(out byte[] data, out PokemonModel model, out ViewPort viewPort) {
         data = Enumerable.Repeat((byte)0xFF, 0x200).ToArray();
         model = new PokemonModel(data);
         viewPort = new ViewPort("file.txt", model, InstantDispatch.Instance, BaseViewModelTestClass.Singletons) { Width = 0x10, Height = 0x10 };
      }
   }
}
