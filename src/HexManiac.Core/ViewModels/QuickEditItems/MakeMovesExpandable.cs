using System;
using System.Collections.Generic;
using System.Linq;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Code;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using static HavenSoft.HexManiac.Core.Models.HardcodeTablesModel;

namespace HavenSoft.HexManiac.Core.ViewModels.QuickEditItems {
   public class MakeMovesExpandable : IQuickEditItem {

      public string Name => "Make Moves Expandable";

      public string Description => "Running this utility will remove the move limiters " +
                                   "and allow for unlimited moves and effects.";

      public string WikiLink => "https://github.com/haven1433/HexManiacAdvance/wiki/Move-Expansion-Explained";

      public event EventHandler CanRunChanged;

      public bool CanRun(IViewPort viewPort) {
         return viewPort is IEditableViewPort;
      }

      public ErrorInfo Run(IViewPort viewPortInterface) {
         var viewPort = (IEditableViewPort)viewPortInterface;
         var token = viewPort.ChangeHistory.CurrentChange;
         var parser = viewPort.Tools.CodeTool.Parser;

         // fix limit for move effects
         var error = RefactorMoveByteFieldInTable(parser, viewPort.Model, token, MoveDataTable, "power", 9);
         if (error.HasError) return error;
         error = RefactorByteToHalfWordInTable(parser, viewPort.Model, token, MoveDataTable, "effect");
         if (error.HasError) return error;

         // update limiters
         ReplaceAll(parser, viewPort.Model, token,
            new[] { "mov r0, #177", "lsl r0, r0, #1", "cmp r1, r0" },
            new[] { "mov r0, #177", "lsl r0, r0, #9", "cmp r1, r0" });

         // TODO update levelup moves

         viewPort.Refresh();
         return ErrorInfo.NoError;
      }

      public static ErrorInfo RefactorMoveByteFieldInTable(ThumbParser parser, IDataModel model, ModelDelta token, string tableName, string fieldName, int newOffset) {
         // setup
         var table = model.GetTable(tableName) as ArrayRun;
         if (table == null) return new ErrorInfo($"Couldn't find table {tableName}.");
         var segToMove = table.ElementContent.FirstOrDefault(seg => seg.Name == fieldName);
         if (segToMove == null) return new ErrorInfo($"Couldn't find field {fieldName} in {tableName}.");
         if (segToMove.Length != 1) return new ErrorInfo($"{fieldName} must be a 1-byte field to refactor.");
         if (newOffset >= table.ElementLength) return new ErrorInfo($"Trying to move {fieldName} to offset {newOffset}, but the table is only {table.ElementLength} bytes wide.");
         var oldFieldIndex = table.ElementContent.IndexOf(segToMove);
         var newFieldIndex = 0;
         var remainingOffset = newOffset;
         while (remainingOffset > 0) {
            remainingOffset -= table.ElementContent[newFieldIndex].Length;
            newFieldIndex += 1;
         }
         var segToReplace = table.ElementContent[newFieldIndex];
         if (segToReplace.Length != 1) return new ErrorInfo($"{segToReplace.Name} must be a 1-byte field to be replaced.");
         int oldOffset = table.ElementContent.Until(seg => seg == segToMove).Sum(seg => seg.Length);

         // update table format
         var format = table.FormatString;
         format = format.ReplaceOne(segToMove.SerializeFormat, "#invalidtabletoken#");
         format = format.ReplaceOne(segToReplace.SerializeFormat, segToMove.SerializeFormat);
         format = format.ReplaceOne("#invalidtabletoken#", segToReplace.SerializeFormat);
         var error = ArrayRun.TryParse(model, format, table.Start, table.PointerSources, out var newTable);
         if (error.HasError) return new ErrorInfo($"Failed to create a table from new format {format}: {error.ErrorMessage}.");

         // update code
         var writer = new TupleSegment(default, 5);
         var allChanges = new List<string>();
         foreach (var address in table.FindAllByteReads(parser, oldFieldIndex)) {
            allChanges.Add(address.ToAddress());
            // ldrb rd, [rn, #]: 01111 # rn rd
            writer.Write(model, token, address, 6, newOffset);
         }

         // update table contents
         for (int i = 0; i < table.ElementCount; i++) {
            var value = model[table.Start + table.ElementLength * i + oldOffset];
            token.ChangeData(model, table.Start + table.ElementLength * i + oldOffset, 0);
            token.ChangeData(model, table.Start + table.ElementLength * i + newOffset, value);
         }

         model.ObserveRunWritten(token, newTable);
         return ErrorInfo.NoError;
      }

