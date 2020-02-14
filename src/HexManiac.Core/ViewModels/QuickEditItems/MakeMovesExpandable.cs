using System;
using System.Collections.Generic;
using System.Linq;
using HavenSoft.HexManiac.Core.Models;
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

      public string Description => "Running this utility will remove the move limiters" +
                                   "and allow the PP pointers to auto-repoint.";

      public string WikiLink => "https://github.com/haven1433/HexManiacAdvance/wiki/Move-Expansion-Explained";

      public event EventHandler CanRunChanged;

      public bool CanRun(IViewPort viewPort) {
         if (!(viewPort is ViewPort)) return false;
         var model = viewPort.Model;
         var moveDataAddress = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, "movedata");
         if (moveDataAddress == Pointer.NULL) return false;
         var game = model.GetGameCode();
         var limiterCode = viewPort.Tools.CodeTool.Parser.Compile(model, 0, @"
    mov   r0, #177
    lsl   r0, r0, #1
    cmp   r1, r0
".Split(Environment.NewLine)).ToArray();
         return model.Find(limiterCode).Any();
      }

      public ErrorInfo Run(IViewPort viewPortInterface) {
         if (!(viewPortInterface is ViewPort viewPort)) return new ErrorInfo("Can only run move expansion on an editable tab.");
         var model = viewPort.Model;
         var game = model.GetGameCode();
         var thumb = viewPort.Tools.CodeTool.Parser;
         var token = viewPort.CurrentChange;

         var moveDataAddress = model.GetAddressFromAnchor(token, -1, "movedata");
         if (moveDataAddress == Pointer.NULL) return new ErrorInfo("Expanding moves requires the existence of a 'movedata' table.");
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
         var codePotentiallyUsingPpPointer = model.Find(codeUsingPpPointer).ToList();
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
         foreach (var limiter in model.Find(limiterCode)) {
            for (int i = 0; i < changedCodeForPpPointer.Length; i++) {
               token.ChangeData(model, limiter + i, newLimiterCode[i]);
            }
         }

         // update PP pointers
         foreach (var source in sourcesToPP) {
            model.ClearFormat(token, source, 4);
            model.WritePointer(token, source, moveDataAddress);
            model.ObserveRunWritten(token, new PointerRun(source));
         }

         return ErrorInfo.NoError;
      }

      public void TabChanged() => CanRunChanged?.Invoke(this, EventArgs.Empty);
   }
}
