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
   }
}
