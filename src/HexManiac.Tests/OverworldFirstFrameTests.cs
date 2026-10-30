using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// An overworld sprite that keeps all of its frames in one tall picture (the player characters, bike, surfing, fishing, ...)
   /// must show one frame of the size it declares, not the whole sheet: in the template picker, the sprite dropdowns and on the map.
   /// </summary>
   public class OverworldFirstFrameTests {
      private const int FrameWidth = 16, FrameHeight = 32, Frames = 9;

      /// <summary>Frame f is filled with the pixel value f + 1 (0 is never used so that every frame is distinguishable).</summary>
      private static IPixelViewModel Sheet(int frames = Frames, int width = FrameWidth) {
         var data = new short[width * FrameHeight * frames];
         for (int i = 0; i < data.Length; i++) data[i] = (short)(i / (width * FrameHeight) + 1);
         return new ReadonlyPixelViewModel(width, FrameHeight * frames, data, -1);
      }

      [Fact]
      public void TallSheet_WithoutFacing_IsTheFirstFrame() {
         var result = ObjectEventViewModel.CropToFrame(Sheet(), FrameWidth, FrameHeight, 1, -1);
         Assert.Equal(FrameWidth, result.PixelWidth);
         Assert.Equal(FrameHeight, result.PixelHeight);
         Assert.All(result.PixelData, pixel => Assert.Equal(1, pixel));
      }

      [Theory]
      [InlineData(0, 1)]
      [InlineData(1, 2)]
      [InlineData(2, 3)]
      [InlineData(3, 1)] // right is drawn by flipping 'left' before it gets here: it never asks for frame 3, which is a walking frame
      [InlineData(76, 1)]
      public void TallSheet_ShowsTheFrameOfTheFacing(int facing, int expectedPixel) {
         var result = ObjectEventViewModel.CropToFrame(Sheet(), FrameWidth, FrameHeight, 1, facing);
         Assert.Equal(FrameHeight, result.PixelHeight);
         Assert.All(result.PixelData, pixel => Assert.Equal(expectedPixel, pixel));
      }

      [Fact]
      public void SpriteWithSeveralListEntries_IsNotCutByFacing() {
         // the list already has one picture per entry: the facing chose the entry, so the frame is the first
         var result = ObjectEventViewModel.CropToFrame(Sheet(1), FrameWidth, FrameHeight, 9, 2);
         Assert.Equal(FrameHeight, result.PixelHeight);
         Assert.All(result.PixelData, pixel => Assert.Equal(1, pixel));
      }

      [Fact]
      public void PictureThatIsAlreadyOneFrame_IsUntouched() {
         var picture = Sheet(1);
         Assert.Same(picture, ObjectEventViewModel.CropToFrame(picture, FrameWidth, FrameHeight, 1, 0));
      }

      [Fact]
      public void PictureSmallerThanDeclared_IsUntouched() {
         // declared 16x32 but only 16x16 of data (a vanilla quirk): never grow or crop it
         var picture = new ReadonlyPixelViewModel(16, 16, new short[256], -1);
         Assert.Same(picture, ObjectEventViewModel.CropToFrame(picture, FrameWidth, FrameHeight, 1, 0));
      }

      [Theory]
      [InlineData(0, 32)]
      [InlineData(16, 0)]
      [InlineData(-1, -1)]
      public void WithoutADeclaredSize_ThePictureIsUntouched(int width, int height) {
         var picture = Sheet();
         Assert.Same(picture, ObjectEventViewModel.CropToFrame(picture, width, height, 1, 0));
      }

      [Fact]
      public void SheetWiderThanDeclared_IsCroppedToTheDeclaredWidth() {
         var result = ObjectEventViewModel.CropToFrame(Sheet(3, 32), FrameWidth, FrameHeight, 1, 0);
         Assert.Equal(FrameWidth, result.PixelWidth);
         Assert.Equal(FrameHeight, result.PixelHeight);
      }

      [Fact]
      public void FacingBeyondTheSheet_FallsBackToTheFirstFrame() {
         var result = ObjectEventViewModel.CropToFrame(Sheet(2), FrameWidth, FrameHeight, 1, 2);
         Assert.All(result.PixelData, pixel => Assert.Equal(1, pixel));
      }

      [Fact]
      public void DeclaredSizeThatIsNotWholeTiles_RoundsUpToWholeTiles() {
         var result = ObjectEventViewModel.CropToFrame(Sheet(), 12, 30, 1, 0);
         Assert.Equal(16, result.PixelWidth);
         Assert.Equal(32, result.PixelHeight);
      }

      [Fact]
      public void NullPicture_StaysNull() {
         Assert.Null(ObjectEventViewModel.CropToFrame(null, FrameWidth, FrameHeight, 1, 0));
      }
   }
}
