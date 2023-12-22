using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using HexManiac.Core.Models.Runs.Sprites;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

/*
      #define MB_NORMAL 0x00
      #define MB_SECRET_BASE_WALL 0x01
      #define MB_TALL_GRASS 0x02
      #define MB_LONG_GRASS 0x03
      #define MB_UNUSED_04 0x04
      #define MB_UNUSED_05 0x05
      #define MB_DEEP_SAND 0x06
      #define MB_SHORT_GRASS 0x07
      #define MB_CAVE 0x08
      #define MB_LONG_GRASS_SOUTH_EDGE 0x09
      #define MB_NO_RUNNING 0x0A
      #define MB_INDOOR_ENCOUNTER 0x0B
      #define MB_MOUNTAIN_TOP 0x0C
      #define MB_BATTLE_PYRAMID_WARP 0x0D
      #define MB_MOSSDEEP_GYM_WARP 0x0E
      #define MB_MT_PYRE_HOLE 0x0F
      #define MB_POND_WATER 0x10
      #define MB_SEMI_DEEP_WATER 0x11
      #define MB_DEEP_WATER 0x12
      #define MB_WATERFALL 0x13
      #define MB_SOOTOPOLIS_DEEP_WATER 0x14
      #define MB_OCEAN_WATER 0x15
      #define MB_PUDDLE 0x16
      #define MB_SHALLOW_WATER 0x17
      #define MB_UNUSED_SOOTOPOLIS_DEEP_WATER 0x18
      #define MB_NO_SURFACING 0x19
      #define MB_UNUSED_SOOTOPOLIS_DEEP_WATER_2 0x1A
      #define MB_STAIRS_OUTSIDE_ABANDONED_SHIP 0x1B
      #define MB_SHOAL_CAVE_ENTRANCE 0x1C
      #define MB_UNUSED_1D 0x1D
      #define MB_UNUSED_1E 0x1E
      #define MB_UNUSED_1F 0x1F
      #define MB_ICE 0x20
      #define MB_SAND 0x21
      #define MB_SEAWEED 0x22
      #define MB_UNUSED_23 0x23
      #define MB_ASHGRASS 0x24
      #define MB_FOOTPRINTS 0x25
      #define MB_THIN_ICE 0x26
      #define MB_CRACKED_ICE 0x27
      #define MB_HOT_SPRINGS 0x28
      #define MB_LAVARIDGE_GYM_B1F_WARP 0x29
      #define MB_SEAWEED_NO_SURFACING 0x2A
      #define MB_REFLECTION_UNDER_BRIDGE 0x2B
      #define MB_UNUSED_2C 0x2C
      #define MB_UNUSED_2D 0x2D
      #define MB_UNUSED_2E 0x2E
      #define MB_UNUSED_2F 0x2F
      #define MB_IMPASSABLE_EAST 0x30
      #define MB_IMPASSABLE_WEST 0x31
      #define MB_IMPASSABLE_NORTH 0x32
      #define MB_IMPASSABLE_SOUTH 0x33
      #define MB_IMPASSABLE_NORTHEAST 0x34
      #define MB_IMPASSABLE_NORTHWEST 0x35
      #define MB_IMPASSABLE_SOUTHEAST 0x36
      #define MB_IMPASSABLE_SOUTHWEST 0x37
      #define MB_JUMP_EAST 0x38
      #define MB_JUMP_WEST 0x39
      #define MB_JUMP_NORTH 0x3A
      #define MB_JUMP_SOUTH 0x3B
      #define MB_JUMP_NORTHEAST 0x3C
      #define MB_JUMP_NORTHWEST 0x3D
      #define MB_JUMP_SOUTHEAST 0x3E
      #define MB_JUMP_SOUTHWEST 0x3F
      #define MB_WALK_EAST 0x40
      #define MB_WALK_WEST 0x41
      #define MB_WALK_NORTH 0x42
      #define MB_WALK_SOUTH 0x43
      #define MB_SLIDE_EAST 0x44
      #define MB_SLIDE_WEST 0x45
      #define MB_SLIDE_NORTH 0x46
      #define MB_SLIDE_SOUTH 0x47
      #define MB_TRICK_HOUSE_PUZZLE_8_FLOOR 0x48
      #define MB_UNUSED_49 0x49
      #define MB_UNUSED_4A 0x4A
      #define MB_UNUSED_4B 0x4B
      #define MB_UNUSED_4C 0x4C
      #define MB_UNUSED_4D 0x4D
      #define MB_UNUSED_4E 0x4E
      #define MB_UNUSED_4F 0x4F
      #define MB_EASTWARD_CURRENT 0x50
      #define MB_WESTWARD_CURRENT 0x51
      #define MB_NORTHWARD_CURRENT 0x52
      #define MB_SOUTHWARD_CURRENT 0x53
      #define MB_UNUSED_54 0x54
      #define MB_UNUSED_55 0x55
      #define MB_UNUSED_56 0x56
      #define MB_UNUSED_57 0x57
      #define MB_UNUSED_58 0x58
      #define MB_UNUSED_59 0x59
      #define MB_UNUSED_5A 0x5A
      #define MB_UNUSED_5B 0x5B
      #define MB_UNUSED_5C 0x5C
      #define MB_UNUSED_5D 0x5D
      #define MB_UNUSED_5E 0x5E
      #define MB_UNUSED_5F 0x5F
      #define MB_NON_ANIMATED_DOOR 0x60
      #define MB_LADDER 0x61
      #define MB_EAST_ARROW_WARP 0x62
      #define MB_WEST_ARROW_WARP 0x63
      #define MB_NORTH_ARROW_WARP 0x64
      #define MB_SOUTH_ARROW_WARP 0x65
      #define MB_CRACKED_FLOOR_HOLE 0x66
      #define MB_AQUA_HIDEOUT_WARP 0x67
      #define MB_LAVARIDGE_GYM_1F_WARP 0x68
      #define MB_ANIMATED_DOOR 0x69
      #define MB_UP_ESCALATOR 0x6A
      #define MB_DOWN_ESCALATOR 0x6B
      #define MB_WATER_DOOR 0x6C
      #define MB_WATER_SOUTH_ARROW_WARP 0x6D
      #define MB_DEEP_SOUTH_WARP 0x6E
      #define MB_UNUSED_6F 0x6F
      #define MB_BRIDGE_OVER_OCEAN 0x70
      #define MB_BRIDGE_OVER_POND_LOW 0x71
      #define MB_BRIDGE_OVER_POND_MED 0x72
      #define MB_BRIDGE_OVER_POND_HIGH 0x73
      #define MB_PACIFIDLOG_VERTICAL_LOG_TOP 0x74
      #define MB_PACIFIDLOG_VERTICAL_LOG_BOTTOM 0x75
      #define MB_PACIFIDLOG_HORIZONTAL_LOG_LEFT 0x76
      #define MB_PACIFIDLOG_HORIZONTAL_LOG_RIGHT 0x77
      #define MB_FORTREE_BRIDGE 0x78
      #define MB_UNUSED_79 0x79
      #define MB_BRIDGE_OVER_POND_MED_EDGE_1 0x7A
      #define MB_BRIDGE_OVER_POND_MED_EDGE_2 0x7B
      #define MB_BRIDGE_OVER_POND_HIGH_EDGE_1 0x7C
      #define MB_BRIDGE_OVER_POND_HIGH_EDGE_2 0x7D
      #define MB_UNUSED_BRIDGE 0x7E
      #define MB_BIKE_BRIDGE_OVER_BARRIER 0x7F
      #define MB_COUNTER 0x80
      #define MB_UNUSED_81 0x81
      #define MB_UNUSED_82 0x82
      #define MB_PC 0x83
      #define MB_CABLE_BOX_RESULTS_1 0x84
      #define MB_REGION_MAP 0x85
      #define MB_TELEVISION 0x86
      #define MB_POKEBLOCK_FEEDER 0x87
      #define MB_UNUSED_88 0x88
      #define MB_SLOT_MACHINE 0x89
      #define MB_ROULETTE 0x8A
      #define MB_CLOSED_SOOTOPOLIS_DOOR 0x8B
      #define MB_TRICK_HOUSE_PUZZLE_DOOR 0x8C
      #define MB_PETALBURG_GYM_DOOR 0x8D
      #define MB_RUNNING_SHOES_INSTRUCTION 0x8E
      #define MB_QUESTIONNAIRE 0x8F
      #define MB_SECRET_BASE_SPOT_RED_CAVE 0x90
      #define MB_SECRET_BASE_SPOT_RED_CAVE_OPEN 0x91
      #define MB_SECRET_BASE_SPOT_BROWN_CAVE 0x92
      #define MB_SECRET_BASE_SPOT_BROWN_CAVE_OPEN 0x93
      #define MB_SECRET_BASE_SPOT_YELLOW_CAVE 0x94
      #define MB_SECRET_BASE_SPOT_YELLOW_CAVE_OPEN 0x95
      #define MB_SECRET_BASE_SPOT_TREE_LEFT 0x96
      #define MB_SECRET_BASE_SPOT_TREE_LEFT_OPEN 0x97
      #define MB_SECRET_BASE_SPOT_SHRUB 0x98
      #define MB_SECRET_BASE_SPOT_SHRUB_OPEN 0x99
      #define MB_SECRET_BASE_SPOT_BLUE_CAVE 0x9A
      #define MB_SECRET_BASE_SPOT_BLUE_CAVE_OPEN 0x9B
      #define MB_SECRET_BASE_SPOT_TREE_RIGHT 0x9C
      #define MB_SECRET_BASE_SPOT_TREE_RIGHT_OPEN 0x9D
      #define MB_UNUSED_9E 0x9E
      #define MB_UNUSED_9F 0x9F
      #define MB_BERRY_TREE_SOIL 0xA0
      #define MB_UNUSED_A1 0xA1
      #define MB_UNUSED_A2 0xA2
      #define MB_UNUSED_A3 0xA3
      #define MB_UNUSED_A4 0xA4
      #define MB_UNUSED_A5 0xA5
      #define MB_UNUSED_A6 0xA6
      #define MB_UNUSED_A7 0xA7
      #define MB_UNUSED_A8 0xA8
      #define MB_UNUSED_A9 0xA9
      #define MB_UNUSED_AA 0xAA
      #define MB_UNUSED_AB 0xAB
      #define MB_UNUSED_AC 0xAC
      #define MB_UNUSED_AD 0xAD
      #define MB_UNUSED_AE 0xAE
      #define MB_UNUSED_AF 0xAF
      #define MB_SECRET_BASE_PC 0xB0
      #define MB_SECRET_BASE_REGISTER_PC 0xB1
      #define MB_SECRET_BASE_SCENERY 0xB2
      #define MB_SECRET_BASE_TRAINER_SPOT 0xB3
      #define MB_SECRET_BASE_DECORATION 0xB4
      #define MB_HOLDS_SMALL_DECORATION 0xB5
      #define MB_UNUSED_B6 0xB6
      #define MB_SECRET_BASE_NORTH_WALL 0xB7
      #define MB_SECRET_BASE_BALLOON 0xB8
      #define MB_SECRET_BASE_IMPASSABLE 0xB9
      #define MB_SECRET_BASE_GLITTER_MAT 0xBA
      #define MB_SECRET_BASE_JUMP_MAT 0xBB
      #define MB_SECRET_BASE_SPIN_MAT 0xBC
      #define MB_SECRET_BASE_SOUND_MAT 0xBD
      #define MB_SECRET_BASE_BREAKABLE_DOOR 0xBE
      #define MB_SECRET_BASE_SAND_ORNAMENT 0xBF
      #define MB_IMPASSABLE_SOUTH_AND_NORTH 0xC0
      #define MB_IMPASSABLE_WEST_AND_EAST 0xC1
      #define MB_SECRET_BASE_HOLE 0xC2
      #define MB_HOLDS_LARGE_DECORATION 0xC3
      #define MB_SECRET_BASE_TV_SHIELD 0xC4
      #define MB_PLAYER_ROOM_PC_ON 0xC5
      #define MB_SECRET_BASE_DECORATION_BASE 0xC6
      #define MB_SECRET_BASE_POSTER 0xC7
      #define MB_UNUSED_C8 0xC8
      #define MB_UNUSED_C9 0xC9
      #define MB_UNUSED_CA 0xCA
      #define MB_UNUSED_CB 0xCB
      #define MB_UNUSED_CC 0xCC
      #define MB_UNUSED_CD 0xCD
      #define MB_UNUSED_CE 0xCE
      #define MB_UNUSED_CF 0xCF
      #define MB_MUDDY_SLOPE 0xD0
      #define MB_BUMPY_SLOPE 0xD1
      #define MB_CRACKED_FLOOR 0xD2
      #define MB_ISOLATED_VERTICAL_RAIL 0xD3
      #define MB_ISOLATED_HORIZONTAL_RAIL 0xD4
      #define MB_VERTICAL_RAIL 0xD5
      #define MB_HORIZONTAL_RAIL 0xD6
      #define MB_UNUSED_D7 0xD7
      #define MB_UNUSED_D8 0xD8
      #define MB_UNUSED_D9 0xD9
      #define MB_UNUSED_DA 0xDA
      #define MB_UNUSED_DB 0xDB
      #define MB_UNUSED_DC 0xDC
      #define MB_UNUSED_DD 0xDD
      #define MB_UNUSED_DE 0xDE
      #define MB_UNUSED_DF 0xDF
      #define MB_PICTURE_BOOK_SHELF 0xE0
      #define MB_BOOKSHELF 0xE1
      #define MB_POKEMON_CENTER_BOOKSHELF 0xE2
      #define MB_VASE 0xE3
      #define MB_TRASH_CAN 0xE4
      #define MB_SHOP_SHELF 0xE5
      #define MB_BLUEPRINT 0xE6
      #define MB_CABLE_BOX_RESULTS_2 0xE7
      #define MB_WIRELESS_BOX_RESULTS 0xE8
      #define MB_TRAINER_HILL_TIMER 0xE9
      #define MB_SKY_PILLAR_CLOSED_DOOR 0xEA
      #define MB_UNUSED_EB 0xEB
      #define MB_UNUSED_EC 0xEC
      #define MB_UNUSED_ED 0xED
      #define MB_UNUSED_EE 0xEE
      #define MB_UNUSED_EF 0xEF

      #define NUM_METATILE_BEHAVIORS 0xF0

      #define MB_INVALID   0xFF
 */

