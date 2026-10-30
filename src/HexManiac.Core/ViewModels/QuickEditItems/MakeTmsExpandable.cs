using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static HavenSoft.HexManiac.Core.Models.HardcodeTablesModel;

namespace HavenSoft.HexManiac.Core.ViewModels.QuickEditItems {
   // TODO for Emerald, update CanSpeciesLearnTmHm also
   /// <summary>
   /// The default implementation of converting a TM item ID to a TM move ID is to use an item offset.
   /// This means that all the TM and HM moves are stored in a single list, and all the TM/HM items must be sequential.
   ///
   /// MakingTmsExpandable will change the implementation to be based on item names instead.
   /// So 'TM30' will use the 30th move in the tmmove table, no matter which item number it is.
   /// To make this work for arbitrary numbers of TMs and HMs, HM moves/compatibility are split out into a separate table.
   /// This requires updating 2 tables and 6 functions.
   /// </summary>
   public class MakeTmsExpandable : IQuickEditItem {
      public const string HmCompatibility = "hmcompatibility";

      #region FunctionLocations

      // helper function used by Special0x196
      private readonly Dictionary<string, int> SetText = new Dictionary<string, int> {
         [FireRed] = 0x008D84,
      };

      // helper function used by CanPokemonLearnTmOrHmMove
      private readonly Dictionary<string, int> ReadPokeData = new Dictionary<string, int> {
         [FireRed] = 0x03FBE8,
         [LeafGreen] = 0x03FBE8,
         [Ruby] = 0x03CB60,
         [Sapphire] = 0x03CB60,
         [Emerald] = 0x06A518,
      };

      // magic strings needed for doing menu text buffering
      private readonly Dictionary<string, int[]> MagicBufferStrings = new Dictionary<string, int[]> {
         [FireRed] = new[] { 0x4166FF, 0x463178, 0x416226, 0x46317C, 0x416703 },
      };

      // 0x58
      private readonly Dictionary<string, int> CanPokemonLearnTmOrHmMove = new Dictionary<string, int> {
         [FireRed] = 0x043C2C,
         [LeafGreen] = 0x043C2C,
         [Ruby] = 0x040374,
         [Sapphire] = 0x040374,
         [Emerald] = 0x06E00C,
      };

      // 0x3C
      private readonly Dictionary<string, int> IsMoveHmMove2 = new Dictionary<string, int> {
         [FireRed] = 0x0441B8,
         [Emerald] = 0x06E804,
      };

      // 0x4C
      private readonly Dictionary<string, int> Special0x196 = new Dictionary<string, int> {
         [FireRed] = 0x0CC8CC,
         [Emerald] = 0x1398C0,
      };

      // 0x18
      private readonly Dictionary<string, int> GetBattleMoveFromItemNumber = new Dictionary<string, int> {
         [FireRed] = 0x125A78,
         [Emerald] = 0x1B6CFC,
      };

      // 0x30
      private readonly Dictionary<string, int> IsMoveHmMove1 = new Dictionary<string, int> {
         [FireRed] = 0x125A90,
         [Emerald] = 0x1B6D14,
      };

      // 0xD0
      private readonly Dictionary<string, int> BufferTmHmNameForMenu = new Dictionary<string, int> {
         [FireRed] = 0x131D48,
         // [Emerald] = null,
      };

      // newly created functions
      private readonly Dictionary<string, int> ConvertItemPointerToTmHmBattleMoveId, IsItemTmHm, ParseNumber, ReadBitArray, IsItemTmHm2;

      #endregion

      public string Name => "Make TMs Expandable";

      public string Description => "The initial games are limited to have no more than 64 TMs+HMs." +
         Environment.NewLine + "This change will allow you to freely add new TMs, up to 256." +
         Environment.NewLine + "It will also split TMs and HMs into separate lists, making them easier to manage." +
         Environment.NewLine + "After this change, TM moves are based on the name given to the TM/HM instead of the item index." +
         Environment.NewLine + "For example, an item named 'TM30' will use the 30th move in the 'data.pokemon.moves.tms' table.";

      public string WikiLink => throw new NotImplementedException();

