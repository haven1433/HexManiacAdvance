using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class AsciiRunTests : BaseViewModelTestClass {
      [Fact]
      public void CanCreateAsciiRun() {
         var (model, viewPort) = (Model, ViewPort);
         model[0x10] = (byte)'a';
         model[0x11] = (byte)'b';
         model[0x12] = (byte)'c';
         model[0x13] = (byte)'d';

         viewPort.SelectionStart = new Point(0, 1);
         viewPort.Edit("^data`asc`4 ");

         var format = (Ascii)viewPort[1, 1].Format;
         Assert.Equal('b', format.ThisCharacter);
      }

      [Fact]
      public void CanEditAsciiRun() {
         var (model, viewPort) = (Model, ViewPort);
         model[0x10] = (byte)'a';
         model[0x11] = (byte)'b';
         model[0x12] = (byte)'c';
         model[0x13] = (byte)'d';

         model.ObserveRunWritten(new ModelDelta(), new AsciiRun(model, 0x10, 4));

         viewPort.SelectionStart = new Point(1, 1);
         viewPort.Edit("3");

         var format = (Ascii)viewPort[1, 1].Format;
         Assert.Equal('3', format.ThisCharacter);
         Assert.Equal(new Point(2, 1), viewPort.SelectionStart);
      }
   }
}