namespace HavenSoft.HexManiac.Core.ViewModels.Map {
   public abstract class TileAttribute {
      public int Behavior { get; set; }
      public int Layer { get; set; }
      public string ErrorInfo { get; protected init; }
      public abstract byte[] Serialize();
      public static TileAttribute Create(byte[] data) {
         if (data.Length == 2) return new AttributeRSE(data);
         return new AttributeFRLG(data);
      }
      public static TileAttribute Create(byte[] fullData, int start, int length) {
         var data = new byte[length];
         Array.Copy(fullData, start, data, 0, length);
         if (length == 2) return new AttributeRSE(data);
         return new AttributeFRLG(data);
      }
   }

   /*
    [METATILE_ATTRIBUTE_BEHAVIOR]       = 0x000001ff,
    [METATILE_ATTRIBUTE_TERRAIN]        = 0x00003e00, 00=normal 01=grass 10=water
    [METATILE_ATTRIBUTE_2]              = 0x0003c000,
    [METATILE_ATTRIBUTE_3]              = 0x00fc0000,
    [METATILE_ATTRIBUTE_ENCOUNTER_TYPE] = 0x07000000, 00=none, 01=land, 10=water
    [METATILE_ATTRIBUTE_5]              = 0x18000000,
    [METATILE_ATTRIBUTE_LAYER_TYPE]     = 0x60000000, 00=normal, 01=covered, 10=split, (reserve 11 for triple-layer tile expansion)
    [METATILE_ATTRIBUTE_7]              = 0x80000000
    */
   public class AttributeFRLG : TileAttribute {
      public int Terrain { get; set; }
      public int Encounter { get; set; }
      public AttributeFRLG(byte[] data) {
         Behavior = data[0];
         Terrain = data[1] >> 1;
         Encounter = data[3] & 3;
         Layer = data[3] >> 5;
         if ((data.ReadMultiByteValue(0, 4) & 0x9CFFF900) != 0) {
            ErrorInfo = "Warning: Expected attribute mask 9CFFF900 to be zero.";
         }
      }
      public override byte[] Serialize() {
         var result = new byte[4];
         result[0] = (byte)Behavior;
         result[1] = (byte)(Terrain << 1);
         result[2] = 0;
         result[3] = (byte)((Layer << 5) | Encounter);
         return result;
      }
   }

