using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Linq;

using static HavenSoft.HexManiac.Core.Models.AutoSearchModel;

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

      // 0x58
      private readonly Dictionary<string, int> CanPokemonLearnTmOrHmMove = new Dictionary<string, int> {
         [FireRed] = 0x043C2C,
      };

      // 0x3C
      private readonly Dictionary<string, int> IsMoveHmMove2 = new Dictionary<string, int> {
         [FireRed] = 0x0441B8,
      };

      // 0x4C
      private readonly Dictionary<string, int> Special0x196 = new Dictionary<string, int> {
         [FireRed] = 0x0CC8CC,
      };

      // 0x18
      private readonly Dictionary<string, int> GetTmHmMoveId = new Dictionary<string, int> {
         [FireRed] = 0x125A78,
      };

      // 0x30
      private readonly Dictionary<string, int> IsMoveHmMove1 = new Dictionary<string, int> {
         [FireRed] = 0x125A90,
      };

      // 0xD0
      private readonly Dictionary<string, int> BufferTmHmNameForMenu = new Dictionary<string, int> {
         [FireRed] = 0x131D48,
      };

      // helper functions that I need to use
      private readonly Dictionary<string, int> SetText = new Dictionary<string, int> {
         [FireRed] = 0x008D84,
      };


      public string Name => "Make TMs Expandable";

      public string Description => "The initial games are limited to have no more than 64 TMs+HMs." +
         Environment.NewLine + "This change will allow you to freely add new TMs, up to 256." +
         Environment.NewLine + "It will also split TMs and HMs into separate lists, making them easier to manage." +
         Environment.NewLine + "After this change, TM moves are based on the name given to the TM/HM instead of the item index." +
         Environment.NewLine + "For example, an item named 'TM30' will use the 30th move in the 'tmmoves' table.";

      public event EventHandler CanRunChanged;

      public static (int start, int length) GetCanPokemonLearnTmMoveOffsets(IDataModel model) {
         var gameCode = new string(Enumerable.Range(0xAC, 4).Select(i => ((char)model[i])).ToArray());
         if (gameCode == FireRed || gameCode == LeafGreen) {
            return (0x043C2C, 0x58);
         } else if (gameCode == Ruby || gameCode == Sapphire) {
            return (0x040374, 0x58);
         } else if (gameCode == Emerald) {
            return (0x06E00C, 0x58);
         } else {
            return (-1, 0);
         }
      }

      public static int GetGetMonDataStart(IDataModel model) {
         var gameCode = new string(Enumerable.Range(0xAC, 4).Select(i => ((char)model[i])).ToArray());
         if (gameCode == FireRed || gameCode == LeafGreen) {
            return 0x03FBE8;
         } else if (gameCode == Ruby || gameCode == Sapphire) {
            return 0x03CB60;
         } else if (gameCode == Emerald) {
            return 0x06A518;
         } else {
            return -1;
         }
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

         // TODO detect if any of the 6 functions to change have been modified
         return true;
      }

      public ErrorInfo Run(IViewPort viewPortInterface) {
         var viewPort = (ViewPort)viewPortInterface;

         SplitTmsHms(viewPort);


         CanRunChanged?.Invoke(this, EventArgs.Empty);
         return ErrorInfo.NoError;
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
         table = table.Append(-8);
         model.ObserveAnchorWritten(token, TmMoves, table);
         for (int i = 0; i < 8; i++) model.WriteMultiByteValue(table.Start + table.Length + i * 2, 2, token, 0);

         // add new tmcompatibility and hmcompatibility formats
         viewPort.Edit($"@{compatibilityAddress:X6} ^{TmCompatibility}[pokemon|b[]{TmMoves}]{EggMoveRun.PokemonNameTable} ");
         viewPort.Edit($"@{hmStart:X6} ^{HmCompatibility}[pokemon|b[]{HmMoves}]{EggMoveRun.PokemonNameTable} ");
      }

      // original-new   ->   3C-24
      private void InsertIsMoveHmMove2(ViewPort viewPort, string game) {
         var start = IsMoveHmMove2[game];
         var length = 0x3C;
         var model = viewPort.Model;
         model.ClearFormat(viewPort.CurrentChange, start, length);
         var code = @"
IsMoveHmMove2:
    ldr   r1, [pc, <table>]
    ldr   r3, [pc, <numberOfMoves>]
    lsl   r3, r3, #1
    add   r3, r3, r1
loop:
    cmp   r1, r3
    beq   <fail>
    ldrh  r2, [r1, #0]
    add   r1, #2
    cmp   r0, r2
    bne   loop
    mov   r0, #1
    b     <end>
fail:
    mov   r0, #0
end:
    bx    r14
table:
    .word <hmmoves>
numberOfMoves:
    .word 00
"        .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
         var bytes = viewPort.Tools.CodeTool.Parser.Compile(viewPort.Model, start, code);
         for (int i = 0; i < bytes.Count - 4; i++) viewPort.CurrentChange.ChangeData(model, start + i, bytes[i]);
         viewPort.Edit($"@{(start + bytes.Count - 4):X6} ::hmmoves ");
         for (int i = bytes.Count; i < length; i++) viewPort.CurrentChange.ChangeData(model, start + i, 0x00);
      }

      // original-new   ->   4C-44
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
    mov   r1, #44
    mul   r1, r0
    ldr   r2, [pc, <itemTable>]
    add   r1, r1, r2
    ldrb  r2, [r1, #0]
    ldrb  r3, [r1, #1]
    cmp   r3, #199
    bne   <fail>
    cmp   r2, #206
    beq   <itemIsTmHm>
    cmp   r2, #194
    bne   <fail>
itemIsTmHm:
    bl    <{GetTmHmMoveId[game]:X6}>
    mov   r1, #13
    mul   r1, r0
    ldr   r0, [pc, <movenamesTable>]
    add   r1, r1, r0
    ldr   r0, [pc, <bufferLocation>]
    bl    <{SetText[game]:X6}>
    mov   r0, #1
    b     <end>
fail:
    mov   r0, #0
end:
    pop   pc, {{}}
itemTable:
    <items>
movenamesTable:
    <movenames>
itemIDLocation:
    020370C0
bufferLocation:
    02021CD0
"        .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
         var bytes = viewPort.Tools.CodeTool.Parser.Compile(viewPort.Model, start, code);
         for (int i = 0; i < bytes.Count; i++) viewPort.CurrentChange.ChangeData(model, start + i, bytes[i]);
         for (int i = bytes.Count; i < length; i++) viewPort.CurrentChange.ChangeData(model, start + i, 0x00);
      }

      // original-new   ->   18-
      private void InsertGetTmHmMoveId(ViewPort viewPort, string game) {
         // TODO working here
      }

      // original-new   ->   30-08
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
"        .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
         var bytes = viewPort.Tools.CodeTool.Parser.Compile(viewPort.Model, start, code);
         for (int i = 0; i < bytes.Count; i++) viewPort.CurrentChange.ChangeData(model, start + i, bytes[i]);
         for (int i = bytes.Count; i < length; i++) viewPort.CurrentChange.ChangeData(model, start + i, 0x00);
      }



      public void TabChanged() => CanRunChanged?.Invoke(this, EventArgs.Empty);

      private void InsertRoutine_CanPokemonLearnTmMove(ViewPort viewPort, int address, int originalLength, int subroutineLocation) {
         /*
         CanPokemonLearnTmHm(pokemonData, tm_move)
             push  lr, {r1}              @ 10110101_00000010    B502
             mov   r1, #65               @ 00100_001_01000001   2141
             mov   r2, #0                @ 00100_010_00000000   2200
             bl    <GetMonData>          @ 11111_xxxxxxxxxxx_11110_xxxxxxxxxxx
             pop   {r2}                  @ 10111100_00000100    BC04
             mov   r1, r0                @ 0001110000_000_001   1C01
             mov   r0, #0                @ 00100_000_00000000   2000
             mov   r3, #103              @ 00100_011_01100111   2367
             lsl   r3, r3, #2            @ 00000_00010_011_011  009B
             cmp   r1, r3                @ 0100001010_011_001   4299
             beq   end                   @ 1101_0000_00001100   D00C
             ldr   r0, =tm_compatibility @ 01001_000_00000110   4806
             ldr   r3, =tm_count         @ 01001_011_00000111   4B07
             add   r3, #7                @ 00110_011_00000111   3307
             lsr   r3, r3, #3            @ 00001_00011_011_011  08DB
             mul   r1, r3                @ 0100001101_011_001   4359
             lsr   r3, r2, #3            @ 00001_00011_010_011  08D3
             add   r1, r1, r3            @ 0001100_011_001_001  18C9
             ldrb  r0, [r0, r1]          @ 0101110_001_000_000  5C40
             mov   r1, #7                @ 00100_001_00000111   2107
             and   r1, r2                @ 0100000000_010_001   4011
             mov   r2, #1                @ 00100_010_0000001    2201
             lsl   r2, r1                @ 0100000010_001_010   408A
             and   r0, r2                @ 0100000000_010_000   4010
         end:
             pop   pc                    @ 10111101_00000000    BD00
         tm_compatibility:
             .word <tmcompatibility>
         tm_count:
             .word ::tmmoves
         */

         // subroutine = pc+#*2+4
         // (subroutine-pc-4)/2 = #
         // pc = address + 6
         var number = (subroutineLocation - address - 10) / 2;
         uint branchlink = 0b_11111_00000000000_11110_00000000000;
         branchlink |= (uint)(number & 0b_11111111111_00000000000) >> 11;
         branchlink |= (uint)(number & 0b_11111111111) << 16;
         var bl = new byte[] {
            (byte)branchlink,
            (byte)(branchlink >> 8),
            (byte)(branchlink >> 16),
            (byte)(branchlink >> 24),
         };

         viewPort.Edit($"@{ address:X6} ");
         viewPort.Edit($"02 B5 41 21 00 22 {bl[0]:X2} {bl[1]:X2} {bl[2]:X2} {bl[3]:X2} 04 BC 01 1C 00 20 ");
         viewPort.Edit($"67 23 9B 00 99 42 0C D0 06 48 07 4B 07 33 DB 08 ");
         viewPort.Edit($"59 43 D3 08 C9 18 40 5C 07 21 11 40 01 22 8A 40 ");
         viewPort.Edit($"10 40 00 BD <{TmCompatibility}> ::{TmMoves} ");  // new data only 0x3C long
         for (int i = 0x3C; i < originalLength; i++) viewPort.Edit("00 ");
      }
   }
}
