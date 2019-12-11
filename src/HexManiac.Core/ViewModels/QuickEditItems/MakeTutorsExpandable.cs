using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Linq;

using static HavenSoft.HexManiac.Core.Models.AutoSearchModel;

namespace HavenSoft.HexManiac.Core.ViewModels.QuickEditItems {
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
         var token = viewPort.CurrentChange;
         var model = viewPort.Model;
         model.ClearFormat(token, address, originalLength);

         var code = $@"
            GetTutorMove: @ (index) -> moveID
               lsl   r0, r0, #1
               ldr   r1, [pc, <move_tutor_list>]
               ldrh  r0, [r0, r1]
               bx    lr
            move_tutor_list:
               .word <tutormoves>
         ".Split(Environment.NewLine);

         var bytes = viewPort.Tools.CodeTool.Parser.Compile(viewPort.Model, address, code);
         for (int i = 0; i < bytes.Count; i++) token.ChangeData(model, address + i, bytes[i]);
         for (int i = bytes.Count; i < originalLength; i++) token.ChangeData(model, address + i, 0x00);

         viewPort.Edit($"@{address + bytes.Count - 4:X6} <{MoveTutors}>");
      }

      private void InsertRoutine_CanPokemonLearnTutorMove(ViewPort viewPort, int address, int originalLength) {
         var token = viewPort.CurrentChange;
         var model = viewPort.Model;
         model.ClearFormat(token, address, originalLength);

         var code = $@"
            CanPokemonLearnTutorMove: @ (pokemon, tutor_move) -> bool
               ldr     r2, [pc, <move_tutor_count>]
               add     r2, #7
               lsr     r2, r2, #3
               mul     r0, r2
               lsr     r2, r1, #3
               add     r0, r0, r2
               mov     r2, #7
               and     r1, r2
               ldr     r2, [pc, <move_tutor_compatibility>]
               ldrb    r0, [r2, r0]
               lsr     r0, r1
               mov     r2, #1
               and     r0, r2
               bx      lr
            move_tutor_compatibility:
               .word <tutorcompatibility>
            move_tutor_count:
               .word 0 @ ::tutormoves
         ".Split(Environment.NewLine);

         var bytes = viewPort.Tools.CodeTool.Parser.Compile(viewPort.Model, address, code);
         for (int i = 0; i < bytes.Count; i++) token.ChangeData(model, address + i, bytes[i]);
         for (int i = bytes.Count; i < originalLength; i++) token.ChangeData(model, address + i, 0x00);

         viewPort.Edit($"@{address + bytes.Count - 8:X6} <{TutorCompatibility}> ::{MoveTutors} ");
      }
   }
}
