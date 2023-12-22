using HavenSoft.HexManiac.Core.Models.Code;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using HexManiac.Core.Models.Runs.Sprites;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
      private IReadOnlyList<MapInfo> cachedMapInfo;
      private readonly Dictionary<int, IPixelViewModel> cachedImages = new Dictionary<int, IPixelViewModel>();

      public IReadOnlyList<string> GetOptions(string table) {
         if (model is PokemonModel pModel) {
            IReadOnlyList<string> result = null;
            pModel.ThreadlockRuns(() => {
               lock (cachedOptions) {
                  if (!cachedOptions.ContainsKey(table)) cachedOptions[table] = GetOptions(model, table) ?? new List<string>();
                  result = cachedOptions[table];
               }
            });
            return result;
         } else {
            lock (cachedOptions) {
               if (!cachedOptions.ContainsKey(table)) cachedOptions[table] = GetOptions(model, table) ?? new List<string>();
               return cachedOptions[table];
            }
         }
      }

      private readonly Dictionary<string, IReadOnlyList<ArrayRun>> cachedDependentArrays = new();
      public IEnumerable<ArrayRun> GetDependantArrays(string anchor) {
         if (cachedDependentArrays.TryGetValue(anchor, out var cache)) return cache;

         var results = new List<ArrayRun>();
         foreach (var array in model.Arrays) {
            if (array.LengthFromAnchor == anchor) results.Add(array);
            foreach (var segment in array.ElementContent) {
               if (segment is ArrayRunBitArraySegment bitSegment) {
                  if (bitSegment.SourceArrayName == anchor) results.Add(array);
               }
            }
         }
         cachedDependentArrays[anchor] = results;
         return results;
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
         lock (cachedImages) {
            if (cachedImages.TryGetValue(run.Start, out var pixels)) return pixels;
            pixels = SpriteDecorator.BuildSprite(model, run);
            cachedImages[run.Start] = pixels;
            return pixels;
         }
      }

      public IPixelViewModel GetImage(BlockmapRun run) {
         lock (cachedImages) {
            if (cachedImages.TryGetValue(run.Start, out var pixels)) return pixels;
            pixels = SpriteDecorator.BuildSprite(model, run);
            cachedImages[run.Start] = pixels;
            return pixels;
         }
      }

      public IReadOnlyList<MapInfo> GetAllMaps() {
         if (cachedMapInfo != null) return cachedMapInfo;
         var results = new List<MapInfo>();
         cachedMapInfo = results;
         var bankTable = model.GetTable(HardcodeTablesModel.MapBankTable);
         if (bankTable == null) return results;
         var banks = new ModelTable(model, bankTable.Start, null, bankTable);
         for (int i = 0; i < banks.Count; i++) {
            var maps = banks[i].GetSubTable("maps");
            if (maps == null) continue;
            for (int j = 0; j < maps.Count; j++) {
               var name = BlockMapViewModel.MapIDToText(model, i, j);
               var mapText = $"maps.bank{i}.{name}";
               results.Add(new(i, j, mapText));
            }
         }
         return results;
      }

      #region Script Cache

      // stores only the start and length
      private Dictionary<int, Dictionary<int, int>> scriptDestinations = new();
      public Dictionary<int, int> ScriptDestinations(int start) {
         if (!scriptDestinations.TryGetValue(start, out var destinations)) {
            destinations = new();
            scriptDestinations[start] = destinations;
         }
         return destinations;
      }

      // stores start, length, and contents
      private readonly Dictionary<int, ScriptInfo> cachedScripts = new();

      public ScriptInfo GetScriptInfo(ScriptParser parser, int scriptStart, CodeBody updateBody, ref int existingSectionCount) {
         if (cachedScripts.TryGetValue(scriptStart, out var scriptInfo)) {
            existingSectionCount = scriptInfo.SectionCount;
            return scriptInfo;
         }
         var destinations = ScriptDestinations(scriptStart);
         var scriptLength = parser.FindLength(model, scriptStart, destinations);
         var content = parser.Parse(model, scriptStart, scriptLength, ref existingSectionCount, updateBody);
         scriptInfo = new ScriptInfo(scriptStart, scriptLength, content, existingSectionCount);
         destinations[scriptStart] = scriptLength;
         cachedScripts[scriptStart] = scriptInfo;
         return scriptInfo;
      }

      #endregion

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

            var valueWithQuotes = model.TextConverter.Convert(model, elementStart, elementLength)?.Replace(Environment.NewLine, string.Empty).Trim() ?? string.Empty;

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
         if (!currentResultsCache.TryGetValue(newResult.ToLower(), out int count)) count = 1;
         currentResultsCache[newResult.ToLower()] = count + 1;
         var hasQuotes = newResult.StartsWith("\"") && newResult.EndsWith("\"");
         if (hasQuotes) newResult = newResult.Substring(1, newResult.Length - 2);
         if (count > 1) newResult += "~" + count;
         if (hasQuotes) newResult = $"\"{newResult}\"";
         results.Add(newResult);
      }
   }

   public record MapInfo(int Group, int Map, string Name) : INotifyPropertyChanged {
      event PropertyChangedEventHandler? INotifyPropertyChanged.PropertyChanged { add { } remove { } }
   }

   public record JumpMapInfo(int Group, int Map, string Name, Action<ChangeMapEventArgs> JumpAction) : MapInfo(Group, Map, Name) {
      public void GotoMap() => JumpAction(new(Group, Map));
   }

   public record ScriptInfo(int Start, int Length, string Content, int SectionCount) : ISearchTreePayload;
}
