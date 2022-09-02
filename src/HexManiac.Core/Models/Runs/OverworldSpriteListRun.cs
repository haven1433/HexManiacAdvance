using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HavenSoft.HexManiac.Core.Models.Runs {
   public class OverworldSpriteListRun : BaseRun, ITableRun, ISpriteRun {
      private const int MaxOverworldSprites = 27;
      private readonly IDataModel model;
      private readonly IReadOnlyList<ArrayRunElementSegment> parent;
      public static readonly string SharedFormatString = AsciiRun.StreamDelimeter + "osl" + AsciiRun.StreamDelimeter;

      public override int Length { get; }

      public int ElementCount { get; }

      public string PaletteHint { get; }

      public override string FormatString {
         get {
            if (string.IsNullOrEmpty(PaletteHint)) return SharedFormatString;
            return AsciiRun.StreamDelimeter + "osl|" + PaletteHint + AsciiRun.StreamDelimeter;
         }
      }

      public int ElementLength => 8;

      public IReadOnlyList<string> ElementNames { get; }

      public IReadOnlyList<ArrayRunElementSegment> ElementContent { get; }

      public SpriteFormat SpriteFormat { get; }

      public int Pages => ElementCount;

      public int RunIndex { get; }

      public bool SupportsImport => true;
      public bool SupportsEdit => true;

      public bool CanAppend => false;

      public OverworldSpriteListRun(IDataModel model, IReadOnlyList<ArrayRunElementSegment> parent, string paletteHint, int runIndex, int start, SortedSpan<int> sources = null) : base(start, sources) {
         this.model = model;
         this.parent = parent;
         PaletteHint = paletteHint;
         RunIndex = runIndex;

         var nextStartBuilder = new List<int>();
         int parentLength = parent.Sum(seg => seg.Length);
         if (parent != null && sources != null && sources.Count > 0) {
            for (int nextSource = sources[0] + parentLength; true; nextSource += parentLength) {
               var nextDest = model.ReadPointer(nextSource);
               if (nextDest < 0 || nextDest >= model.Count) break;
               nextStartBuilder.Add(nextDest);
            }
         }
         var nextStart = nextStartBuilder.ToArray();

         var segments = new List<ArrayRunElementSegment> {
            new ArrayRunPointerSegment(model.FormatRunFactory, "sprite", "`ucs4x1x2`"),
            new ArrayRunElementSegment("length", ElementContentType.Integer, 2),
            new ArrayRunElementSegment("unused", ElementContentType.Integer, 2),
         };
         ElementContent = segments;
         ElementCount = 1;
         Length = ElementLength;
         SpriteFormat = new SpriteFormat(4, 1, 1, string.Empty);

         if (sources == null || sources.Count == 0) return;

         // initialize format from parent info
         var listOffset = GetOffset<ArrayRunPointerSegment>(parent, pSeg => pSeg.InnerFormat.StartsWith("`osl"));
         var widthOffset = GetOffset(parent, seg => seg.Name == "width");
         var heightOffset = GetOffset(parent, seg => seg.Name == "height");
         var keyOffset = GetOffset(parent, seg => seg.Name == "paletteid");
         if (widthOffset == parentLength) widthOffset = -1;
         if (heightOffset == parentLength) heightOffset = -1;
         if (keyOffset == parentLength) keyOffset = -1;

         var elementStart = sources[0] - listOffset;
         var width = widthOffset >= 0 ? Math.Max(1, model.ReadMultiByteValue(elementStart + widthOffset, 2)) : 0;
         var height = heightOffset >= 0 ? Math.Max(1, model.ReadMultiByteValue(elementStart + heightOffset, 2)) : 0;
         // if there was no height/width found, assume that it's square and based on the first element length
         var pixelCount = model.ReadMultiByteValue(start + 4, 4) * 2; // number of pixels is twice the number of bytes for all OW sprites
         bool adjustDimensions = true;
         if (width == 0) { width = (int)Math.Sqrt(pixelCount); adjustDimensions = true; }
         if (height == 0) { height = width; adjustDimensions = true; }
         var tileWidth = (int)Math.Max(1, Math.Ceiling(width / 8.0));
         var tileHeight = (int)Math.Max(1, Math.Ceiling(height / 8.0));
         while (adjustDimensions && pixelCount > 0) {
            adjustDimensions = false;
            while (tileWidth * tileHeight * 64 > pixelCount) {
               adjustDimensions = true;
               tileHeight -= 1;
            }
            if (tileHeight == 0) break;
            while (tileWidth * tileHeight * 64 < pixelCount) {
               if (tileWidth > 500) break;
               adjustDimensions = true;
               tileWidth += 1;
            }
         }

         var key = model.ReadMultiByteValue(elementStart + keyOffset, 2);
         var hint = $"{HardcodeTablesModel.OverworldPalettes}:id={key:X4}";
         if (!string.IsNullOrEmpty(paletteHint)) hint = PaletteHint + $"={runIndex:X4}";
         if (paletteHint != null && paletteHint.Contains("=")) hint = PaletteHint + $"{key:X4}";

         var format = $"`ucs4x{tileWidth}x{tileHeight}|{hint}`";
         if (keyOffset == -1 && string.IsNullOrEmpty(paletteHint)) {
            format = $"`ucs4x{tileWidth}x{tileHeight}`";
            hint = string.Empty;
         }
         segments[0] = new ArrayRunPointerSegment(model.FormatRunFactory, "sprite", format);

         // calculate the element count
         var byteLength = tileWidth * tileHeight * TileSize;
         var nextAnchorStart = model.GetNextAnchor(Start + 1).Start;
         ElementCount = 0;
         Length = 0;
         while (Start + Length < nextAnchorStart) {
            var destination = model.ReadPointer(start + Length);
            if (destination < 0 || destination >= model.Count) break;
            if (model.ReadMultiByteValue(start + Length + 4, 4) != byteLength) break;
            var nextRun = model.GetNextRun(start + Length);
            if (Length > 0 && (start + Length).IsAny(nextStart)) break; // metric: if there's a pointer in the parent table that points here, then it's the next list, not this list.
            ElementCount += 1;
            Length += ElementLength;
            if (ElementCount == MaxOverworldSprites) break; // overworld sprite lists can only have so many elements
         }

         SpriteFormat = new SpriteFormat(4, tileWidth, tileHeight, hint);
         ElementNames = ElementCount.Range().Select(i => string.Empty).ToList();
      }

      public override IDataFormat CreateDataFormat(IDataModel data, int index) => ITableRunExtensions.CreateSegmentDataFormat(this, data, index);

      protected override BaseRun Clone(SortedSpan<int> newPointerSources) => new OverworldSpriteListRun(model, parent, PaletteHint, RunIndex, Start, newPointerSources);

      public ITableRun Append(ModelDelta token, int length) => throw new NotImplementedException();

      public ITableRun Duplicate(int start, SortedSpan<int> pointerSources, IReadOnlyList<ArrayRunElementSegment> segments) => throw new NotImplementedException();

      public void AppendTo(IDataModel model, StringBuilder builder, int start, int length, bool deep) => ITableRunExtensions.AppendTo(this, model, builder, start, length, deep);

      public void Clear(IDataModel model, ModelDelta changeToken, int start, int length) {
         for (int i = 0; i < length; i++) changeToken.ChangeData(model, start + i, 0);
      }

      public OverworldSpriteListRun UpdateFromParent(ModelDelta token, int segmentIndex, int pointerSource, out bool spritesMoved) {
         spritesMoved = false;
         if (!(model.GetNextRun(pointerSource) is ITableRun tableSource)) return this;
         var segName = tableSource.ElementContent[segmentIndex].Name;
         if (!segName.IsAny("width", "height", "paletteid")) return this;

         // if parent width changed, add/subtract space to the right edge of the sprite. Min width is one tile.
         // if the parent height changed, add/subtract space to the bottom of the sprite. Min height is one tile.
         if (segName == "width" || segName == "height") {
            var listOffset = GetOffset<ArrayRunPointerSegment>(parent, pSeg => pSeg.InnerFormat == SharedFormatString);
            var elementStart = PointerSources[0] - listOffset;
            var widthOffset = GetOffset(parent, seg => seg.Name == "width");
            var heightOffset = GetOffset(parent, seg => seg.Name == "height");
            var width = Math.Max(1, model.ReadMultiByteValue(elementStart + widthOffset, 2));
            var height = Math.Max(1, model.ReadMultiByteValue(elementStart + heightOffset, 2));
            var newTileWidth = (int)Math.Max(1, Math.Ceiling(width / 8.0));
            var newTileHeight = (int)Math.Max(1, Math.Ceiling(height / 8.0));
            var movedRuns = new List<ISpriteRun>();
            for (int i = 0; i < ElementCount; i++) {
               var spriteStart = model.ReadPointer(Start + ElementLength * i);
               var sprite = model.GetNextRun(spriteStart) as ISpriteRun;
               if (!movedRuns.Contains(sprite) && sprite != null) {
                  sprite = Resize(model, token, sprite, newTileWidth, newTileHeight);
                  movedRuns.Add(sprite);
               }
               var spriteLengthStart = Start + ElementLength * i + 4;
               model.WriteMultiByteValue(spriteLengthStart, 4, token, newTileWidth * newTileHeight * TileSize);
               spritesMoved |= spriteStart != sprite.Start;
            }
         }

         // if the parent paletteid changed, we just need to update the format, no data change is required.
         return new OverworldSpriteListRun(model, parent, PaletteHint, RunIndex, Start, PointerSources);
      }

      private static int GetOffset<T>(IReadOnlyList<ArrayRunElementSegment> segments, Func<T, bool> segmentIdentifier) where T : ArrayRunElementSegment
         => segments.Until(seg => seg is T t && segmentIdentifier(t)).Sum(seg => seg.Length);

      private static int GetOffset(IReadOnlyList<ArrayRunElementSegment> segments, Func<ArrayRunElementSegment, bool> segmentIdentifier)
         => segments.Until(segmentIdentifier).Sum(seg => seg.Length);

      const int TileSize = 32;
      private static ISpriteRun Resize(IDataModel model, ModelDelta token, ISpriteRun spriteRun, int tileWidth, int tileHeight) {
         if (spriteRun == null) return spriteRun;
         spriteRun = model.RelocateForExpansion(token, spriteRun, tileWidth * tileHeight * TileSize);
         var format = spriteRun.SpriteFormat;

         // extract existing tile data
         var existingTiles = new byte[format.TileWidth, format.TileHeight][];
         for (int x = 0; x < format.TileWidth; x++) {
            for (int y = 0; y < format.TileHeight; y++) {
               var tileIndex = y * format.TileWidth + x;
               existingTiles[x, y] = new byte[TileSize];
               Array.Copy(model.RawData, spriteRun.Start + tileIndex * TileSize, existingTiles[x, y], 0, TileSize);
            }
         }

         // rewrite it with the new dimensions
         for (int x = 0; x < tileWidth; x++) {
            for (int y = 0; y < tileHeight; y++) {
               var tileIndex = y * tileWidth + x;
               var start = spriteRun.Start + tileIndex * TileSize;
               for (int i = 0; i < TileSize; i++) {
                  if (x < format.TileWidth && y < format.TileHeight) {
                     token.ChangeData(model, start + i, existingTiles[x, y][i]);
                  } else {
                     token.ChangeData(model, start + i, 0);
                  }
               }
            }
         }

         return spriteRun;
      }

      public byte[] GetData() {
         var pageSize = SpriteFormat.TileWidth * SpriteFormat.TileHeight * 32;
         var result = new byte[pageSize * Pages];
         for (int i = 0; i < Pages; i++) {
            var start = model.ReadPointer(Start + ElementLength * i);
            var page = model.GetNextRun(start) is ISpriteRun spriteRun ? spriteRun.GetData() : new byte[pageSize];
            Array.Copy(page, 0, result, pageSize * i, pageSize);
         }
         return result;
      }

      // this implementation puts all the child sprites into separate pages
      public int[,] GetPixels(IDataModel model, int page, int arrayIndex) {
         var width = SpriteFormat.TileWidth * 8;
         var height = SpriteFormat.TileHeight * 8;
         var spriteStart = model.ReadPointer(Start + ElementLength * page);
         if (!(model.GetNextRun(spriteStart) is ISpriteRun spriteRun)) return new int[width, height];
         var spritePixels = spriteRun.GetPixels(model, page: 0, tableIndex: -1);
         if (spritePixels.GetLength(0) < width || spritePixels.GetLength(1) < height) return new int[width, height];
         return spritePixels;
      }

      // this implementation puts all the child sprites into a single wide image
      private int[,] GetPixels1(IDataModel model, int page) {
         var width = SpriteFormat.TileWidth * 8 / ElementCount;
         var height = SpriteFormat.TileHeight * 8;

         var overallPixels = new int[width * ElementCount, height];

         for (int i = 0; i < ElementCount; i++) {
            var spriteStart = model.ReadPointer(Start + ElementLength * i);
            if (!(model.GetNextRun(spriteStart) is ISpriteRun spriteRun)) continue;
            var spritePixels = spriteRun.GetPixels(model, page: 0, tableIndex: -1);
            if (spritePixels.GetLength(0) < width || spritePixels.GetLength(1) < height) continue;
            int offset = width * i;
            for (int x = 0; x < width; x++) {
               for (int y = 0; y < height; y++) {
                  overallPixels[offset + x, y] = spritePixels[x, y];
               }
            }
         }

         return overallPixels;
      }

      public ISpriteRun SetPixels(IDataModel model, ModelDelta token, int page, int[,] pixels) {
         var spriteStart = model.ReadPointer(Start + ElementLength * page);
         if (!(model.GetNextRun(spriteStart) is ISpriteRun spriteRun)) return this;

         spriteRun.SetPixels(model, token, 0, pixels);
         return this; // moving a page will never move the list
      }

      public ISpriteRun Duplicate(SpriteFormat newFormat) => new OverworldSpriteListRun(model, parent, PaletteHint, RunIndex, Start, PointerSources);
   }
}
