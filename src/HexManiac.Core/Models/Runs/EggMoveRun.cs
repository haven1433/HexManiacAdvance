using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace HavenSoft.HexManiac.Core.Models.Runs {
   public class EggMoveRun : IFormattedRun {
      public const int MagicNumber = 0x4E20; // anything above this number is a pokemon, anything below it is a move
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

      private IReadOnlyList<string> cachedPokenames, cachedMovenames;
      private int lastFormatRequest = int.MinValue;
      public IDataFormat CreateDataFormat(IDataModel data, int dataIndex) {
         Debug.Assert(data == model);
         if (dataIndex != lastFormatRequest + 1) {
            cachedPokenames = ArrayRunEnumSegment.GetOptions(model, "pokenames") ?? new List<string>();
            cachedMovenames = ArrayRunEnumSegment.GetOptions(model, "movenames") ?? new List<string>();
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
