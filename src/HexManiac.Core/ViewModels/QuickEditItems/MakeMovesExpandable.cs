using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using static HavenSoft.HexManiac.Core.Models.HardcodeTablesModel;

namespace HavenSoft.HexManiac.Core.ViewModels.QuickEditItems {
   public class MakeMovesExpandable : IQuickEditItem {

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

      private static readonly IReadOnlyDictionary<string, int[]> Routines = new Dictionary<string, int[]> {
         [FireRed] = new[] { 0x0114F2, 0x011654, 0x03E8E4, 0x03E986, 0x040E94 },
      };

      private static readonly IReadOnlyDictionary<string, int[]> Limiters = new Dictionary<string, int[]> {
         [Ruby] = new[] { 0x0AC67D, 0x0B276D, 0x120DFB, 0x121605, 0x12162D },
         [Sapphire] = new[] { 0x0AC67D, 0x0B276D, 0x120DFB, 0x121605, 0x12162D },
         [Ruby1_1] = new[] { 0x0AC69D, 0x0B278D, 0x120E1B, 0x121625, 0x12164D },
         [Sapphire1_1] = new[] { 0x0AC69D, 0x0B278D, 0x120E1B, 0x121625, 0x12164D },
         [FireRed] = new[] { 0x0D7603, 0x0D7E9D, 0x0D7EB5 },
         [LeafGreen] = new[] { 0x0D75D7, 0x0D7E71, 0x0D7E89 },
         [FireRed1_1] = new[] { 0x0D7617, 0x0D7EB1, 0x0D7EC9 },
         [LeafGreen1_1] = new[] { 0x0D75EB, 0x0D7E85, 0x0D7E9D },
         [Emerald] = new[] { 0x0D8F13, 0x0DE841, 0x14E50B, 0x14EF51, 0x14EF69 },
      };

      public string Name => "Make Moves Expandable";

      public string Description => "Running this utility will remove the move limiters" +
                                   "and allow the PP pointers to auto-repoint.";

      public event EventHandler CanRunChanged;

      public bool CanRun(IViewPort viewPort) {
         if (!(viewPort is ViewPort)) return false;
         var model = viewPort.Model;
         var moveDataAddress = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, "movedata");
         if (moveDataAddress == Pointer.NULL) return false;
         var game = model.GetGameCode();
         if (!Limiters.TryGetValue(game, out var limiters)) return false;
         if (!Routines.TryGetValue(game, out var routines)) return false;

         var initialRoutine = viewPort.Tools.CodeTool.Parser.Compile(model, 0, "lsl r2, r0, #1", "add r2, r2, r0", "lsl r2, r2, #2");

         var limitersCanBeChanged = limiters.All(limiter => model.Count > limiter && model[limiter].IsAny<byte>(0xD8, 0xD9));
         var codeCanBeChanged = routines.All(routine =>
            model.Count > routine + initialRoutine.Count &&
            Enumerable.Range(0, initialRoutine.Count).All(i => model[routine + i] == initialRoutine[i]));
         return limitersCanBeChanged && codeCanBeChanged;
      }

      public ErrorInfo Run(IViewPort viewPortInterface) {
         if (!(viewPortInterface is ViewPort viewPort)) return new ErrorInfo("Can only run move expansion on an editable tab.");
         var model = viewPort.Model;
         var game = model.GetGameCode();
         var thumb = viewPort.Tools.CodeTool.Parser;
         var token = viewPort.CurrentChange;

         var moveDataAddress = model.GetAddressFromAnchor(token, -1, "movedata");
         if (moveDataAddress == Pointer.NULL) return new ErrorInfo("Expanding moves requires the existence of a 'movedata' table.");

         var sourcesToPP = viewPort.Find($"<{OriginalPPPointer[game]:X6}>");
         if (sourcesToPP.Count != 5) {
            var originalCount = sourcesToPP.Count;
            sourcesToPP = viewPort.Find($"<{(moveDataAddress + 4):X6}>");
            if (sourcesToPP.Count != 5) {
               return new ErrorInfo($"Expanding moves utility expects there to be 5 pointers to PP moves, but there were {originalCount} to {OriginalPPPointer[game]:X6} and {sourcesToPP.Count} to {(moveDataAddress + 4):X6}.");
            }
         }

         // update limiter
         foreach (var limiter in Limiters[game]) {
            if (model[limiter] == 0xD8) {
               token.ChangeData(model, limiter, 0xDF);
            } else if (model[limiter] == 0xD9) {
               token.ChangeData(model, limiter, 0xDE);
            } else {
               return new ErrorInfo($"Limiter could not be updated: {limiter:X6}");
            }
         }

         // update routines
         var code = thumb.Compile(model, 0, "mov r2, #12", "mul r2, r0", "add r2, r2, #4");
         Debug.Assert(code.Count == 6 && !code.Contains(byte.MinValue));
         foreach (var routine in Routines[game]) {
            for (int i = 0; i < code.Count; i++) token.ChangeData(model, routine + i, code[i]);
         }

         // update PP pointers
         foreach (var source in sourcesToPP) {
            var start = source.start;
            model.ClearFormat(token, start, 4);
            model.WritePointer(token, start, moveDataAddress);
            model.ObserveRunWritten(token, new PointerRun(start));
         }

         return ErrorInfo.NoError;
      }

      public void TabChanged() => CanRunChanged?.Invoke(this, EventArgs.Empty);
   }
}
