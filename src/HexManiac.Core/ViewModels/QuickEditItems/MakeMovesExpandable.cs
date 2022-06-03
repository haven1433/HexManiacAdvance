using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Code;
using HavenSoft.HexManiac.Core.Models.Runs;
using static HavenSoft.HexManiac.Core.Models.HardcodeTablesModel;

namespace HavenSoft.HexManiac.Core.ViewModels.QuickEditItems {
   public class MakeMovesExpandable : IQuickEditItem {

      public string Name => "Make Moves Expandable";

      private const string ExpandLevelUpMovesCode = "resources/expand_levelup_moves_code.hma";

      public string Description => "Running this utility will remove the move limiters " +
                                   "and allow for unlimited moves and effects.";

      public string WikiLink => "https://github.com/haven1433/HexManiacAdvance/wiki/Move-Expansion-Explained";

      public event EventHandler CanRunChanged;

      public bool CanRun(IViewPort viewPort) {
         // this ViewPort must be editable
         if (!(viewPort is IEditableViewPort editableViewPort)) return false;

         // the new levelup thumb code must exist
         if (!File.Exists(ExpandLevelUpMovesCode)) return false;

         // we need tables for level-up moves, move names, move stats, and move effects
         foreach (var table in new[] { LevelMovesTableName, MoveDataTable, MoveNamesTable, MoveEffectsTable }) {
            if (viewPort.Model.GetTable(table) == null) return false;
         }

         // the expansion must not have been run yet
         // easy way to check: if the move stats table has 2 bytes for the effect field, it's been tampered with
         var moveStatsTable = viewPort.Model.GetTable(MoveDataTable);
         if (moveStatsTable.ElementContent[0].Length != 1) return false;

         // moves must be 2 bytes each
         var levelMovesTable = viewPort.Model.GetTable(LevelMovesTableName);
         var destination = levelMovesTable.ReadPointer(viewPort.Model, 0);
         var pokemonMoves = viewPort.Model.GetNextRun(destination) as ITableRun;
         if (pokemonMoves == null || pokemonMoves.ElementLength != 2) return false;

         return true;
      }

      public async Task<ErrorInfo> Run(IViewPort viewPortInterface) {
         var viewPort = (IEditableViewPort)viewPortInterface;
         var model = viewPort.Model;
         var token = viewPort.ChangeHistory.CurrentChange;
         var parser = viewPort.Tools.CodeTool.Parser;

         ErrorInfo error;
         error = await ExpandMoveEffects(parser, viewPort, token, .5);
         if (error.HasError) return error;

         // update limiters for move names
         error = ReplaceAll(parser, model, token,
            new[] { "mov r0, #177", "lsl r0, r0, #1", "cmp r1, r0" },
            new[] { "mov r0, #177", "lsl r0, r0, #9", "cmp r1, r0" });
         if (error.HasError) return error;
         await viewPort.UpdateProgress(.6);

         // update levelup moves (update limit from 20 to 25)
         var storeFreeSpaceBuffer = viewPort.Model.FreeSpaceBuffer;
         viewPort.Model.FreeSpaceBuffer = 0;
         using (new StubDisposable { Dispose = () => viewPort.Model.FreeSpaceBuffer = storeFreeSpaceBuffer }) {
            ExpandLevelUpMoveData(viewPort.Model, token);
         }
         await viewPort.UpdateProgress(.7);
         await ExpandLevelUpMoveCode(viewPort, token, .7, 1);

         viewPort.Refresh();
         CanRunChanged?.Invoke(this, EventArgs.Empty);
         return ErrorInfo.NoError;
      }

