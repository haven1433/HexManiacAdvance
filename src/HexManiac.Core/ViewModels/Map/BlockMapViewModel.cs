using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using HexManiac.Core.Models.Runs.Sprites;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;

using static HavenSoft.HexManiac.Core.ViewModels.Map.MapSliderIcons;

namespace HavenSoft.HexManiac.Core.ViewModels.Map {
   public class BlockMapViewModel : ViewModelCore, IPixelViewModel {

      private readonly IFileSystem fileSystem;
      private readonly IDataModel model;
      private readonly Func<ModelDelta> tokenFactory;
      private readonly int group, map;

      // TODO make these dynamic, right now this is only right for FireRed
      private int PrimaryTiles => 640;
      private int PrimaryBlocks => 640;
      private int TotalBlocks => 1024;
      private int PrimaryPalettes => 7;

      #region SelectedEvent

      private IEventModel selectedEvent;
      public IEventModel SelectedEvent {
         get => selectedEvent;
         set {
            var oldValue = selectedEvent;
            selectedEvent = value;
            NotifyPropertyChanged();
            HandleSelectedEventChanged(oldValue);
         }
      }

      private void HandleSelectedEventChanged(IEventModel old) {
         if (old == selectedEvent) return;
         if (old != null) {
            old.EventVisualUpdated -= RefreshFromEventChange;
            old.CycleEvent -= CycleActiveEvent;
         }
         if (selectedEvent != null) {
            selectedEvent.EventVisualUpdated += RefreshFromEventChange;
            selectedEvent.CycleEvent += CycleActiveEvent;
         }
         RedrawEvents();
      }

      private void RefreshFromEventChange(object sender, EventArgs e) => RedrawEvents();

      private void CycleActiveEvent(object sender, EventCycleDirection direction) {
         // organize events into categories
         var events = GetEvents(tokenFactory());
         var categories = new List<List<IEventModel>> { new(), new(), new(), new() };
         int selectionIndex = -1, selectedCategory = -1;
         for (int i = 0; i < events.Count; i++) {
            int currentCategory =
               events[i] is ObjectEventModel ? 0 :
               events[i] is WarpEventModel ? 1 :
               events[i] is ScriptEventModel ? 2 :
               events[i] is SignpostEventModel ? 3 :
               -1;
            categories[currentCategory].Add(events[i]);

            if (events[i].Equals(selectedEvent)) {
               selectionIndex = categories[currentCategory].Count - 1;
               selectedCategory = currentCategory;
            };
         }

         // remove unused categories
         for (int i = 0; i < categories.Count; i++) {
            if (categories[i].Count != 0) continue;
            categories.RemoveAt(i);
            if (selectedCategory > i) selectedCategory--;
            i--;
         }

         // cycle
         if (direction == EventCycleDirection.PreviousCategory) {
            selectedCategory += categories.Count - 1;
            selectionIndex = 0;
         } else if (direction == EventCycleDirection.NextCategory) {
            selectedCategory += 1;
            selectionIndex = 0;
         } else if (direction == EventCycleDirection.PreviousEvent) {
            selectionIndex += categories[selectedCategory].Count - 1;
         } else if (direction == EventCycleDirection.NextEvent) {
            selectionIndex += 1;
         } else {
            throw new NotImplementedException();
         }
         selectedCategory %= categories.Count;
         selectionIndex %= categories[selectedCategory].Count;

         // update selection
         SelectedEvent = categories[selectedCategory][selectionIndex];
      }

      #endregion

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

      #region CollisionHighlight

      private int collisionHighlight = -1;
      public int CollisionHighlight {
         get => collisionHighlight;
         set {
            Set(ref collisionHighlight, value, old => {
               pixelData = null;
               NotifyPropertyChanged(nameof(PixelData));
            });
         }
      }

      #endregion

      #region Cache

      private short[][] palettes;
      private int[][,] tiles;
      private byte[][] blocks;
      private IReadOnlyList<IPixelViewModel> blockRenders;
      private IReadOnlyList<IEventModel> eventRenders;

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

