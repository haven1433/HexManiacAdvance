using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Collections.Generic;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class NestedTablesTests {
      private readonly ViewPort viewPort;
      private readonly PokemonModel model;
      private readonly ModelDelta token = new ModelDelta();
      private readonly byte[] data = new byte[0x200];
      private readonly List<string> messages = new List<string>();
      private readonly List<string> errors = new List<string>();

      public NestedTablesTests() {
         model = new PokemonModel(data);
         viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };
         viewPort.OnError += (sender, e) => errors.Add(e);
         viewPort.OnMessage += (sender, e) => messages.Add(e);
      }

      [Fact]
      public void SupportNestedTextStreams() {
         // can parse
         var info = ArrayRun.TryParse(model, "[description<\"\">]4", 0, null, out var table);
         Assert.False(info.HasError);

         model.ObserveAnchorWritten(token, "table", table);

         // displays pointers
         viewPort.Goto.Execute("000100");
         viewPort.Goto.Execute("000000");
         Assert.IsType<Pointer>(viewPort[4, 0].Format);

         // can't update to point to non-text
         viewPort.SelectionStart = new Point(5, 0);
         errors.Clear();
         viewPort.Edit("000100 "); // expected failure point
         Assert.Single(errors);

         // can update to point to text
         viewPort.SelectionStart = new Point(0, 4);
         viewPort.Edit("FF"); // have to put the FF first, or trying to create a text run will fail
         viewPort.SelectionStart = new Point(0, 4);
         viewPort.Edit("^text\"\" Hello World!\"");
         viewPort.SelectionStart = new Point(5, 0);
         errors.Clear();
         viewPort.Edit("000040 ");
         Assert.Empty(errors);

         // can update to point to null
         viewPort.SelectionStart = new Point(5, 0);
         errors.Clear();
         viewPort.Edit("null ");
         Assert.Empty(errors);
      }

      /// <summary>
      /// The user may want to update a pointer to a location where they know there is text,
      /// even if there's no text recognized there.
      /// In this case, there could be a pointer to NoInfoRun, or there could be no pointer at all.
      /// Either way, accept the change and automatically add the run based on what's pointed to.
      /// </summary>
      [Fact]
      public void SupportChangingPointerToWhereAFormatCouldBe() {
         viewPort.Edit("FF"); // have to put the FF first, or trying to create a text run will fail
         viewPort.MoveSelectionStart.Execute(Direction.Left);
         viewPort.Edit("^text1\"\" Hello World!\"");
         viewPort.SelectionStart = new Point(0, 0);
         viewPort.Edit("^ "); // remove the format. So we have text data, but no format for it.

         viewPort.SelectionStart = new Point(0, 2);
         viewPort.Edit("FF"); // have to put the FF first, or trying to create a text run will fail
         viewPort.MoveSelectionStart.Execute(Direction.Left);
         viewPort.Edit("^text2\"\" Other Text\"");
         viewPort.SelectionStart = new Point(0, 2);
         viewPort.Edit("^text2 "); // remove the format, but keep the anchor

         viewPort.SelectionStart = new Point(0, 4);
         viewPort.Edit("^table[description<\"\">]4 ");
         viewPort.Goto.Execute("000000");  // scroll back: making a table auto-scrolled
         viewPort.SelectionStart = new Point(0, 4);
         errors.Clear();

         // test 1: pointing to data that is the right format, but with no anchor, works
         viewPort.Edit("000000 ");
         Assert.Empty(errors);
         Assert.IsType<PCS>(viewPort[1, 0].Format);

         // test 2: pointing to data that is the right format, but with a NoInfo anchor, works
         viewPort.Edit("text2 ");
         Assert.Empty(errors);
         Assert.IsType<PCS>(viewPort[1, 2].Format);
      }

      /// <summary>
      /// When a run is added, automatically add children runs as needed.
      /// Note that removing this run doesn't remove the children run.
      /// </summary>
      [Fact]
      public void SupportAutomaticallyAddingFormatsBasedOnPointerFormat() {
         viewPort.Edit("FF"); // have to put the FF first, or trying to create a text run will fail
         viewPort.MoveSelectionStart.Execute(Direction.Left);
         viewPort.Edit("^text\"\" Some Text\"");
         viewPort.SelectionStart = new Point(0, 0);
         viewPort.Edit("^ "); // remove the anchor. Keeps the data though.

         viewPort.SelectionStart = new Point(0, 4);
         viewPort.Edit("00 00 00 08");
         viewPort.SelectionStart = new Point(0, 4);
         viewPort.Edit("^table[description<\"\">]4 "); // adding this should automatically add a format at 000000, since a pointer points there.

         viewPort.Goto.Execute("000000"); // scroll back to top
         Assert.IsType<PCSRun>(model.GetNextRun(0));
      }

      [Fact]
      public void AddTableSuchThatPointerLeadsToBadDataDoesNotAddFormat() {
         viewPort.Edit("10 00 00 08"); // point to second line, which is all zeros
         viewPort.SelectionStart = new Point(0, 0);
         viewPort.Edit("^table[description<\"\">]4 "); // adding this tries to add a format at the second line, but realizes that it's the wrong format, so it doesn't.

         Assert.IsType<None>(viewPort[1, 1].Format); // no PCS format was added
      }

      [Fact]
      public void TableToolAllowsEditingTextContent() {
         viewPort.Edit("FF"); // have to put the FF first, or trying to create a text run will fail
         viewPort.MoveSelectionStart.Execute(Direction.Left);
         viewPort.Edit("^text\"\" Some Text\"");

         viewPort.SelectionStart = new Point(0, 4);
         viewPort.Edit("^table[description<\"\">]4 <000000>"); // note that this auto-scrolls, since a table was created
         viewPort.FollowLink(0, 0);

         Assert.Equal(2, viewPort.Tools.TableTool.Children.Count);
         Assert.IsType<TextStreamArrayElementViewModel>(viewPort.Tools.TableTool.Children[1]);
      }

      [Fact]
      public void UpdateViaToolStreamFieldThatCausesMoveAlsoUpdatesToolPointerField() {
         viewPort.Edit("FF 00 23"); // put a valid end, then a spare byte, then some junk. This'll cause a move after adding enough characters.
         viewPort.SelectionStart = new Point(0, 0);
         viewPort.Edit("^text\"\" ?\"");

         viewPort.SelectionStart = new Point(0, 4);
         viewPort.Edit("^table[description<\"\">]4 <000000>"); // note that this auto-scrolls, since a table was created
         viewPort.SelectionStart = new Point(0, 0);
         var textViewModel = (TextStreamArrayElementViewModel)viewPort.Tools.TableTool.Children[1];

         // act: use the tool to change the content, forcing a repoint
         messages.Clear();
         textViewModel.Content = "Xyz";
         var pointerViewModel = (FieldArrayElementViewModel)viewPort.Tools.TableTool.Children[0];

         Assert.Single(messages);                               // we repointed
         Assert.NotEqual("<000000>", pointerViewModel.Content); // other tool field was updated
      }

      [Fact]
      public void SupportsUnnamedPlmStreams() {
         model.WriteMultiByteValue(0x100, 2, new ModelDelta(), 0x0202); // move two at level 1
         model.WriteMultiByteValue(0x102, 2, new ModelDelta(), 0x1404); // move four at level 10 (8+2, shifted once -> 14)
         model.WriteMultiByteValue(0x104, 2, new ModelDelta(), 0xFFFF); // end stream
         viewPort.Edit("<000100>"); // setup something to point at 000100, so we can have an unnamed stream there

         SetupMoveTable(0x20);
         viewPort.Goto.Execute("000100");

         errors.Clear();
         viewPort.Edit("^`plm`");

         Assert.Empty(errors);
         var run = model.GetNextRun(0x100);
         Assert.IsType<PLMRun>(run);
         Assert.Equal(6, run.Length);
         var format = (PlmItem)viewPort[1, 0].Format;
         Assert.Equal(1, format.Level);
         Assert.Equal(2, format.Move);
         Assert.Equal("Two", format.MoveName);

         viewPort.SelectionStart = new Point(1, 0);
         Assert.True(viewPort.IsSelected(new Point(0, 0)));
      }

      [Fact]
      public void PlmStreamAutocomplete() {
         SetupMoveTable(0x20); // goes from 20 to 60
         viewPort.Goto.Execute("000070");

         viewPort.Edit("FFFF");
         viewPort.SelectionStart = new Point(0, 0);
         viewPort.Edit("^stream`plm` 3 T"); // Two, Three
         var format = (UnderEdit)viewPort[0, 0].Format;
         Assert.Equal(2, format.AutocompleteOptions.Count);
      }

      [Fact]
      public void PlmStreamBackspaceWorks() {
         model.WriteMultiByteValue(0x100, 2, new ModelDelta(), 0x0202); // move two at level 1
         model.WriteMultiByteValue(0x102, 2, new ModelDelta(), 0x1404); // move four at level 10 (8+2, shifted once -> 14)
         model.WriteMultiByteValue(0x104, 2, new ModelDelta(), 0xFFFF); // end stream
         viewPort.Edit("<000100>"); // setup something to point at 000100, so we can have an unnamed stream there
         SetupMoveTable(0x20);
         viewPort.Goto.Execute("000100");
         errors.Clear();
         viewPort.Edit("^`plm`");

         viewPort.Goto.Execute("000102");
         viewPort.Edit(ConsoleKey.Backspace);
         var format = (UnderEdit)viewPort[2, 0].Format;
         Assert.Equal("10 Fou", format.CurrentText);
      }

      [Fact]
      public void PlmStreamsAppearInTextTool() {
         SetupMoveTable(0x20);
         SetupPlmStream(0x70, 4);
         viewPort.Goto.Execute("000000");

         viewPort.SelectionStart = new Point(2, 7);
         viewPort.FollowLink(2, 7);

         Assert.Equal(viewPort.Tools.IndexOf(viewPort.Tools.StringTool), viewPort.Tools.SelectedIndex);
         var itemCount = viewPort.Tools.StringTool.Content.Split(Environment.NewLine).Length;
         Assert.Equal(4, itemCount);

         viewPort.Tools.StringTool.ContentIndex = 17; // third line
         Assert.True(viewPort.IsSelected(new Point(4, 7)));
         Assert.True(viewPort.IsSelected(new Point(5, 7)));
      }

      // creates a move table that is 0x40 bytes long
      private void SetupMoveTable(int start) {
         viewPort.Goto.Execute(start.ToString("X6"));
         viewPort.Edit("^" + EggMoveRun.MoveNamesTable + "[name\"\"8]8 Zero\" One\" Two\" Three\" Four\" Five\" Six\" Seven\"");
      }

      // creates a plm stream named 'stream' that is <0x20 bytes long. Length should be <9.
      private void SetupPlmStream(int start, int length) {
         viewPort.Goto.Execute(start.ToString("X6"));
         viewPort.Edit("FFFF"); // make sure we terminate first. This will move as needed, but it allows us to add the stream cleanly by just editing inline.
         viewPort.Goto.Execute(start.ToString("X6"));
         viewPort.Edit("^stream`plm`");
         for (int i = 0; i < length; i++) {
            viewPort.Edit($"{i + 1} {i} ");
         }
      }
   }
}
