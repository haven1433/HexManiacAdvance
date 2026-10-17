using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace HavenSoft.HexManiac.Core.ViewModels {
   /// <summary>The kinds of big button the Goto dialog (the main menu) can show. Each kind has a built-in picture (<see cref="ShortcutFallbackIcons"/>).</summary>
   public enum ShortcutKind {
      /// <summary>A button this program doesn't know (a shortcut made up in somebody's metadata).</summary>
      Generic,
      Pokemon,
      Trainers,
      Moves,
      Items,
      Maps,
      /// <summary>The Sound editor (music and cries): its picture is a Poke Flute.</summary>
      Sound,
      OverworldSprites,
      AnimatedTiles,
      CharacterCustomization,
      /// <summary>The Intro Manager (the start of a new game): Professor Birch's bag.</summary>
      IntroManager,
   }

   /// <summary>
   /// The built-in pictures of the main-menu buttons, used when the ROM and its metadata give a button no picture of its own.
   ///
   /// An unedited game has no picture for some buttons (the Sound editor, the overworld sprites...): before these pictures the button showed a dark grey one-colour glyph that almost disappeared on the dark
   /// background. Now every button always has a coloured sprite in the style of the others (an outline, a few shades, a highlight): a Poke Flute (the Sound button shows the Poke Flute item of the ROM, this is the one for a ROM that has none), an egg, a lightning bolt, a potion, a map, a trainer's head, a
   /// character in the overworld, a flower tile, a cartridge. They are drawn for this program (see ShortcutFallbackIcons.Art.cs); all but the Intro Manager's bag are original art. The bag is Professor Birch's bag of Emerald's starter screen cut down to a few colours.
   ///
   /// A picture that comes from the loaded ROM always wins: the fallback is only asked for when the button has no picture at all (<see cref="Attach"/>).
   /// The pixels are decoded once, the first time a picture is used; a button gets its own small view model with a copy of the pixels, so nothing is shared between buttons
   /// (a view model that outlives the dialog would keep the dialog's images alive) and nothing runs per frame.
   /// </summary>
   public static partial class ShortcutFallbackIcons {
      /// <summary>The colour that stands for 'see through' in the pictures (magenta; no colour of the art is this one).</summary>
      public const short Transparent = 0x7C1F;

      private static readonly object cacheLock = new();
      private static readonly Dictionary<ShortcutKind, Sprite> decoded = new();

      private class Sprite {
         public int Width { get; }
         public int Height { get; }
         public short[] Pixels { get; }
         public Sprite(int width, int height, short[] pixels) => (Width, Height, Pixels) = (width, height, pixels);
      }

      /// <summary>Every kind of button, in the order of the main menu.</summary>
      public static IReadOnlyList<ShortcutKind> AllKinds { get; } = new[] {
         ShortcutKind.Pokemon, ShortcutKind.Trainers, ShortcutKind.Moves, ShortcutKind.Items, ShortcutKind.Maps,
         ShortcutKind.Sound, ShortcutKind.OverworldSprites, ShortcutKind.AnimatedTiles, ShortcutKind.CharacterCustomization, ShortcutKind.IntroManager, ShortcutKind.Generic,
      };

      #region Which kind is this button?

      // the first one that matches the start of the button's destination wins, so the longer ones come first
      private static readonly (string prefix, ShortcutKind kind)[] DestinationPrefixes = {
         ("data.pokemon.moves", ShortcutKind.Moves),
         ("graphics.moves", ShortcutKind.Moves),
         ("data.moves", ShortcutKind.Moves),
         ("graphics.overworld", ShortcutKind.OverworldSprites),
         ("data.pokemon", ShortcutKind.Pokemon),
         ("graphics.pokemon", ShortcutKind.Pokemon),
         ("data.trainers", ShortcutKind.Trainers),
         ("graphics.trainers", ShortcutKind.Trainers),
         ("data.items", ShortcutKind.Items),
         ("graphics.items", ShortcutKind.Items),
         ("data.maps", ShortcutKind.Maps),
         ("graphics.maps", ShortcutKind.Maps),
         ("maps.", ShortcutKind.Maps),
         ("sound.", ShortcutKind.Sound),
      };

      // words (letters only, upper case, no accents) in the button's own text, tried in this order when the destination says nothing
      private static readonly (string word, ShortcutKind kind)[] NameWords = {
         ("MOVE", ShortcutKind.Moves),
         ("POKEMON", ShortcutKind.Pokemon),
         ("TRAINER", ShortcutKind.Trainers),
         ("ITEM", ShortcutKind.Items),
         ("MAP", ShortcutKind.Maps),
         ("SOUND", ShortcutKind.Sound),
         ("MUSIC", ShortcutKind.Sound),
         ("SONG", ShortcutKind.Sound),
         ("CRIES", ShortcutKind.Sound),
         ("CRY", ShortcutKind.Sound),
         ("SPRITE", ShortcutKind.OverworldSprites),
         ("OVERWORLD", ShortcutKind.OverworldSprites),
         ("ANIM", ShortcutKind.AnimatedTiles),
         ("CUSTOM", ShortcutKind.CharacterCustomization),
         ("CHARACTER", ShortcutKind.CharacterCustomization),
         ("INTRO", ShortcutKind.IntroManager),
      };

      /// <summary>
      /// Which kind of button this is, from the place it goes to (the metadata's 'Destination', or 'editor:sound' for the editor buttons) and, if that doesn't say, from its text.
      /// The destination is what every game and hack has in common ('data.pokemon.stats/1', 'data.items.stats', 'maps.bank0.littleroot town.0-9'); the text is only a second chance.
      /// A button that matches nothing is <see cref="ShortcutKind.Generic"/>.
      /// </summary>
      public static ShortcutKind Identify(string gotoAnchor, string displayText) {
         var destination = (gotoAnchor ?? string.Empty).Trim().ToLowerInvariant();
         if (destination.StartsWith(ViewPort.EditorGotoPrefix, StringComparison.Ordinal)) {
            switch (destination.Substring(ViewPort.EditorGotoPrefix.Length).Trim()) {
               case "sound": case "music": case "cries": return ShortcutKind.Sound;
               case "animations": case "tiles": return ShortcutKind.AnimatedTiles;
               case "customization": case "character": return ShortcutKind.CharacterCustomization;
                case "intro": return ShortcutKind.IntroManager;
               case "sprites": case "gallery": case "fieldeffects": case "reflections": case "reflection": return ShortcutKind.OverworldSprites;
            }
         }
         if (destination.StartsWith(HardcodeTablesModel.OverworldSprites, StringComparison.Ordinal)) return ShortcutKind.OverworldSprites;
         foreach (var (prefix, kind) in DestinationPrefixes) {
            if (destination.StartsWith(prefix, StringComparison.Ordinal)) return kind;
         }
         var name = ShortcutIcons.NormalizeName(displayText);
         foreach (var (word, kind) in NameWords) {
            if (name.Contains(word, StringComparison.Ordinal)) return kind;
         }
         return ShortcutKind.Generic;
      }

      #endregion

      #region The pictures

      /// <summary>The size in pixels of the picture of a kind of button (the same as the pictures a ROM gives that button, so the button looks the same size).</summary>
      public static (int width, int height) SizeOf(ShortcutKind kind) {
         var sprite = Decode(kind);
         return sprite == null ? (0, 0) : (sprite.Width, sprite.Height);
      }

      /// <summary>A new picture of this kind of button (null only if the built-in art is broken, which a unit test checks for; the button then simply has no picture, as before).</summary>
      public static IPixelViewModel Create(ShortcutKind kind) {
         var sprite = Decode(kind);
         if (sprite == null) return null;
         return new ReadonlyPixelViewModel(sprite.Width, sprite.Height, (short[])sprite.Pixels.Clone(), Transparent);
      }

      /// <summary>The picture of the button that goes to <paramref name="gotoAnchor"/> and says <paramref name="displayText"/>.</summary>
      public static IPixelViewModel For(string gotoAnchor, string displayText) => Create(Identify(gotoAnchor, displayText));

      /// <summary>
      /// Gives the button its built-in picture for the case that nothing else gives it one. Nothing is drawn now: the picture is made when the button is first shown
      /// and only if the button still has no picture then (its own picture from the ROM, found when the button is shown, wins).
      /// </summary>
      public static void Attach(GotoShortcutViewModel shortcut, string gotoAnchor) {
         if (shortcut == null) return;
         var text = shortcut.DisplayText;
         shortcut.SetFallbackImage(() => For(gotoAnchor, text));
      }

      private static Sprite Decode(ShortcutKind kind) {
         lock (cacheLock) {
            if (decoded.TryGetValue(kind, out var cached)) return cached;
            Sprite sprite = null;
            try {
               var (legend, rows) = ArtFor(kind);
               sprite = Parse(legend, rows);
            } catch (Exception) {
               sprite = null; // broken art is a mistake in this file, never a reason to fail the dialog
            }
            decoded[kind] = sprite;
            return sprite;
         }
      }

      /// <summary>Turns the text rows and the legend ("k=1c1c48 m=4890f0") into GBA colours. Throws if a row is the wrong length or a character has no colour (the tests make sure none does).</summary>
      public static (int width, int height, short[] pixels) ParseArt(string legend, IReadOnlyList<string> rows) {
         var sprite = Parse(legend, rows);
         return (sprite.Width, sprite.Height, sprite.Pixels);
      }

      private static Sprite Parse(string legend, IReadOnlyList<string> rows) {
         var colors = new Dictionary<char, short>();
         foreach (var entry in legend.Split(' ', StringSplitOptions.RemoveEmptyEntries)) {
            if (entry.Length != 8 || entry[1] != '=') throw new FormatException($"bad legend entry '{entry}'");
            int rgb = int.Parse(entry.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            colors[entry[0]] = UncompressedPaletteColor.Pack(((rgb >> 16) & 0xFF) >> 3, ((rgb >> 8) & 0xFF) >> 3, (rgb & 0xFF) >> 3);
         }
         int height = rows.Count, width = height == 0 ? 0 : rows[0].Length;
         if (width == 0) throw new FormatException("no pixels");
         var pixels = new short[width * height];
         for (int y = 0; y < height; y++) {
            if (rows[y].Length != width) throw new FormatException($"row {y} has {rows[y].Length} pixels, not {width}");
            for (int x = 0; x < width; x++) {
               var c = rows[y][x];
               if (c == '.') pixels[y * width + x] = Transparent;
               else if (colors.TryGetValue(c, out var color)) pixels[y * width + x] = color;
               else throw new FormatException($"'{c}' (row {y}) is not in the legend");
            }
         }
         return new Sprite(width, height, pixels);
      }

      #endregion
   }
}
