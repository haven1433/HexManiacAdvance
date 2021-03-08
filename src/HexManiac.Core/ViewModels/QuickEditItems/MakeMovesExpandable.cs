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

      // note that the tablereference does NOT include this information: it includes pointers to these locations.
      private static readonly IReadOnlyDictionary<string, int> OriginalPPPointer = new Dictionary<string, int> {
         [Ruby] = 0x1FB130,
         [Sapphire] = 0x1FB0C0,
         [Ruby1_1] = 0x1FB148,
         [Sapphire1_1] = 0x1FB0D8,
         [FireRed] = 0x250C08,
         [LeafGreen] = 0x250BE4,
         [FireRed1_1] = 0x250C78,
         [LeafGreen1_1] = 0x250C54,
         [Emerald] = 0x31C89C,
      };

      public string Name => "Make Moves Expandable";

      public string Description => "Running this utility will remove the move limiters " +
                                   "and allow the PP pointers to auto-repoint.";

      public string WikiLink => "https://github.com/haven1433/HexManiacAdvance/wiki/Move-Expansion-Explained";

      public event EventHandler CanRunChanged;

      public bool CanRun(IViewPort viewPort) {
         return CanRun1(viewPort);
         // return viewPort is IEditableViewPort;
      }

      public ErrorInfo Run(IViewPort viewPortInterface) {
         //*
         return Run1(viewPortInterface);
         /*/
         var viewPort = (IEditableViewPort)viewPortInterface;
         var token = viewPort.ChangeHistory.CurrentChange;

         var error = RefactorMoveByteFieldInTable(viewPort.Tools.CodeTool.Parser, viewPort.Model, token, MoveDataTable, "power", 9);
         if (error.HasError) return error;
         error = RefactorByteToHalfWordInTable(viewPort.Tools.CodeTool.Parser, viewPort.Model, token, MoveDataTable, "effect");
         if (error.HasError) return error;

         // TODO update limiters
         // TODO update levelup moves

         // TODO
         return ErrorInfo.NoError;
         //*/
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

      public bool CanRun1(IViewPort viewPort) {
         if (!(viewPort is ViewPort)) return false;
         var model = viewPort.Model;
         var moveDataAddress = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, MoveDataTable);
         if (moveDataAddress == Pointer.NULL) return false;
         var limiterCode = viewPort.Tools.CodeTool.Parser.Compile(model, 0, @"
    mov   r0, #177
    lsl   r0, r0, #1
    cmp   r1, r0
".Split(Environment.NewLine)).ToArray();
         return model.ThumbFind(limiterCode).Any();
      }

      public ErrorInfo Run1(IViewPort viewPortInterface) {
         if (!(viewPortInterface is ViewPort viewPort)) return new ErrorInfo("Can only run move expansion on an editable tab.");
         var model = viewPort.Model;
         var game = model.GetGameCode();
         var thumb = viewPort.Tools.CodeTool.Parser;
         var token = viewPort.CurrentChange;

         var moveDataAddress = model.GetAddressFromAnchor(token, -1, MoveDataTable);
         if (moveDataAddress == Pointer.NULL) return new ErrorInfo($"Expanding moves requires the existence of a '{MoveDataTable}' table.");
         var originalPpPointer = OriginalPPPointer[game];
         var sourcesToPP = model.FindPointer(originalPpPointer).ToList();
         if (sourcesToPP.Count != 5) {
            var originalCount = sourcesToPP.Count;
            sourcesToPP = model.FindPointer(moveDataAddress + 4).ToList();
            if (sourcesToPP.Count != 5) {
               return new ErrorInfo($"Expanding moves utility expects there to be 5 pointers to PP moves, but there were {originalCount} to {originalPpPointer:X6} and {sourcesToPP.Count} to {(moveDataAddress + 4):X6}.");
            }
         }

         // update code that uses the pp-pointer to do pointer+4 instead
         var codeUsingPpPointer = viewPort.Tools.CodeTool.Parser.Compile(model, 0, @"
    lsl   r2, r0, #1
    add   r2, r2, r0
    lsl   r2, r2, #2
".Split(Environment.NewLine)).ToArray();
         var changedCodeForPpPointer = viewPort.Tools.CodeTool.Parser.Compile(model, 0, @"
    mov   r2, #12
    mul   r2, r0
    add   r2, r2, #4
".Split(Environment.NewLine)).ToArray();
         var codePotentiallyUsingPpPointer = model.ThumbFind(codeUsingPpPointer).ToList();
         foreach (var pointer in sourcesToPP) {
            var codeForPointer = codePotentiallyUsingPpPointer.Where(code => code < pointer).Last();
            for (int i = 0; i < changedCodeForPpPointer.Length; i++) {
               token.ChangeData(model, codeForPointer + i, changedCodeForPpPointer[i]);
            }
         }

         // update limiter: multiply by 512 instead of 2
         var limiterCode = viewPort.Tools.CodeTool.Parser.Compile(model, 0, @"
    mov   r0, #177
    lsl   r0, r0, #1
    cmp   r1, r0
".Split(Environment.NewLine)).ToArray();
         var newLimiterCode = viewPort.Tools.CodeTool.Parser.Compile(model, 0, @"
    mov   r0, #177
    lsl   r0, r0, #9
    cmp   r1, r0
".Split(Environment.NewLine)).ToArray();
         int limiterCount = 0;
         foreach (var limiter in model.Find(limiterCode)) {
            for (int i = 0; i < changedCodeForPpPointer.Length; i++) {
               token.ChangeData(model, limiter + i, newLimiterCode[i]);
            }
            limiterCount += 1;
            viewPort.Goto.Execute(limiter);
         }

         // update PP pointers
         foreach (var source in sourcesToPP) {
            model.ClearFormat(token, source, 4);
            model.WritePointer(token, source, moveDataAddress);
            model.ObserveRunWritten(token, new PointerRun(source));
         }

         return new ErrorInfo($"Update {sourcesToPP.Count} PP pointers and {limiterCount} limiters.", isWarningLevel: true);
      }

      public void TabChanged() => CanRunChanged?.Invoke(this, EventArgs.Empty);
   }
}
