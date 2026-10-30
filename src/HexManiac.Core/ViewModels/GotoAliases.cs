using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.HexManiac.Core.ViewModels {
   /// <summary>
   /// Goto entries of the original games that a decomp/expansion ROM (CUBE) stores somewhere else.
   /// The expansion merged many of the vanilla tables into bigger ones: the Pokemon front sprites are the 'frontPic' column of data.pokemon.stats,
   /// the move descriptions are a column of data.pokemon.moves.stats, and so on. Without help, 'graphics > pokemon > sprites > front' would just be gone from the Goto menu.
   ///
   /// An alias only exists in a ROM where its own name does not resolve (so it never changes a vanilla game, where every original name is a real anchor)
   /// and one of its targets does. A target is an anchor path ('table/element/field'), the first one that resolves is used.
   /// </summary>
   public static class GotoAliases {
      public record Alias(string Name, IReadOnlyList<string> Targets, string Note, Func<IDataModel, bool> Condition = null);

      private static Alias A(string name, string note, params string[] targets) => new(name, targets, note);

      private const string Species = "data.pokemon.stats", Moves = "data.pokemon.moves.stats", Items = "data.items.stats", TrainerPics = "data.trainers.sprites", TypeSheet = "graphics.pokemon.type.palettes+60";

      /// <summary>Every original Goto entry that has an equivalent in the expansion's data, with where to find it.</summary>
      public static IReadOnlyList<Alias> Known { get; } = new[] {
         // CUBE scripts (Utilities > Scripts): the EXP All percentage only matters while the script is on, so its entry only exists then. The Level Cap table is the same: it only appears while the Level cap script is on.
         new Alias("scripts.expshare", new[] { CubeScripts.ConfigAnchor + "+1" }, "the percentage of EXP that Pokemon get when EXP All is on (0-100)", model => CubeScripts.IsOn(model, CubeScripts.ExpAllIndex)),
         new Alias("scripts.levelcap", new[] { CubeScripts.LevelCapAnchor }, "level caps: a cap applies while its flag is SET, and the highest level among the set flags wins", model => CubeScripts.IsOn(model, CubeScripts.LevelCapIndex)),

         // abilities
         A("data.abilities.descriptions", "the description column of the ability table", "data.abilities.names/0/description", "data.abilities.names"),
         A("data.abilities.pickup.items", "Pickup is one table (item + chance for every 10 levels)", "data.items.pickup"),
         A("data.abilities.pickup.rareitems", "Pickup is one table (item + chance for every 10 levels)", "data.items.pickup"),
         A("data.abilities.pickup.probabilities", "the chance columns of the Pickup table", "data.items.pickup/0/lvl1", "data.items.pickup"),
         A("data.abilities.soundproof.moves", "Soundproof reads the soundMove flag of each move", Moves + "/0/flags1", Moves),

         // pokedex
         A("data.pokedex.national", "the dexNumber column of the species table", Species + "/0/dexNumber", Species),
         A("data.pokedex.regional", "the dexNumber column of the species table (the Hoenn order is data.pokedex.hoennToNational)", Species + "/0/dexNumber", "data.pokedex.hoennToNational", Species),
         A("data.pokedex.stats", "category, height, weight, description and scales are columns of the species table", Species + "/0/category", Species),

         // pokemon
         A("data.pokemon.names", "the name column of the species table", Species + "/0/name", Species),
         A("data.pokemon.evolutions", "the evolutions column of the species table", Species + "/0/evolutions", Species),
         A("data.pokemon.deoxys.stats", "every Deoxys form is a species with its own base stats", Species + "/DEOXYS", Species),
         A("data.pokemon.frontier.banned", "the flags column (isFrontierBanned) of the species table", Species + "/0/flags", Species),
         A("data.pokemon.moves.levelup", "the levelUpMoves column of the species table", Species + "/0/levelUpMoves", Species),
         A("data.pokemon.moves.egg", "the eggMoves column of the species table", Species + "/0/eggMoves", Species),
         A("data.pokemon.moves.tmcompatibility", "the teachableMoves column of the species table", Species + "/0/teachableMoves", Species),
         A("data.pokemon.moves.tutorcompatibility", "the teachableMoves column of the species table", Species + "/0/teachableMoves", Species),
         A("data.pokemon.moves.hms", "HM moves are the field moves (and the HM entries of the TM list)", "data.pokemon.fieldmoves", "data.pokemon.moves.tms"),
         A("data.pokemon.natures.stats", "the statUp / statDown columns of the nature table", "data.pokemon.natures.names/0/statUp", "data.pokemon.natures.names"),
         A("data.pokemon.contest.descriptions", "the description column of the contest effect table", "data.pokemon.contest.effects/0/description", "data.pokemon.contest.effects"),
         A("data.pokemon.contest.thumb", "the function column of the contest effect table", "data.pokemon.contest.effects/0/function", "data.pokemon.contest.effects"),
         A("data.pokemon.type.holdEffects", "the type of a type-boosting item is its hold effect parameter", Items + "/0/holdeffect", Items),
         A("data.pokemon.swarm", "swarms are the outbreak table", "data.pokemon.outbreaks"),

         // moves
         A("data.pokemon.moves.names", "the name column of the move table", Moves + "/0/name", Moves),
         A("data.pokemon.moves.descriptions", "the description column of the move table", Moves + "/0/description", Moves),
         A("data.pokemon.moves.stats.battle", "the battle columns of the move table", Moves + "/0/effect", Moves),
         A("data.pokemon.moves.stats.contest", "the contest columns of the move table", Moves + "/0/contestEffect", Moves),
         A("data.pokemon.moves.details.fallback.names", "the generic column of the type table", "data.pokemon.type.names/0/generic", "data.pokemon.type.names"),
         A("data.pokemon.moves.details.mimic.metronome.forbidden", "the move flags (mimicBanned / metronomeBanned)", Moves + "/0/flags3", Moves),
         A("data.pokemon.moves.details.singing", "the move flags (soundMove)", Moves + "/0/flags1", Moves),

         // trainers and the frontier
         A("data.trainers.money", "the money column of the trainer class table", "data.trainers.classes.names/0/money", "data.trainers.classes.names"),
         A("data.trainers.multibattle.steven.team", "Steven's multi battle team is one of the battle partners", "data.trainers.partners"),
         A("scripts.battle.ai.trainer", "the AI is chosen by the aiFlags column of the trainer table", "data.trainers.stats/0/aiFlags", "data.trainers.stats"),
         A("data.battlefrontier.items", "every Frontier Pokemon has its own held item", "data.battlefrontier.pokemon/0/item", "data.battlefrontier.pokemon"),

         // sound
         A("sound.pokemon.cry.hoennconversion", "cries are indexed by species: the cry column of the species table", Species + "/0/cry", Species),

         // pokemon graphics
         A("graphics.pokemon.sprites.front", "the frontPic column of the species table", Species + "/0/frontPic", Species),
         A("graphics.pokemon.sprites.back", "the backPic column of the species table", Species + "/0/backPic", Species),
         A("graphics.pokemon.palettes.normal", "the palette column of the species table", Species + "/0/palette", Species),
         A("graphics.pokemon.palettes.shiny", "the shinyPalette column of the species table", Species + "/0/shinyPalette", Species),
         A("graphics.pokemon.icons.sprites", "the icon column of the species table", Species + "/0/icon", Species),
         A("graphics.pokemon.icons.deoxys", "every Deoxys form is a species with its own icon", Species + "/DEOXYS/icon", Species),
         A("graphics.pokemon.sprites.footprint", "the footprint column of the species table", Species + "/0/footprint", Species),
         A("graphics.pokemon.sprites.coordinates.front", "the frontPicSize / frontPicYOffset columns of the species table", Species + "/0/frontPicSize", Species),
         A("graphics.pokemon.sprites.coordinates.back", "the backPicSize / backPicYOffset columns of the species table", Species + "/0/backPicSize", Species),
         A("graphics.pokemon.sprites.elevation", "the enemyMonElevation column of the species table", Species + "/0/enemyMonElevation", Species),
         A("graphics.pokemon.castform.sprite.coordinates.front", "every Castform form is a species with its own picture size", Species + "/CASTFORM/frontPicSize", Species),
         A("graphics.pokemon.castform.sprite.coordinates.back", "every Castform form is a species with its own picture size", Species + "/CASTFORM/backPicSize", Species),
         A("graphics.pokemon.castform.sprite.elevations", "every Castform form is a species with its own elevation", Species + "/CASTFORM/enemyMonElevation", Species),
         A("graphics.pokemon.animations.front", "the frontAnimId column of the species table", Species + "/0/frontAnimId", Species),
         A("graphics.pokemon.animations.back", "the backAnimId column of the species table", Species + "/0/backAnimId", Species),
         A("graphics.pokemon.animations.frames", "the frontAnimFrames column of the species table", Species + "/0/frontAnimFrames", Species),
         A("graphics.pokemon.moves.animations", "the battleAnimScript column of the move table", Moves + "/0/battleAnimScript", Moves),
         A("graphics.pokemon.type.index", "the palette column of the type table", "data.pokemon.type.names/0/palette", "data.pokemon.type.names"),

         // item graphics
         A("graphics.items.sprites", "the icon / palette columns of the item table", Items + "/0/icon", Items),
         A("graphics.bag.berry.sprites", "the pic / palette columns of the berry table", "data.items.berry.stats/0/pic", "data.items.berry.stats"),

         // trainer graphics: the front/back pictures, palettes, animations and coordinates of a trainer class are one struct
         A("graphics.trainers.sprites.front", "the front part of the trainer picture table", TrainerPics + "/0/front", TrainerPics),
         A("graphics.trainers.palettes.front", "the front part of the trainer picture table", TrainerPics + "/0/front", TrainerPics),
         A("graphics.trainers.coordinates.front", "the front part of the trainer picture table", TrainerPics + "/0/front", TrainerPics),
         A("graphics.trainers.animations.frames", "the front part of the trainer picture table", TrainerPics + "/0/front", TrainerPics),
         A("graphics.trainers.sprites.back.enter", "the back part of the trainer picture table", TrainerPics + "/0/back", TrainerPics),
         A("graphics.trainers.palettes.back", "the back part of the trainer picture table", TrainerPics + "/0/back", TrainerPics),
         A("graphics.trainers.animations.back", "the back part of the trainer picture table", TrainerPics + "/0/back", TrainerPics),

         // type icons: gMoveTypes_Gfx (the icon sheet, .smol compressed: HMA cannot draw it) follows the 96 byte gMoveTypes_Pal that graphics.pokemon.type.palettes describes
         A("graphics.pokemon.type.icons", "the icon sheet (smol compressed) right behind the type palettes", TypeSheet),
         A("graphics.pokemon.type.map", "the icon sheet (smol compressed) right behind the type palettes", TypeSheet),
         A("graphics.pokemon.bag.type.icon", "the bag uses the same icon sheet as the summary screen", TypeSheet),
         A("graphics.pokemon.bag.type.map", "the bag uses the same icon sheet as the summary screen", TypeSheet),

         // move animations: the expansion's particle table holds the sprite sheet and its palette together
         A("graphics.moves.particles.palettes", "the palette column of the particle sprite table", "graphics.moves.particles.sprites/0/palette", "graphics.moves.particles.sprites"),

         // overworld
         A("graphics.overworld.sprites2", "the field effect sprite templates", "graphics.overworld.fieldeffects"),
      };

      private static readonly Dictionary<string, Alias> byName = Known.ToDictionary(alias => alias.Name, StringComparer.OrdinalIgnoreCase);

      /// <summary>A path may end with '+hex' to mean "that many bytes after the data the path leads to".</summary>
      private static int Resolve(IDataModel model, string path) {
         try {
            var plus = path.LastIndexOf('+');
            if (plus > 0 && int.TryParse(path.Substring(plus + 1), System.Globalization.NumberStyles.HexNumber, null, out var offset)) {
               var baseAddress = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, path.Substring(0, plus));
               return baseAddress == Pointer.NULL ? Pointer.NULL : baseAddress + offset;
            }
            return model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, path);
         } catch (Exception) {
            return Pointer.NULL; // an odd table must not break the Goto menu
         }
      }

      /// <summary>True if the name is a real anchor or matched word of this ROM: then it never needs an alias.</summary>
      private static bool IsReal(IDataModel model, string name) => Resolve(model, name) != Pointer.NULL || model.GetMatchedWords(name).Count > 0;

      /// <summary>The target path an alias leads to in this ROM, or null if the name is not an alias here (it is a real anchor, or nothing in this ROM matches it).</summary>
      public static string FindTarget(IDataModel model, string name) {
         if (model == null || string.IsNullOrEmpty(name)) return null;
         if (!byName.TryGetValue(name.Trim(), out var alias)) return null;
         if (IsReal(model, alias.Name)) return null;
         if (alias.Condition != null) {
            try { if (!alias.Condition(model)) return null; } catch (Exception) { return null; }
         }
         foreach (var target in alias.Targets) {
            if (Resolve(model, target) != Pointer.NULL) return target;
         }
         return null;
      }

      /// <summary>The address an alias leads to in this ROM (Pointer.NULL if the name is not an alias here).</summary>
      public static int FindAddress(IDataModel model, string name) {
         var target = FindTarget(model, name);
         return target == null ? Pointer.NULL : Resolve(model, target);
      }

      /// <summary>The alias names that lead somewhere in this ROM (empty for an original game: there every name is real).</summary>
      public static IReadOnlyList<string> Available(IDataModel model) {
         if (model == null) return Array.Empty<string>();
         var result = new List<string>();
         foreach (var alias in Known) {
            if (FindTarget(model, alias.Name) != null) result.Add(alias.Name);
         }
         return result;
      }

      /// <summary>The available alias names that the Goto text matches (same matching as the anchor names).</summary>
      public static IReadOnlyList<string> Matching(IDataModel model, string text) {
         var available = Available(model);
         if (available.Count == 0) return available;
         text = (text ?? string.Empty).Replace(" ", string.Empty).ToLower();
         if (text.Contains(ArrayRun.ArrayAnchorSeparator)) return Array.Empty<string>();
         if (text.Length == 0) return available;
         return text.FindMatches(available).Select(index => available[index]).ToList();
      }
   }
}
