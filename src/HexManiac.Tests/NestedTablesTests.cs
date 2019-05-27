using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System.Collections.Generic;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class NestedTablesTests {
      private readonly ViewPort viewPort;
      private readonly PokemonModel model;
      private readonly ModelDelta token = new ModelDelta();
      private readonly byte[] data = new byte[0x200];
      private readonly List<string> errors = new List<string>();

      public NestedTablesTests() {
         model = new PokemonModel(data);
         viewPort = new ViewPort("file.txt", model) { Width = 0x10, Height = 0x10 };
         viewPort.OnError += (sender, e) => errors.Add(e);
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
   }
}
