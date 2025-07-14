using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Code;
using HavenSoft.HexManiac.Core.Models.Map;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using HexManiac.Core.Models.Runs.Sprites;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using static HavenSoft.HexManiac.Core.ViewModels.Map.MapSliderIcons;

namespace HavenSoft.HexManiac.Core.ViewModels.Map {
   public record ImageLocation(double X, double Y); // ranges from (0,0) upper-left to (1,1) lower-right

   public class BlockMapViewModel : ViewModelCore, IPixelViewModel {
      public readonly Format format;
      public readonly IFileSystem fileSystem;
      public readonly MapTutorialsViewModel tutorials;
      public readonly IEditableViewPort viewPort;
      public readonly IDataModel model;
      public readonly EventTemplate eventTemplate;
      public readonly Func<ModelDelta> tokenFactory;
      public readonly int group, map;

      public int PrimaryTiles { get; }
      public int PrimaryBlocks { get; }
      public int TotalBlocks => 1024;
      public int PrimaryPalettes { get; } // 7

      public int zIndex;
      public int ZIndex { get => zIndex; set => Set(ref zIndex, value); }

      public event EventHandler RequestClearMapCaches;

      public IEditableViewPort ViewPort => viewPort;

      #region SelectedEvent

      public IEventViewModel selectedEvent;

      public ObservableCollection<EventSelector> EventSelectors { get; } = new();

      #endregion

      public static int MapSizeLimit => 0x2800; // (x+15)*(y+14) must be less that 0x2800 (5*2048). This can lead to limits like 113x66 or 497x6
      public static bool IsMapWithinSizeLimit(int width, int height) => (width + 15) * (height + 14) <= MapSizeLimit;

      public int MapID => group * 1000 + map;

      public MapHeaderViewModel header;

      public bool IsValidMap => GetMapModel() != null;

      #region IPixelViewModel

      public short transparent;
      public short Transparent { get => transparent; set => Set(ref transparent, value); }

      public int pixelWidth, pixelHeight;
      public int PixelWidth { get => pixelWidth; set => Set(ref pixelWidth, value); }
      public int PixelHeight { get => pixelHeight; set => Set(ref pixelHeight, value); }

      public readonly object pixelWriteLock = new();
      public short[] pixelData; // picture of the map

      public double spriteScale = 1;
      public double SpriteScale {
         get => spriteScale;
         set => Set(ref spriteScale, value, old => UpdateEdgesFromScale(old, old * pixelWidth / 2, old * pixelHeight / 2));
      }

      public void UpdateEdgesFromScale(double old, double centerX, double centerY) {
         LeftEdge += (int)(centerX * (1 - SpriteScale / old));
         TopEdge += (int)(centerY * (1 - SpriteScale / old));
      }

      #endregion

      #region IsSelected

      public bool isSelected;

      #endregion

      #region Position

      public int topEdge, leftEdge;
      public int TopEdge { get => topEdge; set => Set(ref topEdge, value); }
      public int LeftEdge { get => leftEdge; set => Set(ref leftEdge, value); }

      public int BottomEdge => topEdge + (int)(PixelHeight * SpriteScale);
      public int RightEdge => leftEdge + (int)(PixelWidth * SpriteScale);

      public ImageLocation hoverPoint = new(0, 0);
      public ImageLocation HoverPoint {
         get => hoverPoint;
         set {
            hoverPoint = value;
            NotifyPropertyChanged();
         }
      }

      public double WidthRatio => 80.0 / PixelWidth / Math.Min(1, SpriteScale);
      public double HeightRatio => 80.0 / PixelHeight / Math.Min(1, SpriteScale);
      public bool showBeneath;
      public bool ShowBeneath { get => showBeneath; set => Set(ref showBeneath, value, old => {
         NotifyPropertiesChanged(nameof(WidthRatio), nameof(HeightRatio));
         tutorials.Complete(Tutorial.SpaceBar_ShowBeneath);
      } ); }

      public MapDisplayOptions showEvents;

      #endregion

      #region Visual Blocks

      public IPixelViewModel blockPixels; // all the available blocks together in one big image

      #endregion

      #region CollisionHighlight

      public int collisionHighlight = -1;

      #endregion

      #region Cache

      public short[][] palettes;
      public int[][,] tiles;
      public byte[][] blocks;
      public byte[][] blockAttributes;
      public readonly List<IPixelViewModel> blockRenders = new(); // one image per block
      public IReadOnlyList<IEventViewModel> eventRenders;

      #endregion

      #region Borders

      public bool includeBorders = true;

      public IPixelViewModel borderBlock;

      #endregion

      #region Name

      public string FullName => MapIDToText(model, MapID);
      public string Name => $"({group}-{map})";

      public ObservableCollection<string> availableNames;
      public ObservableCollection<string> AvailableNames {
         get {
            if (availableNames != null) return availableNames;
            availableNames = new();
            foreach (var name in viewPort.Model.GetOptions(HardcodeTablesModel.MapNameTable)) {
               availableNames.Add(SanitizeName(name.Trim('"')));
            }
            return availableNames;
         }
      }
      public ObservableCollection<string> sortedAvailableNames;
      public ObservableCollection<string> SortedAvailableNames {
         get {
            if (sortedAvailableNames != null) return sortedAvailableNames;
            sortedAvailableNames = new(AvailableNames.OrderBy(name => name));
            return sortedAvailableNames;
         }
      }