      public static ErrorInfo RefactorByteToHalfWordInTable(ThumbParser parser, IDataModel model, ModelDelta token, string tableName, string fieldName) {
         // setup
         var table = model.GetTable(tableName) as ArrayRun;
         if (table == null) return new ErrorInfo($"Couldn't find table {tableName}.");
         var segToGrow = table.ElementContent.FirstOrDefault(seg => seg.Name == fieldName);
         if (segToGrow == null) return new ErrorInfo($"Couldn't find field {fieldName} in {tableName}.");
         if (segToGrow.Length != 1) return new ErrorInfo($"{fieldName} must be a 1-byte field to refactor.");
         var fieldIndex = table.ElementContent.IndexOf(segToGrow);
         if (fieldIndex + 1 == table.ElementContent.Count) return new ErrorInfo($"{fieldName} is the last field in the table.");
         var segToReplace = table.ElementContent[fieldIndex + 1];
         if (segToReplace.Length != 1) return new ErrorInfo($"{segToReplace.Name} must be a 1-byte field to be replaced.");
         int fieldOffset = table.ElementContent.Until(seg => seg == segToGrow).Sum(seg => seg.Length);
         if (fieldOffset % 2 != 0) return new ErrorInfo($"{fieldName} must be on an even byte address to extend to a halfword.");

         // update table format
         var format = table.FormatString;
         var newSegFormat = segToGrow.SerializeFormat.ReplaceOne(".", ":");
         format = format.ReplaceOne(segToGrow.SerializeFormat, newSegFormat);
         format = format.ReplaceOne(segToReplace.SerializeFormat, string.Empty);
         var error = ArrayRun.TryParse(model, format, table.Start, table.PointerSources, out var newTable);
         if (error.HasError) return new ErrorInfo($"Failed to create a table from new format {format}: {error.ErrorMessage}.");

         // update code
         var writer = new TupleSegment(default, 5);
         var allChanges = new List<string>();
         foreach (var address in table.FindAllByteReads(parser, fieldIndex)) {
            allChanges.Add(address.ToAddress());
            // ldrb rd, [rn, #]: 01111 # rn rd
            // ldrh rd, [rn, #]: 10001 # rn rd, but # is /2
            writer.Write(model, token, address, 6, fieldOffset / 2); // divide offset by 2
            writer.Write(model, token, address, 11, 0b10001);        // ldrb -> ldrh
         }

         // update table contents
         for (int i = 0; i < table.ElementCount; i++) {
            var value = model[table.Start + table.ElementLength * i + fieldOffset];
            token.ChangeData(model, table.Start + table.ElementLength * i + fieldOffset + 1, 0); // make sure that any old data gets cleared
         }

         model.ObserveRunWritten(token, newTable);
         return ErrorInfo.NoError;
      }

      public static ErrorInfo ReplaceAll(ThumbParser parser, IDataModel model, ModelDelta token, string[] inputCode, string[] outputCode) {
         var search = parser.Compile(model, 0, inputCode);
         var replace = parser.Compile(model, 0, outputCode);
         if (search.Count != replace.Count) return new ErrorInfo($"Input length ({search.Count}) doesn't match output length ({replace.Count}).");
         var results = model.Find(search.ToArray());
         foreach (var result in results) {
            for (int i = 0; i < replace.Count; i++) token.ChangeData(model, result + i, replace[i]);
         }
         return ErrorInfo.NoError;
      }

      public void TabChanged() => CanRunChanged?.Invoke(this, EventArgs.Empty);
   }
}