      public event EventHandler CanRunChanged;

      public MakeTmsExpandable() {
         ConvertItemPointerToTmHmBattleMoveId = new Dictionary<string, int>();
         IsItemTmHm = new Dictionary<string, int>();
         ParseNumber = new Dictionary<string, int>();
         ReadBitArray = new Dictionary<string, int>();
         IsItemTmHm2 = new Dictionary<string, int>();

         // ReadBitArray fits after IsMoveHmMove2
         foreach (var pair in IsMoveHmMove2) ReadBitArray.Add(pair.Key, pair.Value + 0x24);

         // ParseNumber goes after Special196
         foreach (var pair in Special0x196) ParseNumber.Add(pair.Key, pair.Value + 0x30);

         // IsItemTmHm goes after IsMoveHmMove1
         foreach (var pair in IsMoveHmMove1) IsItemTmHm.Add(pair.Key, pair.Value + 0x8);

         // ConvertItemPointerToTmHmBattleMoveId goes after BufferTmHmNameForMenu
         foreach (var pair in BufferTmHmNameForMenu) ConvertItemPointerToTmHmBattleMoveId.Add(pair.Key, pair.Value + 0x6C);

         // IsItemTmHm2 goes after ConvertItemPointerToTmHmBattleMoveId
         foreach (var pair in ConvertItemPointerToTmHmBattleMoveId) IsItemTmHm2.Add(pair.Key, pair.Value + 0x24);
      }

      public bool CanRun(IViewPort viewPortInterface) {
         // require that I have a tab with real data, not a search tab or a diff tab or something
         if (!(viewPortInterface is ViewPort viewPort)) return false;
         var model = viewPort.Model;

         // require that this data has a tmmoves / hmmoves / tmcompatibility table, since we're messing with those
         var tmmoves = model.GetAddressFromAnchor(viewPort.CurrentChange, -1, TmMoves);
         var hmmoves = model.GetAddressFromAnchor(viewPort.CurrentChange, -1, HmMoves);
         var tmcompatibility = model.GetAddressFromAnchor(viewPort.CurrentChange, -1, TmCompatibility);
         if (tmmoves == Pointer.NULL || hmmoves == Pointer.NULL || tmcompatibility == Pointer.NULL) return false;
         // also require that their length is 58/8, since that being false means something else has already messed with them.
         if ((model.GetNextRun(tmmoves) as ArrayRun)?.ElementCount != 58) return false;
         if ((model.GetNextRun(hmmoves) as ArrayRun)?.ElementCount != 8) return false;

         // TODO detect if any of the functions to change have been modified
         return true;
      }

      public Task<ErrorInfo> Run(IViewPort viewPortInterface) {
         var viewPort = (ViewPort)viewPortInterface;
         var model = viewPort.Model;
         var gameCode = model.GetGameCode();

         SplitTmsHms(viewPort);
         InsertCanPokemonLearnTmOrHmMove(viewPort, gameCode);
         InsertIsMoveHmMove2(viewPort, gameCode);
         InsertReadBitArray(viewPort, gameCode);
         InsertSpecial196(viewPort, gameCode);
         InsertParseNumber(viewPort, gameCode);
         InsertGetBattleMoveFromItemNumber(viewPort, gameCode);
         InsertIsMoveHmMove1(viewPort, gameCode);
         InsertIsItemTmHm(viewPort, gameCode);
         InsertBufferTmHmNameForMenu(viewPort, gameCode);
         InsertConvertItemPointerToTmHmBattleMoveId(viewPort, gameCode);
         InsertIsItemTmHm2(viewPort, gameCode);
         PatchItemRemovalFunctions(viewPort, gameCode);

         CanRunChanged?.Invoke(this, EventArgs.Empty);
         return Task.FromResult(ErrorInfo.NoError);
      }