   /*
    #define METATILE_ATTR_BEHAVIOR_MASK 0x00FF 'behavior' (see below)
    #define METATILE_ATTR_LAYER_MASK    0x3000 00=normal, 01=covered, 10=split
    */
   public class AttributeRSE : TileAttribute {
      public AttributeRSE(byte[] data) {
         Behavior = data[0];
         Layer = data[1] >> 4;
         if ((data[1] & 0xF) != 0) {
            ErrorInfo = "Warning: Expected attribute mask 0F00 to be zero.";
         }
      }
      public override byte[] Serialize() {
         var result = new byte[2];
         result[0] = (byte)Behavior;
         result[1] = (byte)(Layer << 4);
         return result;
      }
   }

   public class BlockEditor : ViewModelCore {
      private readonly ChangeHistory<ModelDelta> history;
      private readonly MapTutorialsViewModel tutorials;
      private readonly IDataModel model;
      private short[][] palettes;
      private int[][,] tiles;
      private byte[][] blocks;
      private byte[][] blockAttributes;
      private readonly IDictionary<IPixelViewModel, int> indexForTileImage;
      private readonly CanvasPixelViewModel[] images;

      private int hoverTile;

      private int layerMode;
      public int LayerMode {
         get => layerMode;
         set => Set(ref layerMode, value, arg => EnterTile(images[hoverTile]));
      }

