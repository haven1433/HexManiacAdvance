using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Collections.Generic;
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
            ElementCount = height;
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

      public override IDataFormat CreateDataFormat(IDataModel data, int index) {
         var inner = ITableRunExtensions.CreateSegmentDataFormat(this, data, index);
         if (index != Start) return inner;
         var address = data.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, TilemapAnchor);
         var run = data.GetNextRun(address) as ITilemapRun;
         var pixels = data.CurrentCacheScope.GetImage(run);

         if (pixels != null) {
            var (width, height) = (ElementContent.Count * 8, ElementCount * 8);
            var pixelData = new short[width * height];
            for (int y = 0; y < height; y++) {
               var originalDataStart = pixels.PixelWidth * (y - Margins.Top * 8) - Margins.Left * 8;
               if (originalDataStart < 0) continue;
               var croppedDataStart = width * y;
               Array.Copy(pixels.PixelData, originalDataStart, pixelData, croppedDataStart, width);
            }
            pixels = new ReadonlyPixelViewModel(new SpriteFormat(4, width / 8, height / 8, string.Empty), pixelData);
            return new SpriteDecorator(inner, pixels, width / 8, height / 8);
         }

         return new SpriteDecorator(inner, pixels);
      }

      public ITableRun Duplicate(int start, SortedSpan<int> pointerSources, IReadOnlyList<ArrayRunElementSegment> segments) {
         return new TilemapTableRun(model, TilemapAnchor, Segment, Margins, start, pointerSources);
      }

      protected override BaseRun Clone(SortedSpan<int> newPointerSources) {
         return new TilemapTableRun(model, TilemapAnchor, Segment, Margins, Start, newPointerSources);
      }
   }

   public struct TilemapMargins {
      public int Left { get; }
      public int Top { get; }
      public int Right { get; }
      public int Bottom { get; }

      public TilemapMargins(int uniform) => (Left, Top, Right, Bottom) = (uniform, uniform, uniform, uniform);
      public TilemapMargins(int left, int top, int right, int bottom) => (Left, Top, Right, Bottom) = (left, top, right, bottom);

      public static TilemapMargins ExtractMargins(ref string token) {
         var name = token;
         var separators = name.Length.Range().Where(i => name[i].IsAny("+-".ToCharArray())).Concat(new[] { name.Length }).ToArray();
         if (separators.Length != 5) return default;
         var textMargins = 4.Range().Select(i => name.Substring(separators[i], separators[i + 1] - separators[i]));
         var margins = textMargins.Select(str => int.TryParse(str, out var value) ? value : default).ToArray();
         token = name.Substring(0, separators[0]);
         return new TilemapMargins(margins[0], margins[1], margins[2], margins[3]);
      }

      public override string ToString() {
         var result = string.Empty;
         if (Left == 0 && Top == 0 && Right == 0 && Bottom == 0) return result;
         foreach (var side in new[] { Left, Top, Right, Bottom })
            result += side < 0 ? side.ToString() : "+" + side;
         return result;
      }
   }
}
