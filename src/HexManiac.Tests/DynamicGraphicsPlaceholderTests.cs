using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>Every object event whose graphics is one of the 16 graphics variables gets the "?" placeholder, on every map, whatever scripts say.</summary>
   [Collection("GraphicsVariableDisplay")]
   public class DynamicGraphicsPlaceholderTests : BaseViewModelTestClass {
      public DynamicGraphicsPlaceholderTests() : base(0x400) { }

      private static readonly IPixelViewModel Default = new ReadonlyPixelViewModel(16, 32);

      [Theory]
      [InlineData(239, false)]
      [InlineData(240, true)]
      [InlineData(255, true)]
      [InlineData(256, false)]
      public void WithoutMetadata_VarIdsAre240To255(int graphics, bool expected) {
         Assert.Equal(expected, ObjectEventViewModel.IsDynamicGraphics(Model, graphics));
      }

      [Theory]
      [InlineData(1024, false)]
      [InlineData(1025, true)]
      [InlineData(1040, true)]
      [InlineData(1041, false)]
      [InlineData(240, false)]
      public void WithExpansionMetadata_VarIdsStartWhereTheRomSays(int graphics, bool expected) {
         Model.SetUnmappedConstant(Token, ObjectEventViewModel.DynamicGraphicsStartConstant, 1025);
         Assert.Equal(expected, ObjectEventViewModel.IsDynamicGraphics(Model, graphics));
      }

      [Fact]
      public void EveryVarId_RendersThePlaceholder_EvenWhenAScriptNamesASprite() {
         var fromScript = ObjectEventViewModel.Render(Model, null, Default, 240, 0, () => 5);
         Assert.NotSame(Default, fromScript);
         Assert.Equal(16, fromScript.PixelWidth);
         Assert.Equal(32, fromScript.PixelHeight);
         for (int var = 240; var < 256; var++) {
            var noScript = ObjectEventViewModel.Render(Model, null, Default, var, 0, () => -1);
            var withScript = ObjectEventViewModel.Render(Model, null, Default, var, 0, () => 7);
            Assert.Same(noScript, withScript);
            Assert.NotSame(Default, noScript);
         }
      }

      [Fact]
      public void PlaceholderHasWhiteQuestionMarkPixelsOnBlack() {
         var image = ObjectEventViewModel.Render(Model, null, Default, 245, 0, () => -1);
         Assert.Contains(image.PixelData, p => p == 0x7FFF);
         Assert.Contains(image.PixelData, p => p == 0);
      }

      [Fact]
      public void ANormalId_IsNotThePlaceholder() {
         var image = ObjectEventViewModel.Render(Model, null, Default, 12, 0, () => -1);
         Assert.Same(Default, image);
      }
   }
}
