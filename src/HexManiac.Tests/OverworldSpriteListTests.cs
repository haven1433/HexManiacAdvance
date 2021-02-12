using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System.Linq;
using Xunit;
namespace HavenSoft.HexManiac.Tests {
   public class OverworldSpriteListTests : BaseViewModelTestClass {
      public const int
         SpriteStart = 0x00,
         Pal1Start = 0x20,
         Pal2Start = 0x40,
         PalTable = 0x60,
         SpriteList = 0x80,
         Parent = 0xA0;

      public OverworldSpriteListTests() {
         ViewPort.Edit($"@{PalTable:X2} <020> 00 00 <040> 01 00 @{PalTable:X2} ^{HardcodeTablesModel.OverworldPalettes}[pal<`ucp4`> id:|h]2 ");
         ViewPort.Edit($"@{SpriteList:X2} <000> 20 ");
         ViewPort.Edit($"@{Parent:X2} FF FF 00 00 40 00 08 00 08 00 <080>");
         ViewPort.Edit($"@{Parent:X2} ^parent[starterbytes:|h paletteid:|h length: width: height: sprites<`osl`>]1 ");
      }

      [Fact]
      public void Setup_MatchesExpected() {
         Assert.IsAssignableFrom<ISpriteRun>(Model.GetNextRun(0x00));
         Assert.IsAssignableFrom<IPaletteRun>(Model.GetNextRun(0x20));
         Assert.IsAssignableFrom<IPaletteRun>(Model.GetNextRun(0x40));
         Assert.IsAssignableFrom<ITableRun>(Model.GetNextRun(0x60));
         Assert.IsAssignableFrom<OverworldSpriteListRun>(Model.GetNextRun(0x80));
         Assert.IsAssignableFrom<ITableRun>(Model.GetNextRun(0xA0));
      }

      [Fact]
      public void OverworldSpriteList_UpdatePaletteIDInline_UpdateSpriteFormatWithNewPaletteID() {
         ViewPort.Edit($"@{Parent + 2:X2} 0001 ");

         var run = (ISpriteRun)Model.GetNextRun(SpriteStart);
         Assert.Equal(0x0001, Model.ReadMultiByteValue(0xA2, 2));
         Assert.Equal($"{HardcodeTablesModel.OverworldPalettes}:id=0001", run.SpriteFormat.PaletteHint);
      }

      [Fact]
      public void OverworldSpriteList_UpdatePaletteIDFromTable_UpdateSpriteFormatWithNewPaletteID() {
         ViewPort.Goto.Execute(Parent);

         var paletteID = ViewPort.Tools.TableTool.Children
            .Where(child => child is FieldArrayElementViewModel)
            .Cast<FieldArrayElementViewModel>()
            .Single(vm => vm.Name == "paletteid");
         paletteID.Content = "0001";

         var run = (ISpriteRun)Model.GetNextRun(SpriteStart);
         Assert.Equal(0x0001, Model.ReadMultiByteValue(0xA2, 2));
         Assert.Equal($"{HardcodeTablesModel.OverworldPalettes}:id=0001", run.SpriteFormat.PaletteHint);
      }

      [Fact]
      public void OverworldSpriteList_UpdatePaletteIDInline_SpriteToolPaletteUpdates() {
         ViewPort.Goto.Execute(SpriteList);
         ViewPort.Edit($"@{Parent + 2:X2} 0001 ");
         ViewPort.Goto.Execute(SpriteList);

         Assert.Equal($"{HardcodeTablesModel.OverworldPalettes}:id=0001", ViewPort.Tools.SpriteTool.SpritePaletteHint);
         Assert.Equal(Pal2Start, ViewPort.Tools.SpriteTool.PaletteAddress);
      }
   }
}