      /// <summary>
      /// Before we do any code changes, split the TMs and HMs into two separate lists.
      /// </summary>
      private void SplitTmsHms(ViewPort viewPort) {
         var model = viewPort.Model;
         var token = viewPort.CurrentChange;
         var compatibilityAddress = model.GetAddressFromAnchor(token, -1, TmCompatibility);
         var tmMovesAddress = model.GetAddressFromAnchor(token, -1, TmMoves);
         var table = (ArrayRun)model.GetNextRun(compatibilityAddress);

         // clear the existing format
         model.ClearFormat(token, table.Start, table.Length);

         // extract all the HM compatibilies to a separate list
         var newTableData = new byte[table.ElementCount];
         for (int i = 0; i < table.ElementCount; i++) {
            var index = table.Start + 6 + i * table.ElementLength;
            var hmCompatibility = model.ReadMultiByteValue(index, 2) >> 2;
            newTableData[i] = (byte)hmCompatibility;
            var lowValue = (byte)(model[index] & 3); // only keep the bottom 2 bits: TM49 and TM50
            token.ChangeData(model, index, lowValue);
            token.ChangeData(model, index + 1, 0);
         }

         // condense all the TM compatibilities by 1 byte
         for (int i = 1; i < table.ElementCount; i++) {
            var a = table.Start + i * (table.ElementLength - 1);
            var b = table.Start + i * table.ElementLength;
            for (int j = 0; j < table.ElementLength - 1; j++) {
               token.ChangeData(model, a + j, model[b + j]);
            }
         }

         // place all the HM compatibilities after the TM compatibilities
         var hmStart = table.Start + table.Length - table.ElementCount;
         for (int i = 0; i < table.ElementCount; i++) {
            token.ChangeData(model, hmStart + i, newTableData[i]);
         }

         // clear HMs from the TmMove table
         table = (ArrayRun)model.GetNextRun(tmMovesAddress);
         table = table.Append(token, -8);
         model.ObserveAnchorWritten(token, TmMoves, table);
         for (int i = 0; i < 8; i++) model.WriteMultiByteValue(table.Start + table.Length + i * 2, 2, token, 0);

         // add new tmcompatibility and hmcompatibility formats
         viewPort.Edit($"@{compatibilityAddress:X6} ^{TmCompatibility}[pokemon|b[]{TmMoves}]{PokemonNameTable} ");
         viewPort.Edit($"@{hmStart:X6} ^{HmCompatibility}[pokemon|b[]{HmMoves}]{PokemonNameTable} ");
      }

      private IReadOnlyList<byte> Compile(ViewPort viewPort, int start, string[] code) {
         return viewPort.Tools.CodeTool.Parser.Compile(viewPort.CurrentChange, viewPort.Model, start, code);
      }

      // original-new   ->   58-58
      private void InsertCanPokemonLearnTmOrHmMove(ViewPort viewPort, string game) {
         var model = viewPort.Model;
         var start = CanPokemonLearnTmOrHmMove[game];
         var length = 0x58;
         model.ClearFormat(viewPort.CurrentChange, start, length);
         var code = $@"
CanPokemonLearnTmOrHmMove:
    push lr, {{r4-r5}}
    mov  r4, r1
    mov  r1, #65
    mov  r2, #0
    bl   <{ReadPokeData[game]:X6}>
    add  r4, #250
    add  r4, #39
    mov  r2, #210
    add  r2, #202
    cmp  r0, r2
    beq  <Fail>
    mov  r5, r0
    mov  r0, r4
    bl   <{IsItemTmHm[game]:X6}>
    cmp  r1, #0
    beq  <Fail>
    mov  r4, r1
    add  r0, #2
    bl   <{ParseNumber[game]:X6}>
    sub  r0, #1
    mov  r1, r5
    cmp  r4, #1
    beq  <CheckTmCompatibility>
CheckHmCompatibility:
    ldr  r2, [pc, <HmCompatibilityTable>]
    ldr  r3, [pc, <HmMoveCount>]
    b    <UseTables>
CheckTmCompatibility:
    ldr  r2, [pc, <TmCompatibilityTable>]
    ldr  r3, [pc, <TmMoveCount>]
UseTables:
    bl   <{ReadBitArray[game]:X6}>
    b    <End>
Fail:
    mov   r0, #0
End:
    pop   pc, {{r4-r5}}
HmCompatibilityTable:
    .word <hmcompatibility>
HmMoveCount:
    .word 00
TmCompatibilityTable:
    .word <{TmCompatibility}>
TmMoveCount:
    .word 00
".Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
         var bytes = Compile(viewPort, start, code);
         viewPort.Edit($"@{(start + bytes.Count - 4 * 4):X6} <> ::{HmMoves} <> ::{TmMoves} ");
      }

