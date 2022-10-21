using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using HexManiac.Core.Models.Runs.Sprites;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;
using static HavenSoft.HexManiac.Core.ViewModels.Map.MapSliderIcons;

namespace HavenSoft.HexManiac.Core.ViewModels.Map {
   public class BlockMapViewModel : ViewModelCore, IPixelViewModel {
      private readonly Format format;
      private readonly IFileSystem fileSystem;
      private readonly IEditableViewPort viewPort;
      private readonly IDataModel model;
      private readonly Func<ModelDelta> tokenFactory;
      private readonly int group, map;

      private int PrimaryTiles { get; } // 640
      private int PrimaryBlocks { get; } // 640
      private int TotalBlocks => 1024;
      private int PrimaryPalettes { get; } // 7

      public IEditableViewPort ViewPort => viewPort;

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
         var events = GetEvents();
         var categories = new List<List<IEventModel>> { new(), new(), new(), new(), new() };
         int selectionIndex = -1, selectedCategory = -1;
         for (int i = 0; i < events.Count; i++) {
            int currentCategory =
               events[i] is ObjectEventModel ? 0 :
               events[i] is WarpEventModel ? 1 :
               events[i] is ScriptEventModel ? 2 :
               events[i] is SignpostEventModel ? 3 :
               events[i] is FlyEventModel ? 4 :
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

      public MapHeaderViewModel Header { get; }

      public bool IsValidMap => GetMapModel() != null;

      #region IPixelViewModel

      private short transparent;
      public short Transparent { get => transparent; private set => Set(ref transparent, value); }

      private int pixelWidth, pixelHeight;
      public int PixelWidth { get => pixelWidth; private set => Set(ref pixelWidth, value); }
      public int PixelHeight { get => pixelHeight; private set => Set(ref pixelHeight, value); }

      private short[] pixelData; // picture of the map
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

      private IPixelViewModel blockPixels; // all the available blocks together in one big image
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
      private byte[][] blockAttributes;
      private readonly List<IPixelViewModel> blockRenders = new(); // one image per block
      private IReadOnlyList<IEventModel> eventRenders;
      public IReadOnlyList<IPixelViewModel> BlockRenders {
         get {
            if (blockRenders.Count == 0) RefreshBlockRenderCache();
            return blockRenders;
         }
      }

      #endregion

      #region Borders

      private bool includeBorders = true;
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

      #region Name

      public string FullName => MapIDToText(model, MapID);
      public string Name => $"({group}-{map})";

      private ObservableCollection<string> availableNames;
      public ObservableCollection<string> AvailableNames {
         get {
            if (availableNames != null) return availableNames;
            availableNames = new();
            foreach (var name in viewPort.Model.GetOptions(HardcodeTablesModel.MapNameTable)) {
               availableNames.Add(name.Trim('"'));
            }
            return availableNames;
         }
      }

      public int SelectedNameIndex {
         get {
            var offset = model.IsFRLG() ? 0x58 : 0;
            var banks = model.GetTableModel(HardcodeTablesModel.MapBankTable);
            var maps = banks[group].GetSubTable("maps");
            var self = maps[map].GetSubTable("map")[0];
            if (!self.HasField("regionSectionID")) return -1;
            return self.GetValue("regionSectionID") - offset;
         }
         set {
            var offset = model.IsFRLG() ? 0x58 : 0;
            var banks = model.GetTableModel(HardcodeTablesModel.MapBankTable, tokenFactory);
            var maps = banks[group].GetSubTable("maps");
            var self = maps[map].GetSubTable("map")[0];
            if (!self.HasField("regionSectionID")) return;
            self.SetValue("regionSectionID", value + offset);
            NotifyPropertyChanged(nameof(FullName));
         }
      }

      #endregion

      #region Wild Data

      private int wildDataIndex = int.MinValue;
      public bool HasWildData {
         get {
            if (wildDataIndex != int.MinValue) return wildDataIndex != -1;
            var wildTable = model.GetTable(HardcodeTablesModel.WildTableName);
            var wildData = new ModelTable(model, wildTable.Start, null, wildTable);
            for (int i = 0; i < wildData.Count; i++) {
               var bank = wildData[i].GetValue("bank");
               var map = wildData[i].GetValue("map");
               var id = bank * 1000 + map;
               if (id != MapID) continue;
               wildDataIndex = i;
               return true;
            }
            wildDataIndex = -1;
            return false;
         }
      }

      private string wildText;
      public string WildText {
         get {
            if (wildText != null) return wildText;
            var wild = model.GetTableModel(HardcodeTablesModel.WildTableName);
            // grass<[rate:: list<>]1> surf<[rate:: list<>]1> tree<[rate:: list<>]1> fish<[rate:: list<>]1>
            var text = new StringBuilder();
            if (wildDataIndex < 0) return text.ToString();
            BuildWildTooltip(text, wild[wildDataIndex], "grass");
            text.AppendLine();
            BuildWildTooltip(text, wild[wildDataIndex], "surf");
            text.AppendLine();
            BuildWildTooltip(text, wild[wildDataIndex], "tree");
            text.AppendLine();
            BuildWildTooltip(text, wild[wildDataIndex], "fish");
            return text.ToString();
         }
      }
      private static void BuildWildTooltip(StringBuilder text, ModelArrayElement wild, string type) {
         // list<[low. high. species:]n>
         var terrain = wild.GetSubTable(type);
         if (terrain == null) return;
         var list = terrain[0].GetSubTable("list");
         if (list == null) return;
         text.Append(type);
         text.Append(": ");
         text.AppendJoin(", ", list.Select(element => element.GetEnumValue("species")).Distinct());
      }

      private StubCommand gotoWildData;
      public ICommand GotoWildData => StubCommand(ref gotoWildData, () => {
         var wildTable = model.GetTable(HardcodeTablesModel.WildTableName);
         if (!HasWildData) {
            var token = tokenFactory();
            var originalStart = wildTable.Start;
            wildTable = model.RelocateForExpansion(token, wildTable, wildTable.Length + wildTable.ElementLength);
            wildTable = wildTable.Append(token, 1);
            model.ObserveRunWritten(token, wildTable);
            var element = new ModelArrayElement(model, wildTable.Start, wildTable.ElementCount - 1, tokenFactory, wildTable);
            element.SetValue("bank", group);
            element.SetValue("map", map);
            element.SetAddress("grass", Pointer.NULL);
            element.SetAddress("surf", Pointer.NULL);
            element.SetAddress("tree", Pointer.NULL);
            element.SetAddress("fish", Pointer.NULL);
            wildDataIndex = wildTable.ElementCount - 1;
            if (wildTable.Start != originalStart) InformRepoint(new("Wild", wildTable.Start));
         }
         viewPort.Goto.Execute(wildTable.Start + wildTable.ElementLength * wildDataIndex);
      }, () => model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, HardcodeTablesModel.WildTableName) != Pointer.NULL);

