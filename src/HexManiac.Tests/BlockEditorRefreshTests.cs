using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using HexManiac.Core.Models.Runs.Sprites;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// The block editor keeps the pictures of the tileset's tiles (the picker you choose tiles from, the tile you picked, the eight tiles of the block).
   /// When the tileset changes (a tile was drawn in the image editor, or an animated tile was stored in a blank spot of the tileset) they must follow.
   /// </summary>
   public class BlockEditorRefreshTests {
      private const int PickedTile = 5;
      private static readonly short Red = (short)(31 << 10), Green = (short)(31 << 5), Blue = 31, Yellow = (short)((31 << 10) | (31 << 5));

      private readonly PokemonModel model = new(new byte[0x100]);
      private readonly ChangeHistory<ModelDelta> history;

      public BlockEditorRefreshTests() {
         history = new ChangeHistory<ModelDelta>(change => change.Revert(model));
         model.SetList(new ModelDelta(), "MapAttributeBehaviors", "Normal", "Tall Grass", "Water");
      }

      /// <summary>1024 tiles of one colour each: tile 5 uses colour <paramref name="colorOfPickedTile"/>, every other tile colour 1.</summary>
      private static int[][,] Tiles(int colorOfPickedTile) {
         var tiles = new int[1024][,];
         for (int i = 0; i < tiles.Length; i++) {
            tiles[i] = new int[8, 8];
            var color = i == PickedTile ? colorOfPickedTile : 1;
            for (int x = 0; x < 8; x++) for (int y = 0; y < 8; y++) tiles[i][x, y] = color;
         }
         return tiles;
      }

      private static short[][] Palettes(short colorOfIndex3 = 0) {
         var palettes = new short[16][];
         for (int p = 0; p < palettes.Length; p++) palettes[p] = new short[] { 0, Blue, Green, Red, Yellow, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
         if (colorOfIndex3 != 0) palettes[0][3] = colorOfIndex3;
         return palettes;
      }

      /// <summary>Four blocks that show the picked tile (palette 0) in all eight tile positions.</summary>
      private static byte[][] Blocks() => Enumerable.Range(0, 4).Select(_ => new byte[] { PickedTile, 0, PickedTile, 0, PickedTile, 0, PickedTile, 0, PickedTile, 0, PickedTile, 0, PickedTile, 0, PickedTile, 0 }).ToArray();

      private BlockEditor Create(int[][,] tiles, short[][] palettes) {
         var attributes = Enumerable.Range(0, 4).Select(_ => new byte[2]).ToArray();
         var editor = new BlockEditor(history, model, new MapTutorialsViewModel(), palettes, tiles, Blocks(), attributes);
         editor.BlockIndex = 1; // the pictures of a block are drawn when a block is chosen
         return editor;
      }

      /// <summary>The colour the picker shows for the first pixel of a tile.</summary>
      private static short PickerColor(BlockEditor editor, int tile) {
         var picture = editor.TileRender;
         return picture.PixelData[(tile / 16) * 8 * picture.PixelWidth + (tile % 16) * 8];
      }

      [Fact]
      public void TilePicker_ShowsTheTilesWhenOpened() {
         var editor = Create(Tiles(2), Palettes());

         editor.ShowTiles = true;

         Assert.Equal(Green, PickerColor(editor, PickedTile));
         Assert.Equal(Blue, PickerColor(editor, PickedTile + 1));
      }

      [Fact]
      public void TilePicker_FollowsTilesThatChangedAfterItWasDrawn() {
         var editor = Create(Tiles(2), Palettes());
         editor.ShowTiles = true;
         var old = editor.TileRender;
         var changes = new List<string>();
         editor.PropertyChanged += (sender, e) => changes.Add(e.PropertyName);

         editor.RefreshTileCache(Tiles(3)); // tile 5 was drawn on

         Assert.NotSame(old, editor.TileRender);
         Assert.Equal(Red, PickerColor(editor, PickedTile));
         Assert.Equal(Blue, PickerColor(editor, PickedTile + 1));
         Assert.Contains(nameof(BlockEditor.TileRender), changes);
      }

      [Fact]
      public void TilePicker_IsNotDrawnAgainForTheSameTiles() {
         var editor = Create(Tiles(2), Palettes());
         editor.ShowTiles = true;
         var old = editor.TileRender;
         var changes = new List<string>();
         editor.PropertyChanged += (sender, e) => changes.Add(e.PropertyName);

         editor.RefreshTileCache(Tiles(2)); // a copy that says the same thing: the editor refreshes this way every time the tab is shown
         editor.RefreshPaletteCache(Palettes());

         Assert.Same(old, editor.TileRender);
         Assert.DoesNotContain(nameof(BlockEditor.TileRender), changes);
      }

      [Fact]
      public void TilePicker_NotShownYet_UsesTheNewTilesWhenItIs() {
         var editor = Create(Tiles(2), Palettes());

         editor.RefreshTileCache(Tiles(4));
         Assert.Null(editor.TileRender); // nothing was needed yet, nothing was drawn

         editor.ShowTiles = true;
         Assert.Equal(Yellow, PickerColor(editor, PickedTile));
      }

      [Fact]
      public void TilePicker_FollowsPalettesThatChanged() {
         var editor = Create(Tiles(3), Palettes());
         editor.ShowTiles = true;
         Assert.Equal(Red, PickerColor(editor, PickedTile));

         editor.RefreshPaletteCache(Palettes(colorOfIndex3: Green));

         Assert.Equal(Green, PickerColor(editor, PickedTile));
      }

      [Fact]
      public void PickedTile_FollowsTilesThatChanged() {
         var editor = Create(Tiles(2), Palettes());
         editor.ShowTiles = true;
         editor.TileSelectionX = PickedTile * 24; // 24 pixels per tile in the picker
         Assert.Equal(Green, editor.DrawTileRender.PixelData[0]);

         editor.RefreshTileCache(Tiles(3));

         Assert.Equal(Red, editor.DrawTileRender.PixelData[0]);
      }

      [Fact]
      public void BlockPictures_FollowTilesThatChanged() {
         var editor = Create(Tiles(2), Palettes());
         Assert.Equal(Green, editor.LeftTopFront.PixelData[0]);
         Assert.Equal(Green, editor.RightBottomBack.PixelData[0]);
         var changes = new List<string>();
         editor.PropertyChanged += (sender, e) => changes.Add(e.PropertyName);

         editor.RefreshTileCache(Tiles(3));

         Assert.Equal(Red, editor.LeftTopFront.PixelData[0]);
         Assert.Equal(Red, editor.RightBottomBack.PixelData[0]);
         Assert.Contains(nameof(BlockEditor.LeftTopFront), changes);
      }

      [Fact]
      public void BlockPictures_AreDrawnWhenTheFirstBlockChosenIsBlockZero() {
         var attributes = Enumerable.Range(0, 4).Select(_ => new byte[2]).ToArray();
         var editor = new BlockEditor(history, model, new MapTutorialsViewModel(), Palettes(), Tiles(2), Blocks(), attributes);

         editor.BlockIndex = 0; // the editor starts on block 0; the map editor chooses it the same way when the block is picked

         Assert.Equal(Green, editor.LeftTopFront.PixelData[0]);
         Assert.Equal(Green, editor.RightBottomBack.PixelData[0]);
      }

      [Fact]
      public void DrawOnTile_ShowsTheNewTileOnThePictureThatWasDrawnOn_NotOnTheOneThatWasHovered() {
         var editor = Create(Tiles(2), Palettes());
         editor.ShowTiles = true;
         editor.TileSelectionX = 7 * 24; // tile 7 is blue
         editor.EnterTile(editor.LeftTopBack);

         editor.DrawOnTile(editor.RightTopFront); // the click arrives without a new "mouse enter" (touch, pen, a click right after the block changed)

         Assert.Equal(Blue, editor.RightTopFront.PixelData[0]);
         Assert.Equal(Green, editor.LeftTopBack.PixelData[0]);
      }
   }

   /// <summary>
   /// The block editor edits a copy of the map's blocks and writes the blocks the map uses back to the tileset's tables.
   /// The highest block in use must be among them (it is the block that was added last, and the one people edit).
   /// </summary>
   public class BlockWriteBackTests {
      private const int Primary = 0x100, Secondary = 0x140, PrimaryBlocks = 0x200, PrimaryAttributes = 0x300, SecondaryBlocks = 0x400, SecondaryAttributes = 0x500;
      private readonly PokemonModel model = new(new byte[0x1000]);
      private readonly ModelDelta token = new();
      private readonly BlocksetModel primary, secondary;

      public BlockWriteBackTests() {
         token.ChangeData(model, Primary + 0, 0); // not compressed
         token.ChangeData(model, Secondary + 1, 1); // secondary
         model.WritePointer(token, Primary + 12, PrimaryBlocks);
         model.WritePointer(token, Primary + 16, PrimaryAttributes);
         model.WritePointer(token, Secondary + 12, SecondaryBlocks);
         model.WritePointer(token, Secondary + 16, SecondaryAttributes);
         primary = new BlocksetModel(model, Primary);
         secondary = new BlocksetModel(model, Secondary);
      }

      private byte[][] AllBlocks(int secondaryCount) => Enumerable.Range(0, primary.PrimaryBlocks + secondaryCount).Select(_ => new byte[16]).ToArray();
      private byte[][] AllAttributes(int secondaryCount) => Enumerable.Range(0, primary.PrimaryBlocks + secondaryCount).Select(_ => new byte[2]).ToArray();

      [Fact]
      public void WriteBlocks_HighestSecondaryBlockInUse_IsWritten() {
         var blocks = AllBlocks(secondaryCount: 4);
         blocks[primary.PrimaryBlocks + 3] = Enumerable.Range(1, 16).Select(i => (byte)i).ToArray();

         BlockmapRun.WriteBlocks(() => token, maxUsedPrimary: 0, maxUsedSecondary: 3, primary, secondary, blocks);

         Assert.Equal(blocks[primary.PrimaryBlocks + 3], model.RawData.Skip(SecondaryBlocks + 3 * 16).Take(16));
      }

      [Fact]
      public void WriteBlocks_OnlyBlockInUseOfThePrimaryBlockset_IsWritten() {
         var blocks = AllBlocks(secondaryCount: 1);
         blocks[0] = Enumerable.Range(1, 16).Select(i => (byte)i).ToArray();

         BlockmapRun.WriteBlocks(() => token, maxUsedPrimary: 0, maxUsedSecondary: 0, primary, secondary, blocks);

         Assert.Equal(blocks[0], model.RawData.Skip(PrimaryBlocks).Take(16));
      }

      [Fact]
      public void WriteBlocks_BlocksAboveTheHighestInUse_AreLeftAlone() {
         var blocks = AllBlocks(secondaryCount: 6);
         for (int i = 0; i < 6; i++) blocks[primary.PrimaryBlocks + i] = Enumerable.Repeat((byte)(i + 1), 16).ToArray();

         BlockmapRun.WriteBlocks(() => token, maxUsedPrimary: 0, maxUsedSecondary: 3, primary, secondary, blocks);

         Assert.All(model.RawData.Skip(SecondaryBlocks + 4 * 16).Take(2 * 16), b => Assert.Equal(0, b));
         Assert.Equal(4, model.RawData[SecondaryBlocks + 3 * 16]);
      }

      [Fact]
      public void WriteBlockAttributes_HighestSecondaryBlockInUse_IsWritten() {
         var attributes = AllAttributes(secondaryCount: 4);
         attributes[primary.PrimaryBlocks + 3] = new byte[] { 0x63, 0x01 };

         BlockmapRun.WriteBlockAttributes(() => token, maxUsedPrimary: 0, maxUsedSecondary: 3, primary, secondary, attributes);

         Assert.Equal(new byte[] { 0x63, 0x01 }, model.RawData.Skip(SecondaryAttributes + 3 * 2).Take(2));
      }

      [Fact]
      public void WriteBlockAttributes_OnlyBlockInUseOfThePrimaryBlockset_IsWritten() {
         var attributes = AllAttributes(secondaryCount: 1);
         attributes[0] = new byte[] { 0x05, 0x02 };

         BlockmapRun.WriteBlockAttributes(() => token, maxUsedPrimary: 0, maxUsedSecondary: 0, primary, secondary, attributes);

         Assert.Equal(new byte[] { 0x05, 0x02 }, model.RawData.Skip(PrimaryAttributes).Take(2));
      }
   }
}
