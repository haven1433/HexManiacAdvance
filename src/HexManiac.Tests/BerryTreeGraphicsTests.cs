using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Map;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// The pictures of berry trees in the map editor.
   /// The expansion lists a berry's frames and palette slots in the berry stats; the vanilla games (no fields there) keep two arrays of pointers
   /// (gBerryTreePicTablePointers, gBerryTreePaletteSlotTablePointers) that no default metadata names, so they are found by their shape.
   /// The tests build the same layout from scratch: 43 berries, every berry with its own table of 9 frames and its own list of 5 palette slots.
   /// </summary>
   public class BerryTreeGraphicsTests : BaseViewModelTestClass {
      public const int
         BerryStats = 0x100,
         Pictures = 0x200,      // 43 pointers to the frame tables
         SlotLists = 0x400,     // 43 lists of 5 palette slots, 8 bytes apart
         FrameTables = 0x600,   // 43 tables of 9 frames, 0x48 bytes each
         SmallBlocks = 0x1300,  // 4 pictures of 16x16 pixels (0x80 bytes), all pixels the same color index
         BigBlocks = 0x1500,    // 4 pictures of 16x32 pixels (0x100 bytes)
         PalTable = 0x1A00,     // graphics.overworld.palettes: 4 palettes
         Palettes = 0x1B00,
         DecoyTarget = 0x1C00,  // zeros
         RealSlots = 0x1D00,    // far from the pictures array
         DecoySlots = 0x0140,   // 43 pointers to zeros, below everything else
         Objects = 0x2000,
         MovedPictures = 0x3000,
         MovedSlots = 0x3100,
         SpriteTable = 0x3200,
         SpriteInfo = 0x3220,
         FrameTableLength = 0x48,
         BerryCount = 43;

      public BerryTreeGraphicsTests() : base(0x4000) { }

      private void Pointer(int at, int target) {
         var value = (uint)(target + 0x08000000);
         for (int i = 0; i < 4; i++) Data[at + i] = (byte)(value >> (8 * i));
      }

      /// <summary>
      /// Berry k draws block k%4 in every frame, and uses the palette slot 2 + (k + stage) % 4 for each of the 5 stages.
      /// Slot s selects the s-2'th entry of graphics.overworld.palettes (like the map editor and the game do), whose colors are all different.
      /// </summary>
      private void BuildVanillaTrees(int slotArray) {
         for (int block = 0; block < 4; block++) {
            for (int i = 0; i < 0x80; i++) Data[SmallBlocks + block * 0x80 + i] = (byte)((block + 1) * 0x11);
            for (int i = 0; i < 0x100; i++) Data[BigBlocks + block * 0x100 + i] = (byte)((block + 1) * 0x11);
         }
         for (int berry = 0; berry < BerryCount; berry++) {
            var table = FrameTables + berry * FrameTableLength;
            for (int frame = 0; frame < 9; frame++) {
               var small = frame < 3;
               Pointer(table + frame * 8, (small ? SmallBlocks : BigBlocks) + (berry % 4) * (small ? 0x80 : 0x100));
               Data[table + frame * 8 + 4] = (byte)(small ? 0x80 : 0x00);
               Data[table + frame * 8 + 5] = (byte)(small ? 0x00 : 0x01);
            }
            for (int stage = 0; stage < 5; stage++) Data[SlotLists + berry * 8 + stage] = (byte)(2 + (berry + stage) % 4);
            Pointer(Pictures + berry * 4, table);
            Pointer(slotArray + berry * 4, SlotLists + berry * 8);
         }
         for (int palette = 0; palette < 4; palette++) {
            Pointer(PalTable + palette * 6, Palettes + palette * 0x20);
            Data[PalTable + palette * 6 + 4] = (byte)(palette + 1);
            for (int color = 0; color < 16; color++) {
               var value = (palette * 16 + color + 1) * 0x21;
               Data[Palettes + palette * 0x20 + color * 2] = (byte)value;
               Data[Palettes + palette * 0x20 + color * 2 + 1] = (byte)(value >> 8);
            }
         }
         ViewPort.Edit($"@{PalTable:X} ^{HardcodeTablesModel.OverworldPalettes}[pal<`ucp4`> id:|h]4 ");
         ViewPort.Edit($"@{BerryStats:X} ^{HardcodeTablesModel.BerryTableName}[firmness.]{BerryCount} ");
      }

      private short ExpectedColor(int berry, int stage) {
         var palette = (IPaletteRun)Model.GetNextRun(Palettes + ((berry + stage) % 4) * 0x20);
         return palette.GetPalette(Model, 0)[berry % 4 + 1];
      }

      #region the vanilla layout: two arrays of pointers

      [Fact]
      public void VanillaLayout_EveryBerryHasItsOwnTables() {
         BuildVanillaTrees(Pictures + BerryCount * 4);

         Assert.False(BerryTreeGraphics.UsesBerryStatsFields(Model));
         Assert.Equal(5, BerryTreeGraphics.StageCount(Model));
         for (int berry = 0; berry < BerryCount; berry++) {
            Assert.True(BerryTreeGraphics.TryGetTables(Model, berry, out var pictureTable, out var paletteSlots));
            Assert.Equal(FrameTables + berry * FrameTableLength, pictureTable);
            Assert.Equal(SlotLists + berry * 8, paletteSlots);
         }
         Assert.False(BerryTreeGraphics.TryGetTables(Model, BerryCount, out _, out _));
         Assert.False(BerryTreeGraphics.TryGetTables(Model, -1, out _, out _));
      }

      [Fact]
      public void VanillaLayout_PaletteSlotsFarFromThePictures_AreFoundAndDecoysAreSkipped() {
         // a table of pointers to empty data below the real thing is not a list of palette slots (slots 0 and 1 belong to the player)
         for (int i = 0; i < BerryCount; i++) Pointer(DecoySlots + i * 4, DecoyTarget);
         BuildVanillaTrees(RealSlots);

         Assert.True(BerryTreeGraphics.TryGetTables(Model, 7, out var pictureTable, out var paletteSlots));

         Assert.Equal(FrameTables + 7 * FrameTableLength, pictureTable);
         Assert.Equal(SlotLists + 7 * 8, paletteSlots);
      }

      [Fact]
      public void VanillaLayout_TablesMovedToANewPlace_AreFoundAgain() {
         BuildVanillaTrees(Pictures + BerryCount * 4);
         Assert.True(BerryTreeGraphics.TryGetTables(Model, 3, out var before, out _));

         // the hack moves both arrays: the old ones are cleared, the new ones point at the same tables
         for (int i = 0; i < BerryCount; i++) {
            for (int b = 0; b < 4; b++) {
               Data[MovedPictures + i * 4 + b] = Data[Pictures + i * 4 + b];
               Data[MovedSlots + i * 4 + b] = Data[Pictures + BerryCount * 4 + i * 4 + b];
               Data[Pictures + i * 4 + b] = 0;
               Data[Pictures + BerryCount * 4 + i * 4 + b] = 0;
            }
         }

         Assert.True(BerryTreeGraphics.TryGetTables(Model, 3, out var after, out var slots));
         Assert.Equal(before, after);
         Assert.Equal(SlotLists + 3 * 8, slots);
      }

      [Fact]
      public void VanillaLayout_NoArrays_NoTables() {
         ViewPort.Edit($"@{BerryStats:X} ^{HardcodeTablesModel.BerryTableName}[firmness.]{BerryCount} ");

         Assert.False(BerryTreeGraphics.TryGetTables(Model, 0, out _, out _));
         Assert.Null(ObjectEventViewModel.RenderBerryStage(Model, 0, 4));
         Assert.Equal(-1, BerryTreeGraphics.FindBerryWithPictureTable(Model, FrameTables));
      }

      [Fact]
      public void VanillaLayout_FrameTable_BelongsToItsBerry() {
         BuildVanillaTrees(Pictures + BerryCount * 4);

         Assert.Equal(0, BerryTreeGraphics.FindBerryWithPictureTable(Model, FrameTables));
         Assert.Equal(12, BerryTreeGraphics.FindBerryWithPictureTable(Model, FrameTables + 12 * FrameTableLength));
         Assert.Equal(-1, BerryTreeGraphics.FindBerryWithPictureTable(Model, FrameTables + 4));
         Assert.Equal(-1, BerryTreeGraphics.FindBerryWithPictureTable(Model, -1));
      }

      [Theory]
      [InlineData(0, 0)]
      [InlineData(0, 4)]
      [InlineData(5, 1)]
      [InlineData(5, 2)]
      [InlineData(21, 3)]
      [InlineData(42, 4)]
      public void VanillaLayout_RenderBerryStage_DrawsTheBerrysFrameInTheStagesPalette(int berry, int stage) {
         BuildVanillaTrees(Pictures + BerryCount * 4);

         var picture = ObjectEventViewModel.RenderBerryStage(Model, berry, stage);

         Assert.NotNull(picture);
         Assert.Equal(16, picture.PixelWidth);
         Assert.Equal(stage < 2 ? 16 : 32, picture.PixelHeight);
         // every pixel of the synthetic frames has the same color index, so the color tells which frame table and which palette slot were used
         Assert.All(picture.PixelData, pixel => Assert.Equal(ExpectedColor(berry, stage), pixel));
      }

      [Fact]
      public void VanillaLayout_StageWithoutAFrame_IsClamped() {
         BuildVanillaTrees(Pictures + BerryCount * 4);

         // the 'trunk' and 'budding' stages only exist in the expansion
         var last = ObjectEventViewModel.RenderBerryStage(Model, 2, 4);
         var beyond = ObjectEventViewModel.RenderBerryStage(Model, 2, 6);

         Assert.Equal(last.PixelData, beyond.PixelData);
      }

      #endregion

      #region the expansion layout: fields of the berry stats

      private void BuildExpansionTrees() {
         BuildVanillaTrees(Pictures + BerryCount * 4);
         // the same tables, but listed in the berry stats of 2 berries (in the other order, to see that the stats are used)
         ViewPort.Edit($"@{BerryStats:X} ^{HardcodeTablesModel.BerryTableName}[firmness. treePicTable<> treePaletteSlots<>]2 ");
         // (9 bytes each: firmness, then the two pointers)
         Pointer(BerryStats + 1, FrameTables + 9 * FrameTableLength);
         Pointer(BerryStats + 5, SlotLists + 9 * 8);
         Pointer(BerryStats + 10, FrameTables + 10 * FrameTableLength);
         Pointer(BerryStats + 14, SlotLists + 10 * 8);
      }

      [Fact]
      public void ExpansionLayout_TablesComeFromTheBerryStats() {
         BuildExpansionTrees();

         Assert.True(BerryTreeGraphics.UsesBerryStatsFields(Model));
         Assert.Equal(7, BerryTreeGraphics.StageCount(Model));
         Assert.True(BerryTreeGraphics.TryGetTables(Model, 1, out var pictureTable, out var paletteSlots));
         Assert.Equal(FrameTables + 10 * FrameTableLength, pictureTable);
         Assert.Equal(SlotLists + 10 * 8, paletteSlots);
         // the arrays of the vanilla games are not consulted, and there are only the berries of the stats
         Assert.False(BerryTreeGraphics.TryGetTables(Model, 2, out _, out _));
         Assert.Equal(-1, BerryTreeGraphics.FindBerryWithPictureTable(Model, FrameTables + 10 * FrameTableLength));
      }

      [Fact]
      public void ExpansionLayout_RenderBerryStage_IsDrawnFromTheStatsFields() {
         BuildExpansionTrees();

         var picture = ObjectEventViewModel.RenderBerryStage(Model, 0, 4);

         // berry 0 of the stats is the tables of berry 9 here: block 9 % 4, slot 2 + (9 + 4) % 4
         Assert.All(picture.PixelData, pixel => Assert.Equal(ExpectedColor(9, 4), pixel));
      }

      #endregion

      #region the event panel

      private ObjectEventViewModel CreateTree(int berry) {
         ViewPort.Edit($"@{Objects:X} ^objects[id. graphics. kind. x:|z y:|z elevation. moveType. range:|t|x::|y:: trainerType: trainerRangeOrBerryID: script<> flag: padding:]1 ");
         var element = new ModelTable(Model, (ITableRun)Model.GetNextRun(Objects), () => Token)[0];
         Model.SetList(Token, "FacingOptions", new[] { "stand", "walk" }, "0");
         var info = new BerryInfo(new Dictionary<int, BerrySpot> { [1] = new BerrySpot(0, berry, 5) }, new ObservableCollection<string>(), 1);
         var tree = new ObjectEventViewModel(null, address => { }, element, null, new List<IPixelViewModel>(), null, info);
         tree.TrainerRangeOrBerryID = 1;
         return tree;
      }

      [Fact]
      public void VanillaLayout_GrowthDropdown_HasFiveStagesWithPictures() {
         BuildVanillaTrees(Pictures + BerryCount * 4);
         var tree = CreateTree(2);

         var stages = tree.BerryStageVisuals;

         Assert.Equal(new[] { "Planted", "Sprouted", "Taller", "Flowering", "Has berries" }, tree.BerryStageOptions);
         Assert.Equal(5, stages.Count);
         Assert.Equal(new[] { 16 * 16, 16 * 16, 16 * 32, 16 * 32, 16 * 32 }, stages.Select(stage => stage.PixelData.Length));
         Assert.Equal(new[] { "Planted", "Sprouted", "Taller", "Flowering", "Has berries" }, stages.Select(stage => stage.Text));
         // berry 2 uses another palette slot at each stage
         Assert.Equal(Enumerable.Range(0, 5).Select(stage => ExpectedColor(2, stage)), stages.Select(stage => stage.PixelData[0]));
         Assert.Equal(4, stages.Select(stage => stage.PixelData[0]).Distinct().Count());
      }

      [Fact]
      public void ExpansionLayout_GrowthDropdown_HasSevenStages() {
         BuildExpansionTrees();
         var tree = CreateTree(0);

         Assert.Equal(7, tree.BerryStageOptions.Count);
         Assert.Equal(7, tree.BerryStageVisuals.Count);
         Assert.All(tree.BerryStageVisuals, stage => Assert.NotEmpty(stage.PixelData));
      }

      [Fact]
      public void NoTreeTables_GrowthDropdown_StillListsTheStagesWithBlankPictures() {
         ViewPort.Edit($"@{BerryStats:X} ^{HardcodeTablesModel.BerryTableName}[firmness.]{BerryCount} ");
         var tree = CreateTree(2);

         Assert.Equal(5, tree.BerryStageOptions.Count);
         Assert.Equal(5, tree.BerryStageVisuals.Count);
      }

      #endregion

      #region the object graphics dropdown

      /// <summary>The generic berry tree graphics of the vanilla games draw the frame table of one berry: the dropdown shows that berry's tree.</summary>
      [Fact]
      public void VanillaLayout_ObjectGraphicsWithABerrysFrameTable_ShowsTheTree() {
         BuildVanillaTrees(Pictures + BerryCount * 4);
         ViewPort.Edit($"@{SpriteInfo:X} FF FF 01 00 40 00 10 00 20 00 <{FrameTables:X6}>");
         ViewPort.Edit($"@{SpriteInfo + 0x20:X} FF FF 01 00 40 00 10 00 10 00 <{FrameTables + FrameTableLength:X6}>");
         Model.SetList(new ModelDelta(), "owNames", "TREE", "SPROUT");
         ViewPort.Edit($"@{SpriteTable:X} <{SpriteInfo:X6}> <{SpriteInfo + 0x20:X6}> @{SpriteTable:X} ^{HardcodeTablesModel.OverworldSprites}[data<[starterbytes:|h paletteid:|h length: width: height: sprites<`osl`>]1>]owNames ");
         var table = (ITableRun)Model.GetTable(HardcodeTablesModel.OverworldSprites);

         var options = OverworldComboOptions.Render(Model, table, new[] { new ComboOption("TREE", 0), new ComboOption("SPROUT", 1) });

         // a tall graphic is drawn as the berry with berries (stage 4), a 16 pixel high one as the sprout (stage 1)
         var tree = Assert.IsType<VisualComboOption>(options[0]);
         Assert.Equal(16 * 32, tree.PixelData.Length);
         Assert.All(tree.PixelData, pixel => Assert.Equal(ExpectedColor(0, 4), pixel));
         var sprout = Assert.IsType<VisualComboOption>(options[1]);
         Assert.Equal(16 * 16, sprout.PixelData.Length);
         Assert.All(sprout.PixelData, pixel => Assert.Equal(ExpectedColor(1, 1), pixel));
      }

      #endregion
   }
}
