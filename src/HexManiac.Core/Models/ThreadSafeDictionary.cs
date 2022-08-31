using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace HavenSoft.HexManiac.Core.Models {
   public class ThreadSafeDictionary<TKey, TValue> : IDictionary<TKey, TValue> {
      private readonly Dictionary<TKey, TValue> inner = new();

      public TValue this[TKey key] {
         get { lock (inner) return inner[key]; }
         set { lock (inner) inner[key] = value; }
      }

      public ICollection<TKey> Keys {
         get { lock (inner) return inner.Keys.ToList(); }
      }

      public ICollection<TValue> Values {
         get { lock (inner) return inner.Values.ToList(); }
      }

      public int Count {
         get { lock (inner) return inner.Count; }
      }

      public bool IsReadOnly => false;

      public void Add(TKey key, TValue value) {
         lock (inner) inner.Add(key, value);
      }

      public void Add(KeyValuePair<TKey, TValue> item) {
         lock (inner) ((ICollection<KeyValuePair<TKey, TValue>>)inner).Add(item);
      }

      public void Clear() {
         lock (inner) inner.Clear();
      }

      public bool Contains(KeyValuePair<TKey, TValue> item) {
         lock (inner) return ((ICollection<KeyValuePair<TKey, TValue>>)inner).Contains(item);
      }

      public bool ContainsKey(TKey key) {
         lock (inner) return inner.ContainsKey(key);
      }

      public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) {
         lock (inner) ((ICollection<KeyValuePair<TKey, TValue>>)inner).CopyTo(array, arrayIndex);
      }

      public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() {
         lock (inner) return inner.GetEnumerator();
      }

      public bool Remove(TKey key) {
         lock (inner) return inner.Remove(key);
      }

      public bool Remove(KeyValuePair<TKey, TValue> item) {
         lock (inner) return ((ICollection<KeyValuePair<TKey, TValue>>)inner).Remove(item);
      }

      public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value) {
         lock (inner) return inner.TryGetValue(key, out value);
      }

      IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
   }
}
