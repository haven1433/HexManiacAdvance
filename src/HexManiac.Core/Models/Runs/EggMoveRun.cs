using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace HavenSoft.HexManiac.Core.Models.Runs {
   public class EggMoveRun : IFormattedRun {
      private const int MagicNumber = 0x4E20; // anything above this number is a pokemon, anything below it is a move
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

      public IDataFormat CreateDataFormat(IDataModel data, int dataIndex) {
         Debug.Assert(data == model);
         var position = dataIndex - Start;
         var groupStart = position % 2 == 1 ? position - 1 : position;
         var value = data.ReadMultiByteValue(groupStart, 2);
         if (value >= MagicNumber) {
            return new EggSection();
         } else {
            return new EggItem();
         }
      }

      public IFormattedRun MergeAnchor(IReadOnlyList<int> sources) {
         var newSources = new HashSet<int>();
         if (sources != null) newSources.AddRange(sources);
         if (PointerSources != null) newSources.AddRange(PointerSources);
         return new EggMoveRun(model, Start) { PointerSources = newSources.ToList() };
      }

      public IFormattedRun RemoveSource(int source) {
         throw new NotImplementedException();
      }
   }
}
