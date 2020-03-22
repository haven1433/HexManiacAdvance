using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.Models.Runs.Sprites {
   public interface ISpriteRun : IFormattedRun {
      int Pages { get; }
      int[,] GetPixels(IDataModel model, int page);
      ISpriteRun SetPixels(IDataModel model, ModelDelta token, int page, int[,] pixels);
   }

   public interface IPaletteRun : IFormattedRun {
      int Pages { get; }
      IReadOnlyList<short> GetPalette(IDataModel model, int page);
      IPaletteRun SetPalette(IDataModel model, ModelDelta token, int page, IReadOnlyList<short> colors);
   }

   public class SpriteRun : BaseRun, ISpriteRun {
      private readonly int bitsPerPixel, tileWidth, tileHeight;

      public int Pages => 1;
      public override int Length { get; }

      public override string FormatString { get; }

      public SpriteRun(int start, int bitsPerPixel, int tileWidth, int tileHeight, IReadOnlyList<int> sources) : base(start, sources) {
         this.bitsPerPixel = bitsPerPixel;
         this.tileWidth = tileWidth;
         this.tileHeight = tileHeight;
         Length = tileWidth * tileHeight * bitsPerPixel * 8;
         FormatString = $"`ucs{bitsPerPixel}x{tileWidth}x{tileHeight}`";
      }

      // not actually LZ, but it is uncompressed and acts much the same way.
      public override IDataFormat CreateDataFormat(IDataModel data, int index) => LzUncompressed.Instance; 

      protected override BaseRun Clone(IReadOnlyList<int> newPointerSources) => new SpriteRun(Start, bitsPerPixel, tileWidth, tileHeight, newPointerSources);

      // TODO support values other than 4bpp
      public int[,] GetPixels(IDataModel model, int page) {
         // convert from raw values to palette-index values
         var result = new int[8 * tileWidth, 8 * tileHeight];
         for (int y = 0; y < tileHeight; y++) {
            int yOffset = y * 8;
            for (int x = 0; x < tileWidth; x++) {
               var tileStart = ((y * tileWidth) + x) * 32 + Start;
               int xOffset = x * 8;
               for (int i = 0; i < 32; i++) {
                  int xx = i % 4;
                  int yy = i / 4;
                  byte raw = model[tileStart + i];
                  result[xOffset + xx + 0, yOffset + yy] = raw >> 4;
                  result[xOffset + xx + 1, yOffset + yy] = (raw & 0xF);
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
   }

   // TODO inline edit of palette based on RGB. Custom DataFormat PaletteColor
   public class PaletteRun : BaseRun, IPaletteRun {
      private readonly int bits;

      public int Pages => 1;
      public override int Length { get; }

      public override string FormatString { get; }

      public PaletteRun(int start, int bits, IReadOnlyList<int> sources) : base(start, sources) {
         this.bits = bits;
         Length = 2 * (int)Math.Pow(2, bits);
         FormatString = $"`ucp{bits}`";
      }

      public override IDataFormat CreateDataFormat(IDataModel data, int index) => LzUncompressed.Instance;

      protected override BaseRun Clone(IReadOnlyList<int> newPointerSources) => new PaletteRun(Start, bits, newPointerSources);

      public IReadOnlyList<short> GetPalette(IDataModel model, int page) {
         var results = new List<short>();
         for (int i = 0; i < Length; i += 2) {
            results.Add((short)model.ReadMultiByteValue(Start + i, 2));
         }
         return results;
      }

      public IPaletteRun SetPalette(IDataModel model, ModelDelta token, int page, IReadOnlyList<short> data) {
         for (int i = 0; i < Length; i += 2) {
            model.WriteMultiByteValue(Start + i, 2, token, data[i / 2]);
         }
         return this;
      }
   }
}