      public string Name => MapIDToText(model, MapID);

      public event EventHandler NeighborsChanged;

      public BlockMapViewModel(IFileSystem fileSystem, IDataModel model, Func<ModelDelta> tokenFactory, int group, int map) {
         this.fileSystem = fileSystem;
         this.model = model;
         this.tokenFactory = tokenFactory;
         (this.group, this.map) = (group, map);
         Transparent = -1;
         InitTableRef();

         RefreshMapSize();

         (LeftEdge, TopEdge) = (-PixelWidth / 2, -PixelHeight / 2);
      }

      private string _bld, _layout, _objects, _warps, _scripts, _signposts, _events, _connections, _header, _map;
      private void InitTableRef() {
         // TODO R/S/E layout format is different
         _bld = $"[isCompressed. isSecondary. padding: tileset<> pal<`ucp4:0123456789ABCDEF`> block<> animation<> attributes<>]1";
         _layout = $"[width:: height:: borderblock<> blockmap<`blm`> blockdata1<{_bld}> blockdata2<{_bld}> borderwidth. borderheight. unused:]1";
         _objects = "[id. graphics. unused: x:500 y:500 elevation. moveType. range:|t|x::|y:: trainerType: trainerRangeOrBerryID: script<`xse`> flag: unused:]/objectCount";
         _warps = "[x:500 y:500 elevation. warpID. map. bank.]/warps";
         _scripts = "[x:500 y:500 elevation: trigger: index:: script<`xse`>]/scriptCount";
         _signposts = "[x:500 y:500 elevation. kind. unused: arg::|h]/signposts";
         _events = $"[objectCount. warpCount. scriptCount. signpostCount. objects<{_objects}> warps<{_warps}> scripts<{_scripts}> signposts<{_signposts}>]1";
         _connections = "[count:: connections<[direction:: offset:: mapGroup. mapNum. unused:]/count>]1";
         _header = "music: layoutID: regionSectionID. cave. weather. mapType. allowBiking. flags.|t|allowEscaping.|allowRunning.|showMapName::: floorNum. battleType.";
         _map = $"[layout<{_layout}> events<{_events}> mapscripts<[type. pointer<>]!00> connections<{_connections}> {_header}]";
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

      public void RedrawEvents() {
         eventRenders = null;
         pixelData = null;
         NotifyPropertyChanged(nameof(PixelData));
      }

      public void Scale(double x, double y, bool enlarge) {
         var old = spriteScale;

         if (enlarge && spriteScale < 10) {
            if (spriteScale < 1) spriteScale *= 2;
            else spriteScale += 1;
         } else if (!enlarge && spriteScale > .1) {
            if (spriteScale > 1) spriteScale -= 1;
            else spriteScale /= 2;
         }

         if (old != spriteScale) UpdateEdgesFromScale(old, x - leftEdge, y - topEdge);
         NotifyPropertyChanged(nameof(SpriteScale));
      }

      /// <summary>
      /// Gets the block index and collision index.
      /// </summary>
      public (int blockIndex, int collisionIndex) GetBlock(double x, double y) {
         (x, y) = ((x - leftEdge) / spriteScale, (y - topEdge) / spriteScale);
         (x, y) = (x / 16, y / 16);

         var layout = GetLayout();
         var (width, height) = (layout.GetValue("width"), layout.GetValue("height"));
         var border = GetBorderThickness(layout);
         var (xx, yy) = ((int)x - border.West, (int)y - border.North);
         if (xx < 0 || yy < 0 || xx > width || yy > height) return (-1, -1);
         var start = layout.GetAddress("blockmap");

         var modelAddress = start + (yy * width + xx) * 2;
         var data = model.ReadMultiByteValue(modelAddress, 2);
         return (data & 0x3FF, data >> 10);
      }

      /// <summary>
      /// If collisionIndex is not valid, it's ignored.
      /// If blockIndex is not valid, it's ignored.
      /// </summary>
      public void DrawBlock(ModelDelta token, int blockIndex, int collisionIndex, double x, double y) {
         (x, y) = ((x - leftEdge) / spriteScale, (y - topEdge) / spriteScale);
         (x, y) = (x / 16, y / 16);

         var layout = GetLayout();
         var (width, height) = (layout.GetValue("width"), layout.GetValue("height"));
         var border = GetBorderThickness(layout);
         var (xx, yy) = ((int)x - border.West, (int)y - border.North);
         if (xx < 0 || yy < 0 || xx > width || yy > height) return;
         var start = layout.GetAddress("blockmap");

         var modelAddress = start + (yy * width + xx) * 2;
         var data = model.ReadMultiByteValue(modelAddress, 2);
         var high = data >> 10;
         var low = data & 0x3FF;
         if (blockIndex >= 0 && blockIndex < blockRenders.Count) low = blockIndex;
         if (collisionIndex >= 0 && collisionIndex < 0x3F) high = collisionIndex;
         model.WriteMultiByteValue(modelAddress, 2, token, (high << 10) | low);

         if (blockIndex >= 0 && blockIndex < blockRenders.Count) {
            var canvas = new CanvasPixelViewModel(pixelWidth, pixelHeight, pixelData);
            (xx, yy) = ((xx + border.West) * 16, (yy + border.North) * 16);
            canvas.Draw(blockRenders[low], xx, yy);
            if (collisionIndex == collisionHighlight) HighlightCollision(canvas.PixelData, xx, yy);
            NotifyPropertyChanged(nameof(PixelData));
         }
      }

      public void UpdateEventLocation(IEventModel ev, double x, double y) {
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
         SelectedEvent = ev;
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

      public IEventModel EventUnderCursor(double x, double y) {
         var layout = GetLayout();
         var border = GetBorderThickness(layout);
         var tileX = (int)((x - LeftEdge) / SpriteScale / 16) - border.West;
         var tileY = (int)((y - TopEdge) / SpriteScale / 16) - border.North;
         foreach (var e in GetEvents(tokenFactory())) {
            if (e.X == tileX && e.Y == tileY) {
               if (selectedEvent == null || selectedEvent.X != e.X || selectedEvent.Y != e.Y) {
                  SelectedEvent = e;
                  pixelData = null;
                  NotifyPropertyChanged(nameof(PixelData));
               }
               return e;
            }
         }
         return SelectedEvent = null;
      }

      public void DeselectEvent() {
         if (selectedEvent == null) return;
         SelectedEvent = null;
         pixelData = null;
         NotifyPropertyChanged(nameof(PixelData));
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
         var token = tokenFactory();
         var mapBanks = new ModelTable(model, model.GetTable(HardcodeTablesModel.MapBankTable).Start, token);
         var enumViewModel = new EnumViewModel(mapBanks.Count.Range(i => i.ToString()).ToArray());
         var option = fileSystem.ShowOptions(
            "Pick a group",
            "Which map group do you want to add the new map to?",
            new[] { new[] { enumViewModel } },
            new VisualOption { Index = 1, Option = "OK", ShortDescription = "Insert New Map" });
         if (option == -1) return;

         var info = (ConnectionInfo)obj;
         var map = GetMapModel(token);
         var connectionsAndCount = map.GetSubTable("connections")[0];
         var connections = connectionsAndCount.GetSubTable("connections").Run;
         connections = model.RelocateForExpansion(token, connections, connections.Length + connections.ElementLength);
         connectionsAndCount.SetValue("count", connections.ElementCount + 1);
         var table = new ModelTable(model, connections.Start, token, connections);
         var newConnection = new ConnectionModel(table[connections.ElementCount]);
         newConnection.Offset = info.Offset;
         newConnection.Direction = info.Direction;

         newConnection.MapGroup = enumViewModel.Choice;

         ITableRun mapTable;
         if (mapBanks.Count == newConnection.MapGroup) {
            var newTable = model.RelocateForExpansion(token, mapBanks.Run, mapBanks.Run.Length + mapBanks.Run.ElementLength);
            newTable = newTable.Append(token, 1);
            model.ObserveRunWritten(token, newTable);
            mapBanks = new ModelTable(model, newTable.Start, token, newTable);
            var tableStart = model.FindFreeSpace(model.FreeSpaceStart, 8);
            mapTable = new TableStreamRun(model, tableStart, SortedSpan.One(mapBanks[newConnection.MapGroup].Start), $"[map<{_map}1>]", null, new DynamicStreamStrategy(model, null), 0);
            model.UpdateArrayPointer(token, null, null, -1, mapBanks[newConnection.MapGroup].Start, tableStart);
         } else {
            mapTable = mapBanks[newConnection.MapGroup].GetSubTable("maps").Run;
         }
         newConnection.MapNum = mapTable.ElementCount;
         mapTable = mapTable.Append(token, 1);
         model.ObserveRunWritten(token, mapTable);
         var address = CreateNewMap(token, info.Size, info.Size);
         model.UpdateArrayPointer(token, null, null, -1, mapTable.Start + mapTable.Length - 4, address);

         var otherMap = new BlockMapViewModel(fileSystem, model, tokenFactory, newConnection.MapGroup, newConnection.MapNum) { allOverworldSprites = allOverworldSprites };
         info = new ConnectionInfo(info.Size, -info.Offset, info.OppositeDirection);
         newConnection = otherMap.AddConnection(info);
         newConnection.Offset = info.Offset;
         newConnection.MapGroup = MapID / 1000;
         newConnection.MapNum = MapID % 1000;

         RefreshMapSize();
         NeighborsChanged.Raise(this);
      }

      private void ConnectExistingMap(object obj) {
         var info = (ConnectionInfo)obj;
         var token = tokenFactory();

         // find available maps
         var options = new Dictionary<int, ConnectionInfo>();
         var table = model.GetTable(HardcodeTablesModel.MapBankTable);
         var mapBanks = new ModelTable(model, table.Start, token);
         for (int group = 0; group < mapBanks.Count; group++) {
            var bank = mapBanks[group];
            var maps = bank.GetSubTable("maps");
            for (int map = 0; map < maps.Count; map++) {
               var mapVM = new BlockMapViewModel(fileSystem, model, tokenFactory, group, map) { allOverworldSprites = allOverworldSprites };
               var newInfo = mapVM.CanConnect(info.OppositeDirection);
               if (newInfo != null) options[mapVM.MapID] = newInfo;
            }
         }

         // select which map to add
         var keys = options.Keys.ToList();
         var enumViewModel = new EnumViewModel(keys.Select(key => MapIDToText(model, key)).ToArray());
         var option = fileSystem.ShowOptions(
            "Pick a group",
            "Which map group do you want to add the new map to?",
            new[] { new[] { enumViewModel } },
            new VisualOption { Index = 1, Option = "OK", ShortDescription = "Insert New Map" });
         if (option == -1) return;
         var choice = keys[enumViewModel.Choice];

         var newConnection = AddConnection(info);
         newConnection.Offset = info.Offset;
         newConnection.Direction = info.Direction;
         newConnection.MapGroup = choice / 1000;
         newConnection.MapNum = choice % 1000;

         var otherMap = new BlockMapViewModel(fileSystem, model, tokenFactory, newConnection.MapGroup, newConnection.MapNum) { allOverworldSprites = allOverworldSprites };
         info = options[choice];
         newConnection = otherMap.AddConnection(info);
         newConnection.Offset = info.Offset;
         newConnection.MapGroup = MapID / 1000;
         newConnection.MapNum = MapID % 1000;

         RefreshMapSize();
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
            var connectionsTable = connections[0].Table;
            if (connectionsTable.ElementCount == 1) {
               Erase(connectionsTable, token);
            } else {
               var shorterTable = connectionsTable.Append(token, -1);
               model.ObserveRunWritten(token, shorterTable);
            }
         }
         var connectionsAndCount = map.GetSubTable("connections")[0];
         connectionsAndCount.SetValue("count", connections.Count - toRemove.Count);

         RefreshMapSize();
         NeighborsChanged.Raise(this);
      }

      private ConnectionModel AddConnection(ConnectionInfo info) {
         var token = tokenFactory();
         var map = GetMapModel(token);
         var connectionsAndCountTable = map.GetSubTable("connections");
         if (connectionsAndCountTable == null) {
            var newConnectionsAndCountTable = CreateNewConnections(token);
            model.UpdateArrayPointer(token, null, null, -1, map.Start + 12, newConnectionsAndCountTable);
            connectionsAndCountTable = map.GetSubTable("connections");
         }
         var connectionsAndCount = connectionsAndCountTable[0];

         ITableRun connections;

         if (connectionsAndCount.GetValue("count") == 0) {
            var newConnectionTableStart = model.FindFreeSpace(model.FreeSpaceStart, 12);
            var childContent = ConnectionInfo.SingleConnectionContent;
            var lengthToken = ConnectionInfo.SingleConnectionLength;
            var childSegments = ArrayRun.ParseSegments(childContent, model);
            var parentStrategy = TableStreamRun.ParseEndStream(model, "connections", lengthToken, childSegments, connectionsAndCountTable.Run.ElementContent);
            connections = new TableStreamRun(model, newConnectionTableStart, SortedSpan.One(connectionsAndCount.Start + 4), $"[{childContent}]{lengthToken}", childSegments, parentStrategy, 0);
            connectionsAndCount.SetAddress("connections", newConnectionTableStart);
         } else {
            connections = connectionsAndCount.GetSubTable("connections").Run;
         }
         var count = connections.ElementCount;
         connections = connections.Append(token, 1);
         model.ObserveRunWritten(token, connections);

         var table = new ModelTable(model, connections.Start, token, connections);
         var newConnection = new ConnectionModel(table[count]);
         token.ChangeData(model, table[count].Start, new byte[12]);
         newConnection.Direction = info.Direction;
         return newConnection;
      }

      /// <summary>
      /// Inserts a new map using the existing borderblocks and block data.
      /// Creates event data with 0 events, creates connection data with 0 connections.
      /// Copies all the flags from the current map
      /// </summary>
      private int CreateNewMap(ModelDelta token, int width, int height) {
         var currentMap = GetMapModel();
         var mapStart = model.FindFreeSpace(model.FreeSpaceStart, 28);
         // music: layoutID: regionSectionID. cave. weather. mapType. allowBiking. flags. floorNum. battleType.
         for (int i = 16; i < 28; i++) token.ChangeData(model, mapStart + i, model[currentMap.Start + i]);

         WritePointerAndSource(token, mapStart + 0, CreateNewLayout(token, width, height));
         WritePointerAndSource(token, mapStart + 4, CreateNewEvents(token));
         WritePointerAndSource(token, mapStart + 8, CreateNewMapScripts(token));
         WritePointerAndSource(token, mapStart + 12, CreateNewConnections(token));

         var table = new TableStreamRun(model, mapStart, SortedSpan<int>.None, _map, null, new FixedLengthStreamStrategy(1));
         model.ObserveRunWritten(token, table);

         return mapStart;
      }

      private void Erase(ITableRun table, ModelDelta token) {
         foreach (var source in table.PointerSources) {
            model.ClearPointer(token, source, table.Start);
            model.WritePointer(token, source, Pointer.NULL);
         }
         model.ClearData(token, table.Start, table.Length);
      }

      #endregion

      #region Helper Methods

      private (int width, int height) GetBlockSize(ModelArrayElement layout = null) {
         var border = GetBorderThickness(layout);
         return (pixelWidth / 16 - border.West - border.East, pixelHeight / 16 - border.North - border.South);
      }

      private BlockMapViewModel GetNeighbor(ConnectionModel connection, Border border) {
         var vm = new BlockMapViewModel(fileSystem, model, tokenFactory, connection.MapGroup, connection.MapNum) {
            IncludeBorders = IncludeBorders,
            SpriteScale = SpriteScale,
            allOverworldSprites = allOverworldSprites,
         };
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
         var list = new List<IEventModel>();
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
               var collision = data >> 10;
               data &= 0x3FF;
               canvas.Draw(blockRenders[data], x * 16, y * 16);
               if (collision == collisionHighlight) HighlightCollision(canvas.PixelData, x * 16, y * 16);
            }
         }

