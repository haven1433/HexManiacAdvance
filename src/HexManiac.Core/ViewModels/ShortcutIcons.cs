using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Map;
using HavenSoft.HexManiac.Core.Models.PokemonAnimations;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Visitors;
using HexManiac.Core.Models.Runs.Sprites;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace HavenSoft.HexManiac.Core.ViewModels {
   /// <summary>
   /// The pictures on the big buttons of the Goto dialog (the main menu): which sprite each one shows and where it is found in the ROM.
   ///
   ///   Pokemon              the front picture of Cradily (found by name in the species names; the button keeps the ROM metadata's picture if there is no Cradily),
   ///                        and the place where its battle animation plugs in (<see cref="CreatePokemonButtonAnimation"/>)
   ///   Items                a Poke Ball (found by name in the items table, with the icon the item table points to)
   ///   Sound                the Poke Flute (the item is found by its name in the items table; its picture is the 'icon' of the items table or, in the unedited games, the item picture table)
   ///   OW Sprites           the first picture of overworld sprite 235 (the RS Brendan of CUBE and Emerald); sprite 0 (the player) when the table has no sprite 235 or it has no picture
   ///   Intro Manager        Professor Birch's bag, found through the code of the starter-choosing screen
   ///   Anim Tiles           the General tileset's flower, animated with the frames and the speed of the game's own animation table
   ///   Character Customization   Brendan's trainer front picture with a dark skin tone and the blue clothes row
   ///
   /// Every picture is found through the ROM's metadata, never through addresses, so it follows the data when tables move. Every function returns null when the ROM
   /// doesn't have the pieces (and never throws), and the caller then keeps the picture the button had before. Pictures are only drawn when a button is first shown.
   /// A button that ends up with no picture at all (an unedited game has none for Sound or OW Sprites) gets a built-in sprite from <see cref="ShortcutFallbackIcons"/>.
   /// </summary>
   public static class ShortcutIcons {
      /// <summary>The species the Pokemon button shows. The user asked for it by name.</summary>
      public const string PokemonButtonSpecies = "Cradily";

      /// <summary>The pause between two plays of the Pokemon button's battle animation (the user asked for 3 seconds).</summary>
      public const int PokemonButtonIdleMilliseconds = 3000;

      /// <summary>Pokemon front pictures are 64x64.</summary>
      public const int PokemonButtonCanvasSize = 64;

      private static readonly string[] DarkSkinNames = { "Dark Brown", "Dark", "Ebony", "Brown" };
      private const string BlueClothesName = "Blue";

      /// <summary>How long the GBA takes to draw one game frame: the animations of the tilesets count in these.</summary>
      public const double GameFramesPerSecond = 59.7275;

      #region Which button gets which picture

      /// <summary>
      /// Called for every shortcut the ROM's metadata lists. The Pokemon and the Items buttons get their new pictures here; every other button is left alone.
      /// The buttons are recognized by the place they go to (the pokemon table, the items table): that is what the metadata of every game and hack has in common.
      /// </summary>
      public static void CustomizeMenuButton(IDataModel model, GotoShortcutViewModel shortcut, string gotoAnchor, string imageAnchor) {
         if (model == null || shortcut == null || string.IsNullOrEmpty(gotoAnchor)) return;
         try {
            if (gotoAnchor.StartsWith(HardcodeTablesModel.PokemonStatsTable, StringComparison.OrdinalIgnoreCase)) {
               CustomizePokemonButton(model, shortcut, imageAnchor);
            } else if (string.Equals(gotoAnchor.Trim(), HardcodeTablesModel.ItemsTableName, StringComparison.OrdinalIgnoreCase)) {
               shortcut.PreferImage(() => PokeBall(model));
            }
         } catch (Exception) {
            // decoration: the button keeps the picture the metadata gave it
         }
      }

      private static void CustomizePokemonButton(IDataModel model, GotoShortcutViewModel shortcut, string imageAnchor) {
         // the picture of the button is the front picture of a species: the metadata's image anchor names the species by number ('data.pokemon.stats/295/frontPic/')
         if (imageAnchor == null || !imageAnchor.StartsWith(HardcodeTablesModel.PokemonStatsTable + "/", StringComparison.OrdinalIgnoreCase) || !imageAnchor.EndsWith("/")) return;
         // looking the species up reads the whole name table, so it waits until the picture (or the animation) is really needed, and is done once for both
         var species = new Lazy<int>(() => FindSpecies(model, PokemonButtonSpecies));
         shortcut.PreferImage(() => species.Value < 0 ? null : SpeciesFront(model, imageAnchor, species.Value));
         shortcut.SetAnimationSource(() => species.Value < 0 ? null : CreatePokemonButtonAnimation(model, species.Value));
      }

      #endregion

      #region THE HOOK FOR THE POKEMON ANIMATION

      // ======================================================================================================================================================
      // INTEGRATOR: this is the one place the in-game Pokemon animation plugs into the Pokemon button of the Goto dialog.
      // Return the animation of the species (its battle animation, then a pause of 'PokemonButtonIdleMilliseconds' before it plays again) or null for a still picture.
      // It is called at most once per button, and only when the dialog is first shown, never while the dialog is prepared.
      // ======================================================================================================================================================
      public static ShortcutAnimation CreatePokemonButtonAnimation(IDataModel model, int speciesIndex) {
         // the in-game front animation of the species (the same engine as the Pokemon editor's preview), then a still picture held for the idle time
         try {
            if (!PokemonAnimationTimeline.TryBuild(model, speciesIndex, PokemonButtonCanvasSize, PokemonButtonIdleMilliseconds, out var timeline)) return null;
            return ShortcutAnimation.Create(timeline.Frames, timeline.DurationsMs);
         } catch (Exception) {
            return null; // icons are decoration: the button keeps its still picture
         }
      }

      #endregion

      #region Pokemon

      /// <summary>The number of the species with this name (the names are compared without caring about capital letters), or -1.</summary>
      public static int FindSpecies(IDataModel model, string name) {
         try {
            var names = model.GetOptions(HardcodeTablesModel.GetSpeciesNameTable(model));
            if (names == null) return -1;
            for (int i = 0; i < names.Count; i++) {
               if (string.Equals(names[i]?.Trim('"', ' '), name, StringComparison.OrdinalIgnoreCase)) return i;
            }
         } catch (Exception) { }
         return -1;
      }

      /// <summary>The front picture of a species, found the way the metadata's own button finds its picture: the same anchor with the species' number swapped in.</summary>
      public static IPixelViewModel SpeciesFront(IDataModel model, string imageAnchorOfAnySpecies, int species) {
         try {
            var parts = imageAnchorOfAnySpecies.Split('/');
            if (parts.Length < 3) return null;
            parts[1] = species.ToString();
            return FirstFrame(BuildPicture(model, string.Join("/", parts)));
         } catch (Exception) {
            return null;
         }
      }

      /// <summary>
      /// A front picture holds two poses, one above the other (64x128): the button shows the top one, as the pokemon looks standing still.
      /// A picture that isn't twice as tall as it is wide is returned as it is.
      /// </summary>
      public static IPixelViewModel FirstFrame(IPixelViewModel picture) {
         if (picture == null) return null;
         int width = picture.PixelWidth, height = picture.PixelHeight;
         var data = picture.PixelData;
         if (width <= 0 || height < width * 2 || data == null || data.Length < width * height) return picture;
         var top = new short[width * width];
         Array.Copy(data, top, top.Length);
         return new ReadonlyPixelViewModel(width, width, top, picture.Transparent);
      }

      /// <summary>The picture an image anchor that ends in a slash ('table/3/frontPic/') leads to, drawn with its palette.</summary>
      public static IPixelViewModel BuildPicture(IDataModel model, string imageAnchor) {
         var spriteAddress = model.GetAddressFromAnchor(new ModelDelta(), -1, imageAnchor);
         if (spriteAddress < 0 || spriteAddress >= model.Count) return null;
         var run = model.GetNextRun(spriteAddress) as BaseRun;
         if (run == null || run.Start != spriteAddress) return null;
         var source = model.GetAddressFromAnchor(new ModelDelta(), -1, imageAnchor.Substring(0, imageAnchor.Length - 1));
         return ToolTipContentVisitor.BuildContentForRun(model, source, spriteAddress, run) as IPixelViewModel;
      }

      #endregion

      #region Items

      /// <summary>The icon of the Poke Ball from the items table (null if the ROM has no item with that name).</summary>
      public static IPixelViewModel PokeBall(IDataModel model) {
         // the game's name is POKe BALL with an accented e: compare the letters only
         return ItemIcon(model, new[] { "POKEBALL" }, exact: true) ?? ItemIcon(model, new[] { "POKEBALL" }, exact: false);
      }

      /// <summary>The icon of the first item whose name equals (or, not exact, contains) one of these names. Names are compared by their letters and digits only, without accents or capitals.</summary>
      public static IPixelViewModel ItemIcon(IDataModel model, IReadOnlyList<string> normalizedNames, bool exact) {
         try {
            var items = model.GetTable(HardcodeTablesModel.ItemsTableName);
            if (items == null) return null;
            var iconSegment = items.ElementContent.FirstOrDefault(segment => segment.Name == "icon" && segment is ArrayRunPointerSegment);
            if (iconSegment == null) return null;
            var names = model.GetOptions(HardcodeTablesModel.ItemsTableName);
            if (names == null) return null;
            var offset = items.ElementContent.Until(segment => segment == iconSegment).Sum(segment => segment.Length);
            foreach (var wanted in normalizedNames) {
               for (int i = 0; i < names.Count && i < items.ElementCount; i++) {
                  var name = NormalizeName(names[i]);
                  if (exact ? name != wanted : !name.Contains(wanted, StringComparison.Ordinal)) continue;
                  var iconAddress = model.ReadPointer(items.Start + items.ElementLength * i + offset);
                  if (iconAddress >= 0 && iconAddress < model.Count && model.GetNextRun(iconAddress) is ISpriteRun spriteRun && spriteRun.Start == iconAddress) {
                     return SpriteDecorator.BuildSprite(model, spriteRun, useTransparency: true);
                  }
               }
            }
         } catch (Exception) { }
         return null;
      }

      /// <summary>Upper case letters and digits only: 'POKe BALL' with an accent and 'Poke Ball' both become POKEBALL.</summary>
      public static string NormalizeName(string name) {
         if (string.IsNullOrEmpty(name)) return string.Empty;
         var result = new StringBuilder(name.Length);
         foreach (var c in name.Normalize(NormalizationForm.FormD)) {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) result.Append(char.ToUpperInvariant(c));
         }
         return result.ToString();
      }

      #endregion

      #region Professor Birch's bag (the Intro Manager button)

      /// <summary>
      /// Professor Birch's bag from the starter-choosing screen, cut out of the screen and halved in size (see <see cref="BirchBag"/>): found through the game's code, so it works on Emerald, CUBE and relocated data.
      /// Null if the ROM has no such screen (FireRed, Ruby, a changed screen); the button then shows the built-in bag.
      /// </summary>
      public static IPixelViewModel BirchBag(IDataModel model) {
         try {
            var pixels = HavenSoft.HexManiac.Core.Models.BirchBag.Draw(model.RawData, ShortcutFallbackIcons.Transparent, out var width, out var height);
            return pixels == null ? null : new ReadonlyPixelViewModel(width, height, pixels, ShortcutFallbackIcons.Transparent);
         } catch (Exception) {
            return null;
         }
      }

      #endregion

      #region The Poke Flute (the Sound button)

      /// <summary>The number of ITEM_POKE_FLUTE in Emerald, FireRed and LeafGreen (the unedited games have it at the same place, in every language).</summary>
      public const int OriginalPokeFluteItem = 350;

      /// <summary>True for the name of the Poke Flute in any of the languages of the games (letters only, see <see cref="NormalizeName"/>): 'POKE FLUTE', 'FLUTE POKE', 'POKEFLOTE', 'FLAUTA POKE', 'POKEFLAUTO'.</summary>
      public static bool IsPokeFluteName(string name) {
         var letters = NormalizeName(name);
         if (!letters.Contains("POKE", StringComparison.Ordinal)) return false;
         return letters.Contains("FLUT", StringComparison.Ordinal) || letters.Contains("FLAUT", StringComparison.Ordinal) || letters.Contains("FLOT", StringComparison.Ordinal);
      }

      /// <summary>
      /// The number of the Poke Flute in the items table: the first item with the name of the Poke Flute. Only if the table has no names at all (or none for item 350) and the game is an unedited-looking
      /// Emerald, FireRed or LeafGreen, the original number 350 is used. -1 if the ROM has no Poke Flute.
      /// </summary>
      public static int FindPokeFluteItem(IDataModel model) {
         try {
            var items = model.GetTable(HardcodeTablesModel.ItemsTableName);
            if (items == null) return -1;
            var names = model.GetOptions(HardcodeTablesModel.ItemsTableName);
            for (int i = 0; names != null && i < names.Count && i < items.ElementCount; i++) {
               if (IsPokeFluteName(names[i])) return i;
            }
            var gameCode = model.GetGameCode() ?? string.Empty;
            var original = gameCode.StartsWith("BPE", StringComparison.Ordinal) || gameCode.StartsWith("BPR", StringComparison.Ordinal) || gameCode.StartsWith("BPG", StringComparison.Ordinal);
            // an item without a name is listed by the program as '~351' (its number): no letter in the name means there is no name
            var nameOfOriginal = names != null && OriginalPokeFluteItem < names.Count ? NormalizeName(names[OriginalPokeFluteItem]) : string.Empty;
            var hasName = nameOfOriginal.Any(char.IsLetter);
            return original && !hasName && OriginalPokeFluteItem < items.ElementCount ? OriginalPokeFluteItem : -1;
         } catch (Exception) {
            return -1;
         }
      }

      /// <summary>
      /// The Poke Flute's item picture. Null if the ROM has no such item or no picture for it. The picture is the one the items table points to (pokeemerald-expansion, CUBE: the 'icon' field)
      /// or, in the unedited games, the entry of the item picture table (graphics.items.sprites, one entry per item) with the same number.
      /// </summary>
      public static IPixelViewModel PokeFlute(IDataModel model) {
         try {
            var item = FindPokeFluteItem(model);
            return item < 0 ? null : ItemIconByNumber(model, item);
         } catch (Exception) {
            return null;
         }
      }

      /// <summary>The picture of the item with this number: the 'icon' field of the items table if it has one, otherwise the same entry of the item picture table.</summary>
      public static IPixelViewModel ItemIconByNumber(IDataModel model, int item) {
         try {
            var items = model.GetTable(HardcodeTablesModel.ItemsTableName);
            if (items == null || item < 0 || item >= items.ElementCount) return null;
            var iconSegment = items.ElementContent.FirstOrDefault(segment => segment.Name == "icon" && segment is ArrayRunPointerSegment);
            if (iconSegment != null) {
               var offset = items.ElementContent.Until(segment => segment == iconSegment).Sum(segment => segment.Length);
               var iconAddress = model.ReadPointer(items.Start + items.ElementLength * item + offset);
               if (iconAddress >= 0 && iconAddress < model.Count && model.GetNextRun(iconAddress) is ISpriteRun spriteRun && spriteRun.Start == iconAddress) {
                  return SpriteDecorator.BuildSprite(model, spriteRun, useTransparency: true);
               }
            }
            var pictures = model.GetTable(HardcodeTablesModel.ItemImagesTableName);
            if (pictures != null && item < pictures.ElementCount) return BuildPicture(model, $"{HardcodeTablesModel.ItemImagesTableName}/{item}/sprite/");
         } catch (Exception) { }
         return null;
      }

      #endregion

      #region The first overworld sprite (the OW Sprites button)

      /// <summary>
      /// The first picture of sprite 0 of the overworld sprite table (graphics.overworld.sprites), drawn with its own palette and with a transparent background: the player standing and facing down
      /// (Brendan in Ruby, Sapphire, Emerald and CUBE, Red in FireRed and LeafGreen). It is the picture the sprite gallery and the map editor show for the sprite, so the table is read the same way for every game.
      /// Null if the ROM has no such table or the sprite has no picture or palette.
      /// </summary>
      public static IPixelViewModel FirstOverworldSprite(IDataModel model) {
         try {
            var table = model.GetTable(HardcodeTablesModel.OverworldSprites);
            if (table == null || table.ElementCount < 1) return null;
            var owTable = new ModelTable(model, table.Start);
            IPixelViewModel picture = null;
            if (table.ElementCount > ButtonSpriteIndex) {
               try { picture = HavenSoft.HexManiac.Core.ViewModels.Map.ObjectEventViewModel.Render(model, owTable, null, ButtonSpriteIndex, 0, () => -1); } catch (Exception) { picture = null; }
            }
            return picture ?? HavenSoft.HexManiac.Core.ViewModels.Map.ObjectEventViewModel.Render(model, owTable, null, 0, 0, () => -1);
         } catch (Exception) {
            return null;
         }
      }

      /// <summary>The overworld sprite the OW Sprites button shows: 235, the Ruby/Sapphire Brendan of CUBE and Emerald. A table with fewer sprites, or a sprite without a picture, falls back to sprite 0.</summary>
      public const int ButtonSpriteIndex = 235;

      #endregion

      #region The player (Brendan)

      /// <summary>Brendan standing in the overworld, facing down: the entry BRENDAN_NORMAL of the overworld sprite table, or if the ROM calls it something else, the first entry with BRENDAN in its name.</summary>
      public static IPixelViewModel BrendanStanding(IDataModel model) {
         try {
            var icon = CharacterSprites.Load(model, CharacterRole.Male).DrawOverworld(0, null, null, null);
            if (icon != null) return icon;
            var names = model.GetOptions(HardcodeTablesModel.OverworldSprites);
            if (names == null) return null;
            var candidates = names.Where(name => name != null && name.Contains("BRENDAN", StringComparison.OrdinalIgnoreCase)).ToList();
            var chosen = candidates.FirstOrDefault(name => name.Contains("NORMAL", StringComparison.OrdinalIgnoreCase)) ?? candidates.FirstOrDefault();
            return chosen == null ? null : CharacterSprites.Load(model, CharacterRole.Male, chosen).DrawOverworld(0, null, null, null);
         } catch (Exception) {
            return null;
         }
      }

      /// <summary>
      /// Brendan's trainer front picture with a dark skin tone and the clothes in blue: the colours are the 'Dark Brown' row of the skin tone table and the 'Blue' row of the clothes table,
      /// painted on the picture the way the game paints them. If the tables don't have rows with those names, fixed colours with the same look are used instead.
      /// Null if the ROM has no customization tables, no roles for the front picture, or no front picture.
      /// </summary>
      public static IPixelViewModel CharacterFront(IDataModel model) {
         try {
            if (!CharacterCustomization.IsSupported(model)) return null;
            var sprites = CharacterSprites.Load(model, CharacterRole.Male);
            if (!sprites.HasFront) return null;
            var customization = new CharacterCustomization(model, () => new NoDataChangeDeltaModel());
            var role = customization.FindRole(CharacterRole.Male, CharacterRole.TrainerPicContext);
            if (role == null) return null;
            var skin = FindRow(customization.SkinTones.ReadRows(), DarkSkinNames) ?? FixedDarkSkin;
            var clothes = FindRow(customization.Clothes.ReadRows(), BlueClothesName) ?? FixedBlueClothes;
            return sprites.DrawFront(role, skin, clothes);
         } catch (Exception) {
            return null;
         }
      }

      // used when the ROM's tables have no row of that name (the index is above 0 because row 0 means 'leave the picture as it is')
      private static readonly CharacterColorRow FixedDarkSkin = new(1, "Dark", new[] { GbaColor.Pack(15, 9, 5), GbaColor.Pack(11, 6, 3), GbaColor.Pack(19, 12, 8) });
      private static readonly CharacterColorRow FixedBlueClothes = new(1, "Blue", new[] { GbaColor.Pack(6, 14, 30), GbaColor.Pack(3, 8, 20), GbaColor.Pack(6, 14, 30) });

      /// <summary>The first row (never row 0, the original look) with one of the names, tried in the order of the names.</summary>
      public static CharacterColorRow FindRow(IReadOnlyList<CharacterColorRow> rows, params string[] names) {
         if (rows == null) return null;
         foreach (var name in names) {
            var row = rows.FirstOrDefault(r => r.Index != 0 && string.Equals(r.Name?.Trim(), name, StringComparison.OrdinalIgnoreCase));
            if (row != null) return row;
         }
         return null;
      }

      #endregion

      #region The flower

      /// <summary>
      /// The General tileset's flower, animated: the metatile whose top layer is the animated tiles, drawn once per frame of the game's own animation table
      /// (tileset animation 'General: Flower' in data.maps.tilesets.builtinanimations), in the table's frame order and at its speed.
      /// Null if the ROM has no such table, no such animation, or no metatile that uses the animated tiles.
      /// </summary>
      public static ShortcutAnimation FlowerAnimation(IDataModel model) {
         try {
            if (!TilesetAnimationConstants.TryRead(model, out var constants)) return null;
            var animations = new TilesetAnimations(model, null, null);
            if (!animations.HasBuiltInTable) return null;
            if (!TryFindFlower(model, animations, constants, out var tilesetStart, out var entry)) return null;

            var blockset = new BlocksetModel(model, tilesetStart);
            if (blockset.IsSecondary) return null;
            var tiles = ReadTilesWithoutChangingTheModel(model, blockset, tilesetStart);
            var palettes = blockset.ReadPalettes();
            var blocks = blockset.ReadBlocks(blockset.PrimaryBlocks);
            var attributes = blockset.ReadBlockAttributes(blockset.PrimaryBlocks);
            if (tiles == null || palettes == null || blocks == null || tiles.Length < entry.FirstTile + 4) return null;
            var block = FindBlockUsingTiles(blocks, entry.FirstTile);
            if (block < 0) return null;

            var rendered = new Dictionary<int, IPixelViewModel>(); // frames that point at the same graphics (the flower shows its first frame twice) are drawn once
            var frames = new List<IPixelViewModel>();
            var durations = new List<int>();
            var milliseconds = Math.Max(1, (int)Math.Round(entry.FrameDelay * 1000.0 / GameFramesPerSecond));
            for (int frame = 0; frame < entry.FrameCount; frame++) {
               var address = animations.FrameAddress(entry, frame);
               if (address < 0) return null;
               if (!rendered.TryGetValue(address, out var picture)) {
                  var framePixels = animations.ReadFramePixels(entry, frame, 1); // one tile wide: the tiles of the frame one above the other
                  var frameTiles = (int[][,])tiles.Clone();
                  for (int tile = 0; tile < entry.TileCount && entry.FirstTile + tile < frameTiles.Length; tile++) frameTiles[entry.FirstTile + tile] = CutTile(framePixels, tile);
                  picture = BlocksetModel.RenderBlock(block, blocks, attributes, frameTiles, palettes);
                  rendered[address] = picture;
               }
               frames.Add(picture);
               durations.Add(milliseconds);
            }
            return ShortcutAnimation.Create(frames, durations);
         } catch (Exception) {
            return null;
         }
      }

      private static bool TryFindFlower(IDataModel model, TilesetAnimations animations, TilesetAnimationConstants constants, out int tilesetStart, out TilesetAnimationEntry entry) {
         (tilesetStart, entry) = (-1, null);
         var table = model.GetTable(TilesetAnimations.BuiltInTableName);
         if (table == null) return false;
         var tried = new HashSet<int>();
         for (int i = 0; i < table.ElementCount; i++) {
            var tileset = model.ReadPointer(table.Start + table.ElementLength * i);
            if (tileset < 0 || tileset >= model.Count || !tried.Add(tileset)) continue;
            var isSecondary = model[tileset + 1] == 1;
            entry = animations.ReadBuiltInEntries(tileset, isSecondary, constants).FirstOrDefault(e => !isSecondary && e.Name != null && e.Name.Contains("flower", StringComparison.OrdinalIgnoreCase));
            if (entry == null) continue;
            tilesetStart = tileset;
            return true;
         }
         entry = null;
         return false;
      }

      /// <summary>
      /// The tiles of a (compressed or plain) tileset. Same as BlocksetModel.ReadTiles, which also quietly changes how the ROM's data is formatted when it finds an unformatted tileset:
      /// drawing a button must not change the file, so that part is left out.
      /// </summary>
      private static int[][,] ReadTilesWithoutChangingTheModel(IDataModel model, BlocksetModel blockset, int tilesetStart) {
         if (!blockset.IsCompressed) return blockset.ReadTiles();
         var start = model.ReadPointer(tilesetStart + 4);
         if (start < 0 || start >= model.Count) return null;
         var run = new LzTilesetRun(new TilesetFormat(4, null), model, start);
         var all = run.GetPixels(model, 0, 1);
         if (all == null) return null;
         var tiles = new int[all.GetLength(1) / 8][,];
         for (int i = 0; i < tiles.Length; i++) tiles[i] = CutTile(all, i);
         return tiles;
      }

      private static int[,] CutTile(int[,] column, int tile) {
         var result = new int[8, 8];
         if (column == null || column.GetLength(0) < 8 || column.GetLength(1) < tile * 8 + 8) return result;
         for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++) result[x, y] = column[x, y + tile * 8];
         return result;
      }

      /// <summary>
      /// The first metatile with a layer made of the animated tiles in their natural order (top left, top right, bottom left, bottom right).
      /// A metatile is 8 tile entries of 2 bytes: 4 for the bottom layer, then 4 for the top layer. The low 10 bits of an entry are the tile number.
      /// </summary>
      public static int FindBlockUsingTiles(byte[][] blocks, int firstTile) {
         for (int i = 0; i < blocks.Length; i++) {
            var block = blocks[i];
            if (block == null || block.Length < 16) continue;
            foreach (var layerStart in new[] { 8, 0 }) { // the flower is on the top layer; look at it first
               var matches = true;
               for (int k = 0; k < 4 && matches; k++) matches = ((block[layerStart + k * 2] | (block[layerStart + k * 2 + 1] << 8)) & 0x3FF) == firstTile + k;
               if (matches) return i;
            }
         }
         return -1;
      }

      #endregion
   }
}
