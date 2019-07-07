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
   public class MakeTmsExpandable : IQuickEditItem {
      public string Name => "Make TMs Expandable";

      public string Description => "The initial games limited to have no more than 64 TMs+HMs." +
         Environment.NewLine + "This change will allow you to freely add new TMs, up to 256.";

      public event EventHandler CanRunChanged;

      public static (int canPokemonLearnTmMove_start, int canPokemonLearnTmMove_Length) GetCanPokemonLearnTmMoveOffsets(IDataModel model) {
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
            return 0x03FDE8;
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

         // require that this data actually supports this change
         var model = viewPort.Model;
         var (canPokemonLearnTmMove_start, canPokemonLearnTmMove_Length) = GetCanPokemonLearnTmMoveOffsets(model);
         if (canPokemonLearnTmMove_start < 0) return false;

         // require that this data has a tmmoves and tmcompatibility table, since we're messing with those
         var tmmoves = model.GetAddressFromAnchor(viewPort.CurrentChange, -1, TmMoves);
         var tmcompatibility = model.GetAddressFromAnchor(viewPort.CurrentChange, -1, TmCompatibility);
         if (tmmoves == Pointer.NULL || tmcompatibility == Pointer.NULL) {
            return false;
         }

         // if the patch has already been applied, you can't apply it again
         if (viewPort.Model.GetNextRun(canPokemonLearnTmMove_start + 0x38) is WordRun) return false;
         return true;
      }

      public ErrorInfo Run(IViewPort viewPortInterface) {
         var viewPort = (ViewPort)viewPortInterface;
         var model = viewPort.Model;
         var (canPokemonLearnTmMove_start, canPokemonLearnTmMove_Length) = GetCanPokemonLearnTmMoveOffsets(model);
         var getMonData_start = GetGetMonDataStart(model);
         var tmmoves = model.GetAddressFromAnchor(viewPort.CurrentChange, -1, TmMoves);
         var tmcompatibility = model.GetAddressFromAnchor(viewPort.CurrentChange, -1, TmCompatibility);

         InsertRoutine_CanPokemonLearnTmMove(viewPort, canPokemonLearnTmMove_start, canPokemonLearnTmMove_Length, getMonData_start);

         CanRunChanged?.Invoke(this, EventArgs.Empty);

         return ErrorInfo.NoError;
      }

      public void TabChanged() => CanRunChanged?.Invoke(this, EventArgs.Empty);

      private void InsertRoutine_CanPokemonLearnTmMove(ViewPort viewPort, int address, int originalLength, int subroutineLocation) {
         /*
         CanPokemonLearnTmHm(pokemonData, tm_move)
             push  lr, {r1}              @ 10110101_00000010    B502
             mov   r1, #65               @ 00100_001_01000001   2141
             mov   r2, #0                @ 00100_010_00000000   2200
             bl    <03FBE8>              @ 11111_xxxxxxxxxxx_11110_xxxxxxxxxxx
             pop   {r2}                  @ 10111100_00000100    BC04
             mov   r1, r0                @ 0001110000_000_001   1C01
             mov   r0, #0                @ 00100_000_00000000   2000
             mov   r3, #103              @ 00100_011_01100111   2367
             lsl   r3, r3, #2            @ 00000_00010_011_011  009B
             cmp   r1, r3                @ 01000101_00_011_001  4519
             beq   end                   @ 1101_0000_00001100   D00C
             ldr   r0, =tm_compatibility @ 01001_000_00000110   4806
             ldr   r3, =tm_count         @ 01001_011_00000111   4B07
             add   r3, #7                @ 00110_011_00000111   3307
             lsr   r3, r3, #3            @ 00001_00011_011_011  08DB
             mul   r1, r2                @ 0100001101_010_001   4351
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
             .word 08252BC8
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
         viewPort.Edit($"02 B5 41 21 00 22 {bl[0]} {bl[1]} {bl[2]} {bl[3]} 04 BC 01 1C 00 20 ");
         viewPort.Edit($"65 23 9B 00 19 45 0C D0 06 48 07 4B 07 33 DB 08 ");
         viewPort.Edit($"51 43 D3 08 C9 18 80 5C 07 21 11 40 01 22 8A 40 ");
         viewPort.Edit($"10 40 00 BD <{TmCompatibility}> ::{TmMoves} ");  // new data only 0x3C long
         for (int i = 0x3C; i < originalLength; i++) viewPort.Edit("00 ");
      }
   }
}
