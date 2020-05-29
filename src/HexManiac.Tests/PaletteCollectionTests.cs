using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class PaletteCollectionTests : BaseViewModelTestClass {
      private readonly StubFileSystem fileSystem = new StubFileSystem();

      public PaletteCollectionTests() {
         ViewPort.Edit("^pal`ucp4`");
      }

      [Fact]
      public void CanCopy() {
         var collection = ViewPort.Tools.SpriteTool.Colors;

         collection.SelectionStart = 0;
         collection.Copy.Execute(fileSystem);

         Assert.Equal("0:0:0", fileSystem.CopyText);
      }

      [Fact]
      public void CanPaste() {
         var collection = ViewPort.Tools.SpriteTool.Colors;

         collection.SelectionStart = 0;
         fileSystem.CopyText = "1:1:1";
         collection.Paste.Execute(fileSystem);

         Assert.Equal(0b0_00001_00001_00001, Model.ReadMultiByteValue(0, 2));
      }
   }
}
