using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Linq;

using static HavenSoft.HexManiac.Core.Models.AutoSearchModel;

namespace HavenSoft.HexManiac.Core.ViewModels {
   public interface IQuickEditItem {
      string Name { get; }
      string Description { get; }

      event EventHandler CanRunChanged;
      bool CanRun(IViewPort viewPort);
      ErrorInfo Run(IViewPort viewPort);
      void TabChanged();
   }

   public class MakeTutorsExpandable : IQuickEditItem {
      public string Name => "Make Tutors Expandable";
      public string Description => "The initial games limited to have exactly 18 (FireRed) or no more than 32 (Emerald) tutors." +
               Environment.NewLine + "This change will allow you to freely add new tutors, up to 256.";

      public event EventHandler CanRunChanged;

      public bool CanRun(IViewPort viewPortInterface) {
         // require that I have a tab with real data, not a search tab or a diff tab or something
         if (!(viewPortInterface is ViewPort viewPort)) return false;

         // require that this data actually supports this change
         var model = viewPort.Model;
         var (getTutorMove, canPokemonLearnTutorMove, getTutorMove_Length, canPokemonLearnTutorMove_Length) = GetOffsets(viewPort);
         if (getTutorMove < 0 || canPokemonLearnTutorMove < 0) return false;

         // require that this data has a tutormoves and tutorcompatibility table, since we're messing with those
         var tutormoves = model.GetAddressFromAnchor(viewPort.CurrentChange, -1, MoveTutors);
         var tutorcompatibility = model.GetAddressFromAnchor(viewPort.CurrentChange, -1, TutorCompatibility);
         if (tutormoves == Pointer.NULL || tutorcompatibility == Pointer.NULL) {
            return false;
         }

         // if the patch has already been applied, you can't apply it again
         if (viewPort.Model.GetNextRun(canPokemonLearnTutorMove + 0x20) is WordRun) return false;
         return true;
      }

      public ErrorInfo Run(IViewPort viewPortInterface) {
         var viewPort = (ViewPort)viewPortInterface;
         var model = viewPort.Model;
         var (getTutorMove, canPokemonLearnTutorMove, getTutorMove_Length, canPokemonLearnTutorMove_Length) = GetOffsets(viewPort);
         var tutormoves = model.GetAddressFromAnchor(viewPort.CurrentChange, -1, MoveTutors);
         var tutorcompatibility = model.GetAddressFromAnchor(viewPort.CurrentChange, -1, TutorCompatibility);

         InsertRoutine_GetTutorMove(viewPort, getTutorMove, getTutorMove_Length);
         InsertRoutine_CanPokemonLearnTutorMove(viewPort, canPokemonLearnTutorMove, canPokemonLearnTutorMove_Length);

         CanRunChanged?.Invoke(this, EventArgs.Empty);

         return ErrorInfo.NoError;
      }

      public void TabChanged() => CanRunChanged?.Invoke(this, EventArgs.Empty);

      public static (int getTutorMove, int canPokemonLearnTutorMove, int getTutorMove_Length, int canPokemonLearnTutorMove_Length) GetOffsets(ViewPort viewPort) {
         var model = viewPort.Model;
         var gameCode = new string(Enumerable.Range(0xAC, 4).Select(i => ((char)model[i])).ToArray());
         if (gameCode == FireRed) {
            return (0x120BA8, 0x120BE8, 0x40, 0x54);
         } else if (gameCode == LeafGreen) {
            return (0x120B80, 0x120BC0, 0x40, 0x54);
         } else if (gameCode == Emerald) {
            return (0x1B2360, 0x1B2370, 0x10, 0x2C);
         } else {
            return (-1, -1, 0, 0);
         }
      }

      private void InsertRoutine_GetTutorMove(ViewPort viewPort, int address, int originalLength) {
         /*
         GetTutorMove(index)
             lsl   r0, r0, #1            @ 00000_00001_000_000   0040
             ldr   r1, =move_tutor_list  @ 01001_001_00000001    4901
             ldrh  r0, [r0, r1]          @ 0101101_001_000_000   5A40
             bx    lr                    @ 010001110_1_110_000   4770
         move_tutor_list:
             .word <tutormoves>
         */

         viewPort.Edit($"@{address:X6} 40 00 01 49 40 5A 70 47 <{MoveTutors}> "); // new data only 0xC long
         for (int i = 0x0C; i < originalLength; i++) viewPort.Edit("00 ");
      }

