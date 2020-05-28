using System;
using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.HexManiac.Core.Models.Runs.Sprites {
   public struct SpriteFormat {
      public int BitsPerPixel { get; }
      public int TileWidth { get; }
      public int TileHeight { get; }
      public int ExpectedByteLength { get; }
      public string PaletteHint { get; }
      public SpriteFormat(int bitsPerPixel, int width, int height, string paletteHint) {
         (BitsPerPixel, TileWidth, TileHeight) = (bitsPerPixel, width, height);
         PaletteHint = paletteHint;
         ExpectedByteLength = 8 * BitsPerPixel * TileWidth * TileHeight;
      }
   }

   public struct PaletteFormat {
      public int Bits { get; }
      public int InitialBlankPages { get; }
      public int Pages { get; }
      public int ExpectedByteLengthPerPage => (int)Math.Pow(2, Bits + 1);

      public PaletteFormat(int bits, int pages, int initialBlankPages = 0) => (Bits, Pages, InitialBlankPages) = (bits, pages, initialBlankPages);
   }

   public struct TilesetFormat {
      public int BitsPerPixel { get; }
      public int Tiles { get; }
      public string PaletteHint { get; }
      public TilesetFormat(int bitsPerPixel, string paletteHint) => (BitsPerPixel, Tiles, PaletteHint) = (bitsPerPixel, -1, paletteHint);
      public TilesetFormat(int bitsPerPixel, int tiles, string paletteHint) : this(bitsPerPixel, paletteHint) => Tiles = tiles;
   }

   public struct TilemapFormat {
      public int BitsPerPixel { get; }
      public int TileWidth { get; }
      public int TileHeight { get; }
      public int ExpectedUncompressedLength => TileWidth * TileHeight * 2; // TODO handle BitsPerPixel
      public string MatchingTileset { get; }
      public string TilesetTableMember { get; }
      public TilemapFormat(int bits, int width, int height, string tileset, string tilesetTableMember = null) {
         BitsPerPixel = bits;
         TileWidth = width;
         TileHeight = height;
         MatchingTileset = tileset ?? string.Empty;
         TilesetTableMember = tilesetTableMember;
      }
   }

   public interface IPagedRun : IAppendToBuilderRun {
      int Pages { get; }
   }

   public interface ISpriteRun : IPagedRun {
      SpriteFormat SpriteFormat { get; }
      int[,] GetPixels(IDataModel model, int page);
      ISpriteRun SetPixels(IDataModel model, ModelDelta token, int page, int[,] pixels);
      ISpriteRun Duplicate(SpriteFormat newFormat);
   }

   public interface IPaletteRun : IPagedRun {
      PaletteFormat PaletteFormat { get; }
      IReadOnlyList<short> GetPalette(IDataModel model, int page);
      IPaletteRun SetPalette(IDataModel model, ModelDelta token, int page, IReadOnlyList<short> colors);
      IPaletteRun Duplicate(PaletteFormat newFormat);
   }

   public static class IPaletteRunExtensions {
      public static IReadOnlyList<short> AllColors(this IPaletteRun run, IDataModel model) {
         if (run.PaletteFormat.Bits == 8) return run.GetPalette(model, 0);
         var collection = new List<short>();

         for (int i = 0; i < run.Pages; i++) collection.AddRange(run.GetPalette(model, i));

         return collection;
      }

      public static IReadOnlyList<IPaletteRun> FindRelatedPalettes(this ISpriteRun spriteRun, IDataModel model) {
         // find all palettes that could be applied to this sprite run
         var results = new List<IPaletteRun>();
         var hint = spriteRun.SpriteFormat.PaletteHint;
         var primarySource = -1;
         for (int i = 0; i < spriteRun.PointerSources.Count; i++) {
            if (!(model.GetNextRun(spriteRun.PointerSources[i]) is ArrayRun)) continue;
            primarySource = spriteRun.PointerSources[i];
            break;
         }
         var spriteTable = model.GetNextRun(primarySource) as ArrayRun;
         var offset = spriteTable?.ConvertByteOffsetToArrayOffset(primarySource) ?? new ArrayOffset(-1, -1, -1, -1);
         if (primarySource < 0) offset = new ArrayOffset(-1, -1, -1, -1);

         if (!string.IsNullOrEmpty(hint)) {
            var run = model.GetNextRun(model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, hint));
            if (run is IPaletteRun palRun) {
               // option 1: hint is to a palette
               results.Add(palRun);
               return results;
            } else if (run is ArrayRun enumArray && enumArray.ElementContent.Count == 1 && enumArray.ElementContent[0] is ArrayRunEnumSegment enumSegment) {
               // option 2: hint is to index into paletteTable, and I'm in a table
               var paletteTable = model.GetNextRun(model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, enumSegment.EnumName)) as ArrayRun;
               if (offset.ElementIndex != -1) {
                  var paletteIndex = model.ReadMultiByteValue(enumArray.Start + enumArray.ElementLength * offset.ElementIndex, enumArray.ElementLength);
                  if (model.GetNextRun(model.ReadPointer(paletteTable.Start + paletteTable.ElementLength * paletteIndex)) is IPaletteRun pRun) {
                     results.Add(pRun);
                  }
               }
            }

            // option 3: I'm in a table, and my hint is a table name
            if (offset.ElementIndex == -1) return results;
            if (!(run is ArrayRun array)) return results;
            results.AddRange(model.GetPointedChildren<IPaletteRun>(array, offset.ElementIndex));
         } else if (spriteTable != null) {
            // option 4: this sprite is in an array, so get all palettes in all related arrays
            foreach (var relatedTable in model.GetRelatedArrays(spriteTable)) {
               results.AddRange(model.GetPointedChildren<IPaletteRun>(relatedTable, offset.ElementIndex));
            }

         }
         return results;
      }

      public static IReadOnlyList<ISpriteRun> FindDependentSprites(this IPaletteRun run, IDataModel model) {
         var results = new List<ISpriteRun>();

         // part of a table
         if (run.PointerSources.Count > 0) {
            var primarySource = run.PointerSources[0];
            for (int i = 0; i < run.PointerSources.Count; i++) {
               if (!(model.GetNextRun(run.PointerSources[i]) is ArrayRun)) continue;
               primarySource = run.PointerSources[i];
               break;
            }

            if (model.GetNextRun(primarySource) is ArrayRun tableRun) {
               var primaryName = model.GetAnchorFromAddress(-1, tableRun.Start);
               var offset = tableRun.ConvertByteOffsetToArrayOffset(primarySource);

               // find all sprites within tables of the same length that reference this table or reference nothing at all
               foreach (var table in model.GetRelatedArrays(tableRun)) {
                  var elementOffset = table.ElementLength * offset.ElementIndex;
                  foreach(var spriteRun in model.GetPointedChildren<ISpriteRun>(table, offset.ElementIndex)) {
                     var paletteHint = spriteRun.SpriteFormat.PaletteHint;
                     if (!string.IsNullOrEmpty(paletteHint) && paletteHint != primaryName) continue;
                     results.Add(spriteRun);
                  }
               }

               // if tableRun is used as an enum in indexTable, we care about payload tables with the same length as the index table that have sprites that use the index table as a paletteHint.
               // in that case, we want every sprite from the payload where the index matches the current tableRun index
               foreach (var indexTable in model.GetEnumArrays(primaryName)) {
                  if (indexTable.ElementContent.Count != 1) continue;
                  var indexTableName = model.GetAnchorFromAddress(-1, indexTable.Start);
                  foreach (var payloadTable in model.GetRelatedArrays(indexTable)) {
                     if (payloadTable.ElementCount != indexTable.ElementCount) continue;
                     foreach (var segment in payloadTable.ElementContent) {
                        if (!(segment is ArrayRunPointerSegment pSegment)) continue;
                        var (parseSuccess, format) = (false, default(SpriteFormat));
                        if (SpriteRun.TryParseSpriteFormat(pSegment.InnerFormat, out var sf1)) (parseSuccess, format) = (true, sf1);
                        if (LzSpriteRun.TryParseSpriteFormat(pSegment.InnerFormat, out var sf2)) (parseSuccess, format) = (true, sf2);
                        if (!parseSuccess) continue;
                        if (format.PaletteHint != indexTableName) continue;
                        var elementPartOffset = payloadTable.ElementContent.Until(content => content == segment).Sum(content => content.Length);
                        for (int i = 0; i < indexTable.ElementCount; i++) {
                           var index = model.ReadMultiByteValue(indexTable.Start + indexTable.ElementLength * i, indexTable.ElementLength);
                           if (offset.ElementIndex != index) continue;
                           var elementOffset = payloadTable.ElementLength * i;
                           var destination = model.ReadPointer(payloadTable.Start + elementOffset + elementPartOffset);
                           if (!(model.GetNextRun(destination) is ISpriteRun spriteRun)) continue;
                           results.Add(spriteRun);
                        }
                     }
                  }
               }
            }
         }

         // loose
         var name = model.GetAnchorFromAddress(-1, run.Start);
         if (string.IsNullOrEmpty(name)) return results;
         foreach (var anchor in model.Anchors) {
            var address = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, anchor);
            var anchorRun = model.GetNextRun(address);
            if (!(anchorRun is ISpriteRun spriteRun)) continue;
            if (spriteRun.SpriteFormat.PaletteHint != name) continue;
            results.Add(spriteRun);
         }

         return results;
      }
   }
}
