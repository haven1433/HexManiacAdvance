using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace HavenSoft.HexManiac.Core {
   public static class SystemExtensions {

      ////// Random utility functions on basic types, mostly added to make code easier to read. //////

      private static readonly IReadOnlyList<string> byteToString = Enumerable.Range(0, 0x100).Select(i => i.ToString("X2")).ToArray();
      public static string ToHexString(this byte value) => byteToString[value];

      public static T LimitToRange<T>(this T value, T lower, T upper) where T : IComparable<T> {
         if (upper.CompareTo(lower) < 0) throw new ArgumentException($"upper value {upper} is less than lower value {lower}");
         if (value.CompareTo(lower) < 0) return lower;
         if (upper.CompareTo(value) < 0) return upper;
         return value;
      }

      public static IEnumerable<T> Until<T>(this IEnumerable<T> list, Func<T, bool> func) {
         foreach (var element in list) {
            if (func(element)) break;
            yield return element;
         }
      }

      public static void Sort<T>(this List<T> list, Func<T, T, int> compare) {
         list.Sort(new StubComparer<T> { Compare = compare });
      }

      public static void AddRange<T>(this HashSet<T> set, IEnumerable<T> items) {
         foreach (var item in items) set.Add(item);
      }

      public static int Count<T>(this IEnumerable<T> list, T c) where T : struct => list.Count(ch => ch.Equals(c));

      public static int IndexOf<T>(this IReadOnlyList<T> list, T element) where T : class {
         for (int i = 0; i < list.Count; i++) {
            if (list[i].Equals(element)) return i;
         }
         return -1;
      }

      public static int IndexOf<T>(this T[] list, T element) where T : IEquatable<T> {
         for (int i = 0; i < list.Length; i++) {
            if (list[i].Equals(element)) return i;
         }
         return -1;
      }

      public static string[] Split(this string self, string token) => self.Split(new[] { token }, StringSplitOptions.None);

      public static bool IsAny<T>(this T self, params T[] options) {
         Debug.Assert(self is IEquatable<T> || self is Enum);
         return options.Contains(self);
      }

      // There are times where we want to do some sort of action on an object fluently: do the action, but return the same object.
      // For example, we might want to run a method or add an event after a property initializer, without needing to name the instance.
      public static T Fluent<T>(this T self, Action<T> action) {
         action(self);
         return self;
      }

      ////// these are some specific string extensions to deal with smart auto-complete //////

      public static bool MatchesPartial(this string full, string partial) {
         foreach (var character in partial) {
            var index = full.IndexOf(character.ToString(), StringComparison.CurrentCultureIgnoreCase);
            if (index == -1) return false;
            full = full.Substring(index + 1);
         }

         return true;
      }
      public static int IndexOfPartial(this IReadOnlyList<string> names, string input) {
         // perfect match first
         var matchIndex = names.IndexOf(input);
         if (matchIndex != -1) return matchIndex;

         // no perfect match found. How about a partial match?
         var match = names.FirstOrDefault(name => name.Contains(input));
         if (match != null) names.IndexOf(match);

         for (var i = 0; i < names.Count; i++) {
            if (names[i].MatchesPartial(input)) return i;
         }

         return -1;
      }
   }
}
