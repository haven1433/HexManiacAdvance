using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace HavenSoft.HexManiac.Core.Models.Runs {
   public class PLMRun : IFormattedRun {
      public const int MaxLearningLevel = 100;
      public static readonly string SharedFormatString = AsciiRun.StreamDelimeter + "plm" + AsciiRun.StreamDelimeter;
      private readonly IDataModel model;
      public int Start { get; }
      public int Length { get; }
      public IReadOnlyList<int> PointerSources { get; private set; }
      public string FormatString => SharedFormatString;

      public PLMRun(IDataModel dataModel, int start) {
         model = dataModel;
         cachedMovenames = ArrayRunEnumSegment.GetOptions(model, EggMoveRun.MoveNamesTable) ?? new List<string>();
         Start = start;
         for (int i = Start; i < model.Count; i += 2) {
            var value = model.ReadMultiByteValue(i, 2);
            if (value == 0xFFFF) {
               Length = i - Start + 2;
               break;
            }
            // validate value
            var (level, move) = SplitToken(value);
            if (level > 100) break;
            if (move > cachedMovenames.Count) break;
         }
      }

      public static (int level, int move) SplitToken(int value) {
         var level = (value & 0xFE00) >> 9;
         var move = (value & 0x1FF);
         return (level, move);
      }

      private IReadOnlyList<string> cachedMovenames;
      private int lastFormatRequest = int.MinValue;
      public IDataFormat CreateDataFormat(IDataModel data, int index) {
         Debug.Assert(data == model);
         if (index != lastFormatRequest + 1) {
            cachedMovenames = ArrayRunEnumSegment.GetOptions(model, EggMoveRun.MoveNamesTable) ?? new List<string>();
         }
         lastFormatRequest = index;

         var position = index - Start;
         var groupStart = position % 2 == 1 ? position - 1 : position;
         position -= groupStart;
         var value = data.ReadMultiByteValue(Start + groupStart, 2);
         var (level, move) = SplitToken(value);
         var moveName = cachedMovenames.Count > move ? cachedMovenames[move] : move.ToString();
         return new PlmItem(groupStart + Start, position, level, move, moveName);
      }

      public IFormattedRun MergeAnchor(IReadOnlyList<int> sources) {
         var newSources = new HashSet<int>();
         if (sources != null) newSources.AddRange(sources);
         if (PointerSources != null) newSources.AddRange(PointerSources);
         return new PLMRun(model, Start) { PointerSources = newSources.ToList() };
      }

      public IFormattedRun RemoveSource(int source) {
         var sources = PointerSources.ToList();
         sources.Remove(source);
         return new PLMRun(model, Start) { PointerSources = sources };
      }

      public bool TryGetMoveNumber(string moveName, out int move) {
         moveName = moveName.Trim('"').ToLower();
         var names = cachedMovenames.Select(name => name.Trim('"').ToLower()).ToList();
         move = names.IndexOfPartial(moveName);
         return move != -1;
      }

      public IEnumerable<string> GetAutoCompleteOptions(string header) {
         return cachedMovenames.Select(name => $"{header} {name} "); // autocomplete needs to complete after selection, so add a space
      }
   }
}