      #endregion

      public event EventHandler NeighborsChanged;
      public event EventHandler AutoscrollTiles;
      public event EventHandler HideSidePanels;
      public event EventHandler<ChangeMapEventArgs> RequestChangeMap;

      private BlockEditor blockEditor;
      public BlockEditor BlockEditor {
         get {
            if (blockEditor == null) {
               var layout = GetLayout();
               var blockModel1 = new BlocksetModel(model, layout.GetAddress("blockdata1"));
               var blockModel2 = new BlocksetModel(model, layout.GetAddress("blockdata2"));
               if (palettes == null) RefreshPaletteCache(layout, blockModel1, blockModel2);
               if (tiles == null) RefreshTileCache(layout, blockModel1, blockModel2);
               if (blocks == null) RefreshBlockCache(layout, blockModel1, blockModel2);
               if (blockAttributes == null) RefreshBlockAttributeCache(layout, blockModel1, blockModel2);
               blockEditor = new BlockEditor(viewPort.ChangeHistory, model, palettes, tiles, blocks, blockAttributes);
               blockEditor.BlocksChanged += HandleBlocksChanged;
               blockEditor.BlockAttributesChanged += HandleBlockAttributesChanged;
               blockEditor.AutoscrollTiles += HandleAutoscrollTiles;
               blockEditor.Bind(nameof(blockEditor.ShowTiles), (editor, args) => BorderEditor.ShowBorderPanel &= !editor.ShowTiles);
            }
            return blockEditor;
         }
      }

      private BorderEditor borderEditor;
      public BorderEditor BorderEditor {
         get {
            if (borderEditor == null) {
               borderEditor = new BorderEditor(this);
               borderEditor.BorderChanged += HandleBorderChanged;
               borderEditor.Bind(nameof(borderEditor.ShowBorderPanel), (editor, args) => {
                  BlockEditor.ShowTiles &= !editor.ShowBorderPanel;
                  HideSidePanels.Raise(this);
               });
            }
            return borderEditor;
         }
      }

      private MapRepointer mapRepointer;
      public MapRepointer MapRepointer => mapRepointer;

      private MapScriptCollection mapScriptCollection;
      public MapScriptCollection MapScriptCollection {
         get {
            if (mapScriptCollection.Unloaded) {
               var map = GetMapModel();
               mapScriptCollection.Load(map);
            }
            return mapScriptCollection;
         }
      }

      public BlockMapViewModel(IFileSystem fileSystem, IEditableViewPort viewPort, Format format, int group, int map) {
         this.format = format;
         this.fileSystem = fileSystem;
         this.viewPort = viewPort;
         this.model = viewPort.Model;
         this.tokenFactory = () => viewPort.ChangeHistory.CurrentChange;
         (this.group, this.map) = (group, map);
         Transparent = -1;
         var mapModel = GetMapModel();
         Header = new(mapModel, tokenFactory);
         RefreshMapSize();
         PrimaryTiles = PrimaryBlocks = model.IsFRLG() ? 640 : 512;
         PrimaryPalettes = model.IsFRLG() ? 7 : 6;

         (LeftEdge, TopEdge) = (-PixelWidth / 2, -PixelHeight / 2);

         mapScriptCollection = new(viewPort);
         mapScriptCollection.NewMapScriptsCreated += (sender, e) => GetMapModel().SetAddress("mapscripts", e.Address);

         mapRepointer = new MapRepointer(format, fileSystem, model, viewPort.ChangeHistory, MapID);
         mapRepointer.ChangeMap += (sender, e) => RequestChangeMap.Raise(this, e);
         mapRepointer.DataMoved += (sender, e) => {
            ClearCaches();
            InformRepoint(e);
            if (e.Type == "Layout") UpdateLayoutID();
         };
      }

      public void InformRepoint(DataMovedEventArgs e) {
         viewPort.RaiseMessage($"{e.Type} data was moved to {e.Address:X6}.");
      }

      public void InformCreate(DataMovedEventArgs e) {
         viewPort.RaiseMessage($"{e.Type} data was created at {e.Address:X6}.");
      }

      public IReadOnlyList<BlockMapViewModel> GetNeighbors(MapDirection direction) {
         var list = new List<BlockMapViewModel>();
         var border = GetBorderThickness();
         if (border == null) return list;
         foreach (var connection in GetConnections()) {
            if (connection.Direction != direction) continue;
            var vm = GetNeighbor(connection, border);
            list.Add(vm);
         }
         return list;
      }

      public void GotoData() {
         var map = GetMapModel();
         viewPort.Goto.Execute(map.Start);
      }

      public void ClearCaches() {
         palettes = null;
         tiles = null;
         blocks = null;
         blockRenders.Clear();
         blockPixels = null;
         eventRenders = null;
         RefreshMapSize();
         if (blockEditor != null) {
            var oldShowTiles = blockEditor.ShowTiles;
            var selection = blockEditor.BlockIndex;
            blockEditor.BlocksChanged -= HandleBlocksChanged;
            blockEditor.BlockAttributesChanged -= HandleBlockAttributesChanged;
            BlockEditor.AutoscrollTiles -= HandleAutoscrollTiles;
            var oldBlockEditor = blockEditor;
            blockEditor = null;
            BlockEditor.BlockIndex = selection;
            BlockEditor.ShowTiles = oldShowTiles;
            oldBlockEditor.ShowTiles = false;
            NotifyPropertyChanged(nameof(BlockEditor));
         }
         if (borderEditor != null) {
            var oldShowBorder = borderEditor.ShowBorderPanel;
            borderEditor.BorderChanged -= HandleBorderChanged;
            var oldBorderEditor = borderEditor;
            borderEditor = null;
            BorderEditor.ShowBorderPanel = oldShowBorder;
            oldBorderEditor.ShowBorderPanel = false;
            NotifyPropertyChanged(nameof(BorderEditor));
         }
         NotifyPropertyChanged(nameof(BlockRenders));
         NotifyPropertyChanged(nameof(BlockPixels));
      }

