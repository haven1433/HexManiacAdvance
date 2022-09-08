using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using HexManiac.Core.Models.Runs.Sprites;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;

using static HavenSoft.HexManiac.Core.ViewModels.Map.MapSliderIcons;

namespace HavenSoft.HexManiac.Core.ViewModels.Map {
   public class BlockMapViewModel : ViewModelCore, IPixelViewModel {

      private readonly IDataModel model;
      private readonly Func<ModelDelta> tokenFactory;
      private readonly int group, map;

      // TODO make these dynamic, right now this is only right for FireRed
      private int PrimaryTiles => 640;
      private int PrimaryBlocks => 640;
      private int TotalBlocks => 1024;
      private int PrimaryPalettes => 7;

      private static int MapSizeLimit => 0x2800; // (x+15)*(y+14) must be less that 0x2800 (5*2048). This can lead to limits like 113x66 or 497x6
      public static bool IsMapWithinSizeLimit(int width, int height) => (width / 16 + 15) * (height / 16 + 14) <= MapSizeLimit;

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

      public event EventHandler NeighborsChanged;

      public BlockMapViewModel(IDataModel model, Func<ModelDelta> tokenFactory, int group, int map) {
         this.model = model;
         this.tokenFactory = tokenFactory;
         (this.group, this.map) = (group, map);
         Transparent = -1;

         RefreshMapSize();

         (LeftEdge, TopEdge) = (-PixelWidth / 2, -PixelHeight / 2);
      }

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
         eventRenders = null;
         RefreshMapSize();
      }

      public void Scale(double x, double y, bool enlarge) {
         var old = spriteScale;

         if (enlarge && spriteScale < 10) spriteScale *= 2;
         else if (!enlarge && spriteScale > .1) spriteScale /= 2;

         if (old != spriteScale) UpdateEdgesFromScale(old, x - leftEdge, y - topEdge);
         NotifyPropertyChanged(nameof(SpriteScale));
      }

      public int GetBlock(double x, double y) {
         (x, y) = ((x - leftEdge) / spriteScale, (y - topEdge) / spriteScale);
         (x, y) = (x / 16, y / 16);

         var layout = GetLayout();
         var (width, height) = (layout.GetValue("width"), layout.GetValue("height"));
         if (x < 0 || y < 0 || x > width || x > height) return -1;
         var start = layout.GetAddress("blockmap");

         var border = GetBorderThickness(layout);

         var modelAddress = start + (((int)y - border.North) * width + (int)x - border.West) * 2;
         var data = model.ReadMultiByteValue(modelAddress, 2);
         var low = data & 0x3FF;
         return low;
      }

