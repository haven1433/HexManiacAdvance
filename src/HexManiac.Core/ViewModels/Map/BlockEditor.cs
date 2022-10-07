using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HexManiac.Core.Models.Runs.Sprites;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Diagnostics;
using System.Linq;

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
      public abstract byte[] Serialize();
      public static TileAttribute Create(byte[] data) {
         if (data.Length == 2) return new AttributeRSE(data);
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
         Debug.Assert((data.ReadMultiByteValue(0, 4) & 0x9CFFF900) == 0, "Expected attribute mask 9CFFF900 to be zero!");
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
         Debug.Assert((data[1] & 0xF) == 0, "Expected attribute mask 0F00 to be zero!");
      }
      public override byte[] Serialize() {
         var result = new byte[2];
         result[0] = (byte)Behavior;
         result[1] = (byte)(Layer << 4);
         return result;
      }
   }

   public class BlockEditor : ViewModelCore {
      // TODO ability to swap background/foreground layer positions
      // TODO changing attributes/flip should write to the model and update the loaded maps
      private readonly short[][] palettes;
      private readonly int[][,] tiles;
      private readonly byte[][] blocks;
      private readonly byte[][] blockAttributes;
      private readonly IDictionary<IPixelViewModel, int> indexForTileImage;
      private readonly IList<IPixelViewModel> images;

      private int hoverTile, selectedTile;

      private bool topLayerInside;
      public bool TopLayerInside {
         get => topLayerInside;
         set => Set(ref topLayerInside, value, arg => EnterTile(images[hoverTile]));
      }

      private int blockIndex = 0;
      public int BlockIndex {
         get => blockIndex;
         set => Set(ref blockIndex, value, UpdateBlockUI);
      }

      public BlockEditor(short[][] palettes, int[][,] tiles, byte[][] blocks, byte[][] blockAttributes) {
         this.palettes = palettes;
         this.tiles = tiles;
         this.blocks = blocks;
         this.blockAttributes = blockAttributes;
         images = new IPixelViewModel[8];
         indexForTileImage = new Dictionary<IPixelViewModel, int>();
      }

      public IPixelViewModel LeftTopBack => images[0];
      public IPixelViewModel RightTopBack => images[1];
      public IPixelViewModel LeftBottomBack => images[2];
      public IPixelViewModel RightBottomBack => images[3];
      public IPixelViewModel LeftTopFront => images[4];
      public IPixelViewModel RightTopFront => images[5];
      public IPixelViewModel LeftBottomFront => images[6];
      public IPixelViewModel RightBottomFront => images[7];

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
         blocks[blockIndex] = null;
         UpdateBlockUI();
      }

      public void FlipV() {
         var (pal, hFlip, vFlip, tile) = LzTilemapRun.ReadTileData(blocks[blockIndex], hoverTile, 2);
         vFlip = !vFlip;
         LzTilemapRun.WriteTileData(blocks[blockIndex], hoverTile, pal, hFlip, vFlip, tile);
         blocks[blockIndex] = null;
         UpdateBlockUI();
      }

      #endregion

      public void EnterTile(IPixelViewModel tile) {
         FlipVVisible = true;
         FlipHVisible = true;
         int p0 = 0, p1 = 24, p2 = 48, p3 = 72, shortD = 8;
         var bottomLayerTogether = new Dictionary<IPixelViewModel, ((int, int),(int,int))> {
            { LeftTopBack,      ((p1 - shortD, p1), (p1, p1 - shortD)) },
            { LeftTopFront,     ((p1         , p0), (p0, p1         )) },
            { RightTopBack,     ((p3         , p1), (p2, p1 - shortD)) },
            { RightTopFront,    ((p3 - shortD, p0), (p3, p1         )) },
            { LeftBottomBack,   ((p1 - shortD, p2), (p1, p3         )) },
            { LeftBottomFront,  ((p1         , p3), (p0, p3 - shortD)) },
            { RightBottomBack,  ((p3         , p2), (p2, p3         )) },
            { RightBottomFront, ((p3 - shortD, p3), (p3, p3 - shortD)) },
         };
         var topLayerTogether = new Dictionary<IPixelViewModel, ((int, int), (int, int))> {
            { LeftTopFront,     ((p1 - shortD, p1), (p1, p1 - shortD)) },
            { LeftTopBack,      ((p1         , p0), (p0, p1         )) },
            { RightTopFront,    ((p3         , p1), (p2, p1 - shortD)) },
            { RightTopBack,     ((p3 - shortD, p0), (p3, p1         )) },
            { LeftBottomFront,  ((p1 - shortD, p2), (p1, p3         )) },
            { LeftBottomBack,   ((p1         , p3), (p0, p3 - shortD)) },
            { RightBottomFront, ((p3         , p2), (p2, p3         )) },
            { RightBottomBack,  ((p3 - shortD, p3), (p3, p3 - shortD)) },
         };
         ((FlipVLeft, FlipVTop), (FlipHLeft, FlipHTop)) = topLayerInside ? topLayerTogether[tile] : bottomLayerTogether[tile];
         hoverTile = indexForTileImage[tile];
      }

      public void SelectTile(IPixelViewModel tile) {
         selectedTile = indexForTileImage[tile];
      }

      public void ExitTiles() {
         FlipVVisible = false;
         FlipHVisible = false;
      }

      #region Attribute UI

      private int behavior, layer, terrain, encounter;
      public int Behavior { get => behavior; set => Set(ref behavior, value, arg => SaveAttributes()); }
      public int Layer { get => layer; set => Set(ref layer, value, arg => SaveAttributes()); }
      public int Terrain { get => terrain; set => Set(ref terrain, value, arg => SaveAttributes()); }
      public int Encounter { get => encounter; set => Set(ref encounter, value, arg => SaveAttributes()); }

      public ObservableCollection<string> BehaviorOptions { get; } = new(); // TODO
      public ObservableCollection<string> LayerOptions { get; } = new() { "Normal", "Covered", "Split", };
      public ObservableCollection<string> TerrainOptions { get; } = new() { "Normal", "Grass", "Water" };
      public ObservableCollection<string> EncounterOptions { get; } = new() { "Normal", "Grass", "Water" };

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
         new List<string> { nameof(Behavior), nameof(Layer), nameof(Terrain), nameof(Encounter) }.ForEach(NotifyPropertyChanged);
      }

      private void SaveAttributes() {
         var attributes = TileAttribute.Create(blockAttributes[blockIndex]);
         attributes.Behavior = behavior;
         attributes.Layer = layer;
         if (attributes is AttributeFRLG fr) {
            fr.Terrain = terrain;
            fr.Encounter = encounter;
         }
         blockAttributes[blockIndex] = attributes.Serialize();
      }

      #endregion

      private void UpdateBlockUI(int old) {
         for (int i = 0; i < 8; i++) images[i] = null;
         UpdateBlockUI();
      }

      private void UpdateBlockUI() {
         if (blockIndex == -1) return;
         for (int i = 0; i < 8; i++) {
            if (images[i] == null) images[i] = BlocksetModel.Read(blocks[blockIndex], i, tiles, palettes, 3);
         }

         indexForTileImage.Clear();
         for (int i = 0; i < 8; i++) indexForTileImage[images[i]] = i;

         foreach (var property in new[] {
            nameof(LeftTopBack),
            nameof(LeftTopFront),
            nameof(RightTopBack),
            nameof(RightTopFront),
            nameof(LeftBottomBack),
            nameof(LeftBottomFront),
            nameof(RightBottomBack),
            nameof(RightBottomFront),
         }) {
            NotifyPropertyChanged(property);
         }

         UpdateAttributeUI();
      }
   }
}
