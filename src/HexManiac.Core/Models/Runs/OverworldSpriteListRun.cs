using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HavenSoft.HexManiac.Core.Models.Runs {
   public class OverworldSpriteListRun : BaseRun, ITableRun {
      private readonly IDataModel model;
      private readonly IReadOnlyList<ArrayRunElementSegment> parent;
      public static readonly string SharedFormatString = AsciiRun.StreamDelimeter + "osl" + AsciiRun.StreamDelimeter;

      public override int Length { get; }

      public int ElementCount { get; }

      public override string FormatString => SharedFormatString;

      public int ElementLength => 8;

      public IReadOnlyList<string> ElementNames => null;

      public IReadOnlyList<ArrayRunElementSegment> ElementContent { get; }

      public OverworldSpriteListRun(IDataModel model, IReadOnlyList<ArrayRunElementSegment> parent, int start, IReadOnlyList<int> sources = null) : base(start, sources) {
         this.model = model;
         this.parent = parent;
         var segments = new List<ArrayRunElementSegment> {
            new ArrayRunPointerSegment("sprite", "`ucs4x1x2`"),
            new ArrayRunElementSegment("length", ElementContentType.Integer, 4),
         };
         ElementContent = segments;
         ElementCount = 1;
         Length = ElementLength;

         if (sources == null || sources.Count == 0) return;

         // initialize format from parent info
         var listOffset = GetOffset<ArrayRunPointerSegment>(parent, pSeg => pSeg.InnerFormat == SharedFormatString);
         var widthOffset = GetOffset(parent, seg => seg.Name == "width");
         var heightOffset = GetOffset(parent, seg => seg.Name == "height");
         var keyOffset = GetOffset(parent, seg => seg.Name == "paletteid");

         var elementStart = sources[0] - listOffset;
         var width = Math.Max((byte)1, model[elementStart + widthOffset]);
         var height = Math.Max((byte)1, model[elementStart + heightOffset]);
         var tileWidth = (int)Math.Max(1, Math.Ceiling(width / 8.0));
         var tileHeight = (int)Math.Max(1, Math.Ceiling(height / 8.0));
         var key = model.ReadMultiByteValue(elementStart + keyOffset, 2);

         var format = $"`ucs4x{width / 8}x{height / 8}|overworld.palettes:id={key:X4}`";
         segments[0] = new ArrayRunPointerSegment("sprite", format);

         // calculate the element count
         var byteLength = tileWidth * tileHeight * 32;
         var nextAnchorStart = model.GetNextAnchor(Start + 1).Start;
         ElementCount = 0;
         Length = 0;
         while (Start + Length < nextAnchorStart) {
            if (model[start + Length + 3] != 0x08) break;
            if (model.ReadMultiByteValue(start + Length + 4, 4) != byteLength) break;
            ElementCount += 1;
            Length += ElementLength;
         }
      }

      public override IDataFormat CreateDataFormat(IDataModel data, int index) => ITableRunExtensions.CreateSegmentDataFormat(this, data, index);

      protected override BaseRun Clone(IReadOnlyList<int> newPointerSources) => new OverworldSpriteListRun(model, parent, Start, newPointerSources);

      public ITableRun Append(ModelDelta token, int length) => throw new NotImplementedException();

      public void AppendTo(IDataModel model, StringBuilder builder, int start, int length, bool deep) => ITableRunExtensions.AppendTo(this, model, builder, start, length, deep);

      public OverworldSpriteListRun UpdateFromParent(ModelDelta token, int segmentIndex, int pointerSource) {
         if (!(model.GetNextRun(pointerSource) is ITableRun tableSource)) return this;
         var segName = tableSource.ElementContent[segmentIndex].Name;
         if (!segName.IsAny("width", "height", "paletteid")) return this;

         // TODO
         // if parent width changed, add/subtract space to the right edge of the sprite. Min width is one tile.
         // if the parent height changed, add/subtract space to the bottom of the sprite. Min height is one tile.
         // if the parent paletteid changed, we just need to update the format, no data change is required.
         return new OverworldSpriteListRun(model, parent, Start, PointerSources);
      }

      private static int GetOffset<T>(IReadOnlyList<ArrayRunElementSegment> segments, Func<T, bool> segmentIdentifier) where T : ArrayRunElementSegment
         => segments.Until(seg => seg is T t && segmentIdentifier(t)).Sum(seg => seg.Length);

      private static int GetOffset(IReadOnlyList<ArrayRunElementSegment> segments, Func<ArrayRunElementSegment, bool> segmentIdentifier)
         => segments.Until(segmentIdentifier).Sum(seg => seg.Length);
   }
}