      public void RedrawEvents() {
         eventRenders = null;
         pixelData = null;
         NotifyPropertiesChanged(nameof(PixelData), nameof(CanCreateFlyEvent));
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

      private int lastDrawVal, lastDrawX, lastDrawY;

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
         if (lastDrawX == xx && lastDrawY == yy) return;
         var start = layout.GetAddress("blockmap");

         var modelAddress = start + (yy * width + xx) * 2;
         var data = model.ReadMultiByteValue(modelAddress, 2);
         var high = data >> 10;
         var low = data & 0x3FF;
         if (blockIndex >= 0 && blockIndex < blockRenders.Count) low = blockIndex;
         if (collisionIndex >= 0 && collisionIndex < 0x3F) high = collisionIndex;
         lastDrawVal = model.ReadMultiByteValue(modelAddress, 2);
         (lastDrawX, lastDrawY) = (xx, yy);
         model.WriteMultiByteValue(modelAddress, 2, token, (high << 10) | low);

         var canvas = new CanvasPixelViewModel(pixelWidth, pixelHeight, pixelData);
         bool updateBlock = blockIndex >= 0 && blockIndex < blockRenders.Count;
         bool updateHighlight = collisionIndex == collisionHighlight && collisionHighlight != -1;
         (xx, yy) = ((xx + border.West) * 16, (yy + border.North) * 16);
         if (updateBlock) canvas.Draw(blockRenders[blockIndex], xx, yy);
         if (updateHighlight) HighlightCollision(pixelData, xx, yy);
         if (updateBlock || updateHighlight) NotifyPropertyChanged(nameof(PixelData));
      }

      public void DrawBlocks(ModelDelta token, int[,] tiles, Point source, Point destination) {
         while (Math.Abs(destination.X - source.X) % tiles.GetLength(0) != 0) destination -= new Point(1, 0);
         while (Math.Abs(destination.Y - source.Y) % tiles.GetLength(1) != 0) destination -= new Point(0, 1);

         var layout = GetLayout();
         var (width, height) = (layout.GetValue("width"), layout.GetValue("height"));
         var start = layout.GetAddress("blockmap");
         int changeCount = 0;
         for (int x = 0; x < tiles.GetLength(0); x++) {
            for (int y = 0; y < tiles.GetLength(1); y++) {
               if (destination.X + x < 0 || destination.Y + y < 0 || destination.X + x >= width || destination.Y + y >= height) continue;
               var address = start + ((destination.Y + y) * width + destination.X + x) * 2;
               if (model.ReadMultiByteValue(address, 2) != tiles[x, y]) {
                  model.WriteMultiByteValue(address, 2, token, tiles[x, y]);
                  changeCount++;
               }
            }
         }
         if (changeCount > 0) {
            pixelData = null;
            NotifyPropertyChanged(nameof(PixelData));
         }
      }

      public void PaintBlock(ModelDelta token, int blockIndex, int collisionIndex, double x, double y) {
         if (blockIndex == -1) return;
         (x, y) = ((x - leftEdge) / spriteScale, (y - topEdge) / spriteScale);
         (x, y) = (x / 16, y / 16);
         var layout = GetLayout();
         var (width, height) = (layout.GetValue("width"), layout.GetValue("height"));
         var border = GetBorderThickness(layout);
         var (xx, yy) = ((int)x - border.West, (int)y - border.North);
         if (xx < 0 || yy < 0 || xx > width || yy > height) return;
         var start = layout.GetAddress("blockmap");

         var size = new Point(width, height);
         if (collisionIndex < 0) collisionIndex = lastDrawVal >> 10;
         var change = new Point(lastDrawVal, (collisionIndex << 10) | blockIndex);
         PaintBlock(token, new(xx - 1, yy), size, start, change);
         PaintBlock(token, new(xx + 1, yy), size, start, change);
         PaintBlock(token, new(xx, yy - 1), size, start, change);
         PaintBlock(token, new(xx, yy + 1), size, start, change);
         pixelData = null;
         NotifyPropertyChanged(nameof(PixelData));
      }

      private void PaintBlock(ModelDelta token, Point p, Point size, int start, Point change) {
         if (change.X == change.Y) return;
         if (p.X < 0 || p.Y < 0 || p.X >= size.X || p.Y >= size.Y) return;
         var address = start + (p.Y * size.X + p.X) * 2;
         if (model.ReadMultiByteValue(address, 2) != change.X) return;
         model.WriteMultiByteValue(address, 2, token, change.Y);
         PaintBlock(token, p + new Point(-1, 0), size, start, change);
         PaintBlock(token, p + new Point(1, 0), size, start, change);
         PaintBlock(token, p + new Point(0, -1), size, start, change);
         PaintBlock(token, p + new Point(0, 1), size, start, change);
      }

      #endregion

      #region Events