         // draw the box for the selected event
         if (selectedEvent != null) {
            canvas.DrawBox((selectedEvent.X + border.West) * 16, (selectedEvent.Y + border.North) * 16, 16, UncompressedPaletteColor.Pack(6, 6, 6));
         }

         // now draw the events on top
         foreach (var obj in eventRenders) {
            var (x, y) = ((obj.X + border.West) * 16 + obj.LeftOffset, (obj.Y + border.North) * 16 + obj.TopOffset);
            canvas.Draw(obj.EventRender, x, y);
         }

         pixelData = canvas.PixelData;
      }

      private void HighlightCollision(short[] pixelData, int x, int y) {
         void Transform(int xx, int yy) {
            var p = (y + yy) * PixelWidth + x + xx;
            var color = UncompressedPaletteColor.ToRGB(pixelData[p]);
            color.r = (color.r - 8).LimitToRange(0, 31);
            color.g = (color.g - 8).LimitToRange(0, 31);
            color.b = (color.b - 8).LimitToRange(0, 31);
            pixelData[p] = UncompressedPaletteColor.Pack(color.r, color.g, color.b);
         }
         for (int i = 0; i < 15; i++) {
            Transform(i, 0);
            Transform(15 - i, 15);
            Transform(0, 15 - i);
            Transform(15, i);
         }
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
         var connectionsAndCountTable = map.GetSubTable("connections");
         var list = new List<ConnectionModel>();
         if (connectionsAndCountTable == null) return list;
         var connectionsAndCount = connectionsAndCountTable[0];
         var count = connectionsAndCount.GetValue("count");
         if (count == 0) return list;
         var connections = connectionsAndCount.GetSubTable("connections");
         if (connections == null) return new ConnectionModel[0];
         for (int i = 0; i < count; i++) list.Add(new(connections[i]));
         return list;
      }

