using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class EggMoveTests {
      private readonly byte[] data;
      private readonly PokemonModel model;
      private readonly ViewPort viewPort;

      public EggMoveTests() {
         data = Enumerable.Range(0, 0x200).Select(i => (byte)0xFF).ToArray();
         model = new PokemonModel(data);
         viewPort = new ViewPort("file.gba", model);

         viewPort.Goto.Execute("000080");
         viewPort.Edit("^pokenames[name\"\"8]8 \"Bob\" \"Steve\" \"Carl\" \"Sam\" \"Bryan\" \"Ryan\" \"Ian\" \"Matt\"");

         viewPort.Goto.Execute("000100");
         viewPort.Edit("^movenames[name\"\"8]8 \"Fire\" \"Water\" \"Earth\" \"Wind\" \"Light\" \"Dark\" \"Normal\" \"Magic\"");

         viewPort.Goto.Execute("000000");
      }

      [Fact]
      public void CanCreateEggMoveStream() {
         viewPort.Edit("^eggmoves`egg` ");

         Assert.Equal(2, model.GetNextRun(0).Length);
      }

      [Fact]
      public void CanSeeEggMoveStreamWithCorrectFormat() {
         var token = new ModelDelta();
         model.WriteMultiByteValue(0, 2, token, EggMoveRun.MagicNumber + 2); // Carl
         model.WriteMultiByteValue(2, 2, token, 3);                          // Wind

         viewPort.Edit("^eggmoves`egg` ");

         Assert.Equal(6, model.GetNextRun(0).Length);
         var section = (EggSection)viewPort[1, 0].Format;
         var item = (EggItem)viewPort[2, 0].Format;
         var endSection = (EggSection)viewPort[5, 0].Format;
         Assert.Equal("[Carl]", section.SectionName);
         Assert.Equal("Wind", item.ItemName);
         Assert.Equal("[]", endSection.SectionName);
      }

      [Fact]
      public void SelectionDoneInPairs() {
         var token = new ModelDelta();
         model.WriteMultiByteValue(0, 2, token, EggMoveRun.MagicNumber + 2); // Carl
         model.WriteMultiByteValue(2, 2, token, 3);                          // Wind
         viewPort.Edit("^eggmoves`egg` ");

         viewPort.SelectionStart = new Point(2, 0); // should select "Wind"
         Assert.True(viewPort.IsSelected(new Point(3, 0)));

         viewPort.MoveSelectionStart.Execute(Direction.Right); // should select "[]"
         Assert.True(viewPort.IsSelected(new Point(4, 0)));
         Assert.True(viewPort.IsSelected(new Point(5, 0)));
      }

      [Fact]
      public void CanEditEggStreamManually() {
         var token = new ModelDelta();
         model.WriteMultiByteValue(0, 2, token, EggMoveRun.MagicNumber + 2); // Carl
         model.WriteMultiByteValue(2, 2, token, 3);                          // Wind
         viewPort.Edit("^eggmoves`egg` ");

         viewPort.Edit("Dark ");
         Assert.Equal(5, model[0]);

         viewPort.Edit("[Bryan]");
         Assert.Equal(EggMoveRun.MagicNumber + 4, model.ReadMultiByteValue(2, 2));
      }

      [Fact]
      public void CanCopyPaste() {
         var fileSystem = new StubFileSystem();
         var token = new ModelDelta();
         model.WriteMultiByteValue(0, 2, token, EggMoveRun.MagicNumber + 2); // Carl
         model.WriteMultiByteValue(2, 2, token, 3);                          // Wind
         viewPort.Edit("^eggmoves`egg` ");

         viewPort.SelectionStart = new Point(0, 0);
         viewPort.SelectionEnd = new Point(2, 0);
         viewPort.Copy.Execute(fileSystem);

         Assert.Equal("^eggmoves`egg` [Carl] Wind", fileSystem.CopyText.value);
      }
   }
}
