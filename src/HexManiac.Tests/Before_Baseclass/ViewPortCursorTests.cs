using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.Visitors;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class ViewPortCursorTests {
      [Fact]
      public void CursorCanMoveAllFourDirections() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };
         viewPort.SelectionStart = new Point(3, 0);

         viewPort.MoveSelectionStart.Execute(Direction.Right);
         viewPort.MoveSelectionStart.Execute(Direction.Down);
         viewPort.MoveSelectionStart.Execute(Direction.Left);
         viewPort.MoveSelectionStart.Execute(Direction.Up);

         Assert.Equal(new Point(3, 0), viewPort.SelectionStart);
      }

      [Fact]
      public void CursorCannotMoveAboveTopRow() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };
         viewPort.SelectionStart = new Point(3, 0);

         viewPort.MoveSelectionStart.Execute(Direction.Up);

         Assert.Equal(new Point(0, 0), viewPort.SelectionStart); // coerced to first byte
      }

      [Fact]
      public void MovingCursorRightFromRightEdgeMovesToNextLine() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { PreferredWidth = -1, Width = 5, Height = 5 };
         viewPort.SelectionStart = new Point(4, 0);

         viewPort.MoveSelectionStart.Execute(Direction.Right);

         Assert.Equal(new Point(0, 1), viewPort.SelectionStart);
      }

      [Fact]
      public void MovingCursorDownFromBottomRowScrolls() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { PreferredWidth = -1, Width = 5, Height = 4 };
         viewPort.SelectionStart = new Point(0, 3);

         viewPort.MoveSelectionStart.Execute(Direction.Down);

         Assert.Equal(1, viewPort.ScrollValue);
      }

      [Fact]
      public void CursorCanMoveOutsideDataRangeButNotOutsideScrollRange() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { PreferredWidth = -1, Width = 5, Height = 5 };
         viewPort.Scroll.Execute(Direction.Right);
         viewPort.SelectionStart = new Point(0, 0);

         viewPort.MoveSelectionStart.Execute(Direction.Up);
         viewPort.MoveSelectionStart.Execute(Direction.Up);

         Assert.Equal(new Point(4, 0), viewPort.SelectionStart);
         Assert.Equal(0, viewPort.ScrollValue);

         viewPort.SelectionStart = new Point(4, 4);
         viewPort.MoveSelectionStart.Execute(Direction.Down);
         viewPort.MoveSelectionStart.Execute(Direction.Down);

         Assert.Equal(new Point(4, 4), viewPort.SelectionStart);
         Assert.Equal(1, viewPort.ScrollValue); // I can scroll lower using Scroll.Execute, but I cannot select lower.
      }

      [Fact]
      public void MoveSelectionEndWorks() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };
         viewPort.SelectionStart = new Point(0, 0);

         viewPort.MoveSelectionStart.Execute(Direction.Right);
         viewPort.MoveSelectionEnd.Execute(Direction.Down);

         Assert.Equal(new Point(1, 1), viewPort.SelectionEnd);
      }

      [Fact]
      public void SetSelectionStartResetsSelectionEnd() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };
         viewPort.SelectionStart = new Point(0, 0);

         viewPort.SelectionEnd = new Point(3, 3);
         viewPort.SelectionStart = new Point(1, 0);

         Assert.Equal(new Point(1, 0), viewPort.SelectionEnd);
      }

      [Fact]
      public void ScrollingUpdatesSelection() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };

         viewPort.SelectionStart = new Point(0, 2);
         viewPort.SelectionEnd = new Point(4, 2);
         viewPort.Scroll.Execute(Direction.Down);

         Assert.Equal(new Point(0, 1), viewPort.SelectionStart);
         Assert.Equal(new Point(4, 1), viewPort.SelectionEnd);
      }

      [Fact]
      public void ForwardSelectionWorks() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };

         viewPort.SelectionStart = new Point(2, 1);
         viewPort.SelectionEnd = new Point(3, 3);

         Assert.True(viewPort.IsSelected(new Point(4, 2)));
      }

      [Fact]
      public void BackSelectionWorks() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };

         viewPort.SelectionStart = new Point(3, 3);
         viewPort.SelectionEnd = new Point(2, 1);

         Assert.True(viewPort.IsSelected(new Point(4, 2)));
      }

      /// <remarks>
      /// Scrolling Down makes you see lower data.
      /// Scrolling Up makes you see higher data.
      /// Scrolling Left makes you see one more byte, left of what's currently in view.
      /// Scrolling Right makes you see one more byte, right of what's currently in view.
      /// </remarks>
      [Fact]
      public void ScrollingBeforeStartOfDataMovesSelectionOnlyWhenDataMoves() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { PreferredWidth = -1, Width = 5, Height = 5 };

         viewPort.Scroll.Execute(Direction.Right); // move the first byte out of view
         viewPort.Scroll.Execute(Direction.Up);    // scroll up, so we can see the first byte again

         // Example of what it should look like right now:
         // .. .. .. 00 <- this is the top line in the view
         // 00 00 00 00
         // 00 00 00 00
         // 00 00 00 00
         // 00 00 00 00

         viewPort.Scroll.Execute(Direction.Left); // try to scroll further. Should fail, because then the whole top row would be empty.
         Assert.Equal(new Point(4, 0), viewPort.SelectionStart); // first byte of data should still be selected.
      }

      [Fact]
      public void ChangingWidthKeepsSameDataSelected() {
         var loadedFile = new LoadedFile("test", new byte[25]);
         var viewPort = new ViewPort(loadedFile) { PreferredWidth = -1, Width = 5, Height = 5 };
         viewPort.SelectionEnd = new Point(2, 2); // 13 cells selected
         viewPort.Width += 1;

         Assert.Equal(new Point(0, 2), viewPort.SelectionEnd); // 13 cells selected
      }

      [Fact]
      public void CannotSelectBeforeFirstByte() {
         var loadedFile = new LoadedFile("test", new byte[30]);
         var viewPort = new ViewPort(loadedFile) { Width = 5, Height = 5 };

         // view the cell left of the first byte
         viewPort.Scroll.Execute(Direction.Left);

         // try to select the cell left of the first byte
         viewPort.MoveSelectionStart.Execute(Direction.Left);

         // assert that the selection is still on the first byte, not the first cell
         Assert.Equal(new Point(1, 0), viewPort.SelectionStart);
      }

      [Fact]
      public void CannotSelectFarAfterLastByte() {
         var loadedFile = new LoadedFile("test", new byte[20]);
         var viewPort = new ViewPort(loadedFile) { PreferredWidth = -1, Width = 5, Height = 5 };

         viewPort.SelectionStart = new Point(4, 4);

         Assert.Equal(new Point(0, 4), viewPort.SelectionStart);
      }

      [Fact]
      public void MovingSelectionInAnUnsizedPanelAutoSizesThePanel() {
         var viewPort = new ViewPort();

         viewPort.SelectionStart = new Point(4, 4);

         Assert.NotEqual(0, viewPort.Width);
         Assert.NotEqual(0, viewPort.Height);
      }

      [Fact]
      public void CannotMoveSelectEndFarPassedEndOfFile() {
         var selection = new Selection(new ScrollRegion { DataLength = 8 }, new BasicModel(new byte[8]), default);

         selection.SelectionEnd = new Point(3, 3);

         Assert.Equal(new Point(0, 2), selection.SelectionEnd);
      }

      [Fact]
      public void CanExpandSelection() {
         CreateStandardTestSetup(out var viewPort, out var model, out var data);

         viewPort.Edit("FF @00 ^word\"\" \"bob\"");
         viewPort.SelectionStart = new Point(1, 0);
         viewPort.ExpandSelection(1, 0);

         Assert.True(viewPort.IsSelected(new Point(0, 0)));
         Assert.True(viewPort.IsSelected(new Point(1, 0)));
         Assert.True(viewPort.IsSelected(new Point(2, 0)));
         Assert.True(viewPort.IsSelected(new Point(3, 0)));
         Assert.False(viewPort.IsSelected(new Point(4, 0)));
      }

      [Theory]
      [InlineData(0)]
      [InlineData(1)]
      [InlineData(2)]
      [InlineData(3)]
      public void SelectingAnyOfAPointerSelectsAllOfAPointer(int index) {
         CreateStandardTestSetup(out var viewPort, out var model, out var data);

         viewPort.Edit("<000100>");
         viewPort.SelectionStart = new Point(index, 0);

         Assert.True(viewPort.IsSelected(new Point(0, 0)));
         Assert.True(viewPort.IsSelected(new Point(1, 0)));
         Assert.True(viewPort.IsSelected(new Point(2, 0)));
         Assert.True(viewPort.IsSelected(new Point(3, 0)));
      }

      [Fact]
      public void SelectLeftSelectsWholePointer() {
         CreateStandardTestSetup(out var viewPort, out var model, out var data);

         viewPort.SelectionStart = new Point(4, 0);
         viewPort.Edit("<000100>");
         viewPort.SelectionStart = new Point(8, 0);
         viewPort.MoveSelectionStart.Execute(Direction.Left);

         Assert.True(viewPort.IsSelected(new Point(4, 0)));
         Assert.True(viewPort.IsSelected(new Point(5, 0)));
         Assert.True(viewPort.IsSelected(new Point(6, 0)));
         Assert.True(viewPort.IsSelected(new Point(7, 0)));
      }

      [Fact]
      public void SelectRightSelectsWholePointer() {
         CreateStandardTestSetup(out var viewPort, out var model, out var data);

         viewPort.SelectionStart = new Point(4, 0);
         viewPort.Edit("<000100>");
         viewPort.SelectionStart = new Point(3, 0);
         viewPort.MoveSelectionStart.Execute(Direction.Right);

         Assert.True(viewPort.IsSelected(new Point(4, 0)));
         Assert.True(viewPort.IsSelected(new Point(5, 0)));
         Assert.True(viewPort.IsSelected(new Point(6, 0)));
         Assert.True(viewPort.IsSelected(new Point(7, 0)));
      }

      [Fact]
      public void SelectUpSelectsWholePointer() {
         CreateStandardTestSetup(out var viewPort, out var model, out var data);

         viewPort.SelectionStart = new Point(4, 0);
         viewPort.Edit("<000100>");
         viewPort.SelectionStart = new Point(5, 1);
         viewPort.MoveSelectionStart.Execute(Direction.Up);

         Assert.True(viewPort.IsSelected(new Point(4, 0)));
         Assert.True(viewPort.IsSelected(new Point(5, 0)));
         Assert.True(viewPort.IsSelected(new Point(6, 0)));
         Assert.True(viewPort.IsSelected(new Point(7, 0)));
      }

      [Fact]
      public void SelectDownSelectsWholePointer() {
         CreateStandardTestSetup(out var viewPort, out var model, out var data);

         viewPort.SelectionStart = new Point(4, 1);
         viewPort.Edit("<000100>");
         viewPort.SelectionStart = new Point(5, 0);
         viewPort.MoveSelectionStart.Execute(Direction.Down);

         Assert.True(viewPort.IsSelected(new Point(4, 1)));
         Assert.True(viewPort.IsSelected(new Point(5, 1)));
         Assert.True(viewPort.IsSelected(new Point(6, 1)));
         Assert.True(viewPort.IsSelected(new Point(7, 1)));
      }

      [Fact]
      public void HighlightLeftSelectsWholePointer() {
         CreateStandardTestSetup(out var viewPort, out var model, out var data);

         viewPort.SelectionStart = new Point(4, 1);
         viewPort.Edit("<000100>");
         viewPort.SelectionStart = new Point(6, 1);
         viewPort.SelectionEnd = new Point(2, 1);

         Assert.True(viewPort.IsSelected(new Point(4, 1)));
         Assert.True(viewPort.IsSelected(new Point(5, 1)));
         Assert.True(viewPort.IsSelected(new Point(6, 1)));
         Assert.True(viewPort.IsSelected(new Point(7, 1)));
      }

      [Fact]
      public void HighlightRightSelectsWholePointer() {
         CreateStandardTestSetup(out var viewPort, out var model, out var data);

         viewPort.SelectionStart = new Point(4, 1);
         viewPort.Edit("<000100>");
         viewPort.SelectionStart = new Point(6, 1);
         viewPort.SelectionEnd = new Point(10, 1);

         Assert.True(viewPort.IsSelected(new Point(4, 1)));
         Assert.True(viewPort.IsSelected(new Point(5, 1)));
         Assert.True(viewPort.IsSelected(new Point(6, 1)));
         Assert.True(viewPort.IsSelected(new Point(7, 1)));
      }

      [Fact]
      public void ContextMenuContainsCopyPaste() {
         CreateStandardTestSetup(out var viewPort, out var model, out var data);

         viewPort.SelectionStart = new Point(2, 2);
         viewPort.SelectionEnd = new Point(5, 2);
         var items = viewPort.GetContextMenuItems(viewPort.SelectionStart);
         items.Single(item => item.Text == "Copy");
         items.Single(item => item.Text == "Paste");

         viewPort.Edit("<000100>");
         viewPort.SelectionStart = new Point(3, 2);
         items = viewPort.GetContextMenuItems(viewPort.SelectionStart);
         items.Single(item => item.Text == "Copy");
         items.Single(item => item.Text == "Paste");

         viewPort.ClearAnchor();
         viewPort.Model.WriteMultiByteValue(0x22, 4, new ModelDelta(), 0x000000FF);
         viewPort.Edit("^text\"\" Hello World!\"");
         viewPort.SelectionStart = new Point(5, 2);
         viewPort.ExpandSelection(5, 2);
         items = viewPort.GetContextMenuItems(viewPort.SelectionStart);
         items.Single(item => item.Text == "Copy");
         items.Single(item => item.Text == "Paste");
      }

      [Fact]
      public void CanForkData() {
         CreateStandardTestSetup(out var viewPort, out var model, out var data);
         viewPort.Edit("<000008> <000008> FF @08 ^text\"\" Test\" @00");
         viewPort.SelectionStart = new Point();
         var items = viewPort.GetContextMenuItems(new Point());

         var group = (ContextItemGroup)items.Single(menuItem => menuItem.Text == "Pointer Operations");
         group.Single(item => item.Text.StartsWith("Repoint")).Command.Execute();

         Assert.NotEqual(0x08, model.ReadPointer(0));
         var originalRun = (PCSRun)model.GetNextRun(8);
         var newRun = (PCSRun)model.GetNextRun(model.ReadPointer(0));
         Assert.Equal(originalRun.SerializeRun(), newRun.SerializeRun());
      }

      [Fact]
      public void AnchorWithData_SelectMiddleOfAnchor_CanSeeAnchorContents() {
         var test = new BaseViewModelTestClass();
         test.SetFullModel(0xFF);

         test.ViewPort.Edit("^text\"\" \"Hello, World\"");
         test.ViewPort.Goto.Execute(5);

         Assert.True(test.ViewPort.AnchorTextVisible);
         Assert.Equal("^text\"\"", test.ViewPort.AnchorText);
      }

      //[Fact]
      //public void CompressedData_SelectLeftFromEnd_CursorMoves() {
      //   var raw = "10 00 08 00 3C 00 00 F0 01 F0 01 F0 01 00 01 CC C0 14 CC BC BB 80 0F C0 00 03 AD 00 00 00 DD AB C0 CC BB CB BC 01 BB BB 9C BB BB CC 09 10 18 40 DC 00 03 AA 0D 00 00 CA 0C FC 30 0B 20 14 F0 01 F0 01 90 5C 00 3B AD DA 08 0D 00 D0 BB 00 41 CD CC DD 0E 00 D0 CC CC 20 03 F0 2A 20 01 DD F6 10 A0 10 04 F0 01 D0 01 D0 30 B7 60 03 BD C8 00 12 00 D7 D0 CB 00 0A 5B BD BB 00 CB CC AA CB 9C 59 CA 99 01 55 55 9B 55 55 55 59 10 03 00 45 44 54 55 44 44 44 45 81 00 03 99 C9 CC 09 55 95 20 03 00 C9 55 55 D9 DD 55 D5 AD 00 AA 55 AD AA AA 55 CD AA 08 BD 55 B9 BB 21 1F CC 00 DD 00 DD AA DD AA BB BB AC AA 00 DC DD BD 9B E9 BA CB 3D 1A F9 BB BB 30 01 10 1F 9D 00 03 BB 00 9D 99 99 CD BB AA CC DE 80 00 6F CF CB DC D9 BB CB 09 22 00 CB 11 3E D0 CC CD 00 41 5D 01 DD 00 CD 5D CC 0D CD 20 07 C0 01 00 30 03 D0 CC 5D CC CC 0D 20 00 DD 00 03 55 C5 DC 00 55 41 D5 10 03 CC 0D 55 55 CD 50 03 E5 11 6E F0 01 A0 01 BD 5C 00 EE 45 00 EE 57 45 00 EE 44 30 03 5C 20 03 00 16 00 E1 A8 60 01 54 60 03 34 00 03 43 43 55 00 34 34 44 55 55 B9 99 99 01 55 95 DE DD 55 55 9F 01 21 2C D5 DC 90 7B 45 00 0F 01 02 CC EE 00 99 CB DD DF DD 99 99 9D 04 D9 CD CC 00 90 11 B7 1C C1 00 00 C0 11 D1 0D CD 11 91 14 CC CC 9C 01 CE DC 00 C3 CC 09 4F C9 00 0C CD DC 20 03 00 09 00 03 00 10 08 00 D0 DC 55 20 03 CD 5C 55 ED 00 EE 00 55 00 59 5D 01 69 01 87 9C 00 03 D4 30 73 20 03 CC 00 0C CD 50 03 45 55 1F 55 CC 54 11 10 40 E5 02 7E E0 03 21 FC DB 00 E3 00 03 34 00 FE 10 03 33 00 FE 00 03 20 34 93 00 03 09 BD 5B 94 00 40 33 00 E7 33 33 44 45 33 43 40 44 00 07 54 45 99 39 54 54 00 00 50 54 44 00 00 45 54 88 00 03 05 54 54 00 6B 45 55 C5 A5 00 07 55 00 07 55 54 00 08 54 01 12 08 05 00 00 99 10 5F 9C 1D D1 00 C9 FD DD FD BD BD 99 D9 00 CB D5 BA BB 9C 55 99 99 08 09 55 55 C9 00 AF 95 CC 59 19 55 95 99 10 E6 10 03 09 BD 00 EB 81 60 03 CB CC DD BB BC CC 02 7B 38 CB DC 02 70 00 28 10 03 45 CC 9D F0 00 03 00 F4 20 03 00 41 CC CC 59 00 B0 11 63 44 00 03 10 EB 54 55 C5 0C E8 81 7F 00 03 53 1E 80 70 03 10 80 01 40 80 10 03 18 88 01 80 88 88 0A 01 CD 5C 94 01 5A 09 20 03 C9 95 10 03 9C 00 60 03 90 23 A8 55 93 52 54 F9 00 03 E9 00 03 90 93 3D 09 FD 00 0D 00 9E FE 09 00 9D EE 00 99 00 CE D9 BB D9 CC CC 14 BB BD 99 00 04 00 00 04 90 95 C6 02 E3 13 07 C9 BC BB 01 E3 04 19 CC 03 DC BB 9B 99 CD 9B 01 DB 00 1E 28 CC B9 00 E7 BB 00 E7 BC CB CC 30 CC CD 00 0F 80 01 22 CC CC 72 54 77 01 19 00 30 03 99 00 03 D9 09 08 CC CC 9D 9D 01 31 DD 22 C2 00 9C DD 77 77 9C 99 50 D5 20 DC 10 00 03 00 00 C5 0C 0D 00 00 CD DD 9F 99 99 F9 9F 08 DD CC EC 9E 00 6B E9 D9 CD 00 DC 9D 80 88 18 01 80 81 00 11 01 10 18 18 11 88 81 00 11 11 DD 11 87 11 9E 11 00 88 11 9D 11 81 18 19 71 24 71 17 B4 32 DD AB 02 10 BB 00 6C CD 10 03 04 DB C9 00 C3 20 19 DD CC 11 BD AA AA 20 01 BB BB DD 13 B6 D0 20 04 33 BF 99 00 C6 9C BB AA AA 40 B9 20 03 BB 9B BB BB 99 99 DD 00 18 30 0A BB 10 C8 10 04 00 F7 CC 00 02 42 7C 00 03 77 BB CC 7C 30 03 DC 81 00 DF 7C 77 77 2C 77 77 F0 01 82 60 01 92 00 77 77 97 80 03 D2 40 09 00 03 9C 77 77 CD CD 77 1A 27 CD 9C 00 7E E5 4E D9 00 B1 CC 11 DD DD BC 10 7B 11 11 71 00 F9 00 81 01 80 11 8D 01 D0 11 0B 1D 00 BD D1 04 6B DB 00 03 00 35 72 9B 10 2C 13 63 13 93 90 C2 02 2C 29 5E C2 01 DA 22 00 15 22 BB 10 01 10 E3 CC 28 BC BC 00 4B CB 00 BA CC 9C 22 43 C2 00 03 22 22 92 99 00 03 00 7B 20 22 9B 01 12 A9 AA AA BA B9 DE 00 07 05 F3 BB 01 CC 61 C0 00 5A 00 FB 2D 5A 72 00 03 22 01 F6 01 CF 9C C0 03 9D 50 99 10 E1 22 10 03 22 27 27 22 1A 22 72 72 00 5B 10 07 99 00 61 CC 84 11 59 77 D7 CC 9C 00 03 CC 27 B0 02 1C 92 40 53 33 32 9C 09 CC DC 51 C9 12 52 BC 12 4A 99 99 D9 26 57 FF D1 0C F6 6B F0 01 F0 01 A0 01 61 0B 86 2F 62 FF 98 02 EC CC D0 12 BB 00 F9 FD CC CD 00 DC EF FD CF FD 99 F9 9E 7D F9 03 3C 20 23 22 BB 02 9E 04 E6 00 01 AD 2F 00 DF 00 90 9E 90 97 11 7B 03 28 F0 67 C0 47 64 11 42 BC BB 9E 99 DD CC 7F FF 12 6B D0 1F 10 47 10 4F 10 18 F0 EB F0 01 80 D0 01"
      //      .Split(" ").Select(text => byte.Parse(text, System.Globalization.NumberStyles.HexNumber)).ToArray();
      //   var test = new BaseViewModelTestClass();
      //   test.Model.ExpandData(test.ViewPort.CurrentChange, 0x600);
      //   Array.Copy(raw, 0, test.Model.RawData, 7, raw.Length);
      //   test.Model.ObserveRunWritten(test.ViewPort.CurrentChange, new LZRun(test.Model, 7));
      //   test.ViewPort.Refresh();

      //   test.ViewPort.SelectionStart = new Point(7, 0);
      //   test.ViewPort.MoveSelectionStart.Execute(Direction.End);
      //   test.ViewPort.MoveSelectionStart.Execute(Direction.Right);
      //   test.ViewPort.MoveSelectionStart.Execute(Direction.Left);
      //   test.ViewPort.MoveSelectionStart.Execute(Direction.Left);

      //   Assert.Equal(14, test.ViewPort.SelectionStart.X);
      //   Assert.Equal(14, test.ViewPort.SelectionEnd.X);
      //}

      [Fact]
      public void SelectDataInBottomRow_MoveSelectionEndDown_DataScrolls() {
         var test = new BaseViewModelTestClass();

         test.ViewPort.SelectionStart = new Point(0, 15);
         test.ViewPort.SelectionEnd = new Point(0, 16);

         Assert.Equal(0x10, test.ViewPort.DataOffset);
      }

      [Fact]
      public void SelectDataInTopRow_MoveSelectionEndUp_DataScrolls() {
         var test = new BaseViewModelTestClass();
         test.ViewPort.Goto.Execute(0x10);

         test.ViewPort.SelectionStart = new Point(0, 0);
         test.ViewPort.SelectionEnd = new Point(0, -1);

         Assert.Equal(0, test.ViewPort.DataOffset);
      }

      [Fact]
      public void CursorAtEndOfFile_MakeFileSmaller_CursorMoves() {
         var test = new BaseViewModelTestClass();
         test.ViewPort.Goto.Execute(test.Model.Count - 1);

         test.Model.ContractData(test.Token, 0x100);
         test.ViewPort.Refresh();

         Assert.Equal(0x101, test.ViewPort.ConvertViewPointToAddress(test.ViewPort.SelectionStart));
      }

      [Fact]
      public void SelectByte_SetSelectionLength_SelectMoreBytes() {
         var test = new BaseViewModelTestClass();

         test.ViewPort.SelectedAddress = "000100";
         test.ViewPort.SelectedLength = "3";

         Assert.Equal(0x000100, test.ViewPort.ConvertViewPointToAddress(test.ViewPort.SelectionStart));
         Assert.Equal(0x000102, test.ViewPort.ConvertViewPointToAddress(test.ViewPort.SelectionEnd));
      }

      [Fact]
      public void SelectLeft_SetSelectionLength_ChangeSelectionEndToSelectionStart() {
         var test = new BaseViewModelTestClass();

         test.ViewPort.SelectionStart = new(2, 0);
         test.ViewPort.SelectionEnd = new(1, 0);
         test.ViewPort.SelectedLength = "3";

         Assert.Equal(1, test.ViewPort.ConvertViewPointToAddress(test.ViewPort.SelectionStart));
         Assert.Equal(3, test.ViewPort.ConvertViewPointToAddress(test.ViewPort.SelectionEnd));
      }

      private static void CreateStandardTestSetup(out ViewPort viewPort, out PokemonModel model, out byte[] data) {
         data = new byte[0x200];
         model = new PokemonModel(data);
         viewPort = AutoSearchTests.NewViewPort("file.txt", model);
         viewPort.Width = 0x10;
         viewPort.Height = 0x10;
      }
   }
}
