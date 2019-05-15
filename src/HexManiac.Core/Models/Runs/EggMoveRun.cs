using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace HavenSoft.HexManiac.Core.Models.Runs {
   public class EggMoveRun : IFormattedRun {
      public const int MagicNumber = 0x4E20; // anything above this number is a pokemon, anything below it is a move
      public const string PokemonNameTable = "pokenames";
      public const string MoveNamesTable = "movenames";
      private readonly IDataModel model;

      public int Start { get; }
      public int Length { get; }
      public IReadOnlyList<int> PointerSources { get; private set; }
      public string FormatString => AsciiRun.StreamDelimeter + "egg" + AsciiRun.StreamDelimeter;

      public EggMoveRun(IDataModel dataModel, int dataIndex) {
         model = dataModel;
         Start = dataIndex;
         Length = 0;
         for (int i = Start; i < model.Count; i += 2) {
            if (model[i] == 0xFF && model[i + 1] == 0xFF) {
               Length = i - Start + 2;
               break;
            }
         }
      }

      public static bool TrySearch(IDataModel data, ModelDelta token, out EggMoveRun eggmoves) {
         eggmoves = default;
         var pokenames = data.GetNextRun(data.GetAddressFromAnchor(token, -1, PokemonNameTable)) as ArrayRun;
         var movenames = data.GetNextRun(data.GetAddressFromAnchor(token, -1, MoveNamesTable)) as ArrayRun;
         if (pokenames == null || movenames == null) return false;

         for (var run = data.GetNextRun(0); run.Start < int.MaxValue; run = data.GetNextRun(run.Start + run.Length)) {
            if (run is ArrayRun || run is PCSRun || run.PointerSources == null) continue;

            // verify expected pointers to this
            if (run.PointerSources.Count != 2) continue;

            // verify limiter
            var length = data.ReadMultiByteValue(run.PointerSources[1] - 4, 4);

            // we just read the 'length' from basically a random byte... verify that it could make sense as a length
            if (length < 0) continue;
            if (run.Start + length * 2 + 3 < 0) continue;
            if (run.Start + length * 2 + 3 > data.Count) continue;
            var endValue = data.ReadMultiByteValue(run.Start + length * 2 + 2, 2);
            if (endValue != 0xFFFF) continue;

            // verify content
            bool possibleMatch = true;
            for (int i = 0; i < length - 2; i++) {
               var value = data.ReadMultiByteValue(run.Start + i * 2, 2);
               if (value == 0xFFFF) break; // early exit, the data was edited, but that's ok. Everything still matches up.
               if (value >= MagicNumber) {
                  value -= MagicNumber;
                  if (value < pokenames.ElementCount) continue;
               }
               if (value < movenames.ElementCount) continue;
               possibleMatch = false;
               break;
            }
            if (!possibleMatch) continue;

            // content is correct, length is correct: this is it!
            eggmoves = new EggMoveRun(data, run.Start);
            return true;
         }

         return false;
      }

      private IReadOnlyList<string> cachedPokenames, cachedMovenames;
      private int lastFormatRequest = int.MinValue;
      public IDataFormat CreateDataFormat(IDataModel data, int dataIndex) {
         Debug.Assert(data == model);
         if (dataIndex != lastFormatRequest + 1) {
            cachedPokenames = ArrayRunEnumSegment.GetOptions(model, PokemonNameTable) ?? new List<string>();
            cachedMovenames = ArrayRunEnumSegment.GetOptions(model, MoveNamesTable) ?? new List<string>();
         }
         lastFormatRequest = dataIndex;

         var position = dataIndex - Start;
         var groupStart = position % 2 == 1 ? position - 1 : position;
         var value = data.ReadMultiByteValue(groupStart, 2);
         if (value >= MagicNumber) {
            value -= MagicNumber;
            string content = cachedPokenames.Count > value ? cachedPokenames[value] : value.ToString();
            if (value == 0xFFFF - MagicNumber) content = string.Empty;
            if (content.StartsWith("\"")) content = content.Substring(1);
            if (content.EndsWith("\"")) content = content.Substring(0, content.Length - 1);
            return new EggSection(groupStart, position, $"[{content}]");
         } else {
            string content = cachedMovenames.Count > value ? cachedMovenames[value] : value.ToString();
            return new EggItem(groupStart, position, content);
         }
      }

      public IFormattedRun MergeAnchor(IReadOnlyList<int> sources) {
         var newSources = new HashSet<int>();
         if (sources != null) newSources.AddRange(sources);
         if (PointerSources != null) newSources.AddRange(PointerSources);
         return new EggMoveRun(model, Start) { PointerSources = newSources.ToList() };
      }

      public IFormattedRun RemoveSource(int source) {
         var sources = PointerSources.ToList();
         sources.Remove(source);
         return new EggMoveRun(model, Start) { PointerSources = sources };
      }
   }
}
