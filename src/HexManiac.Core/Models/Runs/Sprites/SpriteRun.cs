using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace HavenSoft.HexManiac.Core.Models.Runs.Sprites {
   public class SpriteRun : BaseRun, ISpriteRun {
      private readonly int bitsPerPixel, tileWidth, tileHeight;

      public IDataModel Model { get; }
      public SpriteFormat SpriteFormat { get; }
      public int Pages => 1;
      public override int Length { get; }

      public override string FormatString { get; }

      public bool SupportsImport => true;
      public bool SupportsEdit => true;

      public SpriteRun(IDataModel model, int start, SpriteFormat format, SortedSpan<int> sources = null) : base(start, sources) {
         Model = model;
         SpriteFormat = format;
         bitsPerPixel = format.BitsPerPixel;
         tileWidth = format.TileWidth;
         tileHeight = format.TileHeight;
         Length = tileWidth * tileHeight * bitsPerPixel * 8;
         FormatString = $"`ucs{bitsPerPixel}x{tileWidth}x{tileHeight}";
         if (!string.IsNullOrEmpty(format.PaletteHint)) FormatString += "|" + format.PaletteHint;
         FormatString += "`";
      }

      public static bool TryParseSpriteFormat(string pointerFormat, out SpriteFormat spriteFormat) {
         spriteFormat = default;
         if (!pointerFormat.StartsWith("`ucs") || !pointerFormat.EndsWith("`")) return false;
         return LzSpriteRun.TryParseDimensions(pointerFormat, out spriteFormat);
      }

      // not actually LZ, but it is uncompressed and acts much the same way.
      int lastFormatRequested = int.MaxValue;
      public override IDataFormat CreateDataFormat(IDataModel data, int index) {
         var basicFormat = new LzUncompressed(index);
         if (!CreateForLeftEdge) return basicFormat;
         if (lastFormatRequested < index) {
            lastFormatRequested = index;
            return basicFormat;
         }

         var sprite = data.CurrentCacheScope.GetImage(this);
         var availableRows = (Length - (index - Start)) / ExpectedDisplayWidth;
         lastFormatRequested = index;
         return new SpriteDecorator(basicFormat, sprite, ExpectedDisplayWidth, availableRows);
      }

      protected override BaseRun Clone(SortedSpan<int> newPointerSources) => new SpriteRun(Model, Start, SpriteFormat, newPointerSources);

      ISpriteRun ISpriteRun.Duplicate(SpriteFormat format) => Duplicate(format);
      public SpriteRun Duplicate(SpriteFormat format) => new SpriteRun(Model, Start, format, PointerSources);

      public byte[] GetData() {
         var data = new byte[SpriteFormat.ExpectedByteLength];
         Array.Copy(Model.RawData, Start, data, 0, data.Length);
         return data;
      }

      public int[,] GetPixels(IDataModel model, int page, int tableIndex) {
         var pageSize = 8 * bitsPerPixel * tileWidth * tileHeight;
         return GetPixels(model, Start + page * pageSize, tileWidth, tileHeight, bitsPerPixel);
      }

      /// <summary>
      /// convert from raw values to palette-index values
      /// </summary>
      public static int[,] GetPixels(IReadOnlyList<byte> data, int start, int tileWidth, int tileHeight, int bitsPerPixel) {
         var result = new int[8 * tileWidth, 8 * tileHeight];
         Debug.Assert(bitsPerPixel.IsAny(1, 2, 4, 8));
         for (int y = 0; y < tileHeight; y++) {
            int yOffset = y * 8;
            for (int x = 0; x < tileWidth; x++) {
               var tileStart = ((y * tileWidth) + x) * 8 * bitsPerPixel + start;
               int xOffset = x * 8;
               if (bitsPerPixel == 4) {
                  for (int i = 0; i < 32; i++) {
                     int xx = i % 4; // ranges from 0 to 3
                     int yy = i / 4; // ranges from 0 to 7
                     var raw = tileStart + i < data.Count ? data[tileStart + i] : 0;
                     result[xOffset + xx * 2 + 0, yOffset + yy] = (raw & 0xF);
                     result[xOffset + xx * 2 + 1, yOffset + yy] = raw >> 4;
                  }
               } else if (bitsPerPixel == 8) {
                  for (int i = 0; i < 64; i++) {
                     int xx = i % 8;
                     int yy = i / 8;
                     var raw = tileStart + i < data.Count ? data[tileStart + i] : 0;
                     result[xOffset + xx, yOffset + yy] = raw;
                  }
               } else if (bitsPerPixel == 2) {
                  for (int i = 0; i < 16; i++) {
                     int xx = i % 2; // ranges from 0 to 1
                     int yy = i / 2; // ranges from 0 to 7
                     xx = 1 - xx; // swap the left half with the right half
                     var raw = tileStart + i < data.Count ? data[tileStart + i] : 0;
                     result[xOffset + xx * 4 + 0, yOffset + yy] = ((raw >> 6) & 3);
                     result[xOffset + xx * 4 + 1, yOffset + yy] = ((raw >> 4) & 3);
                     result[xOffset + xx * 4 + 2, yOffset + yy] = ((raw >> 2) & 3);
                     result[xOffset + xx * 4 + 3, yOffset + yy] = ((raw >> 0) & 3);
                  }
               } else if (bitsPerPixel == 1) {
                  for (int i = 0; i < 8; i++) {
                     var raw = tileStart + i < data.Count ? data[tileStart + i] : 0;
                     result[xOffset + 0, yOffset + i] = ((raw >> 0) & 1);
                     result[xOffset + 1, yOffset + i] = ((raw >> 1) & 1);
                     result[xOffset + 2, yOffset + i] = ((raw >> 2) & 1);
                     result[xOffset + 3, yOffset + i] = ((raw >> 3) & 1);
                     result[xOffset + 4, yOffset + i] = ((raw >> 4) & 1);
                     result[xOffset + 5, yOffset + i] = ((raw >> 5) & 1);
                     result[xOffset + 6, yOffset + i] = ((raw >> 6) & 1);
                     result[xOffset + 7, yOffset + i] = ((raw >> 7) & 1);
                  }
               } else {
                  // unsure what to do here...
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
                     if (start >= data.Length) break; // don't write the blank 'bonus' tiles for tilesets
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
                     if (start >= data.Length) break; // don't write the blank 'bonus' tiles for tilesets
                     int xx = i % 8, yy = i / 8;
                     data[start] = (byte)pixels[xOffset + xx, yOffset + yy];
                     start += 1;
                  }
               }
            }
         } else if (bitsPerPixel == 2) {
            for (int y = 0; y < tileHeight; y++) {
               int yOffset = y * 8;
               for (int x = 0; x < tileWidth; x++) {
                  int xOffset = x * 8;
                  for (int i = 0; i < 16; i++) {
                     if (start >= data.Length) break; // don't write the blank 'bonus' tiles for tilesets
                     int xx = i % 2, yy = i / 2;
                     xx = 1 - xx;
                     var a = pixels[xOffset + xx * 4 + 0, yOffset + yy];
                     var b = pixels[xOffset + xx * 4 + 1, yOffset + yy];
                     var c = pixels[xOffset + xx * 4 + 2, yOffset + yy];
                     var d = pixels[xOffset + xx * 4 + 3, yOffset + yy];
                     data[start] = (byte)((a << 6) | (b << 4) | (c << 2) | d);
                     start += 1;
                  }
               }
            }
         } else if (bitsPerPixel == 1) {
            for (int y = 0; y < tileHeight; y++) {
               int yOffset = y * 8;
               for (int x = 0; x < tileWidth; x++) {
                  int xOffset = x * 8;
                  for (int i = 0; i < 8; i++) {
                     if (start >= data.Length) break; // don't write the blank 'bonus' tiles for tilesets
                     byte newValue = 0;
                     for (int j = 0; j < 8; j++) newValue |= (byte)(pixels[xOffset + j, yOffset + i] << j);
                     data[start] = newValue;
                     start += 1;
                  }
               }
            }
         } else {
            throw new NotImplementedException();
         }
      }

      public SpriteRun IncreaseHeight(int units, ModelDelta token) {
         if (units < 1) return this;
         var data = GetData();
         var format = SpriteFormat;
         var newData = new byte[data.Length + units * format.BitsPerPixel * 8 * format.TileWidth];
         Array.Copy(data, newData, data.Length);
         var newRun = Model.RelocateForExpansion(token, this, newData.Length);
         token.ChangeData(Model, newRun.Start, newData);
         newRun = newRun.Duplicate(new SpriteFormat(format.BitsPerPixel, format.TileWidth, format.TileHeight + units, format.PaletteHint, format.AllowLengthErrors));
         Model.ObserveRunWritten(token, newRun);
         return newRun;
      }

      public ISpriteRun SetPixels(IDataModel model, ModelDelta token, int page, int[,] pixels) {
         var data = new byte[pixels.Length * SpriteFormat.BitsPerPixel / 8];
         SetPixels(data, 0, pixels, SpriteFormat.BitsPerPixel);
         for (int i = 0; i < data.Length; i++) token.ChangeData(model, Start + i, data[i]);
         return this;
      }

      public void AppendTo(IDataModel model, StringBuilder builder, int start, int length, bool deep) {
         while (length > 0) {
            if (start == this.Start + this.Length - 1) {
               builder.Append(model[start].ToHexString());
            } else if (this.Start <= start && start < this.Start + this.Length) {
               builder.Append(model[start].ToHexString() + " ");
            } else {
               break;
            }
            // Moving to the next byte
            start += 1;
            length -= 1;
         }
      }

      public void Clear(IDataModel model, ModelDelta changeToken, int start, int length) {
         for (int i = 0; i < length; i++) {
            changeToken.ChangeData(model, start + i, 0x00);
         }
      }
   }
}
