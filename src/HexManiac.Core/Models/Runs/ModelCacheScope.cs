using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace HavenSoft.HexManiac.Core.Models.Runs {
   public class ModelCacheScope : IDisposable {
      private static readonly Dictionary<IDataModel, ModelCacheScope> activeScopes = new Dictionary<IDataModel, ModelCacheScope>();
      private readonly IDataModel model;
      private ModelCacheScope(IDataModel model) => this.model = model;
      public static IDisposable CreateScope(IDataModel model) {
         Debug.Assert(!activeScopes.ContainsKey(model));
         activeScopes[model] = new ModelCacheScope(model);
         return activeScopes[model];
      }
      public void Dispose() => activeScopes.Remove(model);

      public static ModelCacheScope GetCache(IDataModel model) => activeScopes.TryGetValue(model, out var scope) ? scope : null;

      private readonly Dictionary<string, IReadOnlyList<string>> cachedOptions = new Dictionary<string, IReadOnlyList<string>>();
      public IReadOnlyList<string> GetOptions(string table) {
         if (!cachedOptions.ContainsKey(table)) cachedOptions[table] = ArrayRunEnumSegment.GetOptions(model, table) ?? new List<string>();
         return cachedOptions[table];
      }
   }
}
