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
         Assert.Equal("b", format.ThisCharacter);
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
         Assert.Equal("3", format.ThisCharacter);
         Assert.Equal(new Point(2, 1), viewPort.SelectionStart);
      }

      [Fact]
      public void AsciiText_TypeSpace_NoChange() {
         ViewPort.Edit("^test`asc`7  ");
         Assert.Equal(0, Model[0]);
         Assert.Equal(0, ViewPort.ConvertViewPointToAddress(ViewPort.SelectionStart));
      }

      [Fact]
      public void AsciiText_EscapeCharacter_EditCharacter() {
         ViewPort.Edit("^test`asc`7 \\");
         var format = (UnderEdit)ViewPort[0, 0].Format;
         Assert.Equal("\\", format.CurrentText);
      }

      [Fact]
      public void AsciiText_EscapeSpace_OneChange() {
         ViewPort.Edit("^test`asc`7  \\ ");
         Assert.Equal((byte)' ', Model[0]);
         Assert.Equal(1, ViewPort.ConvertViewPointToAddress(ViewPort.SelectionStart));
      }

      [Fact]
      public void AsciiText_NoEscapeAnchor_AnchorEdit() {
         ViewPort.Edit("^test`asc`7 ^");
         Assert.IsType<UnderEdit>(ViewPort[0, 0].Format);
      }

      [Fact]
      public void AsciiText_EscapeAnchor_WriteCharacter() {
         ViewPort.Edit("^test`asc`7 \\^");
         Assert.Equal((byte)'^', Model[0]);
         Assert.Equal(1, ViewPort.ConvertViewPointToAddress(ViewPort.SelectionStart));
      }

      [Fact]
      public void AsciiText_EscapeEscape_WriteOneCharacter() {
         ViewPort.Edit("^test`asc`7 \\\\");
         Assert.Equal((byte)'\\', Model[0]);
         Assert.Equal(1, ViewPort.ConvertViewPointToAddress(ViewPort.SelectionStart));
      }

      [Fact]
      public void AsciiFormat_ZeroLength_ConvertToLength1() {
         ViewPort.Edit("^test`asc`0 ");
         Assert.Equal(1, Model.GetNextRun(0).Length);
      }

      // TODO 'copy' should work correctly when including escape characters, like a leading space, ^, or \
   }
}
