using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class OverworldPaletteTableTests : BaseViewModelTestClass {
      public const int
         PalTable = 0x60,
         SpriteList = 0x80,
         Parent = 0xA0;

      public OverworldPaletteTableTests() : base(0x400) { }

      [Fact]
      public void PaletteHint_NamesSeveralTables_UsesTheFirstTableThatHasTheKey() {
         ViewPort.Edit($"@{PalTable:X2} <020> 00 00 <040> 01 00 @{PalTable:X2} ^{HardcodeTablesModel.OverworldPalettes}[pal<`ucp4`> id:|h]2 ");
         ViewPort.Edit("@C0 <100> 05 10 @C0 ^fieldpal[pal<`ucp4`> id:|h]1 ");
         ViewPort.Edit($"@{SpriteList:X2} <000> 20 ");
         ViewPort.Edit($"@{Parent:X2} FF FF 05 10 40 00 08 00 08 00 <080>");
         // the format is applied directly: typing a comma in the editor works like typing a space
         PokemonModel.ApplyAnchor(Model, ViewPort.CurrentChange, Parent, $"^parent[starterbytes:|h paletteid:|h length: width: height: sprites<`osl|fieldpal,{HardcodeTablesModel.OverworldPalettes}:id=`>]1", allowAnchorOverwrite: true);

         var sprite = (ISpriteRun)Model.GetNextRun(0);
         Assert.Equal($"fieldpal,{HardcodeTablesModel.OverworldPalettes}:id=1005", sprite.SpriteFormat.PaletteHint);
         Assert.Equal(0x100, sprite.FindRelatedPalettes(Model, Parent + 10, sprite.SpriteFormat.PaletteHint).Single().Start);

         // an id that only the second table has
         ViewPort.Edit($"@{Parent + 2:X2} 0001 ");

         sprite = (ISpriteRun)Model.GetNextRun(0);
         Assert.Equal($"fieldpal,{HardcodeTablesModel.OverworldPalettes}:id=0001", sprite.SpriteFormat.PaletteHint);
         Assert.Equal(0x40, sprite.FindRelatedPalettes(Model, Parent + 10, sprite.SpriteFormat.PaletteHint).Single().Start);
      }

      [Fact]
      public void FrameList_AlsoPointedAtByAnotherTable_TakesItsPaletteFromTheTableLikeTheParent() {
         ViewPort.Edit($"@{PalTable:X2} <020> 00 00 <040> 01 00 @{PalTable:X2} ^{HardcodeTablesModel.OverworldPalettes}[pal<`ucp4`> id:|h]2 ");
         ViewPort.Edit($"@{SpriteList:X2} <000> 20 ");
         ViewPort.Edit($"@{Parent:X2} FF FF 01 00 40 00 08 00 08 00 <080>");
         ViewPort.Edit($"@{Parent:X2} ^parent[starterbytes:|h paletteid:|h length: width: height: sprites<`osl`>]1 ");
         // another table, at a lower address, points at the same frames (like the berry stats do for berry trees)
         ViewPort.Edit("@70 <080> 00 00 00 00 @70 ^other[frames<> extra::]1 ");

         var list = Assert.IsType<OverworldSpriteListRun>(Model.GetNextRun(SpriteList));

         Assert.Equal(2, list.PointerSources.Count);
         Assert.Equal($"{HardcodeTablesModel.OverworldPalettes}:id=0001", list.SpriteFormat.PaletteHint);
      }

      [Fact]
      public void EnumOfOverworldSprites_ComboOptionsHavePictures() {
         ViewPort.Edit($"@{PalTable:X2} <020> 00 00 <040> 01 00 @{PalTable:X2} ^{HardcodeTablesModel.OverworldPalettes}[pal<`ucp4`> id:|h]2 ");
         ViewPort.Edit($"@{SpriteList:X2} <000> 20 ");
         Model.SetList(new ModelDelta(), "owNames", "HERO", "GHOST");
         ViewPort.Edit("@120 FF FF 01 00 40 00 08 00 08 00 <080>");
         ViewPort.Edit("@140 FF FF 01 00 40 00 08 00 08 00 00 00 00 00"); // this sprite has no graphics
         ViewPort.Edit($"@100 <120> <140> @100 ^{HardcodeTablesModel.OverworldSprites}[data<[starterbytes:|h paletteid:|h length: width: height: sprites<`osl`>]1>]owNames ");
         ViewPort.Edit($"@180 01 00 @180 ^stages[graphics.{HardcodeTablesModel.OverworldSprites}]2 ");

         var segment = (ArrayRunEnumSegment)((ITableRun)Model.GetNextRun(0x180)).ElementContent[0];
         var options = segment.GetComboOptions(Model).ToList();

         Assert.Equal(2, options.Count);
         var picture = Assert.IsType<VisualComboOption>(options[0]);
         Assert.NotEmpty(picture.PixelData);
         Assert.Equal("HERO", picture.Text);
         Assert.False(options[1] is VisualComboOption); // no graphics: the text option stays
         Assert.Equal("GHOST", options[1].Text);
      }
   }
}
