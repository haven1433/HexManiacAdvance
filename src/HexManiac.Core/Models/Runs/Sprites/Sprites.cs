using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HavenSoft.HexManiac.Core.Models.Runs.Sprites {
   public struct SpriteFormat {
      public int BitsPerPixel { get; }
      public int TileWidth { get; }
      public int TileHeight { get; }
      public int ExpectedByteLength { get; }
      public string PaletteHint { get; }
      public bool AllowLengthErrors { get; }
      public SpriteFormat(int bitsPerPixel, int width, int height, string paletteHint, bool allowLengthErrors = false) {
         (BitsPerPixel, TileWidth, TileHeight) = (bitsPerPixel, width, height);
         PaletteHint = paletteHint ?? string.Empty;
         ExpectedByteLength = 8 * BitsPerPixel * TileWidth * TileHeight;
         AllowLengthErrors = allowLengthErrors;
      }
      public override bool Equals(object? obj) {
         if (obj is not SpriteFormat other) return false;
         return
            BitsPerPixel == other.BitsPerPixel &&
            TileWidth == other.TileWidth &&
            TileHeight == other.TileHeight &&
            ExpectedByteLength == other.ExpectedByteLength &&
            PaletteHint == other.PaletteHint &&
            AllowLengthErrors == other.AllowLengthErrors;
      }
   }

   public struct PaletteFormat {
      public int Bits { get; }
      public int InitialBlankPages { get; }
      public int Pages { get; }
      public bool AllowLengthErrors { get; }
      public int ExpectedByteLengthPerPage => (int)Math.Pow(2, Bits + 1);

      public PaletteFormat(int bits, int pages, int initialBlankPages = 0, bool allowLengthErrors = false) => (Bits, Pages, InitialBlankPages, AllowLengthErrors) = (bits, pages, initialBlankPages, allowLengthErrors);
   }

   public struct TilesetFormat {
      public int BitsPerPixel { get; }
      public int Tiles { get; }
      public int MaxTiles { get; }
      public string PaletteHint { get; }
      public bool AllowLengthErrors { get; }
      public TilesetFormat(int bitsPerPixel, string paletteHint, bool allowLengthErrors = false) => (BitsPerPixel, Tiles, MaxTiles, PaletteHint, AllowLengthErrors) = (bitsPerPixel, -1, -1, paletteHint, allowLengthErrors);
      public TilesetFormat(int bitsPerPixel, int tiles, int maxTiles, string paletteHint, bool allowLengthErrors = false) : this(bitsPerPixel, paletteHint, allowLengthErrors) => (Tiles, MaxTiles) = (tiles, maxTiles);
   }

   public struct TilemapFormat {
      public int BitsPerPixel { get; }
      public int TileWidth { get; }
      public int TileHeight { get; }
      public int ExpectedUncompressedLength => TileWidth * TileHeight * 2; // TODO handle BitsPerPixel
      public string MatchingTileset { get; }
      public string TilesetTableMember { get; }
      public bool AllowLengthErrors { get; }
      public TilemapFormat(int bits, int width, int height, string tileset, string tilesetTableMember = null, bool allowLengthErrors = false) {
         BitsPerPixel = bits;
         TileWidth = width;
         TileHeight = height;
         MatchingTileset = tileset ?? string.Empty;
         TilesetTableMember = tilesetTableMember;
         AllowLengthErrors = allowLengthErrors;
      }
   }

   public interface IPagedRun : IAppendToBuilderRun {
      int Pages { get; }
   }

   public interface ISpriteRun : IPagedRun {
      bool SupportsImport { get; }
      bool SupportsEdit { get; }
      SpriteFormat SpriteFormat { get; }
      int DecompressedLength { get; }
      int[,] GetPixels(IDataModel model, int page, int tableIndex);
      ISpriteRun SetPixels(IDataModel model, ModelDelta token, int page, int[,] pixels);
      ISpriteRun SetPixels(IDataModel model, ModelDelta token, IReadOnlyList<int[,]> tiles);
      ISpriteRun Duplicate(SpriteFormat newFormat);
      byte[] GetData();
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

      /// <summary>
      /// Uses the hint, as well as this sprite's table location (if any), to find palettes that can be applied to this sprite.
      /// (1) if the sprite's hint is the name of a palette, return that. Example: title screen pokemon sprite/palette pair.
      /// (2) if the sprite's hint is the name of an enum table, use that enum's source as a list of palettes and get the appropriate one from the matching index of the enum table. Example: pokemon icons
      /// (3) if the sprite's hint is a table name followed by a key=value pair, go grab the a palette from the element within that table such that it's key equals that value. Example: Overworld sprites
      /// (4) if the sprite's hint is a table name, return all palettes within the matching index of that table. Example: trainer sprites/palettes.
      /// (5) if the sprite has no hint, return all palettes in arrays with matching length from the same index. Example: pokemon sprites. Leaving it empty allows both normal and shiny palettes to match.
      /// </summary>
      public static IReadOnlyList<IPaletteRun> FindRelatedPalettes(this ISpriteRun spriteRun, IDataModel model, int primarySource = -1, string hint = null, bool includeAllTableIndex = false) {
         // find all palettes that could be applied to this sprite run
         var noChange = new NoDataChangeDeltaModel();
         var results = new List<IPaletteRun>();
         if (spriteRun?.SpriteFormat.BitsPerPixel < 4) return results; // 1- and 2-bit sprites don't have palettes
         hint = hint ?? spriteRun?.SpriteFormat.PaletteHint;
         if (primarySource == -1) {
            var pointerCount = spriteRun?.PointerSources?.Count ?? 0;
            for (int i = 0; i < pointerCount; i++) {
               if (!(model.GetNextRun(spriteRun.PointerSources[i]) is ArrayRun)) continue;
               primarySource = spriteRun.PointerSources[i];
               break;
            }
            // no array source: fall back to any table source (ex: a sprite/palette pair inside a `[sprite<> palette<>]1` sub-table)
            if (primarySource == -1) {
               for (int i = 0; i < pointerCount; i++) {
                  if (!(model.GetNextRun(spriteRun.PointerSources[i]) is ITableRun)) continue;
                  primarySource = spriteRun.PointerSources[i];
                  break;
               }
            }
         }
         var spriteTable = model.GetNextRun(primarySource) as ITableRun;
         var offset = spriteTable?.ConvertByteOffsetToArrayOffset(primarySource) ?? new ArrayOffset(-1, -1, -1, -1);
         if (primarySource < 0) offset = new ArrayOffset(-1, -1, -1, -1);

         // if the current hint is a palette table and a matched value (like id=1104) and the spriteTable is a single-element table,
         // we need to check for a field with a name that matches the hint field and possibly update
         if (hint != null && hint.Contains(":") && hint.Contains("=") && spriteTable?.ElementCount == 1) {
            var hintField = hint.Split(":")[1].Split("=")[0];
            hintField = spriteTable.ElementContent.Select(seg => seg.Name).FirstOrDefault(seg => seg.Contains(hintField));
            if (hintField != null) {
               var table = new ModelTable(model, spriteTable);
               var hintValue = table[0].GetValue(hintField);
               hint = hint.Split("=")[0] + $"={hintValue:X4}";
            }
         }

         if (!string.IsNullOrEmpty(hint)) {
            hint = ChoosePaletteTable(model, noChange, hint);
            var address = model.GetAddressFromAnchor(noChange, -1, hint);
            var run = model.GetNextRun(address);
            if (run is IPaletteRun palRun && palRun.Start == address) {
               // option 1: hint is to a palette
               results.Add(palRun);
               return results;
            } else if (run is ArrayRun enumArray && enumArray.ElementContent.Count == 1 && enumArray.ElementContent[0] is ArrayRunEnumSegment enumSegment) {
               // option 2: hint is to index into paletteTable, and I'm in a table
               var paletteTable = model.GetNextRun(model.GetAddressFromAnchor(noChange, -1, enumSegment.EnumName)) as ITableRun;
               if (offset.ElementIndex != -1 && paletteTable != null) {
                  var paletteIndex = model.ReadMultiByteValue(enumArray.Start + enumArray.ElementLength * offset.ElementIndex, enumArray.ElementLength);
                  var destination = model.ReadPointer(paletteTable.Start + paletteTable.ElementLength * paletteIndex);
                  var tempRun = model.GetNextRun(destination);
                  if (tempRun is IPaletteRun pRun && pRun.Start == destination) {
                     results.Add(pRun);
                  }
               }
            } else if (hint.Contains(":")) {
               // option 3: hint is a table name, followed by a identifier=value pair
               var tableKeyPair = hint.Split(':');
               var identifierValuePair = tableKeyPair.Length == 2 ? tableKeyPair[1].Split("=") : new string[0];
               if (identifierValuePair.Length == 2) {
                  var paletteTable = model.GetNextRun(model.GetAddressFromAnchor(noChange, -1, tableKeyPair[0])) as ITableRun;
                  var segment = paletteTable?.ElementContent.FirstOrDefault(seg => seg.Name == identifierValuePair[0]);

                  // option 3b: the identifier names one or more palette-pointer fields of that table and the value is the (hex) element index.
                  // Example: `osl|data.pokemon.stats:owPalette,owShinyPalette` -> frames get the hint "data.pokemon.stats:owPalette,owShinyPalette=0019"
                  // and use the overworld palettes stored in species 0x19's own element.
                  var fieldNames = identifierValuePair[0].Split(',');
                  var paletteFields = paletteTable?.ElementContent.Where(seg => fieldNames.Contains(seg.Name) && seg is ArrayRunPointerSegment pSeg &&
                     (PaletteRun.TryParsePaletteFormat(pSeg.InnerFormat, out var _) || LzPaletteRun.TryParsePaletteFormat(pSeg.InnerFormat, out var _))).ToList();
                  if (paletteFields != null && paletteFields.Count > 0 && paletteFields.Count == fieldNames.Length) {
                     // prefer the element that actually (transitively) points at this sprite; fall back to the index in the hint.
                     var elementIndex = FindOwningElementIndex(model, spriteRun, paletteTable);
                     if (elementIndex == -1 && int.TryParse(identifierValuePair[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hintIndex)) elementIndex = hintIndex;
                     if (elementIndex >= 0 && elementIndex < paletteTable.ElementCount) {
                        foreach (var field in paletteFields) {
                           var fieldOffset = paletteTable.ElementContent.Until(seg => seg == field).Sum(seg => seg.Length);
                           var paletteStart = model.ReadPointer(paletteTable.Start + elementIndex * paletteTable.ElementLength + fieldOffset);
                           if (model.GetNextRun(paletteStart) is IPaletteRun fieldPalette && fieldPalette.Start == paletteStart) results.Add(fieldPalette);
                        }
                     }
                     return results;
                  }

                  var pSegment = paletteTable?.ElementContent.FirstOrDefault(seg => seg is ArrayRunPointerSegment pSeg && PaletteRun.TryParsePaletteFormat(pSeg.InnerFormat, out var _));
                  if (pSegment == null) pSegment = paletteTable?.ElementContent.FirstOrDefault(seg => seg is ArrayRunPointerSegment pSeg && LzPaletteRun.TryParsePaletteFormat(pSeg.InnerFormat, out var _));
                  var rawValue = identifierValuePair[1];
                  int keyValue;
                  if (segment is ArrayRunEnumSegment eSegment) keyValue = eSegment.GetOptions(model).ToList().IndexOf(rawValue);
                  else if (segment is ArrayRunHexSegment hSegment) int.TryParse(rawValue, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out keyValue);
                  else int.TryParse(rawValue, out keyValue);
                  if (segment != null) {
                     var segmentOffset = paletteTable.ElementContent.Until(seg => seg == segment).Sum(seg => seg.Length);
                     var pSegmentOffset = paletteTable.ElementContent.Until(seg => seg == pSegment).Sum(seg => seg.Length);
                     var tableIndex = paletteTable.ElementCount.Range().FirstOrDefault(i => model.ReadMultiByteValue(paletteTable.Start + i * paletteTable.ElementLength + segmentOffset, segment.Length) == keyValue);
                     var paletteStart = model.ReadPointer(paletteTable.Start + tableIndex * paletteTable.ElementLength + pSegmentOffset);
                     if (model.GetNextRun(paletteStart) is IPaletteRun pRun && pRun.Start == paletteStart) results.Add(pRun);
                  }
               }
            }

            // option 3c: `sibling/field` - the palette is the 'field' pointer of the struct that this element's 'sibling' pointer points at.
            // Example: a door's tiles<`ucs4x2x12|tileset/pal`> use the 'pal' palette of the tileset named by the door's 'tileset' pointer.
            if (hint.Contains('/') && !hint.Contains(':') && spriteTable != null && offset.ElementIndex != -1) {
               var parts = hint.Split('/');
               if (parts.Length == 2) {
                  var sibling = spriteTable.ElementContent.FirstOrDefault(seg => seg.Name == parts[0] && seg.Type == ElementContentType.Pointer);
                  if (sibling != null) {
                     var siblingOffset = spriteTable.ElementContent.Until(seg => seg == sibling).Sum(seg => seg.Length);
                     var target = model.ReadPointer(spriteTable.Start + offset.ElementIndex * spriteTable.ElementLength + siblingOffset);
                     if (target >= 0 && target < model.Count && model.GetNextRun(target) is ITableRun targetTable && targetTable.Start == target) {
                        var field = targetTable.ElementContent.FirstOrDefault(seg => seg.Name == parts[1] && seg.Type == ElementContentType.Pointer);
                        if (field != null) {
                           var fieldOffset = targetTable.ElementContent.Until(seg => seg == field).Sum(seg => seg.Length);
                           var paletteStart = model.ReadPointer(targetTable.Start + fieldOffset);
                           if (model.GetNextRun(paletteStart) is IPaletteRun fieldPalette && fieldPalette.Start == paletteStart) {
                              results.Add(fieldPalette);
                              return results;
                           }
                        }
                     }
                  }
               }
            }

            // option 4: I'm in a table, and my hint is a table name
            if (offset.ElementIndex == -1) return results;
            if (!(run is ArrayRun array)) return results;
            results.AddRange(model.GetPointedChildren<IPaletteRun>(array, offset.ElementIndex));
         } else if (spriteTable != null) {
            // option 5: this sprite is in an array, so get all palettes in all related arrays
            if (spriteTable is ArrayRun arrayRun) {
               foreach (var relatedTable in GetLengthSortedRelatedArrays(model, arrayRun)) {
                  if (relatedTable == spriteTable) continue; // skip self, I'll handle my own relation later
                  results.AddRange(model.GetPointedChildren<IPaletteRun>(relatedTable, offset.ElementIndex));
               }
            }
            // this sprite may appear in the table multiple times, with different palettes. Example: potions
            if (includeAllTableIndex) {
               var segStart = offset.SegmentStart - offset.ElementIndex * spriteTable.ElementLength - spriteTable.Start;
               for (int i = 0; i < spriteTable.ElementCount; i++) {
                  if (model.ReadPointer(spriteTable.Start + spriteTable.ElementLength * i + segStart) != spriteRun.Start) continue;
                  results.AddRange(model.GetPointedChildren<IPaletteRun>(spriteTable, i));
               }
            } else {
               results.AddRange(model.GetPointedChildren<IPaletteRun>(spriteTable, offset.ElementIndex));
               return KeepPalettesOfTheSameVariant(model, spriteTable, offset, results);
            }
         }
         return results;
      }

      /// <summary>
      /// A table element may hold several sprite/palette sets side by side: normal and female, or the regular sprites and the overworld ones.
      /// Example: frontPic, palette, shinyPalette, frontPicFemale, paletteFemale, shinyPaletteFemale.
      /// A sprite should only offer the palettes of its own set (normal + shiny), so the female and overworld palettes are dropped
      /// unless the sprite's own field name says it belongs to that set. If nothing would be left, everything is kept.
      /// </summary>
      private static IReadOnlyList<IPaletteRun> KeepPalettesOfTheSameVariant(IDataModel model, ITableRun table, ArrayOffset offset, List<IPaletteRun> palettes) {
         if (palettes.Count < 3 || offset.ElementIndex < 0 || offset.SegmentIndex < 0 || offset.SegmentIndex >= table.ElementContent.Count) return palettes;
         var spriteVariant = FieldVariant(table.ElementContent[offset.SegmentIndex].Name);
         var kept = new List<IPaletteRun>();
         foreach (var palette in palettes) {
            var field = FindPointerFieldName(model, table, offset.ElementIndex, palette.Start);
            if (field == null || FieldVariant(field) == spriteVariant) kept.Add(palette);
         }
         return kept.Count == 0 ? palettes : kept;
      }

      private static string FindPointerFieldName(IDataModel model, ITableRun table, int elementIndex, int destination) {
         var segmentOffset = 0;
         foreach (var segment in table.ElementContent) {
            if (segment is ArrayRunPointerSegment) {
               var source = table.Start + table.ElementLength * elementIndex + segmentOffset;
               if (source >= 0 && source + 4 <= model.Count && model.ReadPointer(source) == destination) return segment.Name;
            }
            segmentOffset += segment.Length;
         }
         return null;
      }

      private static (bool female, bool overworld) FieldVariant(string name) {
         name = name ?? string.Empty;
         var overworld = name.Length > 2 && name.StartsWith("ow") && char.IsUpper(name[2]);
         return (name.Contains("Female"), overworld);
      }

      /// <summary>
      /// A key=value hint may name several palette tables, separated by commas: `tableA,tableB:id=1004`.
      /// The first table with an element that has the key is the one to use (field effects have their own palettes besides the overworld ones).
      /// If none has the key, the first table is used, like it is for a hint that names a single table.
      /// </summary>
      private static string ChoosePaletteTable(IDataModel model, ModelDelta noChange, string hint) {
         var colon = hint.IndexOf(':');
         if (colon < 0 || !hint.Substring(0, colon).Contains(',')) return hint;
         var names = hint.Substring(0, colon).Split(',');
         var rest = hint.Substring(colon);
         var identifierValuePair = rest.Substring(1).Split("=");
         if (identifierValuePair.Length != 2) return names[0] + rest;
         foreach (var name in names) {
            if (!(model.GetNextRun(model.GetAddressFromAnchor(noChange, -1, name)) is ITableRun table)) continue;
            var segment = table.ElementContent.FirstOrDefault(seg => seg.Name == identifierValuePair[0]);
            if (segment == null) continue;
            int keyValue;
            if (segment is ArrayRunEnumSegment eSegment) keyValue = eSegment.GetOptions(model).ToList().IndexOf(identifierValuePair[1]);
            else if (segment is ArrayRunHexSegment) int.TryParse(identifierValuePair[1], NumberStyles.HexNumber, CultureInfo.CurrentCulture, out keyValue);
            else int.TryParse(identifierValuePair[1], out keyValue);
            var segmentOffset = table.ElementContent.Until(seg => seg == segment).Sum(seg => seg.Length);
            for (int i = 0; i < table.ElementCount; i++) {
               if (model.ReadMultiByteValue(table.Start + i * table.ElementLength + segmentOffset, segment.Length) == keyValue) return name + rest;
            }
         }
         return names[0] + rest;
      }

      /// <summary>
      /// Walk up the pointer chain (sprite -> frame list -> table element ...) to find which element of 'table' owns this run.
      /// Returns -1 if the run isn't reachable from the table within a few hops.
      /// </summary>
      public static int FindOwningElementIndex(IDataModel model, IFormattedRun run, ITableRun table, int maxDepth = 4) {
         var visited = new HashSet<int>();
         var frontier = new List<IFormattedRun> { run };
         for (int depth = 0; depth < maxDepth && frontier.Count > 0; depth++) {
            var next = new List<IFormattedRun>();
            foreach (var current in frontier) {
               if (current?.PointerSources == null) continue;
               foreach (var source in current.PointerSources) {
                  if (source >= table.Start && source < table.Start + table.Length) {
                     return table.ConvertByteOffsetToArrayOffset(source).ElementIndex;
                  }
                  var owner = model.GetNextRun(source);
                  if (owner == null || owner.Start > source || owner is not ITableRun) continue;
                  if (!visited.Add(owner.Start)) continue;
                  next.Add(owner);
               }
            }
            frontier = next;
         }
         return -1;
      }

      /// <summary>
      /// Find all sprites that depend on a palette, either explicitly or implicitly
      /// </summary>
      public static IReadOnlyList<ISpriteRun> FindDependentSprites(this IPaletteRun run, IDataModel model) {
         var results = new List<ISpriteRun>();

         var tableSources = new List<int>();
         for (int i = 0; i < run.PointerSources.Count; i++) {
            if (!(model.GetNextRun(run.PointerSources[i]) is ITableRun)) continue;
            tableSources.Add(run.PointerSources[i]);
         }

         // part of a table
         if (tableSources.Count > 0) {
            foreach (var primarySource in tableSources) {
               var tableRun = (ITableRun)model.GetNextRun(primarySource);
               var primaryName = model.GetAnchorFromAddress(-1, tableRun.Start);
               var offset = tableRun.ConvertByteOffsetToArrayOffset(primarySource);

               // find all sprites within tables of the same length that reference this table or reference nothing at all
               if (tableRun is ArrayRun arrayRun) {
                  foreach (var table in GetLengthSortedRelatedArrays(model, arrayRun)) {
                     var elementOffset = table.ElementLength * offset.ElementIndex;
                     for (int segmentIndex = 0; segmentIndex < table.ElementContent.Count; segmentIndex++) {
                        if (table.ElementContent[segmentIndex] is not ArrayRunPointerSegment pointerSegment) continue;
                        if (model.GetNextRun(table.ReadPointer(model, offset.ElementIndex, segmentIndex)) is not ISpriteRun spriteRun) continue;
                        if (spriteRun is LzTilemapRun) continue; // don't count tilemaps
                        if (spriteRun.SpriteFormat.BitsPerPixel != run.PaletteFormat.Bits) continue; // only worry about sprites that could use this palette
                        var paletteHint = spriteRun.SpriteFormat.PaletteHint;
                        if (!string.IsNullOrEmpty(paletteHint) && paletteHint != primaryName && !pointerSegment.InnerFormat.EndsWith($"|{primaryName}`")) continue;
                        results.Add(spriteRun);
                     }
                  }
               }

               // if tableRun is used as an enum in indexTable, we care about payload tables with the same length as the index table that have sprites that use the index table as a paletteHint.
               // in that case, we want every sprite from the payload where the index matches the current tableRun index
               if (!string.IsNullOrEmpty(primaryName)) {
                  foreach (var indexTable in model.GetEnumArrays(primaryName)) {
                     if (indexTable.ElementContent.Count != 1) continue;
                     var indexTableName = model.GetAnchorFromAddress(-1, indexTable.Start);
                     foreach (var payloadTable in model.GetRelatedArrays(indexTable)) {
                        foreach (var segment in payloadTable.ElementContent) {
                           if (!(segment is ArrayRunPointerSegment pSegment)) continue;
                           var format = default(SpriteFormat);
                           if (SpriteRun.TryParseSpriteFormat(pSegment.InnerFormat, out var sf1)) format = sf1;
                           if (LzSpriteRun.TryParseSpriteFormat(pSegment.InnerFormat, out var sf2)) format = sf2;
                           if (format.BitsPerPixel == default) continue;
                           if (format.PaletteHint != indexTableName) continue;
                           var elementPartOffset = payloadTable.ElementContent.Until(content => content == segment).Sum(content => content.Length);
                           var checkLength = Math.Min(payloadTable.ElementCount, indexTable.ElementCount);
                           for (int i = 0; i < checkLength; i++) {
                              var index = model.ReadMultiByteValue(indexTable.Start + indexTable.ElementLength * i, indexTable.ElementLength);
                              if (offset.ElementIndex != index) continue;
                              var elementOffset = payloadTable.ElementLength * i;
                              var destination = model.ReadPointer(payloadTable.Start + elementOffset + elementPartOffset);
                              if (!(model.GetNextRun(destination) is ISpriteRun spriteRun)) continue;
                              if (spriteRun is LzTilemapRun) continue; // don't count tilemaps
                              results.Add(spriteRun);
                           }
                        }
                     }
                  }

                  // look for sprites that specify that they use this palette from this table, found via a key (example: overworld sprites)
                  // this is time consuming, so only do it if we haven't found any sprites yet
                  if (results.Count == 0) {
                     foreach (var sprite in model.All<SpriteRun>()) {
                        var hint = sprite.SpriteFormat.PaletteHint;
                        if (string.IsNullOrEmpty(hint)) continue;
                        var tableKeyPair = hint.Split(':');
                        if (tableKeyPair[0] != primaryName) continue;
                        var identifierValuePair = tableKeyPair.Length == 2 ? tableKeyPair[1].Split("=") : new string[0];
                        if (identifierValuePair.Length != 2) continue;
                        var (tableName, keyName, keyValue) = (tableKeyPair[0], identifierValuePair[0], identifierValuePair[1]);
                        var keyOffset = tableRun.ElementContent.Until(seg => seg.Name == keyName).Sum(seg => seg.Length);
                        if (keyOffset == tableRun.ElementLength) continue;
                        var keySegment = tableRun.ElementContent.First(seg => seg.Name == keyName);
                        var actualValue = model.ReadMultiByteValue(tableRun.Start + tableRun.ElementLength * offset.ElementIndex + keyOffset, keySegment.Length);
                        if (!int.TryParse(keyValue, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out int expectedValue)) continue;
                        if (actualValue != expectedValue) continue;
                        results.Add(sprite);
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
            if (spriteRun is ITilemapRun) continue; // don't count tilemaps
            if (spriteRun.SpriteFormat.PaletteHint != name) continue;
            results.Add(spriteRun);
         }

         return results;
      }

      public static IReadOnlyList<ITilemapRun> FindDependentTilemaps(this ISpriteRun tileset, IDataModel model) {
         var results = new List<ITilemapRun>();

         // if the tileset is part of a table, find other tilemaps at the same index in the table
         if (tileset.PointerSources.Count > 0 && model.GetNextRun(tileset.PointerSources[0]) is ArrayRun table) {
            var offset = table.ConvertByteOffsetToArrayOffset(tileset.PointerSources[0]);
            foreach (var relatedTable in model.GetRelatedArrays(table)) {
               foreach (var spriteRun in model.GetPointedChildren<ISpriteRun>(relatedTable, offset.ElementIndex)) {
                  var tableName = model.GetAnchorFromAddress(-1, table.Start);

                  // we only care about tilemaps that specifically want _this_ tileset
                  if (!(spriteRun is ITilemapRun tilemap)) continue;
                  if (tilemap.Format.MatchingTileset != tableName) continue;
                  if (!string.IsNullOrEmpty(tilemap.Format.TilesetTableMember) && tilemap.Format.TilesetTableMember != table.ElementContent[offset.SegmentIndex].Name) continue;

                  results.Add(tilemap);
               }
            }
         }

         // if the tileset has a name, find tilemaps that depend on that name
         var tilesetName = model.GetAnchorFromAddress(-1, tileset.Start);
         if (!string.IsNullOrWhiteSpace(tilesetName)) {
            foreach (var anchor in model.Anchors) {
               var address = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, anchor);
               var anchorRun = model.GetNextRun(address);
               if (!(anchorRun is ITilemapRun tilemap)) continue;
               if (tilemap.Format.MatchingTileset != tilesetName) continue;
               results.Add(tilemap);
            }
         }

         return results;
      }

      /// <summary>
      /// Returns all related arrays, sorted with the longer anchor names first.
      /// This unusual strategy allows 'front' before 'back' and 'normal' before 'shiny'.
      /// </summary>
      public static List<ArrayRun> GetLengthSortedRelatedArrays(IDataModel model, ArrayRun arrayRun) {
         var relatedArrays = model.GetRelatedArrays(arrayRun).ToList();

         // sort by name length
         relatedArrays.Sort((a, b) => model.GetAnchorFromAddress(-1, b.Start).Length - model.GetAnchorFromAddress(-1, a.Start).Length);

         return relatedArrays;
      }
   }
}
