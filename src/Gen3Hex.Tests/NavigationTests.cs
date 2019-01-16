using HavenSoft.Gen3Hex.Core;
using HavenSoft.Gen3Hex.Core.Models;
using HavenSoft.Gen3Hex.Core.ViewModels;
using Xunit;

namespace HavenSoft.Gen3Hex.Tests {
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
         var viewPort = new ViewPort(new LoadedFile("test.txt", new byte[0x200])) { Width = 0x10, Height = 0x10 };
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
   }
}