      private void InsertRoutine_CanPokemonLearnTutorMove(ViewPort viewPort, int address, int originalLength) {
         /*
         CanPokemonLearnTutorMove(pokemon, tutor_move)
             ldr     r2, =move_tutor_count          @ 01001_010_00000111    4A07
             add     r2, #7                         @ 00110_010_00000111    3207
             lsr     r2, r2, #3                     @ 00001_00011_010_010   08D2
             mul     r0, r2                         @ 0100001101_010_000    4350
             lsr     r2, r1, #3                     @ 00001_00011_001_010   08CA
             add     r0, r0, r2                     @ 0001100_010_000_000   1880
             mov     r2, #7                         @ 00100_010_00000111    2207
             and     r1, r2                         @ 0100000000_010_001    4011
             ldr     r2, =move_tutor_compatibility  @ 01001_010_00000010    4A02
             ldrb    r0, [r2, r0]                   @ 0101110_000_010_000   5C10
             lsr     r0, r1                         @ 0100000011_001_000    40C8
             mov     r2, #1                         @ 00100_010_00000001    2201
             and     r0, r2                         @ 0100000000_010_000    4010
             bx      lr                             @ 010001110_1_110_000   4770
         move_tutor_compatibility:
             .word <tutorcompatibility>
         move_tutor_count:
             .word ::tutormoves
         */
         viewPort.Edit($"@{address:X6} ");
         viewPort.Edit("07 4A 07 32 D2 08 50 43 CA 08 80 18 07 22 11 40 ");
         viewPort.Edit("02 4A 10 5C C8 40 01 22 10 40 70 47 ");
         viewPort.Edit($"<{TutorCompatibility}> ::{MoveTutors} ");  // new data only 0x24 long
         for (int i = 0x24; i < originalLength; i++) viewPort.Edit("00 ");
      }
   }

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

         //var model = viewPort.Model;
         //var (start, length) = GetCanPokemonLearnTmMoveOffsets(model);
         //var getMonData_start = GetGetMonDataStart(model);
         //var tmmoves = model.GetAddressFromAnchor(viewPort.CurrentChange, -1, TmMoves);
         //var tmcompatibility = model.GetAddressFromAnchor(viewPort.CurrentChange, -1, TmCompatibility);
         //InsertRoutine_CanPokemonLearnTmMove(viewPort, start, length, getMonData_start);

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

         viewPort.Edit($"@{address:X6} ");
         viewPort.Edit($"02 B5 41 21 00 22 {bl[0]:X2} {bl[1]:X2} {bl[2]:X2} {bl[3]:X2} 04 BC 01 1C 00 20 ");
         viewPort.Edit($"67 23 9B 00 99 42 0C D0 06 48 07 4B 07 33 DB 08 ");
         viewPort.Edit($"59 43 D3 08 C9 18 40 5C 07 21 11 40 01 22 8A 40 ");
         viewPort.Edit($"10 40 00 BD <{TmCompatibility}> ::{TmMoves} ");  // new data only 0x3C long
         for (int i = 0x3C; i < originalLength; i++) viewPort.Edit("00 ");
      }
   }

   public class MakeItemsExpandable : IQuickEditItem {
      public string Name => "Make Items Expandable";

      public string Description => "The initial games have functions that do out-of-bounds checks on item IDs using a hard-coded number of items." +
         Environment.NewLine + "This change will allow HexManiac to update those functions as you to expand the number of items in the game.";

      public event EventHandler CanRunChanged;

      public static int GetPrimaryEditAddress(string gameCode) {
         if (gameCode == FireRed) return 0x09A8A4;
         if (gameCode == LeafGreen) return 0x09A878;
         if (gameCode == Ruby || gameCode == Sapphire) return 0x0A98BC;
         if (gameCode == Emerald) return 0xAD745C;
         return -1;
      }

      public bool CanRun(IViewPort viewPortInterface) {
         var viewPort = viewPortInterface as ViewPort;
         if (viewPort == null) return false;
         var model = viewPortInterface.Model;
         var gameCode = new string(Enumerable.Range(0xAC, 4).Select(i => ((char)model[i])).ToArray());

         var start = GetPrimaryEditAddress(gameCode);
         if (start == -1) return false;
         var run = model.GetNextRun(start);
         return !(run is WordRun);
      }

      public ErrorInfo Run(IViewPort viewPortInterface) {
         var viewPort = (ViewPort)viewPortInterface;
         var model = viewPortInterface.Model;
         var gameCode = new string(Enumerable.Range(0xAC, 4).Select(i => ((char)model[i])).ToArray());
         var start = GetPrimaryEditAddress(gameCode);

         // IsItemIDValid(itemID)
         viewPort.Edit($"@{start:X6} 00 B5 00 04 00 0C 03 49 08 45 00 DB 00 20 02 BC 08 47 00 00 ::items ");

         if (gameCode == FireRed) {
            // DB: comparison was 'less or same'. Make it 'less than'.
            //     then update the constant after the code to just be the number of items.
            viewPort.Edit("@098983 DB @098998 ::items ");
         } else if (gameCode == LeafGreen) {
            // DB: comparison was 'less or same'. Make it 'less than'.
            //     then update the constant after the code to just be the number of items.
            viewPort.Edit("@098967 DB @09896C ::items ");
         } else if (gameCode == Emerald) {
            // Emerald code already uses the number of items specifically. Just add the
            //    format so we can update the constant whenever the user adds new items.
            viewPort.Edit("@1B0014 ::items ");
         }
         // note that we make no updates for Ruby/Sapphire... that's because I don't
         //    know where the item image tables are stored in those games :(

         CanRunChanged?.Invoke(this, EventArgs.Empty);

         return ErrorInfo.NoError;
      }

      public void TabChanged() => CanRunChanged?.Invoke(this, EventArgs.Empty);
   }
}
