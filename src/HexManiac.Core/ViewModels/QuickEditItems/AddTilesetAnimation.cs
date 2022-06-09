using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HavenSoft.HexManiac.Core.ViewModels.QuickEditItems {
   public class AddTilesetAnimation : IQuickEditItem {
      private readonly IFileSystem fileSystem;

      public string Name => "Add Tileset Animation";

      public string Description => "Add a new table and code for adding animations to a map tileset." + Environment.NewLine +
         "Look for your new table with the name `graphics.maps.tilesets.animations. <something>.table`." + Environment.NewLine +
         "Look for your new animation routine with the name `graphics.maps.tilesets.animations. <something>.init`.";

      public string WikiLink => "https://github.com/haven1433/HexManiacAdvance/wiki/Tileset-Animations-Explained";

      public event EventHandler CanRunChanged;

      public AddTilesetAnimation(IFileSystem fileSystem) => this.fileSystem = fileSystem;

      public bool CanRun(IViewPort viewPort) {
         return viewPort is IEditableViewPort vp && vp.Model.GetGameCode() == "BPRE0";
      }

      public Task<ErrorInfo> Run(IViewPort viewPort) {
         var model = viewPort.Model;
         var token = viewPort.ChangeHistory.CurrentChange;
         var name = fileSystem.RequestText("New Animation Anchor Name", "Choose a short name for the new animation (no spaces or special characters)");
         if (name == null) return Task.FromResult(ErrorInfo.NoError);
         name = SanitizeName(viewPort, name);
         var newTableName = $"graphics.maps.tilesets.animations.{name}.table";
         var tableAddress = model.FindFreeSpace(model.FreeSpaceStart, 16) + 4;
         PokemonModel.UniquifyName(model, token, tableAddress, ref newTableName);

         // insert data/metadata for new table
         model.WritePointer(token, tableAddress - 4, Pointer.NULL); // null pointer for the first animation
         model.WritePointer(token, tableAddress, tableAddress - 4); // pointer to the child
         model.WriteMultiByteValue(tableAddress + 4, 2, token, 1);  // 1 frame
         model.WriteMultiByteValue(tableAddress + 6, 1, token, 4);  // timer is 1<<4 -> 16
         model.WriteMultiByteValue(tableAddress + 7, 1, token, 1);  // 1 tile
         model.WriteMultiByteValue(tableAddress + 8, 4, token, 0);  // tile offset
         model.WriteMultiByteValue(tableAddress + 12, 4, token, -1 - 0x01010101); // FEFEFEFE as the end token

         // `mat`=[frame<`uct4xn`>]/frames
         var error = ArrayRun.TryParse(model, "[animations<`mat`> frames: timer. tiles. tileOffset::]!FEFEFEFE", tableAddress, SortedSpan<int>.None, out var tableRun);
         model.ObserveAnchorWritten(token, newTableName, tableRun);

         var callbackAddress = InsertCallback(viewPort, $"graphics.maps.tilesets.animations.{name}.callback", tableAddress);
         var initAddress = InsertInit(viewPort, $"graphics.maps.tilesets.animations.{name}.init", tableAddress, callbackAddress);

         viewPort.Goto.Execute(tableAddress);
         return Task.FromResult(ErrorInfo.NoError);
      }

      /// <summary>
      /// Remove all special characters, spaces, etc
      /// </summary>
      private string SanitizeName(IViewPort viewPort, string name) {
         var characters = name.ToCharArray().Where(char.IsLetterOrDigit).ToList();
         if (characters.Count == 0) characters.AddRange("temp");
         var candidate = new string(characters.ToArray()).ToLower();
         PokemonModel.UniquifyName(viewPort.Model, viewPort.ChangeHistory.CurrentChange, viewPort.Model.FreeSpaceStart, ref candidate);
         return candidate;
      }

      private int InsertCallback(IViewPort viewPort, string callbackName, int tableAddress) {
         var mod = 0x1E4684; // BPRE0
         var appendTilesetAnimToBuffer = 0x06FF04; // BPRE0

         var model = viewPort.Model;
         var token = viewPort.ChangeHistory.CurrentChange;
         var compiler = viewPort.Tools.CodeTool.Parser;
         var start = model.FindFreeSpace(model.FreeSpaceStart, 0x80);

         var code = $@"
push {{r4-r7, lr}}

ldr   r4, =<{tableAddress:X6}>  @ r4 is the item
sub   r4, #12
ldr   r5, =0xFEFEFEFE           @ r5 is the end token
mov   r6, r0                    @ r6 is the timer
                                @ r7 is reserved for long branch-link
loop:
  add  r4, #12

  @ if end of table, break
  ldr  r0, [r4, #0] @ check end of table
  cmp  r0, r5
  beq  <end>

  @ verify that timer is a multiple of the proper power of 2 (item.timer)
  mov  r0, #1
  ldrb r1, [r4, #6] @ item.timer
  lsl  r0, r1
  sub  r0, #1
  and  r0, r6
  cmp  r0, #0
  bne  <loop>

  @ arg0 = the current animation frame graphics
  mov  r0, r6
  lsr  r0, r1
  ldrh r1, [r4, #4] @ item.frames
  ldr  r7, =<{mod:X6}>
  bl   <long_branch>                   @ r0 %= r1
  lsl  r0, #2
  ldr  r2, [r4, #0] @ item.frame
  ldr  r0, [r2, r0]

  @ arg1 = the address to apply the tiles, in RAM
  ldr  r1, [r4, #8] @ item.tileOffset
  lsl  r1, #5
  ldr  r2, =0x6000000
  add  r1, r2

  @ arg2 = the number of bytes to copy from ROM to RAM
  ldrb r2, [r4, #7] @ item.tiles
  lsl  r2, #5

  @ AppendTilesetAnimToBuffer(arg0, arg1, arg2)
  ldr  r7, =<{appendTilesetAnimToBuffer:X6}>
  bl   <long_branch>
  b    <loop>

end:
pop  {{r4-r7, pc}}
long_branch:
  add r7, #1
  bx r7
";

         compiler.Compile(token, model, start, code.SplitLines());

         PokemonModel.UniquifyName(model, token, start, ref callbackName);
         model.ObserveAnchorWritten(token, callbackName, new NoInfoRun(start + 1));

         return start;
      }

      private int InsertInit(IViewPort viewPort, string initName, int tableAddress, int callbackAddress) {
         var sPrimaryTilesetAnimCounter = 0x03000FAE;
         var sPrimaryTilesetAnimCounterMax = 0x03000FB0;
         var sPrimaryTilesetAnimCallback = 0x03000FB8;

         // animations<[frame<`uct4`>]/frames> frames: timer. tiles. tileOffset::

         var model = viewPort.Model;
         var token = viewPort.ChangeHistory.CurrentChange;
         var compiler = viewPort.Tools.CodeTool.Parser;
         var start = model.FindFreeSpace(model.FreeSpaceStart, 0x80);
         var code = $@"
push {{lr}}

@ sPrimaryTilesetAnimCounter = 0
ldr   r0, =0x{sPrimaryTilesetAnimCounter:X8}
mov   r1, #0
strh  r1, [r0, #0]

@ sPrimaryTilesetAnimCounterMax = 1 << table.Max(.timer)
ldr   r2, =0x08{tableAddress:X6}
sub   r2, #12
ldr   r3, =0xFEFEFEFE
loop1:
  add  r2, #12
  ldr  r0, [r2, #0]
  cmp  r0, r3
  beq  <done_loop1>
  ldrb r0, [r2, #6]
  cmp  r0, r1
  blt  <loop1>
  mov  r1, r0
done_loop1:
mov   r0, #1
lsl   r0, r1
mov   r1, r0

@ sPrimaryTilesetAnimCounterMax *= table.Each(.frames)
ldr   r2, =0x08{tableAddress:X6}
sub   r2, #12
loop2:
  add  r2, #12
  ldr  r0, [r2, #0]
  cmp  r0, r3
  beq  <done_loop2>
  ldrh r0, [r2, #4]
  mul  r1, r0
  b <loop2>
done_loop2:
ldr   r0, =0x{sPrimaryTilesetAnimCounterMax:X8}
strh  r1, [r0, #0]

@ sPrimaryTilesetAnimCallback = TilesetAnim_Custom
ldr   r0, =0x{sPrimaryTilesetAnimCallback:X8}
ldr   r1, =<{(callbackAddress + 1):X6}>
str   r1, [r0, #0]

pop   {{pc}}
"; // 45

         compiler.Compile(token, model, start, code.SplitLines());
         PokemonModel.UniquifyName(model, token, start, ref initName);
         model.ObserveAnchorWritten(token, initName, new NoInfoRun(start + 1));

         return start;
      }

      public void TabChanged() => CanRunChanged?.Invoke(this, EventArgs.Empty);
   }
}