      private int blockIndex = 0;
      public int BlockIndex {
         get => blockIndex;
         set => Set(ref blockIndex, value.LimitToRange(0, blocks.Length - 1), UpdateBlockUI);
      }

      private static readonly List<((int, int), (int, int))> bottomLayerTogether, topLayerTogether, twoSets;
      private static readonly ObservableCollection<string> movementPermissions = new();
      static BlockEditor() {
         int p0 = 0, p1 = 24, p2 = 48, p3 = 72, p4 = 96, shortD = 8;
         bottomLayerTogether = new() {
            ((p1 - shortD, p1), (p1, p1 - shortD)),
            ((p3         , p1), (p2, p1 - shortD)),
            ((p1 - shortD, p2), (p1, p3         )),
            ((p3         , p2), (p2, p3         )),
            ((p1         , p0), (p0, p1         )),
            ((p3 - shortD, p0), (p3, p1         )),
            ((p1         , p3), (p0, p3 - shortD)),
            ((p3 - shortD, p3), (p3, p3 - shortD)),
         };
         topLayerTogether = new() {
            ((p1         , p0), (p0, p1         )),
            ((p3 - shortD, p0), (p3, p1         )),
            ((p1         , p3), (p0, p3 - shortD)),
            ((p3 - shortD, p3), (p3, p3 - shortD)),
            ((p1 - shortD, p1), (p1, p1 - shortD)),
            ((p3         , p1), (p2, p1 - shortD)),
            ((p1 - shortD, p2), (p1, p3         )),
            ((p3         , p2), (p2, p3         )),
         };
         twoSets = new() {
            ((p2 - shortD, p2), (p2, p2 - shortD)),
            ((p4         , p2), (p3, p2 - shortD)),
            ((p2 - shortD, p3), (p2, p4         )),
            ((p4         , p3), (p3, p4         )),
            ((p0 - shortD, p0), (p0, p0 - shortD)),
            ((p2         , p0), (p1, p0 - shortD)),
            ((p0 - shortD, p1), (p0, p2         )),
            ((p2         , p1), (p1, p2         )),
         };

         // movement permissions
         movementPermissions.Add("00 - Cross Layers");
         movementPermissions.Add("01 - Wall");
         movementPermissions.Add("02");
         movementPermissions.Add("03");
         for (int i = 1; i < 14; i++) {
            var start = i * 4;
            movementPermissions.Add($"{start + 0:X2} - Elevation {i} Pass");
            movementPermissions.Add($"{start + 1:X2} - Elevation {i} Wall");
            movementPermissions.Add($"{start + 2:X2} - Elevation {i} Unused");
            movementPermissions.Add($"{start + 3:X2} - Elevation {i} Unused");
         }
         movementPermissions.Add("38 - No Use");
         movementPermissions.Add("39 - No Use");
         movementPermissions.Add("3A - No Use");
         movementPermissions.Add("3B - No Use");
         movementPermissions.Add("3C - Above/Below");
         movementPermissions.Add("3D - No Use");
         movementPermissions.Add("3E - No Use");
         movementPermissions.Add("3F - No Use");
      }

