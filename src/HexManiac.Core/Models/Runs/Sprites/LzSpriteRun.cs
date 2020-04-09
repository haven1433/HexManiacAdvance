using System;
using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.Models.Runs.Sprites {
   public class LzSpriteRun : LZRun, ISpriteRun {
      public SpriteFormat SpriteFormat { get; }

      public override string FormatString { get; }

      public int Pages {
         get {
            var length = Model.ReadMultiByteValue(Start + 1, 3);
            return length / SpriteFormat.ExpectedByteLength;
         }
      }

      public LzSpriteRun(SpriteFormat spriteFormat, IDataModel data, int start, IReadOnlyList<int> sources = null)
         : base(data, start, sources) {
         SpriteFormat = spriteFormat;
         if (spriteFormat.ExpectedByteLength > DecompressedLength) InvalidateLength();
         FormatString = $"`lzs{spriteFormat.BitsPerPixel}x{spriteFormat.TileWidth}x{spriteFormat.TileHeight}`";
      }

      public static bool TryParseSpriteFormat(string pointerFormat, out SpriteFormat spriteFormat) {
         spriteFormat = default;
         if (!pointerFormat.StartsWith("`lzs") || !pointerFormat.EndsWith("`")) return false;
         return TryParseDimensions(pointerFormat, out spriteFormat);
      }

      public static bool TryParseDimensions(string format, out SpriteFormat spriteFormat) {
         spriteFormat = default;
         var formatContent = format.Substring(4, format.Length - 5); // snip leading "`xxx" and trailing "`"
         var hintSplit = formatContent.Split('|');
         var dimensionsAsText = hintSplit[0].Split('x');
         if (dimensionsAsText.Length != 3) return false;
         if (!int.TryParse(dimensionsAsText[0], out var bitsPerPixel)) return false;
         if (!int.TryParse(dimensionsAsText[1], out var width)) return false;
         if (!int.TryParse(dimensionsAsText[2], out var height)) return false;
         var hint = hintSplit.Length == 2 ? hintSplit[1] : null;
         spriteFormat = new SpriteFormat(bitsPerPixel, width, height, hint);
         return true;
      }

      protected override BaseRun Clone(IReadOnlyList<int> newPointerSources) => new LzSpriteRun(SpriteFormat, Model, Start, newPointerSources);

      public int[,] GetPixels(IDataModel model, int page) {
         var data = Decompress(model, Start);
         return Sprites.SpriteRun.GetPixels(data, SpriteFormat.ExpectedByteLength * page, SpriteFormat.TileWidth, SpriteFormat.TileHeight);
      }

      public ISpriteRun SetPixels(IDataModel model, ModelDelta token, int page, int[,] pixels) {
         throw new NotImplementedException();
      }
   }
}
