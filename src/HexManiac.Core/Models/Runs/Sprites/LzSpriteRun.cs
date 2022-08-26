using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System.Linq;

namespace HavenSoft.HexManiac.Core.Models.Runs.Sprites {
   public class LzSpriteRun : PagedLZRun, ISpriteRun {
      public SpriteFormat SpriteFormat { get; }

      public override string FormatString { get; }

      public override int Pages {
         get {
            var length = Model.ReadMultiByteValue(Start + 1, 3);
            return length / SpriteFormat.ExpectedByteLength;
         }
      }

      public bool SupportsImport => true;
      public bool SupportsEdit => true;

      protected override int UncompressedPageLength => SpriteFormat.ExpectedByteLength;

      public LzSpriteRun(SpriteFormat spriteFormat, IDataModel data, int start, SortedSpan<int> sources = null)
         : base(data, start, spriteFormat.AllowLengthErrors, sources) {
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
         var allowLengthErrors = dimensionsAsText[2].EndsWith("!");
         if (allowLengthErrors) dimensionsAsText[2] = dimensionsAsText[2].Substring(0, dimensionsAsText[2].Length - 1);
         if (!int.TryParse(dimensionsAsText[0], out var bitsPerPixel)) return false;
         if (!int.TryParse(dimensionsAsText[1], out var width)) return false;
         if (!int.TryParse(dimensionsAsText[2], out var height)) return false;
         var hint = hintSplit.Length == 2 ? hintSplit[1] : null;
         spriteFormat = new SpriteFormat(bitsPerPixel, width, height, hint, allowLengthErrors);
         return true;
      }

      int lastFormatRequested = int.MaxValue;
      public override IDataFormat CreateDataFormat(IDataModel data, int index) {
         var basicFormat = base.CreateDataFormat(data, index);
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

      protected override BaseRun Clone(SortedSpan<int> newPointerSources) => new LzSpriteRun(SpriteFormat, Model, Start, newPointerSources);

      public ISpriteRun Duplicate(SpriteFormat format) => new LzSpriteRun(format, Model, Start, PointerSources);

      public byte[] GetData() => Decompress(Model, Start, SpriteFormat.AllowLengthErrors);

      public int[,] GetPixels(IDataModel model, int page, int tableIndex) {
         var data = Decompress(model, Start, AllowLengthErrors);
         if (data == null) return null;
         return SpriteRun.GetPixels(data, SpriteFormat.ExpectedByteLength * page, SpriteFormat.TileWidth, SpriteFormat.TileHeight, SpriteFormat.BitsPerPixel);
      }

      public ISpriteRun SetPixels(IDataModel model, ModelDelta token, int page, int[,] pixels) {
         var data = Decompress(model, Start, allowLengthErrors: true);
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

      public LzSpriteRun IncreaseHeight(int tiles, ModelDelta token) {
         var data = Decompress(Model, Start);
         var longerData = data.Concat(new byte[SpriteFormat.ExpectedByteLength / SpriteFormat.TileHeight * tiles]).ToArray();
         var newModelData = Compress(longerData, 0, longerData.Length);

         var newRun = Model.RelocateForExpansion(token, this, newModelData.Count);
         for (int i = 0; i < newModelData.Count; i++) token.ChangeData(Model, newRun.Start + i, newModelData[i]);
         for (int i = newModelData.Count; i < Length; i++) token.ChangeData(Model, newRun.Start + i, 0xFF);

         var newFormat = new SpriteFormat(SpriteFormat.BitsPerPixel, SpriteFormat.TileWidth, SpriteFormat.TileHeight + tiles, SpriteFormat.PaletteHint, SpriteFormat.AllowLengthErrors);
         newRun = new LzSpriteRun(newFormat, Model, newRun.Start, newRun.PointerSources);
         Model.ObserveRunWritten(token, newRun);
         return newRun;
      }
   }
}
