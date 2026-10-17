using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System.Collections.Generic;
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

      [Theory]
      [InlineData("^other[a:: b:: c:: d::]1 ")]
      [InlineData("^other[a: b:]1 ")]
      [InlineData("^other[a:: b::]2 ")]
      public void OverworldSpriteList_OverwrittenByAnotherFormat_SpriteNoLongerListsTheListEntryAsASource(string format) {
         // pasting a table over a sprite list left the sprites listing the old list entries as pointer sources (the model then disagreed with the bytes once they changed)
         var sprite = Model.GetNextRun(SpriteStart);
         Assert.Contains(SpriteList, (IEnumerable<int>)sprite.PointerSources);

         ViewPort.Edit($"@{SpriteList:X2} " + format);

         sprite = Model.GetNextRun(SpriteStart);
         Assert.DoesNotContain(SpriteList, (sprite.PointerSources ?? SortedSpan<int>.None).ToList());
      }

      [Theory]
      [InlineData("^script`xse` 6A 5A 0F 00 ")]
      [InlineData("^script`xse` 02 6A 5A 0F 00 ")]
      public void OverworldSpriteList_ReplacedByANonTableFormatAndNewData_SpriteNoLongerListsTheListEntryAsASource(string edit) {
         // pasting a script over a sprite list: the bytes of the first entry stopped being a pointer, but the sprite kept the entry as a source
         var sprite = Model.GetNextRun(SpriteStart);
         Assert.Contains(SpriteList, (IEnumerable<int>)sprite.PointerSources);

         ViewPort.Edit($"@{SpriteList:X2} " + edit);

         sprite = Model.GetNextRun(SpriteStart);
         Assert.DoesNotContain(SpriteList, (sprite.PointerSources ?? SortedSpan<int>.None).ToList());
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

   public class OverworldSpriteListBadEntryTests : BaseViewModelTestClass {
      [Fact]
      public void OverworldSpriteList_AtTheStartOfTheFile_EntryIsNotAPointer_PixelsAreBlankInsteadOfFollowingTheListForever() {
         // the list found itself as its sprite: an entry that is not a pointer reads as a negative address, and the 'next run' from a negative address is the first run of the file
         ViewPort.Edit("@00 85 00 00 EA "); // the first instruction of a rom: not a pointer, but it reads as one with a negative address
         ViewPort.Edit("@40 FF FF 00 00 40 00 08 00 08 00 <000> ");
         ViewPort.Edit("@40 ^parent[starterbytes:|h paletteid:|h length: width: height: sprites<`osl`>]1 ");

         var run = Assert.IsType<OverworldSpriteListRun>(Model.GetNextRun(0));
         var pixels = run.GetPixels(Model, 0, 0);
         var data = run.GetData();

         Assert.Equal(run.SpriteFormat.TileWidth * 8, pixels.GetLength(0));
         Assert.Equal(run.SpriteFormat.TileHeight * 8, pixels.GetLength(1));
         Assert.All(pixels.Cast<int>(), pixel => Assert.Equal(0, pixel));
         Assert.All(data, value => Assert.Equal(0, value));
      }

      [Theory]
      [InlineData(0x1FF)] // the last byte of the file
      [InlineData(0x1FC)]
      [InlineData(0x1F9)]
      public void OverworldSpriteList_PointerTypedToTheLastBytesOfTheFile_NoEntryFitsSoItIsRejectedInsteadOfReadingPastTheEnd(int destination) {
         ViewPort.Edit("@80 <000> 20 ");
         ViewPort.Edit("@A0 FF FF 00 00 40 00 08 00 08 00 <080>");
         ViewPort.Edit("@A0 ^parent[starterbytes:|h paletteid:|h length: width: height: sprites<`osl`>]1 ");
         Assert.Equal(0x80, Model.ReadPointer(0xAA));

         ViewPort.Edit($"@AA <{destination:X6}> ");

         Assert.Single(Errors);
         Assert.NotEqual(destination, Model.ReadPointer(0xAA));
         Assert.IsNotType<OverworldSpriteListRun>(Model.GetNextRun(destination));
      }
   }

   public class OverworldSpriteListResizeTests : BaseViewModelTestClass {
      public const int
         Pal1Start = 0x20,
         Pal2Start = 0x40,
         PalTable = 0x60,
         SpriteList = 0x80,
         Parent = 0xA0,
         SpriteStart = 0x100;

      public OverworldSpriteListResizeTests() : base(0x400) { }

      [Theory]
      [InlineData("`osl`")]
      [InlineData("`osl|" + HardcodeTablesModel.OverworldPalettes + ":id=`")] // the sprite format of a table that names its palettes: the offsets of the parent's fields must be found the same way
      public void OverworldSpriteList_ParentWidthEdited_SpriteResizedFromThatElementsWidthAndHeight(string listFormat) {
         ViewPort.Edit($"@{PalTable:X2} <020> 00 00 <040> 01 00 @{PalTable:X2} ^{HardcodeTablesModel.OverworldPalettes}[pal<`ucp4`> id:|h]2 ");
         ViewPort.Edit($"@{SpriteList:X2} <{SpriteStart:X3}> 40 ");
         ViewPort.Edit($"@{Parent:X2} FF FF 00 00 40 00 10 00 08 00 <{SpriteList:X3}>");
         ViewPort.Edit($"@{Parent:X2} ^parent[starterbytes:|h paletteid:|h length: width: height: sprites<{listFormat}>]1 ");
         ViewPort.ChangeHistory.ChangeCompleted();
         Assert.Equal(0x40, Model.ReadMultiByteValue(SpriteList + 4, 2));
         Assert.IsAssignableFrom<OverworldSpriteListRun>(Model.GetNextRun(SpriteList));

         ViewPort.Edit($"@{Parent + 6:X2} 8 "); // narrower: two tiles wide became one tile wide, one tile tall

         Assert.Equal(8, Model.ReadMultiByteValue(Parent + 6, 2));
         Assert.Equal(0x20, Model.ReadMultiByteValue(SpriteList + 4, 2));
         Assert.IsAssignableFrom<OverworldSpriteListRun>(Model.GetNextRun(SpriteList));
      }

      [Theory]
      [InlineData(6)]
      [InlineData(8)]
      public void OverworldSpriteList_ParentSizeEditedToSomethingAbsurd_SpriteDataIsLeftAloneInsteadOfGrowingByMegabytes(int fieldOffset) {
         ViewPort.Edit($"@{PalTable:X2} <020> 00 00 <040> 01 00 @{PalTable:X2} ^{HardcodeTablesModel.OverworldPalettes}[pal<`ucp4`> id:|h]2 ");
         ViewPort.Edit($"@{SpriteList:X2} <{SpriteStart:X3}> 40 ");
         ViewPort.Edit($"@{Parent:X2} FF FF 00 00 40 00 10 00 08 00 <{SpriteList:X3}>");
         ViewPort.Edit($"@{Parent:X2} ^parent[starterbytes:|h paletteid:|h length: width: height: sprites<`osl`>]1 ");
         ViewPort.ChangeHistory.ChangeCompleted();
         var size = Model.Count;

         ViewPort.Edit($"@{Parent + fieldOffset:X2} 60000 "); // 60000 pixels

         Assert.Equal(size, Model.Count);
         Assert.Equal(0x40, Model.ReadMultiByteValue(SpriteList + 4, 2));
         Assert.Equal(SpriteStart, Model.ReadPointer(SpriteList));
      }
   }
}

