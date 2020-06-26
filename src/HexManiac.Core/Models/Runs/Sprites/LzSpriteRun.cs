using System;
using System.Collections.Generic;
using System.ComponentModel;

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

      public LzSpriteRun(SpriteFormat spriteFormat, IDataModel data, int start, SortedSpan<int> sources = null)
         : base(data, start, sources) {
         SpriteFormat = spriteFormat;
         if (spriteFormat.ExpectedByteLength > DecompressedLength) InvalidateLength();
         var hintContent = string.Empty;
         if (!string.IsNullOrEmpty(spriteFormat.PaletteHint)) hintContent += "|" + spriteFormat.PaletteHint;
         FormatString = $"`lzs{spriteFormat.BitsPerPixel}x{spriteFormat.TileWidth}x{spriteFormat.TileHeight}{hintContent}`";
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

      protected override BaseRun Clone(SortedSpan<int> newPointerSources) => new LzSpriteRun(SpriteFormat, Model, Start, newPointerSources);

      public ISpriteRun Duplicate(SpriteFormat format) => new LzSpriteRun(format, Model, Start, PointerSources);

      public int[,] GetPixels(IDataModel model, int page) {
         var data = Decompress(model, Start);
         return SpriteRun.GetPixels(data, SpriteFormat.ExpectedByteLength * page, SpriteFormat.TileWidth, SpriteFormat.TileHeight, SpriteFormat.BitsPerPixel);
      }

      public ISpriteRun SetPixels(IDataModel model, ModelDelta token, int page, int[,] pixels) {
         var data = Decompress(model, Start);
         page %= Pages;

         var pageLength = SpriteFormat.TileWidth * SpriteFormat.TileHeight * 8 * SpriteFormat.BitsPerPixel;
         SpriteRun.SetPixels(data, page * pageLength, pixels, SpriteFormat.BitsPerPixel);

         var newModelData = Compress(data, 0, data.Length);
         var newRun = (ISpriteRun)model.RelocateForExpansion(token, this, newModelData.Count);
         for (int i = 0; i < newModelData.Count; i++) token.ChangeData(model, newRun.Start + i, newModelData[i]);
         for (int i = newModelData.Count; i < Length; i++) token.ChangeData(model, newRun.Start + i, 0xFF);
         newRun = new LzSpriteRun(SpriteFormat, model, newRun.Start, newRun.PointerSources);
         model.ObserveRunWritten(token, newRun);
         return newRun;
      }

      public LzSpriteRun AppendPage(ModelDelta token) {
         var data = Decompress(Model, Start);
         var lastPage = Pages - 1;
         var pageLength = SpriteFormat.TileWidth * SpriteFormat.TileHeight * 8 * SpriteFormat.BitsPerPixel;
         var newData = new byte[data.Length + pageLength];
         Array.Copy(data, newData, data.Length);
         Array.Copy(data, lastPage * pageLength, newData, data.Length, pageLength);
         var newModelData = Compress(newData, 0, newData.Length);

         var newRun = (LzSpriteRun)Model.RelocateForExpansion(token, this, newModelData.Count);
         for (int i = 0; i < newModelData.Count; i++) token.ChangeData(Model, newRun.Start + i, newModelData[i]);
         newRun = new LzSpriteRun(SpriteFormat, Model, newRun.Start, newRun.PointerSources);
         Model.ObserveRunWritten(token, newRun);
         return newRun;
      }

      public LzSpriteRun DeletePage(int page, ModelDelta token) {
         // TODO
         throw new NotImplementedException();
      }
   }
}