      public ObservableCollection<string> MovementPermissions => movementPermissions;

      public event EventHandler AutoscrollTiles;
      public event EventHandler<byte[][]> BlocksChanged;
      public event EventHandler<byte[][]> BlockAttributesChanged;
      public event EventHandler<string> SendMessage;

      private IPixelViewModel tileRender;
      public IPixelViewModel TileRender => tileRender;

      private IPixelViewModel drawTileRender;
      public IPixelViewModel DrawTileRender => drawTileRender;

      public BlockEditor(ChangeHistory<ModelDelta> history, IDataModel listSource, MapTutorialsViewModel tutorials, short[][] palettes, int[][,] tiles, byte[][] blocks, byte[][] blockAttributes) {
         this.history = history;
         this.tutorials = tutorials;
         this.model = listSource;
         this.palettes = palettes;
         this.tiles = tiles;
         this.blocks = blocks;
         this.blockAttributes = blockAttributes;
         hasTerrainAndEncounter = blockAttributes[0].Length > 2;
         images = new CanvasPixelViewModel[8];
         indexForTileImage = new Dictionary<IPixelViewModel, int>();
         if (listSource.TryGetList("MapAttributeBehaviors", out var behaviors)) behaviors.ForEach(BehaviorOptions.Add);
         if (listSource.TryGetList("MapLayerOptions", out var layer)) layer.ForEach(LayerOptions.Add);
         if (listSource.TryGetList("MapTerrainOptions", out var terrain)) terrain.ForEach(TerrainOptions.Add);
         if (listSource.TryGetList("MapEncounterOptions", out var encounters)) encounters.ForEach(EncounterOptions.Add);
         for (var i = 0; i < palettes.Length; i++) Palettes.Add(new ReadonlyPaletteCollection(palettes[i], i));
      }

      public IPixelViewModel LeftTopBack => images[0];
      public IPixelViewModel RightTopBack => images[1];
      public IPixelViewModel LeftBottomBack => images[2];
      public IPixelViewModel RightBottomBack => images[3];
      public IPixelViewModel LeftTopFront => images[4];
      public IPixelViewModel RightTopFront => images[5];
      public IPixelViewModel LeftBottomFront => images[6];
      public IPixelViewModel RightBottomFront => images[7];

      private static readonly string[] imageNames = "LeftTopBack,RightTopBack,LeftBottomBack,RightBottomBack,LeftTopFront,RightTopFront,LeftBottomFront,RightBottomFront".Split(",");

      #region FlipV / FlipH

      private int flipVLeft, flipVTop, flipHLeft, flipHTop;
      public int FlipVLeft { get => flipVLeft; private set => Set(ref flipVLeft, value); }
      public int FlipVTop { get => flipVTop; private set => Set(ref flipVTop, value); }
      public int FlipHLeft { get => flipHLeft; private set => Set(ref flipHLeft, value); }
      public int FlipHTop { get => flipHTop; private set => Set(ref flipHTop, value); }

      private bool flipVVisible, flipHVisible;
      public bool FlipVVisible { get => flipVVisible; set => Set(ref flipVVisible, value); }
      public bool FlipHVisible { get => flipHVisible; set => Set(ref flipHVisible, value); }

      public void FlipH() {
         var (pal, hFlip, vFlip, tile) = LzTilemapRun.ReadTileData(blocks[blockIndex], hoverTile, 2);
         hFlip = !hFlip;
         LzTilemapRun.WriteTileData(blocks[blockIndex], hoverTile, pal, hFlip, vFlip, tile);
         var newImage = BlocksetModel.Read(blocks[blockIndex], hoverTile, tiles, palettes);
         images[hoverTile].Fill(newImage.PixelData);
         BlocksChanged?.Invoke(this, blocks);
         tutorials.Complete(Tutorial.FlipButton_FlipBlock);
      }

