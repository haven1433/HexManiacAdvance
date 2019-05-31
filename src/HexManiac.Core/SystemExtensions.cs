using System;
using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.HexManiac.Core {
   public static class SystemExtensions {
      public static T LimitToRange<T>(this T value, T lower, T upper) where T : IComparable<T> {
         if (upper.CompareTo(lower) < 0) throw new ArgumentException($"upper value {upper} is less than lower value {lower}");
         if (value.CompareTo(lower) < 0) return lower;
         if (upper.CompareTo(value) < 0) return upper;
         return value;
      }
      public static void Sort<T>(this List<T> list, Func<T, T, int> compare) {
         list.Sort(new StubComparer<T> { Compare = compare });
      }
      public static bool MatchesPartial(this string full, string partial) {
         foreach (var character in partial) {
            var index = full.IndexOf(character.ToString(), StringComparison.CurrentCultureIgnoreCase);
            if (index == -1) return false;
            full = full.Substring(index);
         }

         return true;
      }
      public static int IndexOfPartial(this IList<string> names, string input) {
         // perfect match first
         var matchIndex = names.IndexOf(input);
         if (matchIndex != -1) return matchIndex;

         // no perfect match found. How about a partial match?
         var match = names.FirstOrDefault(name => name.Contains(input));
         if (match == null) return -1;
         return names.IndexOf(match);
      }
      public static void AddRange<T>(this HashSet<T> set, IEnumerable<T> items) {
         foreach (var item in items) set.Add(item);
      }
      public static int Count<T>(this IEnumerable<T> list, T c) where T : struct => list.Count(ch => ch.Equals(c));
   }
}
