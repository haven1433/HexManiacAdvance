using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.HexManiac.Core.Models.Runs {
   public class ModelCacheScope : IDisposable {
      private readonly IDataModel model;
      public ModelCacheScope(IDataModel model) => this.model = model;
      public static IDisposable CreateScope(IDataModel model) {
         return model.CurrentCacheScope;
      }
      public void Dispose() { }

      public static ModelCacheScope GetCache(IDataModel model) {
         return model.CurrentCacheScope;
      }

      private readonly Dictionary<string, IReadOnlyList<string>> cachedOptions = new Dictionary<string, IReadOnlyList<string>>();
      private readonly Dictionary<string, IReadOnlyList<string>> cachedBitOptions = new Dictionary<string, IReadOnlyList<string>>();
      private readonly Dictionary<int, IPixelViewModel> cachedImages = new Dictionary<int, IPixelViewModel>();

      public IReadOnlyList<string> GetOptions(string table) {
         if (!cachedOptions.ContainsKey(table)) cachedOptions[table] = GetOptions(model, table) ?? new List<string>();
         return cachedOptions[table];
      }

      public static string QuoteIfNeeded(string text) {
         if (!text.Contains(" ")) return text;
         if (text.Contains("\"")) return text;
         return $"\"{text}\"";
      }

      public IReadOnlyList<string> GetBitOptions(string enumName) {
         if (cachedBitOptions.ContainsKey(enumName)) return cachedBitOptions[enumName];

         // if it's from a list, then it's not from an enum, but instead from just a string list
         if (model.TryGetList(enumName, out var nameArray)) return nameArray.Select(QuoteIfNeeded).ToList();

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

      public IPixelViewModel GetImage(ISpriteRun run) {
         if (cachedImages.TryGetValue(run.Start, out var pixels)) return pixels;
         pixels = SpriteDecorator.BuildSprite(model, run);
         cachedImages[run.Start] = pixels;
         return pixels;
      }

      private static IReadOnlyList<string> GetOptions(IDataModel model, string enumName) {
         if (model.TryGetList(enumName, out var nameArray)) return nameArray;

         if (!model.TryGetNameArray(enumName, out var enumArray)) {
            if (model.TryGetIndexNames(enumName, out var indexArray)) return indexArray;
            if (model.TryGetDerivedEnumNames(enumName, out var derivedEnumArray)) return derivedEnumArray;
            if (model.TryGetListEnumNames(enumName, out var listContent)) return listContent;
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

         var resultCache = new Dictionary<string, int>();

         for (int i = 0; i < optionCount; i++) {
            var elementStart = enumArray.Start + enumArray.ElementLength * i + segmentOffset;
            var elementLength = segment.Length;
            if (isPointer) {
               elementStart = model.ReadPointer(elementStart);
               if (elementStart < 0 || elementStart >= model.Count) return new string[0];
               elementLength = PCSString.ReadString(model, elementStart, true);
               if (elementLength < 1) return new string[0]; // contents must be a valid string to be used as options
            }

            var valueWithQuotes = PCSString.Convert(model, elementStart, elementLength)?.Replace(Environment.NewLine, string.Empty).Trim() ?? string.Empty;

            if (valueWithQuotes.Contains(' ')) {
               AddResult(resultCache, results, valueWithQuotes);
               continue;
            }

            var value = valueWithQuotes;
            if (value.StartsWith("\"")) value = value.Substring(1);
            if (value.EndsWith("\"")) value = value.Substring(0, value.Length - 1);
            AddResult(resultCache, results, value);
         }

         return results;
      }

      /// <summary>
      /// Adds a ~n suffix onto results that have already been added to this result list before.
      /// This lets us easily distinguish multiple elements with the same name.
      /// </summary>
      private static void AddResult(IDictionary<string, int> currentResultsCache, IList<string> results, string newResult) {
         if (!currentResultsCache.TryGetValue(newResult, out int count)) count = 1;
         currentResultsCache[newResult] = count + 1;
         var hasQuotes = newResult.StartsWith("\"") && newResult.EndsWith("\"");
         if (hasQuotes) newResult = newResult.Substring(1, newResult.Length - 2);
         if (count > 1) newResult += "~" + count;
         if (hasQuotes) newResult = $"\"{newResult}\"";
         results.Add(newResult);
      }
   }
}