      public void FlipV() {
         var (pal, hFlip, vFlip, tile) = LzTilemapRun.ReadTileData(blocks[blockIndex], hoverTile, 2);
         vFlip = !vFlip;
         LzTilemapRun.WriteTileData(blocks[blockIndex], hoverTile, pal, hFlip, vFlip, tile);
         var newImage = BlocksetModel.Read(blocks[blockIndex], hoverTile, tiles, palettes);
         images[hoverTile].Fill(newImage.PixelData);
         BlocksChanged?.Invoke(this, blocks);
         tutorials.Complete(Tutorial.FlipButton_FlipBlock);
      }

      #endregion

      #region Copy/Paste Foreground / Background

      private int[,] copyTiles = new int[2, 2];
      private int[,] copyPalettes = new int[2, 2];
      private bool[,] copyFlipVs = new bool[2, 2];
      private bool[,] copyFlipHs = new bool[2, 2];

      public void CopyBackground() {
         (copyPalettes[0, 0], copyFlipHs[0, 0], copyFlipVs[0, 0], copyTiles[0, 0]) = LzTilemapRun.ReadTileData(blocks[blockIndex], 0, 2);
         (copyPalettes[1, 0], copyFlipHs[1, 0], copyFlipVs[1, 0], copyTiles[1, 0]) = LzTilemapRun.ReadTileData(blocks[blockIndex], 1, 2);
         (copyPalettes[0, 1], copyFlipHs[0, 1], copyFlipVs[0, 1], copyTiles[0, 1]) = LzTilemapRun.ReadTileData(blocks[blockIndex], 2, 2);
         (copyPalettes[1, 1], copyFlipHs[1, 1], copyFlipVs[1, 1], copyTiles[1, 1]) = LzTilemapRun.ReadTileData(blocks[blockIndex], 3, 2);
         SendMessage.Raise(this, "Copied Block Background");
      }

      public void CopyForeground() {
         (copyPalettes[0, 0], copyFlipHs[0, 0], copyFlipVs[0, 0], copyTiles[0, 0]) = LzTilemapRun.ReadTileData(blocks[blockIndex], 4, 2);
         (copyPalettes[1, 0], copyFlipHs[1, 0], copyFlipVs[1, 0], copyTiles[1, 0]) = LzTilemapRun.ReadTileData(blocks[blockIndex], 5, 2);
         (copyPalettes[0, 1], copyFlipHs[0, 1], copyFlipVs[0, 1], copyTiles[0, 1]) = LzTilemapRun.ReadTileData(blocks[blockIndex], 6, 2);
         (copyPalettes[1, 1], copyFlipHs[1, 1], copyFlipVs[1, 1], copyTiles[1, 1]) = LzTilemapRun.ReadTileData(blocks[blockIndex], 7, 2);
         SendMessage.Raise(this, "Copied Block Foreground");
      }

      public void PasteBackground() {
         LzTilemapRun.WriteTileData(blocks[blockIndex], 0, copyPalettes[0, 0], copyFlipHs[0, 0], copyFlipVs[0, 0], copyTiles[0, 0]);
         LzTilemapRun.WriteTileData(blocks[blockIndex], 1, copyPalettes[1, 0], copyFlipHs[1, 0], copyFlipVs[1, 0], copyTiles[1, 0]);
         LzTilemapRun.WriteTileData(blocks[blockIndex], 2, copyPalettes[0, 1], copyFlipHs[0, 1], copyFlipVs[0, 1], copyTiles[0, 1]);
         LzTilemapRun.WriteTileData(blocks[blockIndex], 3, copyPalettes[1, 1], copyFlipHs[1, 1], copyFlipVs[1, 1], copyTiles[1, 1]);
         for (int i = 0; i < 4; i++) {
            var newImage = BlocksetModel.Read(blocks[blockIndex], i, tiles, palettes);
            images[i].Fill(newImage.PixelData);
         }
         BlocksChanged?.Invoke(this, blocks);
         tutorials.Complete(Tutorial.ClickBlock_DrawTile);
         SendMessage.Raise(this, "Pasted Block Background");
      }

      public void PasteForeground() {
         LzTilemapRun.WriteTileData(blocks[blockIndex], 4, copyPalettes[0, 0], copyFlipHs[0, 0], copyFlipVs[0, 0], copyTiles[0, 0]);
         LzTilemapRun.WriteTileData(blocks[blockIndex], 5, copyPalettes[1, 0], copyFlipHs[1, 0], copyFlipVs[1, 0], copyTiles[1, 0]);
         LzTilemapRun.WriteTileData(blocks[blockIndex], 6, copyPalettes[0, 1], copyFlipHs[0, 1], copyFlipVs[0, 1], copyTiles[0, 1]);
         LzTilemapRun.WriteTileData(blocks[blockIndex], 7, copyPalettes[1, 1], copyFlipHs[1, 1], copyFlipVs[1, 1], copyTiles[1, 1]);
         for (int i = 4; i < 8; i++) {
            var newImage = BlocksetModel.Read(blocks[blockIndex], i, tiles, palettes);
            images[i].Fill(newImage.PixelData);
         }
         BlocksChanged?.Invoke(this, blocks);
         tutorials.Complete(Tutorial.ClickBlock_DrawTile);
         SendMessage.Raise(this, "Pasted Block Foreground");
      }

