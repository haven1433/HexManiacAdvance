using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>The walking sheet of one overworld sprite in the image editor: 18 pictures of 16x32 that follow each other in the ROM, listed by their first picture only.</summary>
   public class OverworldSheetFrameSourceTests : BaseViewModelTestClass {
      private const int Table = 0x100, Data0 = 0x140, List0 = 0x200, Sheet0 = 0x800, FrameBytes = 0x100;
      private const int Frames = OverworldSheetFrameSource.PlayerSheetFrames;
      private readonly OverworldSheetFrameSource source;

      public OverworldSheetFrameSourceTests() : base(0x10000) {
         SetFullModel(0);
         Model.SetList(new ModelDelta(), "objecteventgfx", "BRENDAN_NORMAL", "MAY_NORMAL");
         // frame f starts with the pixels f and f+1 (both modulo 16)
         for (int f = 0; f < Frames; f++) Data[Sheet0 + FrameBytes * f] = (byte)((f % 16) | (((f + 1) % 16) << 4));

         ViewPort.Edit($"@{Table:X} ^{HardcodeTablesModel.OverworldSprites}[data<[tileTag: paletteid:|h reflectionPaletteTag:|h length: width: height: info. tracks. unused: oam<> subsprites<> anims<> sprites<`osl`> affine<>]1>]objecteventgfx ");
         Model.WritePointer(Token, Table, Data0);
         Model.WritePointer(Token, Table + 4, Data0);
         Model.WriteMultiByteValue(Data0 + 2, 2, Token, 0x1100); // palette id
         Model.WriteMultiByteValue(Data0 + 6, 2, Token, 0x200); // length
         Model.WriteMultiByteValue(Data0 + 8, 2, Token, 16); // width
         Model.WriteMultiByteValue(Data0 + 10, 2, Token, 32); // height
         Model.WritePointer(Token, Data0 + 28, List0);
         // the list names only the first picture, the size of one picture and a flag that says the others follow it
         ViewPort.Edit($"@{List0:X} <{Sheet0:X6}> 00 01 01 00 ");
         ViewPort.Edit($"@{Data0:X} ^sheet0[tileTag: paletteid:|h reflectionPaletteTag:|h length: width: height: info. tracks. unused: oam<> subsprites<> anims<> sprites<`osl`> affine<>]1 ");
         ViewPort.ChangeHistory.ChangeCompleted();

         source = new OverworldSheetFrameSource(Model, 0, "Sheet");
      }

      private ModelTable List => CharacterSprites.FindOverworldSpriteList(Model, 0);

      [Fact]
      public void TheSheet_IsFoundThroughTheSpriteTable_AndListsItsFirstPictureOnly() {
         Assert.NotNull(List);
         Assert.Equal(1, List.Count);
         Assert.True(CharacterSprites.IsRelativeSheet(Model, List));
         Assert.Equal(Sheet0, Model.ReadPointer(List.Run.Start));
         Assert.Null(CharacterSprites.FindOverworldSpriteList(Model, 7));
         Assert.Null(CharacterSprites.FindOverworldSpriteList(Model, -1));
      }

      [Fact]
      public void Source_HasEighteenFrames_EachOfThePictureSizeTheGameUses() {
         Assert.True(source.Prepare());
         Assert.True(source.IsValid);
         Assert.Equal(18, source.FrameCount);
         Assert.False(source.CanChooseWidth);
         var picture = source.ReadFrame(5, 2);
         Assert.Equal(16, picture.GetLength(0));
         Assert.Equal(32, picture.GetLength(1));
      }

      [Fact]
      public void Source_ReadsEveryFramesOwnBytes() {
         for (int f = 0; f < Frames; f++) {
            var picture = source.ReadFrame(f, 2);
            Assert.Equal(f % 16, picture[0, 0]);
            Assert.Equal((f + 1) % 16, picture[1, 0]);
         }
      }

      /// <summary>The CUBE metadata formats the player's picture as the whole sheet (2 tiles wide, 9 frames of 4 tiles stacked): one frame is still 16x32.</summary>
      private void FormatTheSheetAsOneTallPicture() {
         ViewPort.Edit($"@{Sheet0:X} ^sheetpicture`ucs4x2x36` ");
         var run = Model.GetNextRun(Sheet0) as SpriteRun;
         Assert.NotNull(run);
         Assert.Equal(Sheet0, run.Start);
         Assert.Equal(36, run.SpriteFormat.TileHeight);
      }

      [Fact]
      public void ReadOverworldFrame_ASheetFormattedAsOneTallPicture_ReadsOneFrameOfTheListedSize() {
         FormatTheSheetAsOneTallPicture();
         for (int f = 0; f < 9; f++) {
            var picture = CharacterSprites.ReadOverworldFrame(Model, List, f);
            Assert.Equal(16, picture.GetLength(0));
            Assert.Equal(32, picture.GetLength(1));
            Assert.Equal(f % 16, picture[0, 0]);
            Assert.Equal((f + 1) % 16, picture[1, 0]);
         }
      }

      [Fact]
      public void Source_ASheetFormattedAsOneTallPicture_StillReadsAndWritesSixteenByThirtyTwoFrames() {
         FormatTheSheetAsOneTallPicture();
         var picture = source.ReadFrame(4, 2);
         Assert.Equal(16, picture.GetLength(0));
         Assert.Equal(32, picture.GetLength(1));
         picture[0, 0] = 9;
         source.WriteFrame(ViewPort.CurrentChange, 4, 2, picture);
         Assert.Equal(9, Data[Sheet0 + FrameBytes * 4] & 0xF);
         Assert.Equal(3, Data[Sheet0 + FrameBytes * 3] & 0xF); // the frame before is untouched
         Assert.Equal(5, Data[Sheet0 + FrameBytes * 5] & 0xF); // and the one after
      }

      [Fact]
      public void Previews_ShowTheFirstThreeFramesOfTheSheet_StandingFacingDownUpAndLeft() {
         FormatTheSheetAsOneTallPicture();
         var sprites = CharacterSprites.Load(Model, CharacterRole.Male);
         Assert.Equal(3, sprites.OverworldFrames.Count);
         for (int i = 0; i < 3; i++) {
            Assert.Equal(16, sprites.OverworldFrames[i].GetLength(0));
            Assert.Equal(32, sprites.OverworldFrames[i].GetLength(1));
            Assert.Equal(i, sprites.OverworldFrames[i][0, 0]); // frame i's own first pixel
         }
      }

      [Fact]
      public void Source_AllFramesPointAtTheListForThePalette() {
         Assert.Equal(List.Run.Start, source.SpritePointer(0));
         Assert.Equal(List.Run.Start, source.SpritePointer(17));
      }

      [Fact]
      public void Source_NamesTheFramesTheWayThePlayerSheetIsLaidOut() {
         Assert.Equal("standing, facing down", source.FrameNote(0));
         Assert.Contains("walking down", source.FrameNote(3));
         Assert.Equal("running", source.FrameNote(12));
      }

      [Fact]
      public void WriteFrame_ChangesOnlyThatPicture_AndUndoTakesItBack() {
         var picture = source.ReadFrame(4, 2);
         picture[3, 5] = 9;

         source.WriteFrame(ViewPort.CurrentChange, 4, 2, picture);
         ViewPort.ChangeHistory.ChangeCompleted();

         Assert.Equal(9, source.ReadFrame(4, 2)[3, 5]);
         Assert.Equal(9, Model[Sheet0 + FrameBytes * 4 + 5 * 4 + 1] >> 4); // pixel (3, 5): the high half of byte 1 of row 5 of the first tile
         Assert.Equal(0, source.ReadFrame(3, 2)[3, 5]);
         Assert.Equal(0, source.ReadFrame(5, 2)[3, 5]);
         Assert.Equal(4, source.ReadFrame(4, 2)[0, 0]); // the rest of the picture is as it was
         ViewPort.Undo.Execute(null);
         Assert.Equal(0, source.ReadFrame(4, 2)[3, 5]);
      }

      [Fact]
      public void WriteFrame_ThroughTheEditor_StoresInTheRightPlace() {
         var picture = source.ReadFrame(17, 2);
         picture[15, 31] = 6;

         source.WriteFrame(ViewPort.CurrentChange, 17, 2, picture);

         // the last pixel of the last tile of the last picture
         Assert.Equal(6, Model[Sheet0 + FrameBytes * 17 + FrameBytes - 1] >> 4);
      }

      [Fact]
      public void WriteFrame_OverBytesTakenForAPointer_ClearsThePointerAndUndoBringsItBack() {
         var stray = Sheet0 + FrameBytes * 2 + 64; // the first row of the third tile of the picture: pixels (0..7, 8)
         AddPointer(stray, 0x300);
         Assert.IsType<PointerRun>(Model.GetNextRun(stray));
         ViewPort.ChangeHistory.ChangeCompleted();
         var picture = source.ReadFrame(2, 2);
         picture[3, 8] = 5;

         source.WriteFrame(ViewPort.CurrentChange, 2, 2, picture);
         ViewPort.ChangeHistory.ChangeCompleted();

         Assert.False(Model.GetNextRun(stray) is PointerRun run && run.Start == stray);
         Assert.Equal(5, source.ReadFrame(2, 2)[3, 8]);
         ViewPort.Undo.Execute(null);
         var back = Model.GetNextRun(stray);
         Assert.IsType<PointerRun>(back);
         Assert.Equal(stray, back.Start);
         Assert.Equal(0, source.ReadFrame(2, 2)[3, 8]);
      }

      [Fact]
      public void WriteFrame_ElsewhereInThePicture_LeavesThePointerAlone() {
         var stray = Sheet0 + FrameBytes * 2 + 64;
         AddPointer(stray, 0x300);
         ViewPort.ChangeHistory.ChangeCompleted();
         var picture = source.ReadFrame(2, 2);
         picture[3, 20] = 5;

         source.WriteFrame(ViewPort.CurrentChange, 2, 2, picture);

         Assert.IsType<PointerRun>(Model.GetNextRun(stray));
         Assert.Equal(5, source.ReadFrame(2, 2)[3, 20]);
      }

      [Fact]
      public void Source_IsNoLongerValid_OnceTheListPointsSomewhereElse() {
         Assert.True(source.IsValid);

         Model.WritePointer(Token, List0, Sheet0 + 0x40);

         Assert.False(source.IsValid);
      }

      [Fact]
      public void ASheetThatIsNotThere_CanNotBePrepared() {
         var missing = new OverworldSheetFrameSource(Model, 9, "Nothing");

         Assert.False(missing.Prepare());
         Assert.False(missing.IsValid);
         Assert.Equal(0, missing.FrameCount);
      }
   }
}
