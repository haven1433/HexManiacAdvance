using System;
using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.HexManiac.Core.ViewModels.Images {
   /// <summary>
   /// What the image editor remembers about the last thing it copied, so that pasting into another image editor tab
   /// (same ROM or another one) does not have to guess palette indexes from colors: the Windows clipboard only carries a picture.
   /// The clipboard picture stays the source of truth: the remembered indexes are only used while the picture on the clipboard is still the one we copied.
   /// </summary>
   public class ImageClip {
      public short[] Colors { get; }
      public int Width { get; }
      /// <summary>For each pixel, the index of the color within its palette page (0 is transparent).</summary>
      public int[] Indexes { get; }
      /// <summary>For each pixel, the colors of the palette page that the index refers to.</summary>
      public IReadOnlyList<short>[] Palettes { get; }
      /// <summary>The editor the clip came from (not used for anything but telling "paste on itself" from "paste somewhere else").</summary>
      public object Owner { get; }
      public int OriginX { get; }
      public int OriginY { get; }

      public ImageClip(short[] colors, int width, int[] indexes, IReadOnlyList<short>[] palettes, object owner = null, int originX = 0, int originY = 0) {
         (Colors, Width, Indexes, Palettes, Owner, OriginX, OriginY) = (colors, width, indexes, palettes, owner, originX, originY);
      }

      public bool Matches(short[] colors, int width) {
         if (colors == null || Colors == null || width != Width || colors.Length != Colors.Length) return false;
         for (int i = 0; i < colors.Length; i++) if (colors[i] != Colors[i]) return false;
         return true;
      }
   }

   public static class ImageClipboardMemory {
      private static ImageClip last;
      public static ImageClip Last { get => last; set => last = value; }

      /// <summary>Returns the remembered clip only if it describes exactly the picture that is on the clipboard now.</summary>
      public static ImageClip Find(short[] colors, int width) {
         var clip = last;
         return clip != null && clip.Matches(colors, width) ? clip : null;
      }
   }

   public class ImagePasteResult {
      /// <summary>Palette index (within the page of the destination pixel) for each pasted pixel, indexed [x, y].</summary>
      public int[,] Indexes { get; }
      public int Width => Indexes.GetLength(0);
      public int Height => Indexes.GetLength(1);
      /// <summary>Pixels that already had a matching color at the same palette index (or were transparent).</summary>
      public int DirectPixels { get; set; }
      /// <summary>Pixels whose color was found in the destination palette, but at a different index than it had in the source.</summary>
      public int MatchedPixels { get; set; }
      /// <summary>Pixels whose color is not in the destination palette: they got the closest color instead.</summary>
      public int RemappedPixels { get; set; }
      /// <summary>How many different source colors were replaced with a close color.</summary>
      public int RemappedColors { get; set; }
      public int TransparentPixels { get; set; }

      public ImagePasteResult(int width, int height) => Indexes = new int[width, height];

      public string Describe() {
         var text = $"Pasted {Width}x{Height} px";
         if (RemappedColors > 0) {
            text += $"; {RemappedColors} color{(RemappedColors == 1 ? " was" : "s were")} remapped to the closest color in the palette.";
         } else if (MatchedPixels > 0) {
            text += "; colors were matched to the palette.";
         } else {
            text += "; the palette matched.";
         }
         return text;
      }
   }

   public static class ImagePasteMapper {
      /// <summary>
      /// Works out which palette index each pixel of a picture should get in the destination.
      /// Order of preference per pixel:
      ///  1. transparent source pixels (index 0) stay transparent.
      ///  2. if the source index is known and the destination palette shows the same color at that index, keep the index (identical palettes paste index for index).
      ///  3. an index whose color is exactly the pixel's color (index 0, the transparent color, is only used if nothing else matches).
      ///  4. the closest color (by distance in 5-bit RGB), never the transparent index.
      /// </summary>
      /// <param name="targetPalette">Gives the colors of the palette page that the destination pixel (x, y) in the picture will be drawn with.</param>
      public static ImagePasteResult Map(short[] colors, int width, ImageClip source, Func<int, int, IReadOnlyList<short>> targetPalette) {
         var height = colors.Length / width;
         var result = new ImagePasteResult(width, height);
         var remappedColors = new HashSet<(int, short)>();
         for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
               var i = y * width + x;
               var color = colors[i];
               var palette = targetPalette(x, y);
               var sourceIndex = source != null && source.Indexes != null && i < source.Indexes.Length ? source.Indexes[i] : -1;

               if (sourceIndex == 0) {
                  result.Indexes[x, y] = 0;
                  result.TransparentPixels++;
                  result.DirectPixels++;
                  continue;
               }
               if (sourceIndex > 0 && sourceIndex < palette.Count && palette[sourceIndex] == color) {
                  result.Indexes[x, y] = sourceIndex;
                  result.DirectPixels++;
                  continue;
               }

               var exact = -1;
               for (int j = 1; j < palette.Count && exact < 0; j++) if (palette[j] == color) exact = j;
               if (exact < 0 && palette.Count > 0 && palette[0] == color) exact = 0;
               if (exact >= 0) {
                  result.Indexes[x, y] = exact;
                  if (exact == 0) result.TransparentPixels++;
                  if (sourceIndex < 0 || sourceIndex == exact) result.DirectPixels++; else result.MatchedPixels++;
                  continue;
               }

               result.Indexes[x, y] = Closest(palette, color);
               result.RemappedPixels++;
               remappedColors.Add((palette.Count, color));
            }
         }
         result.RemappedColors = remappedColors.Count;
         return result;
      }

      /// <summary>Index of the closest color; the transparent index 0 is only chosen if it is the only choice.</summary>
      public static int Closest(IReadOnlyList<short> palette, short color) {
         int best = 0, bestDistance = int.MaxValue;
         for (int j = 1; j < palette.Count; j++) {
            var distance = Distance(palette[j], color);
            if (distance < bestDistance) (best, bestDistance) = (j, distance);
         }
         return best;
      }

      public static int Distance(short a, short b) {
         int dr = ((a >> 10) & 31) - ((b >> 10) & 31);
         int dg = ((a >> 5) & 31) - ((b >> 5) & 31);
         int db = (a & 31) - (b & 31);
         return dr * dr + dg * dg + db * db;
      }
   }
}