      private IList<IPixelViewModel> allOverworldSprites;
      public static IList<IPixelViewModel> RenderOWs(IDataModel model) {
         var list = new List<IPixelViewModel>();
         var run = model.GetTable(HardcodeTablesModel.OverworldSprites);
         var ows = new ModelTable(model, run.Start, null, run);
         for (int i = 0; i < ows.Count; i++) {
            list.Add(ObjectEventModel.Render(model, ows, i));
         }
         return list;
      }

      private IReadOnlyList<IEventModel> GetEvents(ModelDelta token = null) {
         if (allOverworldSprites == null) allOverworldSprites = RenderOWs(model);
         var table = model.GetTable(HardcodeTablesModel.MapBankTable);
         var mapBanks = new ModelTable(model, table.Start, token);
         var bank = mapBanks[group].GetSubTable("maps");
         var mapTable = bank[map].GetSubTable("map");
         var results = new List<IEventModel>();
         var events = new EventGroupModel(mapTable[0].GetSubTable("events")[0], allOverworldSprites);
         results.AddRange(events.Objects);
         results.AddRange(events.Warps);
         results.AddRange(events.Scripts);
         results.AddRange(events.Signposts);
         return results;
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
         var (width, height) = (pixelWidth / 16, pixelHeight / 16);
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
               var map = new BlockMapViewModel(fileSystem, model, tokenFactory, connections[i].MapGroup, connections[i].MapNum) { allOverworldSprites = allOverworldSprites };
               var removeWidth = map.pixelWidth / 16;
               var removeOffset = connections[i].Offset;
               foreach (int j in removeWidth.Range()) availableSpace.Remove(j + removeOffset);
            } else if (direction == MapDirection.Left || direction == MapDirection.Right) {
               var map = new BlockMapViewModel(fileSystem, model, tokenFactory, connections[i].MapGroup, connections[i].MapNum) { allOverworldSprites = allOverworldSprites };
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
               (spanStart, spanLength) = (j, 1);
            } else if (availableSpace[j - 1] + 1 == availableSpace[j]) {
               spanLength++;
            } else {
               if (spanLength > longestSpanLength) (longestSpanStart, longestSpanLength) = (spanStart, spanLength);
               (spanStart, spanLength) = (j, 1);
            }
         }

         if (spanLength > longestSpanLength) (longestSpanStart, longestSpanLength) = (spanStart, spanLength);

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

      private int CreateNewLayout(ModelDelta token, int width, int height) {
         // width:: height:: borderblock<> blockmap<`blm`> blockdata1<> blockdata2<> borderwidth. borderheight. unused:
        var myLayout = GetLayout();
         var blockmapLength = width * height * 2;
         var blockmapStart = model.FindFreeSpace(model.FreeSpaceStart, blockmapLength + 28);
         var layoutStart = blockmapStart + blockmapLength;
         token.ChangeData(model, blockmapStart, new byte[blockmapLength]);

         model.WriteValue(token, layoutStart + 0, width);
         model.WriteValue(token, layoutStart + 4, height);
         WritePointerAndSource(token, layoutStart + 8, myLayout.GetAddress("borderblock"));
         WritePointerAndSource(token, layoutStart + 12, blockmapStart);
         WritePointerAndSource(token, layoutStart + 16, myLayout.GetAddress("blockdata1"));
         WritePointerAndSource(token, layoutStart + 20, myLayout.GetAddress("blockdata2"));
         if (myLayout.HasField("borderwidth")) {
            model.WriteValue(token, layoutStart + 24, myLayout.GetValue("borderwidth"));
            model.WriteValue(token, layoutStart + 25, myLayout.GetValue("borderwidth"));
            model.WriteMultiByteValue(layoutStart + 26, 2, token, 0);
         }
         return layoutStart;
      }

      private void WritePointerAndSource(ModelDelta token, int source, int destination) {
         model.WritePointer(token, source, destination);
         model.ObserveRunWritten(token, NoInfoRun.FromPointer(model, source));
      }

      private int CreateNewEvents(ModelDelta token) {
         // objectCount. warpCount. scriptCount. signpostCount. objects<> warps<> scripts<> signposts<>
         var eventStart = model.FindFreeSpace(model.FreeSpaceStart, 20);
         token.ChangeData(model, eventStart, new byte[20]);
         return eventStart;
      }

      private int CreateNewMapScripts(ModelDelta token) {
         // mapscripts<[type. pointer<>]!00>
         var eventStart = model.FindFreeSpace(model.FreeSpaceStart, 4);
         token.ChangeData(model, eventStart, new byte[] { 0, 0xFF, 0xFF, 0xFF });
         return eventStart;
      }

      private int CreateNewConnections(ModelDelta token) {
         // count:: connections<>
         var connectionStart = model.FindFreeSpace(model.FreeSpaceStart, 8);
         token.ChangeData(model, connectionStart, new byte[8]);
         var run = new TableStreamRun(model, connectionStart, SortedSpan<int>.None, $"[{ConnectionInfo.ConnectionTableContent}]", null, new FixedLengthStreamStrategy(1));
         model.ObserveRunWritten(token, run);
         return connectionStart;
      }

      public static string MapIDToText(IDataModel model, int id) {
         var group = id / 1000;
         var map = id % 1000;
         var offset = 0x58; // 88 maps names from Ruby

         var mapBanks = new ModelTable(model, model.GetTable(HardcodeTablesModel.MapBankTable).Start);
         var bank = mapBanks[group].GetSubTable("maps");
         var mapTable = bank[map].GetSubTable("map");
         var key = mapTable[0].GetValue("regionSectionID") - offset;

         var names = new ModelTable(model, model.GetTable(HardcodeTablesModel.MapNameTable).Start);
         var name = names[key].GetStringValue("name");

         return $"{group}.{map} ({name})";
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

   public record ConnectionInfo(int Size, int Offset, MapDirection Direction) {
      public const string SingleConnectionContent = "direction:: offset:: mapGroup. mapNum. unused:";
      public const string SingleConnectionLength = "/count";
      public static readonly string SingleConnectionFormat = $"[{SingleConnectionContent}]{SingleConnectionLength}";
      public static readonly string ConnectionTableContent = $"count:: connections<{SingleConnectionFormat}>";
      public MapDirection OppositeDirection => Direction switch {
         MapDirection.Up => MapDirection.Down,
         MapDirection.Down => MapDirection.Up,
         MapDirection.Left => MapDirection.Right,
         MapDirection.Right => MapDirection.Left,
         MapDirection.Dive => MapDirection.Emerge,
         MapDirection.Emerge => MapDirection.Dive,
         _ => throw new NotImplementedException(),
      };
   }

   public class ConnectionModel {
      private readonly ModelArrayElement connection;
      public ConnectionModel(ModelArrayElement connection) => this.connection = connection;

      public MapDirection Direction {
         get => (MapDirection)connection.GetValue("direction");
         set => connection.SetValue("direction", (int)value);
      }

      public ITableRun Table => connection.Table;

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

      public EventGroupModel(ModelArrayElement events, IList<IPixelViewModel> ows) {
         this.events = events;

         var objectCount = events.GetValue("objectCount");
         var objects = events.GetSubTable("objects");
         var objectList = new List<ObjectEventModel>();
         if (objects != null) {
            for (int i = 0; i < objectCount; i++) objectList.Add(new ObjectEventModel(objects[i], ows));
         }
         Objects = objectList;

         var warpCount = events.GetValue("warpCount");
         var warps = events.GetSubTable("warps");
         var warpList = new List<WarpEventModel>();
         if (warps != null) {
            for (int i = 0; i < warpCount; i++) warpList.Add(new WarpEventModel(warps[i]));
         }
         Warps = warpList;

         var scriptCount = events.GetValue("scriptCount");
         var scripts = events.GetSubTable("scripts");
         var scriptList = new List<ScriptEventModel>();
         if (scripts != null) {
            for (int i = 0; i < scriptCount; i++) scriptList.Add(new ScriptEventModel(scripts[i]));
         }
         Scripts = scriptList;

         var signpostCount = events.GetValue("signpostCount");
         var signposts = events.GetSubTable("signposts");
         var signpostList = new List<SignpostEventModel>();
         if (signposts != null) {
            for (int i = 0; i < signpostCount; i++) signpostList.Add(new SignpostEventModel(signposts[i]));
         }
         Signposts = signpostList;
      }

      public IReadOnlyList<ObjectEventModel> Objects { get; }
      public IReadOnlyList<WarpEventModel> Warps { get; }
      public IReadOnlyList<ScriptEventModel> Scripts { get; }
      public IReadOnlyList<SignpostEventModel> Signposts { get; }
      /*
       *  events<[objectCount. warpCount. scriptCount. signpostCount.
            objects<[id. graphics. unused: x:500 y:500 elevation. moveType. range:|t|x::|y:: trainerType: trainerRangeOrBerryID: script<`xse`> flag: unused:]/objectCount>
            warps<[x:500 y:500 elevation. warpID. map. bank.]/warps>
            scripts<[x:500 y:500 elevation: trigger: index:: script<`xse`>]/scriptCount>
            signposts<[x:500 y:500 elevation. kind. unused: arg::|h]/signposts>]1>
       */
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

   public enum ZoomDirection {
      None = 0,
      Shrink = 1,
      Enlarge = 2,
   }
}
