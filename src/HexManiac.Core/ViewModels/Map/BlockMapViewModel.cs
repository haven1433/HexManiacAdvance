using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Code;
using HavenSoft.HexManiac.Core.Models.Map;
using HavenSoft.HexManiac.Core.Models.Runs;
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
   public class BlockMapViewModel : ViewModelCore, IPixelViewModel {
      private readonly Format format;
      private readonly IFileSystem fileSystem;
      private readonly MapTutorialsViewModel tutorials;
      private readonly IEditableViewPort viewPort;
      private readonly IDataModel model;
      private readonly Func<ModelDelta> tokenFactory;
      private readonly int group, map;

      private int PrimaryTiles { get; }
      private int PrimaryBlocks { get; }
      private int TotalBlocks => 1024;
      private int PrimaryPalettes { get; } // 7

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
      private IReadOnlyList<IEventViewModel> eventRenders;
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
               availableNames.Add(SanitizeName(name.Trim('"')));
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

      public static string SanitizeName(string name) {
         return name.Replace("\\CC0000", " ");
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
               blockEditor.Bind(nameof(blockEditor.ShowTiles), (editor, args) => BorderEditor.ShowBorderPanel &= !editor.ShowTiles);
               blockEditor.Bind(nameof(blockEditor.BlockIndex), (editor, args) => lastDrawX = lastDrawY = -1);
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

      public BlockMapViewModel(IFileSystem fileSystem, MapTutorialsViewModel tutorials, IEditableViewPort viewPort, Format format, int group, int map) {
         this.format = format;
         this.fileSystem = fileSystem;
         this.tutorials = tutorials;
         this.viewPort = viewPort;
         this.model = viewPort.Model;
         this.tokenFactory = () => viewPort.ChangeHistory.CurrentChange;
         (this.group, this.map) = (group, map);
         Transparent = -1;
         var mapModel = GetMapModel();
         Header = new(mapModel, format, tokenFactory);
         Header.Bind(nameof(Header.PrimaryIndex), (sender, e) => ClearCaches());
         Header.Bind(nameof(Header.SecondaryIndex), (sender, e) => ClearCaches());
         RefreshMapSize();
         PrimaryTiles = PrimaryBlocks = model.IsFRLG() ? 640 : 512;
         PrimaryPalettes = model.IsFRLG() ? 7 : 6;

         (LeftEdge, TopEdge) = (-PixelWidth / 2, -PixelHeight / 2);

         mapScriptCollection = new(viewPort);
         mapScriptCollection.NewMapScriptsCreated += (sender, e) => GetMapModel().SetAddress("mapscripts", e.Address);

         mapRepointer = new MapRepointer(format, fileSystem, viewPort, viewPort.ChangeHistory, MapID);
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
         borderBlock = null;
         berryInfo = null;
         WildPokemon.ClearCache();
         RefreshMapSize();
         if (blockEditor != null) {
            blockEditor.BlocksChanged -= HandleBlocksChanged;
            blockEditor.BlockAttributesChanged -= HandleBlockAttributesChanged;
            BlockEditor.AutoscrollTiles -= HandleAutoscrollTiles;
            var oldBlockEditor = blockEditor;
            blockEditor = null;
            BlockEditor.BlockIndex = oldBlockEditor.BlockIndex;
            (BlockEditor.TileSelectionX, BlockEditor.TileSelectionY) = (oldBlockEditor.TileSelectionX, oldBlockEditor.TileSelectionY);
            BlockEditor.PaletteSelection = oldBlockEditor.PaletteSelection;
            BlockEditor.ShowTiles = oldBlockEditor.ShowTiles;
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
         NotifyPropertiesChanged(nameof(BlockRenders), nameof(BlockPixels), nameof(BerryInfo));
         if (SelectedEvent != null) CycleActiveEvent(default, EventCycleDirection.None);
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
            if (map.Layout.PrimaryBlockset.Start != primaryBlocksetAddress && blockIndex < PrimaryBlocks) continue;
            if (map.Layout.SecondaryBlockset.Start != secondaryBlocksetAddress && blockIndex >= PrimaryBlocks) continue;
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
               var targetLocation = targetMapName.Split('(')[0];
               var targetName = '(' + targetMapName.Split('(')[1];
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
         returnWarp.WarpID = GetEvents().Where(e => e is WarpEventViewModel).Until(e => e.Equals(warp)).Count();
         (returnWarp.X, returnWarp.Y) = (4, 8);
         if (!warpIsBottomSquare) (returnWarp.X, returnWarp.Y) = (4, 7);

         // repoint border block
         newMap.MapRepointer.RepointBorderBlock.Execute();

         return newMap;
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
         var map0 = warps[0].TargetMap;
         var matchingWarp0 = map0.Events.Warps[warps[0].WarpID];
         var warpIsAgainstWall = matchingWarp0.Y == map0.Layout.Height - 1;
         if (warpIsAgainstWall) {
            blockMap[3, 7] = map0.Blocks[matchingWarp0.X - 1, matchingWarp0.Y - 1].Tile;
            blockMap[4, 7] = map0.Blocks[matchingWarp0.X, matchingWarp0.Y - 1].Tile;
            blockMap[5, 7] = map0.Blocks[matchingWarp0.X + 1, matchingWarp0.Y - 1].Tile;

            blockMap[3, 8] = map0.Blocks[matchingWarp0.X - 1, matchingWarp0.Y].Tile;
            blockMap[4, 8] = map0.Blocks[matchingWarp0.X, matchingWarp0.Y].Tile;
            blockMap[5, 8] = map0.Blocks[matchingWarp0.X + 1, matchingWarp0.Y].Tile;
         } else {
            blockMap[3, 7] = map0.Blocks[matchingWarp0.X - 1, matchingWarp0.Y].Tile;
            blockMap[4, 7] = map0.Blocks[matchingWarp0.X, matchingWarp0.Y].Tile;
            blockMap[5, 7] = map0.Blocks[matchingWarp0.X + 1, matchingWarp0.Y].Tile;

            blockMap[3, 8] = map0.Blocks[matchingWarp0.X - 1, matchingWarp0.Y + 1].Tile;
            blockMap[4, 8] = map0.Blocks[matchingWarp0.X, matchingWarp0.Y + 1].Tile;
            blockMap[5, 8] = map0.Blocks[matchingWarp0.X + 1, matchingWarp0.Y + 1].Tile;
         }

         // draw the map
         var viewModel = new BlockMapViewModel(fileSystem, tutorials, viewPort, format, warps[0].Bank, warps[0].Map) { BerryInfo = BerryInfo };
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
         var border = GetBorderThickness(layout);
         var (xx, yy) = ((int)x - border.West, (int)y - border.North);
         DrawBlock(token, blockIndex, collisionIndex, xx, yy);
      }

      public void DrawBlock(ModelDelta token, int blockIndex, int collisionIndex, int xx, int yy) {
         var layout = GetLayout();
         var (width, height) = (layout.GetValue("width"), layout.GetValue("height"));
         var border = GetBorderThickness(layout);

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
         if (updateHighlight && xx < pixelWidth && yy < pixelHeight) HighlightCollision(pixelData, xx, yy);
         if (updateBlock || updateHighlight) NotifyPropertyChanged(nameof(PixelData));
         tutorials.Complete(Tutorial.LeftClickMap_DrawBlock);
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

      public void RepeatBlock(Func<ModelDelta> futureToken, int block, int collision, int x, int y, int w, int h) {
         var layout = GetLayout();
         var (width, height) = (layout.GetValue("width"), layout.GetValue("height"));
         var start = layout.GetAddress("blockmap");
         int changeCount = 0;
         for (int xx = 0; xx < w; xx++) {
            for (int yy = 0; yy < h; yy++) {
               if (x + xx < 0 || y + yy < 0 || x + xx >= width || y + yy >= height) continue;
               var address = start + ((yy + y) * width + xx + x) * 2;
               // var block = blockValues[xx % blockValues.GetLength(0), yy % blockValues.GetLength(1)];
               var blockValue = model.ReadMultiByteValue(address, 2);
               var originalBlockValue = blockValue;
               if (block >= 0) blockValue = (blockValue & 0xFC00) + block;
               if (collision >= 0) blockValue = (blockValue & 0x3FF) + (collision << 10);
               if (blockValue != originalBlockValue) {
                  model.WriteMultiByteValue(address, 2, futureToken(), blockValue);
                  changeCount++;
               }
            }
         }
         if (changeCount > 0) {
            pixelData = null;
            NotifyPropertyChanged(nameof(PixelData));
         }
      }

      public void RepeatBlocks(Func<ModelDelta> futureToken, int[,] blockValues, int x, int y, int w, int h) {
         var layout = GetLayout();
         var (width, height) = (layout.GetValue("width"), layout.GetValue("height"));
         var start = layout.GetAddress("blockmap");
         int changeCount = 0;
         for (int xx = 0; xx < w; xx++) {
            for (int yy = 0; yy < h; yy++) {
               if (x + xx < 0 || y + yy < 0 || x + xx >= width || y + yy >= height) continue;
               var address = start + ((yy + y) * width + xx + x) * 2;
               var block = blockValues[xx % blockValues.GetLength(0), yy % blockValues.GetLength(1)];
               if (model.ReadMultiByteValue(address, 2) != block) {
                  model.WriteMultiByteValue(address, 2, futureToken(), block);
                  changeCount++;
               }
            }
         }
         if (changeCount > 0) {
            pixelData = null;
            NotifyPropertyChanged(nameof(PixelData));
         }
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

      public void UpdateEventLocation(IEventViewModel ev, double x, double y) {
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

      public IEventViewModel EventUnderCursor(double x, double y, bool autoSelect = true) {
         var layout = GetLayout();
         var border = GetBorderThickness(layout);
         var tileX = (int)((x - LeftEdge) / SpriteScale / 16) - border.West;
         var tileY = (int)((y - TopEdge) / SpriteScale / 16) - border.North;
         IEventViewModel last = null;
         foreach (var e in GetEvents()) {
            if (e.X == tileX && e.Y == tileY) last = e;
         }
         if (autoSelect) SelectedEvent = last;
         pixelData = null;
         NotifyPropertyChanged(nameof(PixelData));
         return last;
      }

      public IPixelViewModel AutoCrop(int warpID) {
         const int SizeX = 7, SizeY = 7;
         var map = GetMapModel();
         var layout = GetLayout(map);
         var (width, height) = (layout.GetValue("width"), layout.GetValue("height"));
         var events = new EventGroupModel(ViewPort.Tools.CodeTool.ScriptParser, GotoAddress, map.GetSubTable("events")[0], allOverworldSprites, BerryInfo, group, this.map);
         if (events.Warps.Count <= warpID) return null;
         var warp = events.Warps[warpID];
         var startX = warp.X - SizeX / 2;
         var startY = warp.Y - SizeY / 2;
         while (startX < 0) startX += 1;
         while (startY < 0) startY += 1;
         while (startX + SizeX > width) startX--;
         while (startY + SizeY > height) startY--;

         return ReadonlyPixelViewModel.Crop(this, startX * 16, startY * 16, SizeX * 16, SizeY * 16);
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
         foreach (var bank in model.GetTableModel(HardcodeTablesModel.MapBankTable)) {
            if (bank == null) continue;
            foreach (var mapList in bank.GetSubTable("maps")) {
               if (mapList == null) continue;
               var mapTable = mapList.GetSubTable("map");
               if (mapTable == null) continue;
               var map = mapTable[0];
               yield return new(map);
            }
         }
      }

      private void ResizeMapData(MapDirection direction, int amount) {
         if (amount == 0) return;
         var token = tokenFactory();
         var map = GetMapModel();
         var layout = GetLayout(map);
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

      private void ConnectNewMap(ConnectionInfo info) {
         var token = tokenFactory();
         var mapBanks = new ModelTable(model, model.GetTable(HardcodeTablesModel.MapBankTable).Start, tokenFactory);
         tutorials.Complete(Tutorial.RightClick_CreateConnection);
         var option = MapRepointer.GetMapBankForNewMap(
            "Maps are organized into banks. The game doesn't care, so you can use the banks however you like."
            + Environment.NewLine +
            "Which map bank do you want to use for the new map?");
         if (option == -1) return;

         var map = GetMapModel();
         var connectionsAndCount = map.GetSubTable("connections")[0];
         var connections = connectionsAndCount.GetSubTable("connections").Run;
         var originalConnectionStart = connections.Start;
         connections = model.RelocateForExpansion(token, connections, connections.Length + connections.ElementLength);
         if (connections.Start != originalConnectionStart) InformRepoint(new("Connections", connections.Start));
         connectionsAndCount.SetValue("count", connections.ElementCount + 1);
         var table = new ModelTable(model, connections.Start, tokenFactory, connections);
         var newConnection = new ConnectionModel(table[connections.ElementCount], group, this.map);
         newConnection.Offset = info.Offset;
         newConnection.Direction = info.Direction;

         var otherMap = CreateNewMap(token, option, info.Size, info.Size);

         newConnection.MapGroup = otherMap.group;
         newConnection.MapNum = otherMap.map;
         info = new ConnectionInfo(info.Size, -info.Offset, info.OppositeDirection);
         newConnection = otherMap.AddConnection(info);
         newConnection.Offset = info.Offset;
         newConnection.MapGroup = MapID / 1000;
         newConnection.MapNum = MapID % 1000;

         RefreshMapSize();
         NeighborsChanged.Raise(this);
         viewPort.ChangeHistory.ChangeCompleted();
      }

      private BlockMapViewModel CreateNewMap(ModelDelta token, int bank, int width, int height) {
         var mapTable = MapRepointer.AddNewMapToBank(bank);
         var newMap = MapRepointer.CreateNewMap(token);
         var layout = MapRepointer.CreateNewLayout(token);

         // update width / height
         model.WriteValue(token, layout.Element.Start + 0, width);
         model.WriteValue(token, layout.Element.Start + 4, height);

         layout.Element.SetAddress(Format.BlockMap, MapRepointer.CreateNewBlockMap(token, width, height));

         newMap.Element.SetAddress(Format.Layout, layout.Element.Start);
         model.UpdateArrayPointer(token, null, null, -1, mapTable.Start + mapTable.Length - 4, newMap.Element.Start);

         var otherMap = new BlockMapViewModel(fileSystem, tutorials, viewPort, format, bank, mapTable.ElementCount - 1) {
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
               var mapVM = new BlockMapViewModel(fileSystem, tutorials, viewPort, format, group, map) {
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
            "Pick a group",
            "Which map do you want to connect to?",
            new[] { new[] { enumViewModel } },
            new VisualOption { Index = 1, Option = "OK", ShortDescription = "Connect Existing Map" });
         if (option == -1) return;
         var choice = keys[enumViewModel.Choice];

         var otherMap = new BlockMapViewModel(fileSystem, tutorials, viewPort, format, choice / 1000, choice % 1000) {
            allOverworldSprites = allOverworldSprites,
            BerryInfo = BerryInfo,
         };
         var size = GetBlockSize();
         var otherSize = otherMap.GetBlockSize();
         if (info.Direction.IsAny(MapDirection.Left, MapDirection.Right)) {
            info = info with { Offset = info.Offset - (otherSize.height - info.Size) / 2 };
         } else if (info.Direction.IsAny(MapDirection.Up, MapDirection.Down)) {
            info = info with { Offset = info.Offset - (otherSize.width - info.Size) / 2 };
         }

         var newConnection = AddConnection(info);
         newConnection.Offset = info.Offset;
         newConnection.Direction = info.Direction;
         newConnection.MapGroup = choice / 1000;
         newConnection.MapNum = choice % 1000;

         info = options[choice];
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
         viewPort.ChangeHistory.ChangeCompleted();
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
         var newConnection = new ConnectionModel(table[count], group, this.map);
         token.ChangeData(model, table[count].Start, new byte[12]);
         newConnection.Direction = info.Direction;
         return newConnection;
      }

      public ObjectEventViewModel CreateObjectEvent(int graphics, int scriptAddress) {
         var token = tokenFactory();
         var map = GetMapModel();
         var events = map.GetSubTable("events")[0];
         var element = AddEvent(events, tokenFactory, "objectCount", "objects");
         if (allOverworldSprites == null) allOverworldSprites = RenderOWs(model);
         var newEvent = new ObjectEventViewModel(ViewPort.Tools.CodeTool.ScriptParser, GotoAddress, element, allOverworldSprites, BerryInfo) {
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

      public WarpEventViewModel CreateWarpEvent(int bank, int map) {
         var mapModel = GetMapModel();
         var events = mapModel.GetSubTable("events")[0];
         var element = AddEvent(events, tokenFactory, "warpCount", "warps");
         var newEvent = new WarpEventViewModel(element) { X = 0, Y = 0, Elevation = 0, Bank = bank, Map = map, WarpID = element.ArrayIndex + 1 };
         SelectedEvent = newEvent;
         return newEvent;
      }

      public ScriptEventViewModel CreateScriptEvent() {
         var map = GetMapModel();
         var events = map.GetSubTable("events")[0];
         var element = AddEvent(events, tokenFactory, "scriptCount", "scripts");
         var newEvent = new ScriptEventViewModel(GotoAddress, element) { X = 0, Y = 0, Elevation = 0, Index = 0, Trigger = 0, ScriptAddress = Pointer.NULL };
         SelectedEvent = newEvent;
         return newEvent;
      }

      public SignpostEventViewModel CreateSignpostEvent() {
         var map = GetMapModel();
         var events = map.GetSubTable("events")[0];
         var element = AddEvent(events, tokenFactory, "signpostCount", "signposts");
         var newEvent = new SignpostEventViewModel(element, GotoAddress) { X = 0, Y = 0, Elevation = 0, Kind = 0, Pointer = Pointer.NULL };
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
         return new FlyEventViewModel(model, group, this.map, tokenFactory);
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
         var vm = new BlockMapViewModel(fileSystem, tutorials, viewPort, format, connection.MapGroup, connection.MapNum) {
            IncludeBorders = IncludeBorders,
            SpriteScale = SpriteScale,
            allOverworldSprites = allOverworldSprites,
            BerryInfo = BerryInfo,
            CollisionHighlight = CollisionHighlight,
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
      }

      private void RefreshBlockAttributeCache(ModelArrayElement layout = null, BlocksetModel blockModel1 = null, BlocksetModel blockModel2 = null) {
         if (blockModel1 == null || blockModel2 == null) {
            if (layout == null) layout = GetLayout();
            if (blockModel1 == null) blockModel1 = new BlocksetModel(model, layout.GetAddress("blockdata1"));
            if (blockModel2 == null) blockModel2 = new BlocksetModel(model, layout.GetAddress("blockdata2"));
         }
         int width = layout.GetValue("width"), height = layout.GetValue("height");
         int start = layout.GetAddress(Format.BlockMap);
         var maxUsedPrimary = BlockmapRun.GetMaxUsedBlock(model, start, width, height, PrimaryBlocks);
         var maxUsedSecondary = BlockmapRun.GetMaxUsedBlock(model, start, width, height, 1024) - PrimaryBlocks;

         blockAttributes = BlockmapRun.ReadBlockAttributes(maxUsedPrimary, maxUsedSecondary, blockModel1, blockModel2);
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
         var list = new List<IEventViewModel>();
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
                  var (xEdge, yEdge) = (x - border.West - width, y - border.North - height);
                  var (rightEdge, bottomEdge) = (xEdge >= 0, yEdge >= 0);
                  // top/left
                  if (!rightEdge && !bottomEdge && x % borderWidth == 0 && y % borderHeight == 0) canvas.Draw(borderBlock, x * 16, y * 16);
                  // right edge
                  if (rightEdge && !bottomEdge && xEdge % borderWidth == 0 && y % borderHeight == 0) canvas.Draw(borderBlock, x * 16, y * 16);
                  // bottom edge
                  if (!rightEdge && bottomEdge && x % borderWidth == 0 && yEdge % borderHeight == 0) canvas.Draw(borderBlock, x * 16, y * 16);
                  // bottom right corner
                  if (rightEdge && bottomEdge && xEdge % borderWidth == 0 && yEdge % borderHeight == 0) canvas.Draw(borderBlock, x * 16, y * 16);
                  continue;
               }
               var data = model.ReadMultiByteValue(start + ((y - border.North) * width + x - border.West) * 2, 2);
               var collision = data >> 10;
               data &= 0x3FF;
               if (blockRenders.Count > data) canvas.Draw(blockRenders[data], x * 16, y * 16);
               if (collision == collisionHighlight) HighlightCollision(canvas.PixelData, x * 16, y * 16);
               if (collisionHighlight == -1 && selectedEvent is ObjectEventViewModel obj && obj.ShouldHighlight(x - border.West, y - border.North)) {
                  HighlightCollision(canvas.PixelData, x * 16, y * 16);
               }
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

      private IReadOnlyList<IPixelViewModel> allOverworldSprites;
      public IReadOnlyList<IPixelViewModel> AllOverworldSprites {
         get {
            if (allOverworldSprites == null) allOverworldSprites = RenderOWs(model);
            return allOverworldSprites;
         }
         init => allOverworldSprites = value;
      }
      public static List<IPixelViewModel> RenderOWs(IDataModel model) {
         var list = new List<IPixelViewModel>();
         var run = model.GetTable(HardcodeTablesModel.OverworldSprites);
         var ows = new ModelTable(model, run.Start, null, run);
         for (int i = 0; i < ows.Count; i++) {
            list.Add(ObjectEventViewModel.Render(model, ows, i, 0));
         }
         return list;
      }

      private IReadOnlyList<IEventViewModel> GetEvents() {
         if (allOverworldSprites == null) allOverworldSprites = RenderOWs(model);
         var map = GetMapModel();
         var results = new List<IEventViewModel>();
         var events = new EventGroupModel(ViewPort.Tools.CodeTool.ScriptParser, GotoAddress, map.GetSubTable("events")[0], allOverworldSprites, BerryInfo, group, this.map);
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
               var map = new BlockMapViewModel(fileSystem, tutorials, viewPort, format, connections[i].MapGroup, connections[i].MapNum) {
                  allOverworldSprites = allOverworldSprites,
                  BerryInfo = BerryInfo,
               };
               var removeWidth = map.pixelWidth / 16;
               var removeOffset = connections[i].Offset;
               foreach (int j in removeWidth.Range()) availableSpace.Remove(j + removeOffset);
            } else if (direction == MapDirection.Left || direction == MapDirection.Right) {
               var map = new BlockMapViewModel(fileSystem, tutorials, viewPort, format, connections[i].MapGroup, connections[i].MapNum) {
                  allOverworldSprites = allOverworldSprites,
                  BerryInfo = BerryInfo,
               };
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

      private void GotoAddress(int address) {
         if (model.GetNextRun(address).Start > address) {
            viewPort.Tools.SelectedTool = viewPort.Tools.CodeTool;
            viewPort.Tools.CodeTool.Mode = CodeMode.Script;
         }
         viewPort.Goto.Execute(address);
      }

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
         viewPort.ChangeHistory.ChangeCompleted();
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

      public EventGroupModel(ScriptParser parser, Action<int> gotoAddress, ModelArrayElement events, IReadOnlyList<IPixelViewModel> ows, BerryInfo berries, int bank, int map) {
         this.events = events;

         var objectCount = events.GetValue("objectCount");
         var objects = events.GetSubTable("objects");
         var objectList = new List<ObjectEventViewModel>();
         if (objects != null) {
            for (int i = 0; i < objectCount; i++) {
               var newEvent = new ObjectEventViewModel(parser, gotoAddress, objects[i], ows, berries);
               newEvent.DataMoved += (sender, e) => DataMoved.Raise(this, e);
               objectList.Add(newEvent);
            }
         }
         Objects = objectList;

         var warpCount = events.GetValue("warpCount");
         var warps = events.GetSubTable("warps");
         var warpList = new List<WarpEventViewModel>();
         if (warps != null) {
            for (int i = 0; i < warpCount; i++) warpList.Add(new WarpEventViewModel(warps[i]));
         }
         Warps = warpList;

         var scriptCount = events.GetValue("scriptCount");
         var scripts = events.GetSubTable("scripts");
         var scriptList = new List<ScriptEventViewModel>();
         if (scripts != null) {
            for (int i = 0; i < scriptCount; i++) scriptList.Add(new ScriptEventViewModel(gotoAddress, scripts[i]));
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

         var flyEvent = new FlyEventViewModel(events.Model, bank, map, () => events.Token);
         if (flyEvent.Valid) {
            FlyEvent = flyEvent;
         }
      }

      public IReadOnlyList<ObjectEventViewModel> Objects { get; }
      public IReadOnlyList<WarpEventViewModel> Warps { get; }
      public IReadOnlyList<ScriptEventViewModel> Scripts { get; }
      public IReadOnlyList<SignpostEventViewModel> Signposts { get; }
      public FlyEventViewModel FlyEvent { get; }

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
