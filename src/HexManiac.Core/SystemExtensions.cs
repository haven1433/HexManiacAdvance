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
      public static void Sort<T>(this List<T> list, Func<T,T,int> compare) {
         list.Sort(new StubComparer<T> { Compare = compare });
      }
   }
}
