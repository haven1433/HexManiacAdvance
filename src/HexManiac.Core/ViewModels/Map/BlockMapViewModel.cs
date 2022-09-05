using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.HexManiac.Core.ViewModels.Map {
   public class BlockMapViewModel : ViewModelCore, IPixelViewModel {

      /*From FireRed fieldmap.h
         #define NUM_TILES_IN_PRIMARY 640
         #define NUM_TILES_TOTAL 1024
         #define NUM_METATILES_IN_PRIMARY 640
         #define NUM_METATILES_TOTAL 1024
         #define NUM_PALS_IN_PRIMARY 7
         #define NUM_PALS_TOTAL 13
         #define MAX_MAP_DATA_SIZE 0x2800
       */

      private readonly IDataModel model;
      private readonly int group, map;

      private int PrimaryTiles => 640;
      private int TotalTiles => 1024;
      private int PrimaryBlocks => 640;
      private int TotalBlocks => 1024;
      private int PrimaryPalettes => 7;
      private int TotalPalettes => 13;

      private int MapSizeLimit => 0x2800; // (x+15)*(y+14) must be less that 0x2800 (5*2048). This can lead to limits like 113x66 or 497x6
      public bool IsMapWithinSizeLimit => (PixelWidth / 16 + 15) * (PixelHeight / 16 + 14) <= MapSizeLimit;

      public int MapID => group * 1000 + map;

      #region IPixelViewModel

      private short transparent;
      public short Transparent { get => transparent; private set => Set(ref transparent, value); }

      private int pixelWidth, pixelHeight;
      public int PixelWidth { get => pixelWidth; private set => Set(ref pixelWidth, value); }
      public int PixelHeight { get => pixelHeight; private set => Set(ref pixelHeight, value); }

      private short[] pixelData;
      public short[] PixelData {
         get {
            if (pixelData == null) FillMapPixelData();
            return pixelData;
         }
      }

      private double spriteScale = 1;
      public double SpriteScale {
         get => spriteScale;
         set => Set(ref spriteScale, value, old => UpdateEdgesFromScale(old, old * pixelWidth / 2, old * pixelHeight / 2));
      }

      private void UpdateEdgesFromScale(double old, double centerX, double centerY) {
         LeftEdge += (int)(centerX * (1 - SpriteScale / old));
         TopEdge += (int)(centerY * (1 - SpriteScale / old));
      }

      #endregion

      #region Position

      private int topEdge, leftEdge;
      public int TopEdge { get => topEdge; set => Set(ref topEdge, value); }
      public int LeftEdge { get => leftEdge; set => Set(ref leftEdge, value); }

      #endregion

      #region Visual Blocks

      private IPixelViewModel blockPixels;
      public IPixelViewModel BlockPixels {
         get {
            if (blockPixels == null) FillBlockPixelData();
            return blockPixels;
         }
      }

      #endregion

      #region Cache

      private short[][] palettes;
      private int[][,] tiles;
      private byte[][] blocks;
      private IReadOnlyList<IPixelViewModel> blockRenders;

      #endregion

      #region Borders

      private bool includeBorders = false;
      public bool IncludeBorders {
         get => includeBorders;
         set => Set(ref includeBorders, value, IncludeBordersChanged);
      }
      private void IncludeBordersChanged(bool oldValue) {
         var width = PixelWidth;
         var height = PixelHeight;
         RefreshMapSize();
         LeftEdge -= (PixelWidth - width) / 2;
         TopEdge -= (PixelHeight - height) / 2;
      }

      private IPixelViewModel borderBlock;
      public IPixelViewModel BorderBlock {
         get {
            if (borderBlock == null) RefreshBorderRender();
            return borderBlock;
         }
         set {
            borderBlock = value;
            NotifyPropertyChanged();
         }
      }

      #endregion

      public BlockMapViewModel(IDataModel model, int group, int map) {
         this.model = model;
         (this.group, this.map) = (group, map);
         Transparent = -1;

         RefreshMapSize();

         (LeftEdge, TopEdge) = (-PixelWidth / 2, -PixelHeight / 2);
      }

      public IReadOnlyList<BlockMapViewModel> GetNeighbors(MapDirection direction) {
         var list = new List<BlockMapViewModel>();
         var (myN, _, _, myW) = GetBorderThickness();
         foreach (var connection in GetConnections()) {
            if (connection.Direction != direction) continue;
            var vm = new BlockMapViewModel(model, connection.MapGroup, connection.MapNum) { IncludeBorders = IncludeBorders, SpriteScale = SpriteScale };
            var (n, _, _, w) = vm.GetBorderThickness();
            vm.TopEdge = TopEdge + (connection.Offset + myN - n) * (int)(16 * SpriteScale);
            vm.LeftEdge = LeftEdge + (connection.Offset + myW - w) * (int)(16 * SpriteScale);
            if (direction == MapDirection.Left) vm.LeftEdge = LeftEdge - (int)(vm.PixelWidth * SpriteScale);
            if (direction == MapDirection.Right) vm.LeftEdge = LeftEdge + (int)(PixelWidth * SpriteScale);
            if (direction == MapDirection.Up) vm.TopEdge = TopEdge - (int)(vm.PixelHeight * SpriteScale);
            if (direction == MapDirection.Down) vm.TopEdge = TopEdge + (int)(PixelHeight * SpriteScale);
            list.Add(vm);
         }
         return list;
      }

      public void Scale(double x, double y, bool enlarge) {
         var old = spriteScale;

         if (enlarge && spriteScale < 10) spriteScale *= 2;
         else if (!enlarge && spriteScale > .1) spriteScale /= 2;

         if (old != spriteScale) UpdateEdgesFromScale(old, x - leftEdge, y - topEdge);
         NotifyPropertyChanged(nameof(SpriteScale));
      }

      private void RefreshPaletteCache(BlocksetModel blockModel1 = null, BlocksetModel blockModel2 = null) {
         if (blockModel1 == null || blockModel2 == null) {
            var layout = GetLayout();
            if (blockModel1 == null) blockModel1 = new BlocksetModel(model, layout.GetAddress("tiles1"));
            if (blockModel2 == null) blockModel2 = new BlocksetModel(model, layout.GetAddress("tiles2"));
         }

         var primary = blockModel1.ReadPalettes();
         var secondary = blockModel2.ReadPalettes();
         var result = new short[TotalPalettes][];
         for (int i = 0; i < PrimaryPalettes; i++) result[i] = primary[i];
         for (int i = PrimaryPalettes; i < TotalPalettes; i++) result[i] = secondary[i];
         palettes = result;
      }

      private void RefreshTileCache(BlocksetModel blockModel1 = null, BlocksetModel blockModel2 = null) {
         if (blockModel1 == null || blockModel2 == null) {
            var layout = GetLayout();
            if (blockModel1 == null) blockModel1 = new BlocksetModel(model, layout.GetAddress("tiles1"));
            if (blockModel2 == null) blockModel2 = new BlocksetModel(model, layout.GetAddress("tiles2"));
         }

         var primary = blockModel1.ReadTiles();
         var secondary = blockModel2.ReadTiles();
         var result = new int[TotalTiles][,];
         for (int i = 0; i < PrimaryTiles; i++) result[i] = primary[i];
         for (int i = PrimaryTiles; i < TotalTiles && i < secondary.Length + PrimaryTiles; i++) result[i] = secondary[i - PrimaryTiles];
         tiles = result;
      }

      private void RefreshBlockCache(BlocksetModel blockModel1 = null, BlocksetModel blockModel2 = null) {
         if (blockModel1 == null || blockModel2 == null) {
            var layout = GetLayout();
            if (blockModel1 == null) blockModel1 = new BlocksetModel(model, layout.GetAddress("tiles1"));
            if (blockModel2 == null) blockModel2 = new BlocksetModel(model, layout.GetAddress("tiles2"));
         }

         var primary = blockModel1.ReadBlocks();
         var secondary = blockModel2.ReadBlocks();
         blocks = primary.Concat(secondary).ToArray();
      }

      private void RefreshBlockRenderCache(ModelArrayElement layout = null, BlocksetModel blockModel1 = null, BlocksetModel blockModel2 = null) {
         if (blocks == null || tiles == null || palettes == null) {
            if (layout == null) layout = GetLayout();
            if (blockModel1 == null) blockModel1 = new BlocksetModel(model, layout.GetAddress("tiles1"));
            if (blockModel2 == null) blockModel2 = new BlocksetModel(model, layout.GetAddress("tiles2"));
         }
         if (blocks == null) RefreshBlockCache(blockModel1, blockModel2);
         if (tiles == null) RefreshTileCache(blockModel1, blockModel2);
         if (palettes == null) RefreshPaletteCache(blockModel1, blockModel2);

         var renders = new List<IPixelViewModel>();
         for (int i = 0; i < blocks.Length; i++) {
            renders.Add(BlocksetModel.RenderBlock(blocks[i], tiles, palettes));
         }
         this.blockRenders = renders;
      }

      private void RefreshMapSize() {
         var layout = GetLayout();
         var (width, height) = (layout.GetValue("width"), layout.GetValue("height"));
         var border = GetBorderThickness(layout);
         (pixelWidth, pixelHeight) = ((width + border.West + border.East) * 16, (height + border.North + border.South) * 16);
         pixelData = null;
         NotifyPropertyChanged(nameof(PixelData));
      }

      private void FillMapPixelData() {
         var layout = GetLayout();
         if (blockRenders == null) RefreshBlockRenderCache(layout);
         if (borderBlock == null) RefreshBorderRender();
         var (width, height) = (layout.GetValue("width"), layout.GetValue("height"));
         var border = GetBorderThickness(layout);
         var start = layout.GetAddress("map");

         var canvas = new CanvasPixelViewModel(pixelWidth, pixelHeight);
         var (borderWidth, borderHeight) = (borderBlock.PixelWidth / 16, borderBlock.PixelHeight / 16);
         for (int y = 0; y < height + border.North + border.South; y++) {
            for (int x = 0; x < width + border.West + border.East; x++) {
               if (y < border.North || x < border.West || y >= border.North + height || x >= border.West + width) {
                  if (x % borderWidth == 0 && y % borderHeight == 0) canvas.Draw(borderBlock, x * 16, y * 16);
                  continue;
               }
               var data = model.ReadMultiByteValue(start + ((y - border.North) * width + x - border.West) * 2, 2);
               data &= 0x3FF;
               canvas.Draw(blockRenders[data], x * 16, y * 16);
            }
         }

         pixelData = canvas.PixelData;
      }

      public const int BlocksPerRow = 8;
      private void FillBlockPixelData() {
         var layout = GetLayout();
         if (blockRenders == null) RefreshBlockRenderCache(layout);

         var blockHeight = TotalBlocks / BlocksPerRow;
         var canvas = new CanvasPixelViewModel(BlocksPerRow * 16, blockHeight * 16) { SpriteScale = 2 };

         for (int y = 0; y < blockHeight; y++) {
            for (int x = 0; x < BlocksPerRow; x++) {
               canvas.Draw(blockRenders[y * BlocksPerRow + x], x * 16, y * 16);
            }
         }

         blockPixels = canvas;
      }

      private void RefreshBorderRender(ModelArrayElement layout = null) {
         if (layout == null) layout = GetLayout();
         if (blockRenders == null) RefreshBlockRenderCache(layout);
         var width = layout.HasField("borderwidth") ? layout.GetValue("borderwidth") : 2;
         var height = layout.HasField("borderheight") ? layout.GetValue("borderheight") : 2;

         var start = layout.GetAddress("borderblock");
         var canvas = new CanvasPixelViewModel(width * 16, height * 16);
         for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
               var data = model.ReadMultiByteValue(start + (y * width + x) * 2, 2);
               data &= 0x3FF;
               canvas.Draw(blockRenders[data], x * 16, y * 16);
            }
         }

         BorderBlock = canvas;
      }

      private ModelArrayElement GetLayout() {
         var table = model.GetTable(HardcodeTablesModel.MapBankTable);
         var mapBanks = new ModelTable(model, table.Start);
         var bank = mapBanks[group].GetSubTable("maps");
         var mapTable = bank[map].GetSubTable("map");
         return mapTable[0].GetSubTable("layout")[0];
      }

      private IReadOnlyList<ConnectionModel> GetConnections() {
         var table = model.GetTable(HardcodeTablesModel.MapBankTable);
         var mapBanks = new ModelTable(model, table.Start);
         var bank = mapBanks[group].GetSubTable("maps");
         var mapTable = bank[map].GetSubTable("map");
         var connectionsAndCount = mapTable[0].GetSubTable("connections")[0];
         var connections = connectionsAndCount.GetSubTable("connections");
         var list = new List<ConnectionModel>();
         foreach (var c in connections) list.Add(new(c));
         return list;
      }

      private Border GetBorderThickness(ModelArrayElement layout = null) {
         if (!includeBorders) return new(0, 0, 0, 0);
         var connections = GetConnections();
         if (layout == null) layout = GetLayout();
         var width = layout.HasField("borderwidth") ? layout.GetValue("borderwidth") : 2;
         var height = layout.HasField("borderheight") ? layout.GetValue("borderheight") : 2;
         var (east, west) = (width, width);
         var (north, south) = (height, height);
         var directions = connections.Select(c => c.Direction).ToList();
         if (directions.Contains(MapDirection.Down)) south = 0;
         if (directions.Contains(MapDirection.Up)) north = 0;
         if (directions.Contains(MapDirection.Left)) west = 0;
         if (directions.Contains(MapDirection.Right)) east = 0;
         return new(north, east, south, west);
      }

      /*
         ruby:    data.maps.banks,                       layout<[
                                                            width:: height:: borderblock<[border:|h]4> map<>
                                                            tiles1<[isCompressed. isSecondary. padding: tileset<`lzt4`> pal<`ucp4:0123456789ABCDEF`> block<> attributes<> animation<>]1>
                                                            tiles2<>]1>
                                                         events<[e1 e2 e3 e4 ee1<> ee2<> ee3<> ee4<>]1>
                                                         mapscripts<[type. pointer<>]!00>
                                                         connections<>
                                                         music: layoutID: regionSectionID. cave. weather. mapType. padding. escapeRope. flags. battleType.

         firered: data.maps.banks,                       layout<[
                                                            width:: height:: borderblock<>
                                                            map<>
                                                            tiles1<[isCompressed. isSecondary. padding: tileset<`lzt4`> pal<`ucp4:0123456789ABCDEF`> block<> animation<> attributes<>]1>
                                                            tiles2<>
                                                            borderwidth. borderheight. unused:]1>
                                                         events<[e1 e2 e3 e4 ee1<> ee2<> ee3<> ee4<>]1>
                                                         mapscripts<[type. pointer<>]!00>
                                                         connections<[count:: connections<[direction:: offset:: mapGroup. mapNum. unused:]/count>]>
                                                         music: layoutID: regionSectionID. cave. weather. mapType. allowBiking. flags.|t|allowEscaping.|allowRunning.|showMapName::: floorNum. battleType.

         emerald: data.maps.banks,                       layout<[width:: height:: borderblock<[border:|h]4> map<> tiles1<[isCompressed. isSecondary. padding: tileset<`lzt4`> pal<`ucp4:0123456789ABCDEF`> block<> attributes<> animation<>]1> tiles2<>]1>
                                                         events<[objects. warps. scripts. signposts.
                                                            objectP<[id. graphics. unused: x:1000 y:1000 elevation. moveType. range:|t|x::|y:: trainerType: trainerRangeOrBerryID: script<`xse`> flag: unused:]/objects>
                                                            warpP<[x:1000 y:1000 elevation. warpID. map. bank.]/warps>
                                                            scriptP<[x:1000 y:1000 elevation: trigger: index: unused: script<`xse`>]/scripts>
                                                            signpostP<[x:1000 y:1000 elevation. kind. unused: arg::|h]/signposts>]1>
                                                         mapscripts<[type. pointer<>]!00>
                                                         connections<>
                                                         music: layoutID: regionSectionID. cave. weather. mapType. padding: flags.|t|allowCycling.|allowEscaping.|allowRunning.|showMapName::. battleType.
       */
   }

   public record Border(int North, int East, int South, int West);

   public class ConnectionModel {
      private readonly ModelArrayElement connection;
      public ConnectionModel(ModelArrayElement connection) => this.connection = connection;

      public MapDirection Direction => (MapDirection)connection.GetValue("direction");
      public int Offset => connection.GetValue("offset");
      public int MapGroup => connection.GetValue("mapGroup");
      public int MapNum => connection.GetValue("mapNum");
   }

   public enum MapDirection {
      None = 0,
      Down = 1,
      Up = 2,
      Left = 3,
      Right = 4,
      Dive = 5,
      Emerge = 6,
   }

   public class BlocksetModel {
      private readonly IDataModel model;
      private readonly int address;

      public BlocksetModel(IDataModel model, int address) {
         this.model = model;
         this.address = address;
      }

      int Read(int offset) => model[address + offset];
      int ReadPointer(int offset) => model.ReadPointer(address + offset);

      public bool IsCompressed => Read(0) == 1;
      public bool IsSecondary => Read(1) == 1;

      public int[][,] ReadTiles() {
         if (!IsCompressed) throw new NotImplementedException();
         int start = ReadPointer(4);
         var run = new LzTilesetRun(new TilesetFormat(4, null), model, start);
         var fullData = run.GetPixels(model, 0, 1);
         var list = new List<int[,]>();
         for (int i = 0; i < fullData.GetLength(1) / 8; i++) {
            var tile = new int[8, 8];
            for (int x = 0; x < 8; x++) {
               for (int y = 0; y < 8; y++) {
                  tile[x, y] = fullData[x, y + i * 8];
               }
            }
            list.Add(tile);
         }
         return list.ToArray();
      }

      public short[][] ReadPalettes() {
         int start = ReadPointer(8);
         var data = new short[16][];
         for (int i = 0; i < 16; i++) {
            data[i] = new short[16];
            for (int j = 0; j < 16; j++) {
               var bgr = (short)model.ReadMultiByteValue(start + i * 32 + j * 2, 2);
               data[i][j] = PaletteRun.FlipColorChannels(bgr);
            }
         }
         return data;
      }

      public byte[][] ReadBlocks() {
         // each block is 16 bytes
         int start = ReadPointer(12);
         int blockCount = 640;
         if (IsSecondary) blockCount = 1024 - blockCount;
         var data = new byte[blockCount][];
         for (int i = 0; i < blockCount; i++) {
            data[i] = new byte[16];
            for (int j = 0; j < 16; j++) {
               data[i][j] = model[start + i * 16 + j];
            }
         }
         return data;
      }

      public static IPixelViewModel RenderBlock(byte[] block, int[][,] tiles, short[][] palettes) {
         var canvas = new CanvasPixelViewModel(16, 16);

         // bottom layer
         var tile = Read(block, 0, tiles, palettes);
         canvas.Draw(tile, 0, 0);

         tile = Read(block, 1, tiles, palettes);
         canvas.Draw(tile, 8, 0);

         tile = Read(block, 2, tiles, palettes);
         canvas.Draw(tile, 0, 8);

         tile = Read(block, 3, tiles, palettes);
         canvas.Draw(tile, 8, 8);

         // top layer
         tile = Read(block, 4, tiles, palettes);
         canvas.Draw(tile, 0, 0);

         tile = Read(block, 5, tiles, palettes);
         canvas.Draw(tile, 8, 0);

         tile = Read(block, 6, tiles, palettes);
         canvas.Draw(tile, 0, 8);

         tile = Read(block, 7, tiles, palettes);
         canvas.Draw(tile, 8, 8);

         return canvas;
      }

      private static IPixelViewModel Read(byte[] block, int index, int[][,] tiles, short[][] palettes) {
         var (pal, hFlip, vFlip, tile) = LzTilemapRun.ReadTileData(block, index, 2);

         if (tiles.Length < tile) {
            return new ReadonlyPixelViewModel(new SpriteFormat(4, 1, 1, default), new short[64]);
         }

         var tileData = new short[64];
         for (int yy = 0; yy < 8; yy++) {
            for (int xx = 0; xx < 8; xx++) {
               var inX = hFlip ? 7 - xx : xx;
               var inY = vFlip ? 7 - yy : yy;
               if (tiles[tile] == null || palettes[pal] == null) tileData[yy * 8 + xx] = 0; 
               else tileData[yy * 8 + xx] = palettes[pal][tiles[tile][inX, inY]];
            }
         }
         return new ReadonlyPixelViewModel(new SpriteFormat(4, 1, 1, default), tileData, palettes[pal][0]);
      }
   }
}
