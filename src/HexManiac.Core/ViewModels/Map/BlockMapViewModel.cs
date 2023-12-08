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
      private readonly Format format;
      private readonly IFileSystem fileSystem;
      private readonly MapTutorialsViewModel tutorials;
      private readonly IEditableViewPort viewPort;
      private readonly IDataModel model;
      private readonly EventTemplate eventTemplate;
      private readonly Func<ModelDelta> tokenFactory;
      private readonly int group, map;

      private int PrimaryTiles { get; }
      private int PrimaryBlocks { get; }
      private int TotalBlocks => 1024;
      private int PrimaryPalettes { get; } // 7

      private int zIndex;
      public int ZIndex { get => zIndex; set => Set(ref zIndex, value); }

      public event EventHandler RequestClearMapCaches;

      public IEditableViewPort ViewPort => viewPort;

      #region SelectedEvent

      private IEventViewModel selectedEvent;
      public IEventViewModel SelectedEvent {
         get => selectedEvent;
         set {
            var oldValue = selectedEvent;
            selectedEvent = value;
            NotifyPropertyChanged();
            HandleSelectedEventChanged(oldValue);
         }
      }

      public ObservableCollection<EventSelector> EventSelectors { get; } = new();

      private void HandleSelectedEventChanged(IEventViewModel old) {
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

         EventSelectors.Clear();
         if (selectedEvent != null) {
            var parts = selectedEvent.EventIndex.Split("/");
            var index = int.Parse(parts[0]) - 1;
            var count = int.Parse(parts[1]);
            for (int i = 0; i < count; i++) {
               EventSelector selector = new() { IsSelected = i == index, Index = i };
               selector.Bind(nameof(selector.IsSelected), (sender, e) => {
                  var events = GetEvents().Where(ev => ev.GetType() == selectedEvent.GetType()).ToList();
                  SelectedEvent = events[sender.Index];
               });
               EventSelectors.Add(selector);
            }
         }
      }

      private void RefreshFromEventChange(object sender, EventArgs e) => RedrawEvents();

      private void CycleActiveEvent(object sender, EventCycleDirection direction) {
         // organize events into categories
         var events = GetEvents();
         var categories = new List<List<IEventViewModel>> { new(), new(), new(), new(), new() };
         int selectionIndex = -1, selectedCategory = -1;
         for (int i = 0; i < events.Count; i++) {
            int currentCategory =
               events[i] is ObjectEventViewModel ? 0 :
               events[i] is WarpEventViewModel ? 1 :
               events[i] is ScriptEventViewModel ? 2 :
               events[i] is SignpostEventViewModel ? 3 :
               events[i] is FlyEventViewModel ? 4 :
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
         } else if (direction == EventCycleDirection.None) {
            // we just wanted to regenerate the event object
         } else {
            throw new NotImplementedException();
         }
         if (selectedCategory < 0) {
            SelectedEvent = null;
            return;
         }
         selectedCategory %= categories.Count;
         selectionIndex %= categories[selectedCategory].Count;

         // update selection
         SelectedEvent = categories[selectedCategory][selectionIndex];
         tutorials.Complete(Tutorial.EventButtons_CycleEvent);
      }

      #endregion

      private static int MapSizeLimit => 0x2800; // (x+15)*(y+14) must be less that 0x2800 (5*2048). This can lead to limits like 113x66 or 497x6
      public static bool IsMapWithinSizeLimit(int width, int height) => (width + 15) * (height + 14) <= MapSizeLimit;

      public int MapID => group * 1000 + map;

      private MapHeaderViewModel header;
      public MapHeaderViewModel Header {
         get {
            if (header == null) {
               header = new MapHeaderViewModel(GetMapModel(), format, tokenFactory);
               header.Bind(nameof(Header.PrimaryIndex), (sender, e) => ClearCaches());
               header.Bind(nameof(Header.SecondaryIndex), (sender, e) => ClearCaches());
            }
            return header;
         }
      }

      public bool IsValidMap => GetMapModel() != null;

      #region IPixelViewModel

      private short transparent;
      public short Transparent { get => transparent; private set => Set(ref transparent, value); }

      private int pixelWidth, pixelHeight;
      public int PixelWidth { get => pixelWidth; private set => Set(ref pixelWidth, value); }
      public int PixelHeight { get => pixelHeight; private set => Set(ref pixelHeight, value); }

      private readonly object pixelWriteLock = new();
      private short[] pixelData; // picture of the map
      public short[] PixelData {
         get {
            lock (pixelWriteLock) {
               if (pixelData == null) FillMapPixelData();
               return pixelData;
            }
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

      #region IsSelected

      private bool isSelected;
      public bool IsSelected {
         get => isSelected;
         set => Set(ref isSelected, value, old => ClearPixelCache());
      }

      #endregion

      #region Position

      private int topEdge, leftEdge;
      public int TopEdge { get => topEdge; set => Set(ref topEdge, value); }
      public int LeftEdge { get => leftEdge; set => Set(ref leftEdge, value); }

      public int BottomEdge => topEdge + (int)(PixelHeight * SpriteScale);
      public int RightEdge => leftEdge + (int)(PixelWidth * SpriteScale);

      private ImageLocation hoverPoint = new(0, 0);
      public ImageLocation HoverPoint {
         get => hoverPoint;
         set {
            hoverPoint = value;
            NotifyPropertyChanged();
         }
      }

      public double WidthRatio => 80.0 / PixelWidth / Math.Min(1, SpriteScale);
      public double HeightRatio => 80.0 / PixelHeight / Math.Min(1, SpriteScale);
      private bool showBeneath;
      public bool ShowBeneath { get => showBeneath; set => Set(ref showBeneath, value, old => {
         NotifyPropertiesChanged(nameof(WidthRatio), nameof(HeightRatio));
         tutorials.Complete(Tutorial.SpaceBar_ShowBeneath);
      } ); }

      private MapDisplayOptions showEvents;
      public MapDisplayOptions ShowEvents { get => showEvents; set => SetEnum(ref showEvents, value, old => ClearPixelCache()); }

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
            Set(ref collisionHighlight, value, old => ClearPixelCache());
         }
      }

      #endregion

      #region Cache

      private short[][] palettes;
      private int[][,] tiles;
      private byte[][] blocks;
      private byte[][] blockAttributes;
      private readonly List<IPixelViewModel> blockRenders = new(); // one image per block
      private IReadOnlyList<IEventViewModel> eventRenders;
      public IReadOnlyList<IPixelViewModel> BlockRenders {
         get {
            lock (blockRenders) {
               if (blockRenders.Count == 0) RefreshBlockRenderCache();
            }
            return blockRenders;
         }
      }

      private void ClearPixelCache() {
         lock (pixelWriteLock) {
            pixelData = null;
            NotifyPropertyChanged(nameof(PixelData));
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
               availableNames.Add(SanitizeName(name.Trim('"')));
            }
            return availableNames;
         }
      }
      private ObservableCollection<string> sortedAvailableNames;
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

      public int GotoNameIndex {
         get => SortedAvailableNames.IndexOf(AvailableNames[SelectedNameIndex]);
         set {
            if (!value.InRange(0, SortedAvailableNames.Count)) return;
            var name = sortedAvailableNames[value];
            // find the first map with that name
            var tableIndex = availableNames.IndexOf(name);
            var mapWithName = AllMapsModel.Create(model).SelectMany(bank => bank).FirstOrDefault(map => map.NameIndex == tableIndex);
            if (mapWithName == null) {
               viewPort.RaiseError($"Could not find a map named {name}");
            } else {
               name = MapIDToText(model, mapWithName.Group, mapWithName.Map);
               viewPort.Goto.Execute($"maps.bank{mapWithName.Group}.{name}");
            }
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

      private ObservableCollection<JumpMapInfo> layoutUseCache;
      private ObservableCollection<JumpMapInfo> FindLayoutUses() {
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

      private BlockEditor blockEditor;
      public BlockEditor BlockEditor {
         get {
            if (blockEditor == null) {
               var layout = GetLayout();
               if (layout == null) return null;
               var blockModel1 = new BlocksetModel(model, layout.GetAddress("blockdata1"));
               var blockModel2 = new BlocksetModel(model, layout.GetAddress("blockdata2"));
               if (palettes == null) RefreshPaletteCache(layout, blockModel1, blockModel2);
               if (tiles == null) RefreshTileCache(layout, blockModel1, blockModel2);
               if (blocks == null) RefreshBlockCache(layout, blockModel1, blockModel2);
               if (blockAttributes == null) RefreshBlockAttributeCache(layout, blockModel1, blockModel2);
               blockEditor = new BlockEditor(viewPort.ChangeHistory, model, tutorials, palettes, tiles, blocks, blockAttributes);
               blockEditor.BlocksChanged += HandleBlocksChanged;
               blockEditor.BlockAttributesChanged += HandleBlockAttributesChanged;
               blockEditor.AutoscrollTiles += HandleAutoscrollTiles;
               BlockEditor.SendMessage += (sender, e) => viewPort.RaiseMessage(e);
               blockEditor.Bind(nameof(blockEditor.ShowTiles), (editor, args) => BorderEditor.ShowBorderPanel &= !editor.ShowTiles);
               blockEditor.Bind(nameof(blockEditor.BlockIndex), (editor, args) => { lastDrawX = lastDrawY = -1; ClearPixelCache(); });
            }
            return blockEditor;
         }
      }

      private BorderEditor borderEditor;
      public BorderEditor BorderEditor {
         get {
            if (borderEditor == null) {
               borderEditor = new BorderEditor(this, tutorials);
               borderEditor.BorderChanged += HandleBorderChanged;
               borderEditor.Bind(nameof(borderEditor.ShowBorderPanel), (editor, args) => {
                  if (BlockEditor == null) return;
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

      private WildPokemonViewModel wildPokemon;
      public WildPokemonViewModel WildPokemon {
         get {
            if (wildPokemon == null) wildPokemon = new WildPokemonViewModel(viewPort, tutorials, group, map);
            return wildPokemon;
         }
      }

      private SurfConnectionViewModel surfConnection;
      public SurfConnectionViewModel SurfConnection {
         get {
            if (surfConnection == null) {
               surfConnection = new SurfConnectionViewModel(viewPort, group, map);
               surfConnection.RequestChangeMap += (sender, e) => RequestChangeMap.Raise(this, e);
               surfConnection.ConnectNewMap += (sender, e) => ConnectNewMap(e);
               surfConnection.ConnectExistingMap += (sender, e) => ConnectExistingMap(e);
               surfConnection.RequestRemoveConnection += (sender, e) => {
                  var connections = AllMapsModel.Create(model, tokenFactory)[group][map].Connections;
                  var toRemove = connections.Count.Range().Where(i => connections[i].Direction.IsAny(MapDirection.Dive, MapDirection.Emerge)).ToList();
                  RemoveConnections(toRemove);
               };
            }
            return surfConnection;
         }
      }

      public BlockMapViewModel(IFileSystem fileSystem, MapTutorialsViewModel tutorials, IEditableViewPort viewPort, Format format, EventTemplate eventTemplate, int group, int map) {
         this.format = format;
         this.fileSystem = fileSystem;
         this.tutorials = tutorials;
         this.viewPort = viewPort;
         this.model = viewPort.Model;
         this.eventTemplate = eventTemplate;
         this.tokenFactory = () => viewPort.ChangeHistory.CurrentChange;
         (this.group, this.map) = (group, map);
         Transparent = -1;
         var mapModel = GetMapModel();
         RefreshMapSize();
         PrimaryTiles = PrimaryBlocks = model.IsFRLG() ? 640 : 512;
         PrimaryPalettes = model.IsFRLG() ? 7 : 6;

         (LeftEdge, TopEdge) = (-PixelWidth / 2, -PixelHeight / 2);

         mapScriptCollection = new(viewPort);
         mapScriptCollection.NewMapScriptsCreated += (sender, e) => GetMapModel().SetAddress("mapscripts", e.Address);

         mapRepointer = new MapRepointer(format, fileSystem, viewPort, viewPort.ChangeHistory, MapID, () => {
            Header.Refresh();
            layoutUseCache = null; // invalidate the cache during a refresh
            NotifyPropertiesChanged(nameof(BlockMapShareCount), nameof(BlockMapUses), nameof(BlockMapIsShared));
         });
         mapRepointer.ChangeMap += (sender, e) => RequestChangeMap.Raise(this, e);
         mapRepointer.DataMoved += (sender, e) => {
            ClearCaches();
            if (e == null) return;
            InformRepoint(e);
            if (e.Type == "Layout") UpdateLayoutID();
         };
      }

      private BerryInfo berryInfo;
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

      private BerryInfo SetupBerryInfo() {
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

      public IReadOnlyList<BlockMapViewModel> GetNeighbors(MapDirection direction) {
         var list = new List<BlockMapViewModel>();
         var border = GetBorderThickness();
         if (border == null) return list;
         var connections = GetConnections();
         if (connections == null) return list;
         foreach (var connection in connections) {
            if (connection.Direction != direction) continue;
            var vm = GetNeighbor(connection, border);
            list.Add(vm);
         }
         return list;
      }

      public bool UpdateFrom(BlockMapViewModel other) {
         if (other == null) return false;
         if (other == this) return true;
         if (other.MapID == MapID) {
            IncludeBorders = other.IncludeBorders;
            SpriteScale = other.SpriteScale;
            LeftEdge = other.LeftEdge;
            TopEdge = other.TopEdge;
            ZIndex = other.ZIndex;
            IsSelected = other.IsSelected;
            return true;
         }
         return false;
      }

      public void GotoData() {
         var map = GetMapModel();
         viewPort.Goto.Execute(map.Start);
      }

      public void ClearCaches() {
         palettes = null;
         tiles = null;
         blocks = null;
         lock (blockRenders) {
            blockRenders.Clear();
         }
         blockPixels = null;
         eventRenders = null;
         borderBlock = null;
         berryInfo = null;
         WildPokemon.ClearCache();
         RefreshMapSize(false);
         RefreshBlockAttributeCache();
         if (borderEditor != null) {
            var oldShowBorder = borderEditor.ShowBorderPanel;
            borderEditor.BorderChanged -= HandleBorderChanged;
            var oldBorderEditor = borderEditor;
            borderEditor = null;
            BorderEditor.ShowBorderPanel = oldShowBorder;
            oldBorderEditor.ShowBorderPanel = false;
            NotifyPropertyChanged(nameof(BorderEditor));
         }
         Header.UpdateFromModel();
         ClearPixelCache();
         if (!MapScriptCollection.Unloaded) MapScriptCollection.Load(GetMapModel());
         NotifyPropertiesChanged(nameof(BlockRenders), nameof(BlockPixels), nameof(BerryInfo));
         if (SelectedEvent != null) CycleActiveEvent(default, EventCycleDirection.None);
      }

      public void RedrawEvents() {
         eventRenders = null;
         NotifyPropertyChanged(nameof(CanCreateFlyEvent));
         ClearPixelCache();
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

      // check for other warps on this same tile and see what primary/secondary blockset is expected for the new map.
      // if no reasonable tileset is found, just use the current map's blocksets
      public BlockMapViewModel CreateMapForWarp(WarpEventViewModel warp) {
         // what bank should the new map go in?
         var option = MapRepointer.GetMapBankForNewMap(
            "Maps are organized into banks. The game doesn't care, so you can use the banks however you like."
            + Environment.NewLine +
            "Which map bank do you want to use for the new map?");
         if (option == -1) return null;
         var token = tokenFactory();
         MapModel thisMap = new(GetMapModel());

         // give me this block
         var blockIndex = thisMap.Blocks[warp.X, warp.Y].Tile;

         // give me all maps that use this blockset
         var borderBlockAddress = thisMap.Layout.BorderBlockAddress;
         var primaryBlocksetAddress = thisMap.Layout.PrimaryBlockset.Start;
         var secondaryBlocksetAddress = thisMap.Layout.SecondaryBlockset.Start;

         var maps = new List<MapModel>();
         foreach (var map in GetAllMaps()) {
            if (map?.Layout?.PrimaryBlockset?.Start != primaryBlocksetAddress && blockIndex < PrimaryBlocks) continue;
            if (map?.Layout?.SecondaryBlockset?.Start != secondaryBlocksetAddress && blockIndex >= PrimaryBlocks) continue;
            maps.Add(map);
         }

         // give me all warps in those maps (except for this warp itself)
         // give me all warps that are on this tile
         var warps = new List<WarpEventModel>();
         foreach (var map in maps) {
            var layout = map.Layout;
            foreach (var w in map.Events.Warps) {
               if (w.Element.Start == warp.Element.Start) continue;
               if (map.Blocks[w.X, w.Y].Tile != blockIndex) continue;
               warps.Add(w);
            }
         }

         // give me maps that those warp to
         // give me all the primary/secondary blocksets for those maps
         // give me all the borders for those maps
         var prototypes = new Dictionary<LayoutPrototype, List<WarpEventModel>>();
         foreach (var w in warps) {
            var m = w.TargetMap;
            if (m == null) continue;
            var (primary, secondary, border) = (m.Layout.PrimaryBlockset, m.Layout.SecondaryBlockset, m.Layout.BorderBlockAddress);
            if (primary == null || secondary == null || border == Pointer.NULL) continue;
            var prototype = new LayoutPrototype(primary.Start, secondary.Start, border);
            if (!prototypes.ContainsKey(prototype)) prototypes.Add(prototype, new());
            prototypes[prototype].Add(w);
         }

         var orderedPrototypes = prototypes.Keys.ToList();
         var initialBlockmap = new int[9, 9];
         var warpIsBottomSquare = true;

         if (orderedPrototypes.Count > 1) {
            var initialBlockmaps = new List<int[,]>();
            var warpIsBottomSquareForIndex = new List<bool>();

            // for each prototype, create an image that represents what that map prototype would look like
            var images = new List<VisualOption>();
            foreach (var prototype in orderedPrototypes) {
               var targets = prototypes[prototype];
               var (render, blockmap, isBottomSquare) = RenderPrototype(targets);
               initialBlockmaps.Add(blockmap);
               warpIsBottomSquareForIndex.Add(isBottomSquare);
               var targetMapName = MapIDToText(model, targets[0].Bank, targets[0].Map);
               var nameParts = targetMapName.SplitLast('.');
               var targetLocation = nameParts[0];
               var targetName = nameParts[1];
               var visOption = new VisualOption { Index = orderedPrototypes.IndexOf(prototype), Option = $"Like {targetLocation}", ShortDescription = targetName, Visual = render };
               images.Add(visOption);
            }

            // create one additional 'blank' prototype
            initialBlockmaps.Add(new int[9, 9]);
            var blank = new LayoutPrototype(primaryBlocksetAddress, secondaryBlocksetAddress, borderBlockAddress);
            orderedPrototypes.Add(blank);
            var blankRender = new CanvasPixelViewModel(9 * 16, 9 * 16) { SpriteScale = images[0].Visual.SpriteScale };
            var blankVisOption = new VisualOption { Index = orderedPrototypes.Count - 1, Option = "Blank", ShortDescription = "Use Current Blocksets", Visual = blankRender };
            warpIsBottomSquareForIndex.Add(true);
            images.Add(blankVisOption);

            var choice = fileSystem.ShowOptions("Create New Map", "Start from which template?", null, images.ToArray());
            if (choice == -1) return null;
            var chosenPrototype = orderedPrototypes[choice];
            // use the most frequent primary/secondary blockset and border blocks
            primaryBlocksetAddress = chosenPrototype.PrimaryBlockset;
            secondaryBlocksetAddress = chosenPrototype.SecondaryBlockset;
            borderBlockAddress = chosenPrototype.BorderBlock;
            initialBlockmap = initialBlockmaps[choice];
            warpIsBottomSquare = warpIsBottomSquareForIndex[choice];
         } else if (orderedPrototypes.Count == 1) {
            // no dialog, just go with this one
            var chosenPrototype = orderedPrototypes[0];
            primaryBlocksetAddress = chosenPrototype.PrimaryBlockset;
            secondaryBlocksetAddress = chosenPrototype.SecondaryBlockset;
            borderBlockAddress = chosenPrototype.BorderBlock;
            var (render, blockmap, isBottomSquare) = RenderPrototype(prototypes.Values.Single());
            initialBlockmap = blockmap;
            warpIsBottomSquare = isBottomSquare;
         }

         // create a new 9x9 map
         var newMap = CreateNewMap(token, option, 9, 9);
         if (newMap == null) {
            if (model.GetTable(HardcodeTablesModel.MapLayoutTable) == null) {
               viewPort.RaiseError(MapRepointer.MapLayoutMissing);
            } else {
               viewPort.RaiseError(MapRepointer.MapBankFullError);
            }
            return null;
         }
         var newLayout = newMap.GetLayout();
         newLayout.SetAddress(Format.BorderBlock, borderBlockAddress);
         newLayout.SetAddress(Format.PrimaryBlockset, primaryBlocksetAddress);
         newLayout.SetAddress(Format.SecondaryBlockset, secondaryBlocksetAddress);
         var start = newLayout.GetAddress(Format.BlockMap);
         for (int x = 0; x < 9; x++) {
            for (int y = 0; y < 9; y++) {
               model.WriteMultiByteValue(start + (y * 9 + x) * 2, 2, token, initialBlockmap[x, y]);
            }
         }

         // place reverse warp at bottom heading back
         (warp.Bank, warp.Map, warp.WarpID) = (newMap.group, newMap.map, 1);
         var returnWarp = newMap.CreateWarpEvent(group, map);
         returnWarp.WarpID = warp.Element.ArrayIndex + 1;
         (returnWarp.X, returnWarp.Y) = (4, 8);
         if (!warpIsBottomSquare) (returnWarp.X, returnWarp.Y) = (4, 7);

         // repoint border block
         newMap.MapRepointer.RepointBorderBlock.Execute();

         return newMap;
      }

      public void UpdateClone(BlockMapViewModel neighbor, ObjectEventViewModel parentEvent, bool deleted = false) {
         if (!model.IsFRLG() || neighbor == null || parentEvent == null) return;
         var obj = EventGroup.Objects.FirstOrDefault(obj => obj.Kind && obj.Elevation == parentEvent.Element.ArrayIndex + 1 && obj.TrainerType == neighbor.map && obj.TrainerRangeOrBerryID == neighbor.group);
         var (thisX, thisY) = ConvertCoordinates(0, 0);
         var (thatX, thatY) = neighbor.ConvertCoordinates(0, 0);
         var (xDif, yDif) = (thisX - thatX, thisY - thatY);
         var desiredX = parentEvent.X + xDif;
         var desiredY = parentEvent.Y + yDif;
         var layout = GetLayout();
         var (width, height) = (layout.GetValue("width"), layout.GetValue("height"));
         var needClone = desiredX >= -8 && desiredY >= -8 && desiredX < width + 8 && desiredY < height + 8;
         if (deleted) needClone = false;
         if (obj == null && needClone) {
            obj = CreateObjectEvent(parentEvent.Graphics, Pointer.NULL);
            obj.Kind = true;
            obj.Elevation = parentEvent.Element.ArrayIndex + 1;
            obj.TrainerType = neighbor.map;
            obj.TrainerRangeOrBerryID = neighbor.group;
            obj.Flag = parentEvent.Flag;
            ViewPort.RaiseMessage($"Clone added to map ({this.group}-{this.map}) for object {parentEvent.Element.ArrayIndex + 1}.");
         } else if (!needClone) {
            if (obj != null) {
               obj.Delete();
               ClearCaches();
               ViewPort.RaiseMessage($"Clone removed from map ({this.group}-{this.map}) for object {parentEvent.Element.ArrayIndex + 1}.");
            }
            return;
         }

         obj.X = desiredX;
         obj.Y = desiredY;
         ClearCaches();
      }

      // from the maps that use this blockmap/blockset/border,
      // figure out appropriate wall/floor tiles to make a small prototype map
      private (IPixelViewModel, int[,], bool) RenderPrototype(List<WarpEventModel> warps) {
         // TODO I don't just want to return an image, I want to return the tiles/collisions to use as well

         // find the edge tiles
         var topTile = warps.Select(warp => warp.TargetMap.Layout).SelectMany(layout => (layout.Width - 2).Range(x => layout.BlockMap[x + 1, 0].Block)).ToHistogram().MostCommonKey();
         var bottomTile = warps.Select(warp => warp.TargetMap.Layout).SelectMany(layout => (layout.Width - 2).Range(x => layout.BlockMap[x + 1, layout.Height - 1].Block)).ToHistogram().MostCommonKey();
         var leftTile = warps.Select(warp => warp.TargetMap.Layout).SelectMany(layout => (layout.Height - 2).Range(y => layout.BlockMap[0, y + 1].Block)).ToHistogram().MostCommonKey();
         var rightTile = warps.Select(warp => warp.TargetMap.Layout).SelectMany(layout => (layout.Height - 2).Range(y => layout.BlockMap[layout.Width - 1, y + 1].Block)).ToHistogram().MostCommonKey();
         var topLeftTile = warps.Select(warp => warp.TargetMap.Layout).Select(layout => layout.BlockMap[0, 0].Block).ToHistogram().MostCommonKey();
         var topRightTile = warps.Select(warp => warp.TargetMap.Layout).Select(layout => layout.BlockMap[layout.Width - 1, 0].Block).ToHistogram().MostCommonKey();
         var bottomLeftTile = warps.Select(warp => warp.TargetMap.Layout).Select(layout => layout.BlockMap[0, layout.Height - 1].Block).ToHistogram().MostCommonKey();
         var bottomRightTile = warps.Select(warp => warp.TargetMap.Layout).Select(layout => layout.BlockMap[layout.Width - 1, layout.Height - 1].Block).ToHistogram().MostCommonKey();

         // find the floor tile
         var centerTiles = new List<int>();
         foreach (var warp in warps) {
            var layout = warp.TargetMap.Layout;
            for (int x = 1; x < layout.Width - 1; x++) {
               for (int y = 1; y < layout.Height - 1; y++) {
                  centerTiles.Add(layout.BlockMap[x, y].Block);
               }
            }
         }
         var floorTile = centerTiles.ToHistogram().MostCommonKey();

         // build the map image data
         var blockMap = new int[9, 9];
         for (int i = 1; i < 8; i++) {
            blockMap[0, i] = leftTile;
            blockMap[8, i] = rightTile;
            blockMap[i, 0] = topTile;
            blockMap[i, 8] = bottomTile;
            for (int j = 1; j < 8; j++) blockMap[i, j] = floorTile;
         }
         (blockMap[0, 0], blockMap[8, 0], blockMap[0, 8], blockMap[8, 8]) = (topLeftTile, topRightTile, bottomLeftTile, bottomRightTile);

         // find the door tile
         bool warpIsAgainstWall = true;
         if (warps.Count > 0) {
            var map0 = warps[0].TargetMap;
            var mapWarps = map0.Events.Warps;
            if (mapWarps.Count > warps[0].WarpID) {
               var matchingWarp0 = mapWarps[warps[0].WarpID];
               warpIsAgainstWall = matchingWarp0.Y == map0.Layout.Height - 1;
               if (warpIsAgainstWall) {
                  blockMap[3, 7] = map0.Blocks[matchingWarp0.X - 1, matchingWarp0.Y - 1].Block;
                  blockMap[4, 7] = map0.Blocks[matchingWarp0.X, matchingWarp0.Y - 1].Block;
                  blockMap[5, 7] = map0.Blocks[matchingWarp0.X + 1, matchingWarp0.Y - 1].Block;

                  blockMap[3, 8] = map0.Blocks[matchingWarp0.X - 1, matchingWarp0.Y].Block;
                  blockMap[4, 8] = map0.Blocks[matchingWarp0.X, matchingWarp0.Y].Block;
                  blockMap[5, 8] = map0.Blocks[matchingWarp0.X + 1, matchingWarp0.Y].Block;
               } else {
                  blockMap[3, 7] = map0.Blocks[matchingWarp0.X - 1, matchingWarp0.Y].Block;
                  blockMap[4, 7] = map0.Blocks[matchingWarp0.X, matchingWarp0.Y].Block;
                  blockMap[5, 7] = map0.Blocks[matchingWarp0.X + 1, matchingWarp0.Y].Block;

                  blockMap[3, 8] = map0.Blocks[matchingWarp0.X - 1, matchingWarp0.Y + 1].Block;
                  blockMap[4, 8] = map0.Blocks[matchingWarp0.X, matchingWarp0.Y + 1].Block;
                  blockMap[5, 8] = map0.Blocks[matchingWarp0.X + 1, matchingWarp0.Y + 1].Block;
               }
            }
         }

         // draw the map
         var viewModel = new BlockMapViewModel(fileSystem, tutorials, viewPort, format, eventTemplate, warps[0].Bank, warps[0].Map) { BerryInfo = BerryInfo };
         var canvas = new CanvasPixelViewModel(9 * 16, 9 * 16);
         for (int y = 0; y < 9; y++) {
            for (int x = 0; x < 9; x++) {
               canvas.Draw(viewModel.BlockRenders[blockMap[x, y] & 0x3FF], x * 16, y * 16);
            }
         }

         canvas.SpriteScale = .5;
         return (canvas, blockMap, warpIsAgainstWall);
      }

      #region Draw / Paint

      private (int, int) ConvertCoordinates(double x, double y) {
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

      private int lastDrawVal, lastDrawX, lastDrawY;

      /// <summary>
      /// If collisionIndex is not valid, it's ignored.
      /// If blockIndex is not valid, it's ignored.
      /// </summary>
      public void DrawBlock(ModelDelta token, int blockIndex, int collisionIndex, double x, double y) {
         var (xx, yy) = ConvertCoordinates(x, y);
         DrawBlock(token, blockIndex, collisionIndex, xx, yy);
      }

      public void DrawBlock(ModelDelta token, int blockIndex, int collisionIndex, int xx, int yy) {
         var layout = GetLayout();
         var (width, height) = (layout.GetValue("width"), layout.GetValue("height"));
         var border = GetBorderThickness(layout);

         xx = xx.LimitToRange(0, width - 1);
         yy = yy.LimitToRange(0, height - 1);
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

         var canvas = new CanvasPixelViewModel(pixelWidth, pixelHeight, PixelData);
         bool updateBlock = blockIndex >= 0 && blockIndex < blockRenders.Count;
         bool updateHighlight = collisionIndex == collisionHighlight && collisionHighlight != -1;
         (xx, yy) = ((xx + border.West) * 16, (yy + border.North) * 16);
         if (updateBlock) canvas.Draw(blockRenders[blockIndex], xx, yy);
         if (updateHighlight && xx < pixelWidth && yy < pixelHeight) HighlightCollision(PixelData, xx, yy);
         if (updateBlock || updateHighlight) NotifyPropertyChanged(nameof(PixelData));
         tutorials.Complete(Tutorial.LeftClickMap_DrawBlock);
      }

      public void Draw9Grid(ModelDelta token, int[,] grid, double x, double y) {
         var (xx, yy) = ConvertCoordinates(x, y);
         Draw9Grid(token, grid, xx, yy);
      }

      public void Draw25Grid(ModelDelta token, int[,] grid, double x, double y) {
         var (xx, yy) = ConvertCoordinates(x, y);
         Draw25Grid(token, grid, xx, yy);
      }

      public void Draw9Grid(ModelDelta token, int[,] grid, int xx, int yy) {
         var targets = new List<int>();
         for (int x = 0; x < 3; x++) for (int y = 0; y < 3; y++) targets.Add(grid[x, y] & 0x3FF);

         var layout = GetLayout();
         var (width, height) = (layout.GetValue("width"), layout.GetValue("height"));
         var start = layout.GetAddress("blockmap");

         int get(Point p) => p.X < 0 || p.Y < 0 || p.X >= width || p.Y >= height ? -1 : model.ReadMultiByteValue(start + (p.Y * width + p.X) * 2, 2) & 0x3FF;
         void set(Point p, int block) => model.WriteMultiByteValue(start + (p.Y * width + p.X) * 2, 2, token, block);

         // change all connected blocks based on the grid
         var todo = new List<Point> { new(xx, yy), new(xx - 1, yy), new(xx + 1, yy), new(xx, yy - 1), new(xx, yy + 1) };
         lock (pixelWriteLock) {
            set(todo[0], grid[1, 1]);
            foreach (var cell in todo) {
               var cellValue = get(cell);
               if (!targets.Contains(cellValue)) continue;

               var north = targets.Contains(get(new(cell.X, cell.Y - 1)));
               var south = targets.Contains(get(new(cell.X, cell.Y + 1)));
               var west = targets.Contains(get(new(cell.X - 1, cell.Y)));
               var east = targets.Contains(get(new(cell.X + 1, cell.Y)));
               var aggregate = (north ? "N" : " ") + (east ? "E" : " ") + (south ? "S" : " ") + (west ? "W" : " ");

               var block = aggregate switch {
                  " ES " => grid[0, 0],
                  " ESW" => grid[1, 0],
                  "  SW" => grid[2, 0],
                  "NES " => grid[0, 1],
                  "NESW" => grid[1, 1],
                  "N SW" => grid[2, 1],
                  "NE  " => grid[0, 2],
                  "NE W" => grid[1, 2],
                  "N  W" => grid[2, 2],
                  _ => grid[1, 1],
               };
               set(cell, block);
            }
         }

         ClearPixelCache();
      }

      public void Draw25Grid(ModelDelta token, int[,] grid, int xx, int yy) {
         var targets = new List<int>();
         for (int x = 0; x < 5; x++) {
            for (int y = 0; y < 5; y++) {
               if (x == 0 && y == 0) continue;
               if (x == 4 && y == 0) continue;
               if (x == 0 && y == 4) continue;
               if (x == 4 && y == 4) continue;
               targets.Add(grid[x, y] & 0x3FF);
            }
         }

         var layout = GetLayout();
         var (width, height) = (layout.GetValue("width"), layout.GetValue("height"));
         var start = layout.GetAddress("blockmap");

         int get(Point p) => p.X < 0 || p.Y < 0 || p.X >= width || p.Y >= height ? -1 : model.ReadMultiByteValue(start + (p.Y * width + p.X) * 2, 2) & 0x3FF;
         void set(Point p, int block) => model.WriteMultiByteValue(start + (p.Y * width + p.X) * 2, 2, token, block);

         // change all connected blocks based on the grid
         var todo = new List<Point> {
            new(xx, yy),
            new(xx - 1, yy), new(xx + 1, yy), new(xx, yy - 1), new(xx, yy + 1),
            new(xx + 1, yy + 1), new(xx + 1, yy - 1), new(xx - 1, yy + 1), new(xx - 1, yy - 1),
         };
         lock (pixelWriteLock) {
            set(todo[0], grid[1, 1]);
            foreach (var cell in todo) {
               var cellValue = get(cell);
               if (!targets.Contains(cellValue)) continue;

               var northwest = targets.Contains(get(new(cell.X - 1, cell.Y - 1)));
               var northeast = targets.Contains(get(new(cell.X + 1, cell.Y - 1)));
               var southwest = targets.Contains(get(new(cell.X - 1, cell.Y + 1)));
               var southeast = targets.Contains(get(new(cell.X + 1, cell.Y + 1)));
               var north = targets.Contains(get(new(cell.X, cell.Y - 1)));
               var south = targets.Contains(get(new(cell.X, cell.Y + 1)));
               var west = targets.Contains(get(new(cell.X - 1, cell.Y)));
               var east = targets.Contains(get(new(cell.X + 1, cell.Y)));

               var aggregate = (north ? "N" : " ") + (east ? "E" : " ") + (south ? "S" : " ") + (west ? "W" : " ");
               var corners = (northwest ? "7" : " ") + (northeast ? "9" : " ") + (southeast ? "3" : " ") + (southwest ? "1" : " ");

               // grid[x, y]
               var block = aggregate switch {
                  " ES " => grid[1, 0],
                  " ESW" => grid[2, 0],
                  "  SW" => grid[3, 0],
                  "NES " => grid[0, 2],
                  "NESW" => grid[2, 2],
                  "N SW" => grid[4, 2],
                  "NE  " => grid[0, 3],
                  "NE W" => grid[2, 4],
                  "N  W" => grid[4, 3],
                  _ => grid[1, 1],
               };

               if ("NW".All(aggregate.Contains) && !corners.Contains('7')) block = grid[1, 1];
               if ("NE".All(aggregate.Contains) && !corners.Contains('9')) block = grid[3, 1];
               if ("SE".All(aggregate.Contains) && !corners.Contains('3')) block = grid[3, 3];
               if ("SW".All(aggregate.Contains) && !corners.Contains('1')) block = grid[1, 3];

               set(cell, block);
            }
         }

         ClearPixelCache();
      }

      public void DrawBlocks(ModelDelta token, int[,] tiles, Point source, Point destination) {
         while (Math.Abs(destination.X - source.X) % tiles.GetLength(0) != 0) destination -= new Point(1, 0);
         while (Math.Abs(destination.Y - source.Y) % tiles.GetLength(1) != 0) destination -= new Point(0, 1);

         var layout = GetLayout();
         var (width, height) = (layout.GetValue("width"), layout.GetValue("height"));
         var start = layout.GetAddress("blockmap");
         int changeCount = 0;
         lock (pixelWriteLock) {
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
         }

         if (changeCount > 0) ClearPixelCache();
      }

      public void RepeatBlock(Func<ModelDelta> futureToken, IReadOnlyList<int> blockOptions, int collision, int x, int y, int w, int h, bool refreshScreen) {
         var layout = GetLayout();
         var (width, height) = (layout.GetValue("width"), layout.GetValue("height"));
         var start = layout.GetAddress("blockmap");
         int changeCount = 0;
         var rnd = new Random();
         lock (pixelWriteLock) {
            for (int xx = 0; xx < w; xx++) {
               for (int yy = 0; yy < h; yy++) {
                  if (x + xx < 0 || y + yy < 0 || x + xx >= width || y + yy >= height) continue;
                  var address = start + ((yy + y) * width + xx + x) * 2;
                  var blockValue = model.ReadMultiByteValue(address, 2);
                  lastDrawVal = blockValue;
                  var block = blockOptions[0];
                  if (blockOptions.Count > 1) block = rnd.From(blockOptions);
                  if (block >= 0) blockValue = (blockValue & 0xFC00) + block;
                  if (collision >= 0) blockValue = (blockValue & 0x3FF) + (collision << 10);
                  if (blockValue != lastDrawVal) {
                     model.WriteMultiByteValue(address, 2, futureToken(), blockValue);
                     changeCount++;
                  }
               }
            }
         }
         if (changeCount > 0 && refreshScreen) ClearPixelCache();
      }

      public void RepeatBlocks(Func<ModelDelta> futureToken, int[,] blockValues, int x, int y, int w, int h, bool refreshScreen) {
         var layout = GetLayout();
         var (width, height) = (layout.GetValue("width"), layout.GetValue("height"));
         var start = layout.GetAddress("blockmap");
         int changeCount = 0;
         lock (pixelWriteLock) {
            for (int xx = 0; xx < w; xx++) {
               for (int yy = 0; yy < h; yy++) {
                  if (x + xx < 0 || y + yy < 0 || x + xx >= width || y + yy >= height) continue;
                  var address = start + ((yy + y) * width + xx + x) * 2;
                  var block = blockValues == null ? -1 : blockValues[xx % blockValues.GetLength(0), yy % blockValues.GetLength(1)];
                  if (block == -1 && collisionHighlight >= 0) {
                     var existingBlock = (model.ReadMultiByteValue(address, 2) & 0x3F);
                     block = (existingBlock | (collisionHighlight << 10));
                  }
                  if (block != -1 && model.ReadMultiByteValue(address, 2) != block) {
                     model.WriteMultiByteValue(address, 2, futureToken(), block);
                     changeCount++;
                  }
               }
            }
         }
         if (changeCount > 0 && refreshScreen) ClearPixelCache();
      }

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
         var (before, after) = (lastDrawVal, (collisionIndex << 10) | blockIndex);
         lock (pixelWriteLock) {
            PaintBlock(token, new(xx - 1, yy), size, start, before, after);
            PaintBlock(token, new(xx + 1, yy), size, start, before, after);
            PaintBlock(token, new(xx, yy - 1), size, start, before, after);
            PaintBlock(token, new(xx, yy + 1), size, start, before, after);
         }
         ClearPixelCache();
      }

      private void PaintBlock(ModelDelta token, Point p, Point size, int start, int before, int after) {
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

      public void PaintBlockBag(ModelDelta token, List<int> blockIndexes, int collisionIndex, double x, double y) {
         if (blockIndexes.Count < 1) return;
         (x, y) = ((x - leftEdge) / spriteScale, (y - topEdge) / spriteScale);
         (x, y) = (x / 16, y / 16);
         var layout = GetLayout();
         var (width, height) = (layout.GetValue("width"), layout.GetValue("height"));
         var border = GetBorderThickness(layout);
         var (xx, yy) = ((int)x - border.West, (int)y - border.North);
         if (xx < 0 || yy < 0 || xx > width || yy > height) return;
         var start = layout.GetAddress("blockmap");

         var complete = new HashSet<Point> { new(xx, yy) };
         var check = new List<Point> { new(xx - 1, yy), new(xx + 1, yy), new(xx, yy - 1), new(xx, yy + 1) };
         var targets = blockIndexes.Select(bi => (collisionIndex << 10) | bi).ToList();
         var rnd = new Random();
         lock (pixelWriteLock) {
            while (check.Count > 0) {
               var p = check[check.Count - 1];
               check.RemoveAt(check.Count - 1);
               if (p.X < 0 || p.Y < 0 || p.X >= width || p.Y >= height) continue;
               if (complete.Contains(p)) continue;
               complete.Add(p);

               var address = start + (p.Y * width + p.X) * 2;
               if (model.ReadMultiByteValue(address, 2) != lastDrawVal) continue;
               model.WriteMultiByteValue(address, 2, token, rnd.From(targets));

               check.AddRange(new Point[] { new(p.X - 1, p.Y), new(p.X + 1, p.Y), new(p.X, p.Y - 1), new(p.X, p.Y + 1) });
            }
         }
         ClearPixelCache();
      }

      private IEnumerable<Point> GetAllMatchingConnectedBlocks(int x, int y) {
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

      public void PaintWaveFunction(ModelDelta token, double x, double y, Func<int, int, WaveCell> wave) {
         (x, y) = ((x - leftEdge) / spriteScale, (y - topEdge) / spriteScale);
         (x, y) = (x / 16, y / 16);
         var layout = GetLayout();
         var (width, height) = (layout.GetValue("width"), layout.GetValue("height"));
         var border = GetBorderThickness(layout);
         var (xx, yy) = ((int)x - border.West, (int)y - border.North);
         if (xx < 0 || yy < 0 || xx > width || yy > height) return;
         var start = layout.GetAddress("blockmap");

         Point right = new(1, 0), down = new(0, 1);
         var rnd = new Random();
         var toDraw = new Dictionary<Point, WaveCell>();
         var allCells = GetAllMatchingConnectedBlocks(xx, yy).ToList();
         void Fill(Point p, int value) => model.WriteMultiByteValue(start + (p.Y * width + p.X) * 2, 2, token, value);

         lock (pixelWriteLock) {
            // set all effected spaces to 0 so they won't count toward eachother's wave-function
            foreach (var cell in allCells) Fill(cell, 0);

            // initial wave function collapse values
            foreach (var cell in allCells) toDraw[cell] = wave(cell.X, cell.Y);

            // reduction loop: find the most restricted cell, collapse it, then propogate its new restrictions
            while (toDraw.Count > 0) {
               var smallest = toDraw.Values.Select(v => v.Probabilities.Count).Min();
               var smallestPoints = toDraw.Where(kvp => kvp.Value.Probabilities.Count == smallest).Select(kvp => kvp.Key).ToList();
               var point = rnd.From(smallestPoints);
               Fill(point, toDraw[point].Collapse(rnd));
               toDraw.Remove(point);
               foreach (var neighbor in new List<Point> { point - right, point + right, point - down, point + down }) {
                  if (!toDraw.ContainsKey(neighbor)) continue;
                  toDraw[neighbor] = wave(neighbor.X, neighbor.Y);
               }
            }
         }

         ClearPixelCache();
      }

      #endregion

      #region Events

      public void UpdateEventLocation(IEventViewModel ev, double x, double y) {
         (lastDrawX, lastDrawY) = (-1, -1);
         var layout = GetLayout();
         var border = GetBorderThickness(layout);
         if (border == null || ev == null) return;
         (x, y) = ((x - leftEdge) / spriteScale, (y - topEdge) / spriteScale);
         var (xx, yy) = ((int)(x / 16) - border.West, (int)(y / 16) - border.North);
         if (ev.X == xx && ev.Y == yy) return;
         if (xx < 0 || yy < 0) return;
         var (width, height) = (layout.GetValue("width"), layout.GetValue("height"));
         if (xx >= width || yy >= height) return;
         ev.X = xx;
         ev.Y = yy;
         SelectedEvent = ev;
         ClearPixelCache();
      }

      public IReadOnlyList<IEventViewModel> EventsUnderCursor(double x, double y, bool autoSelect = true) {
         var matches = new List<IEventViewModel>();
         if (showEvents == MapDisplayOptions.NoEvents) return matches;
         var layout = GetLayout();
         var border = GetBorderThickness(layout);
         var tileX = (int)((x - LeftEdge) / SpriteScale / 16) - border.West;
         var tileY = (int)((y - TopEdge) / SpriteScale / 16) - border.North;
         foreach (var e in GetEvents()) {
            if (e.X == tileX && e.Y == tileY) matches.Add(e);
         }
         if (autoSelect && SelectedEvent != matches.LastOrDefault()) {
            SelectedEvent = matches.LastOrDefault();
            ClearPixelCache();
         }
         return matches;
      }

      const int SizeX = 7, SizeY = 7;
      public IPixelViewModel AutoCrop(int warpID) {
         if (allOverworldSprites == null) allOverworldSprites = RenderOWs(model);
         if (defaultOverworldSprite == null) defaultOverworldSprite = GetDefaultOW(model);
         var map = GetMapModel();
         if (map == null) return null;
         var events = new EventGroupModel(ViewPort.Tools.CodeTool.ScriptParser, GotoAddress, GotoBankMap, map.GetSubTable("events")[0], eventTemplate, allOverworldSprites, defaultOverworldSprite, BerryInfo, group, this.map);
         if (events.Warps.Count <= warpID) return null;
         var warp = events.Warps[warpID];
         return AutoCrop(warp.X, warp.Y);
      }

      public IPixelViewModel AutoCrop(int centerX, int centerY) {
         var map = GetMapModel();
         if (map == null) return null;
         var layout = GetLayout(map);
         if (layout == null) return null;
         var (width, height) = (layout.GetValue("width"), layout.GetValue("height"));

         var startX = centerX - SizeX / 2;
         var startY = centerY - SizeY / 2;
         while (startX < 0) startX += 1;
         while (startY < 0) startY += 1;
         while (startX + SizeX > width) startX--;
         while (startY + SizeY > height) startY--;

         return ReadonlyPixelViewModel.Crop(this, startX * 16, startY * 16, SizeX * 16, SizeY * 16);
      }

      public void DeselectEvent() {
         if (selectedEvent == null) return;
         SelectedEvent = null;
         ClearPixelCache();
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
               yield return new ConnectionSlider(connection, sourceMapInfo, Notify, id, LeftRight, tutorials, right: map.LeftEdge, bottom: map.BottomEdge - tileSize);
               yield return new ConnectionSlider(connection, sourceMapInfo, Notify, id + 1, LeftRight, tutorials, left: map.RightEdge, bottom: map.BottomEdge - tileSize);
            }

            if (connection.Direction == MapDirection.Down) {
               connectionCount.down++;
               yield return new ConnectionSlider(connection, sourceMapInfo, Notify, id, LeftRight, tutorials, right: map.LeftEdge, top: map.TopEdge + tileSize);
               yield return new ConnectionSlider(connection, sourceMapInfo, Notify, id + 1, LeftRight, tutorials, left: map.RightEdge, top: map.TopEdge + tileSize);
            }

            if (connection.Direction == MapDirection.Left) {
               connectionCount.left++;
               yield return new ConnectionSlider(connection, sourceMapInfo, Notify, id, UpDown, tutorials, right: map.RightEdge - tileSize, bottom: map.TopEdge);
               yield return new ConnectionSlider(connection, sourceMapInfo, Notify, id + 1, UpDown, tutorials, right: map.RightEdge - tileSize, top: map.BottomEdge);
            }

            if (connection.Direction == MapDirection.Right) {
               connectionCount.right++;
               yield return new ConnectionSlider(connection, sourceMapInfo, Notify, id, UpDown, tutorials, left: map.LeftEdge + tileSize, bottom: map.TopEdge);
               yield return new ConnectionSlider(connection, sourceMapInfo, Notify, id + 1, UpDown, tutorials, left: map.LeftEdge + tileSize, top: map.BottomEdge);
            }

            id += 2;
         }

         // get sliders for size expansion
         var centerX = (LeftEdge + RightEdge - MapSlider.SliderSize) / 2;
         var centerY = (TopEdge + BottomEdge - MapSlider.SliderSize) / 2;
         yield return new ExpansionSlider(ResizeMapData, id + 0, ExtendUp, GetConnectionCommands(connections, MapDirection.Up), left: centerX, bottom: TopEdge);
         yield return new ExpansionSlider(ResizeMapData, id + 1, ExtendDown, GetConnectionCommands(connections, MapDirection.Down), left: centerX, top: BottomEdge);
         yield return new ExpansionSlider(ResizeMapData, id + 2, ExtendLeft, GetConnectionCommands(connections, MapDirection.Left), right: LeftEdge, top: centerY);
         yield return new ExpansionSlider(ResizeMapData, id + 3, ExtendRight, GetConnectionCommands(connections, MapDirection.Right), left: RightEdge, top: centerY);
      }

      private IEnumerable<IMenuCommand> GetConnectionCommands(IReadOnlyList<ConnectionModel> connections, MapDirection direction) {
         var toRemove = new List<int>();

         var info = CanConnect(direction);
         if (info != null) {
            if (info.Size > 3) {
               // we can make a map here of width/height longestSpanLength
               // and the offset is availableSpace[longestSpanStart]
               yield return new MenuCommand<ConnectionInfo>("Create New Map", ConnectNewMap) { Parameter = info };
               yield return new MenuCommand<ConnectionInfo>("Connect Existing Map", ConnectExistingMap) { Parameter = info };
            } else if (info.Offset < 0) {
               // we can make a map here of width/height 4
               // and the offset is -3
               yield return new MenuCommand<ConnectionInfo>("Create New Map", ConnectNewMap) { Parameter = info };
               yield return new MenuCommand<ConnectionInfo>("Connect Existing Map", ConnectExistingMap) { Parameter = info };
            } else {
               // we can make a map here of width/height 4
               // and the offset is dimensionLength-1
               yield return new MenuCommand<ConnectionInfo>("Create New Map", ConnectNewMap) { Parameter = info };
               yield return new MenuCommand<ConnectionInfo>("Connect Existing Map", ConnectExistingMap) { Parameter = info };
            }
         }

         for (int i = 0; i < connections.Count; i++) {
            if (connections[i].Direction != direction) continue;
            toRemove.Add(i);
         }
         if (toRemove.Count > 0) {
            // we can remove these connections
            yield return new MenuCommand<IReadOnlyList<int>>("Remove Connections", RemoveConnections) { Parameter = toRemove };
         }
      }

      #endregion

      #region Work Methods

      private IEnumerable<MapModel> GetAllMaps() {
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

      private void ResizeMapData(MapDirection direction, int amount) {
         if (amount == 0) return;
         var token = tokenFactory();
         var map = GetMapModel();
         var layout = GetLayout(map);
         if (layout == null) return;
         var run = model.GetNextRun(layout.GetAddress("blockmap")) as BlockmapRun;
         if (run == null) return;
         var borderWidth = layout.HasField("borderwidth") ? layout.GetValue("borderwidth") : 2;
         var borderHeight = layout.HasField("borderheight") ? layout.GetValue("borderheight") : 2;
         var newRun = run.TryChangeSize(tokenFactory, direction, amount, borderWidth, borderHeight);
         if (newRun != null) {
            var tileSize = (int)(16 * spriteScale);
            if (direction == MapDirection.Left) LeftEdge -= amount * tileSize;
            if (direction == MapDirection.Up) TopEdge -= amount * tileSize;
            foreach (var connection in GetConnections(map, group, this.map)) {
               if (direction == MapDirection.Left) {
                  if (connection.Direction == MapDirection.Down || connection.Direction == MapDirection.Up) {
                     connection.Offset += amount;
                     var inverse = connection.GetInverse();
                     if (inverse != null) inverse.Offset -= amount;
                  }
               } else if (direction == MapDirection.Up) {
                  if (connection.Direction == MapDirection.Left || connection.Direction == MapDirection.Right) {
                     connection.Offset += amount;
                     var inverse = connection.GetInverse();
                     if (inverse != null) inverse.Offset -= amount;
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
            tutorials.Complete(Tutorial.DragConnectionButtons_ResizeMap);
         }
      }

      public void ConnectNewMap(ConnectionInfo info) {
         ViewPort.ChangeHistory.ChangeCompleted();
         using (viewPort.ChangeHistory.ContinueCurrentTransaction()) {
            var token = tokenFactory();
            var mapBanks = new ModelTable(model, model.GetTable(HardcodeTablesModel.MapBankTable).Start, tokenFactory);
            tutorials.Complete(Tutorial.RightClick_CreateConnection);
            var option = MapRepointer.GetMapBankForNewMap(
               "Maps are organized into banks. The game doesn't care, so you can use the banks however you like."
               + Environment.NewLine +
               "Which map bank do you want to use for the new map?");
            if (option == -1) return;

            var map = GetMapModel();

            var (width, height) = (info.Size, info.Size);

            var layoutModel = new LayoutModel(GetLayout());
            if (info.Direction.IsAny(MapDirection.Up, MapDirection.Down)) {
               height = Math.Min(layoutModel.Height, height);
            } else if (info.Direction.IsAny(MapDirection.Right, MapDirection.Left)) {
               width = Math.Min(layoutModel.Width, width);
            }
            var isZConnection = info.Direction.IsAny(MapDirection.Dive, MapDirection.Emerge);
            if (isZConnection) height = info.Offset;
            var otherMap = CreateNewMap(token, option, width, height);
            if (otherMap == null) {
               if (model.GetTable(HardcodeTablesModel.MapLayoutTable) == null) {
                  viewPort.RaiseError(MapRepointer.MapLayoutMissing);
               } else {
                  viewPort.RaiseError(MapRepointer.MapBankFullError);
               }
               return;
            }

            var connections = GetOrCreateConnections(map, token);
            var connectionsAndCount = map.GetSubTable("connections")[0];

            var originalConnectionStart = connections.Start;
            connections = model.RelocateForExpansion(token, connections, connections.Length + connections.ElementLength);
            if (connections.Start != originalConnectionStart) InformRepoint(new("Connections", connections.Start));
            connectionsAndCount.SetValue("count", connections.ElementCount + 1);
            var table = new ModelTable(model, connections.Start, tokenFactory, connections);
            var newConnection = new ConnectionModel(table[connections.ElementCount], group, this.map);
            newConnection.Offset = isZConnection ? 0 : info.Offset;
            newConnection.Direction = info.Direction;
            newConnection.Unused = 0;

            newConnection.MapGroup = otherMap.group;
            newConnection.MapNum = otherMap.map;
            info = new ConnectionInfo(info.Size, isZConnection ? 0 : -info.Offset, info.OppositeDirection);
            newConnection = otherMap.AddConnection(info);
            newConnection.Offset = isZConnection ? 0 : info.Offset;
            newConnection.MapGroup = MapID / 1000;
            newConnection.MapNum = MapID % 1000;
            newConnection.Unused = 0;

            RefreshMapSize();
            NeighborsChanged.Raise(this);
         }
         viewPort.ChangeHistory.ChangeCompleted();
      }

      private BlockMapViewModel CreateNewMap(ModelDelta token, int bank, int width, int height) {
         if (model.GetTable(HardcodeTablesModel.MapLayoutTable) == null) return null;
         var mapTable = MapRepointer.AddNewMapToBank(bank);
         if (mapTable == null) return null; // failed to create map in given bank
         var newMap = MapRepointer.CreateNewMap(token);
         var layout = MapRepointer.CreateNewLayout(token);

         // update width / height
         model.WriteValue(token, layout.Element.Start + 0, width);
         model.WriteValue(token, layout.Element.Start + 4, height);

         layout.Element.SetAddress(Format.BlockMap, MapRepointer.CreateNewBlockMap(token, width, height));

         newMap.Element.SetAddress(Format.Layout, layout.Element.Start);
         model.UpdateArrayPointer(token, null, null, -1, mapTable.Start + mapTable.Length - 4, newMap.Element.Start);

         var otherMap = new BlockMapViewModel(fileSystem, tutorials, viewPort, format, eventTemplate, bank, mapTable.ElementCount - 1) {
            allOverworldSprites = allOverworldSprites,
            BerryInfo = BerryInfo,
         };
         otherMap.UpdateLayoutID();
         return otherMap;
      }

      private void ConnectExistingMap(ConnectionInfo info) {
         var token = tokenFactory();

         // find available maps
         var options = new Dictionary<int, ConnectionInfo>();
         var table = model.GetTable(HardcodeTablesModel.MapBankTable);
         var mapBanks = new ModelTable(model, table.Start, tokenFactory);
         for (int group = 0; group < mapBanks.Count; group++) {
            var bank = mapBanks[group];
            var maps = bank.GetSubTable("maps");
            for (int map = 0; map < maps.Count; map++) {
               var mapVM = new BlockMapViewModel(fileSystem, tutorials, viewPort, format, eventTemplate, group, map) {
                  allOverworldSprites = allOverworldSprites,
                  BerryInfo = BerryInfo,
               };
               var newInfo = mapVM.CanConnect(info.OppositeDirection);
               if (newInfo != null) options[mapVM.MapID] = newInfo;
            }
         }

         // select which map to add
         var keys = options.Keys.ToList();
         var enumViewModel = new EnumViewModel(keys.Select(key => MapIDToText(model, key)).ToArray());

         tutorials.Complete(Tutorial.RightClick_CreateConnection);
         var option = fileSystem.ShowOptions(
            "Pick a map",
            "Which map do you want to connect to?",
            new[] { new[] { enumViewModel } },
            new VisualOption { Index = 1, Option = "OK", ShortDescription = "Connect Existing Map" });
         if (option == -1) return;
         var choice = keys[enumViewModel.Choice];

         var otherMap = new BlockMapViewModel(fileSystem, tutorials, viewPort, format, eventTemplate, choice / 1000, choice % 1000) {
            allOverworldSprites = allOverworldSprites,
            BerryInfo = BerryInfo,
         };
         var size = GetBlockSize();
         var otherSize = otherMap.GetBlockSize();
         if (info.Direction.IsAny(MapDirection.Left, MapDirection.Right)) {
            info = info with { Offset = info.Offset - (otherSize.height - info.Size) / 2 };
         } else if (info.Direction.IsAny(MapDirection.Up, MapDirection.Down)) {
            info = info with { Offset = info.Offset - (otherSize.width - info.Size) / 2 };
         } else if (info.Direction.IsAny(MapDirection.Dive, MapDirection.Emerge)) {
            info = info with { Offset = 0 };
         }

         var newConnection = AddConnection(info);
         if (newConnection == null) return;
         newConnection.Offset = info.Offset;
         newConnection.Direction = info.Direction;
         newConnection.MapGroup = choice / 1000;
         newConnection.MapNum = choice % 1000;

         info = options[choice] with { Offset = -info.Offset };
         newConnection = otherMap.AddConnection(info);
         newConnection.Offset = info.Offset;
         newConnection.MapGroup = MapID / 1000;
         newConnection.MapNum = MapID % 1000;

         RefreshMapSize();
         NeighborsChanged.Raise(this);
         viewPort.ChangeHistory.ChangeCompleted();
      }

      private void RemoveConnections(IReadOnlyList<int> toRemove) {
         var token = tokenFactory();
         var map = GetMapModel();
         var connections = GetConnections(map, group, this.map);
         var border = GetBorderThickness();
         for (int i = 0; i < toRemove.Count; i++) {
            var connectedMap = GetNeighbor(connections[toRemove[i] - i], border);
            connectedMap.RemoveMatchedConnection(token, this.group, this.map, connections[toRemove[i] - i].Direction.Reverse());

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
         viewPort.ChangeHistory.ChangeCompleted();
      }

      /// <summary>
      /// Remove only this specific connection, because it's pair is being removed.
      /// </summary>
      private void RemoveMatchedConnection(ModelDelta token, int mapGroup, int mapNum, MapDirection direction) {
         // don't remove self-referential connections
         if (mapGroup == group && mapNum == this.map) return;

         var map = GetMapModel();
         var connections = GetConnections(map, group, this.map);
         for (int i = 0; i < connections.Count; i++) {
            if (connections[i].Direction != direction || connections[i].MapGroup != mapGroup || connections[i].MapNum != mapNum) continue;

            for (int j = i + 1; j < connections.Count; j++) {
               connections[j - 1].Direction = connections[j].Direction;
               connections[j - 1].Offset = connections[j].Offset;
               connections[j - 1].MapGroup = connections[j].MapGroup;
               connections[j - 1].MapNum = connections[j].MapNum;
            }

            // doesn't depend on i, but only do these if a match was found
            var connectionsTable = connections[0].Table;
            if (connectionsTable.ElementCount == 1) {
               Erase(connectionsTable, token);
            } else {
               var shorterTable = connectionsTable.Append(token, -1);
               model.ObserveRunWritten(token, shorterTable);
            }
            var connectionsAndCount = map.GetSubTable("connections")[0];
            connectionsAndCount.SetValue("count", connections.Count - 1);
            RefreshMapSize();
            NeighborsChanged.Raise(this);
            break;
         }
      }

      private ConnectionModel AddConnection(ConnectionInfo info) {
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

      private ITableRun GetOrCreateConnections(ModelArrayElement map, ModelDelta token) {
         if (map == null) return null;
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

         return connections;
      }

      private ModelTable CreateEventTable(ModelArrayElement map) {
         // create some blank event data: 0 events for each of the four categories
         var token = tokenFactory();
         var eventAddress = MapRepointer.CreateNewEvents(token);
         model.UpdateArrayPointer(token, map.Table.ElementContent[1], map.Table.ElementContent, 0, map.Start + 4, eventAddress);
         return map.GetSubTable("events");
      }

      public event EventHandler CanEditTilesetChanged;
      public bool CanEditTileset(string type) {
         var model = new MapModel(GetMapModel(), group, map);
         var spriteAddress = model.Layout.PrimaryBlockset.TilesetAddress;
         var paletteAddress = model.Layout.PrimaryBlockset.PaletteAddress;
         if (type == "Secondary") {
            spriteAddress = model.Layout.SecondaryBlockset.TilesetAddress;
            paletteAddress = model.Layout.SecondaryBlockset.PaletteAddress;
         }
         return this.model.GetNextRun(spriteAddress) is ISpriteRun sRun && sRun.Start == spriteAddress &&
            this.model.GetNextRun(paletteAddress) is IPaletteRun pRun && pRun.Start == paletteAddress;
      }
      public void EditTileset(string type) {
         var model = new MapModel(GetMapModel(), group, map);
         if (type == "Primary") {
            ViewPort.Tools.SpriteTool.SpriteAddress = model.Layout.PrimaryBlockset.TilesetAddress;
            ViewPort.Tools.SpriteTool.PaletteAddress = model.Layout.PrimaryBlockset.PaletteAddress;
         } else {
            ViewPort.Tools.SpriteTool.SpriteAddress = model.Layout.SecondaryBlockset.TilesetAddress;
            ViewPort.Tools.SpriteTool.PaletteAddress = model.Layout.SecondaryBlockset.PaletteAddress;
         }

         var newTab = new ImageEditorViewModel(ViewPort.ChangeHistory, this.model, ViewPort.Tools.SpriteTool.SpriteAddress, ViewPort.Save, ViewPort.Tools.SpriteTool.PaletteAddress);
         var args = new TabChangeRequestedEventArgs(newTab);
         ViewPort.MapEditor.RaiseRequestTabChange(args);

         if (newTab.CanEditTilesetWidth) {
            newTab.CurrentTilesetWidth = 16.LimitToRange(newTab.MinimumTilesetWidth, newTab.MaximumTilesetWidth);
         }
      }

      public ObjectEventViewModel CreateObjectEvent(int graphics, int scriptAddress) {
         var token = tokenFactory();
         var map = GetMapModel();
         if (map == null) return null;
         var eventsTable = map.GetSubTable("events");
         if (eventsTable == null) eventsTable = CreateEventTable(map);
         var events = eventsTable[0];
         var element = AddEvent(events, tokenFactory, "objectCount", "objects");
         var targetID = 1;
         var takenIDs = events.TryGetSubTable("objects", out var eventTable) ? eventTable.Select(ev => ev.TryGetValue("id", out int id) ? id : 0).ToHashSet() : new HashSet<int>();
         while (targetID < element.Table.ElementCount && takenIDs.Contains(targetID)) targetID++;
         if (allOverworldSprites == null) allOverworldSprites = RenderOWs(model);
         if (defaultOverworldSprite == null) defaultOverworldSprite = GetDefaultOW(model);
         var newEvent = new ObjectEventViewModel(ViewPort.Tools.CodeTool.ScriptParser, GotoAddress, element, eventTemplate, allOverworldSprites, defaultOverworldSprite, BerryInfo) {
            X = 0, Y = 0,
            Elevation = 0,
            ObjectID = targetID,
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

      public WarpEventViewModel CreateWarpEvent(int bank, int map) {
         var mapModel = GetMapModel();
         if (mapModel == null) return null;
         var events = mapModel.GetSubTable("events")[0];
         var element = AddEvent(events, tokenFactory, "warpCount", "warps");
         var newEvent = new WarpEventViewModel(element, GotoBankMap) { X = 0, Y = 0, Elevation = 0, Bank = bank, Map = map, WarpID = element.ArrayIndex + 1 };
         SelectedEvent = newEvent;
         return newEvent;
      }

      public ScriptEventViewModel CreateScriptEvent() {
         var map = GetMapModel();
         if (map == null) return null;
         var events = map.GetSubTable("events")[0];
         var element = AddEvent(events, tokenFactory, "scriptCount", "scripts");
         var newEvent = new ScriptEventViewModel(GotoAddress, element, eventTemplate) { X = 0, Y = 0, Elevation = 0, Index = 0, Trigger = 0, ScriptAddress = Pointer.NULL };
         SelectedEvent = newEvent;
         return newEvent;
      }

      public SignpostEventViewModel CreateSignpostEvent() {
         var map = GetMapModel();
         if (map == null) return null;
         var events = map.GetSubTable("events")[0];
         var element = AddEvent(events, tokenFactory, "signpostCount", "signposts");
         var newEvent = new SignpostEventViewModel(element, GotoAddress) { X = 0, Y = 0, Elevation = 0, Kind = 0, Pointer = Pointer.NULL };
         SelectedEvent = newEvent;
         return newEvent;
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

      private void Erase(ITableRun table, ModelDelta token) {
         foreach (var source in table.PointerSources) {
            model.ClearPointer(token, source, table.Start);
            model.WritePointer(token, source, Pointer.NULL);
         }
         model.ClearData(token, table.Start, table.Length);
      }

      private void UpdateLayoutID() {
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

      private (int width, int height) GetBlockSize(ModelArrayElement layout = null) {
         var border = GetBorderThickness(layout);
         if (border == null) return (0, 0);
         return (pixelWidth / 16 - border.West - border.East, pixelHeight / 16 - border.North - border.South);
      }

      private BlockMapViewModel GetNeighbor(ConnectionModel connection, Border border) {
         var vm = new BlockMapViewModel(fileSystem, tutorials, viewPort, format, eventTemplate, connection.MapGroup, connection.MapNum) {
            IncludeBorders = IncludeBorders,
            SpriteScale = SpriteScale,
            allOverworldSprites = allOverworldSprites,
            BerryInfo = BerryInfo,
            CollisionHighlight = CollisionHighlight,
            ShowEvents = ShowEvents
         };
         var (n, _, _, w) = vm.GetBorderThickness();
         vm.TopEdge = TopEdge + (connection.Offset + border.North - n) * (int)(16 * SpriteScale);
         vm.LeftEdge = LeftEdge + (connection.Offset + border.West - w) * (int)(16 * SpriteScale);
         if (connection.Direction == MapDirection.Left) vm.LeftEdge = LeftEdge - (int)(vm.PixelWidth * SpriteScale);
         if (connection.Direction == MapDirection.Right) vm.LeftEdge = LeftEdge + (int)(PixelWidth * SpriteScale);
         if (connection.Direction == MapDirection.Up) vm.TopEdge = TopEdge - (int)(vm.PixelHeight * SpriteScale);
         if (connection.Direction == MapDirection.Down) vm.TopEdge = TopEdge + (int)(PixelHeight * SpriteScale);
         vm.ZIndex = ZIndex;
         if (connection.Direction.IsAny(MapDirection.Dive, MapDirection.Emerge)) vm.ZIndex = ZIndex - 1;
         return vm;
      }

      private void RefreshPaletteCache(ModelArrayElement layout = null, BlocksetModel blockModel1 = null, BlocksetModel blockModel2 = null) {
         if (blockModel1 == null || blockModel2 == null) {
            if (layout == null) layout = GetLayout();
            if (blockModel1 == null) blockModel1 = new BlocksetModel(model, layout.GetAddress(Format.PrimaryBlockset));
            if (blockModel2 == null) blockModel2 = new BlocksetModel(model, layout.GetAddress(Format.SecondaryBlockset));
         }

         palettes = BlockmapRun.ReadPalettes(blockModel1, blockModel2, PrimaryPalettes);
         blockEditor?.RefreshPaletteCache(palettes);
      }

      private void RefreshTileCache(ModelArrayElement layout = null, BlocksetModel blockModel1 = null, BlocksetModel blockModel2 = null) {
         if (blockModel1 == null || blockModel2 == null) {
            if (layout == null) layout = GetLayout();
            if (blockModel1 == null) blockModel1 = new BlocksetModel(model, layout.GetAddress("blockdata1"));
            if (blockModel2 == null) blockModel2 = new BlocksetModel(model, layout.GetAddress("blockdata2"));
         }

         tiles = BlockmapRun.ReadTiles(blockModel1, blockModel2, PrimaryTiles);
         blockEditor?.RefreshTileCache(tiles);
      }

      private void RefreshBlockCache(ModelArrayElement layout = null, BlocksetModel blockModel1 = null, BlocksetModel blockModel2 = null) {
         if (layout == null) layout = GetLayout();
         if (blockModel1 == null || blockModel2 == null) {
            if (blockModel1 == null) blockModel1 = new BlocksetModel(model, layout.GetAddress(Format.PrimaryBlockset));
            if (blockModel2 == null) blockModel2 = new BlocksetModel(model, layout.GetAddress(Format.SecondaryBlockset));
         }
         int width = layout.GetValue("width"), height = layout.GetValue("height");
         int start = layout.GetAddress(Format.BlockMap);
         var maxUsedPrimary = BlockmapRun.GetMaxUsedBlock(model, start, width, height, PrimaryBlocks);
         var maxUsedSecondary = BlockmapRun.GetMaxUsedBlock(model, start, width, height, 1024) - PrimaryBlocks;

         blocks = BlockmapRun.ReadBlocks(maxUsedPrimary, maxUsedSecondary, blockModel1, blockModel2);
         blockEditor?.RefreshBlockCache(blocks);
      }

      private void RefreshBlockAttributeCache(ModelArrayElement layout = null, BlocksetModel blockModel1 = null, BlocksetModel blockModel2 = null) {
         if (blockModel1 == null || blockModel2 == null) {
            if (layout == null) layout = GetLayout();
            if (layout == null) return;
            if (blockModel1 == null) blockModel1 = new BlocksetModel(model, layout.GetAddress("blockdata1"));
            if (blockModel2 == null) blockModel2 = new BlocksetModel(model, layout.GetAddress("blockdata2"));
         }
         int width = layout.GetValue("width"), height = layout.GetValue("height");
         int start = layout.GetAddress(Format.BlockMap);
         var maxUsedPrimary = BlockmapRun.GetMaxUsedBlock(model, start, width, height, PrimaryBlocks);
         var maxUsedSecondary = BlockmapRun.GetMaxUsedBlock(model, start, width, height, 1024) - PrimaryBlocks;

         blockAttributes = BlockmapRun.ReadBlockAttributes(maxUsedPrimary, maxUsedSecondary, blockModel1, blockModel2);
         blockEditor?.RefreshBlockAttributeCache(blockAttributes);
      }

      private void RefreshBlockRenderCache(ModelArrayElement layout = null, BlocksetModel blockModel1 = null, BlocksetModel blockModel2 = null) {
         if (blocks == null || tiles == null || palettes == null) {
            if (layout == null) layout = GetLayout();
            if (layout == null) return;
            if (blockModel1 == null) blockModel1 = new BlocksetModel(model, layout.GetAddress(Format.PrimaryBlockset));
            if (blockModel2 == null) blockModel2 = new BlocksetModel(model, layout.GetAddress(Format.SecondaryBlockset));
         }

         lock (blockRenders) {
            if (blocks == null) RefreshBlockCache(layout, blockModel1, blockModel2);
            if (blockAttributes == null) RefreshBlockAttributeCache(layout, blockModel1, blockModel2);
            if (tiles == null) RefreshTileCache(layout, blockModel1, blockModel2);
            if (palettes == null) RefreshPaletteCache(layout, blockModel1, blockModel2);
            blockRenders.Clear();
            if (blocks != null && tiles != null && palettes != null) {
               blockRenders.AddRange(BlockmapRun.CalculateBlockRenders(blocks, blockAttributes, tiles, palettes));
            }
         }
      }

      private void RefreshMapSize(bool clearPixels = true) {
         var layout = GetLayout();
         if (layout == null) return;
         var (width, height) = (layout.GetValue("width"), layout.GetValue("height"));
         var border = GetBorderThickness(layout);
         (pixelWidth, pixelHeight) = ((width + border.West + border.East) * 16, (height + border.North + border.South) * 16);
         if (clearPixels) ClearPixelCache();
      }

      private void RefreshMapEvents(ModelArrayElement layout) {
         if (eventRenders != null) return;
         var layoutModel = new LayoutModel(layout);
         var list = new List<IEventViewModel>();
         var events = GetEvents();
         foreach (var obj in events) {
            obj.Render(model, layoutModel);
            list.Add(obj);
         }
         eventRenders = list;
      }

      private void FillMapPixelData() {
         var layout = GetLayout();
         if (layout == null) return;
         lock (blockRenders) {
            if (blockRenders.Count == 0) RefreshBlockRenderCache(layout);
         }
         var borderBlockCopy = borderBlock; // race condition: borderblock might be erased while we're drawing
         if (borderBlockCopy == null) borderBlockCopy = RefreshBorderRender();
         var (width, height) = (layout.GetValue("width"), layout.GetValue("height"));
         var border = GetBorderThickness(layout);
         var start = layout.GetAddress("blockmap");
         var blockHighlight = blockEditor?.BlockIndex ?? -1;
         var selectedEvent = this.selectedEvent; // threading protection

         var canvas = new CanvasPixelViewModel(pixelWidth, pixelHeight);
         var (borderWidth, borderHeight) = (borderBlockCopy.PixelWidth / 16, borderBlockCopy.PixelHeight / 16);
         for (int y = 0; y < height + border.North + border.South; y++) {
            for (int x = 0; x < width + border.West + border.East; x++) {
               if (y < border.North || x < border.West || y >= border.North + height || x >= border.West + width) {
                  var (xEdge, yEdge) = (x - border.West - width, y - border.North - height);
                  var (rightEdge, bottomEdge) = (xEdge >= 0, yEdge >= 0);
                  // top/left
                  if (!rightEdge && !bottomEdge && x % borderWidth == 0 && y % borderHeight == 0) canvas.Draw(borderBlockCopy, x * 16, y * 16);
                  // right edge
                  if (rightEdge && !bottomEdge && xEdge % borderWidth == 0 && y % borderHeight == 0) canvas.Draw(borderBlockCopy, x * 16, y * 16);
                  // bottom edge
                  if (!rightEdge && bottomEdge && x % borderWidth == 0 && yEdge % borderHeight == 0) canvas.Draw(borderBlockCopy, x * 16, y * 16);
                  // bottom right corner
                  if (rightEdge && bottomEdge && xEdge % borderWidth == 0 && yEdge % borderHeight == 0) canvas.Draw(borderBlockCopy, x * 16, y * 16);
                  continue;
               }
               var data = model.ReadMultiByteValue(start + ((y - border.North) * width + x - border.West) * 2, 2);
               var collision = data >> 10;
               data &= 0x3FF;
               lock (blockRenders) {
                  if (blockRenders.Count > data) canvas.Draw(blockRenders[data], x * 16, y * 16);
               }
               if (showEvents != MapDisplayOptions.NoEvents) {
                  if (collision == collisionHighlight) HighlightCollision(canvas.PixelData, x * 16, y * 16);
                  if (collisionHighlight == -1 && selectedEvent is ObjectEventViewModel obj && obj.ShouldHighlight(x - border.West, y - border.North)) {
                     HighlightCollision(canvas.PixelData, x * 16, y * 16);
                  }
                  if (collisionHighlight != -1 && blockHighlight != -1 && collision != collisionHighlight && data == blockHighlight) {
                     // this matches the chosen block, but not the chosen collision
                     HighlightBlock(canvas.PixelData, x * 16, y * 16);
                  }
               }
            }
         }

         // draw the box for the selected event
         var gray = UncompressedPaletteColor.Pack(6, 6, 6);
         if (selectedEvent != null && selectedEvent.X >= 0 && selectedEvent.X < width && selectedEvent.Y >= 0 && selectedEvent.Y < height) {
            canvas.DrawBox((selectedEvent.X + border.West) * 16, (selectedEvent.Y + border.North) * 16, 16, gray);
         }

         // now draw the events on top
         if (showEvents != MapDisplayOptions.NoEvents) {
            if (eventRenders == null) RefreshMapEvents(layout);
            if (eventRenders != null) {
               foreach (var obj in eventRenders) {
                  if (obj.EventRender != null) {
                     if (obj is ObjectEventViewModel || showEvents == MapDisplayOptions.AllEvents) {
                        var (x, y) = ((obj.X + border.West) * 16 + obj.LeftOffset, (obj.Y + border.North) * 16 + obj.TopOffset);
                        canvas.Draw(obj.EventRender, x, y);
                     }
                  }
               }
            }
         }

         // finally, draw a one-pixel border around the entire map (but not the border blocks)
         if (isSelected) {
            var (borderW, borderH) = (border.West + border.East, border.North + border.South);
            canvas.DarkenRect(border.West * 16, border.North * 16, pixelWidth - borderW * 16, pixelHeight - borderH * 16, 12);
         }

         pixelData = canvas.PixelData;
      }

      private void HighlightCollision(short[] pixelData, int x, int y) {
         new CanvasPixelViewModel(PixelWidth, PixelHeight, pixelData).DarkenRect(x, y, 16, 16, 8);
      }

      private void HighlightBlock(short[] pixelData, int x, int y) {
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
      private void FillBlockPixelData() {
         var layout = GetLayout();
         lock (blockRenders) {
            if (blockRenders.Count == 0) RefreshBlockRenderCache(layout);
         }

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

      private IPixelViewModel RefreshBorderRender(ModelArrayElement layout = null) {
         if (layout == null) layout = GetLayout();
         lock (blockRenders) {
            if (blockRenders.Count == 0) RefreshBlockRenderCache(layout);
         }
         var width = layout.HasField("borderwidth") ? layout.GetValue("borderwidth") : 2;
         var height = layout.HasField("borderheight") ? layout.GetValue("borderheight") : 2;

         var start = layout.GetAddress("borderblock");
         var canvas = new CanvasPixelViewModel(width * 16, height * 16);
         for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
               var data = model.ReadMultiByteValue(start + (y * width + x) * 2, 2);
               data &= 0x3FF;
               lock (blockRenders) {
                  if (!data.InRange(0, blockRenders.Count)) continue; // can't draw this block. Transient race condition?
                  canvas.Draw(blockRenders[data], x * 16, y * 16);
               }
            }
         }

         return BorderBlock = canvas;
      }

      private ModelArrayElement GetMapModel() => GetMapModel(model, group, map, tokenFactory);
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

      private IReadOnlyList<ConnectionModel> GetConnections() {
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

      private IPixelViewModel defaultOverworldSprite;
      private IReadOnlyList<IPixelViewModel> allOverworldSprites;
      public IReadOnlyList<IPixelViewModel> AllOverworldSprites {
         get {
            if (allOverworldSprites == null) allOverworldSprites = RenderOWs(model);
            return allOverworldSprites;
         }
         init => allOverworldSprites = value;
      }
      public static IPixelViewModel GetDefaultOW(IDataModel model) {
         var defaultSpriteAddress = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, HardcodeTablesModel.PokeIconsTable + "/0/icon/");
         var defaultSpriteRun = model.GetNextRun(defaultSpriteAddress) as ISpriteRun;
         var defaultImage = defaultSpriteRun == null ? new ReadonlyPixelViewModel(16, 16) : model.CurrentCacheScope.GetImage(defaultSpriteRun);
         if (defaultImage.PixelHeight > 24) {
            var canvas = new CanvasPixelViewModel(defaultImage.PixelWidth, 24) { Transparent = defaultImage.PixelData[0] };
            canvas.Draw(defaultImage, 0, 24 - Math.Min(32, defaultImage.PixelHeight));
            defaultImage = canvas;
         }
         return defaultImage;
      }
      public static List<IPixelViewModel> RenderOWs(IDataModel model) {
         var list = new List<IPixelViewModel>();
         var run = model.GetTable(HardcodeTablesModel.OverworldSprites);
         var ows = run == null ? null : new ModelTable(model, run.Start, null, run);
         var defaultImage = GetDefaultOW(model);
         for (int i = 0; i < (ows?.Count ?? 1); i++) {
            list.Add(ObjectEventViewModel.Render(model, ows, defaultImage, i, -1));
         }
         return list;
      }

      public EventGroupModel EventGroup {
         get {
            if (allOverworldSprites == null) allOverworldSprites = RenderOWs(model);
            if (defaultOverworldSprite == null) defaultOverworldSprite = GetDefaultOW(model);
            var map = GetMapModel();
            var eventsTable = map.GetSubTable("events");
            if (eventsTable == null) return null;
            var eventElements = eventsTable[0];
            if (eventElements == null) return null;
            var events = new EventGroupModel(ViewPort.Tools.CodeTool.ScriptParser, GotoAddress, GotoBankMap, eventElements, eventTemplate, allOverworldSprites, defaultOverworldSprite, BerryInfo, group, this.map);
            events.DataMoved += HandleEventDataMoved;
            return events;
         }
      }

      private IReadOnlyList<IEventViewModel> GetEvents() {
         var results = new List<IEventViewModel>();
         var events = EventGroup;
         if (events == null) return results;
         results.AddRange(events.Objects);
         results.AddRange(events.Warps);
         results.AddRange(events.Scripts);
         results.AddRange(events.Signposts);
         results.AddRange(events.FlyEvents);
         return results;
      }

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

      private ConnectionInfo CanConnect(MapDirection direction) {
         var connections = GetConnections();
         var (width, height) = (pixelWidth / 16, pixelHeight / 16);
         var dimensionLength = (direction switch {
            MapDirection.Up => width,
            MapDirection.Down => width,
            MapDirection.Left => height,
            MapDirection.Right => height,
            MapDirection.Dive => 0,
            MapDirection.Emerge => 0,
            _ => throw new NotImplementedException(),
         });
         var availableSpace = dimensionLength.Range().ToList();

         // can't add a connection where there already is one
         for (int i = 0; i < (connections?.Count ?? 0); i++) {
            if (connections[i].Direction != direction) continue;
            if (direction == MapDirection.Up || direction == MapDirection.Down) {
               var map = new BlockMapViewModel(fileSystem, tutorials, viewPort, format, eventTemplate, connections[i].MapGroup, connections[i].MapNum) {
                  allOverworldSprites = allOverworldSprites,
                  BerryInfo = BerryInfo,
               };
               var removeWidth = map.pixelWidth / 16;
               var removeOffset = connections[i].Offset;
               foreach (int j in removeWidth.Range()) availableSpace.Remove(j + removeOffset);
            } else if (direction == MapDirection.Left || direction == MapDirection.Right) {
               var map = new BlockMapViewModel(fileSystem, tutorials, viewPort, format, eventTemplate, connections[i].MapGroup, connections[i].MapNum) {
                  allOverworldSprites = allOverworldSprites,
                  BerryInfo = BerryInfo,
               };
               var removeHeight = map.pixelHeight / 16;
               var removeOffset = connections[i].Offset;
               foreach (int j in removeHeight.Range()) availableSpace.Remove(j + removeOffset);
            } else if (direction.IsAny(MapDirection.Dive, MapDirection.Emerge)) {
               // can't dive or emerge to a map that already has a dive/emerge
               var map = AllMapsModel.Create(model, tokenFactory)[connections[i].MapGroup][connections[i].MapNum];
               if (map.Connections.Any(c => c.Direction.IsAny(MapDirection.Emerge, MapDirection.Dive))) return null;
            }
         }

         if (direction.IsAny(MapDirection.Dive, MapDirection.Emerge)) {
            var layout = AllMapsModel.Create(model, tokenFactory)[group][map].Layout;
            return new ConnectionInfo(layout.Width, layout.Height, direction);
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

      private void GotoAddress(int address) => GotoAddress(viewPort, address);
      private void GotoBankMap(int bank, int map) => viewPort.MapEditor.NavigateTo(bank, map, int.MinValue, int.MinValue);

      /// <summary>
      /// Wrapper around standard viewPort.Goto that also formats the script when you do the goto.
      /// </summary>
      public static void GotoAddress(IEditableViewPort viewPort, int address) {
         var nextRun = viewPort.Model.GetNextRun(address);
         var tool = viewPort.Tools.CodeTool;
         if (nextRun.Start > address) {
            viewPort.Tools.SelectedTool = tool;
            tool.Mode = CodeMode.Script;
         } else if (nextRun.Start == address && nextRun is XSERun) {
            tool.ScriptParser.FormatScript<XSERun>(new NoDataChangeDeltaModel(), viewPort.Model, address);
         }
         viewPort.GotoScript(address);
      }

      private void HandleBlocksChanged(object sender, byte[][] blocks) {
         var layout = GetLayout();
         var layoutModel = new LayoutModel(layout);

         if (model.GetNextRun(layoutModel.BlockMap.Start) is BlockmapRun blockmapRun) {
            var blockModel1 = layoutModel.PrimaryBlockset.FullBlocksetModel;
            var blockModel2 = layoutModel.SecondaryBlockset.FullBlocksetModel;
            var primaryMax = BlockmapRun.GetMaxUsedBlock(model, blockmapRun.Start, blockmapRun.BlockWidth, blockmapRun.BlockHeight, blockmapRun.PrimaryBlocks);
            var secondaryMax = BlockmapRun.GetMaxUsedBlock(model, blockmapRun.Start, blockmapRun.BlockWidth, blockmapRun.BlockHeight, 1024) - blockmapRun.PrimaryBlocks;
            primaryMax = Math.Max(primaryMax, mapRepointer.EstimateBlockCount(layout, true).currentCount);
            secondaryMax = Math.Max(secondaryMax, mapRepointer.EstimateBlockCount(layout, false).currentCount);
            BlockmapRun.WriteBlocks(tokenFactory, primaryMax, secondaryMax, blockModel1, blockModel2, blocks);
         }

         viewPort.ChangeHistory.ChangeCompleted();
         RequestClearMapCaches.Raise(this);
      }

      private void HandleBorderChanged(object sender, EventArgs e) {
         RequestClearMapCaches.Raise(this);
      }

      private void HandleBlockAttributesChanged(object sender, byte[][] attributes) {
         var layout = GetLayout();
         var layoutModel = new LayoutModel(layout);

         if (model.GetNextRun(layoutModel.BlockMap.Start) is BlockmapRun blockmapRun) {
            var blockModel1 = layoutModel.PrimaryBlockset.FullBlocksetModel;
            var blockModel2 = layoutModel.SecondaryBlockset.FullBlocksetModel;
            var primaryMax = BlockmapRun.GetMaxUsedBlock(model, blockmapRun.Start, blockmapRun.BlockWidth, blockmapRun.BlockHeight, blockmapRun.PrimaryBlocks);
            var secondaryMax = BlockmapRun.GetMaxUsedBlock(model, blockmapRun.Start, blockmapRun.BlockWidth, blockmapRun.BlockHeight, 1024) - blockmapRun.PrimaryBlocks;
            primaryMax = Math.Max(primaryMax, mapRepointer.EstimateBlockCount(layout, true).currentCount);
            secondaryMax = Math.Max(secondaryMax, mapRepointer.EstimateBlockCount(layout, false).currentCount);
            BlockmapRun.WriteBlockAttributes(tokenFactory, primaryMax, secondaryMax, blockModel1, blockModel2, attributes);
         }

         viewPort.ChangeHistory.ChangeCompleted();
         RequestClearMapCaches.Raise(this);
      }

      private void HandleAutoscrollTiles(object sender, EventArgs e) => AutoscrollTiles.Raise(this);

      private void HandleEventDataMoved(object sender, DataMovedEventArgs e) => InformRepoint(e);

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
      private bool isSelected;
      public bool IsSelected { get => isSelected; set => Set(ref isSelected, value); }

      private int index;
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
      private readonly ModelArrayElement connection;
      private readonly int sourceGroup, sourceMap;
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
         return neighbors.FirstOrDefault(c => c.MapGroup == sourceGroup && c.MapNum == sourceMap && c.Direction == direction);
      }

      public void Clear(IDataModel model, ModelDelta token) {
         token.ChangeData(model, connection.Start, connection.Length.Range(i => (byte)0xFF).ToList());
      }
   }

   public class EventGroupModel {
      private readonly ModelArrayElement events;

      public event EventHandler<DataMovedEventArgs> DataMoved;

      public EventGroupModel(ScriptParser parser, Action<int> gotoAddress, Action<int, int> gotoBankMap, ModelArrayElement events, EventTemplate eventTemplate, IReadOnlyList<IPixelViewModel> ows, IPixelViewModel defaultOW, BerryInfo berries, int bank, int map) {
         this.events = events;

         var objectCount = events.GetValue("objectCount");
         var objects = events.GetSubTable("objects");
         var objectList = new List<ObjectEventViewModel>();
         if (objects != null) {
            for (int i = 0; i < objectCount; i++) {
               var newEvent = new ObjectEventViewModel(parser, gotoAddress, objects[i], eventTemplate, ows, defaultOW, berries);
               newEvent.DataMoved += (sender, e) => DataMoved.Raise(this, e);
               objectList.Add(newEvent);
            }
         }
         Objects = objectList;

         var warpCount = events.GetValue("warpCount");
         var warps = events.GetSubTable("warps");
         var warpList = new List<WarpEventViewModel>();
         if (warps != null) {
            for (int i = 0; i < warpCount; i++) warpList.Add(new WarpEventViewModel(warps[i], gotoBankMap));
         }
         Warps = warpList;

         var scriptCount = events.GetValue("scriptCount");
         var scripts = events.GetSubTable("scripts");
         var scriptList = new List<ScriptEventViewModel>();
         if (scripts != null) {
            for (int i = 0; i < scriptCount; i++) scriptList.Add(new ScriptEventViewModel(gotoAddress, scripts[i], eventTemplate));
         }
         Scripts = scriptList;

         var signpostCount = events.GetValue("signpostCount");
         var signposts = events.GetSubTable("signposts");
         var signpostList = new List<SignpostEventViewModel>();
         if (signposts != null) {
            for (int i = 0; i < signpostCount; i++) {
               var newEvent = new SignpostEventViewModel(signposts[i], gotoAddress);
               newEvent.DataMoved += (sender, e) => DataMoved.Raise(this, e);
               signpostList.Add(newEvent);
            }
         }
         Signposts = signpostList;

         var flyList = new List<FlyEventViewModel>();
         foreach (var flyEvent in FlyEventViewModel.Create(events.Model, bank, map, () => events.Token)) {
            if (flyEvent.Valid) flyList.Add(flyEvent);
         }
         FlyEvents = flyList;
      }

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
