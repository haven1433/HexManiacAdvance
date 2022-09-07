using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;

using static HavenSoft.HexManiac.Core.ViewModels.Map.MapButtonIcons;

namespace HavenSoft.HexManiac.Core.ViewModels.Map {
   public class BlockMapViewModel : ViewModelCore, IPixelViewModel {

      private readonly IDataModel model;
      private readonly int group, map;

      // TODO make these dynamic, right now this is only right for FireRed
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

      private int BottomEdge => topEdge + (int)(PixelHeight * SpriteScale);
      private int RightEdge => leftEdge + (int)(PixelWidth * SpriteScale);

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
      private IReadOnlyList<ObjectEventModel> eventRenders;

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

      public event EventHandler NeighborsChanged;
      public IReadOnlyList<BlockMapViewModel> GetNeighbors(MapDirection direction) {
         var list = new List<BlockMapViewModel>();
         var border = GetBorderThickness();
         foreach (var connection in GetConnections()) {
            if (connection.Direction != direction) continue;
            var vm = GetNeighbor(connection, border);
            list.Add(vm);
         }
         return list;
      }

      public void ClearCaches() {
         palettes = null;
         tiles = null;
         blocks = null;
         blockRenders = null;
         blockPixels = null;
         pixelData = null;
         eventRenders = null;
         NotifyPropertyChanged(nameof(PixelData));
      }

      public void Scale(double x, double y, bool enlarge) {
         var old = spriteScale;

         if (enlarge && spriteScale < 10) spriteScale *= 2;
         else if (!enlarge && spriteScale > .1) spriteScale /= 2;

         if (old != spriteScale) UpdateEdgesFromScale(old, x - leftEdge, y - topEdge);
         NotifyPropertyChanged(nameof(SpriteScale));
      }

      public void DrawBlock(ModelDelta token, int index, double x, double y) {
         if (index < 0 || index > blockRenders.Count) return;
         (x, y) = ((x - leftEdge) / spriteScale, (y - topEdge) / spriteScale);
         (x, y) = (x / 16, y / 16);

         var layout = GetLayout();
         var (width, height) = (layout.GetValue("width"), layout.GetValue("height"));
         if (x < 0 || y < 0 || x > width || x > height) return;
         var start = layout.GetAddress("map");

         var border = GetBorderThickness(layout);

         var modelAddress = start + (((int)y - border.North) * width + (int)x - border.West) * 2;
         var data = model.ReadMultiByteValue(modelAddress, 2);
         var high = data & 0xFC00;
         var low = index;
         model.WriteMultiByteValue(modelAddress, 2, token, high | low);

         var canvas = new CanvasPixelViewModel(pixelWidth, pixelHeight, pixelData);
         canvas.Draw(blockRenders[index], (int)x * 16, (int)y * 16);

         NotifyPropertyChanged(nameof(PixelData));
      }

      public int GetBlock(double x, double y) {
         (x, y) = ((x - leftEdge) / spriteScale, (y - topEdge) / spriteScale);
         (x, y) = (x / 16, y / 16);

         var layout = GetLayout();
         var (width, height) = (layout.GetValue("width"), layout.GetValue("height"));
         if (x < 0 || y < 0 || x > width || x > height) return -1;
         var start = layout.GetAddress("map");

         var border = GetBorderThickness(layout);

         var modelAddress = start + (((int)y - border.North) * width + (int)x - border.West) * 2;
         var data = model.ReadMultiByteValue(modelAddress, 2);
         var low = data & 0x3FF;
         return low;
      }

      public IEnumerable<MapButton> GetConnectionButtons() {
         var results = new List<MapButton>();
         var connections = GetConnections();
         var border = GetBorderThickness();
         var tileSize = (int)(16 * spriteScale);
         int id = 0;
         foreach (var connection in connections) {
            void Notify() => NeighborsChanged.Raise(this);
            var map = GetNeighbor(connection, border);

            if (connection.Direction == MapDirection.Up) {
               yield return new MapButton(id, connection, Notify, LeftRight, right: map.LeftEdge, bottom: map.BottomEdge - tileSize);
               yield return new MapButton(id + 1, connection, Notify, LeftRight, left: map.RightEdge, bottom: map.BottomEdge - tileSize);
            }

            if (connection.Direction == MapDirection.Down) {
               yield return new MapButton(id, connection, Notify, LeftRight, right: map.LeftEdge, top: map.TopEdge + tileSize);
               yield return new MapButton(id + 1, connection, Notify, LeftRight, left: map.RightEdge, top: map.TopEdge + tileSize);
            }

            if (connection.Direction == MapDirection.Left) {
               yield return new MapButton(id, connection, Notify, UpDown, right: map.RightEdge - tileSize, bottom: map.TopEdge);
               yield return new MapButton(id + 1, connection, Notify, UpDown, right: map.RightEdge - tileSize, top: map.BottomEdge);
            }

            if (connection.Direction == MapDirection.Right) {
               yield return new MapButton(id, connection, Notify, UpDown, left: map.LeftEdge + tileSize, bottom: map.TopEdge);
               yield return new MapButton(id + 1, connection, Notify, UpDown, left: map.LeftEdge + tileSize, top: map.BottomEdge);
            }

            id += 2;
         }
      }

