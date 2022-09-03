using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace HavenSoft.HexManiac.Core.Models.Runs {
   public class TilemapTableRun : BaseRun, ITableRun {
      private readonly IDataModel model;

      public ArrayRunElementSegment Segment { get; }
      public string TilemapAnchor { get; }

      public TilemapMargins Margins { get; }

      public int ElementCount { get; }

      public int ElementLength { get; }

      public IReadOnlyList<string> ElementNames { get; } = new string[0];

      public IReadOnlyList<ArrayRunElementSegment> ElementContent { get; }

      public bool CanAppend => false;

      public override int Length { get; }

      public override string FormatString { get; }

      public TilemapTableRun(IDataModel model, string anchorName, ArrayRunElementSegment segment, TilemapMargins margins, int start, SortedSpan<int> sources = null) : base(start, sources) {
         this.model = model;
         TilemapAnchor = anchorName;
         Segment = segment;
         Margins = margins;

         var tilemapAddress = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, anchorName);
         var tilemap = model.GetNextRun(tilemapAddress) as ITilemapRun;
         if (tilemap != null) {
            var height = tilemap.SpriteFormat.TileHeight + margins.Top + margins.Bottom;
            var width = tilemap.SpriteFormat.TileWidth + margins.Left + margins.Right;
            ElementCount = height * margins.LengthMultiplier;
            ElementLength = width * Segment.Length;
            ElementContent = width.Range().Select(i => segment).ToArray();
         }

         Length = ElementCount * ElementLength;
         FormatString = $"[{segment.SerializeFormat}]{anchorName}{Margins}";
      }

      public ITableRun Append(ModelDelta token, int length) => throw new NotImplementedException();

      public void AppendTo(IDataModel model, StringBuilder builder, int start, int length, bool deep) {
         ITableRunExtensions.AppendTo(this, model, builder, start, length, deep);
      }

      public void Clear(IDataModel model, ModelDelta changeToken, int start, int length) {
         ITableRunExtensions.Clear(this, model, changeToken, start, length);
      }

      int lastFormatCreated = int.MaxValue;
      public override IDataFormat CreateDataFormat(IDataModel data, int index) {
         var inner = ITableRunExtensions.CreateSegmentDataFormat(this, data, index);
         if (index > lastFormatCreated) {
            lastFormatCreated = index;
            return inner;
         }

         if ((index - Start) % ElementLength != 0) return inner;
         var address = data.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, TilemapAnchor);
         var run = data.GetNextRun(address) as ITilemapRun;
         var pixels = data.CurrentCacheScope.GetImage(run);
         if (pixels == null) return inner;

         var missingTopRows = (index - Start) / ElementLength;
         pixels = Crop(pixels, -Margins.Left * 8, -Margins.Top * 8, -Margins.Right * 8, -Margins.Bottom * 8);
         pixels = DuplicateDown(pixels, Margins.LengthMultiplier);
         pixels = Crop(pixels, 0, missingTopRows * 8, 0, 0);
         lastFormatCreated = index;
         return new SpriteDecorator(inner, pixels, pixels.PixelWidth / 8, pixels.PixelHeight / 8);
      }

      public static IPixelViewModel Crop(IPixelViewModel pixels, int left, int top, int right, int bottom) {
         var (width, height) = (pixels.PixelWidth - left - right, pixels.PixelHeight - top - bottom);
         Debug.Assert(width % 8 == 0, $"Cropped image must still have a width/height that's a multiple of 8, but width was {width}.");
         Debug.Assert(height % 8 == 0, $"Cropped image must still have a width/height that's a multiple of 8, but height was {height}.");
         var pixelData = new short[width * height];
         for (int y = 0; y < height; y++) {
            var originalDataStart = pixels.PixelWidth * (y + top) + left;
            if (originalDataStart < 0) continue;
            var croppedDataStart = width * y;
            Array.Copy(pixels.PixelData, originalDataStart, pixelData, croppedDataStart, width);
         }
         return new ReadonlyPixelViewModel(new SpriteFormat(4, width / 8, height / 8, string.Empty), pixelData);
      }

      public static IPixelViewModel DuplicateDown(IPixelViewModel pixels, int count) {
         if (count == 1) return pixels;
         Debug.Assert(count > 0, $"Cannot duplicate {count} times.");
         var pixelData = new short[pixels.PixelWidth * pixels.PixelHeight * count];
         for (int y = 0; y < pixels.PixelHeight; y++) {
            var originalDataStart = pixels.PixelWidth * y;
            for (int i = 0; i < count; i++) {
               var newDataStart = pixels.PixelWidth * y + pixels.PixelData.Length * i;
               Array.Copy(pixels.PixelData, originalDataStart, pixelData, newDataStart, pixels.PixelWidth);
            }
         }
         return new ReadonlyPixelViewModel(new SpriteFormat(4, pixels.PixelWidth / 8, pixels.PixelHeight / 8 * count, string.Empty), pixelData);
      }

      public ITableRun Duplicate(int start, SortedSpan<int> pointerSources, IReadOnlyList<ArrayRunElementSegment> segments) {
         return new TilemapTableRun(model, TilemapAnchor, Segment, Margins, start, pointerSources);
      }

      protected override BaseRun Clone(SortedSpan<int> newPointerSources) {
         return new TilemapTableRun(model, TilemapAnchor, Segment, Margins, Start, newPointerSources);
      }
   }

   public class TilemapMargins {
      public static readonly TilemapMargins Default = new TilemapMargins(1, 0);
      public int LengthMultiplier { get; }
      public int Left { get; }
      public int Top { get; }
      public int Right { get; }
      public int Bottom { get; }

      public TilemapMargins(int multiplier, int uniform) => (LengthMultiplier, Left, Top, Right, Bottom) = (multiplier, uniform, uniform, uniform, uniform);
      public TilemapMargins(int multiplier, int left, int top, int right, int bottom) => (LengthMultiplier, Left, Top, Right, Bottom) = (multiplier, left, top, right, bottom);

      public static TilemapMargins ExtractMargins(ref string token) {
         if (string.IsNullOrWhiteSpace(token)) return Default;
         var name = token;
         var separators = name.Length.Range().Where(i => name[i].IsAny("*+-".ToCharArray())).Concat(new[] { name.Length }).ToArray();
         if (separators.Length == 1) return Default;
         int multiplier = 1;
         if (name[separators[0]] == '*') {
            var tokenLength = separators[1] - separators[0];
            var multiplierText = name.Substring(separators[0] + 1, tokenLength - 1);
            if (!int.TryParse(multiplierText, out multiplier)) multiplier = 1;
            name = name.Substring(0, separators[0]) + name.Substring(separators[1]);
            token = name;
            separators = separators.Skip(1).Select(i => i - tokenLength).ToArray();
         }
         if (separators.Length != 5) return new TilemapMargins(multiplier, 0);
         if (separators.Take(4).Any(i => name[i] == '*')) return new TilemapMargins(multiplier, 0);
         var textMargins = 4.Range().Select(i => name.Substring(separators[i], separators[i + 1] - separators[i]));
         var margins = textMargins.Select(str => int.TryParse(str, out var value) ? value : default).ToArray();
         token = name.Substring(0, separators[0]);
         return new TilemapMargins(multiplier, margins[0], margins[1], margins[2], margins[3]);
      }

      public override string ToString() {
         var result = string.Empty;
         if (LengthMultiplier != 1) result += "*" + LengthMultiplier;
         if (Left == 0 && Top == 0 && Right == 0 && Bottom == 0) return result;
         foreach (var side in new[] { Left, Top, Right, Bottom })
            result += side < 0 ? side.ToString() : "+" + side;
         return result;
      }
   }
}
