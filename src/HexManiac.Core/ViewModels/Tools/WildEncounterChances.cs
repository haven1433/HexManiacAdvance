using System;
using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   /// <summary>
   /// Which wild encounter slot is picked is not stored in the ROM: the game decides it from the slot number alone,
   /// with the same fixed odds in every map (ENCOUNTER_CHANCE_* in the decomp projects, hard-coded in the original games).
   /// The table tool uses these to tell you how common each row of an encounter list is.
   /// </summary>
   public static class WildEncounterChances {
      public enum Area { Land, Water, RockSmash, Fishing, Hidden }

      /// <summary>Walking in grass, caves, ...: 12 slots.</summary>
      public static readonly IReadOnlyList<int> Land = new[] { 20, 20, 10, 10, 10, 10, 5, 5, 4, 4, 1, 1 };

      /// <summary>Surfing: 5 slots.</summary>
      public static readonly IReadOnlyList<int> Water = new[] { 60, 30, 5, 4, 1 };

      /// <summary>Smashing rocks: 5 slots, the same odds as surfing.</summary>
      public static readonly IReadOnlyList<int> RockSmash = new[] { 60, 30, 5, 4, 1 };

      /// <summary>Fishing: 10 slots. Every rod rolls only its own slots: the old rod 0-1, the good rod 2-4, the super rod 5-9. So each rod's odds add up to 100.</summary>
      public static readonly IReadOnlyList<int> Fishing = new[] { 70, 30, 60, 20, 20, 40, 40, 15, 4, 1 };

      /// <summary>Where each rod's slots start in the fishing list.</summary>
      public static readonly IReadOnlyList<(int Start, string Name)> FishingGroups = new[] { (0, "Old rod"), (2, "Good rod"), (5, "Super rod") };

      public const int HiddenSlotCount = 3;

      /// <summary>
      /// Works out which area an encounter list is from the name of the field that points to it ("morningGrass", "daySurf", "fish", ...).
      /// Returns false if the name doesn't say. Names are matched loosely (case-insensitive, by the word inside).
      /// </summary>
      public static bool TryGetArea(string fieldName, out Area area) {
         area = Area.Land;
         if (string.IsNullOrWhiteSpace(fieldName)) return false;
         var name = fieldName.ToLowerInvariant();
         if (name.Contains("fish") || name.Contains("rod")) { area = Area.Fishing; return true; }
         if (name.Contains("hidden")) { area = Area.Hidden; return true; }
         if (name.Contains("surf") || name.Contains("water")) { area = Area.Water; return true; }
         if (name.Contains("tree") || name.Contains("rock") || name.Contains("smash")) { area = Area.RockSmash; return true; }
         if (name.Contains("grass") || name.Contains("land") || name.Contains("walk")) { area = Area.Land; return true; }
         return false;
      }

      /// <summary>
      /// The odds (in percent) of each slot of an encounter list, if they are known.
      /// <paramref name="areaName"/> is the name of the field that points to the list; it may be empty or unknown,
      /// in which case the number of slots decides (12 can only be a land list, 10 only fishing, 5 water or rock smash, which share their odds).
      /// A list whose size doesn't match its area (or whose area has no fixed odds, like the hidden Pokémon of the DexNav) gets nothing.
      /// </summary>
      public static bool TryGetChances(string areaName, int slotCount, out IReadOnlyList<int> chances, out IReadOnlyList<(int Start, string Name)> groups) {
         chances = null;
         groups = Array.Empty<(int, string)>();
         if (TryGetArea(areaName, out var area)) {
            var known = Get(area);
            if (known == null || known.Count != slotCount) return false;
            chances = known;
         } else {
            chances = slotCount switch {
               12 => Land,
               10 => Fishing,
               5 => Water,
               _ => null,
            };
            if (chances == null) return false;
            area = slotCount == 12 ? Area.Land : slotCount == 10 ? Area.Fishing : Area.Water;
         }
         if (area == Area.Fishing) groups = FishingGroups;
         return true;
      }

      private static IReadOnlyList<int> Get(Area area) => area switch {
         Area.Land => Land,
         Area.Water => Water,
         Area.RockSmash => RockSmash,
         Area.Fishing => Fishing,
         _ => null, // hidden: the game has no fixed odds for these
      };

      /// <summary>"20%"</summary>
      public static string Format(int percent) => percent + "%";

      /// <summary>
      /// The format of some tables comments a row with its odds ("20%"). Those comments say the same as the chance column, so they are not shown a second time.
      /// </summary>
      public static bool IsPercentComment(string comment) {
         if (string.IsNullOrWhiteSpace(comment)) return false;
         var text = comment.Trim();
         if (!text.EndsWith("%")) return false;
         text = text.Substring(0, text.Length - 1).Trim();
         return text.Length > 0 && text.All(c => char.IsDigit(c) || c == '.');
      }
   }
}
