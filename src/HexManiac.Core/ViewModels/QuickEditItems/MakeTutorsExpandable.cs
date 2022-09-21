using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static HavenSoft.HexManiac.Core.Models.HardcodeTablesModel;

namespace HavenSoft.HexManiac.Core.ViewModels.QuickEditItems {
   public class MakeTutorsExpandable : IQuickEditItem {
      public string Name => "Make Tutors Expandable";

      public string Description => "The initial games limited to have exactly 18 (FireRed) or no more than 32 (Emerald) tutors." +
               Environment.NewLine + "This change will allow you to freely add new tutors, up to 256.";

      public string WikiLink => "https://github.com/haven1433/HexManiacAdvance/wiki/Tutor-Expansion-Explained";

      public event EventHandler CanRunChanged;

      public bool CanRun(IViewPort viewPortInterface) {
         // require that I have a tab with real data, not a search tab or a diff tab or something
         if (!(viewPortInterface is ViewPort viewPort)) return false;

         // require that we fan find the specials table and that it's long enough
         var token = new NoDataChangeDeltaModel();
         var specialsAddress = viewPort.Model.GetAddressFromAnchor(token, -1, HardcodeTablesModel.SpecialsTable);
         if (specialsAddress < 0 || specialsAddress > viewPort.Model.Count) return false;
         var specials = viewPort.Model.GetNextRun(specialsAddress) as ITableRun;
         if (specials == null) return false;
         if (specials.ElementCount < 397) return false;

         // require that this data actually supports this change
         var model = viewPort.Model;
         var gameCode = model.GetGameCode();
         var (getTutorMove, canPokemonLearnTutorMove, _, _) = GetOffsets(gameCode);
         if (getTutorMove < 0 || canPokemonLearnTutorMove < 0) return false;

         // require that this data has a tutormoves and tutorcompatibility table, since we're messing with those
         var tutormoves = model.GetAddressFromAnchor(token, -1, MoveTutors);
         var tutorcompatibility = model.GetAddressFromAnchor(token, -1, TutorCompatibility);
         if (tutormoves == Pointer.NULL || tutorcompatibility == Pointer.NULL) {
            return false;
         }

         // if the patch has already been applied, you can't apply it again
         if (viewPort.Model.GetNextRun(canPokemonLearnTutorMove + 0x20) is WordRun) return false;
         return true;
      }

      public Task<ErrorInfo> Run(IViewPort viewPortInterface) {
         var viewPort = (ViewPort)viewPortInterface;
         var model = viewPort.Model;
         var token = new NoDataChangeDeltaModel();
         var gameCode = model.GetGameCode();

         var (getTutorMove, canPokemonLearnTutorMove, getTutorMove_Length, canPokemonLearnTutorMove_Length) = GetOffsets(gameCode);
         var specialsAddress = model.GetAddressFromAnchor(token, -1, SpecialsTable);
         var tutorSpecial = model.ReadPointer(specialsAddress + 397 * 4); // Emerald tutors is actually special 477, but we don't need to edit it so it doesn't matter.
         tutorSpecial -= 1; // the pointer is to thumb code, so it's off by one.

         var tutormoves = model.GetAddressFromAnchor(token, -1, MoveTutors);
         var tutorcompatibility = model.GetAddressFromAnchor(token, -1, TutorCompatibility);

         InsertRoutine_GetTutorMove(viewPort, getTutorMove, getTutorMove_Length);
         InsertRoutine_CanPokemonLearnTutorMove(viewPort, canPokemonLearnTutorMove, canPokemonLearnTutorMove_Length);
         UpdateRoutine_TutorSpecial(viewPort, tutorSpecial, gameCode);

         CanRunChanged?.Invoke(this, EventArgs.Empty);

         return Task.FromResult(ErrorInfo.NoError);
      }

      public void TabChanged() => CanRunChanged?.Invoke(this, EventArgs.Empty);

      public static (int getTutorMove, int canPokemonLearnTutorMove, int getTutorMove_Length, int canPokemonLearnTutorMove_Length) GetOffsets(string gameCode) {
         if (gameCode == FireRed) {
            return (0x120BA8, 0x120BE8, 0x40, 0x54);
         } else if (gameCode == FireRed1_1) {
            return (0x120C20, 0x120C60, 0x40, 0x54);
         } else if (gameCode == LeafGreen) {
            return (0x120B80, 0x120BC0, 0x40, 0x54);
         } else if (gameCode == LeafGreen1_1) {
            return (0x120BF8, 0x120C38, 0x40, 0x54);
         } else if (gameCode == Emerald) {
            return (0x1B2360, 0x1B2370, 0x10, 0x2C);
         } else {
            return (-1, -1, 0, 0);
         }
      }

      private IReadOnlyList<byte> Compile(ViewPort viewPort, int start, string[] code) {
         return viewPort.Tools.CodeTool.Parser.Compile(viewPort.CurrentChange, viewPort.Model, start, code);
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
               .word <{MoveTutors}>
         ".Split(Environment.NewLine);

         var bytes = Compile(viewPort, address, code);
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
               .word <{TutorCompatibility}>
            move_tutor_count:
               .word 0 @ ::{MoveTutors}
         ".Split(Environment.NewLine);

         var bytes = Compile(viewPort, address, code);
         for (int i = bytes.Count; i < originalLength; i++) token.ChangeData(model, address + i, 0x00);

         viewPort.Edit($"@{address + bytes.Count - 8:X6} <{TutorCompatibility}> ::{MoveTutors} ");
      }

      private void UpdateRoutine_TutorSpecial(ViewPort viewPort, int tutorSpecial, string gameCode) {
         if (gameCode == Emerald) return; // Emerald's tutor special doesn't have a limiter, so it doesn't need to be updated.

         // change the code from 'branch-hi' to 'nop' so that the standard codepath is taken for tutorID>14
         int instructionIndex = 5;
         int instructionWidth = 2;
         var branchOffset = tutorSpecial + instructionIndex * instructionWidth;
         viewPort.Model.WriteMultiByteValue(branchOffset, 2, viewPort.CurrentChange, 0x0000);

         // a separate routine several layers down also needs to be updated
         // change the code from 'branch-hi' to 'nop' so that the standard codepath is taken for tutorID>14
         instructionIndex = 20;
         branchOffset = (gameCode == FireRed) ? 0x11F430 : 0x11F408; // FireRed / LeafGreen
         branchOffset += instructionIndex * instructionWidth;
         viewPort.Model.WriteMultiByteValue(branchOffset, 2, viewPort.CurrentChange, 0x0000);
      }
   }
}