      public static async Task<ErrorInfo> ExpandMoveEffects(ThumbParser parser, IEditableViewPort viewPort, ModelDelta token, double targetLoadingPercent) {
         // make move effects 2 bytes instead of 1 byte
         var model = viewPort.Model;
         var table = model.GetTable(MoveDataTable);
         var fieldNames = table.ElementContent.Select(seg => seg.Name).ToArray();
         async Task<ErrorInfo> shiftField(int i) {
            var result = RefactorMoveByteFieldInTable(parser, model, token, MoveDataTable, fieldNames[i], i + 1);
            await viewPort.UpdateProgress((9 - i) / 9.0 * targetLoadingPercent);
            return result;
         }

         // effect. power. type. accuracy. pp. effectAccuracy. target. priority. info. unused. unused:
         ErrorInfo error;
         error = await shiftField(8); if (error.HasError) return error;
         error = await shiftField(7); if (error.HasError) return error;
         error = await shiftField(6); if (error.HasError) return error;
         error = await shiftField(5); if (error.HasError) return error;
         error = await shiftField(4); if (error.HasError) return error;
         error = await shiftField(3); if (error.HasError) return error;
         error = await shiftField(2); if (error.HasError) return error;
         error = await shiftField(1); if (error.HasError) return error;
         error = RefactorByteToHalfWordInTable(parser, model, token, MoveDataTable, fieldNames[0]); if (error.HasError) return error;
         await viewPort.UpdateProgress(targetLoadingPercent);

         // update offset pointers (because the PP field moved)
         foreach (OffsetPointerRun pointerRun in table.PointerSources
            .Select(address => model.GetNextRun(address))
            .Where(pointer => pointer is OffsetPointerRun)
         ) {
            model.ClearFormat(token, pointerRun.Start, 4);
            model.WritePointer(token, pointerRun.Start, table.Start + 5);
            model.ObserveRunWritten(token, new OffsetPointerRun(pointerRun.Start, 5));
         }

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
         var ldrbWriter = new TupleSegment(default, 5);
         foreach (var address in table.FindAllByteReads(parser, oldFieldIndex)) {
            var instructionOpCode = model[address + 1] >> 3;
            if (instructionOpCode == 0b01111) {
               // ldrb rd, [rn, #]: 01111 # rn rd
               ldrbWriter.Write(model, token, address, 6, newOffset);
            } else if (instructionOpCode == 0b00100) {
               // mov  rd, #: 00100 rd #
               model[address] = (byte)newOffset;
            }
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
         var search = parser.Compile(model, 0, out var _, inputCode);
         var replace = parser.Compile(model, 0, out var _, outputCode);
         if (search.Count != replace.Count) return new ErrorInfo($"Input length ({search.Count}) doesn't match output length ({replace.Count}).");
         var results = model.Find(search.ToArray());
         foreach (var result in results) {
            for (int i = 0; i < replace.Count; i++) token.ChangeData(model, result + i, replace[i]);
         }
         return ErrorInfo.NoError;
      }

      public static void ExpandLevelUpMoveData(IDataModel model, ModelDelta token) {
         // Note that ALL the data changes before ANY new metadata gets written.
         // This means that edited data blocks will get seen as NoInfoRuns during the write.
         var levelMovesTable = model.GetTable(LevelMovesTableName);
         for (int i = 0; i < levelMovesTable.ElementCount; i++) {
            var pokemonMovesStart = model.ReadPointer(levelMovesTable.Start + levelMovesTable.ElementLength * i);
            if (!(model.GetNextRun(pokemonMovesStart) is TableStreamRun pokemonMovesTable)) continue;

            // calculate the new 4-byte format from the old 2-byte format
            var newData = new byte[pokemonMovesTable.ElementCount * 4];
            for (int j = 0; j < pokemonMovesTable.ElementCount; j++) {
               var (level, move) = PLMRun.SplitToken(model.ReadMultiByteValue(pokemonMovesTable.Start + pokemonMovesTable.ElementLength * j, 2));
               newData[j * 4 + 0] = (byte)move;
               newData[j * 4 + 1] = (byte)(move >> 8);
               newData[j * 4 + 2] = (byte)level;
               newData[j * 4 + 3] = (byte)(level >> 8);
            }

            // write the new 4-byte format into the rom
            var newMovesLocation = model.RelocateForExpansion(token, pokemonMovesTable, newData.Length + 4);
            model.ClearFormat(token, newMovesLocation.Start, newMovesLocation.Length * 2);
            for (int j = 0; j < newData.Length; j++) token.ChangeData(model, newMovesLocation.Start + j, newData[j]);
            for (int j = 0; j < 4; j++) token.ChangeData(model, newMovesLocation.Start + newData.Length + j, 0xFF);

            // update FreeSpaceStart to just after the current run ends.
            var afterThisRun = newMovesLocation.Start + newData.Length + 4;
            model.FreeSpaceStart = Math.Max(model.FreeSpaceStart, afterThisRun);
         }

         // write metadata
         var errorInfo = ArrayRun.TryParse(model, $"[movesFromLevel<[move:{MoveNamesTable} level:]!FFFFFFFF>]{PokemonNameTable}", levelMovesTable.Start, levelMovesTable.PointerSources, out var newTableRun);
         if (errorInfo.HasError) {
            throw new NotImplementedException("There was an unexpected error creating the new table: " + errorInfo.ErrorMessage);
         }
         model.ObserveAnchorWritten(token, LevelMovesTableName, newTableRun);
      }

      public static async Task ExpandLevelUpMoveCode(IEditableViewPort viewPort, ModelDelta token, double loadingStart, double loadingEnd) {
         var script = File.ReadAllText(ExpandLevelUpMovesCode);
         await viewPort.Edit(script, loadingStart, loadingEnd);
      }

      const byte OriginalElementLength = 13, NewElementLength = 17;
      public static void ExpandMoveNameData(IDataModel model, ModelDelta token) {
         var table = model.GetTable(MoveNamesTable);
         table = model.RelocateForExpansion(token, table, table.ElementCount * NewElementLength);
         for (int i = table.ElementCount - 1; i >= 0; i--) {
            var text = table.ReadText(model, i);
            var writeBytes = model.TextConverter.Convert(text, out var _);
            while (writeBytes.Count < NewElementLength) writeBytes.Add(0x00);
            token.ChangeData(model, table.Start + i * NewElementLength, writeBytes.ToArray());
         }
         ArrayRun.TryParse(model, $"[name\"\"{NewElementLength}]{table.ElementCount}", table.Start, table.PointerSources, out var newTable);
         model.ObserveRunWritten(token, newTable);
      }

      public static void ExpandMoveNameCode(IDataModel model, ThumbParser parser, ModelDelta token) {
         var table = (ArrayRun)model.GetTable(MoveNamesTable);
         foreach (var source in table.PointerSources) {
            var funcLoc = source - 2;
            while (funcLoc >= 0 && model[funcLoc] != 0xB5) funcLoc -= 2;
            if (funcLoc < 0) continue;
            for (funcLoc += 2; funcLoc < source; funcLoc += 2) {
               if (model[funcLoc] != OriginalElementLength) continue;
               var loadArrayLine = parser.Parse(model, funcLoc, 2).Trim().SplitLines().Last().Trim();
               // change commands like mov r0, #13 or add r1, #13
               // given the proximity to the name table pointer, the constant is probably used by that table
               if (loadArrayLine.EndsWith(", #" + OriginalElementLength)) {
                  token.ChangeData(model, funcLoc, NewElementLength);
               }
            }
         }
      }

      public void TabChanged() => CanRunChanged?.Invoke(this, EventArgs.Empty);
   }
}