      public void LoadClipboard(BlockEditor other) {
         copyPalettes = other.copyPalettes;
         copyFlipHs = other.copyFlipHs;
         copyFlipVs = other.copyFlipVs;
         copyTiles = other.copyTiles;
      }

      #endregion

      public void EnterTile(IPixelViewModel tile) {
         FlipVVisible = true;
         FlipHVisible = true;
         if (tile == null) return;
         if (!indexForTileImage.TryGetValue(tile, out int index)) return;
         ((FlipVLeft, FlipVTop), (FlipHLeft, FlipHTop)) = layerMode == 0 ? twoSets[index] : layerMode == 1 ? bottomLayerTogether[index] : topLayerTogether[index];
         hoverTile = indexForTileImage[tile];
      }

      public void DrawOnTile(IPixelViewModel tile) {
         if (!showTiles || tile == null) return;
         var index = indexForTileImage[tile];
         LzTilemapRun.WriteTileData(blocks[blockIndex], index, drawPalette, drawFlipH, drawFlipV, drawTile);
         var newImage = BlocksetModel.Read(blocks[blockIndex], index, tiles, palettes);
         images[hoverTile].Fill(newImage.PixelData);
         BlocksChanged?.Invoke(this, blocks);
         tutorials.Complete(Tutorial.ClickBlock_DrawTile);
      }

      public void GetSelectionFromTile(IPixelViewModel tileImage) {
         ShowTiles = true;
         var index = indexForTileImage[tileImage];
         var (pal, hFlip, vFlip, tileIndex) = LzTilemapRun.ReadTileData(blocks[blockIndex], index, 2);
         drawTile = tileIndex;
         (drawFlipV, drawFlipH) = (vFlip, hFlip);
         PaletteSelection = pal;
         NotifyPropertiesChanged(nameof(TileSelectionX), nameof(TileSelectionY));
         AutoscrollTiles.Raise(this);
         AnimateTileSelection();
         history.ChangeCompleted();
         UpdateDrawTileRender();
      }

      public void ExitTiles() {
         FlipVVisible = false;
         FlipHVisible = false;
      }

      #region Attribute UI

      private int behavior, layer, terrain, encounter;
      public int Behavior { get => behavior; set => Set(ref behavior, value, SaveAttributes); }
      public int Layer { get => layer; set => Set(ref layer, value, SaveAttributes); }
      public int Terrain { get => terrain; set => Set(ref terrain, value, SaveAttributes); }
      public int Encounter { get => encounter; set => Set(ref encounter, value, SaveAttributes); }

      private string errorText;
      public bool HasError => errorText != null;
      public string ErrorText => errorText;

      public ObservableCollection<string> BehaviorOptions { get; } = new();
      public ObservableCollection<string> LayerOptions { get; } = new();
      public ObservableCollection<string> TerrainOptions { get; } = new();
      public ObservableCollection<string> EncounterOptions { get; } = new();

      private bool hasTerrainAndEncounter;
      public bool HasTerrainAndEncounter { get => hasTerrainAndEncounter; set => Set(ref hasTerrainAndEncounter, value); }

      private void UpdateAttributeUI() {
         var attributes = TileAttribute.Create(blockAttributes[blockIndex]);
         behavior = attributes.Behavior;
         layer = attributes.Layer;
         if (attributes is AttributeFRLG fr) {
            terrain = fr.Terrain;
            encounter = fr.Encounter;
         }
         errorText = attributes.ErrorInfo;
         NotifyPropertiesChanged(nameof(Behavior), nameof(Layer), nameof(Terrain), nameof(Encounter), nameof(HasError), nameof(ErrorText));
      }

      private void SaveAttributes(int arg = default) {
         var attributes = TileAttribute.Create(blockAttributes[blockIndex]);
         attributes.Behavior = behavior;
         attributes.Layer = layer;
         if (attributes is AttributeFRLG fr) {
            fr.Terrain = terrain;
            fr.Encounter = encounter;
         }
         blockAttributes[blockIndex] = attributes.Serialize();
         BlockAttributesChanged?.Invoke(this, blockAttributes);
      }

      #endregion

      private void UpdateBlockUI(int old) {
         for (int i = 0; i < 8; i++) images[i] = null;
         UpdateBlockUI();
      }