      public void UpdateEventLocation(IEventModel ev, double x, double y) {
         (lastDrawX, lastDrawY) = (-1, -1);
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

      public IEventModel EventUnderCursor(double x, double y) {
         var layout = GetLayout();
         var border = GetBorderThickness(layout);
         var tileX = (int)((x - LeftEdge) / SpriteScale / 16) - border.West;
         var tileY = (int)((y - TopEdge) / SpriteScale / 16) - border.North;
         foreach (var e in GetEvents()) {
            if (e.X == tileX && e.Y == tileY) {
               SelectedEvent = e;
               pixelData = null;
               NotifyPropertyChanged(nameof(PixelData));
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

      #endregion

      #region Connections

      public IEnumerable<MapSlider> GetMapSliders() {
         var connections = GetConnections();
         if (connections == null) yield break;
         var border = GetBorderThickness();
         if (border == null) yield break;
         var tileSize = (int)(16 * spriteScale);
         int id = 0;

         // get sliders for up/down/left/right connections
         var connectionCount = (down: 0, up: 0, left: 0, right: 0);
         foreach (var connection in connections) {
            void Notify() => NeighborsChanged.Raise(this);
            var map = GetNeighbor(connection, border);
            var sourceMapInfo = (group, this.map);

            if (connection.Direction == MapDirection.Up) {
               connectionCount.up++;
               yield return new ConnectionSlider(connection, sourceMapInfo, Notify, id, LeftRight, right: map.LeftEdge, bottom: map.BottomEdge - tileSize);
               yield return new ConnectionSlider(connection, sourceMapInfo, Notify, id + 1, LeftRight, left: map.RightEdge, bottom: map.BottomEdge - tileSize);
            }

            if (connection.Direction == MapDirection.Down) {
               connectionCount.down++;
               yield return new ConnectionSlider(connection, sourceMapInfo, Notify, id, LeftRight, right: map.LeftEdge, top: map.TopEdge + tileSize);
               yield return new ConnectionSlider(connection, sourceMapInfo, Notify, id + 1, LeftRight, left: map.RightEdge, top: map.TopEdge + tileSize);
            }

            if (connection.Direction == MapDirection.Left) {
               connectionCount.left++;
               yield return new ConnectionSlider(connection, sourceMapInfo, Notify, id, UpDown, right: map.RightEdge - tileSize, bottom: map.TopEdge);
               yield return new ConnectionSlider(connection, sourceMapInfo, Notify, id + 1, UpDown, right: map.RightEdge - tileSize, top: map.BottomEdge);
            }

            if (connection.Direction == MapDirection.Right) {
               connectionCount.right++;
               yield return new ConnectionSlider(connection, sourceMapInfo, Notify, id, UpDown, left: map.LeftEdge + tileSize, bottom: map.TopEdge);
               yield return new ConnectionSlider(connection, sourceMapInfo, Notify, id + 1, UpDown, left: map.LeftEdge + tileSize, top: map.BottomEdge);
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

      #endregion

      #region Work Methods

      private void ResizeMapData(MapDirection direction, int amount) {
         if (amount == 0) return;
         var token = tokenFactory();
         var map = GetMapModel();
         var layout = GetLayout(map);
         var run = model.GetNextRun(layout.GetAddress("blockmap")) as BlockmapRun;
         if (run == null) return;

         var newRun = run.TryChangeSize(tokenFactory, direction, amount);
         if (newRun != null) {
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
            foreach (var e in GetEvents()) {
               if (direction == MapDirection.Left) {
                  e.X += amount;
               } else if (direction == MapDirection.Up) {
                  e.Y += amount;
               }
            }
            RefreshMapSize();
            NeighborsChanged.Raise(this);
            if (newRun.Start != run.Start) InformRepoint(new("Map", newRun.Start));
         }
      }

      private void ConnectNewMap(object obj) {
         var token = tokenFactory();
         var mapBanks = new ModelTable(model, model.GetTable(HardcodeTablesModel.MapBankTable).Start, tokenFactory);
         var option = MapRepointer.GetMapBankForNewMap("Which map group do you want to add the new map to?");
         if (option == -1) return;

         var info = (ConnectionInfo)obj;
         var map = GetMapModel();
         var connectionsAndCount = map.GetSubTable("connections")[0];
         var connections = connectionsAndCount.GetSubTable("connections").Run;
         var originalConnectionStart = connections.Start;
         connections = model.RelocateForExpansion(token, connections, connections.Length + connections.ElementLength);
         if (connections.Start != originalConnectionStart) InformRepoint(new("Connections", connections.Start));
         connectionsAndCount.SetValue("count", connections.ElementCount + 1);
         var table = new ModelTable(model, connections.Start, tokenFactory, connections);
         var newConnection = new ConnectionModel(table[connections.ElementCount]);
         newConnection.Offset = info.Offset;
         newConnection.Direction = info.Direction;

         newConnection.MapGroup = option;

         var mapTable = MapRepointer.AddNewMapToBank(option);

         newConnection.MapNum = mapTable.ElementCount - 1;
         var address = MapRepointer.CreateNewMap(token);
         var layoutStart = MapRepointer.CreateNewLayout(token);
         WritePointerAndSource(token, layoutStart + 12, MapRepointer.CreateNewBlockMap(token, info.Size, info.Size));
         WritePointerAndSource(token, address + 0, layoutStart);

         model.UpdateArrayPointer(token, null, null, -1, mapTable.Start + mapTable.Length - 4, address);

         var otherMap = new BlockMapViewModel(fileSystem, viewPort, format, newConnection.MapGroup, newConnection.MapNum) { allOverworldSprites = allOverworldSprites };
         otherMap.UpdateLayoutID();
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
         var mapBanks = new ModelTable(model, table.Start, tokenFactory);
         for (int group = 0; group < mapBanks.Count; group++) {
            var bank = mapBanks[group];
            var maps = bank.GetSubTable("maps");
            for (int map = 0; map < maps.Count; map++) {
               var mapVM = new BlockMapViewModel(fileSystem, viewPort, format, group, map) { allOverworldSprites = allOverworldSprites };
               var newInfo = mapVM.CanConnect(info.OppositeDirection);
               if (newInfo != null) options[mapVM.MapID] = newInfo;
            }
         }

         // select which map to add
         var keys = options.Keys.ToList();
         var enumViewModel = new EnumViewModel(keys.Select(key => MapIDToText(model, key)).ToArray());

         var option = fileSystem.ShowOptions(
            "Pick a group",
            "Which map do you want to connect to?",
            new[] { new[] { enumViewModel } },
            new VisualOption { Index = 1, Option = "OK", ShortDescription = "Connect Existing Map" });
         if (option == -1) return;
         var choice = keys[enumViewModel.Choice];

         var newConnection = AddConnection(info);
         newConnection.Offset = info.Offset;
         newConnection.Direction = info.Direction;
         newConnection.MapGroup = choice / 1000;
         newConnection.MapNum = choice % 1000;

         var otherMap = new BlockMapViewModel(fileSystem, viewPort, format, newConnection.MapGroup, newConnection.MapNum) { allOverworldSprites = allOverworldSprites };
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
         var map = GetMapModel();
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
         var map = GetMapModel();
         var connectionsAndCountTable = map.GetSubTable("connections");
         if (connectionsAndCountTable == null) {
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
            connections = connectionsAndCount.GetSubTable("connections").Run;
         }
         var count = connections.ElementCount;
         connections = connections.Append(token, 1);
         model.ObserveRunWritten(token, connections);

         var table = new ModelTable(model, connections.Start, tokenFactory, connections);
         var newConnection = new ConnectionModel(table[count]);
         token.ChangeData(model, table[count].Start, new byte[12]);
         newConnection.Direction = info.Direction;
         return newConnection;
      }

      public ObjectEventModel CreateObjectEvent(int graphics, int scriptAddress) {
         var token = tokenFactory();
         var map = GetMapModel();
         var events = map.GetSubTable("events")[0];
         var element = AddEvent(events, tokenFactory, "objectCount", "objects");
         if (allOverworldSprites == null) allOverworldSprites = RenderOWs(model);
         var newEvent = new ObjectEventModel(GotoAddress, element, allOverworldSprites) {
            X = 0, Y = 0,
            Elevation = 0,
            ObjectID = element.Table.ElementCount,
            ScriptAddress = scriptAddress,
            Graphics = graphics,
            RangeX = 0,
            RangeY = 0,
            Flag = 0,
            MoveType = 0,
            TrainerType = 0,
            TrainerRangeOrBerryID = 0,
         };
         newEvent.ClearUnused();
         SelectedEvent = newEvent;
         return newEvent;
      }

      public WarpEventModel CreateWarpEvent(int bank, int map) {
         var mapModel = GetMapModel();
         var events = mapModel.GetSubTable("events")[0];
         var element = AddEvent(events, tokenFactory, "warpCount", "warps");
         var newEvent = new WarpEventModel(element) { X = 0, Y = 0, Elevation = 0, Bank = bank, Map = map, WarpID = 0 };
         SelectedEvent = newEvent;
         return newEvent;
      }

      public ScriptEventModel CreateScriptEvent() {
         var map = GetMapModel();
         var events = map.GetSubTable("events")[0];
         var element = AddEvent(events, tokenFactory, "scriptCount", "scripts");
         var newEvent = new ScriptEventModel(GotoAddress, element) { X = 0, Y = 0, Elevation = 0, Index = 0, Trigger = 0, ScriptAddress = Pointer.NULL };
         SelectedEvent = newEvent;
         return newEvent;
      }

      public SignpostEventModel CreateSignpostEvent() {
         var map = GetMapModel();
         var events = map.GetSubTable("events")[0];
         var element = AddEvent(events, tokenFactory, "signpostCount", "signposts");
         var newEvent = new SignpostEventModel(element) { X = 0, Y = 0, Elevation = 0, Kind = 0, Arg = "0" };
         SelectedEvent = newEvent;
         return newEvent;
      }

      public bool CanCreateFlyEvent {
         get {
            var map = GetMapModel();
            var region = map.GetValue(Format.RegionSection);
            if (model.IsFRLG()) region -= 88;
            var connections = model.GetTableModel(HardcodeTablesModel.FlyConnections);
            if (region < 0 || region >= connections.Count) return false;
            return connections[region].GetValue("flight") == 0;
         }
      }

      public FlyEventModel CreateFlyEvent() {
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
            var newSpawns = spawns.Run.Append(tokenFactory(), 1);
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
         return new FlyEventModel(model, group, this.map, tokenFactory);
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
         model.ObserveRunWritten(token, newRun);
         if (newRun.Start != elementTable.Start) InformRepoint(new(fieldName, newRun.Start));
         return new ModelArrayElement(model, newRun.Start, newRun.ElementCount - 1, tokenFactory, newRun);
      }

      private void Erase(ITableRun table, ModelDelta token) {
         foreach (var source in table.PointerSources) {
            model.ClearPointer(token, source, table.Start);
            model.WritePointer(token, source, Pointer.NULL);
         }
         model.ClearData(token, table.Start, table.Length);
      }

      private void UpdateLayoutID() {
         // step 1: test if we need to update the layout id
         var layoutTable = model.GetTable(HardcodeTablesModel.MapLayoutTable);
         var map = GetMapModel();
         var layoutID = map.GetValue("layoutID") - 1;
         var addressFromMap = map.GetAddress("layout");
         var addressFromTable = model.ReadPointer(layoutTable.Start + layoutTable.ElementLength * layoutID);
         if (addressFromMap == addressFromTable) return;

         var matches = layoutTable.ElementCount.Range().Where(i => model.ReadPointer(layoutTable.Start + layoutTable.ElementLength * i) == addressFromMap).ToList();
         var token = tokenFactory();
         if (matches.Count == 0) {
            var originalLayoutTableStart = layoutTable.Start;
            layoutTable = model.RelocateForExpansion(token, layoutTable, layoutTable.Length + 4);
            layoutTable = layoutTable.Append(token, 1);
            model.ObserveRunWritten(token, layoutTable);
            model.UpdateArrayPointer(token, layoutTable.ElementContent[0], layoutTable.ElementContent, -1, layoutTable.Start + layoutTable.ElementLength * (layoutTable.ElementCount - 1), addressFromMap);
            if (originalLayoutTableStart != layoutTable.Start) InformRepoint(new("Layout Table", layoutTable.Start));
            matches.Add(layoutTable.ElementCount - 1);
         }
         map.SetValue("layoutID", matches[0] + 1);
      }

      #endregion

      #region Helper Methods

      private (int width, int height) GetBlockSize(ModelArrayElement layout = null) {
         var border = GetBorderThickness(layout);
         return (pixelWidth / 16 - border.West - border.East, pixelHeight / 16 - border.North - border.South);
      }

      private BlockMapViewModel GetNeighbor(ConnectionModel connection, Border border) {
         var vm = new BlockMapViewModel(fileSystem, viewPort, format, connection.MapGroup, connection.MapNum) {
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
            if (blockModel1 == null) blockModel1 = new BlocksetModel(model, layout.GetAddress(Format.PrimaryBlockset));
            if (blockModel2 == null) blockModel2 = new BlocksetModel(model, layout.GetAddress(Format.SecondaryBlockset));
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

      private void RefreshBlockAttributeCache(ModelArrayElement layout = null, BlocksetModel blockModel1 = null, BlocksetModel blockModel2 = null) {
         if (blockModel1 == null || blockModel2 == null) {
            if (layout == null) layout = GetLayout();
            if (blockModel1 == null) blockModel1 = new BlocksetModel(model, layout.GetAddress("blockdata1"));
            if (blockModel2 == null) blockModel2 = new BlocksetModel(model, layout.GetAddress("blockdata2"));
         }
         blockAttributes = BlockmapRun.ReadBlockAttributes(blockModel1, blockModel2);
      }

      private void RefreshBlockRenderCache(ModelArrayElement layout = null, BlocksetModel blockModel1 = null, BlocksetModel blockModel2 = null) {
         if (blocks == null || tiles == null || palettes == null) {
            if (layout == null) layout = GetLayout();
            if (blockModel1 == null) blockModel1 = new BlocksetModel(model, layout.GetAddress(Format.PrimaryBlockset));
            if (blockModel2 == null) blockModel2 = new BlocksetModel(model, layout.GetAddress(Format.SecondaryBlockset));
         }
         if (blocks == null) RefreshBlockCache(layout, blockModel1, blockModel2);
         if (tiles == null) RefreshTileCache(layout, blockModel1, blockModel2);
         if (palettes == null) RefreshPaletteCache(layout, blockModel1, blockModel2);

         blockRenders.Clear();
         blockRenders.AddRange(BlockmapRun.CalculateBlockRenders(blocks, tiles, palettes));
      }

      private void RefreshMapSize() {
         var layout = GetLayout();
         if (layout == null) return;
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
         if (layout == null) return;
         if (blockRenders.Count == 0) RefreshBlockRenderCache(layout);
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
               if (blockRenders.Count > data) canvas.Draw(blockRenders[data], x * 16, y * 16);
               if (collision == collisionHighlight) HighlightCollision(canvas.PixelData, x * 16, y * 16);
            }
         }

         // draw the box for the selected event
         if (selectedEvent != null && selectedEvent.X >= 0 && selectedEvent.X < width && selectedEvent.Y >= 0 && SelectedEvent.Y < height) {
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
         if (blockRenders.Count == 0) RefreshBlockRenderCache(layout);

         var blockHeight = (int)Math.Ceiling((double)blockRenders.Count / BlocksPerRow);
         var canvas = new CanvasPixelViewModel(BlocksPerRow * 16, blockHeight * 16) { SpriteScale = 2 };

         for (int y = 0; y < blockHeight; y++) {
            for (int x = 0; x < BlocksPerRow; x++) {
               if (blockRenders.Count <= y * BlocksPerRow + x) break;
               canvas.Draw(blockRenders[y * BlocksPerRow + x], x * 16, y * 16);
            }
         }

         blockPixels = canvas;
      }

      private void RefreshBorderRender(ModelArrayElement layout = null) {
         if (layout == null) layout = GetLayout();
         if (blockRenders.Count == 0) RefreshBlockRenderCache(layout);
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

      private ModelArrayElement GetMapModel() => GetMapModel(model, group, map, tokenFactory);
      public static ModelArrayElement GetMapModel(IDataModel model, int group, int map, Func<ModelDelta> tokenFactory) {
         var table = model.GetTable(HardcodeTablesModel.MapBankTable);
         if (table == null) return null;
         var mapBanks = new ModelTable(model, table.Start, tokenFactory);
         var bank = mapBanks[group].GetSubTable("maps");
         if (bank == null) return null;
         var mapTable = bank[map].GetSubTable("map");
         if (mapTable == null) return null;
         return mapTable[0];
      }

      public ModelArrayElement GetLayout(ModelArrayElement map = null) {
         if (map == null) map = GetMapModel();
         if (map == null) return null;
         return map.GetSubTable("layout")[0];
      }

      private IReadOnlyList<ConnectionModel> GetConnections() {
         var map = GetMapModel(model, group, this.map, tokenFactory);
         return GetConnections(map);
      }
      public static IReadOnlyList<ConnectionModel> GetConnections(ModelArrayElement map) {
         if (map == null) return null;
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

      private IReadOnlyList<IPixelViewModel> allOverworldSprites;
      public IReadOnlyList<IPixelViewModel> AllOverworldSprites {
         get {
            if (allOverworldSprites == null) allOverworldSprites = RenderOWs(model);
            return allOverworldSprites;
         }
      }
      public static List<IPixelViewModel> RenderOWs(IDataModel model) {
         var list = new List<IPixelViewModel>();
         var run = model.GetTable(HardcodeTablesModel.OverworldSprites);
         var ows = new ModelTable(model, run.Start, null, run);
         for (int i = 0; i < ows.Count; i++) {
            list.Add(ObjectEventModel.Render(model, ows, i, 0));
         }
         return list;
      }

      private IReadOnlyList<IEventModel> GetEvents() {
         if (allOverworldSprites == null) allOverworldSprites = RenderOWs(model);
         var map = GetMapModel();
         var results = new List<IEventModel>();
         var events = new EventGroupModel(GotoAddress, map.GetSubTable("events")[0], allOverworldSprites, group, this.map);
         events.DataMoved += HandleEventDataMoved;
         results.AddRange(events.Objects);
         results.AddRange(events.Warps);
         results.AddRange(events.Scripts);
         results.AddRange(events.Signposts);
         if (events.FlyEvent != null) results.Add(events.FlyEvent);
         return results;
      }

      public Border GetBorderThickness(ModelArrayElement layout = null) {
         if (!includeBorders) return new(0, 0, 0, 0);
         var connections = GetConnections();
         if (connections == null) return null;
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
               var map = new BlockMapViewModel(fileSystem, viewPort, format, connections[i].MapGroup, connections[i].MapNum) { allOverworldSprites = allOverworldSprites };
               var removeWidth = map.pixelWidth / 16;
               var removeOffset = connections[i].Offset;
               foreach (int j in removeWidth.Range()) availableSpace.Remove(j + removeOffset);
            } else if (direction == MapDirection.Left || direction == MapDirection.Right) {
               var map = new BlockMapViewModel(fileSystem, viewPort, format, connections[i].MapGroup, connections[i].MapNum) { allOverworldSprites = allOverworldSprites };
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

      private void WritePointerAndSource(ModelDelta token, int source, int destination) {
         model.WritePointer(token, source, destination);
         model.ObserveRunWritten(token, NoInfoRun.FromPointer(model, source));
      }

      private void GotoAddress(int address) => viewPort.Goto.Execute(address);

      private void HandleBlocksChanged(object sender, byte[][] blocks) {
         var layout = GetLayout();
         var blockModel1 = new BlocksetModel(model, layout.GetAddress("blockdata1"));
         var blockModel2 = new BlocksetModel(model, layout.GetAddress("blockdata2"));
         BlockmapRun.WriteBlocks(tokenFactory(), blockModel1, blockModel2, blocks);
         this.blocks = null;
         blockRenders.Clear();
         blockPixels = null;
         pixelData = null;
         NotifyPropertiesChanged(nameof(BlockPixels), nameof(PixelData), nameof(BlockRenders));
      }

      private void HandleBorderChanged(object sender, EventArgs e) {
         blocks = null;
         blockRenders.Clear();
         blockPixels = null;
         pixelData = null;
         borderBlock = null;
         NotifyPropertiesChanged(nameof(BlockPixels), nameof(PixelData), nameof(BlockRenders), nameof(BorderBlock));
      }

      private void HandleBlockAttributesChanged(object sender, byte[][] attributes) {
         var layout = GetLayout();
         var blockModel1 = new BlocksetModel(model, layout.GetAddress("blockdata1"));
         var blockModel2 = new BlocksetModel(model, layout.GetAddress("blockdata2"));
         BlockmapRun.WriteBlockAttributes(tokenFactory(), blockModel1, blockModel2, attributes);
      }

      private void HandleAutoscrollTiles(object sender, EventArgs e) => AutoscrollTiles.Raise(this);

      private void HandleEventDataMoved(object sender, DataMovedEventArgs e) => InformRepoint(e);

      public static string MapIDToText(IDataModel model, int id) {
         var group = id / 1000;
         var map = id % 1000;
         var offset = model.IsFRLG() ? 0x58 : 0;

         var mapBanks = new ModelTable(model, model.GetTable(HardcodeTablesModel.MapBankTable).Start);
         var bank = mapBanks[group].GetSubTable("maps");
         if (bank == null) return $"{group}-{map}";
         var mapTable = bank[map]?.GetSubTable("map");
         if (mapTable == null) return $"{group}-{map}";
         if (!mapTable[0].HasField("regionSectionID")) return $"{group}-{map}";
         var key = mapTable[0].GetValue("regionSectionID") - offset;

         var names = new ModelTable(model, model.GetTable(HardcodeTablesModel.MapNameTable).Start);
         var name = names[key].GetStringValue("name");

         return $"{group}-{map} ({name})";
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
                                                            objects<[id. graphics. unused: x:500 y:500 elevation. moveType. range:|t|x::|y:: trainerType: trainerRangeOrBerryID: script<`xse`> flag: unused:]/objectCount>
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

   public record Border(int North, int East, int South, int West);

   public class Format {
      public static string RegionSection => "regionSectionID";
      public static string Layout => "layout";
      public static string Warps => "warps";
      public static string Objects => "objects";
      public static string Connections => "connections";
      public static string Scripts => "scripts";
      public static string Signposts => "signposts";
      public static string ObjectCount => "objectCount";
      public static string WarpCount => "warpCount";
      public static string ScriptCount => "scriptCount";
      public static string SignpostCount => "signpostCount";
      public static string BorderBlock => "borderblock";
      public static string BlockMap => "blockmap";
      public static string PrimaryBlockset => "blockdata1";
      public static string SecondaryBlockset => "blockdata2";
      public static string Tileset => "tileset";
      public static string BlockAttributes => "attributes";
      public static string Blocks => "block";
      public static string TileAnimationRoutine => "animation";
      public static string Palette => "pal";
      public static string BorderWidth => "borderwidth";
      public static string BorderHeight => "borderheight";

      public string BlockDataFormat { get; }
      public string LayoutFormat { get; }
      public string ObjectsFormat { get; }
      public string WarpsFormat { get; }
      public string ScriptsFormat { get; }
      public string SignpostsFormat { get; }
      public string EventsFormat { get; }
      public string ConnectionsFormat { get; }
      public string HeaderFormat { get; }
      public string MapFormat { get; }

      public Format(bool isRSE) {
         BlockDataFormat = $"[isCompressed. isSecondary. padding: {Tileset}<> {Palette}<`ucp4:0123456789ABCDEF`> {Blocks}<> {TileAnimationRoutine}<> {BlockAttributes}<>]1";
         if (isRSE) BlockDataFormat = $"[isCompressed. isSecondary. padding: {Tileset}<> {Palette}<`ucp4:0123456789ABCDEF`> {Blocks}<> {BlockAttributes}<> {TileAnimationRoutine}<>]1";
         LayoutFormat = $"[width:: height:: {BorderBlock}<> {BlockMap}<`blm`> {PrimaryBlockset}<{BlockDataFormat}> {SecondaryBlockset}<{BlockDataFormat}> {BorderWidth}. {BorderHeight}. unused:]1";
         if (isRSE) LayoutFormat = $"[width:: height:: {BorderBlock}<> {BlockMap}<`blm`> {PrimaryBlockset}<{BlockDataFormat}> {PrimaryBlockset}<{BlockDataFormat}>]1";
         ObjectsFormat = $"[id. graphics. unused: x:500 y:500 elevation. moveType. range:|t|x::|y:: trainerType: trainerRangeOrBerryID: script<`xse`> flag: unused:]/{ObjectCount}";
         WarpsFormat = $"[x:500 y:500 elevation. warpID. map. bank.]/{WarpCount}";
         ScriptsFormat = $"[x:500 y:500 elevation: trigger: index:: script<`xse`>]/{ScriptCount}";
         SignpostsFormat = $"[x:500 y:500 elevation. kind. unused: arg::|h]/{SignpostCount}";
         EventsFormat = $"[{ObjectCount}. {WarpCount}. {ScriptCount}. {SignpostCount}. {Objects}<{ObjectsFormat}> {Warps}<{WarpsFormat}> {Scripts}<{ScriptsFormat}> {Signposts}<{SignpostsFormat}>]1";
         ConnectionsFormat = "[count:: connections<[direction:: offset:: mapGroup. mapNum. unused:]/count>]1";
         HeaderFormat = "music: layoutID: regionSectionID. cave. weather. mapType. allowBiking. flags.|t|allowEscaping.|allowRunning.|showMapName::: floorNum. battleType.";
         MapFormat = $"[{Layout}<{LayoutFormat}> events<{EventsFormat}> mapscripts<[type. pointer<>]!00> {Connections}<{ConnectionsFormat}> {HeaderFormat}]";
      }
   }

   public record ConnectionInfo(int Size, int Offset, MapDirection Direction) {
      public const string SingleConnectionContent = "direction:: offset:: mapGroup. mapNum. unused:";
      public const string SingleConnectionLength = "/count";
      public static readonly string SingleConnectionFormat = $"[{SingleConnectionContent}]{SingleConnectionLength}";
      public static readonly string ConnectionTableContent = $"count:: connections<{SingleConnectionFormat}>";
      public MapDirection OppositeDirection => Direction.Reverse();
   }

   public class MapHeaderViewModel : ViewModelCore, INotifyPropertyChanged {
      private ModelArrayElement map;
      private readonly Func<ModelDelta> tokenFactory;
      // music: layoutID: regionSectionID. cave. weather. mapType. allowBiking. flags.|t|allowEscaping.|allowRunning.|showMapName::: floorNum. battleType.

      public MapHeaderViewModel(ModelArrayElement element, Func<ModelDelta> tokens) {
         (map, tokenFactory) = (element, tokens);
         if (element.Model.TryGetList("songnames", out var songnames)) {
            foreach (var name in songnames) MusicOptions.Add(name);
         }
         if (element.Model.TryGetList("maptypes", out var mapTypes)) {
            foreach (var name in mapTypes) MapTypeOptions.Add(name);
         }
      }

      // flags.|t|allowBiking.|allowEscaping.|allowRunning.|showMapName.
      public int Music { get => GetValue(); set => SetValue(value); }
      public int LayoutID { get => GetValue(); set => SetValue(value); }
      public int RegionSectionID { get => GetValue(); set => SetValue(value); }
      public int Cave { get => GetValue(); set => SetValue(value); }
      public int Weather { get => GetValue(); set => SetValue(value); }
      public int MapType { get => GetValue(); set => SetValue(value); }
      public bool AllowBiking { get => GetBool(); set => SetBool(value); }
      public bool AllowEscaping { get => GetBool(); set => SetBool(value); }
      public bool AllowRunning { get => GetBool(); set => SetBool(value); }
      public bool ShowMapName { get => GetBool(); set => SetBool(value); }
      public int FloorNum { get => GetValue(); set => SetValue(value); }
      public int BattleType { get => GetValue(); set => SetValue(value); }

      public bool ShowFloorNumField => map.HasField("floorNum");                // FR/LG only
      public bool ShowAllowBikingField => map.HasField("allowBiking") || (map.HasField("flags") && map.GetTuple("flags").HasField("allowBiking"));       // not for R/S

      public bool HasMusicOptions => MusicOptions.Count > 0;
      public ObservableCollection<string> MusicOptions { get; } = new();

      public bool HasMapTypeOptions => MapTypeOptions.Count > 0;
      public ObservableCollection<string> MapTypeOptions { get; } = new();

      private int GetValue([CallerMemberName]string name = null) {
         name = char.ToLower(name[0]) + name.Substring(1);
         if (!map.HasField(name)) return -1;
         return map.GetValue(name);
      }

      // when we call SetValue, get the latest token
      private void SetValue(int value, [CallerMemberName]string name = null) {
         if (value == GetValue(name)) return;
         map = new(map.Model, map.Table.Start, (map.Start - map.Table.Start) / map.Table.ElementCount, tokenFactory, map.Table);
         var originalName = name;
         name = char.ToLower(name[0]) + name.Substring(1);
         map.SetValue(name, value);
         NotifyPropertyChanged(originalName);
      }

      private bool GetBool([CallerMemberName]string name = null) {
         name = char.ToLower(name[0]) + name.Substring(1);
         if (map.HasField(name)) {
            return map.GetValue(name) != 0;
         } else if (map.HasField("flags")) {
            var tuple = map.GetTuple("flags");
            if (!tuple.HasField(name)) return false;
            return tuple.GetValue(name) != 0;
         }

         return false;
      }

      private void SetBool(bool value, [CallerMemberName]string name = null) {
         var originalName = name;
         name = char.ToLower(name[0]) + name.Substring(1);
         if (map.HasField(name)) {
            map.SetValue(name, value ? 1 : 0);
            NotifyPropertyChanged(originalName);
         } else if (map.HasField("flags")) {
            var tuple = map.GetTuple("flags");
            if (tuple.HasField(name)) {
               tuple.SetValue(name, value ? 1 : 0);
               NotifyPropertyChanged(originalName);
            }
         }
      }
   }

   public class ConnectionModel {
      private readonly ModelArrayElement connection;
      public IDataModel Model => connection.Model;
      public Func<ModelDelta> Tokens => () => connection.Token;
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

      public event EventHandler<DataMovedEventArgs> DataMoved;

      public EventGroupModel(Action<int> gotoAddress, ModelArrayElement events, IReadOnlyList<IPixelViewModel> ows, int bank, int map) {
         this.events = events;

         var objectCount = events.GetValue("objectCount");
         var objects = events.GetSubTable("objects");
         var objectList = new List<ObjectEventModel>();
         if (objects != null) {
            for (int i = 0; i < objectCount; i++) {
               var newEvent = new ObjectEventModel(gotoAddress, objects[i], ows);
               newEvent.DataMoved += (sender, e) => DataMoved.Raise(this, e);
               objectList.Add(newEvent);
            }
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
            for (int i = 0; i < scriptCount; i++) scriptList.Add(new ScriptEventModel(gotoAddress, scripts[i]));
         }
         Scripts = scriptList;

         var signpostCount = events.GetValue("signpostCount");
         var signposts = events.GetSubTable("signposts");
         var signpostList = new List<SignpostEventModel>();
         if (signposts != null) {
            for (int i = 0; i < signpostCount; i++) {
               var newEvent = new SignpostEventModel(signposts[i]);
               newEvent.DataMoved += (sender, e) => DataMoved.Raise(this, e);
               signpostList.Add(newEvent);
            }
         }
         Signposts = signpostList;

         var flyEvent = new FlyEventModel(events.Model, bank, map, () => events.Token);
         if (flyEvent.Valid) {
            FlyEvent = flyEvent;
         }
      }

      public IReadOnlyList<ObjectEventModel> Objects { get; }
      public IReadOnlyList<WarpEventModel> Warps { get; }
      public IReadOnlyList<ScriptEventModel> Scripts { get; }
      public IReadOnlyList<SignpostEventModel> Signposts { get; }
      public FlyEventModel FlyEvent { get; }

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
