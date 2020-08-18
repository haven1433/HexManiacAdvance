using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

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

      public static ModelCacheScope GetCache(IDataModel model) {
         if (activeScopes.TryGetValue(model, out var scope)) {
            return scope;
         } else {
            Debug.Fail("Requested a scoped cache, we're not currently in a scope!");
            return new ModelCacheScope(model);
         }
      }

      private readonly Dictionary<string, IReadOnlyList<string>> cachedOptions = new Dictionary<string, IReadOnlyList<string>>();
      private readonly Dictionary<string, IReadOnlyList<string>> cachedBitOptions = new Dictionary<string, IReadOnlyList<string>>();

      public IReadOnlyList<string> GetOptions(string table) {
         if (!cachedOptions.ContainsKey(table)) cachedOptions[table] = GetOptions(model, table) ?? new List<string>();
         return cachedOptions[table];
      }

      public IReadOnlyList<string> GetBitOptions(string enumName) {
         if (cachedBitOptions.ContainsKey(enumName)) return cachedBitOptions[enumName];

         // if it's from a list, then it's not from an enum, but instead from just a string list
         if (model.TryGetList(enumName, out var nameArray)) return nameArray;

         var sourceAddress = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, enumName);
         if (sourceAddress == Pointer.NULL) return null;
         if (!(model.GetNextRun(sourceAddress) is ArrayRun sourceRun)) return null;
         if (!(sourceRun.ElementContent[0] is ArrayRunEnumSegment sourceSegment)) {
            if (sourceRun.ElementContent[0].Type == ElementContentType.PCS) return sourceRun.ElementNames;
            return null;
         }

         var enumOptions = sourceSegment.GetOptions(model).ToList();
         if (enumOptions == null) return null;

         var results = new List<string>(sourceRun.ElementCount);
         for (int i = 0; i < sourceRun.ElementCount; i++) {
            var value = model.ReadMultiByteValue(sourceRun.Start + sourceRun.ElementLength * i, sourceSegment.Length);
            if (value < enumOptions.Count) {
               results.Add(enumOptions[value]);
            } else {
               results.Add(value.ToString());
            }
         }

         cachedBitOptions[enumName] = results;
         return results;
      }

      private static IReadOnlyList<string> GetOptions(IDataModel model, string enumName) {
         if (model.TryGetList(enumName, out var nameArray)) return nameArray;

         if (!model.TryGetNameArray(enumName, out var enumArray)) {
            if (model.TryGetIndexNames(enumName, out var indexArray)) return indexArray;
            if (model.TryGetDerivedEnumNames(enumName, out var derivedEnumArray)) return derivedEnumArray;
            return new string[0];
         }

         // array must be at least as long as than the current value
         var optionCount = enumArray.ElementCount;

         // sweet, we can convert from the integer value to the enum value
         var results = new List<string>();
         var textIndex = 0;
         int segmentOffset = 0;
         while (textIndex < enumArray.ElementContent.Count) {
            var segmnt = enumArray.ElementContent[textIndex];
            if (segmnt.Type == ElementContentType.PCS) break;
            if (segmnt is ArrayRunPointerSegment pSegment && pSegment.InnerFormat == PCSRun.SharedFormatString) break;
            segmentOffset += segmnt.Length;
            textIndex++;
         }
         if (textIndex == enumArray.ElementContent.Count) return new string[0];
         var segment = enumArray.ElementContent[textIndex];
         var isPointer = segment.Type == ElementContentType.Pointer;
         for (int i = 0; i < optionCount; i++) {
            var elementStart = enumArray.Start + enumArray.ElementLength * i + segmentOffset;
            var elementLength = segment.Length;
            if (isPointer) {
               elementStart = model.ReadPointer(elementStart);
               if (elementStart < 0 || elementStart >= model.Count) return new string[0];
               elementLength = PCSString.ReadString(model, elementStart, true);
               if (elementLength < 1) return new string[0]; // contents must be a valid string to be used as options
            }

            var valueWithQuotes = PCSString.Convert(model, elementStart, elementLength)?.Trim() ?? string.Empty;

            if (valueWithQuotes.Contains(' ')) {
               results.Add(valueWithQuotes);
               continue;
            }

            var value = valueWithQuotes;
            if (value.StartsWith("\"")) value = value.Substring(1);
            if (value.EndsWith("\"")) value = value.Substring(0, value.Length - 1);
            results.Add(value);
         }

         return results;
      }
   }
}