      public void DrawBlock(ModelDelta token, int index, double x, double y) {
         if (index < 0 || index > blockRenders.Count) return;
         (x, y) = ((x - leftEdge) / spriteScale, (y - topEdge) / spriteScale);
         (x, y) = (x / 16, y / 16);

         var layout = GetLayout();
         var (width, height) = (layout.GetValue("width"), layout.GetValue("height"));
         if (x < 0 || y < 0 || x > width || x > height) return;
         var start = layout.GetAddress("blockmap");

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

      public void UpdateEventLocation(ObjectEventModel ev, double x, double y) {
         var layout = GetLayout();
         var border = GetBorderThickness(layout);
         (x, y) = ((x - leftEdge) / spriteScale, (y - topEdge) / spriteScale);
         var (xx, yy) = ((int)(x / 16) - border.West, (int)(y / 16) - border.North);
         if (ev.X == xx && ev.Y == yy) return;
         if (xx < 0 || yy < 0) return;
         var (width, height) = (layout.GetValue("width"), layout.GetValue("height"));
         if (xx >= width || yy >= height) return;
         ev.X = xx;
         ev.Y = yy;
         pixelData = null;
         NotifyPropertyChanged(nameof(PixelData));
      }

      public IEnumerable<MapSlider> GetMapSliders() {
         var results = new List<MapSlider>();
         var connections = GetConnections();
         var border = GetBorderThickness();
         var tileSize = (int)(16 * spriteScale);
         int id = 0;

         // get sliders for up/down/left/right connections
         var connectionCount = (down: 0, up: 0, left: 0, right: 0);
         foreach (var connection in connections) {
            void Notify() => NeighborsChanged.Raise(this);
            var map = GetNeighbor(connection, border);

            if (connection.Direction == MapDirection.Up) {
               connectionCount.up++;
               yield return new ConnectionSlider(connection, Notify, id, LeftRight, right: map.LeftEdge, bottom: map.BottomEdge - tileSize);
               yield return new ConnectionSlider(connection, Notify, id + 1, LeftRight, left: map.RightEdge, bottom: map.BottomEdge - tileSize);
            }

            if (connection.Direction == MapDirection.Down) {
               connectionCount.down++;
               yield return new ConnectionSlider(connection, Notify, id, LeftRight, right: map.LeftEdge, top: map.TopEdge + tileSize);
               yield return new ConnectionSlider(connection, Notify, id + 1, LeftRight, left: map.RightEdge, top: map.TopEdge + tileSize);
            }

            if (connection.Direction == MapDirection.Left) {
               connectionCount.left++;
               yield return new ConnectionSlider(connection, Notify, id, UpDown, right: map.RightEdge - tileSize, bottom: map.TopEdge);
               yield return new ConnectionSlider(connection, Notify, id + 1, UpDown, right: map.RightEdge - tileSize, top: map.BottomEdge);
            }

            if (connection.Direction == MapDirection.Right) {
               connectionCount.right++;
               yield return new ConnectionSlider(connection, Notify, id, UpDown, left: map.LeftEdge + tileSize, bottom: map.TopEdge);
               yield return new ConnectionSlider(connection, Notify, id + 1, UpDown, left: map.LeftEdge + tileSize, top: map.BottomEdge);
            }

            id += 2;
         }

         // get sliders for size expansion
         var centerX = (LeftEdge + RightEdge - MapSlider.SliderSize) / 2;
         var centerY = (TopEdge + BottomEdge - MapSlider.SliderSize) / 2;
         yield return new ExpansionSlider(ResizeMapData, id + 0, UpDown, GetConnectionCommands(connections, MapDirection.Up), left: centerX, bottom: TopEdge);
         yield return new ExpansionSlider(ResizeMapData, id + 1, UpDown, GetConnectionCommands(connections, MapDirection.Down), left: centerX, top: BottomEdge);
         yield return new ExpansionSlider(ResizeMapData, id + 2, LeftRight, GetConnectionCommands(connections, MapDirection.Left), right: LeftEdge, top: centerY);
         yield return new ExpansionSlider(ResizeMapData, id + 3, LeftRight, GetConnectionCommands(connections, MapDirection.Right), left: RightEdge, top: centerY);
      }

      private IEnumerable<SliderCommand> GetConnectionCommands(IReadOnlyList<ConnectionModel> connections, MapDirection direction) {
         var toRemove = new List<int>();

         var info = CanConnect(direction);
         if (info != null) {
            if (info.Size > 3) {
               // we can make a map here of width/height longestSpanLength
               // and the offset is availableSpace[longestSpanStart]
               yield return new SliderCommand("Create New Map", ConnectNewMap) { Parameter = info };
               yield return new SliderCommand("Connect Existing Map", ConnectExistingMap) { Parameter = info };
            } else if (info.Offset < 0) {
               // we can make a map here of width/height 4
               // and the offset is -3
               yield return new SliderCommand("Create New Map", ConnectNewMap) { Parameter = info };
               yield return new SliderCommand("Connect Existing Map", ConnectExistingMap) { Parameter = info };
            } else {
               // we can make a map here of width/height 4
               // and the offset is dimensionLength-1
               yield return new SliderCommand("Create New Map", ConnectNewMap) { Parameter = info };
               yield return new SliderCommand("Connect Existing Map", ConnectExistingMap) { Parameter = info };
            }
         }

         for (int i = 0; i < connections.Count; i++) {
            if (connections[i].Direction != direction) continue;
            toRemove.Add(i);
         }
         if (toRemove.Count > 0) {
            // we can remove these connections
            yield return new SliderCommand("Remove Connections", RemoveConnections) { Parameter = toRemove };
         }
      }

      public ObjectEventModel EventUnderCursor(double x, double y) {
         var layout = GetLayout();
         var border = GetBorderThickness(layout);
         var tileX = (int)((x - LeftEdge) / SpriteScale / 16) - border.West;
         var tileY = (int)((y - TopEdge) / SpriteScale / 16) - border.North;
         foreach (var e in GetEvents(tokenFactory())) {
            if (e.X == tileX && e.Y == tileY) return e;
         }
         return null;
      }

      #region Work Methods

      private void ResizeMapData(MapDirection direction, int amount) {
         if (amount == 0) return;
         var token = tokenFactory();
         var map = GetMapModel(token);
         var layout = GetLayout(map);
         var run = model.GetNextRun(layout.GetAddress("blockmap")) as BlockmapRun;
         if (run == null) return;

         if (run.TryChangeSize(tokenFactory, direction, amount) != null) {
            var tileSize = (int)(16 * spriteScale);
            if (direction == MapDirection.Left) LeftEdge -= amount * tileSize;
            if (direction == MapDirection.Up) TopEdge -= amount * tileSize;
            foreach (var connection in GetConnections(map)) {
               if (direction == MapDirection.Left) {
                  if (connection.Direction == MapDirection.Down || connection.Direction == MapDirection.Up) {
                     connection.Offset += amount;
                  }
               } else if (direction == MapDirection.Up) {
                  if (connection.Direction == MapDirection.Left || connection.Direction == MapDirection.Right) {
                     connection.Offset += amount;
                  }
               }
            }
            foreach (var e in GetEvents(token)) {
               if (direction == MapDirection.Left) {
                  e.X += amount;
               } else if (direction == MapDirection.Up) {
                  e.Y += amount;
               }
            }
            RefreshMapSize();
            NeighborsChanged.Raise(this);
         }
      }

      private void ConnectNewMap(object obj) {
         var info = (ConnectionInfo)obj;
         var token = tokenFactory();
         var map = GetMapModel(token);
         var connectionsAndCount = map.GetSubTable("connections")[0];
         var connections = connectionsAndCount.GetSubTable("connections").Run;
         connections = model.RelocateForExpansion(token, connections, connections.Length + connections.ElementLength);
         connectionsAndCount.SetValue("count", connections.ElementCount + 1);
         var table = new ModelTable(model, connections.Start, token, connections);
         var newConnection = new ConnectionModel(table[connections.ElementCount]);
         newConnection.Offset = info.Offset;
         newConnection.Direction = info.Direction;

         // TODO ask the user what group they want to add the new map to -------------------
         int group = 0;
         var mapBanks = new ModelTable(model, model.GetTable(HardcodeTablesModel.MapBankTable).Start, token);
         var mapTable = mapBanks[group].GetSubTable("maps").Run;
         mapTable = mapTable.Append(token, 1);
         var address = CreateNewMap(token, info.Size, info.Size);

         model.WritePointer(token, mapTable.Length - 4, address); // TODO this probably causes a metadata inconsistency, I need to fix it
         NeighborsChanged.Raise(this);
      }

      private void ConnectExistingMap(object obj) {
         var info = (ConnectionInfo)obj;
         var token = tokenFactory();
         var opposite = info.Direction switch {
            MapDirection.Up => MapDirection.Down,
            MapDirection.Down => MapDirection.Up,
            MapDirection.Left => MapDirection.Right,
            MapDirection.Right => MapDirection.Left,
            _ => throw new NotImplementedException(),
         };

         // find available maps
         var options = new Dictionary<int, ConnectionInfo>();
         var table = model.GetTable(HardcodeTablesModel.MapBankTable);
         var mapBanks = new ModelTable(model, table.Start, token);
         for (int group = 0; group < mapBanks.Count; group++) {
            var bank = mapBanks[group];
            var maps = bank.GetSubTable("maps");
            for (int map = 0; map < maps.Count; map++) {
               var mapVM = new BlockMapViewModel(model, tokenFactory, group, map);
               var newInfo = mapVM.CanConnect(opposite);
               if (newInfo != null) options[mapVM.MapID] = newInfo;
            }
         }

         // TODO ask the user which one they want ------------------------------------
         var choice = options.Keys.First();

         var newConnection = AddConnection(info);
         newConnection.Offset = info.Offset;
         newConnection.Direction = info.Direction;
         newConnection.MapGroup = choice / 1000;
         newConnection.MapNum = choice % 1000;

         var otherMap = new BlockMapViewModel(model, tokenFactory, newConnection.MapGroup, newConnection.MapNum);
         info = options[choice];
         newConnection = otherMap.AddConnection(info);
         newConnection.Offset = info.Offset;
         newConnection.MapGroup = MapID / 1000;
         newConnection.MapNum = MapID % 1000;

         NeighborsChanged.Raise(this);
      }

      private void RemoveConnections(object obj) {
         var toRemove = (IReadOnlyList<int>)obj;
         var token = tokenFactory();
         var map = GetMapModel(token);
         var connections = GetConnections(map);
         for (int i = 0; i < toRemove.Count; i++) {
            for (int j = toRemove[i] - i + 1; j < connections.Count - i; j++) {
               connections[j - 1].Direction = connections[j].Direction;
               connections[j - 1].Offset = connections[j].Offset;
               connections[j - 1].MapGroup = connections[j].MapGroup;
               connections[j - 1].MapNum = connections[j].MapNum;
            }
            connections[connections.Count - i - 1].Clear(model, token);
         }
         var connectionsAndCount = map.GetSubTable("connections")[0];
         connectionsAndCount.SetValue("count", connections.Count - toRemove.Count);
         NeighborsChanged.Raise(this);
      }

      private ConnectionModel AddConnection(ConnectionInfo info) {
         var token = tokenFactory();
         var map = GetMapModel(token);
         var connectionsAndCount = map.GetSubTable("connections")[0];
         var connections = connectionsAndCount.GetSubTable("connections").Run;
         var count = connections.ElementCount;
         connections = connections.Append(token, 1);
         // TODO verify that connectionsAndCount.SetValue("count", ); is updated
         var table = new ModelTable(model, connections.Start, token, connections);
         var newConnection = new ConnectionModel(table[count]);
         return newConnection;
      }

      /// <summary>
      /// Inserts a new map using the existing borderblocks and block data.
      /// Creates event data with 0 events, creates connection data with 0 connections.
      /// Copies all the flags from the current map
      /// 
      /// </summary>
      private int CreateNewMap(ModelDelta token, int width, int height) {
         // TODO create new layout data
         //      TODO set width/height / blockmap
         //      TODO copy borderblock, blockdata1, blockdata2, borderwidth/borderheight
         // TODO create new event data with 0 events
         // TODO create new mapscript data with 0 scripts
         // TODO create new connections data with 0 connections
         // TODO copy flags for music, layoutID, regionSectionID, etc

         // TODO return the address of the new data
         throw new NotImplementedException();
      }

      #endregion

      #region Helper Methods

      private (int width, int height) GetBlockSize(ModelArrayElement layout = null) {
         var border = GetBorderThickness(layout);
         return (pixelWidth / 16 - border.West - border.East, pixelHeight / 16 - border.North - border.South);
      }

      private BlockMapViewModel GetNeighbor(ConnectionModel connection, Border border) {
         var vm = new BlockMapViewModel(model, tokenFactory, connection.MapGroup, connection.MapNum) { IncludeBorders = IncludeBorders, SpriteScale = SpriteScale };
         var (n, _, _, w) = vm.GetBorderThickness();
         vm.TopEdge = TopEdge + (connection.Offset + border.North - n) * (int)(16 * SpriteScale);
         vm.LeftEdge = LeftEdge + (connection.Offset + border.West - w) * (int)(16 * SpriteScale);
         if (connection.Direction == MapDirection.Left) vm.LeftEdge = LeftEdge - (int)(vm.PixelWidth * SpriteScale);
         if (connection.Direction == MapDirection.Right) vm.LeftEdge = LeftEdge + (int)(PixelWidth * SpriteScale);
         if (connection.Direction == MapDirection.Up) vm.TopEdge = TopEdge - (int)(vm.PixelHeight * SpriteScale);
         if (connection.Direction == MapDirection.Down) vm.TopEdge = TopEdge + (int)(PixelHeight * SpriteScale);
         return vm;
      }

      private void RefreshPaletteCache(ModelArrayElement layout = null, BlocksetModel blockModel1 = null, BlocksetModel blockModel2 = null) {
         if (blockModel1 == null || blockModel2 == null) {
            if (layout == null) layout = GetLayout();
            if (blockModel1 == null) blockModel1 = new BlocksetModel(model, layout.GetAddress("blockdata1"));
            if (blockModel2 == null) blockModel2 = new BlocksetModel(model, layout.GetAddress("blockdata2"));
         }

         palettes = BlockmapRun.ReadPalettes(blockModel1, blockModel2, PrimaryPalettes);
      }

      private void RefreshTileCache(ModelArrayElement layout = null, BlocksetModel blockModel1 = null, BlocksetModel blockModel2 = null) {
         if (blockModel1 == null || blockModel2 == null) {
            if (layout == null) layout = GetLayout();
            if (blockModel1 == null) blockModel1 = new BlocksetModel(model, layout.GetAddress("blockdata1"));
            if (blockModel2 == null) blockModel2 = new BlocksetModel(model, layout.GetAddress("blockdata2"));
         }

         tiles = BlockmapRun.ReadTiles(blockModel1, blockModel2, PrimaryTiles);
      }

      private void RefreshBlockCache(ModelArrayElement layout = null, BlocksetModel blockModel1 = null, BlocksetModel blockModel2 = null) {
         if (blockModel1 == null || blockModel2 == null) {
            if (layout == null) layout = GetLayout();
            if (blockModel1 == null) blockModel1 = new BlocksetModel(model, layout.GetAddress("blockdata1"));
            if (blockModel2 == null) blockModel2 = new BlocksetModel(model, layout.GetAddress("blockdata2"));
         }

         blocks = BlockmapRun.ReadBlocks(blockModel1, blockModel2);
      }

      private void RefreshBlockRenderCache(ModelArrayElement layout = null, BlocksetModel blockModel1 = null, BlocksetModel blockModel2 = null) {
         if (blocks == null || tiles == null || palettes == null) {
            if (layout == null) layout = GetLayout();
            if (blockModel1 == null) blockModel1 = new BlocksetModel(model, layout.GetAddress("blockdata1"));
            if (blockModel2 == null) blockModel2 = new BlocksetModel(model, layout.GetAddress("blockdata2"));
         }
         if (blocks == null) RefreshBlockCache(layout, blockModel1, blockModel2);
         if (tiles == null) RefreshTileCache(layout, blockModel1, blockModel2);
         if (palettes == null) RefreshPaletteCache(layout, blockModel1, blockModel2);

         this.blockRenders = BlockmapRun.CalculateBlockRenders(blocks, tiles, palettes);
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
         var events = GetEvents();
         foreach (var obj in events) {
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
         var start = layout.GetAddress("blockmap");

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

      private ModelArrayElement GetMapModel(ModelDelta token = null) {
         var table = model.GetTable(HardcodeTablesModel.MapBankTable);
         var mapBanks = new ModelTable(model, table.Start, token);
         var bank = mapBanks[group].GetSubTable("maps");
         var mapTable = bank[map].GetSubTable("map");
         return mapTable[0];
      }

      private ModelArrayElement GetLayout(ModelArrayElement map = null, ModelDelta token = null) {
         if (map == null) map = GetMapModel(token);
         return map.GetSubTable("layout")[0];
      }

      private IReadOnlyList<ConnectionModel> GetConnections(ModelArrayElement map = null, ModelDelta token = null) {
         if (map == null) map = GetMapModel(token);
         var connectionsAndCount = map.GetSubTable("connections")[0];
         var connections = connectionsAndCount.GetSubTable("connections");
         var list = new List<ConnectionModel>();
         foreach (var c in connections) list.Add(new(c));
         return list;
      }

      private IReadOnlyList<ObjectEventModel> GetEvents(ModelDelta token = null) {
         var table = model.GetTable(HardcodeTablesModel.MapBankTable);
         var mapBanks = new ModelTable(model, table.Start, token);
         var bank = mapBanks[group].GetSubTable("maps");
         var mapTable = bank[map].GetSubTable("map");
         var events = new EventGroupModel(mapTable[0].GetSubTable("events")[0]);
         return events.Objects;
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

      private ConnectionInfo CanConnect(MapDirection direction) {
         var connections = GetConnections();
         var (width, height) = ((int)(pixelWidth / spriteScale / 16), (int)(pixelHeight / spriteScale / 16));
         var dimensionLength = (direction switch {
            MapDirection.Up => width,
            MapDirection.Down => width,
            MapDirection.Left => height,
            MapDirection.Right => height,
            _ => throw new NotImplementedException(),
         });
         var availableSpace = dimensionLength.Range().ToList();

         // can't add a connection where there already is one
         for (int i = 0; i < connections.Count; i++) {
            if (connections[i].Direction != direction) continue;
            if (direction == MapDirection.Up || direction == MapDirection.Down) {
               var map = new BlockMapViewModel(model, tokenFactory, connections[i].MapGroup, connections[i].MapNum);
               var removeWidth = map.pixelWidth / 16;
               var removeOffset = connections[i].Offset;
               foreach (int j in removeWidth.Range()) availableSpace.Remove(j + removeOffset);
            } else if (direction == MapDirection.Left || direction == MapDirection.Right) {
               var map = new BlockMapViewModel(model, tokenFactory, connections[i].MapGroup, connections[i].MapNum);
               var removeHeight = map.pixelHeight / 16;
               var removeOffset = connections[i].Offset;
               foreach (int j in removeHeight.Range()) availableSpace.Remove(j + removeOffset);
            }
         }

         // find the longest stretch of available space
         var longestSpanLength = 0;
         var longestSpanStart = -1;
         var spanLength = 0;
         var spanStart = -1;
         for (int j = 0; j < availableSpace.Count; j++) {
            if (spanStart == -1) {
               spanStart = j;
               spanLength = 1;
            } else if (availableSpace[j - 1] + 1 == availableSpace[j]) {
               spanLength++;
            } else {
               if (spanLength > longestSpanLength) {
                  longestSpanLength = spanLength;
                  longestSpanStart = spanStart;
               }
               spanStart = j;
               spanLength = 1;
            }
         }

         // if a long space is availabe, we can connect to it
         // otherwise, we could technically connect to an edge
         if (longestSpanLength > 3) {
            // we can make a map here of width/height longestSpanLength
            // and the offset is availableSpace[longestSpanStart]
            return new ConnectionInfo(longestSpanLength, availableSpace[longestSpanStart], direction);
         } else if (availableSpace.Contains(0)) {
            // we can make a map here of width/height 4
            // and the offset is -3
            return new ConnectionInfo(4, -3, direction);
         } else if (availableSpace.Contains(dimensionLength - 1)) {
            // we can make a map here of width/height 4
            // and the offset is dimensionLength-1
            return new ConnectionInfo(4, dimensionLength - 1, direction);
         }

         return null;
      }

      #endregion

      /*
         ruby:    data.maps.banks,                       layout<[
                                                            width:: height:: borderblock<[border:|h]4> blockmap<`blm`>
                                                            blockdata1<[isCompressed. isSecondary. padding: tileset<`lzt4`> pal<`ucp4:0123456789ABCDEF`> block<> attributes<> animation<>]1>
                                                            blockdata2<>]1>
                                                         events<[e1 e2 e3 e4 ee1<> ee2<> ee3<> ee4<>]1>
                                                         mapscripts<[type. pointer<>]!00>
                                                         connections<>
                                                         music: layoutID: regionSectionID. cave. weather. mapType. padding. escapeRope. flags. battleType.

         firered: data.maps.banks,                       layout<[
                                                            width:: height:: borderblock<>
                                                            blockmap<`blm`>
                                                            blockdata1<[isCompressed. isSecondary. padding: tileset<`lzt4`> pal<`ucp4:0123456789ABCDEF`> block<> animation<> attributes<>]1>
                                                            blockdata2<>
                                                            borderwidth. borderheight. unused:]1>
                                                         events<[objectCount. warpCount. scriptCount. signpostCount.
                                                            objects<[id. graphics. unused: x:500 y:500 elevation. moveType. range:|t|x::|y:: trainerType: trainerRangeOrBerryID: script<`xse`> flag: unused:]/objectCount>
                                                            warps<[x:500 y:500 elevation. warpID. map. bank.]/warps>
                                                            scripts<[x:500 y:500 elevation: trigger: index:: script<`xse`>]/scriptCount>
                                                            signposts<[x:500 y:500 elevation. kind. unused: arg::|h]/signposts>]1>
                                                         mapscripts<[type. pointer<>]!00>
                                                         connections<[count:: connections<[direction:: offset:: mapGroup. mapNum. unused:]/count>]>
                                                         music: layoutID: regionSectionID. cave. weather. mapType. allowBiking. flags.|t|allowEscaping.|allowRunning.|showMapName::: floorNum. battleType.

         emerald: data.maps.banks,                       layout<[width:: height:: borderblock<[border:|h]4> blockmap<`blm`> blockdata1<[isCompressed. isSecondary. padding: tileset<`lzt4`> pal<`ucp4:0123456789ABCDEF`> block<> attributes<> animation<>]1> blockdata2<>]1>
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

   public record Border(int North, int East, int South, int West);

   public record ConnectionInfo(int Size, int Offset, MapDirection Direction);

   public class ConnectionModel {
      private readonly ModelArrayElement connection;
      public ConnectionModel(ModelArrayElement connection) => this.connection = connection;

      public MapDirection Direction {
         get => (MapDirection)connection.GetValue("direction");
         set => connection.SetValue("direction", (int)value);
      }

      public int Offset {
         get => connection.GetValue("offset");
         set => connection.SetValue("offset", value);
      }

      public int MapGroup {
         get => connection.GetValue("mapGroup");
         set => connection.SetValue("mapGroup", value);
      }

      public int MapNum {
         get => connection.GetValue("mapNum");
         set => connection.SetValue("mapNum", value);
      }

      public void Clear(IDataModel model, ModelDelta token) {
         token.ChangeData(model, connection.Start, connection.Length.Range(i => (byte)0xFF).ToList());
      }
   }

   public class EventGroupModel {
      private readonly ModelArrayElement events;
      public EventGroupModel(ModelArrayElement events) {
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
      public int X {
         get => objectEvent.GetValue("x");
         set => objectEvent.SetValue("x", value);
      }
      public int Y {
         get => objectEvent.GetValue("y");
         set => objectEvent.SetValue("y", value);
      }
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
}