      private BlockMapViewModel GetNeighbor(ConnectionModel connection, Border border) {
         var vm = new BlockMapViewModel(model, connection.MapGroup, connection.MapNum) { IncludeBorders = IncludeBorders, SpriteScale = SpriteScale };
         var (n, _, _, w) = vm.GetBorderThickness();
         vm.TopEdge = TopEdge + (connection.Offset + border.North - n) * (int)(16 * SpriteScale);
         vm.LeftEdge = LeftEdge + (connection.Offset + border.West - w) * (int)(16 * SpriteScale);
         if (connection.Direction == MapDirection.Left) vm.LeftEdge = LeftEdge - (int)(vm.PixelWidth * SpriteScale);
         if (connection.Direction == MapDirection.Right) vm.LeftEdge = LeftEdge + (int)(PixelWidth * SpriteScale);
         if (connection.Direction == MapDirection.Up) vm.TopEdge = TopEdge - (int)(vm.PixelHeight * SpriteScale);
         if (connection.Direction == MapDirection.Down) vm.TopEdge = TopEdge + (int)(PixelHeight * SpriteScale);
         return vm;
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

      private void RefreshMapEvents() {
         if (eventRenders != null) return;
         var list = new List<ObjectEventModel>();
         var table = model.GetTable(HardcodeTablesModel.MapBankTable);
         var mapBanks = new ModelTable(model, table.Start);
         var bank = mapBanks[group].GetSubTable("maps");
         var mapTable = bank[map].GetSubTable("map");
         var events = new EventModel(mapTable[0].GetSubTable("events")[0]);
         foreach (var obj in events.Objects) {
            obj.Render(model);
            list.Add(obj);
         }
         eventRenders = list;
      }

      private void FillMapPixelData() {
         var layout = GetLayout();
         if (blockRenders == null) RefreshBlockRenderCache(layout);
         if (borderBlock == null) RefreshBorderRender();
         if (eventRenders == null) RefreshMapEvents();
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

         // now draw the events on top
         foreach (var obj in eventRenders) {
            var (x, y) = ((obj.X + border.West) * 16 + obj.LeftOffset, (obj.Y + border.North) * 16 + obj.TopOffset);
            canvas.Draw(obj.ObjectRender, x, y);
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
                                                         events<[objectCount. warpCount. scriptCount. signpostCount.
                                                            objects<[id. graphics. unused: x:500 y:500 elevation. moveType. range:|t|x::|y:: trainerType: trainerRangeOrBerryID: script<`xse`> flag: unused:]/objectCount>
                                                            warps<[x:500 y:500 elevation. warpID. map. bank.]/warps>
                                                            scripts<[x:500 y:500 elevation: trigger: index:: script<`xse`>]/scriptCount>
                                                            signposts<[x:500 y:500 elevation. kind. unused: arg::|h]/signposts>]1>
                                                         mapscripts<[type. pointer<>]!00>
                                                         connections<[count:: connections<[direction:: offset:: mapGroup. mapNum. unused:]/count>]>
                                                         music: layoutID: regionSectionID. cave. weather. mapType. allowBiking. flags.|t|allowEscaping.|allowRunning.|showMapName::: floorNum. battleType.

         emerald: data.maps.banks,                       layout<[width:: height:: borderblock<[border:|h]4> map<> tiles1<[isCompressed. isSecondary. padding: tileset<`lzt4`> pal<`ucp4:0123456789ABCDEF`> block<> attributes<> animation<>]1> tiles2<>]1>
                                                         events<[objects. warps. scripts. signposts.
                                                            objectP<[id. graphics. unused: x:500 y:500 elevation. moveType. range:|t|x::|y:: trainerType: trainerRangeOrBerryID: script<`xse`> flag: unused:]/objects>
                                                            warpP<[x:500 y:500 elevation. warpID. map. bank.]/warps>
                                                            scriptP<[x:500 y:500 elevation: trigger: index: unused: script<`xse`>]/scripts>
                                                            signpostP<[x:500 y:500 elevation. kind. unused: arg::|h]/signposts>]1>
                                                         mapscripts<[type. pointer<>]!00>
                                                         connections<>
                                                         music: layoutID: regionSectionID. cave. weather. mapType. padding: flags.|t|allowCycling.|allowEscaping.|allowRunning.|showMapName::. battleType.
       */
   }

   public enum MapButtonIcons {
      None, LeftRight, UpDown, X
   }

   public class MapButton : ViewModelCore {
      private readonly Action notify;
      private readonly ConnectionModel connection;
      private readonly int id;

      public MapButtonIcons Icon { get; }

      private bool anchorLeftEdge, anchorTopEdge;
      private int anchorX, anchorY;
      public bool AnchorLeftEdge { get => anchorLeftEdge; set => Set(ref anchorLeftEdge, value); } // if false, we anchor the right edge instead
      public bool AnchorTopEdge { get => anchorTopEdge; set => Set(ref anchorTopEdge, value); }    // if false, we anchor the bottom edge instead
      public int AnchorPositionX { get => anchorX; set => Set(ref anchorX, value); }
      public int AnchorPositionY { get => anchorY; set => Set(ref anchorY, value); }

      public MapButton(int id, ConnectionModel connection, Action notify, MapButtonIcons icon, int left = int.MinValue, int top = int.MinValue, int right = int.MinValue, int bottom = int.MinValue) {
         AnchorPositionX = left;
         AnchorLeftEdge = AnchorPositionX != int.MinValue;
         if (!AnchorLeftEdge) AnchorPositionX = -right;
         AnchorPositionY = top;
         AnchorTopEdge = AnchorPositionY != int.MinValue;
         if (!AnchorTopEdge) AnchorPositionY = -bottom;
         Icon = icon;
         (this.notify, this.connection) = (notify, connection);
      }

      public void Move(int x, int y) {
         if (Icon == LeftRight) {
            connection.Offset += x;
            if (x != 0) notify();
         } else if (Icon == UpDown) {
            connection.Offset += y;
            if (y != 0) notify();
         } else {
            throw new NotImplementedException();
         }
      }

      public bool TryUpdate(MapButton? other) {
         if (other.id != id ||
            other.connection.MapNum != connection.MapNum ||
            other.connection.MapGroup != connection.MapGroup) return false;
         AnchorLeftEdge = other.AnchorLeftEdge;
         AnchorTopEdge = other.AnchorTopEdge;
         AnchorPositionX = other.AnchorPositionX;
         AnchorPositionY = other.AnchorPositionY;
         return true;
      }
   }

   public record Border(int North, int East, int South, int West);

   public class ConnectionModel {
      private readonly ModelArrayElement connection;
      public ConnectionModel(ModelArrayElement connection) => this.connection = connection;

      public MapDirection Direction => (MapDirection)connection.GetValue("direction");
      public int Offset {
         get => connection.GetValue("offset");
         set => connection.SetValue("offset", value);
      }
      public int MapGroup => connection.GetValue("mapGroup");
      public int MapNum => connection.GetValue("mapNum");
   }

   public class EventModel {
      private readonly ModelArrayElement events;
      public EventModel(ModelArrayElement events) {
         this.events = events;
         var objectCount = events.GetValue("objectCount");
         var objects = events.GetSubTable("objects");
         var objectList = new List<ObjectEventModel>();
         if (objects != null) {
            for (int i = 0; i < objectCount; i++) objectList.Add(new ObjectEventModel(objects[i]));
         }
         Objects = objectList;
      }
      public IReadOnlyList<ObjectEventModel> Objects { get; }
   }

   public class ObjectEventModel {
      private readonly ModelArrayElement objectEvent;
      public ObjectEventModel(ModelArrayElement objectEvent) => this.objectEvent = objectEvent;
      public int ObjectID => objectEvent.GetValue("id");
      public int Graphics => objectEvent.GetValue("graphics");
      public int X => objectEvent.GetValue("x");
      public int Y => objectEvent.GetValue("y");
      public int Elevation => objectEvent.GetValue("elevation");
      public int MoveType => objectEvent.GetValue("moveType");
      public int RangeX => objectEvent.GetValue("range") & 0xF;
      public int RangeY => objectEvent.GetValue("range") >> 4;
      public int TrainerType => objectEvent.GetValue("trainerType");
      public int TrainerRangeOrBerryID => objectEvent.GetValue("trainerRangeOrBerryID");
      public int ScriptAddress => objectEvent.GetAddress("scirpt");
      public int Flag => objectEvent.GetValue("flag");

      public IPixelViewModel ObjectRender { get; private set; }
      public void Render(IDataModel model) {
         var owTable = new ModelTable(model, model.GetTable(HardcodeTablesModel.OverworldSprites).Start);
         if (Graphics >= owTable.Count) {
            ObjectRender = new ReadonlyPixelViewModel(new SpriteFormat(4, 2, 2, null), new short[256], 0);
            return;
         }
         var element = owTable[Graphics];
         var data = element.GetSubTable("data")[0];
         var sprites = data.GetSubTable("sprites")[0];
         var graphicsAddress = sprites.GetAddress("sprite");
         var graphicsRun = model.GetNextRun(graphicsAddress) as ISpriteRun;
         if (graphicsRun == null) {
            ObjectRender = new ReadonlyPixelViewModel(new SpriteFormat(4, 16, 16, null), new short[256], 0);
            return;
         }
         ObjectRender = ReadonlyPixelViewModel.Create(model, graphicsRun, true);
      }
      public int TopOffset => 16 - ObjectRender.PixelHeight;
      public int LeftOffset => (16 - ObjectRender.PixelWidth) / 2;
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
