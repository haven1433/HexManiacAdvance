using System;
using System.Collections.Generic;

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
      public static void AddRange<T>(this HashSet<T> set, IEnumerable<T> items) {
         foreach (var item in items) set.Add(item);
      }
   }
}
