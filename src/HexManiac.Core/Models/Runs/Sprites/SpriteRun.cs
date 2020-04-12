using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace HavenSoft.HexManiac.Core.Models.Runs.Sprites {
   public class SpriteRun : BaseRun, ISpriteRun {
      private readonly int bitsPerPixel, tileWidth, tileHeight;

      public SpriteFormat SpriteFormat { get; }
      public int Pages => 1;
      public override int Length { get; }

      public override string FormatString { get; }

      public SpriteRun(int start, SpriteFormat format, IReadOnlyList<int> sources = null) : base(start, sources) {
         SpriteFormat = format;
         bitsPerPixel = format.BitsPerPixel;
         tileWidth = format.TileWidth;
         tileHeight = format.TileHeight;
         Length = tileWidth * tileHeight * bitsPerPixel * 8;
         FormatString = $"`ucs{bitsPerPixel}x{tileWidth}x{tileHeight}`";
      }

      public static bool TryParseSpriteFormat(string pointerFormat, out SpriteFormat spriteFormat) {
         spriteFormat = default;
         if (!pointerFormat.StartsWith("`ucs") || !pointerFormat.EndsWith("`")) return false;
         return LzSpriteRun.TryParseDimensions(pointerFormat, out spriteFormat);
      }

      // not actually LZ, but it is uncompressed and acts much the same way.
      public override IDataFormat CreateDataFormat(IDataModel data, int index) => LzUncompressed.Instance;

      protected override BaseRun Clone(IReadOnlyList<int> newPointerSources) => new SpriteRun(Start, SpriteFormat, newPointerSources);

      public int[,] GetPixels(IDataModel model, int page) {
         var pageSize = 8 * bitsPerPixel * tileWidth * tileHeight;
         return GetPixels(model, Start + page * pageSize, tileWidth, tileHeight, bitsPerPixel);
      }

      /// <summary>
      /// convert from raw values to palette-index values
      /// </summary>
      public static int[,] GetPixels(IReadOnlyList<byte> data, int start, int tileWidth, int tileHeight, int bitsPerPixel) {
         var result = new int[8 * tileWidth, 8 * tileHeight];
         for (int y = 0; y < tileHeight; y++) {
            int yOffset = y * 8;
            for (int x = 0; x < tileWidth; x++) {
               var tileStart = ((y * tileWidth) + x) * 32 + start;
               int xOffset = x * 8;
               if (bitsPerPixel == 4) {
                  for (int i = 0; i < 32; i++) {
                     int xx = i % 4; // ranges from 0 to 3
                     int yy = i / 4; // ranges from 0 to 7
                     var raw = (byte)(tileStart + i < data.Count ? data[tileStart + i] : 0);
                     result[xOffset + xx * 2 + 0, yOffset + yy] = (raw & 0xF);
                     result[xOffset + xx * 2 + 1, yOffset + yy] = raw >> 4;
                  }
               } else {
                  Debug.Assert(bitsPerPixel == 8);
                  for (int i = 0; i < 64; i++) {
                     int xx = i % 8;
                     int yy = i / 8;
                     var raw = tileStart + i < data.Count ? data[tileStart + i] : 0;
                     result[xOffset + xx, yOffset + yy] = raw;
                  }
               }
            }
         }
         return result;
      }

      public ISpriteRun SetPixels(IDataModel model, ModelDelta token, int page, int[,] pixels) {
         for (int y = 0; y < tileHeight; y++) {
            int yOffset = y * 8;
            for (int x = 0; x < tileWidth; x++) {
               var tileStart = ((y * tileWidth) + x) * 32 + Start;
               int xOffset = x * 8;
               for (int i = 0; i < 32; i++) {
                  int xx = i % 4;
                  int yy = i / 4;
                  var high = pixels[xOffset + xx + 0, yOffset + yy];
                  var low = pixels[xOffset + xx + 1, yOffset + yy];
                  var raw = ((high << 4) | low);
                  token.ChangeData(model, tileStart + i, (byte)raw);
               }
            }
         }
         return this;
      }

      public void AppendTo(IDataModel model, StringBuilder builder, int start, int length) {
         while (length > 0) {
            builder.Append(model[start].ToHexString() + " ");
            start += 1;
            length -= 1;
         }
      }
   }
}