      public int SelectedNameIndex {
         get {
            var offset = model.IsFRLG() ? 0x58 : 0;
            var banks = model.GetTableModel(HardcodeTablesModel.MapBankTable);
            var maps = banks[group].GetSubTable("maps");
            var map = maps[this.map];
            if (map == null) return -1;
            var subTable = map.GetSubTable("map");
            if (subTable == null) return -1;
            var self = subTable[0];
            if (!self.HasField("regionSectionID")) return -1;
            return self.GetValue("regionSectionID") - offset;
         }
         set {
            var offset = model.IsFRLG() ? 0x58 : 0;
            var banks = model.GetTableModel(HardcodeTablesModel.MapBankTable, tokenFactory);
            var maps = banks[group].GetSubTable("maps");
            var mapTable = maps[map];
            var subTable = mapTable.GetSubTable("map");
            if (subTable == null) return;
            var mapElement = subTable[0];
            var self = maps[map].GetSubTable("map")[0];
            if (!self.HasField("regionSectionID")) return;
            self.SetValue("regionSectionID", value + offset);
            NotifyPropertyChanged(nameof(FullName));
         }
      }

      public static string SanitizeName(string name) {
         return name.Replace("\\CC0000", " ");
      }

      #endregion

      #region Blockmap Sharing

      public bool BlockMapIsShared => FindLayoutUses().Count > 1;

      public int BlockMapShareCount => FindLayoutUses().Count;

      public ObservableCollection<JumpMapInfo> BlockMapUses => FindLayoutUses();

      public ObservableCollection<JumpMapInfo> layoutUseCache;
      public ObservableCollection<JumpMapInfo> FindLayoutUses() {
         if (layoutUseCache != null) return layoutUseCache;
         var map = GetMapModel();
         var layout = GetLayout(map);
         if (layout == null) return null;
         var modelRun = model.GetNextRun(layout.Start);
         if (modelRun.Start != layout.Start) return null;
         var uses = new ObservableCollection<JumpMapInfo>();
         var names = model.GetOptions(HardcodeTablesModel.MapNameTable);

         foreach (var candidate in GetAllMaps()) {
            if (!modelRun.PointerSources.Contains(candidate.Element.Start)) continue;
            var nameIndex = candidate.NameIndex;
            var name = nameIndex.InRange(0, names.Count) ? names[nameIndex] : "unknown";
            uses.Add(new(candidate.Group, candidate.Map, name, args => RequestChangeMap.Raise(this, args)));
         }

         return layoutUseCache = uses;
      }

      #endregion

      public event EventHandler NeighborsChanged;
      public event EventHandler AutoscrollTiles;
      public event EventHandler HideSidePanels;
      public event EventHandler<ChangeMapEventArgs> RequestChangeMap;

      public BlockEditor blockEditor;

      public BorderEditor borderEditor;

      public MapRepointer mapRepointer;
      public MapRepointer MapRepointer => mapRepointer;

      public MapScriptCollection mapScriptCollection;

      public WildPokemonViewModel wildPokemon;

      public SurfConnectionViewModel surfConnection;

      public BerryInfo berryInfo;
      public BerryInfo BerryInfo {
         get {
            if (berryInfo != null) return berryInfo;
            return berryInfo = SetupBerryInfo();
         }
         set {
            berryInfo = value;
            NotifyPropertyChanged(nameof(BerryInfo));
         }
      }

      public BerryInfo SetupBerryInfo() {
         var collection = new ObservableCollection<string>();
         var options = model.GetOptions(HardcodeTablesModel.BerryTableName);
         if (options == null) options = 100.Range().Select(i => i.ToString()).ToList();
         foreach (var option in options) collection.Add(option);
         var spots = Flags.GetBerrySpots(model, ViewPort.Tools.CodeTool.ScriptParser);
         return new(spots, collection);
      }

      public void InformRepoint(DataMovedEventArgs e) {
         viewPort.RaiseMessage($"{e.Type} data was moved to {e.Address:X6}.");
      }

      public void InformCreate(DataMovedEventArgs e) {
         viewPort.RaiseMessage($"{e.Type} data was created at {e.Address:X6}.");
      }

