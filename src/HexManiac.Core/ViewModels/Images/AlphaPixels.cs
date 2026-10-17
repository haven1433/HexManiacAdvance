namespace HavenSoft.HexManiac.Core.ViewModels.Images {
   /// <summary>
   /// Turns a picture that marks one colour as see-through (<see cref="IPixelViewModel.Transparent"/>) into pixels that have a real alpha channel.
   /// The WPF image used to hide those pixels with an opacity mask of the same size, and WPF smooths that mask when the picture is zoomed:
   /// the soft edge of the mask let the see-through colour (the pale green of the player's sprites) show as a glow around every sprite.
   /// A bitmap that carries the alpha itself is scaled with the same hard pixel edges as its colours, and a see-through pixel holds no colour at all
   /// (premultiplied alpha: 0 in every channel), so nothing can leak around the outline, whatever the scaling.
   /// </summary>
   public static class AlphaPixels {
      /// <summary>One display pixel (5 bits per channel, red in the top bits: r&lt;&lt;10 | g&lt;&lt;5 | b) as an opaque 0xAARRGGBB value; the 5 to 8 bit scaling is the one of the palette swatches.</summary>
      public static int ToOpaqueArgb(short color) {
         int r = (color >> 10) & 0x1F, g = (color >> 5) & 0x1F, b = color & 0x1F;
         r = r * 255 / 31; g = g * 255 / 31; b = b * 255 / 31;
         return unchecked((int)0xFF000000) | (r << 16) | (g << 8) | b;
      }

      /// <summary>
      /// The first 'count' display pixels as premultiplied 0xAARRGGBB values (the bytes of the WPF format Pbgra32, B first, as little-endian integers).
      /// The pixels equal to 'transparent' become 0 (alpha 0, no colour); every other pixel is opaque, so premultiplying changes nothing.
      /// </summary>
      public static int[] ToPremultipliedBgra(short[] pixels, int count, short transparent) {
         var result = new int[count];
         for (int i = 0; i < count; i++) {
            var color = pixels[i];
            if (color == transparent) continue;
            result[i] = ToOpaqueArgb(color);
         }
         return result;
      }
   }
}