      // original-new   ->   3C-24*
      private void InsertIsMoveHmMove2(ViewPort viewPort, string game) {
         var start = IsMoveHmMove2[game];
         var length = 0x3C;
         var model = viewPort.Model;
         model.ClearFormat(viewPort.CurrentChange, start, length);
         var code = $@"
IsMoveHmMove2:
    ldr   r1, [pc, <table>]
    ldr   r3, [pc, <numberOfMoves>]
    lsl   r3, r3, #1
    add   r3, r3, r1
Loop:
    cmp   r1, r3
    beq   <Fail>
    ldrh  r2, [r1, #0]
    add   r1, #2
    cmp   r0, r2
    bne   <Loop>
    mov   r0, #1
    b     <End>
Fail:
    mov   r0, #0
End:
    bx    r14
table:
    .word <{HmMoves}>
numberOfMoves:
    .word 00
".Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
         var bytes = Compile(viewPort, start, code);
         viewPort.Edit($"@{(start + bytes.Count - 4 * 2):X6} <> ::{HmMoves} ");
      }

      // added          ->   3C-3C (+18)
      private void InsertReadBitArray(ViewPort viewPort, string game) {
         var start = ReadBitArray[game];
         var model = viewPort.Model;
         var code = @"
ReadBitArray:
    add   r3, #7
    lsr   r3, r3, #3
    mul   r1, r3
    lsr   r3, r0, #3
    add   r1, r1, r3
    ldrb  r1, [r1, r2]
    mov   r2, #7
    and   r2, r0
    mov   r0, #1
    lsl   r0, r2
    and   r0, r1
    bx    r14
".Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
         Compile(viewPort, start, code);
      }

      // original-new   ->   4C-30*
      private void InsertSpecial196(ViewPort viewPort, string game) {
         var start = Special0x196[game];
         var length = 0x4C;
         var model = viewPort.Model;
         model.ClearFormat(viewPort.CurrentChange, start, length);
         var code = $@"
Special196:
    push  lr, {{}}
    ldr   r0, [pc, <itemIDLocation>]
    ldrh  r0, [r0, #0]
    bl    <{GetBattleMoveFromItemNumber[game]:X6}>
    cmp   r0, #0
    beq   <Fail>
    mov   r1, #13
    mul   r1, r0
    ldr   r0, [pc, <movenamesTable>]
    add   r1, r1, r0
    ldr   r0, [pc, <bufferLocation>]
    bl    <{SetText[game]:X6}>
    mov   r0, #1
    b     <End>
Fail:
    mov   r0, #0
End:
    pop   pc, {{}}
movenamesTable:
    .word <{MoveNamesTable}>
itemIDLocation:
    .word 020370C0
bufferLocation:
    .word 02021CD0
".Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
         var bytes = Compile(viewPort, start, code);
         for (int i = bytes.Count; i < length; i++) viewPort.CurrentChange.ChangeData(model, start + i, 0x00);
         viewPort.Edit($"@{(start + bytes.Count - 4 * 3):X6} <> ");
      }

      // added          ->   4C-48 (+18)
      private void InsertParseNumber(ViewPort viewPort, string game) {
         var start = ParseNumber[game];
         var model = viewPort.Model;
         var code = $@"
ParseNumber:
    mov  r2, #0
    mov  r3, #10
Loop:
    ldrb r1, [r0, #0]
    cmp  r1, #255
    beq  <Done>
    sub  r1, #161
    mul  r2, r3
    add  r2, r2, r1
    add  r0, #1
    b    <Loop>
Done:
    mov  r0, r2
    bx   r14
".Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
         var bytes = Compile(viewPort, start, code);
         for (int i = 0; i < bytes.Count; i++) viewPort.CurrentChange.ChangeData(model, start + i, bytes[i]);
      }

      // original-new   ->   18-14
      private void InsertGetBattleMoveFromItemNumber(ViewPort viewPort, string game) {
         var start = GetBattleMoveFromItemNumber[game];
         var length = 0x18;
         var model = viewPort.Model;
         model.ClearFormat(viewPort.CurrentChange, start, length);
         var code = $@"
GetBattleMoveFromItemNumber:
    push  lr, {{}}
    bl    <{IsItemTmHm[game]:X6}>
    cmp   r1, #0
    beq   <Fail>
    bl    <{ConvertItemPointerToTmHmBattleMoveId[game]:X6}>
    b     <End>
Fail:
    mov   r0, #0
End:
    pop   pc, {{}}
".Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
         var bytes = Compile(viewPort, start, code);
         for (int i = bytes.Count; i < length; i++) viewPort.CurrentChange.ChangeData(model, start + i, 0x00);
      }

      // original-new   ->   30-08*
      private void InsertIsMoveHmMove1(ViewPort viewPort, string game) {
         var model = viewPort.Model;
         var start = IsMoveHmMove1[game];
         var length = 0x30;
         model.ClearFormat(viewPort.CurrentChange, start, length);
         var code = $@"
IsMoveHmMove1:
    ldr  r1, [pc, <mainroutine>]
    bx   r1
mainroutine:
    .word <{(IsMoveHmMove2[game] + 1):X6}>
".Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
         var bytes = Compile(viewPort, start, code);
         for (int i = bytes.Count; i < length; i++) viewPort.CurrentChange.ChangeData(model, start + i, 0x00);
      }

      // added          ->   30-30 (+28)
      private void InsertIsItemTmHm(ViewPort viewPort, string game) {
         var start = IsItemTmHm[game];
         var model = viewPort.Model;
         // input:  r0 = itemID
         // result: r0 = pointer to the item
         //         r1 = {0,1,2} for {none, isTm, isHm}
         var code = $@"
IsItemTmHm:
    mov  r1, #44
    mul  r0, r1
    ldr  r1, [pc, <ItemsTable>]
    add  r0, r0, r1
    ldrb r2, [r0, #1]
    cmp  r2, #199
    bne  <Fail>
    ldrb r2, [r0, #0]
    cmp  r2, #206
    beq  <IsTm>
    cmp  r2, #194
    bne  <Fail>
IsHm:
    mov  r1, #2
    b    <End>
IsTm:
    mov  r1, #1
    b    <End>
Fail:
    mov  r1, #0
End:
    bx   r14
ItemsTable:
    .word <{ItemsTableName}>
".Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
         var bytes = Compile(viewPort, start, code);
         viewPort.Edit($"@{(start + bytes.Count - 4 * 1):X6} <> ");
      }

      // original-new   ->   D0-6C*
      private void InsertBufferTmHmNameForMenu(ViewPort viewPort, string game) {
         var model = viewPort.Model;
         var start = BufferTmHmNameForMenu[game];
         var length = 0xD0;
         model.ClearFormat(viewPort.CurrentChange, start, length);
         var code = $@"
BufferTmHmNameForMenu(address, itemID):
    push lr, {{r4-r5}}
    mov  r4, r1
    ldr  r5, [pc, <ItemsTable>]
    ldr  r1, [pc, <MagicString0>]
    bl   <{SetText[game]:X6}>
    mov  r2, #44
    mul  r2, r4
    add  r5, r5, r2
    ldrb r2, [r5, #0]
    cmp  r2, #206
    beq  <CaseTm>
    ldr  r1, [pc, <MagicString1>]
    bl   <{SetText[game]:X6}>
CaseTm:
    ldr  r1, [pc, <MagicString2>]
    bl   <{SetText[game]:X6}>
    add  r1, r5, #2
    bl   <{SetText[game]:X6}>
    ldr  r1, [pc, <MagicString3>]
    bl   <{SetText[game]:X6}>
    ldr  r1, [pc, <MagicString4>]
    bl   <{SetText[game]:X6}>
    mov  r5, r0
    mov  r0, r4
    bl   <{GetBattleMoveFromItemNumber[game]:X6}>
    mov  r1, #13
    mul  r0, r1
    ldr  r1, [pc, <MovesTable>]
    add  r1, r1, r0
    mov  r0, r5
    bl   <{SetText[game]:X6}>
    pop  pc, {{r4-r5}}
    nop
ItemsTable:
    .word <{ItemsTableName}>
MagicString0:
    .word <{MagicBufferStrings[game][0]:X6}>
MagicString1:
    .word <{MagicBufferStrings[game][1]:X6}>
MagicString2:
    .word <{MagicBufferStrings[game][2]:X6}>
MagicString3:
    .word <{MagicBufferStrings[game][3]:X6}>
MagicString4:
    .word <{MagicBufferStrings[game][4]:X6}>
MovesTable:
    .word <{MoveNamesTable}>
".Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
         var bytes = Compile(viewPort, start, code);
         for (int i = bytes.Count; i < length; i++) viewPort.CurrentChange.ChangeData(model, start + i, 0x00);
         viewPort.Edit($"@{(start + bytes.Count - 4 * 7):X6} <> ");
         viewPort.Edit($"@{(start + bytes.Count - 4 * 1):X6} <> ");
      }

      // added          ->   D0-90*(+24)
      private void InsertConvertItemPointerToTmHmBattleMoveId(ViewPort viewPort, string game) {
         var model = viewPort.Model;
         var start = ConvertItemPointerToTmHmBattleMoveId[game];
         var code = $@"
ConvertItemPointerToTmHmBattleMoveId:
    push  lr, {{r4}}
    cmp   r1, #1
    beq   <LoadTmTable>
LoadHmTable:
    ldr   r4, [pc, <HmMovesTable>]
    b     <DoParse>
LoadTmTable:
    ldr   r4, [pc, <TmMovesTable>]
DoParse:
    add   r0, #2
    bl    <{ParseNumber[game]:X6}>
    sub   r0, #1
    lsl   r0, r0, #1
    ldrh  r0, [r4, r0]
    pop   pc, {{r4}}
    nop
TmMovesTable:
    .word <{TmMoves}>
HmMovesTable:
    .word <{HmMoves}>
".Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
         var bytes = Compile(viewPort, start, code);
         viewPort.Edit($"@{(start + bytes.Count - 4 * 2):X6} <> <> ");
      }

      // added          ->   D0-98 (+8)
      private void InsertIsItemTmHm2(ViewPort viewPort, string game) {
         // does the same thing as IsItemTmHm, but leaves r0 alone.
         // r1 is {0,1,2} for {none,tm,hm} and r0 is the itemID.
         var model = viewPort.Model;
         var start = IsItemTmHm2[game];
         var code = $@"
IsItemTmHm2:
    push  lr, {{r0}}
    bl    <{IsItemTmHm[game]:X6}>
    pop   pc, {{r0}}
".Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
         var bytes = Compile(viewPort, start, code);
      }

      private void PatchItemRemovalFunctions(ViewPort viewPort, string game) {
         var model = viewPort.Model;
         var deleteLocation = 0x09A1D8; // ReduceItemCount[game]
         var start = 0x124F6A; // ItemRemoval1[game];
         var length = 9 * 2;
         var code = $@"
    ldrh  r0, [r7, #0]
    bl    <{IsItemTmHm[game]:X6}>
    cmp   r1, #2
    beq   <{(start + length):X6}>
    ldrh  r0, [r7, #0]
    mov   r1, #1
    bl    <{deleteLocation:X6}>
".Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
         Compile(viewPort, start, code);

         start = 0x125C74; // ItemRemoval2[game];
         length = 8 * 2;
         code = $@"
    mov   r0, r4
    bl    <{IsItemTmHm2[game]:X6}>
    cmp   r1, #2
    beq   <{(start + length):X6}>
    mov   r1, #1
    bl    <{deleteLocation:X6}>
".Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
         Compile(viewPort, start, code);
      }

      public void TabChanged() => CanRunChanged?.Invoke(this, EventArgs.Empty);
   }
}