      public void Scale(double x, double y, bool enlarge) {
         (lastDrawX, lastDrawY) = (-1, -1);
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

      #region Draw / Paint

      public (int, int) ConvertCoordinates(double x, double y) {
         (x, y) = ((x - leftEdge) / spriteScale, (y - topEdge) / spriteScale);
         (x, y) = (x / 16, y / 16);

         var layout = GetLayout();
         var (width, height) = (layout.GetValue("width"), layout.GetValue("height"));
         var border = GetBorderThickness(layout);
         var (xx, yy) = ((int)Math.Floor(x) - border.West, (int)Math.Floor(y) - border.North);
         return (xx, yy);
      }

      /// <summary>
      /// Gets the block index and collision index.
      /// </summary>
      public (int blockIndex, int collisionIndex) GetBlock(double x, double y) {
         (lastDrawX, lastDrawY) = (-1, -1);
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

      public int lastDrawVal, lastDrawX, lastDrawY;

      /// <summary>
      /// If collisionIndex is not valid, it's ignored.
      /// If blockIndex is not valid, it's ignored.
      /// </summary>
      public void DrawBlock(ModelDelta token, int blockIndex, int collisionIndex, double x, double y) {
         var (xx, yy) = ConvertCoordinates(x, y);
         DrawBlock(token, blockIndex, collisionIndex, xx, yy);
      }

      public void Draw9Grid(ModelDelta token, int[,] grid, double x, double y) {
         var layout = GetLayout();
         var (width, height) = (layout.GetValue("width"), layout.GetValue("height"));
         var (xx, yy) = ConvertCoordinates(x, y);
         xx = xx.LimitToRange(0, width - 2);
         yy = yy.LimitToRange(0, height - 2);
         Draw9Grid(token, grid, xx, yy);
      }

      public void DiscoverCornersFor9Grid(int[,] grid) {
         innerCornersFor9Grid = null;
         var layout = new LayoutModel(GetLayout());

         // numpad layout for easy reference
         var np7 = grid[0, 0] & 0x3FF;
         var np8 = grid[1, 0] & 0x3FF;
         var np9 = grid[2, 0] & 0x3FF;
         var np4 = grid[0, 1] & 0x3FF;
         var np5 = grid[1, 1] & 0x3FF;
         var np6 = grid[2, 1] & 0x3FF;
         var np1 = grid[0, 2] & 0x3FF;
         var np2 = grid[1, 2] & 0x3FF;
         var np3 = grid[2, 2] & 0x3FF;

         // histograms for inside corners, where the ! is a 'hole':
         // 7  .  9
         // .  !  .
         // 1  .  3
         var corner7 = new AutoDictionary<int, int>(_ => 0);
         var corner9 = new AutoDictionary<int, int>(_ => 0);
         var corner1 = new AutoDictionary<int, int>(_ => 0);
         var corner3 = new AutoDictionary<int, int>(_ => 0);

         // use maps in the game to figure out the corners
         foreach (var map in GetAllMaps()) {
            if (map.Layout.PrimaryBlockset.Start != layout.PrimaryBlockset.Start && map.Layout.SecondaryBlockset.Start != layout.SecondaryBlockset.Start) continue;

            var blockMap = map.Layout.BlockMap;
            for (int y = 0; y < blockMap.Height; y++) {
               bool topEdge = y == 0, bottomEdge = y == blockMap.Height - 1;
               for (int x = 0; x < blockMap.Width; x++) {
                  bool leftEdge = x == 0, rightEdge = x == blockMap.Width - 1;
                  var block = blockMap[x, y].Block; // store tile and collision, but only check that tiles match
                  if (!rightEdge && !bottomEdge && blockMap[x + 1, y].Tile.IsAny(np3, np2) && blockMap[x, y + 1].Tile.IsAny(np3, np6)) {
                     corner7[block] += 1;
                  }
                  if (!leftEdge && !bottomEdge && blockMap[x - 1, y].Tile.IsAny(np1, np2) && blockMap[x, y + 1].Tile.IsAny(np1, np4)) {
                     corner9[block] += 1;
                  }
                  if (!rightEdge && !topEdge && blockMap[x + 1, y].Tile.IsAny(np8, np9) && blockMap[x, y - 1].Tile.IsAny(np6, np9)) {
                     corner1[block] += 1;
                  }
                  if (!leftEdge && !topEdge && blockMap[x - 1, y].Tile.IsAny(np7, np8) && blockMap[x, y - 1].Tile.IsAny(np4, np7)) {
                     corner3[block] += 1;
                  }
               }
            }
         }

         var corners = new int[2, 2];
         var defaultBlock = grid[1, 1] & 0x3FF;
         var key = corner7.MostCommonKey(); corners[0, 0] = key != 0 ? key : defaultBlock;
         key = corner9.MostCommonKey(); corners[1, 0] = key != 0 ? key : defaultBlock;
         key =  corner1.MostCommonKey(); corners[0, 1] = key != 0 ? key : defaultBlock;
         key =  corner3.MostCommonKey(); corners[1, 1] = key != 0 ? key : defaultBlock;
         innerCornersFor9Grid = corners;
      }

      public void PrepareFor9GridDraw(int[,] grid) {
         // when we do a 9-grid draw,
         // we want to track which draws are part of the current interaction only
         var layout = new LayoutModel(GetLayout());
         current9gridInteractionMap = new bool[layout.Width, layout.Height];

         // NOTE add this code if we want to make new draws connect to existing draws

         /*
         var corners = this.innerCornersFor9Grid ?? new int[,] { { grid[1, 1], grid[1, 1] }, { grid[1, 1], grid[1, 1] } };

         var targets = new HashSet<int>();
         for (int y = 0; y < 3; y++) for (int x = 0; x < 3; x++) targets.Add(grid[x, y]);
         for (int y = 0; y < 2; y++) for (int x = 0; x < 2; x++) targets.Add(corners[x, y]);

         for (int y = 0; y < layout.Height; y++) {
            for (int x = 0; x < layout.Width; x++) {
               current9gridInteractionMap[x, y] = targets.Contains(layout.BlockMap[x, y].Block);
            }
         }
         //*/
      }

      public static int Get9GridBlock(bool[,] map, Point p, int[,] grid9, int[,] corners) {
         int neighborhood = 0;
         for (int y = -1; y < 2; y++) {
            for (int x = -1; x < 2; x++) {
               if (!(p.X + x).InRange(0, map.GetLength(0)) || !(p.Y + y).InRange(0, map.GetLength(1))) continue;
               var bit = 8 - ((y + 1) * 3 + x + 1);
               neighborhood |= (map[p.X + x, p.Y + y] ? 1 : 0) << bit;
            }
         }

         return neighborhood switch {
            // normal corners - 4 varients of each (2 corners don't matter)
            0b110_110_000 => grid9[2, 2], // bottom-right
            0b110_110_100 => grid9[2, 2],
            0b111_110_100 => grid9[2, 2],
            0b111_110_000 => grid9[2, 2],
            0b011_011_000 => grid9[0, 2], // bottom-left
            0b011_011_001 => grid9[0, 2],
            0b111_011_001 => grid9[0, 2],
            0b111_011_000 => grid9[0, 2],
            0b000_110_110 => grid9[2, 0], // top-right
            0b100_110_110 => grid9[2, 0],
            0b100_110_111 => grid9[2, 0],
            0b000_110_111 => grid9[2, 0],
            0b000_011_011 => grid9[0, 0], // top-left
            0b001_011_011 => grid9[0, 0],
            0b000_011_111 => grid9[0, 0],
            0b001_011_111 => grid9[0, 0],

            // edges - 4 variants of each (2 corners don't matter)
            0b111_111_000 => grid9[1, 2], // bottom
            0b111_111_100 => grid9[1, 2],
            0b111_111_001 => grid9[1, 2],
            0b111_111_101 => grid9[1, 2],
            0b000_111_111 => grid9[1, 0], // top
            0b100_111_111 => grid9[1, 0],
            0b001_111_111 => grid9[1, 0],
            0b101_111_111 => grid9[1, 0],
            0b011_011_011 => grid9[0, 1], // left
            0b111_011_011 => grid9[0, 1],
            0b011_011_111 => grid9[0, 1],
            0b111_011_111 => grid9[0, 1],
            0b110_110_110 => grid9[2, 1], // right
            0b111_110_110 => grid9[2, 1],
            0b110_110_111 => grid9[2, 1],
            0b111_110_111 => grid9[2, 1],

            // inside corners
            0b111_111_110 => corners[0, 0],
            0b111_111_011 => corners[1, 0],
            0b110_111_111 => corners[0, 1],
            0b011_111_111 => corners[1, 1],

            _ => grid9[1, 1],
         };
      }

      public bool[,] current9gridInteractionMap;
      public int[,]? innerCornersFor9Grid;

      public int[,] ReadRectangle(int x, int y, int w, int h) {
         var results = new int[w, h];
         var layout = GetLayout();
         var (width, height) = (layout.GetValue("width"), layout.GetValue("height"));
         var start = layout.GetAddress("blockmap");
         for (int xx = 0; xx < w; xx++) {
            for (int yy = 0; yy < h; yy++) {
               if (x + xx < 0 || y + yy < 0 || x + xx >= width || y + yy >= height) continue;
               var address = start + ((yy + y) * width + xx + x) * 2;
               results[xx, yy] = model.ReadMultiByteValue(address, 2);
            }
         }
         return results;
      }

      public void PaintBlock(ModelDelta token, Point p, Point size, int start, int before, int after) {
         if (before == after) return;
         if (p.X < 0 || p.Y < 0 || p.X >= size.X || p.Y >= size.Y) return;
         var address = start + (p.Y * size.X + p.X) * 2;
         if (model.ReadMultiByteValue(address, 2) != before) return;
         model.WriteMultiByteValue(address, 2, token, after);
         PaintBlock(token, p + new Point(-1, 0), size, start, before, after);
         PaintBlock(token, p + new Point(1, 0), size, start, before, after);
         PaintBlock(token, p + new Point(0, -1), size, start, before, after);
         PaintBlock(token, p + new Point(0, 1), size, start, before, after);
      }

      public IEnumerable<Point> GetAllMatchingConnectedBlocks(int x, int y) {
         var added = new HashSet<Point>();
         var toAdd = new Queue<Point>();
         toAdd.Enqueue(new(x, y));

         var layout = GetLayout();
         var (width, height) = (layout.GetValue("width"), layout.GetValue("height"));
         var start = layout.GetAddress("blockmap");
         var address = start + (y * width + x) * 2;
         int read(Point p) => model.ReadMultiByteValue(start + (p.Y * width + p.X) * 2, 2);
         var matchBlock = read(new(x, y));

         while (toAdd.Count > 0) {
            var current = toAdd.Dequeue();
            if (added.Contains(current)) continue;
            if (current.X < 0 || current.X >= width) continue;
            if (current.Y < 0 || current.Y >= height) continue;
            if (read(current) != matchBlock) continue;
            yield return current;
            added.Add(current);
            toAdd.Enqueue(current + new Point(-1, 0));
            toAdd.Enqueue(current + new Point(1, 0));
            toAdd.Enqueue(current + new Point(0, -1));
            toAdd.Enqueue(current + new Point(0, 1));
         }
      }

      #endregion

      #region Events

      const int SizeX = 7, SizeY = 7;

      #endregion

      #region Work Methods

      public IEnumerable<MapModel> GetAllMaps() {
         var banks = model.GetTableModel(HardcodeTablesModel.MapBankTable);
         for (int i = 0; i < banks.Count; i++) {
            var bank = banks[i];
            if (bank == null) continue;
            var maps = bank.GetSubTable("maps");
            for (int j = 0; j < maps.Count; j++) {
               var mapList = maps[j];
               if (mapList == null) continue;
               var mapTable = mapList.GetSubTable("map");
               if (mapTable == null) continue;
               var map = mapTable[0];
               yield return new(map, i, j);
            }
         }
      }

      public ConnectionModel AddConnection(ConnectionInfo info) {
         var token = tokenFactory();
         var map = GetMapModel();

         var connections = GetOrCreateConnections(map, token);
         if (connections == null) return null;

         var count = connections.ElementCount;
         connections = connections.Append(token, 1);
         model.ObserveRunWritten(token, connections);

         var table = new ModelTable(model, connections.Start, tokenFactory, connections);
         var newConnection = new ConnectionModel(table[count], group, this.map);
         token.ChangeData(model, table[count].Start, new byte[12]);
         newConnection.Direction = info.Direction;
         return newConnection;
      }

      public ITableRun GetOrCreateConnections(ModelArrayElement map, ModelDelta token) {
         if (map == null) return null;
         var connectionsAndCountTable = map.GetSubTable("connections");
         if (connectionsAndCountTable == null || map.Model.GetNextRun(connectionsAndCountTable.Run.Start).Start != connectionsAndCountTable.Run.Start) {
            var newConnectionsAndCountTable = MapRepointer.CreateNewConnections(token);
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
            InformCreate(new("Connection", newConnectionTableStart));
         } else {
            var connectionsTable = connectionsAndCount.GetSubTable("connections");
            if (connectionsTable == null) return null;
            connections = connectionsTable.Run;
         }

         return connections;
      }

      public ModelTable CreateEventTable(ModelArrayElement map) {
         // create some blank event data: 0 events for each of the four categories
         var token = tokenFactory();
         var eventAddress = MapRepointer.CreateNewEvents(token);
         model.UpdateArrayPointer(token, map.Table.ElementContent[1], map.Table.ElementContent, 0, map.Start + 4, eventAddress);
         return map.GetSubTable("events");
      }

      public event EventHandler CanEditTilesetChanged;
      public bool CanEditTileset(string type) {
         var model = new MapModel(GetMapModel(), group, map);
         var spriteAddress = model.Layout?.PrimaryBlockset?.TilesetAddress ?? -1;
         var paletteAddress = model.Layout?.PrimaryBlockset?.PaletteAddress ?? -1;
         if (type == "Secondary") {
            spriteAddress = model.Layout?.SecondaryBlockset?.TilesetAddress ?? -1;
            paletteAddress = model.Layout?.SecondaryBlockset?.PaletteAddress ?? -1;
         }
         return this.model.GetNextRun(spriteAddress) is ISpriteRun sRun && sRun.Start == spriteAddress &&
            this.model.GetNextRun(paletteAddress) is IPaletteRun pRun && pRun.Start == paletteAddress;
      }

      public bool CanCreateFlyEvent {
         get {
            var map = GetMapModel();
            if (map == null) return false;
            var region = map.GetValue(Format.RegionSection);
            if (model.IsFRLG()) region -= 88;
            var connections = model.GetTableModel(HardcodeTablesModel.FlyConnections);
            if (region < 0 || region >= connections.Count) return false;
            return connections[region].GetValue("flight") == 0;
         }
      }

      public FlyEventViewModel CreateFlyEvent() {
         var map = GetMapModel();
         var region = map.GetValue(Format.RegionSection);
         if (model.IsFRLG()) region -= 88;
         var connections = model.GetTableModel(HardcodeTablesModel.FlyConnections, tokenFactory);
         if (region < 0 || region >= connections.Count) return null;
         var flight = connections[region].GetValue("flight");
         if (flight != 0) return null;
         var spawns = model.GetTableModel(HardcodeTablesModel.FlySpawns, tokenFactory);

         // hunt for an available spawn location
         var emptySpawn = -1;
         for (int i = 0; i < spawns.Count; i++) {
            if (spawns[i].GetValue("x") == 0 && spawns[i].GetValue("y") == 0 && spawns[i].GetValue("bank") == 0 && spawns[i].GetValue("map") == 0) {
               emptySpawn = i;
               break;
            }
         }

         // if there were no empty entries in the table, add a new one
         if (emptySpawn == -1) {
            var newSpawns = model.RelocateForExpansion(tokenFactory(), spawns.Run, spawns.Run.Length + spawns.Run.ElementLength);
            newSpawns = newSpawns.Append(tokenFactory(), 1);
            model.ObserveRunWritten(tokenFactory(), newSpawns);
            if (newSpawns.Start != spawns.Run.Start) InformRepoint(new("Fly Spawns", newSpawns.Start));
            spawns = new ModelTable(model, newSpawns, tokenFactory);
            emptySpawn = spawns.Count - 1;
         }

         // update the connections and spawn table
         connections[region].SetValue("flight", emptySpawn + 1);
         connections[region].SetValue("bank", group);
         connections[region].SetValue("map", this.map);
         spawns[emptySpawn].SetValue("bank", group);
         spawns[emptySpawn].SetValue("map", this.map);

         NotifyPropertyChanged(nameof(CanCreateFlyEvent));
         return new FlyEventViewModel(spawns[emptySpawn], group, this.map, emptySpawn + 1);
      }

      // TODO use this for connections as well, since the structure is the same
      public ModelArrayElement AddEvent(ModelArrayElement events, Func<ModelDelta> tokenFactory, string countName, string fieldName) {
         var model = events.Model;
         var count = events.GetValue(countName);
         var elementTable = events.GetSubTable(fieldName)?.Run;
         if (count == 0 || elementTable == null) {
            var segment = (ArrayRunPointerSegment)events.Table.ElementContent.Single(seg => seg.Name == fieldName);
            var divider = segment.InnerFormat.LastIndexOf("/");
            var newTableStart = model.FindFreeSpace(model.FreeSpaceStart, 24);
            var childContent = segment.InnerFormat.Substring(0, divider);
            childContent = childContent.Substring(1, childContent.Length - 2);
            var lengthToken = segment.InnerFormat.Substring(divider);
            var childSegments = ArrayRun.ParseSegments(childContent, model);
            var parentStrategy = TableStreamRun.ParseEndStream(model, fieldName, lengthToken, childSegments, events.Table.ElementContent);
            elementTable = new TableStreamRun(model, newTableStart, SortedSpan.One(events.Table.ElementContent.Until(seg => seg.Name == fieldName).Sum(seg => seg.Length) + events.Table.Start), segment.InnerFormat, childSegments, parentStrategy, 0);
            events.SetAddress(fieldName, newTableStart);
         }
         var token = tokenFactory();
         var newRun = elementTable.Append(token, 1);
         if (model.GetNextRun(newRun.Start + 1).Start != newRun.Start) model.ClearFormat(token, newRun.Start + 1, newRun.Length - 1);
         model.ObserveRunWritten(token, newRun);
         if (newRun.Start != elementTable.Start) InformRepoint(new(fieldName, newRun.Start));
         return new ModelArrayElement(model, newRun.Start, newRun.ElementCount - 1, tokenFactory, newRun);
      }

      public void Erase(ITableRun table, ModelDelta token) {
         foreach (var source in table.PointerSources) {
            model.ClearPointer(token, source, table.Start);
            model.WritePointer(token, source, Pointer.NULL);
         }
         model.ClearData(token, table.Start, table.Length);
      }

      public void UpdateLayoutID() {
         var message = UpdateLayoutID(model, group, map, tokenFactory);
         if (message != null) {
            InformRepoint(message);
         }
      }

      public static DataMovedEventArgs UpdateLayoutID(IDataModel model, int groupID, int mapID, Func<ModelDelta> tokenFactory) {
         // step 1: test if we need to update the layout id
         var layoutTable = model.GetTable(HardcodeTablesModel.MapLayoutTable);
         var map = GetMapModel(model, groupID, mapID, tokenFactory);
         var layoutID = map.GetValue("layoutID") - 1;
         var addressFromMap = map.GetAddress("layout");
         var addressFromTable = model.ReadPointer(layoutTable.Start + layoutTable.ElementLength * layoutID);
         if (addressFromMap == addressFromTable) return null;

         var matches = layoutTable.ElementCount.Range().Where(i => model.ReadPointer(layoutTable.Start + layoutTable.ElementLength * i) == addressFromMap).ToList();
         var token = tokenFactory();
         DataMovedEventArgs result = null;
         if (matches.Count == 0) {
            var originalLayoutTableStart = layoutTable.Start;
            layoutTable = model.RelocateForExpansion(token, layoutTable, layoutTable.Length + 4);
            layoutTable = layoutTable.Append(token, 1);
            model.ObserveRunWritten(token, layoutTable);
            model.UpdateArrayPointer(token, layoutTable.ElementContent[0], layoutTable.ElementContent, -1, layoutTable.Start + layoutTable.ElementLength * (layoutTable.ElementCount - 1), addressFromMap);
            if (originalLayoutTableStart != layoutTable.Start) result = new("Layout Table", layoutTable.Start);
            matches.Add(layoutTable.ElementCount - 1);
         }
         map.SetValue("layoutID", matches[0] + 1);
         return result;
      }

      #endregion

      #region Helper Methods

      public (int width, int height) GetBlockSize(ModelArrayElement layout = null) {
         var border = GetBorderThickness(layout);
         if (border == null) return (0, 0);
         return (pixelWidth / 16 - border.West - border.East, pixelHeight / 16 - border.North - border.South);
      }

      public void RefreshPaletteCache(ModelArrayElement layout = null, BlocksetModel blockModel1 = null, BlocksetModel blockModel2 = null) {
         if (blockModel1 == null || blockModel2 == null) {
            if (layout == null) layout = GetLayout();
            if (blockModel1 == null) blockModel1 = new BlocksetModel(model, layout.GetAddress(Format.PrimaryBlockset));
            if (blockModel2 == null) blockModel2 = new BlocksetModel(model, layout.GetAddress(Format.SecondaryBlockset));
         }

         palettes = BlockmapRun.ReadPalettes(blockModel1, blockModel2, PrimaryPalettes);
         blockEditor?.RefreshPaletteCache(palettes);
      }

      public void RefreshTileCache(ModelArrayElement layout = null, BlocksetModel blockModel1 = null, BlocksetModel blockModel2 = null) {
         if (blockModel1 == null || blockModel2 == null) {
            if (layout == null) layout = GetLayout();
            if (blockModel1 == null) blockModel1 = new BlocksetModel(model, layout.GetAddress("blockdata1"));
            if (blockModel2 == null) blockModel2 = new BlocksetModel(model, layout.GetAddress("blockdata2"));
         }

         tiles = BlockmapRun.ReadTiles(blockModel1, blockModel2, PrimaryTiles);
         blockEditor?.RefreshTileCache(tiles);
      }

      public void RefreshBlockAttributeCache(ModelArrayElement layout = null, BlocksetModel blockModel1 = null, BlocksetModel blockModel2 = null) {
         if (blockModel1 == null || blockModel2 == null) {
            if (layout == null) layout = GetLayout();
            if (layout == null) return;
            if (blockModel1 == null) blockModel1 = new BlocksetModel(model, layout.GetAddress("blockdata1"));
            if (blockModel2 == null) blockModel2 = new BlocksetModel(model, layout.GetAddress("blockdata2"));
         }
         if (!blockModel1.Start.InRange(0, model.Count) || !blockModel2.Start.InRange(0, model.Count)) return;

         int width = layout.GetValue("width"), height = layout.GetValue("height");
         int start = layout.GetAddress(Format.BlockMap);
         var maxUsedPrimary = BlockmapRun.GetMaxUsedBlock(model, start, width, height, PrimaryBlocks);
         var maxUsedSecondary = BlockmapRun.GetMaxUsedBlock(model, start, width, height, 1024) - PrimaryBlocks;

         blockAttributes = BlockmapRun.ReadBlockAttributes(maxUsedPrimary, maxUsedSecondary, blockModel1, blockModel2);
         blockEditor?.RefreshBlockAttributeCache(blockAttributes);
      }

      public void HighlightCollision(short[] pixelData, int x, int y) {
         new CanvasPixelViewModel(PixelWidth, PixelHeight, pixelData).DarkenRect(x, y, 16, 16, 8);
      }

      public void HighlightBlock(short[] pixelData, int x, int y) {
         void Transform(int xx, int yy) {
            var p = (y + yy) * PixelWidth + x + xx;
            pixelData[p] = CanvasPixelViewModel.ShiftTowards(pixelData[p], (31, 31, 0), 8); // yellow
         }
         for (int i = 0; i < 15; i++) {
            Transform(i, 0);
            Transform(15 - i, 15);
            Transform(0, 15 - i);
            Transform(15, i);
         }
      }

      public const int BlocksPerRow = 8;

      public ModelArrayElement GetMapModel() => GetMapModel(model, group, map, tokenFactory);
      public static ModelArrayElement GetMapModel(IDataModel model, int group, int map, Func<ModelDelta> tokenFactory) {
         var table = model.GetTable(HardcodeTablesModel.MapBankTable);
         if (table == null) return null;
         var mapBanks = new ModelTable(model, table.Start, tokenFactory);
         if (mapBanks.Count <= group) return null;
         var bank = mapBanks[group]?.GetSubTable("maps");
         if (bank == null) return null;
         if (bank.Count <= map) return null;
         var mapTable = bank[map]?.GetSubTable("map");
         if (mapTable == null) return null;
         return mapTable[0];
      }

      public ModelArrayElement GetLayout(ModelArrayElement map = null) {
         if (map == null) map = GetMapModel();
         if (map == null) return null;
         var layout = map.GetSubTable("layout");
         if (layout == null) return null;
         return layout[0];
      }

      public IReadOnlyList<ConnectionModel> GetConnections() {
         var map = GetMapModel(model, group, this.map, tokenFactory);
         return GetConnections(map, group, this.map);
      }
      public static IReadOnlyList<ConnectionModel> GetConnections(ModelArrayElement map, int bankNum, int mapNum) {
         if (map == null) return null;
         var connectionsAndCountTable = map.GetSubTable("connections");
         var list = new List<ConnectionModel>();
         if (connectionsAndCountTable == null) return list;
         var connectionsAndCount = connectionsAndCountTable[0];
         var count = connectionsAndCount.GetValue("count");
         if (count == 0) return list;
         var connections = connectionsAndCount.GetSubTable("connections");
         if (connections == null) return new ConnectionModel[0];
         for (int i = 0; i < count; i++) list.Add(new(connections[i], bankNum, mapNum));
         return list;
      }

      public IPixelViewModel defaultOverworldSprite;
      public IReadOnlyList<IPixelViewModel> allOverworldSprites;

      public Border GetBorderThickness(ModelArrayElement layout = null) {
         if (!includeBorders) return new(0, 0, 0, 0);
         var connections = GetConnections();
         if (connections == null) return new(0, 0, 0, 0);
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

      public void WritePointerAndSource(ModelDelta token, int source, int destination) {
         model.WritePointer(token, source, destination);
         model.ObserveRunWritten(token, NoInfoRun.FromPointer(model, source));
      }

      public void HandleBorderChanged(object sender, EventArgs e) {
         RequestClearMapCaches.Raise(this);
      }

      public void HandleAutoscrollTiles(object sender, EventArgs e) => AutoscrollTiles.Raise(this);

      public void HandleEventDataMoved(object sender, DataMovedEventArgs e) => InformRepoint(e);

      public static string MapIDToText(IDataModel model, int id) {
         var group = id / 1000;
         var map = id % 1000;
         return MapIDToText(model, group, map);
      }

      public static string MapIDToText(IDataModel model, int group, int map){
         var offset = model.IsFRLG() ? 0x58 : 0;

         var mapBanks = new ModelTable(model, model.GetTable(HardcodeTablesModel.MapBankTable).Start);
         var bank = mapBanks[group].GetSubTable("maps");
         if (bank == null) return $"{group}-{map}";
         if (bank.Count <= map) return $"{group}-{map}";
         var mapTable = bank[map]?.GetSubTable("map");
         if (mapTable == null) return $"{group}-{map}";
         if (!mapTable[0].HasField("regionSectionID")) return $"{group}-{map}";
         var key = mapTable[0].GetValue("regionSectionID") - offset;

         var names = model.GetTableModel(HardcodeTablesModel.MapNameTable);
         var name = names == null ? string.Empty : names[key].GetStringValue("name");
         name = SanitizeName(name);
         if (name.Length == 0) name = "(unnamed)";

         return $"{name}.{group}-{map}";
      }

      #endregion

      /*
         ruby:    data.maps.banks,                       layout<[
                                                            width:: height:: borderblock<[border:|h]4> blockmap<`blm`>
                                                            blockdata1<[isCompressed. isSecondary. padding: tileset<> pal<`ucp4:0123456789ABCDEF`> block<> attributes<> animation<>]1>
                                                            blockdata2<[isCompressed. isSecondary. padding: tileset<> pal<`ucp4:0123456789ABCDEF`> block<> attributes<> animation<>]1>
                                                         events<[e1 e2 e3 e4 ee1<> ee2<> ee3<> ee4<>]1>
                                                         mapscripts<[type. pointer<>]!00>
                                                         connections<>
                                                         music: layoutID: regionSectionID. cave. weather. mapType. padding. escapeRope. flags. battleType.

         firered: data.maps.banks,                       layout<[
                                                            width:: height:: borderblock<>
                                                            blockmap<`blm`>
                                                            blockdata1<[isCompressed. isSecondary. padding: tileset<> pal<`ucp4:0123456789ABCDEF`> block<> animation<> attributes<>]1>
                                                            blockdata2<[isCompressed. isSecondary. padding: tileset<> pal<`ucp4:0123456789ABCDEF`> block<> animation<> attributes<>]1>
                                                            borderwidth. borderheight. unused:]1>
                                                         events<[objectCount. warpCount. scriptCount. signpostCount.
                                                            objects<[id. graphics. kind: x:500 y:500 elevation. moveType. range:|t|x::|y:: trainerType: trainerRangeOrBerryID: script<`xse`> flag: unused:]/objectCount>
                                                            warps<[x:500 y:500 elevation. warpID. map. bank.]/warps>
                                                            scripts<[x:500 y:500 elevation: trigger: index:: script<`xse`>]/scriptCount>
                                                            signposts<[x:500 y:500 elevation. kind. unused: arg::|h]/signposts>]1>
                                                         mapscripts<[type. pointer<>]!00>
                                                         connections<[count:: connections<[direction:: offset:: mapGroup. mapNum. unused:]/count>]>
                                                         music: layoutID: regionSectionID. cave. weather. mapType. allowBiking. flags.|t|allowEscaping.|allowRunning.|showMapName::: floorNum. battleType.

         emerald: data.maps.banks,                       layout<[width:: height:: borderblock<[border:|h]4> blockmap<`blm`>
                                                            blockdata1<[isCompressed. isSecondary. padding: tileset<> pal<`ucp4:0123456789ABCDEF`> block<> attributes<> animation<>]1>
                                                            blockdata2<[isCompressed. isSecondary. padding: tileset<> pal<`ucp4:0123456789ABCDEF`> block<> attributes<> animation<>]1>
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

   public class EventSelector : ViewModelCore {
      public bool isSelected;
      public bool IsSelected { get => isSelected; set => Set(ref isSelected, value); }

      public int index;
      public int Index { get => index; set => Set(ref index, value); }

      public void Select() => IsSelected = true;
   }

   public record Border(int North, int East, int South, int West);

   public record ConnectionInfo(int Size, int Offset, MapDirection Direction) {
      public const string SingleConnectionContent = "direction:: offset:: mapGroup. mapNum. unused:";
      public const string SingleConnectionLength = "/count";
      public static readonly string SingleConnectionFormat = $"[{SingleConnectionContent}]{SingleConnectionLength}";
      public static readonly string ConnectionTableContent = $"count:: connections<{SingleConnectionFormat}>";
      public MapDirection OppositeDirection => Direction.Reverse();
   }

   public record BerryInfo(IDictionary<int, BerrySpot> BerryMap, ObservableCollection<string> BerryOptions);

   public class ConnectionModel {
      public readonly ModelArrayElement connection;
      public readonly int sourceGroup, sourceMap;
      public IDataModel Model => connection.Model;
      public Func<ModelDelta> Tokens => () => connection.Token;
      public ConnectionModel(ModelArrayElement connection, int sourceGroup, int sourceMap) => (this.connection, this.sourceGroup, this.sourceMap) = (connection, sourceGroup, sourceMap);

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

      public int Unused {
         get => connection.GetValue("unused");
         set => connection.SetValue("unused", value);
      }

      public ConnectionModel GetInverse() {
         var direction = Direction.Reverse();
         var map = BlockMapViewModel.GetMapModel(Model, MapGroup, MapNum, Tokens);
         var neighbors = BlockMapViewModel.GetConnections(map, MapGroup, MapNum);
         if (neighbors == null) return null;
         return neighbors.FirstOrDefault(c => c.MapGroup == sourceGroup && c.MapNum == sourceMap && c.Direction == direction);
      }

      public void Clear(IDataModel model, ModelDelta token) {
         token.ChangeData(model, connection.Start, connection.Length.Range(i => (byte)0xFF).ToList());
      }
   }

   public class EventGroupModel {
      public readonly ModelArrayElement events;

      public event EventHandler<DataMovedEventArgs> DataMoved;

      public IReadOnlyList<ObjectEventViewModel> Objects { get; }
      public IReadOnlyList<WarpEventViewModel> Warps { get; }
      public IReadOnlyList<ScriptEventViewModel> Scripts { get; }
      public IReadOnlyList<SignpostEventViewModel> Signposts { get; }
      public IReadOnlyList<FlyEventViewModel> FlyEvents { get; }

      public IEnumerable<IEventViewModel> All => Objects.Concat<IEventViewModel>(Warps).Concat(Scripts).Concat(Signposts).Concat(FlyEvents);

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

   public static class MapDirectionExtensions {
      public static MapDirection Reverse(this MapDirection direction) => direction switch {
         MapDirection.Up => MapDirection.Down,
         MapDirection.Down => MapDirection.Up,
         MapDirection.Left => MapDirection.Right,
         MapDirection.Right => MapDirection.Left,
         MapDirection.Dive => MapDirection.Emerge,
         MapDirection.Emerge => MapDirection.Dive,
         _ => throw new NotImplementedException(),
      };
   }

   public enum ZoomDirection {
      None = 0,
      Shrink = 1,
      Enlarge = 2,
   }
}
