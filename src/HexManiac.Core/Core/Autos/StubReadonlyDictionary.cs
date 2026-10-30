using System.Diagnostics.CodeAnalysis;
using HavenSoft.AutoImplement.Delegation;

namespace System.Collections.Generic {
   public class StubReadonlyDictionary<K, V> : IReadOnlyDictionary<K, V> {
      public Func<K, V> Items = arg => default;
      public V this[K key] => Items(key);

      public PropertyImplementation<IEnumerable<K>> Keys;
      IEnumerable<K> IReadOnlyDictionary<K, V>.Keys => Keys?.get() ?? default;

      public PropertyImplementation<IEnumerable<V>> Values;
      IEnumerable<V> IReadOnlyDictionary<K, V>.Values => Values?.get() ?? default;

      public PropertyImplementation<int> Count;
      int IReadOnlyCollection<KeyValuePair<K, V>>.Count => Count?.get() ?? default;

      public Func<K, bool> ContainsKey;
      bool IReadOnlyDictionary<K, V>.ContainsKey(K key) => ContainsKey?.Invoke(key) ?? default;

      public Func<IEnumerator<KeyValuePair<K, V>>> GetEnumerator;
      IEnumerator<KeyValuePair<K, V>> IEnumerable<KeyValuePair<K, V>>.GetEnumerator() => GetEnumerator?.Invoke() ?? default;

      public bool TryGetValue(K key, [MaybeNullWhen(false)] out V value) {
         value = default;
         if (!ContainsKey(key)) return false;
         value = Items(key);
         return true;
      }

      IEnumerator IEnumerable.GetEnumerator() => GetEnumerator?.Invoke() ?? default;
   }
}
