#nullable enable
using System;
using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core;

/// <summary>
/// If used as AutoDictionary or IDictionary,
/// requesting a value from an unused key will create and add a default value.
/// Casting to a Dictionary will act like a Dictionary.
/// Contains and TryGet still work the normal way.
/// </summary>
public class AutoDictionary<TKey, TValue> : Dictionary<TKey, TValue>, IDictionary<TKey, TValue> where TKey : notnull {
   private readonly Func<TKey, TValue> factory;

   /// <param name="factory">A method for creating a new value based on a key.</param>
   public AutoDictionary(Func<TKey, TValue> factory) => this.factory = factory;

   TValue IDictionary<TKey, TValue>.this[TKey key] {
      get => this[key];
      set => this[key] = value;
   }

   public new TValue this[TKey key] {
      get => TryGetValue(key, out var value) ? value : base[key] = factory(key);
      set => base[key] = value;
   }
}
