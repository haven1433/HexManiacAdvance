using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// The character customization previews showed a pale green glow around every sprite: the see-through colour of the sprites was hidden with a smoothed opacity mask.
   /// The image control now hands WPF pixels that carry their own alpha (AlphaPixels), where a see-through pixel holds no colour at all.
   /// </summary>
   public class AlphaPixelsTests {
      private static short Pack(int red, int green, int blue) => (short)((red << 10) | (green << 5) | blue);

      private const int Opaque = unchecked((int)0xFF000000);

      // the pale green that the player's sprites use as their see-through colour (colour 0 of the palette)
      private static readonly short PaleGreen = Pack(14, 24, 20);

      [Fact]
      public void SeeThroughPixel_IsAlphaZeroAndHoldsNoColor() {
         var result = AlphaPixels.ToPremultipliedBgra(new[] { PaleGreen, Pack(31, 0, 0) }, 2, PaleGreen);

         Assert.Equal(0, result[0]);
      }

      [Fact]
      public void OtherPixels_AreOpaqueWithTheirOwnColor() {
         var result = AlphaPixels.ToPremultipliedBgra(new[] { PaleGreen, Pack(31, 0, 0), Pack(0, 31, 0), Pack(0, 0, 31), Pack(31, 31, 31) }, 5, PaleGreen);

         Assert.Equal(Opaque | 0xFF0000, result[1]);
         Assert.Equal(Opaque | 0x00FF00, result[2]);
         Assert.Equal(Opaque | 0x0000FF, result[3]);
         Assert.Equal(Opaque | 0xFFFFFF, result[4]);
      }

      [Fact]
      public void BlackPixel_IsOpaque_NotSeeThrough() {
         var result = AlphaPixels.ToPremultipliedBgra(new short[] { 0, 0 }, 2, PaleGreen);

         Assert.Equal(new[] { Opaque, Opaque }, result);
      }

      [Fact]
      public void ColorChannels_AreScaledTheSameWayAsThePaletteSwatches() {
         // 5 bits per channel to 8 bits: channel * 255 / 31 (HexManiac's colour swatches use the same scaling)
         Assert.Equal(Opaque | (255 * 14 / 31 << 16) | (255 * 24 / 31 << 8) | (255 * 20 / 31), AlphaPixels.ToOpaqueArgb(PaleGreen));
         Assert.Equal(Opaque | (255 * 1 / 31 << 16) | (255 * 2 / 31 << 8) | (255 * 3 / 31), AlphaPixels.ToOpaqueArgb(Pack(1, 2, 3)));
      }

      [Fact]
      public void OnlyTheFirstPixelsAreConverted() {
         var result = AlphaPixels.ToPremultipliedBgra(new short[] { 1, 2, 3, 4 }, 3, PaleGreen);

         Assert.Equal(3, result.Length);
      }

      [Fact]
      public void DrawnSprite_HasNoTraceOfTheSeeThroughColor() {
         // a 4x2 'sprite': colour 0 is the see-through colour, 1 is the outline, 2 a body colour
         var palette = new short[] { PaleGreen, Pack(1, 1, 1), Pack(30, 10, 5) };
         var pixels = new int[4, 2];
         pixels[0, 0] = 0; pixels[1, 0] = 1; pixels[2, 0] = 2; pixels[3, 0] = 0;
         pixels[0, 1] = 0; pixels[1, 1] = 0; pixels[2, 1] = 1; pixels[3, 1] = 0;
         var picture = CharacterSprites.Draw(pixels, palette);

         var result = AlphaPixels.ToPremultipliedBgra(picture.PixelData, picture.PixelWidth * picture.PixelHeight, picture.Transparent);

         // every see-through pixel is exactly 0: alpha 0 and no green, red or blue that a scaling filter could mix into the outline
         var seeThrough = Enumerable.Range(0, result.Length).Where(i => picture.PixelData[i] == picture.Transparent).ToList();
         Assert.Equal(5, seeThrough.Count);
         Assert.All(seeThrough, i => Assert.Equal(0, result[i]));
         // and the other pixels keep their full colour
         Assert.Equal(3, result.Count(v => (v >> 24 & 0xFF) == 0xFF));
      }

      [Fact]
      public void SeeThroughColor_IsUnique_SoNoSpritePixelIsMistakenForIt() {
         // a palette whose body colour equals colour 0: the sprite code moves the see-through colour away, so the body keeps its colour (and stays opaque)
         var palette = new short[] { Pack(5, 5, 5), Pack(5, 5, 5), Pack(9, 9, 9) };
         var pixels = new int[3, 1];
         pixels[0, 0] = 0; pixels[1, 0] = 1; pixels[2, 0] = 2;
         var picture = CharacterSprites.Draw(pixels, palette);

         var result = AlphaPixels.ToPremultipliedBgra(picture.PixelData, 3, picture.Transparent);

         Assert.Equal(0, result[0]);
         Assert.Equal(AlphaPixels.ToOpaqueArgb(Pack(5, 5, 5)), result[1]);
         Assert.Equal(AlphaPixels.ToOpaqueArgb(Pack(9, 9, 9)), result[2]);
      }
   }
}
