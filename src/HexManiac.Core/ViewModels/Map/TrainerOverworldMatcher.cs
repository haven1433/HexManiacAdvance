using System;
using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.HexManiac.Core.ViewModels.Map {
   /// <summary>
   /// Finds the overworld sprite that goes with a trainer picture, using only the names of the two lists.
   /// It never guesses: the answer comes from an explicit table or from two names that are the same
   /// (once the class prefix, the FRLG suffix and punctuation are ignored). Everything else is "no match" (-1),
   /// and the caller keeps whatever overworld sprite is already selected.
   /// </summary>
   public static class TrainerOverworldMatcher {
      private static readonly string[] IgnoredNamePrefixes = { "TRAINER_PIC_", "OBJ_EVENT_GFX_" };

      // These class prefixes only say what the person does. The overworld sprite is named after the person.
      private static readonly string[] ClassPrefixes = {
         "AQUA_LEADER_", "MAGMA_LEADER_", "ELITE_FOUR_", "CHAMPION_", "LEADER_",
         "SALON_MAIDEN_", "DOME_ACE_", "PALACE_MAVEN_", "ARENA_TYCOON_", "FACTORY_HEAD_", "PIKE_QUEEN_", "PYRAMID_KING_",
      };

      private const string FrlgSuffix = "_FRLG";

      /// <summary>
      /// Trainer pictures whose overworld sprite has a different name. The first name that exists in the overworld list wins.
      /// This table is checked before the name comparison, so it also decides when the same name would give the wrong style
      /// (the Emerald cooltrainer wears the MAN_3 sprite, not the FireRed COOLTRAINER_M one).
      /// </summary>
      private static readonly Dictionary<string, string[]> Curated = new() {
         // the two teams: admins wear the same clothes as the grunts, the leaders have their own sprite
         ["AQUA_GRUNT_M"] = new[] { "AQUA_MEMBER_M" },
         ["AQUA_GRUNT_F"] = new[] { "AQUA_MEMBER_F" },
         ["AQUA_ADMIN_M"] = new[] { "AQUA_MEMBER_M" },
         ["AQUA_ADMIN_F"] = new[] { "AQUA_MEMBER_F" },
         ["MAGMA_GRUNT_M"] = new[] { "MAGMA_MEMBER_M" },
         ["MAGMA_GRUNT_F"] = new[] { "MAGMA_MEMBER_F" },
         ["MAGMA_ADMIN"] = new[] { "MAGMA_MEMBER_M" },
         ["AQUA_LEADER_ARCHIE"] = new[] { "ARCHIE" },
         ["MAGMA_LEADER_MAXIE"] = new[] { "MAXIE" },

         // people whose picture and sprite are named differently
         ["BRENDAN"] = new[] { "RIVAL_BRENDAN_NORMAL" },
         ["MAY"] = new[] { "RIVAL_MAY_NORMAL" },
         ["RS_BRENDAN"] = new[] { "LINK_RS_BRENDAN" },
         ["RS_MAY"] = new[] { "LINK_RS_MAY" },
         ["TWINS"] = new[] { "TWIN" },
         ["TWINS_FRLG"] = new[] { "TWIN" },
         ["LEADER_TATE_AND_LIZA"] = new[] { "TATE", "LIZA" },
         ["PROFESSOR_OAK_FRLG"] = new[] { "PROF_OAK" },
         ["RIVAL_EARLY_FRLG"] = new[] { "BLUE" },
         ["RIVAL_LATE_FRLG"] = new[] { "BLUE" },
         ["CHAMPION_RIVAL_FRLG"] = new[] { "BLUE" },
         ["ROCKET_GRUNT_M_FRLG"] = new[] { "ROCKET_M" },
         ["ROCKET_GRUNT_F_FRLG"] = new[] { "ROCKET_F" },
         ["FISHERMAN_FRLG"] = new[] { "FISHER" },
         ["SWIMMER_M_FRLG"] = new[] { "SWIMMER_M_WATER" },
         ["SWIMMER_F_FRLG"] = new[] { "SWIMMER_F_WATER" },

         // classes without an overworld sprite of their own: every event in the Emerald map data that uses
         // the picture uses this sprite (pictures that the game splits over two sprites are left out on purpose)
         ["COOLTRAINER_M"] = new[] { "MAN_3" },
         ["COOLTRAINER_F"] = new[] { "WOMAN_5" },
         ["DRAGON_TAMER"] = new[] { "MAN_3" },
         ["BIRD_KEEPER"] = new[] { "MAN_5" },
         ["GUITARIST"] = new[] { "MAN_5" },
         ["KINDLER"] = new[] { "MAN_5" },
         ["POKEMON_BREEDER_M"] = new[] { "MAN_4" },
         ["POKEMON_BREEDER_F"] = new[] { "WOMAN_2" },
         ["AROMA_LADY"] = new[] { "WOMAN_2" },
         ["LADY"] = new[] { "WOMAN_2" },
         ["COLLECTOR"] = new[] { "MANIAC" },
         ["POKEMANIAC"] = new[] { "MANIAC" },
         ["BUG_MANIAC"] = new[] { "MANIAC" },
         ["RUIN_MANIAC"] = new[] { "HIKER" },
         ["PSYCHIC_F"] = new[] { "LASS" },
         ["SR_AND_JR"] = new[] { "LASS" },
         ["SCHOOL_KID_F"] = new[] { "GIRL_3" },
         ["BATTLE_GIRL"] = new[] { "GIRL_3" },
         ["SWIMMING_TRIATHLETE_M"] = new[] { "SWIMMER_M" },
         ["SWIMMING_TRIATHLETE_F"] = new[] { "SWIMMER_F" },
         ["POKEMON_RANGER_M"] = new[] { "CAMPER" },
         ["POKEMON_RANGER_F"] = new[] { "PICNICKER" },
      };

      /// <summary>True if at least one name is a real name. Index-only lists ("0", "1", ...) can't be matched by name.</summary>
      public static bool HasNames(IReadOnlyList<string> names) => names != null && names.Any(name => Key(name) != null);

      /// <summary>
      /// The index of the overworld sprite that goes with the trainer picture, or -1 if there is no sprite that clearly goes with it.
      /// </summary>
      /// <param name="trainerPicName">The name of the trainer picture, like AQUA_GRUNT_M or TRAINER_PIC_AQUA_GRUNT_M.</param>
      /// <param name="overworldNames">The names of the overworld sprites, in table order. Entries may be null.</param>
      public static int Find(string trainerPicName, IReadOnlyList<string> overworldNames) {
         if (overworldNames == null) return -1;
         var picName = Normalize(trainerPicName);
         if (Key(picName) == null) return -1;

         var firstWithKey = new Dictionary<string, int>();
         for (int i = 0; i < overworldNames.Count; i++) {
            var key = Key(overworldNames[i]);
            if (key != null) firstWithKey.TryAdd(key, i);
         }

         foreach (var candidate in GetCandidates(picName)) {
            var key = Key(candidate);
            if (key != null && firstWithKey.TryGetValue(key, out var index)) return index;
         }
         return -1;
      }

      /// <summary>For each trainer picture, the index of its overworld sprite or -1.</summary>
      public static IReadOnlyList<int> BuildMapping(IReadOnlyList<string> trainerPicNames, IReadOnlyList<string> overworldNames) {
         var results = new int[trainerPicNames.Count];
         for (int i = 0; i < results.Length; i++) results[i] = Find(trainerPicNames[i], overworldNames);
         return results;
      }

      /// <summary>The overworld sprite names that would be accepted for a picture, best first.</summary>
      public static IEnumerable<string> GetCandidates(string trainerPicName) {
         var name = Normalize(trainerPicName);
         if (name == null) yield break;

         if (Curated.TryGetValue(name, out var curated)) {
            foreach (var candidate in curated) yield return candidate;
         }

         yield return name;

         var withoutPrefix = name;
         foreach (var prefix in ClassPrefixes) {
            if (!name.StartsWith(prefix, StringComparison.Ordinal)) continue;
            withoutPrefix = name.Substring(prefix.Length);
            break;
         }
         var hasPrefix = withoutPrefix != name;
         var hasSuffix = name.EndsWith(FrlgSuffix, StringComparison.Ordinal);

         if (hasPrefix) yield return withoutPrefix;
         if (hasSuffix) yield return name.Substring(0, name.Length - FrlgSuffix.Length);
         if (hasPrefix && hasSuffix && withoutPrefix.EndsWith(FrlgSuffix, StringComparison.Ordinal)) {
            yield return withoutPrefix.Substring(0, withoutPrefix.Length - FrlgSuffix.Length);
         }
      }

      /// <summary>Upper case with underscores, and without the prefix that the decomp puts in front of every constant.</summary>
      private static string Normalize(string name) {
         if (name == null) return null;
         name = name.Trim().Trim('"').Trim().ToUpperInvariant().Replace(' ', '_').Replace('-', '_');
         foreach (var prefix in IgnoredNamePrefixes) {
            if (name.StartsWith(prefix, StringComparison.Ordinal)) name = name.Substring(prefix.Length);
         }
         return name;
      }

      /// <summary>Letters and digits only, so POKEMANIAC and POKE_MANIAC are the same name. Null if there are no letters (an index is not a name).</summary>
      private static string Key(string name) {
         name = Normalize(name);
         if (string.IsNullOrEmpty(name)) return null;
         var key = new string(name.Where(char.IsLetterOrDigit).ToArray());
         return key.Any(char.IsLetter) ? key : null;
      }
   }
}
