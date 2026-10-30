using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.HexManiac.Core.Models {
   public class LazyList<T> : IReadOnlyList<T> {
      private IEnumerable<T> lazyData;
      private IReadOnlyList<T> concreteData;

      public LazyList(IEnumerable<T> data) => lazyData = data;

      private IReadOnlyList<T> Init() {
         if (concreteData != null) return concreteData;
         lock (this) {
            if (lazyData == null) return concreteData;
            concreteData = lazyData.ToList();
            lazyData = null;
         }
         return concreteData;
      }

      public int Count => Init().Count;

      public T this[int index] => Init()[index];

      public IEnumerator<T> GetEnumerator() => Init().GetEnumerator();

      IEnumerator IEnumerable.GetEnumerator() => Init().GetEnumerator();
   }
}
