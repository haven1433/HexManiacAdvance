using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.HexManiac.Core.ViewModels {
   /// <summary>
   /// A palette of the overworld that a field effect is drawn with, and where to find it in the data.
   /// </summary>
   public class FieldEffectPalette {
      /// <summary>The tag the field effect asks for (the palette lists find their palettes by this number).</summary>
      public int Tag { get; init; }

      /// <summary>The table that lists the palette, for example graphics.overworld.palettes2.</summary>
      public string TableName { get; init; }

      /// <summary>The element of that table.</summary>
      public int Index { get; init; }

      /// <summary>Where the sixteen colours are stored.</summary>
      public int Address { get; init; }

      public IReadOnlyList<short> Colors { get; init; }

      /// <summary>True if the effect does not name this palette (tag FFFF): it is only the one the pictures are drawn with here.</summary>
      public bool IsPreviewOnly { get; init; }

      /// <summary>A name for people: "general field effect palette", "cavedust palette", "overworld object palette".</summary>
      public string Title { get; init; }

      /// <summary>What to type in the Goto box to see the palette's entry: <c>graphics.overworld.palettes2/1</c>.</summary>
      public string GotoDestination => $"{TableName}/{Index}";
   }

   /// <summary>What the game's data says about one field effect (grass rustle, shadows, ripples, dust, sparkles, ...): a sprite template.</summary>
   public class FieldEffectInfo {
      public int Index { get; init; }

      /// <summary>Its name from the list of field effect names, or empty if the ROM has none.</summary>
      public string Name { get; init; } = string.Empty;

      /// <summary>Where the sprite template is stored (null pointer: -1).</summary>
      public int Template { get; init; } = -1;

      public int TileTag { get; init; } = -1;
      public int PaletteTag { get; init; } = -1;

      /// <summary>The size of one frame in pixels, from the template's OAM data (0 if it is not known).</summary>
      public int Width { get; init; }
      public int Height { get; init; }

      public int FrameCount { get; init; }

      /// <summary>The list of frames, or null if the template has none (the game's empty template, or a table without picture formats).</summary>
      public OverworldSpriteListRun List { get; init; }

      public FieldEffectPalette Palette { get; init; }

      public bool HasPicture => List != null;

      /// <summary>The palette tag the way the tables show it: four hex digits, or "none" for FFFF (the game picks the palette in code).</summary>
      public string PaletteTagText => PaletteTag < 0 ? "?" : PaletteTag == FieldEffects.NoTag ? "none" : PaletteTag.ToString("X4");
   }

   /// <summary>
   /// The field effects of a game: the objects that grass, water, sand, dust and shadows are made of, found in a table of sprite templates
   /// (<c>graphics.overworld.fieldeffects</c>, in unedited games and older tomls <c>graphics.overworld.sprites2</c>).
   /// A sprite template is { tileTag, paletteTag, oam, anims, images, affineAnims, callback }: this class reads the parts a picture needs
   /// and resolves the palette through the tag the way the game does (the general field effect palettes, the palettes of the effect's own, or an overworld object's).
   /// </summary>
   public static class FieldEffects {
      public const string FriendlyName = "Field Effects";
      public const string TableName = "graphics.overworld.fieldeffects";
      public const string LegacyTableName = "graphics.overworld.sprites2";
      public const string NameListName = "fieldeffectobjects";
      public const string GeneralPalettesTable = "graphics.overworld.palettes2";

      /// <summary>The palette tag that means "no palette": the effect is drawn with the palette of the object it belongs to.</summary>
      public const int NoTag = 0xFFFF;

      private const int TemplateLength = 24;
      private const int OamOffset = 4, ImagesOffset = 12;

      public static bool IsFieldEffectTable(string tableName) =>
         string.Equals(tableName, TableName, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(tableName, LegacyTableName, StringComparison.OrdinalIgnoreCase);

      /// <summary>The name the model knows the table by, or null if the game has no such table.</summary>
      public static string FindTableName(IDataModel model) {
         if (model == null) return null;
         foreach (var name in new[] { TableName, LegacyTableName }) {
            if (FindTable(model, name) != null) return name;
         }
         return null;
      }

      public static ITableRun FindTable(IDataModel model) {
         var name = FindTableName(model);
         return name == null ? null : FindTable(model, name);
      }

      public static bool IsSupported(IDataModel model) => FindTableName(model) != null;

      /// <summary>The table with this anchor, but only if it really starts there: the 'next run' of a missing anchor is not that table.</summary>
      private static ITableRun FindTable(IDataModel model, string anchor) {
         var address = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, anchor);
         if (address < 0) return null;
         var run = model.GetNextRun(address) as ITableRun;
         return run != null && run.Start == address ? run : null;
      }

      /// <summary>The name of an effect: the table's own names if it has any, otherwise the list of field effect names, otherwise empty.</summary>
      public static string NameOf(IDataModel model, int index) {
         var tableName = FindTableName(model);
         if (tableName == null || index < 0) return string.Empty;
         var names = model.GetOptions(tableName);
         if (names != null && index < names.Count && !string.IsNullOrEmpty(names[index]) && names[index] != index.ToString()) return names[index];
         if (model.TryGetList(NameListName, out var list) && index < list.Count && !string.IsNullOrEmpty(list[index])) return list[index];
         // an unedited Emerald has no list of names: the game's own names (FLDEFFOBJ_*) for its 37 effects
         if (Count(model) == VanillaEmeraldNames.Length && index < VanillaEmeraldNames.Length) return VanillaEmeraldNames[index];
         return string.Empty;
      }

      /// <summary>The names of the 37 field effect objects of pokeemerald (FLDEFFOBJ_SHADOW_S ...), in order.</summary>
      private static readonly string[] VanillaEmeraldNames = {
         "SHADOW_S", "SHADOW_M", "SHADOW_L", "SHADOW_XL", "TALL_GRASS", "RIPPLE", "ASH", "SURF_BLOB", "ARROW", "GROUND_IMPACT_DUST",
         "JUMP_TALL_GRASS", "SAND_FOOTPRINTS", "JUMP_BIG_SPLASH", "SPLASH", "JUMP_SMALL_SPLASH", "LONG_GRASS", "JUMP_LONG_GRASS", "UNUSED_GRASS", "UNUSED_GRASS_2", "UNUSED_SAND",
         "WATER_SURFACING", "REFLECTION_DISTORTION", "SPARKLE", "DEEP_SAND_FOOTPRINTS", "TREE_DISGUISE", "MOUNTAIN_DISGUISE", "BIRD", "BIKE_TIRE_TRACKS", "SAND_DISGUISE", "SAND_PILE",
         "SHORT_GRASS", "HOT_SPRINGS_WATER", "ASH_PUFF", "ASH_LAUNCH", "BUBBLES", "SMALL_SPARKLE", "RAYQUAZA",
      };

      public static int Count(IDataModel model) => FindTable(model)?.ElementCount ?? 0;

      /// <summary>The size in pixels of one frame of an effect, from its template's OAM data: (0, 0) if the game has no such effect or its template has no usable OAM data. Cheap: nothing is drawn.</summary>
      public static (int width, int height) FrameSize(IDataModel model, int index) {
         var table = FindTable(model);
         if (table == null || index < 0 || index >= table.ElementCount) return (0, 0);
         var template = TemplateAddress(model, table, index);
         if (template < 0 || template + TemplateLength > model.Count) return (0, 0);
         return ReadOamSize(model, template);
      }

      private static (int width, int height) ReadOamSize(IDataModel model, int template) {
         var oam = model.ReadPointer(template + OamOffset);
         if (oam < 0 || oam + 4 > model.Count) return (0, 0);
         return OverworldSpriteListRun.TryGetOamSize(model.ReadMultiByteValue(oam, 2), model.ReadMultiByteValue(oam + 2, 2), out var width, out var height) ? (width, height) : (0, 0);
      }

      /// <summary>Everything the picture and the info box need to know about one field effect. Null if the game has no such effect.</summary>
      public static FieldEffectInfo Describe(IDataModel model, int index) {
         var table = FindTable(model);
         if (table == null || index < 0 || index >= table.ElementCount) return null;
         var name = NameOf(model, index);
         var template = TemplateAddress(model, table, index);
         if (template < 0 || template + TemplateLength > model.Count) return new FieldEffectInfo { Index = index, Name = name };

         var tileTag = model.ReadMultiByteValue(template, 2);
         var paletteTag = model.ReadMultiByteValue(template + 2, 2);
         var (width, height) = ReadOamSize(model, template);

         OverworldSpriteListRun list = null;
         var images = model.ReadPointer(template + ImagesOffset);
         if (images >= 0 && images < model.Count && model.GetNextRun(images) is OverworldSpriteListRun found && found.Start == images) list = found;

         var frames = 0;
         if (list != null) {
            var pageWidth = list.SpriteFormat.TileWidth * 8;
            var stackHeight = list.SpriteFormat.TileHeight * 8;
            if (width == 0 || height == 0) (width, height) = (pageWidth, stackHeight);
            // several entries are several pictures; one entry can be a sheet with the frames stacked top to bottom
            frames = list.Pages > 1 ? list.Pages : height > 0 && stackHeight % height == 0 ? Math.Max(1, stackHeight / height) : 1;
         }

         FieldEffectPalette palette = null;
         if (list != null) {
            palette = FindPalette(model, paletteTag, list.PaletteHint);
            if (palette == null) palette = PreviewPalette(model, list, paletteTag);
         }

         return new FieldEffectInfo {
            Index = index,
            Name = name,
            Template = template,
            TileTag = tileTag,
            PaletteTag = paletteTag,
            Width = width,
            Height = height,
            FrameCount = frames,
            List = list,
            Palette = palette,
         };
      }

      private static int TemplateAddress(IDataModel model, ITableRun table, int index) {
         var row = table.Start + table.ElementLength * index;
         // the usual table is a list of pointers to templates; a table of the templates themselves works too
         if (table.ElementLength == 4 && table.ElementContent.Count > 0 && table.ElementContent[0].Type == ElementContentType.Pointer) return model.ReadPointer(row);
         return row;
      }

      #region palettes

      /// <summary>
      /// The palette the game loads for this tag: the first of the palette tables the list of frames names (its hint), then the usual ones, with an element whose id is the tag.
      /// Returns null for the 'no tag' tag and for a tag nobody lists.
      /// </summary>
      public static FieldEffectPalette FindPalette(IDataModel model, int tag, string hint = null) {
         if (tag < 0 || tag == NoTag) return null;
         foreach (var tableName in PaletteTableNames(model, hint)) {
            var table = FindTable(model, tableName);
            if (table == null) continue;
            var keyOffset = OffsetOf(table, segment => segment.Name == "id");
            var pointerOffset = OffsetOf(table, segment => segment is ArrayRunPointerSegment);
            if (keyOffset < 0 || pointerOffset < 0) continue;
            for (int i = 0; i < table.ElementCount; i++) {
               var row = table.Start + table.ElementLength * i;
               if (model.ReadMultiByteValue(row + keyOffset, 2) != tag) continue;
               var address = model.ReadPointer(row + pointerOffset);
               if (!(model.GetNextRun(address) is IPaletteRun run) || run.Start != address) continue;
               return new FieldEffectPalette {
                  Tag = tag,
                  TableName = tableName,
                  Index = i,
                  Address = address,
                  Colors = run.GetPalette(model, 0),
                  Title = TitleOf(tableName, tag),
               };
            }
         }
         return null;
      }

      /// <summary>An effect without a palette tag is drawn with whatever palette its object has. The pictures use the palette HexManiac picks for them: say which.</summary>
      private static FieldEffectPalette PreviewPalette(IDataModel model, OverworldSpriteListRun list, int tag) {
         var run = list.FindRelatedPalettes(model).FirstOrDefault();
         if (run == null) return null;
         foreach (var source in run.PointerSources ?? SortedSpan<int>.None) {
            if (!(model.GetNextRun(source) is ITableRun table)) continue;
            var tableName = model.GetAnchorFromAddress(-1, table.Start);
            if (string.IsNullOrEmpty(tableName)) continue;
            return new FieldEffectPalette {
               Tag = tag,
               TableName = tableName,
               Index = table.ConvertByteOffsetToArrayOffset(source).ElementIndex,
               Address = run.Start,
               Colors = run.GetPalette(model, 0),
               IsPreviewOnly = true,
               Title = TitleOf(tableName, tag),
            };
         }
         return null;
      }

      private static IEnumerable<string> PaletteTableNames(IDataModel model, string hint) {
         var seen = new HashSet<string>();
         if (!string.IsNullOrEmpty(hint)) {
            var tables = hint.Contains(':') ? hint.Substring(0, hint.IndexOf(':')) : hint;
            foreach (var name in tables.Split(',')) if (name.Length > 0 && seen.Add(name)) yield return name;
         }
         foreach (var name in new[] { GeneralPalettesTable, HardcodeTablesModel.OverworldPalettes }) if (seen.Add(name)) yield return name;
      }

      private static int OffsetOf(ITableRun table, Func<ArrayRunElementSegment, bool> choose) {
         var offset = 0;
         foreach (var segment in table.ElementContent) {
            if (choose(segment)) return offset;
            offset += segment.Length;
         }
         return -1;
      }

      private static string TitleOf(string tableName, int tag) {
         var tail = tableName.Substring(tableName.LastIndexOf('.') + 1);
         if (tableName == GeneralPalettesTable) return "general field effect palette";
         if (tableName == HardcodeTablesModel.OverworldPalettes) return "overworld object palette";
         return $"{tail} palette";
      }

      /// <summary>The sixteen colours as a strip of sixteen squares (scale is the size of a square).</summary>
      public static IPixelViewModel CreatePaletteStrip(IReadOnlyList<short> colors, double scale = 12) {
         var data = new short[16];
         for (int i = 0; i < data.Length; i++) data[i] = colors != null && i < colors.Count ? colors[i] : (short)0;
         return new ReadonlyPixelViewModel(16, 1, data) { SpriteScale = scale };
      }

      #endregion

      #region pictures

      /// <summary>
      /// Every frame of an effect, drawn with the palette the game would use (colour 0 see-through). A sheet with the frames stacked is cut into frames.
      /// An effect without pictures has no frames.
      /// </summary>
      public static IReadOnlyList<IPixelViewModel> RenderFrames(IDataModel model, int index, double scale = 1) {
         var info = Describe(model, index);
         return info == null ? new IPixelViewModel[0] : RenderFrames(model, info, scale);
      }

      public static IReadOnlyList<IPixelViewModel> RenderFrames(IDataModel model, FieldEffectInfo info, double scale = 1) {
         var frames = new List<IPixelViewModel>();
         var list = info?.List;
         if (list == null) return frames;
         // the palette the game uses (found by tag); if the effect has none, Describe already picked the one HexManiac would show it with
         var colors = info.Palette?.Colors;
         if (colors == null || colors.Count == 0) colors = TileViewModel.CreateDefaultPalette(16);
         if (colors.Count == 16) colors = SpriteTool.CreatePaletteWithUniqueTransparentColor(colors);
         const int blankPages = 0;
         var frameWidth = list.SpriteFormat.TileWidth * 8;
         var frameHeight = info.Height > 0 && info.Width == frameWidth ? info.Height : list.SpriteFormat.TileHeight * 8;
         for (int page = 0; page < list.Pages; page++) {
            var pixels = list.GetPixels(model, page, -1);
            if (pixels == null) continue;
            for (int top = 0; top + frameHeight <= pixels.GetLength(1) && frames.Count < 64; top += frameHeight) {
               var frame = top == 0 && frameHeight == pixels.GetLength(1) ? pixels : Cut(pixels, top, frameHeight);
               frames.Add(new ReadonlyPixelViewModel(frameWidth, frameHeight, SpriteTool.Render(frame, colors, blankPages, 0), colors[0]) { SpriteScale = scale });
            }
         }
         return frames;
      }

      private static int[,] Cut(int[,] pixels, int top, int height) {
         var width = pixels.GetLength(0);
         var result = new int[width, height];
         for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) result[x, y] = pixels[x, top + y];
         }
         return result;
      }

      /// <summary>The picture a button or a gallery shows for the effect: its first frame. Null if the effect has no picture.</summary>
      public static IPixelViewModel RenderFirstFrame(IDataModel model, int index) => RenderFrames(model, index).FirstOrDefault();

      #endregion
   }

   /// <summary>
   /// Draws the first frame of field effects for the galleries, one effect at a time and only when somebody asks for it.
   /// The pictures are remembered for as long as the model's data doesn't change.
   /// </summary>
   public class FieldEffectRenderer {
      private const string CacheKey = "field-effect-renderer";

      private readonly IDataModel model;
      private readonly Dictionary<int, (IPixelViewModel image, bool isPlaceholder)> rendered = new();

      private FieldEffectRenderer(IDataModel model) => this.model = model;

      public static FieldEffectRenderer Get(IDataModel model) => model.CurrentCacheScope.GetOrAdd(CacheKey, () => new FieldEffectRenderer(model));

      public int RenderedCount { get { lock (rendered) return rendered.Count; } }

      /// <summary>The first frame of an effect. An effect without pictures gets an empty square, flagged as a placeholder.</summary>
      public (IPixelViewModel image, bool isPlaceholder) Get(int index) {
         lock (rendered) {
            if (rendered.TryGetValue(index, out var existing)) return existing;
            var result = Render(index) ?? Placeholder;
            rendered[index] = result;
            return result;
         }
      }

      /// <summary>Draws an effect again (its picture or palette may have been edited). Returns null, and keeps the old picture, if it can't be drawn right now.</summary>
      public (IPixelViewModel image, bool isPlaceholder)? Redraw(int index) {
         lock (rendered) {
            var result = Render(index);
            if (result == null) return null;
            rendered[index] = result.Value;
            return result;
         }
      }

      private static (IPixelViewModel image, bool isPlaceholder) Placeholder => (new ReadonlyPixelViewModel(16, 16, new short[256], 0), true);

      private (IPixelViewModel image, bool isPlaceholder)? Render(int index) {
         try {
            var frame = FieldEffects.RenderFirstFrame(model, index);
            return frame == null ? null : (frame, false);
         } catch (Exception) {
            return null;
         }
      }
   }
}
