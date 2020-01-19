using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace HavenSoft.HexManiac.Core.Models.Runs {
   /// <summary>
   /// PLMRuns have a hard-coded dependency on a table named 'movenames', which it uses to... get the names of the moves.
   /// </summary>
   public class PLMRun : BaseRun, IStreamRun {
      public const int MaxLearningLevel = 100;
      public static readonly string SharedFormatString = AsciiRun.StreamDelimeter + "plm" + AsciiRun.StreamDelimeter;
      private readonly IDataModel model;
      public override int Length { get; }
      public override string FormatString => SharedFormatString;

      public PLMRun(IDataModel dataModel, int start, IReadOnlyList<int> pointerSources = null) : base(start, pointerSources) {
         model = dataModel;
         var moveNames = ModelCacheScope.GetCache(dataModel).GetOptions(HardcodeTablesModel.MoveNamesTable);
         Length = 1;
         for (int i = Start; i < model.Count; i += 2) {
            var value = model.ReadMultiByteValue(i, 2);
            if (value == 0xFFFF) {
               Length = i - Start + 2;
               break;
            }
            // validate value
            var (level, move) = SplitToken(value);
            if (level > 101 || level < 1) break;
            if (move > moveNames.Count) break;
         }
      }

      public static (int level, int move) SplitToken(int value) {
         var level = (value & 0xFE00) >> 9;
         var move = (value & 0x1FF);
         return (level, move);
      }

      public static int CombineToken(int level, int move) => (level << 9) | move;

      public override IDataFormat CreateDataFormat(IDataModel data, int index) {
         Debug.Assert(data == model);
         var moveNames = ModelCacheScope.GetCache(model).GetOptions(HardcodeTablesModel.MoveNamesTable);
         var position = index - Start;
         var groupStart = position % 2 == 1 ? position - 1 : position;
         position -= groupStart;
         var value = data.ReadMultiByteValue(Start + groupStart, 2);
         var (level, move) = SplitToken(value);
         var moveName = moveNames.Count > move ? moveNames[move] : move.ToString();
         return new PlmItem(groupStart + Start, position, level, move, moveName);
      }

      protected override BaseRun Clone(IReadOnlyList<int> newPointerSources) => new PLMRun(model, Start, newPointerSources);

      public bool TryGetMoveNumber(string moveName, out int move) {
         moveName = moveName.Trim('"').ToLower();
         var moveNames = ModelCacheScope.GetCache(model).GetOptions(HardcodeTablesModel.MoveNamesTable);
         var names = moveNames.Select(name => name.Trim('"').ToLower()).ToList();

         // perfect match?
         move = names.IndexOf(moveName);
         if (move != -1) return true;

         // partial match?
         move = names.IndexOfPartial(moveName);

         // last try: numeric match
         return move != -1 || int.TryParse(moveName, out move);
      }

      public IEnumerable<string> GetAutoCompleteOptions(string header) {
         var moveNames = ModelCacheScope.GetCache(model).GetOptions(HardcodeTablesModel.MoveNamesTable);
         return moveNames.Select(name => $"{header} {name}" + (name.EndsWith("\"") ? "" : " ")); // autocomplete needs to complete after selection, so add a space if there's no quotes
      }

      public string SerializeRun() {
         var moveNames = ModelCacheScope.GetCache(model).GetOptions(HardcodeTablesModel.MoveNamesTable);
         var builder = new StringBuilder();
         for (int i = 0; i < Length - 2; i += 2) {
            var address = Start + i;
            var (level, move) = SplitToken(model.ReadMultiByteValue(address, 2));
            var moveName = moveNames.Count > move ? moveNames[move] : move.ToString();
            builder.Append($"{level} {moveName}");
            if (i < Length - 4) builder.AppendLine();
         }
         return builder.ToString();
      }

      public IStreamRun DeserializeRun(string content, ModelDelta token) {
         var data = new List<int>();
         var moveNames = ModelCacheScope.GetCache(model).GetOptions(HardcodeTablesModel.MoveNamesTable);
         moveNames = moveNames.Select(name => name.Trim('"').ToLower()).ToList();

         var lines = content.ToLower().Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
         foreach (var line in lines) {
            var parts = line.Split(new[] { ' ' }, 2);
            if (!int.TryParse(parts[0], out var level)) continue;
            var moveName = parts.Length == 1 ? "0" : parts[1];
            moveName = moveName.Trim().Trim('"');

            var index = moveNames.IndexOf(moveName);
            if (index != -1) { data.Add(CombineToken(level, index)); continue; }

            // look for a partial move match
            for (int i = 0; i < moveNames.Count; i++) {
               if (moveNames[i].MatchesPartial(moveName)) { data.Add(CombineToken(level, i)); break; }
            }
         }

         var run = model.RelocateForExpansion(token, this, data.Count * 2 + 2);
         for (int i = 0; i < data.Count; i++) model.WriteMultiByteValue(run.Start + i * 2, 2, token, data[i]);
         model.WriteMultiByteValue(run.Start + data.Count * 2, 2, token, EggMoveRun.EndStream); // write the new end token
         for (int i = data.Count * 2 + 2; i < Length; i++) token.ChangeData(model, run.Start + i, 0xFF); // fill any remaining old space with FF

         return new PLMRun(model, run.Start);
      }

      public bool DependsOn(string anchorName) => anchorName == HardcodeTablesModel.MoveNamesTable;

      public IEnumerable<(int, int)> Search(string parentArrayName, int index) {
         for (int i = 0; i < Length - 2; i += 2) {
            var fullValue = model.ReadMultiByteValue(Start + i, 2);
            if (SplitToken(fullValue).move == index) {
               yield return (Start + i, Start + i + 1);
            }
         }
      }

      public void AppendTo(StringBuilder text, int start, int length) {
         var moveNames = ModelCacheScope.GetCache(model).GetOptions(HardcodeTablesModel.MoveNamesTable);
         for (int i = 0; i < length && i < Length - Start; i += 2) {
            if (i > 0) text.Append(" ");
            if (i + start - Start == Length - 2) { text.Append("[]"); continue; }
            var (level, move) = SplitToken(model.ReadMultiByteValue(start + i, 2));
            var moveName = moveNames.Count > move ? moveNames[move] : move.ToString();
            text.Append($"{level} {moveName}");
         }
      }
   }
}
