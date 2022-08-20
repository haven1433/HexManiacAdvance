using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System.Collections.Generic;
using Xunit;

[assembly: System.Reflection.AssemblyTitle("HexTests")]

namespace HavenSoft.HexManiac.Tests {
   public class ViewPortTests : BaseViewModelTestClass {
      [Fact]
      public void ViewPortNotifiesOnSizeChange() {
         var viewPort = new ViewPort();
         var changedProperties = new List<string>();
         viewPort.PropertyChanged += (sender, e) => changedProperties.Add(e.PropertyName);

         viewPort.Width = 12;
         viewPort.Height = 50;

         Assert.Contains(nameof(viewPort.Width), changedProperties);
         Assert.Contains(nameof(viewPort.Height), changedProperties);
      }

      [Fact]
      public void ViewPortStartsEmpty() {
         var viewPort = new ViewPort();

         Assert.Equal(HexElement.Undefined, viewPort[0, 0]);
         Assert.Equal(0, viewPort.MinimumScroll);
         Assert.Equal(0, viewPort.MaximumScroll);
      }

      [Fact]
      public void ViewPortScrollStartsAtTopRow() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };

         Assert.Equal(0, viewPort.ScrollValue);
      }

      /// <summary>
      /// The scroll bar is in terms of lines.
      /// The viewport should not be able to scroll such that all the data is out of view.
      /// </summary>
      [Fact]
      public void ViewPortScrollingDoesNotAllowEmptyScreen() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { PreferredWidth = -1, Width = 5, Height = 5 };

         Assert.Equal(0, viewPort.MinimumScroll);
         Assert.Equal(4, viewPort.MaximumScroll);
      }

      [Fact]
      public void ViewPortCannotScrollLowerThanMinimumScroll() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };

         viewPort.ScrollValue = int.MinValue;

         Assert.Equal(viewPort.MinimumScroll, viewPort.ScrollValue);
      }

      [Fact]
      public void ChangingWidthUpdatesScrollValueIfNeededOnScrollRegion() {
         var scroll = new ScrollRegion { DataLength = 25, Width = 5, Height = 5 };

         scroll.ScrollValue++;
         scroll.Width--;

         Assert.Equal(2, scroll.ScrollValue);
         Assert.Equal(6, scroll.MaximumScroll);
      }

      [Fact]
      public void ChangingWidthUpdatesScrollValueIfNeeded() {
         // ScrollValue=0 is always the line that contains the first byte of the file.
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { PreferredWidth = -1, Width = 5, Height = 5 };

         viewPort.ScrollValue++; // scroll down one line
         viewPort.Width--;      // decrease the width so that there is data 2 lines above

         // Example of what it should look like:
         // .. .. .. ..
         // .. .. .. 00
         // 00 00 00 00
         // 00 00 00 00 <- this is the top line in view
         // 00 00 00 00
         // 00 00 00 00
         // 00 00 00 00
         // 00 00 00 00 <- this is the bottom line in view
         // .. .. .. ..
         Assert.Equal(2, viewPort.ScrollValue);
         Assert.Equal(6, viewPort.MaximumScroll);
      }

      [Fact]
      public void ResizingCannotLeaveTotallyBlankLineAtTop() {
         var loadedFile = new LoadedFile("test", new byte[36]);
         var viewPort = new ViewPort(loadedFile) { PreferredWidth = -1, Width = 6, Height = 6 };

         viewPort.ScrollValue++;   // scroll down one line
         viewPort.Width--;         // decrease the width so that there is data 2 lines above

         // Example of what it should look like right now:
         // .. .. .. ..
         // .. .. .. 00
         // 00 00 00 00
         // 00 00 00 00 <- this is the top line in view
         // 00 00 00 00
         // 00 00 00 00
         // 00 00 00 00
         // 00 00 00 00 <- this is the bottom line in view
         // .. .. .. ..

         viewPort.ScrollValue = viewPort.MinimumScroll; // scroll up to top
         viewPort.Width--;                              // decrease the width to hide the last visible byte in the top row

         // expected: viewPort should auto-scroll here to make the top line full of data again
         Assert.Equal(0, viewPort.ScrollValue);
         Assert.NotEqual(HexElement.Undefined, viewPort[0, 0]);
      }

      [Fact]
      public void RequestingOutOfRangeDataReturnsUndefinedFormat() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { PreferredWidth = -1, Width = 5, Height = 5 };

         Assert.Equal(HexElement.Undefined, viewPort[0, -1]);
         Assert.Equal(HexElement.Undefined, viewPort[5, 0]);
         Assert.Equal(HexElement.Undefined, viewPort[0, 5]);
         Assert.Equal(HexElement.Undefined, viewPort[-1, 0]);
      }

      [Fact]
      public void MaximumScrollChangesBasedOnDataOffset() {
         var loadedFile = new LoadedFile("test", new byte[26]);
         var viewPort = new ViewPort(loadedFile) { PreferredWidth = -1, Width = 5, Height = 5 };

         // initial condition: given 4 data per row, there should be 7 rows (0-6) because 7*4=28
         viewPort.Width--;
         Assert.Equal(6, viewPort.MaximumScroll);
         viewPort.Width++;

         viewPort.ScrollValue++;   // scroll down one line
         viewPort.Width--;         // decrease the width so that there is data 2 lines above

         // Example of what it should look like right now:
         // .. .. .. 00
         // 00 00 00 00
         // 00 00 00 00 <- this is the top line in view
         // 00 00 00 00
         // 00 00 00 00
         // 00 00 00 00
         // 00 00 00 00 <- this is the bottom line in view
         // 00 .. .. ..

         // notice from the diagram above that there should now be _8_ rows (0-7).
         Assert.Equal(7, viewPort.MaximumScroll);
      }

      [Fact]
      public void ScrollingRightUpdatesScrollValueIfNeeded() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };
         var changeCount = 0;

         viewPort.CollectionChanged += (sender, e) => changeCount += 1;

         viewPort.Scroll.Execute(Direction.Right);

         Assert.Equal(1, viewPort.ScrollValue);
         Assert.NotEqual(0, changeCount);
      }

      [Fact]
      public void ScrollingRightAndLeftCancel() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         loadedFile.Contents[3] = 0x10;
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };

         viewPort.Scroll.Execute(Direction.Left);
         viewPort.Scroll.Execute(Direction.Right);

         Assert.Equal(0x10, viewPort[3, 0].Value);
      }

      [Fact]
      public void EmptyFileMaximumScrollMatchesMinimumScroll() {
         var viewPort = new ViewPort();

         Assert.Equal(viewPort.MinimumScroll, viewPort.MaximumScroll);
      }

      [Fact]
      public void EmptyFileCannotScroll() {
         var viewPort = new ViewPort();

         Assert.False(viewPort.Scroll.CanExecute(Direction.Left));
         Assert.False(viewPort.Scroll.CanExecute(Direction.Right));
         Assert.False(viewPort.Scroll.CanExecute(Direction.Up));
         Assert.False(viewPort.Scroll.CanExecute(Direction.Down));
      }

      [Fact]
      public void NotifyCollectionChangeAfterScrolling() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };
         var propertyNotifications = new List<string>();
         viewPort.PropertyChanged += (sender, e) => propertyNotifications.Add(e.PropertyName);
         int collectionNotifications = 0;
         viewPort.CollectionChanged += (sender, e) => collectionNotifications++;

         viewPort.Scroll.Execute(Direction.Down);

         Assert.Equal(1, collectionNotifications);
      }

      [Fact]
      public void ViewPort_GotoWithOffset_Goto() {
         var test = new BaseViewModelTestClass();
         test.ViewPort.Edit("@100+10 ");
         Assert.Equal(0x110, test.ViewPort.ConvertViewPointToAddress(test.ViewPort.SelectionStart));
      }

      [Fact]
      public void ViewPort_GotoNonExistingAnchorWithSizeSpecified_AnchorCreated() {
         var test = new BaseViewModelTestClass();
         test.ViewPort.Edit("@newAnchor(10) ");
         var destination = test.Model.GetAddressFromAnchor(new ModelDelta(), -1, "newAnchor");
         Assert.NotEqual(Pointer.NULL, destination);
      }

      [Fact]
      public void ScrollWithSingleTableView_SelectCellBeforeStartOfTable_FirstCellOfTableSelected() {
         var scroll = new ScrollRegion { AllowSingleTableMode = true, Width = 16, Height = 16 };
         var select = new Selection(scroll, new PokemonModel(new byte[0x200]), new ChangeHistory<ModelDelta>(p => p));
         scroll.SetTableMode(8, 24);

         select.SelectionStart = new Point(4, 0);

         Assert.Equal(new Point(8, 0), select.SelectionStart);
      }

      [Fact]
      public void ScrollWithSingleTableView_SelectionEndBeforeStartOfTable_FirstCellOfTableSelected() {
         var scroll = new ScrollRegion { AllowSingleTableMode = true, Width = 16, Height = 16 };
         var select = new Selection(scroll, new PokemonModel(new byte[0x200]), new ChangeHistory<ModelDelta>(p => p));
         scroll.SetTableMode(8, 24);

         select.SelectionStart = new Point(4, 0);
         select.SelectionEnd = new Point(4, 0);

         Assert.Equal(new Point(8, 0), select.SelectionEnd);
      }

      [Fact]
      public void ScrollWithSingleTableView_SelectAfterTable_CoerceSelectionIntoTable() {
         var scroll = new ScrollRegion { AllowSingleTableMode = true, Width = 16, Height = 16 };
         var select = new Selection(scroll, new PokemonModel(new byte[0x200]), new ChangeHistory<ModelDelta>(p => p));
         scroll.SetTableMode(8, 24);

         select.SelectionStart = new Point(0, 2);
         Assert.Equal(new Point(15, 1), select.SelectionStart);
      }

      [Fact]
      public void ScrollWithinSingleTableView_SelectEndAfterTable_CoerceSelectionEndIntoTable() {
         var scroll = new ScrollRegion { AllowSingleTableMode = true, Width = 16, Height = 16 };
         var select = new Selection(scroll, new PokemonModel(new byte[0x200]), new ChangeHistory<ModelDelta>(p => p));
         scroll.SetTableMode(8, 24);

         select.SelectionEnd = new Point(0, 2);
         Assert.Equal(new Point(15, 1), select.SelectionEnd);
      }

      [Fact]
      public void ClosedTools_SelectPointer_AnchorTextUpdates() {
         ViewPort.Tools.SelectedIndex = -1;
         ViewPort.Edit("@100 ^anchor @00 <100> ");

         ViewPort.Goto.Execute(0);

         Assert.True(ViewPort.AnchorTextVisible);
         Assert.Equal("^anchor", ViewPort.AnchorText);
      }

      [Fact]
      public void FindBytes_2Matches_BothGetHighlighted() {
         Model.WriteMultiByteValue(0x10, 4, Token, 0x12345678);
         Model.WriteMultiByteValue(0x24, 4, Token, 0x12345678);

         ViewPort.FindBytes = new byte[] { 0x78, 0x56, 0x34, 0x12 };

         Assert.False(((None)ViewPort[0, 0].Format).IsSearchResult);
         Assert.False(((None)ViewPort[15, 15].Format).IsSearchResult);

         Assert.True(((None)ViewPort[0, 1].Format).IsSearchResult);
         Assert.True(((None)ViewPort[3, 1].Format).IsSearchResult);
         Assert.False(((None)ViewPort[4, 1].Format).IsSearchResult);

         Assert.False(((None)ViewPort[3, 2].Format).IsSearchResult);
         Assert.True(((None)ViewPort[4, 2].Format).IsSearchResult);
         Assert.True(((None)ViewPort[7, 2].Format).IsSearchResult);
         Assert.False(((None)ViewPort[8, 2].Format).IsSearchResult);
      }

      [Fact]
      public void GotoTwice_GoBack_GoBack() {
         ViewPort.Goto.Execute(0x24);
         ViewPort.Goto.Execute(0x24);

         ViewPort.Back.Execute();

         Assert.Equal(0, ViewPort.DataOffset);
      }

      [Fact]
      public void EditToExpand_Undo_SizeResets() {
         var initialCount = Model.Count;
         ViewPort.Goto.Execute(Model.Count - 1);
         ViewPort.Edit("AA AA AA AA AA AA AA AA ");

         ViewPort.Undo.Execute();

         Assert.Equal(initialCount, Model.Count);
      }
   }
}
