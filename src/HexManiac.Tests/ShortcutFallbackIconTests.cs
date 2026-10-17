using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   // The built-in sprites of the main-menu buttons (ShortcutFallbackIcons): every button has a coloured picture even when the ROM gives it none (an unedited game has no picture for Sound or OW Sprites).

   #region The pictures themselves

   public class ShortcutFallbackIconArtTests {
      private static (int red, int green, int blue) Rgb(short color) => (((color >> 10) & 31) * 255 / 31, ((color >> 5) & 31) * 255 / 31, (color & 31) * 255 / 31);

      [Fact]
      public void ThereIsAPictureForEveryKindOfButton() {
         var kinds = Enum.GetValues(typeof(ShortcutKind)).Cast<ShortcutKind>().ToList();

         Assert.Equal(kinds.Count, ShortcutFallbackIcons.AllKinds.Count);
         foreach (var kind in kinds) {
            Assert.Contains(kind, ShortcutFallbackIcons.AllKinds);
            Assert.NotNull(ShortcutFallbackIcons.Create(kind));
         }
      }

      [Fact]
      public void EveryPicture_HasTheSizeItSays_AndRealPixels() {
         foreach (var kind in ShortcutFallbackIcons.AllKinds) {
            var picture = ShortcutFallbackIcons.Create(kind);
            var (width, height) = ShortcutFallbackIcons.SizeOf(kind);

            Assert.Equal(width, picture.PixelWidth);
            Assert.Equal(height, picture.PixelHeight);
            Assert.Equal(width * height, picture.PixelData.Length);
            Assert.Equal(ShortcutFallbackIcons.Transparent, picture.Transparent);
            Assert.InRange(width, 16, kind == ShortcutKind.IntroManager ? 40 : 24);   // the same sizes as the item icons, overworld sprites and tiles the ROMs give these buttons; the bag is wide
            Assert.InRange(height, 16, 24);
            Assert.True(picture.PixelData.Count(pixel => pixel != ShortcutFallbackIcons.Transparent) > width * height / 4, $"{kind} is mostly empty");
         }
      }

      [Fact]
      public void EveryPicture_IsInColour_NotOneFlatGlyph() {
         foreach (var kind in ShortcutFallbackIcons.AllKinds) {
            var colors = ShortcutFallbackIcons.Create(kind).PixelData.Where(pixel => pixel != ShortcutFallbackIcons.Transparent).Distinct().ToList();

            Assert.True(colors.Count >= 5, $"{kind} has only {colors.Count} colours: it should have an outline, shades and a highlight like the pictures of the ROMs");
         }
      }

      [Fact]
      public void EveryPicture_ShowsOnADarkBackground() {
         // the glyphs this replaces were drawn in a grey of 33/255 and almost vanished on the dark theme (about 40/255): a good part of every picture must stand out from it
         foreach (var kind in ShortcutFallbackIcons.AllKinds) {
            var opaque = ShortcutFallbackIcons.Create(kind).PixelData.Where(pixel => pixel != ShortcutFallbackIcons.Transparent).ToList();
            var light = opaque.Count(pixel => { var (r, g, b) = Rgb(pixel); return (r + g + b) / 3 >= 90; });

            Assert.True(light * 10 >= opaque.Count * 3, $"{kind}: only {light} of {opaque.Count} pixels are light");
         }
      }

      [Fact]
      public void EveryPicture_ShowsOnALightBackground_Too() {
         foreach (var kind in ShortcutFallbackIcons.AllKinds) {
            var opaque = ShortcutFallbackIcons.Create(kind).PixelData.Where(pixel => pixel != ShortcutFallbackIcons.Transparent).ToList();
            var dark = opaque.Count(pixel => { var (r, g, b) = Rgb(pixel); return (r + g + b) / 3 <= 90; });

            Assert.True(dark >= 8, $"{kind} has no dark outline ({dark} dark pixels)");
         }
      }

      [Fact]
      public void ThePicturesAreDifferentFromEachOther() {
         var all = ShortcutFallbackIcons.AllKinds.Select(kind => string.Join(",", ShortcutFallbackIcons.Create(kind).PixelData)).ToList();

         Assert.Equal(all.Count, all.Distinct().Count());
      }

      [Fact]
      public void TheSoundPicture_IsAPokeFlute() {
         // the Sound button shows the Poke Flute item of the ROM; the built-in picture (a ROM without the item) is a Poke Flute too: cream tube, gold fittings, red bulb
         var pixels = ShortcutFallbackIcons.Create(ShortcutKind.Sound).PixelData.Where(pixel => pixel != ShortcutFallbackIcons.Transparent).Select(Rgb).ToList();

         Assert.Contains(pixels, c => c.red >= 200 && c.green <= 90 && c.blue <= 90);       // the red bulb
         Assert.Contains(pixels, c => c.red >= 220 && c.green >= 170 && c.blue <= 80);      // the gold ring and mouthpiece
         Assert.Contains(pixels, c => c.red >= 220 && c.green >= 220 && c.blue >= 200);     // the cream tube
         Assert.DoesNotContain(pixels, c => c.blue > c.red + 60);                            // and no longer the blue music note
      }

      [Fact]
      public void TheIntroManagerPicture_IsABrownBagWithCreamPaper_NotAProfessor() {
         // Professor Birch's bag: tan and brown leather, a cream flap, no Poke Ball red and no skin-and-white professor
         var pixels = ShortcutFallbackIcons.Create(ShortcutKind.IntroManager).PixelData.Where(pixel => pixel != ShortcutFallbackIcons.Transparent).Select(Rgb).ToList();

         Assert.True(pixels.Count(c => c.red > c.blue + 50 && c.red >= 150 && c.green >= 100) * 3 >= pixels.Count, "mostly tan leather");
         Assert.Contains(pixels, c => c.red >= 200 && c.green >= 195 && c.blue >= 180);   // the cream paper
         Assert.DoesNotContain(pixels, c => c.red >= 200 && c.green <= 90 && c.blue <= 90); // no red Poke Ball
         Assert.DoesNotContain(pixels, c => c.red >= 250 && c.green >= 250 && c.blue >= 250); // and no white coat
      }

      [Fact]
      public void ThePicturesAreMadeOfGbaColours_AndNeverOfTheTransparentKey() {
         foreach (var kind in ShortcutFallbackIcons.AllKinds) {
            foreach (var pixel in ShortcutFallbackIcons.Create(kind).PixelData) {
               Assert.Equal(0, pixel & 0x8000); // 15 bits only
            }
         }
      }

      [Fact]
      public void EachButtonGetsItsOwnViewModelAndItsOwnPixels() {
         var first = ShortcutFallbackIcons.Create(ShortcutKind.Sound);
         var second = ShortcutFallbackIcons.Create(ShortcutKind.Sound);

         Assert.NotSame(first, second);               // a view model that lives on would keep the dialog's images alive
         Assert.NotSame(first.PixelData, second.PixelData);
         Assert.Equal(first.PixelData, second.PixelData);
         var before = second.PixelData[0];
         first.PixelData[0] = (short)(first.PixelData[0] == 5 ? 6 : 5);
         Assert.Equal(before, second.PixelData[0]);
         Assert.Equal(before, ShortcutFallbackIcons.Create(ShortcutKind.Sound).PixelData[0]); // and a change to one never reaches the next one made
      }

      [Fact]
      public void ParseArt_MakesGbaColoursFromEightBitColours() {
         var (width, height, pixels) = ShortcutFallbackIcons.ParseArt("a=f80000 b=00f800 c=0000f8", new[] { "a.b", "c.." });

         Assert.Equal(3, width);
         Assert.Equal(2, height);
         Assert.Equal(new short[] { 31 << 10, ShortcutFallbackIcons.Transparent, 31 << 5, 31, ShortcutFallbackIcons.Transparent, ShortcutFallbackIcons.Transparent }, pixels);
      }

      [Fact]
      public void ParseArt_RefusesBrokenArt() {
         Assert.Throws<FormatException>(() => ShortcutFallbackIcons.ParseArt("a=f80000", new[] { "a.", "a" }));       // a short row
         Assert.Throws<FormatException>(() => ShortcutFallbackIcons.ParseArt("a=f80000", new[] { "ab" }));            // no colour for b
         Assert.Throws<FormatException>(() => ShortcutFallbackIcons.ParseArt("a=f8000", new[] { "a" }));              // a colour that is not 6 digits
         Assert.Throws<FormatException>(() => ShortcutFallbackIcons.ParseArt("a=f80000", new string[0]));             // nothing at all
      }
   }

   #endregion

   #region Which kind of button is it

   public class ShortcutFallbackIconIdentifyTests {
      [Theory]
      [InlineData("data.pokemon.stats/1", "Pokemon", ShortcutKind.Pokemon)]
      [InlineData("data.trainers.stats", "Trainers", ShortcutKind.Trainers)]
      [InlineData("data.pokemon.moves.stats.battle", "Moves", ShortcutKind.Moves)]   // the default metadata of the vanilla games
      [InlineData("data.pokemon.moves.stats", "Moves", ShortcutKind.Moves)]          // CUBE.toml
      [InlineData("data.items.stats", "Items", ShortcutKind.Items)]
      [InlineData("maps.bank0.littleroot town.0-9 ", "Maps", ShortcutKind.Maps)]
      [InlineData("maps.0-9 (BOURG-EN-VOL)", "Maps", ShortcutKind.Maps)]             // the French games
      [InlineData("editor:sound", "Sound", ShortcutKind.Sound)]
      [InlineData("editor:animations", "Anim Tiles", ShortcutKind.AnimatedTiles)]
      [InlineData("editor:customization", "Character\nCustomization", ShortcutKind.CharacterCustomization)]
      [InlineData("graphics.overworld.sprites", "OW Sprites", ShortcutKind.OverworldSprites)]
      [InlineData("EDITOR:Sound", "x", ShortcutKind.Sound)]                          // capital letters never mattered to the goto box
      [InlineData("  data.items.stats ", "x", ShortcutKind.Items)]
      [InlineData("editor:music", "x", ShortcutKind.Sound)]                          // the aliases of ViewPort.OpenEditorTab
      [InlineData("editor:cries", "x", ShortcutKind.Sound)]
      [InlineData("editor:tiles", "x", ShortcutKind.AnimatedTiles)]
      [InlineData("editor:character", "x", ShortcutKind.CharacterCustomization)]
      [InlineData("editor:intro", "Intro\nManager", ShortcutKind.IntroManager)]
      [InlineData("my.table", "Intro Manager", ShortcutKind.IntroManager)]
      [InlineData("editor:sprites", "x", ShortcutKind.OverworldSprites)]
      [InlineData("editor:gallery", "x", ShortcutKind.OverworldSprites)]
      [InlineData("graphics.pokemon.sprites.front/1", "x", ShortcutKind.Pokemon)]
      [InlineData("graphics.items.sprites", "x", ShortcutKind.Items)]
      [InlineData("sound.tracks", "x", ShortcutKind.Sound)]
      public void TheDestinationSaysWhichKind(string destination, string text, ShortcutKind expected) {
         Assert.Equal(expected, ShortcutFallbackIcons.Identify(destination, text));
      }

      [Theory]
      [InlineData(null, "Music", ShortcutKind.Sound)]
      [InlineData("", "Music", ShortcutKind.Sound)]
      [InlineData("my.songs", "Songs", ShortcutKind.Sound)]
      [InlineData("my.table", "Pokémon", ShortcutKind.Pokemon)]                 // an accent doesn't matter
      [InlineData("my.table", "Trainer Info", ShortcutKind.Trainers)]
      [InlineData("my.table", "Character\nCustomization", ShortcutKind.CharacterCustomization)]
      [InlineData("my.table", "Pokemon Moves", ShortcutKind.Moves)]
      [InlineData("my.table", "Bag items", ShortcutKind.Items)]
      [InlineData("my.table", "World MAP", ShortcutKind.Maps)]
      public void WithoutAHelpfulDestination_TheTextIsTheSecondChance(string destination, string text, ShortcutKind expected) {
         Assert.Equal(expected, ShortcutFallbackIcons.Identify(destination, text));
      }

      [Theory]
      [InlineData("my.table", "Whatever")]
      [InlineData("", "")]
      [InlineData(null, null)]
      [InlineData("editor:unknown", "Something")]
      public void AButtonNobodyKnows_IsGeneric(string destination, string text) {
         Assert.Equal(ShortcutKind.Generic, ShortcutFallbackIcons.Identify(destination, text));
      }

      [Fact]
      public void TheNamesOfTheRealMenuButtons_AllHaveTheirOwnKind() {
         // every button of the main menu of CUBE (data/GotoShortcut entries plus the editor buttons)
         var buttons = new[] {
            ("data.pokemon.stats/1", "Pokemon"), ("data.trainers.stats", "Trainers"), ("data.pokemon.moves.stats", "Moves"), ("data.items.stats", "Items"), ("maps.bank0.littleroot town.0-9 ", "Maps"),
            ("editor:sound", "Sound"), (HardcodeTablesModel.OverworldSprites, "OW Sprites"), ("editor:animations", "Anim Tiles"), ("editor:customization", "Character\nCustomization"), ("editor:intro", "Intro\nManager"),
         };
         var kinds = buttons.Select(button => ShortcutFallbackIcons.Identify(button.Item1, button.Item2)).ToList();

         Assert.Equal(buttons.Length, kinds.Distinct().Count());
         Assert.DoesNotContain(ShortcutKind.Generic, kinds);
      }
   }

   #endregion

   #region The button uses the picture only when it has none

   public class GotoShortcutFallbackImageTests {
      private static IPixelViewModel Frame() => new ReadonlyPixelViewModel(1, 1, new short[1], -1);

      private static GotoShortcutViewModel Create(IPixelViewModel image) => new GotoShortcutViewModel(null, null, image, "anchor", "text");

      private static GotoShortcutViewModel Create(Func<IPixelViewModel> factory) => new GotoShortcutViewModel(null, null, factory, "anchor", "text");

      [Fact]
      public void AButtonWithoutAPicture_ShowsTheFallback() {
         var fallback = Frame();
         var shortcut = Create((IPixelViewModel)null);

         shortcut.SetFallbackImage(() => fallback);

         Assert.Same(fallback, shortcut.Image);
      }

      [Fact]
      public void AButtonWithAFactoryThatHasNoPicture_ShowsTheFallback() {
         var fallback = Frame();
         var shortcut = Create(() => null);
         shortcut.SetFallbackImage(() => fallback);

         Assert.Same(fallback, shortcut.Image);
      }

      [Fact]
      public void AFactoryThatFails_ShowsTheFallback() {
         var fallback = Frame();
         var shortcut = Create(() => throw new InvalidOperationException("no item table"));
         shortcut.SetFallbackImage(() => fallback);

         Assert.Same(fallback, shortcut.Image);
      }

      [Fact]
      public void ThePictureOfTheRom_AlwaysWins_AndTheFallbackIsNeverMade() {
         var calls = 0;
         var own = Frame();
         var withPicture = Create(own);
         withPicture.SetFallbackImage(() => { calls++; return Frame(); });
         var withFactory = Create(() => own);
         withFactory.SetFallbackImage(() => { calls++; return Frame(); });

         Assert.Same(own, withPicture.Image);
         Assert.Same(own, withFactory.Image);
         Assert.Equal(0, calls);
      }

      [Fact]
      public void PreferImage_ThatFindsNothing_AndAButtonThatHadNoPicture_ShowsTheFallback() {
         var fallback = Frame();
         var shortcut = Create((IPixelViewModel)null);
         shortcut.PreferImage(() => null);
         shortcut.SetFallbackImage(() => fallback);

         Assert.Same(fallback, shortcut.Image);
      }

      [Fact]
      public void PreferImage_ThatFindsAPicture_BeatsTheFallback() {
         var preferred = Frame();
         var shortcut = Create((IPixelViewModel)null);
         shortcut.SetFallbackImage(() => Frame());
         shortcut.PreferImage(() => preferred);

         Assert.Same(preferred, shortcut.Image);
      }

      [Fact]
      public void TheFallback_IsMadeWhenTheButtonIsFirstShown_AndOnlyOnce() {
         var calls = 0;
         var fallback = Frame();
         var shortcut = Create(() => null);
         shortcut.SetFallbackImage(() => { calls++; return fallback; });

         Assert.Equal(0, calls); // preparing the dialog draws nothing

         Assert.Same(fallback, shortcut.Image);
         Assert.Same(fallback, shortcut.Image);
         Assert.Equal(1, calls);
      }

      [Fact]
      public void AFallbackThatHasNothing_OrFails_IsAskedOnce_AndIsNotAnException() {
         var calls = 0;
         var shortcut = Create(() => null);
         shortcut.SetFallbackImage(() => { calls++; throw new InvalidOperationException("broken art"); });

         Assert.Null(shortcut.Image);
         Assert.Null(shortcut.Image);
         Assert.Equal(1, calls);
      }

      [Fact]
      public void AnimationWithoutFrames_LeavesTheFallbackToShow() {
         var fallback = Frame();
         var shortcut = Create(() => null);
         shortcut.SetAnimationSource(() => null); // the flower can't be found in this ROM
         shortcut.SetFallbackImage(() => fallback);

         Assert.Same(fallback, shortcut.Image);
         Assert.False(shortcut.IsAnimated);
      }

      [Fact]
      public void AnAnimation_BeatsTheFallback() {
         var frames = new[] { Frame(), Frame() };
         var shortcut = Create(() => null);
         shortcut.SetAnimationSource(() => ShortcutAnimation.Create(frames, new[] { 100, 100 }));
         shortcut.SetFallbackImage(() => Frame());

         Assert.Same(frames[0], shortcut.Image);
         Assert.True(shortcut.IsAnimated);
      }

      [Fact]
      public void Attach_GivesTheBuiltInPictureOfTheKindOfButton() {
         var shortcut = new GotoShortcutViewModel(null, null, (IPixelViewModel)null, "editor:sound", "Sound");

         ShortcutFallbackIcons.Attach(shortcut, "editor:sound");

         Assert.NotNull(shortcut.Image);
         Assert.Equal(ShortcutFallbackIcons.Create(ShortcutKind.Sound).PixelData, shortcut.Image.PixelData);
      }

      [Fact]
      public void Attach_NeverReplacesThePictureOfTheRom() {
         var own = Frame();
         var shortcut = new GotoShortcutViewModel(null, null, own, "data.items.stats", "Items");

         ShortcutFallbackIcons.Attach(shortcut, "data.items.stats");

         Assert.Same(own, shortcut.Image);
         ShortcutFallbackIcons.Attach(null, "data.items.stats"); // a button that isn't there is no problem
      }
   }

   #endregion

   #region The whole main menu of a ROM that has no pictures for some buttons

   public class ShortcutFallbackEditorTests : BaseViewModelTestClass {
      private GotoShortcutViewModel[] ButtonsOfANewEditor() {
         var editor = New.EditorViewModel();
         editor.Add(ViewPort);
         return editor.GotoViewModel.Shortcuts.ToArray();
      }

      [Fact]
      public void SoundAndOverworldSpritesButtons_HaveColouredSprites_WhenTheRomHasNoPictureForThem() {
         // a ROM with the tables of the Sound editor and of the overworld sprites, but no items table with icons and no names for the player's sprites: an unedited game
         CreateTextTable(SoundTab.SongTable, 0x100, "song a", "song b");
         CreateTextTable(HardcodeTablesModel.OverworldSprites, 0x140, "one", "two");

         var buttons = ButtonsOfANewEditor();
         var sound = buttons.Single(button => button.DisplayText == "Sound");
         var sprites = buttons.Single(button => button.DisplayText == "OW Sprites");

         Assert.Equal(ShortcutFallbackIcons.Create(ShortcutKind.Sound).PixelData, sound.Image.PixelData);
         Assert.Equal(ShortcutFallbackIcons.Create(ShortcutKind.OverworldSprites).PixelData, sprites.Image.PixelData);
         Assert.Equal(ShortcutFallbackIcons.SizeOf(ShortcutKind.Sound), (sound.Image.PixelWidth, sound.Image.PixelHeight));
      }

      [Fact]
      public void AShortcutFromTheMetadata_WhoseImageCantBeFound_GetsTheBuiltInPictureOfItsKind() {
         var shortcuts = new[] {
            new StoredGotoShortcut("Items", "data.items.stats/13/icon/", "data.items.stats"),
            new StoredGotoShortcut("Moves", "graphics.items.sprites/298/sprite/", "data.pokemon.moves.stats.battle"),
         };
         Model.LoadMetadata(new StoredMetadata(default, default, default, default, default, default, shortcuts, Singletons.MetadataInfo, default));
         CreateTextTable(HardcodeTablesModel.ItemsTableName, 0x100, "ONE", "TWO");
         CreateTextTable("data.pokemon.moves.stats.battle", 0x120, "AAA", "BBB");

         var buttons = ButtonsOfANewEditor();
         var items = buttons.Single(button => button.DisplayText == "Items");
         var moves = buttons.Single(button => button.DisplayText == "Moves");

         Assert.Equal(ShortcutFallbackIcons.Create(ShortcutKind.Items).PixelData, items.Image.PixelData);
         Assert.Equal(ShortcutFallbackIcons.Create(ShortcutKind.Moves).PixelData, moves.Image.PixelData);
      }

      [Fact]
      public void EveryButtonOfTheMenu_HasAPicture() {
         CreateTextTable(SoundTab.SongTable, 0x100, "song a", "song b");
         CreateTextTable(HardcodeTablesModel.OverworldSprites, 0x140, "one", "two");

         foreach (var button in ButtonsOfANewEditor()) Assert.NotNull(button.Image);
      }
   }

   #endregion

   #region The Sound button shows the Poke Flute

   public class ShortcutPokeFluteNameTests {
      [Theory]
      [InlineData("Poké Flute")]
      [InlineData("POKé FLUTE")]
      [InlineData("\"POKE FLUTE\"")]
      [InlineData("Poke_Flute")]
      [InlineData("FLÛTE POKé")]     // the other languages of the games, in either order of the words
      [InlineData("POKéFLÖTE")]
      [InlineData("FLAUTA POKé")]
      [InlineData("POKéFLAUTO")]
      public void ThePokeFlute_IsRecognizedByItsName(string name) {
         Assert.True(ShortcutIcons.IsPokeFluteName(name));
      }

      [Theory]
      [InlineData("Blue Flute")]
      [InlineData("YELLOW FLUTE")]
      [InlineData("Poké Ball")]
      [InlineData("Poke Doll")]
      [InlineData("Soothe Bell")]
      [InlineData("Flute")]
      [InlineData("")]
      public void EveryOtherItem_IsNot(string name) {
         Assert.False(ShortcutIcons.IsPokeFluteName(name));
      }

      [Fact]
      public void ANameThatIsNotThere_IsNotThePokeFlute() {
         Assert.False(ShortcutIcons.IsPokeFluteName(null));
      }
   }

   public class ShortcutPokeFluteItemTests : BaseViewModelTestClass {
      private const int Table = 0x100;

      public ShortcutPokeFluteItemTests() : base(0x2000) { }

      [Fact]
      public void ThePokeFlute_IsFoundByItsName_WhereverItIs() {
         CreateTextTable(HardcodeTablesModel.ItemsTableName, Table, "BLUE FLUTE", "POKE BALL", "POKE FLUTE", "SODA POP");

         Assert.Equal(2, ShortcutIcons.FindPokeFluteItem(Model));
      }

      [Fact]
      public void ARomWithoutItemNamedLikeIt_HasNoPokeFlute() {
         CreateTextTable(HardcodeTablesModel.ItemsTableName, Table, "BLUE FLUTE", "POKE BALL");

         Assert.Equal(-1, ShortcutIcons.FindPokeFluteItem(Model));
      }

      [Fact]
      public void ARomWithoutAnItemTable_HasNoPokeFlute_AndNothingThrows() {
         Assert.Equal(-1, ShortcutIcons.FindPokeFluteItem(Model));
         Assert.Null(ShortcutIcons.PokeFlute(Model));
      }

      private void CreateItemsWithoutNames(string gameCode) {
         SetFullModel(0xFF); // every name is empty
         SetGameCode(gameCode);
         ViewPort.Edit($"@{Table:X} ^{HardcodeTablesModel.ItemsTableName}[name\"\"12]360 ");
      }

      private void RenameItem(int item, string name) => ViewPort.Edit($"@{Table + item * 12:X} {name}\" ");

      [Theory]
      [InlineData("BPRE0")] // FireRed
      [InlineData("BPGE1")] // LeafGreen 1.1
      [InlineData("BPEE0")] // Emerald
      [InlineData("BPRF0")] // a language other than English
      public void WithoutNames_TheUneditedGamesUseTheOriginalNumberOfThePokeFlute(string gameCode) {
         CreateItemsWithoutNames(gameCode);

         Assert.Equal(ShortcutIcons.OriginalPokeFluteItem, ShortcutIcons.FindPokeFluteItem(Model));
         Assert.Equal(350, ShortcutIcons.OriginalPokeFluteItem);
      }

      [Theory]
      [InlineData("AXVE0")] // Ruby
      [InlineData("AXPE1")] // Sapphire
      [InlineData("XXXX0")]
      public void WithoutNames_AnyOtherGame_HasNoPokeFlute(string gameCode) {
         CreateItemsWithoutNames(gameCode);

         Assert.Equal(-1, ShortcutIcons.FindPokeFluteItem(Model));
      }

      [Fact]
      public void AnItemThatSomeoneRenamed_IsNotTakenForThePokeFluteByItsNumber() {
         CreateItemsWithoutNames("BPRE0");
         RenameItem(350, "MASTER BALL");

         Assert.Equal(-1, ShortcutIcons.FindPokeFluteItem(Model));
      }

      [Fact]
      public void TheNameWins_OverTheNumber() {
         CreateItemsWithoutNames("BPRE0");
         RenameItem(7, "POKE FLUTE"); // not at 350 (a hack that moved it)

         Assert.Equal(7, ShortcutIcons.FindPokeFluteItem(Model));
      }
   }

   public class ShortcutPokeFluteIconTests : BaseViewModelTestClass {
      private const short Blue = 0x7C00;      // as the palette stores it (blue is the high bits)
      private const short BlueDrawn = 0x001F;  // as the picture's pixels have it (the red and blue bits are swapped)
      private const int Items = 0x100, Pictures = 0x300, Sprite = 0x400, Palette = 0x600, Songs = 0x700;

      public ShortcutPokeFluteIconTests() : base(0x1000) { }

      private void WriteName(int address, string name, int maxLength) {
         var bytes = Model.TextConverter.Convert(name, out _);
         for (int i = 0; i < maxLength; i++) Model[address + i] = i < bytes.Count ? bytes[i] : (byte)0xFF;
      }

      // a 24x24 picture (3x3 tiles) of palette colour 1, with a palette whose colour 1 is blue
      private void CreatePicture() {
         for (int i = 0; i < 9 * 32; i++) Model[Sprite + i] = 0x11;
         Model.WriteMultiByteValue(Palette + 2, 2, Token, Blue);
      }

      [Fact]
      public void ExpansionStyle_ThePictureIsTheIconFieldOfTheItem() {
         CreatePicture();
         ViewPort.Edit($"@{Items:X} ^{HardcodeTablesModel.ItemsTableName}[name\"\"12 icon<`ucs4x3x3`> palette<`ucp4`>]3 ");
         WriteName(Items, "BLUE FLUTE", 12);
         WriteName(Items + 20, "POKE BALL", 12);
         WriteName(Items + 40, "Poké Flute", 12);
         Model.WritePointer(Token, Items + 40 + 12, Sprite);
         Model.WritePointer(Token, Items + 40 + 16, Palette);
         ViewPort.Edit($"@{Sprite:X} ^fluteicon`ucs4x3x3` @{Palette:X} ^flutepalette`ucp4` ");

         var picture = ShortcutIcons.PokeFlute(Model);

         Assert.NotNull(picture);
         Assert.Equal((24, 24), (picture.PixelWidth, picture.PixelHeight));
         Assert.All(picture.PixelData, pixel => Assert.Equal(BlueDrawn, pixel));
      }

      private void CreateOriginalStyleItems(string flute = "POKE FLUTE") {
         CreateTextTable(HardcodeTablesModel.ItemsTableName, Items, "BLUE FLUTE", "POKE BALL", flute);
         CreatePicture();
         ViewPort.Edit($"@{Pictures:X} ^{HardcodeTablesModel.ItemImagesTableName}[sprite<`ucs4x3x3`> palette<`ucp4`>]3 ");
         Model.WritePointer(Token, Pictures + 2 * 8, Sprite);
         Model.WritePointer(Token, Pictures + 2 * 8 + 4, Palette);
         ViewPort.Edit($"@{Sprite:X} ^fluteicon`ucs4x3x3` @{Palette:X} ^flutepalette`ucp4` ");
      }

      [Fact]
      public void OriginalGamesStyle_ThePictureIsTheEntryOfTheItemPictureTable() {
         CreateOriginalStyleItems();

         var picture = ShortcutIcons.PokeFlute(Model);

         Assert.NotNull(picture);
         Assert.Equal((24, 24), (picture.PixelWidth, picture.PixelHeight));
         Assert.All(picture.PixelData, pixel => Assert.Equal(BlueDrawn, pixel));
      }

      [Fact]
      public void ARomWithAFluteButNoPokeFlute_GivesNoPicture_NotTheBlueFlute() {
         CreateOriginalStyleItems("SODA POP");

         Assert.Null(ShortcutIcons.PokeFlute(Model));
      }

      [Fact]
      public void APokeFluteWithoutAPicture_GivesNoPicture() {
         CreateTextTable(HardcodeTablesModel.ItemsTableName, Items, "BLUE FLUTE", "POKE BALL", "POKE FLUTE");

         Assert.Equal(2, ShortcutIcons.FindPokeFluteItem(Model));
         Assert.Null(ShortcutIcons.PokeFlute(Model));
      }

      private GotoShortcutViewModel SoundButton() {
         CreateTextTable(SoundTab.SongTable, Songs, "song a", "song b");
         var editor = New.EditorViewModel();
         editor.Add(ViewPort);
         return editor.GotoViewModel.Shortcuts.Single(button => button.DisplayText == "Sound");
      }

      [Fact]
      public void TheSoundButton_ShowsThePokeFluteOfTheRom() {
         CreateOriginalStyleItems();

         var button = SoundButton();

         Assert.NotNull(button.Image);
         Assert.All(button.Image.PixelData, pixel => Assert.Equal(BlueDrawn, pixel)); // the ROM's picture, not the built-in one
      }

      [Fact]
      public void TheSoundButton_OfARomWithoutAPokeFlute_ShowsTheBuiltInPokeFlute() {
         CreateOriginalStyleItems("SODA POP");

         var button = SoundButton();

         Assert.Equal(ShortcutFallbackIcons.Create(ShortcutKind.Sound).PixelData, button.Image.PixelData);
      }
   }

   #endregion

   #region The OW Sprites button shows the first picture of sprite 0

   public class ShortcutFirstOverworldSpriteTests : BaseViewModelTestClass {
      private const short Red = 0x001F, Blue = 0x7C00;           // as the palettes store them
      private const short BlueDrawn = 0x001F;                     // as the picture's pixels have it (the red and blue bits are swapped)
      private const int Palettes = 0x60, SpriteList = 0x80, Table = 0x100, Sprite0 = 0x120, Sprite1 = 0x140, Songs = 0x300;

      public ShortcutFirstOverworldSpriteTests() : base(0x400) { }

      // sprite 0 is 8x8, its picture is made of colour 1, and its palette (id 1) has a blue colour 1; the palette with id 0 has a red one
      private void CreateTable(bool sprite0HasGraphics = true, bool withNames = false) {
         for (int i = 0; i < 0x20; i++) Model[i] = 0x11;
         Model.WriteMultiByteValue(0x20 + 2, 2, Token, Red);
         Model.WriteMultiByteValue(0x40 + 2, 2, Token, Blue);
         ViewPort.Edit($"@{Palettes:X2} <020> 00 00 <040> 01 00 @{Palettes:X2} ^{HardcodeTablesModel.OverworldPalettes}[pal<`ucp4`> id:|h]2 ");
         ViewPort.Edit($"@{SpriteList:X2} <000> 20 ");
         ViewPort.Edit(sprite0HasGraphics ? $"@{Sprite0:X} FF FF 01 00 40 00 08 00 08 00 <080>" : $"@{Sprite0:X} FF FF 01 00 40 00 08 00 08 00 00 00 00 00");
         ViewPort.Edit($"@{Sprite1:X} FF FF 00 00 40 00 08 00 08 00 <080>"); // sprite 1 uses the same picture with the red palette
         if (withNames) Model.SetList(new ModelDelta(), "owNames", "BRENDAN_NORMAL", "OTHER");
         ViewPort.Edit($"@{Table:X} <{Sprite0:X}> <{Sprite1:X}> @{Table:X} ^{HardcodeTablesModel.OverworldSprites}[data<[starterbytes:|h paletteid:|h length: width: height: sprites<`osl`>]1>]{(withNames ? "owNames" : "2")} ");
      }

      [Fact]
      public void ARomWithoutTheTable_HasNoSprite() {
         Assert.Null(ShortcutIcons.FirstOverworldSprite(Model));
      }

      [Fact]
      public void Sprite0_IsDrawnWithItsOwnPalette() {
         CreateTable();

         var picture = ShortcutIcons.FirstOverworldSprite(Model);

         Assert.NotNull(picture);
         Assert.Equal((8, 8), (picture.PixelWidth, picture.PixelHeight));
         Assert.All(picture.PixelData, pixel => Assert.Equal(BlueDrawn, pixel)); // the palette with the id sprite 0 names, not the one of sprite 1
      }

      [Fact]
      public void TheSprite_IsTheOneTheGalleryShowsForSprite0() {
         CreateTable();

         var fromGallery = OverworldSpriteRenderer.Get(Model).Get(0);

         Assert.False(fromGallery.isPlaceholder);
         Assert.Equal(fromGallery.image.PixelData, ShortcutIcons.FirstOverworldSprite(Model).PixelData);
      }

      [Fact]
      public void ASpriteWithoutGraphics_GivesNoPicture_NotAPlaceholder() {
         CreateTable(sprite0HasGraphics: false);

         Assert.Null(ShortcutIcons.FirstOverworldSprite(Model));
      }

      [Fact]
      public void ForAPlayerCalledBrendan_ItIsThePictureTheCustomizationButtonUsesToo() {
         CreateTable(withNames: true);

         var first = ShortcutIcons.FirstOverworldSprite(Model);
         var brendan = ShortcutIcons.BrendanStanding(Model);

         Assert.NotNull(first);
         Assert.NotNull(brendan);
         Assert.Equal((brendan.PixelWidth, brendan.PixelHeight), (first.PixelWidth, first.PixelHeight));
         Assert.Equal(brendan.PixelData, first.PixelData);
      }

      private GotoShortcutViewModel Button() {
         CreateTextTable(SoundTab.SongTable, Songs, "song a", "song b");
         var editor = New.EditorViewModel();
         editor.Add(ViewPort);
         return editor.GotoViewModel.Shortcuts.Single(button => button.DisplayText == "OW Sprites");
      }

      [Fact]
      public void TheButton_ShowsSprite0() {
         CreateTable();

         var button = Button();

         Assert.Equal((8, 8), (button.Image.PixelWidth, button.Image.PixelHeight));
         Assert.All(button.Image.PixelData, pixel => Assert.Equal(BlueDrawn, pixel));
      }

      [Fact]
      public void TheButton_OfARomWhoseSprite0HasNoGraphics_ShowsTheBuiltInPicture() {
         CreateTable(sprite0HasGraphics: false);

         var button = Button();

         Assert.Equal(ShortcutFallbackIcons.Create(ShortcutKind.OverworldSprites).PixelData, button.Image.PixelData);
      }
   }

   /// <summary>The OW Sprites button shows overworld sprite 235 (the RS Brendan) when the table has it, sprite 0 otherwise.</summary>
   public class ShortcutOverworldSprite235Tests : BaseViewModelTestClass {
      private const short BlueDrawn = 0x001F, RedDrawn = 0x7C00; // as the picture's pixels have it (the red and blue bits are swapped)
      private const int Palettes = 0x60, SpriteList = 0x80, Sprite0 = 0x120, Sprite1 = 0x140, Table = 0x400;

      public ShortcutOverworldSprite235Tests() : base(0x1000) { }

      // palette id 1 is blue and id 0 is red; every sprite but 235 uses the blue one (id 1), sprite 235 the red one (id 0)
      private void CreateTable(int count, bool lastHasGraphics = true) {
         for (int i = 0; i < 0x20; i++) Model[i] = 0x11;
         Model.WriteMultiByteValue(0x20 + 2, 2, Token, 0x001F);
         Model.WriteMultiByteValue(0x40 + 2, 2, Token, 0x7C00);
         ViewPort.Edit($"@{Palettes:X2} <020> 00 00 <040> 01 00 @{Palettes:X2} ^{HardcodeTablesModel.OverworldPalettes}[pal<`ucp4`> id:|h]2 ");
         ViewPort.Edit($"@{SpriteList:X2} <000> 20 ");
         ViewPort.Edit($"@{Sprite0:X} FF FF 01 00 40 00 08 00 08 00 <080>");
         ViewPort.Edit(lastHasGraphics ? $"@{Sprite1:X} FF FF 00 00 40 00 08 00 08 00 <080>" : $"@{Sprite1:X} FF FF 00 00 40 00 08 00 08 00 00 00 00 00");
         for (int i = 0; i < count; i++) Model.WritePointer(Token, Table + i * 4, i == ShortcutIcons.ButtonSpriteIndex ? Sprite1 : Sprite0);
         ViewPort.Edit($"@{Table:X} ^{HardcodeTablesModel.OverworldSprites}[data<[starterbytes:|h paletteid:|h length: width: height: sprites<`osl`>]1>]{count} ");
      }

      [Fact]
      public void ATableWith236Sprites_ShowsSprite235() {
         CreateTable(236);

         var picture = ShortcutIcons.FirstOverworldSprite(Model);

         Assert.NotNull(picture);
         Assert.All(picture.PixelData, pixel => Assert.Equal(RedDrawn, pixel));
      }

      [Fact]
      public void ATableWith235Sprites_ShowsSprite0() {
         CreateTable(235);

         var picture = ShortcutIcons.FirstOverworldSprite(Model);

         Assert.NotNull(picture);
         Assert.All(picture.PixelData, pixel => Assert.Equal(BlueDrawn, pixel));
      }

      [Fact]
      public void Sprite235WithoutGraphics_FallsBackToSprite0() {
         CreateTable(236, lastHasGraphics: false);

         var picture = ShortcutIcons.FirstOverworldSprite(Model);

         Assert.NotNull(picture);
         Assert.All(picture.PixelData, pixel => Assert.Equal(BlueDrawn, pixel));
      }

      [Fact]
      public void TheButton_ShowsSprite235() {
         CreateTable(236);
         var editor = New.EditorViewModel();
         editor.Add(ViewPort);

         var button = editor.GotoViewModel.Shortcuts.Single(b => b.DisplayText == "OW Sprites");

         Assert.All(button.Image.PixelData, pixel => Assert.Equal(RedDrawn, pixel));
      }
   }

   #endregion
}
