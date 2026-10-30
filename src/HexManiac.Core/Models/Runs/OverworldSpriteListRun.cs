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

      public int DecompressedLength => Length;

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

      public bool CanAppend => true;

      public OverworldSpriteListRun(IDataModel model, IReadOnlyList<ArrayRunElementSegment> parent, string paletteHint, int runIndex, int start, SortedSpan<int> sources = null) : this(model, parent, paletteHint, runIndex, start, -1, sources) { }

      private OverworldSpriteListRun(IDataModel model, IReadOnlyList<ArrayRunElementSegment> parent, string paletteHint, int runIndex, int start, int elementCount = -1, SortedSpan<int> sources = null) : base(start, sources) {
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
         ElementNames = new[] { "" };
         Length = ElementLength;
         SpriteFormat = new SpriteFormat(4, 1, 1, string.Empty);

         if (sources == null || sources.Count == 0) return;
         if (start < 0 || start + ElementLength > model.Count) { Length = 0; return; } // not even one entry fits in the file

         // initialize format from parent info
         var listOffset = GetOffset<ArrayRunPointerSegment>(parent, pSeg => pSeg.InnerFormat.StartsWith("`osl"));
         var widthOffset = GetOffset(parent, seg => seg.Name == "width");
         var heightOffset = GetOffset(parent, seg => seg.Name == "height");
         var keyOffset = GetOffset(parent, seg => seg.Name == "paletteid");
         if (widthOffset == parentLength) widthOffset = -1;
         if (heightOffset == parentLength) heightOffset = -1;
         if (keyOffset == parentLength) keyOffset = -1;

         var elementStart = ParentSource(model, parent, sources) - listOffset;
         var width = widthOffset >= 0 ? Math.Max(1, model.ReadMultiByteValue(elementStart + widthOffset, 2)) : 0;
         var height = heightOffset >= 0 ? Math.Max(1, model.ReadMultiByteValue(elementStart + heightOffset, 2)) : 0;
         // if there was no height/width found, assume that it's square and based on the first element length
         // (the size is a u16: pokeemerald-expansion uses the next byte as a 'relativeFrames' flag)
         var pixelCount = model.ReadMultiByteValue(start + 4, 2) * 2; // number of pixels is twice the number of bytes for all OW sprites
         // a sprite template (the field effects) has no width or height field: its shape is in the OAM data the template points at.
         // Guessing a square from the byte count draws every 32x8 / 16x32 / ... picture with its tiles in the wrong places.
         int oamWidth = 0, oamHeight = 0;
         var shapeFromOam = widthOffset < 0 && heightOffset < 0 && TryReadOamShape(model, parent, elementStart, listOffset, pixelCount, out oamWidth, out oamHeight);
         if (shapeFromOam) (width, height) = (oamWidth, oamHeight);
         bool adjustDimensions = !shapeFromOam;
         if (width == 0) { width = (int)Math.Sqrt(pixelCount); adjustDimensions = true; }
         if (height == 0) { height = width; adjustDimensions = true; }
         var tileWidth = (int)Math.Max(1, Math.Ceiling(width / 8.0));
         var tileHeight = (int)Math.Max(1, Math.Ceiling(height / 8.0));
         if (tileWidth > 30 || tileHeight > 20) return; // sanity size check
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

         // pokeemerald-expansion 'relative frames': a single entry whose data is one LZ-compressed image holding every frame.
         // Show it as one tall sprite (frames stacked top to bottom) instead of trying to read the compressed bytes as pixels.
         var byteLength = tileWidth * tileHeight * TileSize;
         var relativeFrames = model.ReadMultiByteValue(start + 6, 1) != 0;
         var firstFrame = model.ReadPointer(start);
         var compressedFrames = 0;
         if (relativeFrames && firstFrame >= 0 && firstFrame + 4 <= model.Count && model[firstFrame] == 0x10) {
            var decompressedLength = model.ReadMultiByteValue(firstFrame + 1, 3);
            if (byteLength > 0 && decompressedLength % byteLength == 0 && decompressedLength / byteLength <= 64) compressedFrames = decompressedLength / byteLength;
         }
         if (compressedFrames > 0) {
            format = string.IsNullOrEmpty(hint) ? $"`lzs4x{tileWidth}x{tileHeight * compressedFrames}`" : $"`lzs4x{tileWidth}x{tileHeight * compressedFrames}|{hint}`";
         }

         // calculate the element count
         var nextAnchorStart = model.GetNextAnchor(Start + 1).Start;
         ElementCount = 0;
         Length = 0;
         if (compressedFrames > 0) {
            ElementCount = 1;
            Length = ElementLength;
         } else if (elementCount == -1) {
            while (Start + Length < nextAnchorStart) {
               if (start + Length + ElementLength > model.Count) break;
               var destination = model.ReadPointer(start + Length);
               if (destination < 0 || destination >= model.Count) break;
               if (model.ReadMultiByteValue(start + Length + 4, 2) != byteLength) break;
               var nextRun = model.GetNextRun(start + Length);
               if (Length > 0 && (start + Length).IsAny(nextStart)) break; // metric: if there's a pointer in the parent table that points here, then it's the next list, not this list.
               ElementCount += 1;
               Length += ElementLength;
               if (ElementCount == MaxOverworldSprites) break; // overworld sprite lists can only have so many elements
            }
         } else {
            ElementCount = elementCount;
            Length = ElementCount * ElementLength;
         }

         ElementCount = Math.Max(ElementCount, 1);
         Length = Math.Max(ElementLength, Length);

         // pokeemerald-expansion 'relative frames', not compressed: the one entry points at the first frame of a sheet and the other frames follow it.
         // Nothing in the list says how many there are, but the animations of the template do (the biggest image they show): draw the sheet as one tall sprite.
         var sheetFrames = 0;
         if (compressedFrames == 0 && ElementCount == 1 && relativeFrames && firstFrame >= 0 && byteLength > 0 && listOffset >= 4) {
            sheetFrames = CountAnimationFrames(model, parent, elementStart, listOffset);
            if (sheetFrames > MaxSheetFrames || firstFrame + (long)sheetFrames * byteLength > model.Count) sheetFrames = 0;
         }
         if (sheetFrames > 1) {
            format = string.IsNullOrEmpty(hint) ? $"`ucs4x{tileWidth}x{tileHeight * sheetFrames}`" : $"`ucs4x{tileWidth}x{tileHeight * sheetFrames}|{hint}`";
         }
         segments[0] = new ArrayRunPointerSegment(model.FormatRunFactory, "sprite", format);
         var frameStack = Math.Max(compressedFrames, sheetFrames);
         SpriteFormat = new SpriteFormat(4, tileWidth, frameStack > 1 ? tileHeight * frameStack : tileHeight, hint);
         ElementNames = ElementCount.Range().Select(i => string.Empty).ToList();
      }

      private const int MaxSheetFrames = 64;

      /// <summary>
      /// The shapes an OAM entry can have: [shape][size] -> (width, height) in pixels. Shape 3 does not exist.
      /// Attribute 0 (u16) holds the shape in bits 14-15, attribute 1 (u16) holds the size in bits 14-15.
      /// </summary>
      private static readonly (int width, int height)[][] OamShapes = {
         new[] { (8, 8), (16, 16), (32, 32), (64, 64) },
         new[] { (16, 8), (32, 8), (32, 16), (64, 32) },
         new[] { (8, 16), (8, 32), (16, 32), (32, 64) },
      };

      /// <summary>The pixel size of an OAM entry (a pair of attribute words), or false if the shape is not one a sprite can have.</summary>
      public static bool TryGetOamSize(int attribute0, int attribute1, out int width, out int height) {
         width = height = 0;
         var shape = (attribute0 >> 14) & 3;
         var size = (attribute1 >> 14) & 3;
         if (shape >= OamShapes.Length) return false;
         (width, height) = OamShapes[shape][size];
         return true;
      }

      /// <summary>
      /// A sprite template is { tileTag, paletteTag, oam, anims, images, affineAnims, callback }: the two pointers in front of the image list are the OAM data and the animations.
      /// Returns the size the OAM data gives the sprite, but only if it fits the number of pixels the first image says it has.
      /// </summary>
      private static bool TryReadOamShape(IDataModel model, IReadOnlyList<ArrayRunElementSegment> parent, int elementStart, int listOffset, int pixelCount, out int width, out int height) {
         width = height = 0;
         if (listOffset < 8 || pixelCount <= 0 || !IsPointerAt(parent, listOffset - 8) || !IsPointerAt(parent, listOffset - 4)) return false;
         var oam = model.ReadPointer(elementStart + listOffset - 8);
         if (oam < 0 || oam + 4 > model.Count) return false;
         if (!TryGetOamSize(model.ReadMultiByteValue(oam, 2), model.ReadMultiByteValue(oam + 2, 2), out width, out height)) return false;
         if (width * height != pixelCount) { width = height = 0; return false; }
         return true;
      }

      /// <summary>
      /// How many different images the animations of a sprite template show: the biggest frame number in any of its animation sequences, plus one. Returns 0 if the template has no usable animations.
      /// The animation table is a list of pointers to sequences of 4-byte commands: frame (image number first, as a signed 16-bit value), loop (-3), jump (-2) and end (-1).
      /// </summary>
      private static int CountAnimationFrames(IDataModel model, IReadOnlyList<ArrayRunElementSegment> parent, int elementStart, int listOffset) {
         if (!IsPointerAt(parent, listOffset - 4)) return 0;
         var table = model.ReadPointer(elementStart + listOffset - 4);
         if (table < 0 || table + 4 > model.Count) return 0;
         // The player's table holds 28 sequences (the running ones, whose pictures 9 to 17 are the second half of the player's sheet, are number 20 to 23),
         // so every entry has to be read. The next table is another sprite's: it starts at the next anchor (the pointer that uses it makes one), so stop there.
         var nextAnchor = model.GetNextAnchor(table + 1).Start;
         var limit = nextAnchor > table && nextAnchor < model.Count ? nextAnchor - table : 0;
         var biggest = OverworldAnimations.BiggestFrame(OverworldAnimations.Read(model, table, limit));
         return biggest + 1;
      }

      private static bool IsPointerAt(IReadOnlyList<ArrayRunElementSegment> segments, int offset) {
         var current = 0;
         foreach (var segment in segments) {
            if (current == offset) return segment.Type == ElementContentType.Pointer && segment.Length == 4;
            if (current > offset) return false;
            current += segment.Length;
         }
         return false;
      }

      public override IDataFormat CreateDataFormat(IDataModel data, int index) => ITableRunExtensions.CreateSegmentDataFormat(this, data, index);

      protected override BaseRun Clone(SortedSpan<int> newPointerSources) => new OverworldSpriteListRun(model, parent, PaletteHint, RunIndex, Start, ElementCount, newPointerSources);

      public ITableRun Append(ModelDelta token, int length) {
         var naturalLength = Length;
         var newRun = model.RelocateForExpansion(token, this, naturalLength + length * ElementLength);

         // add new element data
         for (int i = 0; i < ElementLength * length; i++) {
            var prevIndex = newRun.Start + naturalLength + i - newRun.ElementLength * length;
            while (prevIndex < newRun.Start) prevIndex += newRun.ElementLength;
            byte prevData = prevIndex >= newRun.Start ? model[prevIndex] : default;
            token.ChangeData(model, newRun.Start + naturalLength + i, prevData);
         }

         // remove excess element data
         for (int i = naturalLength + length * ElementLength; i < naturalLength; i++) token.ChangeData(model, newRun.Start + i, 0xFF);

         return new OverworldSpriteListRun(model, parent, PaletteHint, RunIndex, newRun.Start, PointerSources);
      }

      public ITableRun Duplicate(int start, SortedSpan<int> pointerSources, IReadOnlyList<ArrayRunElementSegment> segments) => throw new NotImplementedException();

      public void AppendTo(IDataModel model, StringBuilder builder, int start, int length, int depth) => ITableRunExtensions.AppendTo(this, model, builder, start, length, depth);

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
            var listOffset = GetOffset<ArrayRunPointerSegment>(parent, pSeg => pSeg.InnerFormat.StartsWith("`osl")); // `osl` or `osl|palette hint`
            var elementStart = pointerSource - listOffset;
            var widthOffset = GetOffset(parent, seg => seg.Name == "width");
            var heightOffset = GetOffset(parent, seg => seg.Name == "height");
            var width = Math.Max(1, model.ReadMultiByteValue(elementStart + widthOffset, 2));
            var height = Math.Max(1, model.ReadMultiByteValue(elementStart + heightOffset, 2));
            var newTileWidth = (int)Math.Max(1, Math.Ceiling(width / 8.0));
            var newTileHeight = (int)Math.Max(1, Math.Ceiling(height / 8.0));
            if (newTileWidth > 30 || newTileHeight > 20) return this; // not a size an overworld sprite can have (see the sanity check in the constructor): don't resize megabytes of data to match
            var movedRuns = new List<ISpriteRun>();
            for (int i = 0; i < ElementCount; i++) {
               var spriteStart = model.ReadPointer(Start + ElementLength * i);
               var sprite = GetPageSprite(model, i);
               if (sprite is LzSpriteRun) continue; // compressed relative-frame image: can't be resized in place
               if (!movedRuns.Contains(sprite) && sprite != null) {
                  sprite = Resize(model, token, sprite, newTileWidth, newTileHeight);
                  movedRuns.Add(sprite);
               }
               var spriteLengthStart = Start + ElementLength * i + 4;
               model.WriteMultiByteValue(spriteLengthStart, 4, token, newTileWidth * newTileHeight * TileSize);
               if (sprite != null) spritesMoved |= spriteStart != sprite.Start;
            }
         }

         // if the parent paletteid changed, we just need to update the format, no data change is required.
         return new OverworldSpriteListRun(model, parent, PaletteHint, RunIndex, Start, PointerSources);
      }

      /// <summary>
      /// A list of frames can be pointed at from more than one table: a berry tree's picture table is used by the berry tree graphics
      /// and also listed in the berry stats. The size and palette come from the table that is laid out like the parent segments,
      /// not from whichever pointer happens to be first.
      /// </summary>
      private static int ParentSource(IDataModel model, IReadOnlyList<ArrayRunElementSegment> parent, SortedSpan<int> sources) {
         foreach (var source in sources) {
            if (!(model.GetNextRun(source) is ITableRun table) || table.ElementContent.Count != parent.Count) continue;
            var sameLayout = true;
            for (int i = 0; i < parent.Count && sameLayout; i++) sameLayout = table.ElementContent[i].Name == parent[i].Name && table.ElementContent[i].Length == parent[i].Length;
            if (sameLayout) return source;
         }
         return sources[0];
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

      /// <summary>
      /// The sprite that entry 'page' of the list points to, or null if the entry does not point at a sprite.
      /// An entry that is not a pointer reads as a negative address (not always -1), and the 'next run' from a negative address is the first run of the file: possibly some unrelated sprite (which would be drawn, or resized), or this very list.
      /// A list never holds a list either: a pointer back at a list (also at itself) would be followed forever.
      /// </summary>
      private ISpriteRun GetPageSprite(IDataModel model, int page) {
         var spriteStart = model.ReadPointer(Start + ElementLength * page);
         if (spriteStart < 0 || spriteStart >= model.Count) return null;
         var run = model.GetNextRun(spriteStart);
         if (run is OverworldSpriteListRun) return null;
         return run as ISpriteRun;
      }

      public byte[] GetData() {
         var pageSize = SpriteFormat.TileWidth * SpriteFormat.TileHeight * 32;
         var result = new byte[pageSize * Pages];
         for (int i = 0; i < Pages; i++) {
            var page = GetPageSprite(model, i)?.GetData() ?? new byte[pageSize];
            Array.Copy(page, 0, result, pageSize * i, pageSize);
         }
         return result;
      }

      // this implementation puts all the child sprites into separate pages
      public int[,] GetPixels(IDataModel model, int page, int arrayIndex) {
         var width = SpriteFormat.TileWidth * 8;
         var height = SpriteFormat.TileHeight * 8;
         var spriteRun = GetPageSprite(model, page);
         if (spriteRun == null) return new int[width, height];
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
            var spriteRun = GetPageSprite(model, i);
            if (spriteRun == null) continue;
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
         var spriteRun = GetPageSprite(model, page);
         if (spriteRun == null) return this;

         spriteRun.SetPixels(model, token, 0, pixels);
         return this; // moving a page will never move the list
      }

      // we should never try to set a overworld sprite list from a tileset
      public ISpriteRun SetPixels(IDataModel model, ModelDelta token, IReadOnlyList<int[,]> tiles) => throw new NotImplementedException();

      public ISpriteRun Duplicate(SpriteFormat newFormat) => new OverworldSpriteListRun(model, parent, PaletteHint, RunIndex, Start, PointerSources);
   }
}