      private void UpdateBlockUI() {
         if (blockIndex == -1) return;

         for (int i = 0; i < 8; i++) {
            if (images[i] == null) {
               var image = BlocksetModel.Read(blocks[blockIndex], i, tiles, palettes);
               images[i] = new CanvasPixelViewModel(image.PixelWidth, image.PixelHeight, image.PixelData) { SpriteScale = 3 };
               indexForTileImage[images[i]] = i;
               NotifyPropertyChanged(imageNames[i]);
            }
         }

         indexForTileImage.Clear();
         for (int i = 0; i < 8; i++) indexForTileImage[images[i]] = i;

         UpdateAttributeUI();
      }

      #region Tile UI

      const int TilesPerRow = 16, PixelPerTile = 24;

      private bool showTiles;
      public bool ShowTiles {
         get => showTiles;
         set => Set(ref showTiles, value, arg => {
            if (showTiles && tileRender == null) UpdateTileRender(drawPalette);
         });
      }

      public void ToggleShowTiles() {
         ShowTiles = !ShowTiles;
         tutorials.Complete(Tutorial.BlockButton_EditBlocks);
      }

      public void HideTiles() => ShowTiles = false;

      // hack: a DataTrigger can watch this property and start an animation whenever this property changes.
      //       the value doesn't matter, just the change
      private bool tileSelectionToggle;
      public bool TileSelectionToggle { get => tileSelectionToggle; set => Set(ref tileSelectionToggle, value); }
      private void AnimateTileSelection() => TileSelectionToggle = !TileSelectionToggle;

      private int drawTile, drawPalette;
      private bool drawFlipV, drawFlipH;
      public int TileSelectionX {
         get => (drawTile % TilesPerRow) * PixelPerTile;
         set {
            var (x, y) = (drawTile % TilesPerRow, drawTile / TilesPerRow);
            if (x != value / PixelPerTile) {
               x = value / PixelPerTile;
               drawTile = y * TilesPerRow + x;
               if (drawTile >= tiles.Length) drawTile = tiles.Length - 1;
               NotifyPropertyChanged();
               drawFlipH = drawFlipV = false;
               tutorials.Complete(Tutorial.ClickTile_SelectTile);
               UpdateDrawTileRender();
            }
         }
      }
      public int TileSelectionY {
         get => (drawTile / TilesPerRow) * PixelPerTile;
         set {
            var (x, y) = (drawTile % TilesPerRow, drawTile / TilesPerRow);
            if (y != value / PixelPerTile) {
               y = value / PixelPerTile;
               drawTile = y * TilesPerRow + x;
               if (drawTile >= tiles.Length) {
                  drawTile = tiles.Length - 1;
                  NotifyPropertiesChanged(nameof(TileSelectionX));
               }
               NotifyPropertyChanged();
               drawFlipH = drawFlipV = false;
               history.ChangeCompleted();
               tutorials.Complete(Tutorial.ClickTile_SelectTile);
               UpdateDrawTileRender();
            }
         }
      }
      public int PaletteSelection {
         get => drawPalette;
         set => Set(ref drawPalette, value, arg => UpdateTileRender(drawPalette));
      }

      public ObservableCollection<ReadonlyPaletteCollection> Palettes { get; } = new();

      private void UpdateTileRender(int paletteIndex) {
         var tileLines = (tiles.Length + TilesPerRow - 1) / TilesPerRow;
         var pixelData = new short[8 * 8 * TilesPerRow * tileLines];
         for (int i = 0; i < pixelData.Length; i++) pixelData[i] = short.MinValue;
         var render = new CanvasPixelViewModel(8 * 16, 8 * 16 * 4, pixelData) { SpriteScale = 3, Transparent = short.MinValue };
         var palette = SpriteTool.CreatePaletteWithUniqueTransparentColor(palettes[paletteIndex]);
         for (int y = 0; y < 64; y++) {
            for (int x = 0; x < 16; x++) {
               var tile = new ReadonlyPixelViewModel(8, 8, SpriteTool.Render(tiles[y * 16 + x], palette, 0, 0), palette[0]);
               if (tile == null || tile.PixelData.Length == 0) break;
               render.Draw(tile, x * 8, y * 8);
            }
         }
         tileRender = render;
         NotifyPropertyChanged(nameof(TileRender));
         UpdateDrawTileRender();
      }

      private void UpdateDrawTileRender() {
         var palette = SpriteTool.CreatePaletteWithUniqueTransparentColor(palettes[drawPalette]);
         drawTileRender = new CanvasPixelViewModel(8, 8, SpriteTool.Render(tiles[drawTile], palette, 0, 0)) { Transparent = palette[0], SpriteScale = 4 };
         NotifyPropertiesChanged(nameof(DrawTileRender));
      }

      #endregion

      #region Cache

      public void RefreshPaletteCache(short[][] palette) => this.palettes = palette;
      public void RefreshTileCache(int[][,] tiles) => this.tiles = tiles;
      public void RefreshBlockCache(byte[][] blocks) {
         this.blocks = blocks;
         BlockIndex = blockIndex;
      }
      public void RefreshBlockAttributeCache(byte[][] blockAttributes) => this.blockAttributes = blockAttributes;

      #endregion
   }
}
