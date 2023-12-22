using HavenSoft.HexManiac.Core.Models.Code;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core {
   public static class SystemExtensions {

      ////// Random utility functions on basic types, mostly added to make code easier to read. //////

      private static readonly IReadOnlyList<string> byteToString = 0x100.Range().Select(i => i.ToString("X2")).ToArray();
      public static string ToHexString(this byte value) => byteToString[value];

      public static void RaiseCanExecuteChanged(this StubCommand self) => self?.CanExecuteChanged.Invoke(self, EventArgs.Empty);

      public static void Raise(this EventHandler handler, object source) => handler?.Invoke(source, EventArgs.Empty);
      public static void Raise<T>(this EventHandler<T> handler, object source, T args) => handler?.Invoke(source, args);

      public static T LimitToRange<T>(this T value, T lower, T upper) where T : IComparable<T> {
         if (upper.CompareTo(lower) < 0) throw new ArgumentException($"upper value {upper} is less than lower value {lower}");
         if (value.CompareTo(lower) < 0) return lower;
         if (upper.CompareTo(value) < 0) return upper;
         return value;
      }

      /// <summary>
      /// Returns true if min &lt;= value and value &lt; max
      /// </summary>
      public static bool InRange<T>(this T value, T min, T max) where T : IComparable<T> {
         return min.CompareTo(value) <= 0 && value.CompareTo(max) < 0;
      }

      public static bool TryParseInt(this string str, out int result) {
         var negate = str.StartsWith("-");
         if (negate) str = str.Substring(1);
         if (str.StartsWith("0x") && int.TryParse(str.Substring(2), NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result)) return true;
         if (int.TryParse(str, out result)) {
            if (negate) result = -result;
            return true;
         }
         return false;
      }
      public static bool TryParseInt(this ReadOnlySpan<char> str, out int result) {
         if (str.StartsWith("0x") && int.TryParse(str.Slice(2), NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result)) return true;
         if (int.TryParse(str, out result)) return true;
         return false;
      }

      // allows writing 5.Range() instead of Enumerable.Range(0, 5)
      public static IEnumerable<int> Range(this int count) => Enumerable.Range(0, count);

      public static IEnumerable<T> Range<T>(this int count, Func<int, T> selector) => Enumerable.Range(0, count).Select(selector);

      /// <summary>
      /// Returns the number of times each element appears in the sequence.
      /// </summary>
      public static Dictionary<T, int> ToHistogram<T>(this IEnumerable<T> elements) where T : IEquatable<T> {
         var result = new Dictionary<T, int>();
         foreach (var element in elements) {
            if (!result.ContainsKey(element)) result[element] = 0;
            result[element]++;
         }
         return result;
      }

      public static T MostCommonKey<T>(this IDictionary<T, int> histogram) {
         var first = true;
         T best = default;
         var bestCount = 0;
         foreach (var pair in histogram) {
            if (first) {
               best = pair.Key;
               bestCount = pair.Value;
               first = false;
            } else if (pair.Value > bestCount) {
               best = pair.Key;
               bestCount = pair.Value;
            }
         }
         return best;
      }

      /// <summary>
      /// Returns all the elements in a collection until one meets a condition.
      /// Does not include the element that meets the condition.
      /// </summary>
      public static IEnumerable<T> Until<T>(this IEnumerable<T> list, Func<T, bool> func) {
         foreach (var element in list) {
            if (func(element)) break;
            yield return element;
         }
      }

      public static IEnumerable<string> TrimAll(this IEnumerable<string> list) {
         foreach (var item in list) {
            var text = item?.Trim();
            if (!string.IsNullOrEmpty(text)) yield return text;
         }
      }

      public static T FirstOfTypeOrDefault<T>(this IEnumerable list) where T : class {
         foreach (var item in list) {
            if (item is T t) return t;
         }
         return null;
      }

      public static T FirstOfType<T>(this IEnumerable list) where T : class {
         return list.FirstOfTypeOrDefault<T>() ??
            throw new InvalidOperationException($"Enumerable did not contain any {typeof(T)} elements.");
      }

      public static int MatchPairIndex<T>(this ReadOnlySpan<T> collection, T start, T end) where T : IEquatable<T> {
         var started = false;
         var depth = 0;
         int index = -1;
         foreach (var c in collection) {
            index += 1;
            if (!c.Equals(start) && !c.Equals(end)) continue;
            if (c.Equals(start)) {
               started = true;
               depth += 1;
               continue;
            } else {
               if (!started) continue;
               depth -= 1;
               if (depth == 0) return index;
            }
         }
         return -1;
      }

      public static bool IsNullOrEmpty<T>(this IEnumerable<T> list) {
         if (list == null) return true;
         return !list.Any();
      }

      public static void Sort<T>(this List<T> list, Func<T, T, int> compare) {
         list.Sort(new StubComparer<T> { Compare = compare });
      }

      public static void AddRange<T>(this HashSet<T> set, IEnumerable<T> items) {
         foreach (var item in items) set.Add(item);
      }

      public static void AddRange<T>(this ObservableCollection<T> set, IEnumerable<T> items) {
         foreach (var item in items) set.Add(item);
      }

      public static bool All<T>(this ReadOnlySpan<T> span, Func<T,bool> predicate) {
         var match = true;
         for (int i = 0; match && i < span.Length; i++) {
            match = predicate(span[i]);
         }
         return match;
      }

      public static int Count<T>(this IEnumerable<T> list, T c) where T : struct => list.Count(ch => ch.Equals(c));
      public static int Count<T>(this ReadOnlySpan<T> list, T c) where T : struct {
         int count = 0;
         foreach (var ch in list) {
            if (c.Equals(ch)) count++;
         }
         return count;
      }

      public static int IndexOf<T>(this IReadOnlyList<T> list, T element) {
         for (int i = 0; i < list.Count; i++) {
            if (Equals(list[i], element)) return i;
         }
         return -1;
      }

      public static int IndexOf<T>(this T[] list, T element) where T : IEquatable<T> {
         for (int i = 0; i < list.Length; i++) {
            if (list[i].Equals(element)) return i;
         }
         return -1;
      }

      public static void WriteInto<T>(this IReadOnlyList<T> list, T[] array, int index) {
         for (int i = 0; i < list.Count; i++) array[index + i] = list[i];
      }

      public static void AddShort(this IList<byte> data, int value) {
         data.Add((byte)value);
         data.Add((byte)(value >> 8));
      }

      public static string Join(this string separator, IEnumerable<string> elements) => string.Join(separator, elements);

      public static string[] Split(this string self, string token) => self.Split(new[] { token }, StringSplitOptions.None);

      public static string[] SplitLines(this string self) => self.Split(new[] { Environment.NewLine, "\n" }, StringSplitOptions.None);

      public static string[] SplitLast(this string self, char c) {
         var result = new[] { self, string.Empty };
         for (int i = self.Length - 1; i >= 0; i--) {
            if (self[i] != c) continue;
            result[0] = self.Substring(0, i);
            result[1] = self.Substring(i + 1);
            break;
         }
         return result;
      }

      public static string CombineLines(this IReadOnlyList<string> lines) => lines.Aggregate((a, b) => a + Environment.NewLine + b);

      public static string ReplaceOne(this string input, string search, string replacement) {
         var index = input.IndexOf(search);
         if (index == -1) return input;
         return input.Substring(0, index) + replacement + input.Substring(index + search.Length);
      }

      public static StringBuilder TrimEnd(this StringBuilder sb) {
         if (sb == null || sb.Length == 0) return sb;

         int i = sb.Length - 1;

         for (; i >= 0; i--)
            if (!char.IsWhiteSpace(sb[i]))
               break;

         if (i < sb.Length - 1)
            sb.Length = i + 1;

         return sb;
      }

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

      /// <summary>
      /// Returns how many letters within partial can be matched into the full string
      /// </summary>
      public static int MatchLength(this string full, string partial, bool onlyCheckLettersAndDigits = false) {
         int j = 0;
         for (int i = 0; i < partial.Length; i++) {
            if (onlyCheckLettersAndDigits && !char.IsLetterOrDigit(partial[i])) continue;
            var testPartial = char.ToUpperInvariant(partial[i]);
            if (partial[i] == 'é') testPartial = 'E';
            if (partial[i] == 'á') testPartial = 'A';
            while (j < full.Length) {
               var testFull = char.ToUpperInvariant(full[j]);
               if (full[j] == 'é') testFull = 'E';
               if (full[j] == 'á') testFull = 'A';
               j++;
               if (testFull == testPartial) break;
               if (j == full.Length) return i;
            }
            if (j == full.Length) return i + 1;
         }

         return partial.Length;
      }

      public static bool MatchesPartial(this string full, string partial, bool onlyCheckLettersAndDigits = false) {
         if (full == null || partial == null) return false;
         return MatchLength(full, partial, onlyCheckLettersAndDigits) == partial.Length;
      }

      /// <summary>
      /// Measures the amount of 'skipped letters' in a match
      /// </summary>
      public static int SkipCount(this string full, string partial) {
         if (partial.Length < 2) return 0;
         int bestSkipCount = full.Length;

         char compare(char letter) {
            var c = char.ToUpperInvariant(letter);
            if (letter == 'é') c = 'E';
            if (letter == 'á') c = 'A';
            return c;
         }

         var startPartial = compare(partial[0]);
         for (int startPoint = 0; startPoint <= full.Length - partial.Length; startPoint++) {
            if (compare(full[startPoint]) != startPartial) continue;
            int skipCount = 0, k = 1;
            var currentPartial = compare(partial[1]);
            for (int j = startPoint + 1; j < full.Length; j++) {
               if (compare(full[j]) == currentPartial) {
                  k += 1;
                  if (k == partial.Length) break;
                  currentPartial = compare(partial[k]);
               } else {
                  skipCount += 1;
               }
            }
            if (k < partial.Length) continue;
            bestSkipCount = Math.Min(bestSkipCount, skipCount);
            if (bestSkipCount == 0) return 0;
         }
         return bestSkipCount;
      }

      public static int IndexOfPartial(this IReadOnlyList<string> names, string input) {
         // perfect match first
         var matchIndex = names.IndexOf(input);
         if (matchIndex != -1) return matchIndex;

         var partialList = names.Count.Range().Where(i => names[i].MatchesPartial(input)).ToList();
         partialList = ScriptParser.SortOptions(partialList, input, i => names[i]).ToList();
         if (partialList.Count == 0) return -1;
         return partialList[0];
      }

      public static IEnumerable<string> EnumerateOrders(IReadOnlyList<string> parts) {
         if (parts.Count == 1) {
            yield return parts[0];
            yield break;
         }

         foreach (var token in parts) {
            var otherTokens = parts.Except(new[] { token }).ToList();
            foreach (var result in EnumerateOrders(otherTokens)) {
               yield return token + '.' + result;
            }
         }
      }

      public static IEnumerable<T> Except<T>(this IEnumerable<T> collection, params T[] remove) => Enumerable.Except(collection, remove);

      public static bool MatchesPartialWithReordering(this string full, string partial) {
         if (partial.Length == 0) return true;
         if (partial.Contains('.')) return full.MatchesPartial(partial);
         var parts = full.Split('.').ToList();
         while (partial.Length > 0) {
            if (parts.Count == 0) return false;
            int bestMatchIndex = 0, bestMatchValue = parts[0].MatchLength(partial);
            for (int i = 1; i < parts.Count; i++) {
               var matchValue = parts[i].MatchLength(partial);
               if (matchValue <= bestMatchValue) continue;
               (bestMatchIndex, bestMatchValue) = (i, matchValue);
            }
            if (bestMatchValue == 0) return false;
            parts.RemoveAt(bestMatchIndex);
            partial = partial.Substring(bestMatchValue);
         }
         return true;
      }

      public static bool MatchesPartialWithReordering1(this string full, string partial) {
         if (partial.Length == 0) return true;
         var parts = full.Split('.').Where(part => part.Any(partial.Contains)).ToList(); // only bother checking the parts where at least some letter matches
         foreach (var possibleOrder in EnumerateOrders(parts)) {
            if (!possibleOrder.MatchesPartial(partial, true)) continue;
            return true;
         }
         return false;
      }

      // returns a bitfield of all the letters
      public static uint BitLetters(this string token) {
         var result = 0u;
         foreach (var letter in token) {
            if (letter >= 'a' && letter <= 'z') result |= 1u << letter - 'a';
            if (letter >= 'A' && letter <= 'Z') result |= 1u << letter - 'A';
         }
         return result;
      }

      public static string ToAddress(this int address) => address.ToString("X6"); // for debugging

      public static IList<int> FindMatches(this string input, IReadOnlyList<string> options) {
         var result = new List<int>();
         var seekBits = input.BitLetters();
         for (int i = 0; i < options.Count; i++) {
            var includedBits = options[i].BitLetters();
            if ((seekBits & ~includedBits) != 0) continue;
            if (!input.Contains(".")) {
               if (options[i].MatchesPartialWithReordering(input)) result.Add(i);
            } else {
               if (options[i].MatchesPartial(input)) result.Add(i);
            }
         }
         return result;
      }

      public static byte[] ToByteArray(this string content) {
         return content.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(t => (byte)int.Parse(t, NumberStyles.HexNumber)).ToArray();
      }

      public static T From<T>(this Random rnd, IReadOnlyList<T> list) {
         var index = rnd.Next(list.Count);
         return list[index];
      }

      public static T Ensure<T>(this IList<T> list, Func<T, bool> predicate, T element) {
         var existing = list.FirstOrDefault(predicate);
         if (existing != null) return existing;
         list.Add(element);
         return element;
      }

      public static V Ensure<K, V>(this IDictionary<K, V> dict, K key, Func<V> valueFactory) {
         if (dict.TryGetValue(key, out var result)) return result;
         var value = valueFactory();
         dict[key] = value;
         return value;
      }
   }

   public static class NativeProcess {
      /// <summary>
      /// Process.Start works differently in .Net Core compared to .Net Framework.
      /// This wrapper method allows old Process.Start calls to work as expected.
      /// </summary>
      public static void Start(string url) {
         Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
      }

      public static void Start(string url, string arguments) {
         Process.Start(new ProcessStartInfo(url, arguments) { UseShellExecute = true });
      }
   }

   /// <summary>
   /// Represents a simple editable value with change notification.
   /// </summary>
   public class EditableValue<T> : ViewModelCore where T : IEquatable<T> {
      private T field;
      public T Value {
         get => field;
         set => Set(ref field, value);
      }
   }

   public static class DebugDictionaryExtensions {
      public static DebugDictionary<TKey, TValue> Debug<TKey, TValue>(this IDictionary<TKey, TValue> self, TKey watch) => new DebugDictionary<TKey, TValue>(self, watch);
   }

   public class DebugDictionary<TKey, TValue> : DictionaryDecorator<TKey, TValue> {
      private readonly TKey watchKey;
      public DebugDictionary(IDictionary<TKey, TValue> core, TKey watch) => (InnerDictionary, watchKey) = (core, watch);
      public override bool Remove(KeyValuePair<TKey, TValue> item) {
         if (item.Key.Equals(watchKey)) Debugger.Break();
         return base.Remove(item);
      }
      public override bool Remove(TKey key) {
         if (key.Equals(watchKey)) Debugger.Break();
         return base.Remove(key);
      }
   }

   /// <summary>
   /// Original generated by auto-implement.
   /// But had a generation error, so it's included in source.
   /// </summary>
   public class DictionaryDecorator<TKey, TValue> : IDictionary<TKey, TValue> {
      protected IDictionary<TKey, TValue> InnerDictionary { get; set; }
      public virtual bool ContainsKey(TKey key) {
         if (InnerDictionary != null) {
            return InnerDictionary.ContainsKey(key);
         }
         return default;
      }

      public virtual void Add(TKey key, TValue value) {
         if (InnerDictionary != null) {
            InnerDictionary.Add(key, value);
         }
      }

      public virtual bool Remove(TKey key) {
         if (InnerDictionary != null) {
            return InnerDictionary.Remove(key);
         }
         return default;
      }

      public virtual bool TryGetValue(TKey key, out TValue value) {
         value = default;
         if (InnerDictionary != null) {
            return InnerDictionary.TryGetValue(key, out value);
         }
         return default;
      }

      public virtual TValue this[TKey key] {
         get {
            if (InnerDictionary != null) {
               return InnerDictionary[key];
            }
            return default;
         }
         set {
            if (InnerDictionary != null) {
               InnerDictionary[key] = value;
            }
         }
      }
      public virtual ICollection<TKey> Keys {
         get {
            if (InnerDictionary != null) {
               return InnerDictionary.Keys;
            }
            return default;
         }
      }
      public virtual ICollection<TValue> Values {
         get {
            if (InnerDictionary != null) {
               return InnerDictionary.Values;
            }
            return default;
         }
      }
      public virtual void Add(KeyValuePair<TKey, TValue> item) {
         if (InnerDictionary != null) {
            InnerDictionary.Add(item);
         }
      }

      public virtual void Clear() {
         if (InnerDictionary != null) {
            InnerDictionary.Clear();
         }
      }

      public virtual bool Contains(KeyValuePair<TKey, TValue> item) {
         if (InnerDictionary != null) {
            return InnerDictionary.Contains(item);
         }
         return default;
      }

      public virtual void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) {
         if (InnerDictionary != null) {
            InnerDictionary.CopyTo(array, arrayIndex);
         }
      }

      public virtual bool Remove(KeyValuePair<TKey, TValue> item) {
         if (InnerDictionary != null) {
            return InnerDictionary.Remove(item);
         }
         return default;
      }

      public virtual int Count {
         get {
            if (InnerDictionary != null) {
               return InnerDictionary.Count;
            }
            return default;
         }
      }
      public virtual bool IsReadOnly {
         get {
            if (InnerDictionary != null) {
               return InnerDictionary.IsReadOnly;
            }
            return default;
         }
      }
      public virtual IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() {
         if (InnerDictionary != null) {
            return InnerDictionary.GetEnumerator();
         }
         return default;
      }

      IEnumerator IEnumerable.GetEnumerator() {
         return GetEnumerator();
      }
   }
}
