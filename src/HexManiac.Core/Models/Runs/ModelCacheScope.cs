using System;
using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.Models.Runs {
   public class ModelCacheScope : IDisposable {
      private static readonly Dictionary<IDataModel, ModelCacheScope> activeScopes = new Dictionary<IDataModel, ModelCacheScope>();

      private readonly IDataModel model;
      private int references;
      private ModelCacheScope(IDataModel model) => this.model = model;
      public static IDisposable CreateScope(IDataModel model) {
         lock (activeScopes) {
            if (!activeScopes.ContainsKey(model)) {
               activeScopes[model] = new ModelCacheScope(model);
            }
            activeScopes[model].references++;
            return activeScopes[model];
         }
      }
      public void Dispose() {
         lock (activeScopes) {
            activeScopes[model].references--;
            if (activeScopes[model].references == 0) activeScopes.Remove(model);
         }
      }

      public static ModelCacheScope GetCache(IDataModel model) => activeScopes.TryGetValue(model, out var scope) ? scope : null;

      private readonly Dictionary<string, IReadOnlyList<string>> cachedOptions = new Dictionary<string, IReadOnlyList<string>>();
      public IReadOnlyList<string> GetOptions(string table) {
         if (!cachedOptions.ContainsKey(table)) cachedOptions[table] = ArrayRunEnumSegment.GetOptions(model, table) ?? new List<string>();
         return cachedOptions[table];
      }
   }
}
