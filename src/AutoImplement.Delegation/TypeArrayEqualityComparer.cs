using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.AutoImplement.Delegation;

/// <summary>
/// Provides a simple comparison implementation for enumerable things.
/// Used for creating dictionaries of implementations for generic methods (for stubs).
/// </summary>
public class EnumerableEqualityComparer<T> : IEqualityComparer<IEnumerable<T>> {
   /// <inheritdoc />
   public bool Equals(IEnumerable<T> a, IEnumerable<T> b) => a.SequenceEqual(b);

   /// <inheritdoc />
   public int GetHashCode(IEnumerable<T> elements) {
      uint code = 0;
      foreach (var element in elements) {
         code = ((code <<13) | (code >> 19)); // cyclic bit shift by a prime number
         code ^= (uint)element.GetHashCode();
      }

      return (int)code;
   }
}
