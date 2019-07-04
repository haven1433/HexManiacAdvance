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
}
