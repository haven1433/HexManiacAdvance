using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.HexManiac.Core.Models.PokemonAnimations {
   /// <summary>What the game reads, besides the pictures, to animate the front sprite of one species.</summary>
   public readonly struct PokemonAnimationInputs {
      /// <summary>Which movement the game runs (the ANIM_* id).</summary>
      public int AnimId { get; }

      /// <summary>Game frames the game waits before it starts the movement.</summary>
      public int Delay { get; }

      /// <summary>The address of the pair of pointers to the command lists, or a negative number if the species has none.</summary>
      public int FramesAddress { get; }

      public PokemonAnimationInputs(int animId, int delay, int framesAddress) => (AnimId, Delay, FramesAddress) = (animId, delay, framesAddress);
   }

   /// <summary>
   /// Where a ROM keeps what the front animation of a species needs: the picture sheet, the palette, the movement id, the delay and the frame commands.
   /// There are two layouts:
   ///
   ///   <see cref="SpeciesTableLayout"/>    pokeemerald-expansion (and CUBE): everything is a field of the pokemon table (frontAnimId, frontAnimDelay, frontAnimFrames, frontPic, palette)
   ///   <see cref="SeparateTablesLayout"/>  the unedited games (vanilla Emerald and its translations): every piece is a table of its own, with one row per species
   ///                                       (graphics.pokemon.sprites.front, graphics.pokemon.palettes.normal, graphics.pokemon.animations.front/.frames/.delay)
   ///
   /// <see cref="Find"/> tells which one a ROM has (or null: no preview). It only looks at the table lists, so it is cheap, but it belongs where the table tool
   /// builds a view or the preview builds a picture, never in a per-cell path.
   /// </summary>
   public abstract class PokemonAnimationLayout {
      /// <summary>How many pictures the animation uses: the sheet of a front sprite holds two.</summary>
      public const int PicturesPerSheet = 2;

      /// <summary>Which layout the ROM has, or null if its metadata does not describe the animation of a species.</summary>
      public static PokemonAnimationLayout Find(IDataModel model) {
         if (model == null) return null;
         return SpeciesTableLayout.TryCreate(model) ?? (PokemonAnimationLayout)SeparateTablesLayout.TryCreate(model);
      }

      /// <summary>Everything but the pictures. False if the species has no animation data in this ROM.</summary>
      public abstract bool TryReadInputs(IDataModel model, int species, out PokemonAnimationInputs inputs);

      /// <summary>The animation of the species, with its pictures and palette. Null if something is missing or not readable.</summary>
      public abstract PokemonAnimationSource Read(IDataModel model, int species, IReadOnlyList<short> hmaPaletteOverride);

      /// <summary>The address of the pointer to the species' front sprite sheet (the table tool's sprite view is found by it, and knows which palette is selected).</summary>
      public abstract int FrontPictureAddress(IDataModel model, int species);

      /// <summary>The address of the frame commands field of the species (what <see cref="PokemonAnimationInputs.FramesAddress"/> is read from), or -1.</summary>
      public abstract int FramesFieldAddress(IDataModel model, int species);

      #region shared helpers

      /// <summary>The address of a field of one table row, or -1 if the table has no such field.</summary>
      public static int FieldAddress(ITableRun table, int row, string field) {
         if (table == null || row < 0 || row >= table.ElementCount || field == null) return -1;
         int offset = 0;
         foreach (var segment in table.ElementContent) {
            if (segment.Name == field) return table.Start + table.ElementLength * row + offset;
            offset += segment.Length;
         }
         return -1;
      }

      /// <summary>
      /// The row of a table that holds a species. Tables that are one shorter than the species list (the movement ids and delays start with species 1)
      /// carry that in their length (data.pokemon.names-1): row 0 is species 1.
      /// </summary>
      public static int RowOfSpecies(ITableRun table, int species) {
         var margin = (table as ArrayRun)?.ParentOffset.BeginningMargin ?? 0;
         return species + margin;
      }

      /// <summary>The species a row of such a table belongs to (the opposite of <see cref="RowOfSpecies"/>).</summary>
      public static int SpeciesOfRow(ITableRun table, int row) {
         var margin = (table as ArrayRun)?.ParentOffset.BeginningMargin ?? 0;
         return row - margin;
      }

      /// <summary>The name of the first field of this kind, or null. A field with the preferred name wins, so a table with more than one such field is read at the right one.</summary>
      public static string FieldNamed(ITableRun table, string preferred, Func<ArrayRunElementSegment, bool> isKind) {
         if (table == null) return null;
         var named = table.ElementContent.FirstOrDefault(segment => segment.Name == preferred && isKind(segment));
         if (named != null) return named.Name;
         return table.ElementContent.FirstOrDefault(isKind)?.Name;
      }

      public static bool IsPointer(ArrayRunElementSegment segment) => segment is ArrayRunPointerSegment || segment.Type == ElementContentType.Pointer;

      public static bool IsNumber(ArrayRunElementSegment segment) => segment.Type == ElementContentType.Integer && segment.Length >= 1 && segment.Length <= 4 && !(segment is ArrayRunTupleSegment) && !(segment is ArrayRunCalculatedSegment);

      /// <summary>
      /// The pictures of a front sprite: two 64x64 pictures of palette indexes. They are in one sheet 64 wide and 128 tall (a sprite format with 8x16 tiles),
      /// or, when the format is only 8x8 tiles big (the unedited games), they are the first two pages of the sprite.
      /// </summary>
      public static byte[][] ReadPictures(IDataModel model, ITableRun table, int row, string pointerField) {
         var pointerAddress = FieldAddress(table, row, pointerField);
         if (pointerAddress < 0) return null;
         var run = FindSpriteRun(model, table, pointerField, pointerAddress);
         if (run == null) return null;

         var pictures = new List<byte[]>();
         int pages = Math.Max(1, Math.Min(run.Pages, 4));
         for (int page = 0; page < pages && pictures.Count < PicturesPerSheet; page++) {
            var pixels = run.GetPixels(model, page, row);
            if (pixels == null) break;
            var split = PokemonAnimationSource.SplitFrames(pixels);
            if (split == null) break;
            foreach (var picture in split) {
               if (pictures.Count < PicturesPerSheet) pictures.Add(picture);
            }
         }
         return pictures.Count == 0 ? null : pictures.ToArray();
      }

      private static ISpriteRun FindSpriteRun(IDataModel model, ITableRun table, string pointerField, int pointerAddress) {
         var destination = model.ReadPointer(pointerAddress);
         if (destination < 0 || destination >= model.Count) return null;
         if (model.GetNextRun(destination) is ISpriteRun existing && existing.Start == destination) return existing;
         // the sprite was not recognized by the model (the metadata said where it is, but nothing has formatted it yet): read it with the format of the pointer
         if (table.ElementContent.FirstOrDefault(segment => segment.Name == pointerField) is not ArrayRunPointerSegment pointerSegment) return null;
         IFormattedRun temporary = new NoInfoRun(destination, new SortedSpan<int>(pointerAddress));
         model.FormatRunFactory.GetStrategy(pointerSegment.InnerFormat).TryParseData(model, string.Empty, destination, ref temporary);
         return temporary as ISpriteRun;
      }

      /// <summary>The 16 colours (HexManiac's colour order) a pointer field of a table row leads to, or null.</summary>
      public static IReadOnlyList<short> ReadPalette(IDataModel model, ITableRun table, int row, string pointerField) {
         var pointerAddress = FieldAddress(table, row, pointerField);
         if (pointerAddress < 0) return null;
         var destination = model.ReadPointer(pointerAddress);
         if (destination < 0 || destination >= model.Count) return null;
         var run = model.GetNextRun(destination) as IPaletteRun;
         if (run == null || run.Start != destination) {
            if (table.ElementContent.FirstOrDefault(segment => segment.Name == pointerField) is not ArrayRunPointerSegment pointerSegment) return null;
            IFormattedRun temporary = new NoInfoRun(destination, new SortedSpan<int>(pointerAddress));
            model.FormatRunFactory.GetStrategy(pointerSegment.InnerFormat).TryParseData(model, string.Empty, destination, ref temporary);
            run = temporary as IPaletteRun;
         }
         return run?.GetPalette(model, 0);
      }

      /// <summary>An unsigned number field of a table row, or -1.</summary>
      public static int ReadNumber(IDataModel model, ITableRun table, int row, string field) {
         var address = FieldAddress(table, row, field);
         if (address < 0) return -1;
         var segment = table.ElementContent.First(s => s.Name == field);
         if (address + segment.Length > model.Count) return -1;
         return model.ReadMultiByteValue(address, segment.Length);
      }

      #endregion
   }

   /// <summary>The animation is described by fields of the pokemon table (pokeemerald-expansion, and CUBE).</summary>
   public sealed class SpeciesTableLayout : PokemonAnimationLayout {
      public const string AnimIdField = "frontAnimId", DelayField = "frontAnimDelay", FramesField = "frontAnimFrames", PictureField = "frontPic", PaletteField = "palette";

      private readonly ITableRun table;

      private SpeciesTableLayout(ITableRun table) => this.table = table;

      public static SpeciesTableLayout TryCreate(IDataModel model) {
         var run = model.GetTable(HardcodeTablesModel.PokemonStatsTable);
         if (run == null) return null;
         var names = run.ElementContent;
         foreach (var field in new[] { AnimIdField, DelayField, FramesField, PictureField, PaletteField }) {
            if (!names.Any(segment => segment.Name == field)) return null;
         }
         return new SpeciesTableLayout(run);
      }

      public override bool TryReadInputs(IDataModel model, int species, out PokemonAnimationInputs inputs) {
         inputs = default;
         if (species < 0 || species >= table.ElementCount) return false;
         var element = new ModelTable(model, table)[species];
         inputs = new PokemonAnimationInputs(element.GetValue(AnimIdField), element.GetValue(DelayField), element.GetAddress(FramesField));
         return true;
      }

      public override PokemonAnimationSource Read(IDataModel model, int species, IReadOnlyList<short> hmaPaletteOverride) {
         if (species < 0 || species >= table.ElementCount) return null;
         var element = new ModelTable(model, table)[species];

         var pixels = element.GetSprite(PictureField);
         if (pixels == null) return null;
         var images = PokemonAnimationSource.SplitFrames(pixels);
         if (images == null) return null;

         var palette = hmaPaletteOverride ?? element.GetPalette(PaletteField);
         if (palette == null || palette.Count < 16) return null;

         var source = new PokemonAnimationSource {
            Images = images,
            Palette = PokemonAnimationSource.ToGbaPalette(palette),
            AnimId = element.GetValue(AnimIdField),
            Delay = element.GetValue(DelayField),
         };
         source.Frames = PokemonAnimationSource.ReadFrameCommands(model, element.GetAddress(FramesField));
         return source;
      }

      public override int FrontPictureAddress(IDataModel model, int species) => FieldAddress(table, species, PictureField);

      public override int FramesFieldAddress(IDataModel model, int species) => FieldAddress(table, species, FramesField);
   }

   /// <summary>
   /// The animation is described by tables of its own, one row per species: the unedited games. HexManiac's default metadata of Pokemon Emerald names them
   /// graphics.pokemon.sprites.front (sprite sheet), graphics.pokemon.palettes.normal (palette), graphics.pokemon.animations.front (movement id, starts at species 1),
   /// graphics.pokemon.animations.frames (frame commands) and graphics.pokemon.animations.delay (starts at species 1; optional, no delay without it).
   /// </summary>
   public sealed class SeparateTablesLayout : PokemonAnimationLayout {
      public const string DelayTableName = "graphics.pokemon.animations.delay";
      public const string AnimIdTableName = "graphics.pokemon.animations.front";
      public const string FramesTableName = "graphics.pokemon.animations.frames";

      /// <summary>
      /// The species whose sprite the Emerald games never start the second picture of (HasTwoFramesAnimation): Unown shows letters and Spinda spots,
      /// Castform and Deoxys use their sheets for their forms. Their frame lists are not what the game plays.
      /// The numbers are the species numbers of the games (include/constants/species.h of pokeemerald): the code of the game compares with these constants.
      /// </summary>
      public static readonly IReadOnlyList<int> SingleFrameSpeciesOfEmerald = new[] { 201 /* Unown */, 308 /* Spinda */, 385 /* Castform */, 410 /* Deoxys */ };

      private readonly ITableRun pictures, palettes, ids, frames, delays;
      private readonly string pictureField, paletteField, idField, framesField, delayField;
      private readonly bool singleFrameSpecies;

      private SeparateTablesLayout(IDataModel model, ITableRun pictures, ITableRun palettes, ITableRun ids, ITableRun frames, ITableRun delays) {
         this.pictures = pictures; this.palettes = palettes; this.ids = ids; this.frames = frames; this.delays = delays;
         pictureField = FieldNamed(pictures, "sprite", IsPointer);
         paletteField = FieldNamed(palettes, "palette", IsPointer);
         idField = FieldNamed(ids, "index", IsNumber);
         framesField = FieldNamed(frames, "data", IsPointer);
         delayField = FieldNamed(delays, "delay", IsNumber);
         // the Emerald games (the codes begin with BPE) decide in code which species keep to their first picture
         singleFrameSpecies = (model.GetGameCode() ?? string.Empty).StartsWith("BPE", StringComparison.Ordinal);
      }

      public static SeparateTablesLayout TryCreate(IDataModel model) {
         var pictures = model.GetTable(HardcodeTablesModel.FrontSpritesTable);
         var palettes = model.GetTable(HardcodeTablesModel.PokePalettesTable);
         var ids = model.GetTable(AnimIdTableName);
         var frames = model.GetTable(FramesTableName);
         if (pictures == null || palettes == null || ids == null || frames == null) return null;
         var delays = model.GetTable(DelayTableName);
         var layout = new SeparateTablesLayout(model, pictures, palettes, ids, frames, delays);
         if (layout.pictureField == null || layout.paletteField == null || layout.idField == null || layout.framesField == null) return null;
         if (delays != null && layout.delayField == null) layout = new SeparateTablesLayout(model, pictures, palettes, ids, frames, null);
         return layout;
      }

      /// <summary>The name of the field of the front sprite table that leads to the sheet. The preview is shown under it.</summary>
      public string PictureFieldName => pictureField;

      /// <summary>True for the front sprite table of this layout: the table whose rows the preview is attached to.</summary>
      public bool IsPictureTable(ITableRun table) => ReferenceEquals(table, pictures) || (table != null && pictures != null && table.Start == pictures.Start);

      public override bool TryReadInputs(IDataModel model, int species, out PokemonAnimationInputs inputs) {
         inputs = default;
         var idRow = RowOfSpecies(ids, species);
         var framesRow = RowOfSpecies(frames, species);
         if (idRow < 0 || idRow >= ids.ElementCount || framesRow < 0 || framesRow >= frames.ElementCount) return false;
         int delay = 0;
         var delayRow = delays == null ? -1 : RowOfSpecies(delays, species);
         if (delayRow >= 0 && delayRow < delays.ElementCount) delay = Math.Max(0, ReadNumber(model, delays, delayRow, delayField));

         var framesAddress = FieldAddress(frames, framesRow, framesField);
         inputs = new PokemonAnimationInputs(ReadNumber(model, ids, idRow, idField), delay, framesAddress < 0 ? -1 : model.ReadPointer(framesAddress));
         return true;
      }

      public override PokemonAnimationSource Read(IDataModel model, int species, IReadOnlyList<short> hmaPaletteOverride) {
         if (!TryReadInputs(model, species, out var inputs)) return null;
         var pictureRow = RowOfSpecies(pictures, species);
         var paletteRow = RowOfSpecies(palettes, species);
         if (pictureRow < 0 || pictureRow >= pictures.ElementCount || paletteRow < 0 || paletteRow >= palettes.ElementCount) return null;

         var images = ReadPictures(model, pictures, pictureRow, pictureField);
         if (images == null) return null;
         var palette = hmaPaletteOverride ?? ReadPalette(model, palettes, paletteRow, paletteField);
         if (palette == null || palette.Count < 16) return null;

         var source = new PokemonAnimationSource {
            Images = images,
            Palette = PokemonAnimationSource.ToGbaPalette(palette),
            AnimId = inputs.AnimId,
            Delay = inputs.Delay,
         };
         // the game starts the second picture only for some species: for the others the movement runs on the first picture
         source.Frames = StartsSecondPicture(species) ? PokemonAnimationSource.ReadFrameCommands(model, inputs.FramesAddress) : null;
         return source;
      }

      /// <summary>False for the species the game never shows the second picture of.</summary>
      public bool StartsSecondPicture(int species) => !(singleFrameSpecies && SingleFrameSpeciesOfEmerald.Contains(species));

      public override int FrontPictureAddress(IDataModel model, int species) => FieldAddress(pictures, RowOfSpecies(pictures, species), pictureField);

      public override int FramesFieldAddress(IDataModel model, int species) => FieldAddress(frames, RowOfSpecies(frames, species), framesField);
   }
}
