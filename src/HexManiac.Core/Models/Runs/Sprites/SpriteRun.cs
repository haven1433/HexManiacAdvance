using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
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
         Debug.Assert(bitsPerPixel == 8 || bitsPerPixel == 4);
         for (int y = 0; y < tileHeight; y++) {
            int yOffset = y * 8;
            for (int x = 0; x < tileWidth; x++) {
               var tileStart = ((y * tileWidth) + x) * 8 * bitsPerPixel + start;
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

      public static void SetPixels(byte[] data, int start, int[,] pixels, int bitsPerPixel) {
         int width = pixels.GetLength(0), height = pixels.GetLength(1);
         int tileWidth = width / 8, tileHeight = height / 8;
         if (bitsPerPixel == 4) {
            for (int y = 0; y < tileHeight; y++) {
               int yOffset = y * 8;
               for (int x = 0; x < tileWidth; x++) {
                  int xOffset = x * 8;
                  for (int i = 0; i < 32; i++) {
                     int xx = i % 4, yy = i / 4;
                     var low = pixels[xOffset + xx * 2 + 0, yOffset + yy];
                     var high = pixels[xOffset + xx * 2 + 1, yOffset + yy];
                     data[start] = (byte)((high << 4) | low);
                     start += 1;
                  }
               }
            }
         } else if (bitsPerPixel == 8) {
            for (int y = 0; y < tileHeight; y++) {
               int yOffset = y * 8;
               for (int x = 0; x < tileWidth; x++) {
                  int xOffset = x * 8;
                  for (int i = 0; i < 64; i++) {
                     int xx = i % 8, yy = i / 8;
                     data[start] = (byte)pixels[xOffset + xx, yOffset + yy];
                     start += 1;
                  }
               }
            }
         } else {
            throw new NotImplementedException();
         }
      }

      public ISpriteRun SetPixels(IDataModel model, ModelDelta token, int page, int[,] pixels) {
         var data = new byte[pixels.Length * SpriteFormat.BitsPerPixel / 8];
         SetPixels(data, 0, pixels, SpriteFormat.BitsPerPixel);
         for (int i = 0; i < data.Length; i++) token.ChangeData(model, Start + i, data[i]);
         return this;
      }

      public void AppendTo(IDataModel model, StringBuilder builder, int start, int length, bool deep) {
         while (length > 0) {
            builder.Append(model[start].ToHexString() + " ");
            start += 1;
            length -= 1;
         }
      }
   }
}
