using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Map;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HexManiac.Core.Models.Runs.Sprites;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;


/* List of tables used by the map editor (or event templates):

* data.maps.banks
* data.maps.layouts
* graphics.overworld.sprites
* data.maps.names
* data.items.berry.stats
* data.maps.fly.connections
* data.maps.fly.spawns

* data.pokemon.names
* data.pokemon.types.names
* data.pokedex.regional
* data.pokedex.national
* graphics.pokemon.sprites.front
* graphics.pokemon.icons.sprites

* data.trainers.stats
* data.trainers.classes.names
* graphics.trainers.sprites.front

* data.items.stats
* graphics.items.sprites
* data.pokemon.moves.tutors
* data.pokemon.trades

 */

namespace HavenSoft.HexManiac.Core.ViewModels.Map {
   /// <summary>
   /// Represents the entire map editor tab, with all visible controls, maps, edit boxes, etc
   /// </summary>
   public class MapEditorViewModel : ViewModelCore, ITabContent {
      public readonly Format format;
      public readonly IFileSystem fileSystem;
      public readonly IEditableViewPort viewPort;
      public readonly IDataModel model;
      public readonly ChangeHistory<ModelDelta> history;
      public readonly Singletons singletons;
      public readonly EventTemplate templates;
      public readonly Random rnd = new();

      public IEditableViewPort ViewPort => viewPort;
      public IFileSystem FileSystem => fileSystem;
      public Format Format => format;

      public long blockset;
      public int blockset1, blockset2;
      public BlockMapViewModel primaryMap;
      public bool showHeaderPanel;
      public bool ShowHeaderPanel {
         get => showHeaderPanel;
         set => Set(ref showHeaderPanel, value);
      }

      public void ToggleHeaderPanel() {
         ShowHeaderPanel = !ShowHeaderPanel;
         Tutorials.Complete(Tutorial.ToolbarButton_EditMapHeader);
      }

      public IEventViewModel selectedEvent;
      public bool showTemplateSettings;
      public bool ShowTemplateSettings {
         get => showTemplateSettings;
         set => Set(ref showTemplateSettings, value, old => {
            if (showTemplateSettings) Tutorials.Complete(Tutorial.ToolbarTemplate_ConfigureObject);
         });
      }

      public ObservableCollection<BlockMapViewModel> VisibleMaps { get; } = new();

      public ObservableCollection<MapSlider> MapButtons { get; } = new();

      public EventTemplate Templates => templates;

      public int PrimaryBlocks { get; }
      public int PrimaryTiles { get; }

      public string hoverPoint, zoomLevel;
      public string HoverPoint { get => hoverPoint; set => Set(ref hoverPoint, value); }
      public string ZoomLevel { get => zoomLevel; set => Set(ref zoomLevel, value); }

      public MapTutorialsViewModel Tutorials { get; }

      #region Block Picker

      public int drawBlockIndex = -10;

      public bool autoUpdateCollision = true;
      public bool AutoUpdateCollision {
         get => autoUpdateCollision;
         set => Set(ref autoUpdateCollision, value);
      }

      public int collisionIndex = -1;

      public ObservableCollection<string> CollisionOptions { get; } = new();

      public bool BlockSelectionToggle { get; set; }

      public void AnimateBlockSelection() {
         BlockSelectionToggle = !BlockSelectionToggle;
         NotifyPropertyChanged(nameof(BlockSelectionToggle));
      }

      #endregion

      #region ITabContent

      public string Name => (primaryMap?.FullName ?? "Map") + (history.HasDataChange ? "*" : string.Empty);
      public string FullFileName => viewPort.FullFileName;
      public bool SpartanMode { get; set; }
      public bool IsMetadataOnlyChange => false;
      public bool CanDuplicate => true;

      public event EventHandler<string> OnError;
      public event EventHandler<string> OnMessage;
      public event EventHandler ClearMessage;
      public event EventHandler Closed;
      public event EventHandler<TabChangeRequestedEventArgs> RequestTabChange;
      public void RaiseRequestTabChange(TabChangeRequestedEventArgs e) => RequestTabChange.Raise(this, e);
      public event EventHandler<Action> RequestDelayedWork;
      public event EventHandler RequestMenuClose;
      public event EventHandler<Direction> RequestDiff;
      public event EventHandler<CanDiffEventArgs> RequestCanDiff;
      public event EventHandler<CanPatchEventArgs> RequestCanCreatePatch;
      public event EventHandler<CanPatchEventArgs> RequestCreatePatch;
      public event EventHandler RequestRefreshGotoShortcuts;

      public bool CanIpsPatchRight => false;
      public bool CanUpsPatchRight => false;
      public void IpsPatchRight() { }
      public void UpsPatchRight() { }

      public void LaunchFileLocation(IFileSystem fileSystem) => fileSystem.LaunchProcess("explorer.exe", $"/select,\"{FullFileName}\"");

      public bool TryImport(LoadedFile file, IFileSystem fileSystem) => false;

      public readonly List<int> forwardStack = new(), backStack = new();
      public bool CanExecuteBack() => backStack.Count > 0;
      public bool CanExecuteForward() => forwardStack.Count > 0;

      #endregion

      public event EventHandler AutoscrollTiles;

      #region Constructor

      public bool IsValidState { get; set; }

      #endregion

      public void UpdateMapShortcut(BlockMapViewModel map) {
         var groupIndex = map.MapID / 1000;
         var mapIndex = map.MapID % 1000;
         var shortcut = model.GotoShortcuts.FirstOrDefault(shortcut => shortcut.DisplayText == "Maps");
         var index = model.GotoShortcuts.IndexOf(shortcut);
         var newShortcut = new GotoShortcutModel($"data.maps.banks/{groupIndex}/maps/{mapIndex}/map/0/layout/0/blockmap/", $"maps.bank{groupIndex}.{map.FullName}", "Maps");
         model.UpdateGotoShortcut(index, newShortcut);
         RequestRefreshGotoShortcuts.Raise(this);
      }

      public IEnumerable<BlockMapViewModel> PreferLoaded(IEnumerable<BlockMapViewModel> maps) {
         foreach (var map in maps) {
            var match = VisibleMaps.FirstOrDefault(m => m.MapID == map.MapID);
            yield return match ?? map;
         }
      }

      #region Map Interaction

      public double cursorX, cursorY, deltaX, deltaY;
      public event EventHandler AutoscrollBlocks;

      public bool showHighlightCursor;
      public bool ShowHighlightCursor { get => showHighlightCursor; set => Set(ref showHighlightCursor, value); }

      public double highlightCursorX, highlightCursorY, highlightCursorWidth, highlightCursorHeight;
      public double HighlightCursorX { get => highlightCursorX; set => Set(ref highlightCursorX, value); }
      public double HighlightCursorY { get => highlightCursorY; set => Set(ref highlightCursorY, value); }
      public double HighlightCursorWidth { get => highlightCursorWidth; set => Set(ref highlightCursorWidth, value); }
      public double HighlightCursorHeight { get => highlightCursorHeight; set => Set(ref highlightCursorHeight, value); }

      public bool showBeneath;
      
      public bool hideEvents;

      // if it returns an empty array: no hover tip to display
      // if it returns null: continue displaying previous hover tip
      // if it returns content: display that as the new hover tip
      public static readonly object[] EmptyTooltip = new object[0];

      /// <summary>
      /// returns true if the hover changed
      /// </summary>
      public bool UpdateHover(BlockMapViewModel map, int left, int top, int width, int height) {
         var (prevX, prevY) = (highlightCursorX, highlightCursorY);
         var (prevW, prevH) = (highlightCursorWidth, highlightCursorHeight);

         var border = map.GetBorderThickness();
         if (border == null) return false;
         ShowHighlightCursor = true;

         // limit selection right/bottom to the map
         var mapWidth = map.PixelWidth / 16 - border.West - border.East;
         var mapHeight = map.PixelHeight / 16 - border.North - border.South;
         while (left + width > mapWidth && width > 1) width--;
         while (top + height > mapHeight && height > 1) height--;

         HighlightCursorX = (left + border.West + width / 2.0) * 16 * map.SpriteScale + map.LeftEdge;
         HighlightCursorY = (top + border.North + height / 2.0) * 16 * map.SpriteScale + map.TopEdge;
         HighlightCursorWidth = width * 16 * map.SpriteScale + 4;
         HighlightCursorHeight = height * 16 * map.SpriteScale + 4;

         HoverPoint = $"({left}, {top})";
         if (width > 1 || height > 1) HoverPoint += $" [{width}x{height}]";

         if (prevX != highlightCursorX) return true;
         if (prevY != highlightCursorY) return true;
         if (prevW != highlightCursorWidth) return true;
         return prevH != highlightCursorHeight;
      }

      #region Primary Interaction (left-click)

      public PrimaryInteractionType interactionType;
      public Point drawSource, lastDraw;

      #region Rectangle-Drawing helper methods

      // this will hold whatever blocks were originally in the map, so we can put them back as the user moves the rect around.
      public int[,] rectangleBackup;

      /// <summary>
      /// Use the values of drawSource and lastDraw to fill the backup from the current map
      /// </summary>
      public void FillBackup() {
         var (left, right) = (lastDraw.X, drawSource.X);
         var (top, bottom) = (lastDraw.Y, drawSource.Y);
         if (left > right) (left, right) = (right, left);
         if (top > bottom) (top, bottom) = (bottom, top);
         var (width, height) = (right - left + 1, bottom - top + 1);
         rectangleBackup = primaryMap.ReadRectangle(left, top, width, height);
      }

      #endregion

      #endregion

      public bool withinEventCreationInteraction = false;
      public EventCreationType eventCreationType;
      public void StartEventCreationInteraction(EventCreationType type) {
         interactionType = PrimaryInteractionType.Event;
         VisibleMaps.Clear();
         VisibleMaps.Add(primaryMap);
         MapButtons.Clear();
         eventCreationType = type;
         withinEventCreationInteraction = true;
      }


      #region Selection (right-click)

      public int[,] tilesToDraw;
      public Point selectDownPosition;
      public bool drawMultipleTiles;
      public bool DrawMultipleTiles {
         get => drawMultipleTiles;
         set => Set(ref drawMultipleTiles, value, old => NotifyPropertyChanged(nameof(BlockBagVisible)));
      }

      public bool blockEditorVisible;
      public bool BlockEditorVisible { get => blockEditorVisible; set => Set(ref blockEditorVisible, value, old => NotifyPropertyChanged(nameof(BlockBagVisible))); }

      public bool BlockBagVisible => blockEditorVisible && !drawMultipleTiles;

      public IPixelViewModel multiTileDrawRender;
      public IPixelViewModel MultiTileDrawRender {
         get => multiTileDrawRender;
         set {
            multiTileDrawRender = value;
            NotifyPropertyChanged();
         }
      }

      public void SelectMove(double x, double y) {
         var map = MapUnderCursor(x, y);
         if (map != primaryMap) return;
         if (x < map.LeftEdge || x > map.LeftEdge + map.PixelWidth * map.SpriteScale) return;
         if (y < map.TopEdge || y > map.TopEdge + map.PixelHeight * map.SpriteScale) return;
         var selectMovePosition = ToBoundedMapTilePosition(map, x, y, 1, 1);
         var left = Math.Min(selectDownPosition.X, selectMovePosition.X);
         var top = Math.Min(selectDownPosition.Y, selectMovePosition.Y);
         var width = Math.Abs(selectDownPosition.X - selectMovePosition.X) + 1;
         var height = Math.Abs(selectDownPosition.Y - selectMovePosition.Y) + 1;
         UpdateHover(map, left, top, width, height);
      }

      public Point ToTilePosition(double x, double y) {
         (x, y) = ((x - primaryMap.LeftEdge) / primaryMap.SpriteScale / 16, (y - primaryMap.TopEdge) / primaryMap.SpriteScale / 16);
         var borders = primaryMap.GetBorderThickness();
         var position = new Point((int)Math.Floor(x) - borders.West, (int)Math.Floor(y) - borders.North);
         return position;
      }

      public Point ToBoundedMapTilePosition(BlockMapViewModel map, double x, double y, int selectionWidth, int selectionHeight) {
         (x, y) = ((x - map.LeftEdge) / map.SpriteScale / 16, (y - map.TopEdge) / map.SpriteScale / 16);
         var borders = map.GetBorderThickness();
         if (borders == null) return new(0, 0);
         var position = new Point((int)Math.Floor(x) - borders.West, (int)Math.Floor(y) - borders.North);

         // limit to within the content of this map
         var width = map.PixelWidth / 16 - borders.West - borders.East;
         var height = map.PixelHeight / 16 - borders.North - borders.South;
         position = new(position.X.LimitToRange(0, width - 1), position.Y.LimitToRange(0, height - 1));

         return position;
      }

      public ImageLocation ToPixelPosition(BlockMapViewModel map, double x, double y) {
         (x, y) = ((x - map.LeftEdge) / map.SpriteScale, (y - map.TopEdge) / map.SpriteScale);
         return new ImageLocation(x / map.PixelWidth, y / map.PixelHeight);
      }

      public (double x, double y) ToMapPosition(BlockMapViewModel map, int x, int y) {
         var borders = map.GetBorderThickness();
         var pX = (x + borders.West) * 16 * map.SpriteScale + map.LeftEdge;
         var pY = (y + borders.North) * 16 * map.SpriteScale + map.TopEdge;
         return (pX, pY);
      }

      #endregion

      #region Event Hover / Context Menus

      public IEventViewModel eventContext;

      public const int TextSummaryLimit = 60, TextCountLimit = 5;

      #endregion

      public void Pan(int intX, int intY) {
         foreach (var map in VisibleMaps) {
            map.LeftEdge += intX;
            map.TopEdge += intY;
         }
         foreach (var button in MapButtons) {
            button.AnchorPositionX += button.AnchorLeftEdge ? intX : -intX;
            button.AnchorPositionY += button.AnchorTopEdge ? intY : -intY;
         }
      }

      public BlockMapViewModel MapUnderCursor(double x, double y) {
         BlockMapViewModel closestMap = null;
         double closestDistance = int.MaxValue;
         foreach (var map in VisibleMaps) {
            if (map.ZIndex < 0) continue;
            var dx = x.LimitToRange(map.LeftEdge, map.LeftEdge + map.PixelWidth * map.SpriteScale);
            var dy = y.LimitToRange(map.TopEdge, map.TopEdge + map.PixelHeight * map.SpriteScale);
            var distance = (x - dx) * (x - dx) + (y - dy) * (y - dy);
            if (distance == 0) return map;
            if (distance > closestDistance) continue;
            closestDistance = distance;
            closestMap = map;
         }
         return closestMap;
      }

      #endregion

      #region Blocks Interection

      public readonly List<int> blockBag = new();
      public bool selectionFromBlock = false;
      public Point blockInteractionStart;

      // when in 9-grid mode, show a 2x2 draw area, and draw from blocks in the 9-grid
      public bool use9Grid;
      public bool IsValid9GridSelection {
         get {
            return tilesToDraw != null && tilesToDraw.GetLength(0) == 3 && tilesToDraw.GetLength(1) == 3;
         }
      }

      public IPixelViewModel BlockBag { get; set; }

      #endregion

      #region ShiftInteraction

      public MapSlider shiftButton;

      public void ShiftDown(double x, double y) {
         (cursorX, cursorY) = (x, y);
         (deltaX, deltaY) = (0, 0);
         shiftButton = ButtonUnderCursor(x, y);
         HighlightCursorWidth = HighlightCursorHeight = 0;

         var layout = primaryMap.GetLayout();
         var run = model.GetNextRun(layout.GetAddress("blockmap")) as BlockmapRun;
         run?.StoreContentBackupForSizeChange();
      }

      public void ShiftMove(double x, double y) {
         if (shiftButton == null) return;
         deltaX += x - cursorX;
         deltaY += y - cursorY;
         var blockSize = (int)(16 * primaryMap.SpriteScale);
         var (intX, intY) = ((int)deltaX / blockSize, (int)deltaY / blockSize);
         deltaX -= intX * blockSize;
         deltaY -= intY * blockSize;
         (cursorX, cursorY) = (x, y);
         shiftButton.Move(intX, intY);
      }

      public MapSlider ButtonUnderCursor(double x, double y) {
         int left, right, top, bottom;
         foreach (var button in MapButtons) {
            if (button.AnchorLeftEdge) {
               left = button.AnchorPositionX;
               right = left + MapSlider.SliderSize;
            } else {
               right = -button.AnchorPositionX;
               left = right - MapSlider.SliderSize;
            }
            if (button.AnchorTopEdge) {
               top = button.AnchorPositionY;
               bottom = top + MapSlider.SliderSize;
            } else {
               bottom = -button.AnchorPositionY;
               top = bottom - MapSlider.SliderSize;
            }
            if (left <= x && x < right && top <= y && y < bottom) return button;
         }
         return null;
      }

      #endregion

      #region Wave Function Collapse

      public Dictionary<long, WaveNeighbors[]> waveFunctionPrimary;
      public Dictionary<long, WaveNeighbors[]> waveFunctionSecondary;
      public Dictionary<long, WaveNeighbors[]> waveFunctionMixed;

      public void AddProbabilities(List<List<CollapseProbability>> probabilities, BlockCells cells, int xx, int yy, WaveNeighbors[] primaryWaveNeighbors, WaveNeighbors[] secondaryWaveNeighbors, WaveNeighbors[] mixedWaveNeighbors, Func<WaveNeighbors, List<CollapseProbability>> reverse) {
         if (xx < 0 || yy < 0 || xx >= cells.Width || yy >= cells.Height) return;
         var edge = cells[xx, yy].Tile;
         if (edge == 0) return;
         if (IsPrimaryBlock(cells[xx, yy])) {
            var mergedProbabilities = new List<CollapseProbability>();
            if (primaryWaveNeighbors[edge] != null) mergedProbabilities.AddRange(reverse(primaryWaveNeighbors[edge]));
            if (mixedWaveNeighbors[edge] != null) mergedProbabilities.AddRange(reverse(mixedWaveNeighbors[edge]));
            probabilities.Add(mergedProbabilities);
         } else {
            var mergedProbabilities = new List<CollapseProbability>();
            if (secondaryWaveNeighbors[edge - PrimaryBlocks] != null) mergedProbabilities.AddRange(reverse(secondaryWaveNeighbors[edge - PrimaryBlocks]));
            if (mixedWaveNeighbors[edge] != null) mergedProbabilities.AddRange(reverse(mixedWaveNeighbors[edge]));
            probabilities.Add(mergedProbabilities);
         }
      }

      public void CheckPrimary(BlockCells cells, int x, int y, BlockCell center, WaveNeighbors[] primaryWaveNeighbors, WaveNeighbors[] mixedWaveNeighbors, Func<WaveNeighbors, List<CollapseProbability>> direction) {
         var primaryElement = primaryWaveNeighbors[center.Tile];
         var mixedElement = mixedWaveNeighbors[center.Tile];
         if (x < 0 || x >= cells.Width || y < 0 || y >= cells.Height) return;
         var edge = cells[x, y];
         if (IsPrimaryBlock(edge)) {
            if (primaryElement == null) primaryElement = primaryWaveNeighbors[center.Tile] = new(new(), new(), new(), new());
            var collapse = direction(primaryElement).Ensure(cp => cp.Block == edge.Tile, new CollapseProbability(edge.Tile));
            collapse.Count += 1;
         } else {
            if (mixedElement == null) mixedElement = mixedWaveNeighbors[center.Tile] = new(new(), new(), new(), new());
            var collapse = direction(mixedElement).Ensure(cp => cp.Block == edge.Tile, new CollapseProbability(edge.Tile));
            collapse.Count += 1;
         }
      }

      public void CheckSecondary(BlockCells cells, int x, int y, BlockCell center, WaveNeighbors[] secondaryWaveNeighbors, WaveNeighbors[] mixedWaveNeighbors, Func<WaveNeighbors, List<CollapseProbability>> direction) {
         var secondaryElement = secondaryWaveNeighbors[center.Tile - PrimaryBlocks];
         var mixedElement = mixedWaveNeighbors[center.Tile];
         if (x < 0 || x >= cells.Width || y < 0 || y >= cells.Height) return;
         var edge = cells[x, y];
         if (IsPrimaryBlock(edge)) {
            if (secondaryElement == null) secondaryElement = secondaryWaveNeighbors[center.Tile - PrimaryBlocks] = new(new(), new(), new(), new());
            var collapse = direction(secondaryElement).Ensure(cp => cp.Block == edge.Tile, new CollapseProbability(edge.Tile));
            collapse.Count += 1;
         } else {
            if (mixedElement == null) mixedElement = mixedWaveNeighbors[center.Tile] = new(new(), new(), new(), new());
            var collapse = direction(mixedElement).Ensure(cp => cp.Block == edge.Tile, new CollapseProbability(edge.Tile));
            collapse.Count += 1;
         }
      }

      public bool IsPrimaryBlock(BlockCell cell) => cell.Tile < PrimaryBlocks;
      public int TotalBlocks => 1024;

      #endregion

      #region Layout Table Fixing (for maps added via another tool)

      public void FixLayoutTable() {
         var token = new NoDataChangeDeltaModel();
         if (model.GetTable(HardcodeTablesModel.MapLayoutTable) is not ArrayRun layouts) return;
         var layoutFormat = layouts.ElementContent[0] is ArrayRunPointerSegment pSeg ? pSeg.InnerFormat : null;
         if (layoutFormat == null) return;
         var excess = 0;
         while (true) {
            var start = layouts.Start + layouts.Length + 4 * excess;
            if (model.GetNextRun(start) is not PointerRun pRun) break;
            if (pRun.Start != start) break;
            var destination = model.ReadPointer(start);
            if (!destination.InRange(0, model.Count)) break;
            if (model.GetNextRun(destination) is not ITableRun tRun) break;
            if (tRun.FormatString != layoutFormat) break;
            excess++;
         }
         model.ClearFormat(token, layouts.Start + layouts.Length, excess * 4);
         model.ObserveRunWritten(token, layouts.ResizeMetadata(layouts.ElementCount + excess));
      }

      #endregion

      public Dictionary<long, int[]> preferredCollisionsPrimary;
      public Dictionary<long, int[]> preferredCollisionsSecondary;
      public void CountCollisionForBlocks() {
         preferredCollisionsPrimary = new Dictionary<long, int[]>();
         preferredCollisionsSecondary = new Dictionary<long, int[]>();

         // step 1: count the usages of each collision for each block in each blockset
         var banksTable = model.GetTable(HardcodeTablesModel.MapBankTable);
         var banks = new ModelTable(model, banksTable.Start, null, banksTable);
         var blocksetHistogram1 = new Dictionary<long, Dictionary<int, int>[]>();
         var blocksetHistogram2 = new Dictionary<long, Dictionary<int, int>[]>();
         foreach (var bank in banks) {
            var maps = bank.GetSubTable("maps");
            if (maps == null) continue;
            foreach (var entry in maps) {
               var mapTable = entry.GetSubTable("map");
               if (mapTable == null) continue;
               var map = mapTable[0];
               if (map == null) continue;
               var layoutTable = map.GetSubTable("layout");
               if (layoutTable == null) continue;
               var layout = layoutTable[0];
               if (layout == null) continue;
               // width:: height:: blockmap<> blockdata1<> blockdata2<>
               var address1 = layout.GetAddress("blockdata1");
               var address2 = layout.GetAddress("blockdata2");
               if (!blocksetHistogram1.TryGetValue(address1, out var blocks1)) {
                  blocks1 = new Dictionary<int, int>[0x400];
                  for (int i = 0; i < blocks1.Length; i++) blocks1[i] = new();
                  blocksetHistogram1[address1] = blocks1;
               }
               if (!blocksetHistogram2.TryGetValue(address2, out var blocks2)) {
                  blocks2 = new Dictionary<int, int>[0x400];
                  for (int i = 0; i < blocks2.Length; i++) blocks2[i] = new();
                  blocksetHistogram2[address2] = blocks2;
               }
               var size = layout.GetValue("width") * layout.GetValue("height");
               var start = layout.GetAddress("blockmap");
               if (start < 0 || start > model.Count - size * 2) continue;
               for (int i = 0; i < size; i++) {
                  var pair = model.ReadMultiByteValue(start + i * 2, 2);
                  var collision = pair >> 10;
                  var tile = pair & 0x3FF;
                  var isPrimary = tile < PrimaryTiles;
                  if (isPrimary) {
                     if (!blocks1[tile].TryGetValue(collision, out var value)) value = 0;
                     blocks1[tile][collision] = value + 1;
                  } else {
                     if (!blocks2[tile].TryGetValue(collision, out var value)) value = 0;
                     blocks2[tile][collision] = value + 1;
                  }
               }
            }
         }

         // step 2: remember the preferred collision for each block in each blockset
         foreach (var blockset in blocksetHistogram1.Keys) {
            var histogram = blocksetHistogram1[blockset];
            var preference = new int[0x400];
            for (int i = 0; i < preference.Length; i++) {
               int mostUsedCollision = -1, useAmount = -1;
               foreach (var (tile, amount) in histogram[i]) {
                  if (amount > useAmount) (mostUsedCollision, useAmount) = (tile, amount);
               }
               preference[i] = mostUsedCollision.LimitToRange(0, 0x3F);
            }
            preferredCollisionsPrimary[blockset] = preference;
         }
         foreach (var blockset in blocksetHistogram2.Keys) {
            var histogram = blocksetHistogram2[blockset];
            var preference = new int[0x400];
            for (int i = 0; i < preference.Length; i++) {
               int mostUsedCollision = -1, useAmount = -1;
               foreach (var (tile, amount) in histogram[i]) {
                  if (amount > useAmount) (mostUsedCollision, useAmount) = (tile, amount);
               }
               preference[i] = mostUsedCollision.LimitToRange(0, 0x3F);
            }
            preferredCollisionsSecondary[blockset] = preference;
         }
      }

      public long UpdateBlockset(int mapID) {
         var banksTable = model.GetTable(HardcodeTablesModel.MapBankTable);
         var banks = new ModelTable(model, banksTable.Start, null, banksTable);
         var bank = banks[mapID / 1000];
         if (bank == null) return -1;
         var maps = bank.GetSubTable("maps");
         if (maps == null) return -1;
         var mapsElement = maps[mapID % 1000];
         if (mapsElement == null) return -1;
         var map = mapsElement.GetSubTable("map");
         if (map == null) return -1;
         var mapElement = map[0];
         if (mapElement == null) return -1;
         var layoutTable = mapElement.GetSubTable(Format.Layout);
         if (layoutTable == null) return -1;
         var layout = layoutTable[0];
         var address1 = layout.GetAddress(Format.PrimaryBlockset);
         var address2 = layout.GetAddress(Format.SecondaryBlockset);
         if (address1 == Pointer.NULL || address2 == Pointer.NULL) return Pointer.NULL;
         var combined = (address1 << 25) | address2;
         (blockset1, blockset2) = (address1, address2);
         return combined;
      }

      public void HandleAutoscrollTiles(object sender, EventArgs e) => AutoscrollTiles.Raise(this);

      public int GetPreferredCollision(int tile) {
         if (preferredCollisionsPrimary == null) CountCollisionForBlocks();
         int[] preference;
         if (tile < PrimaryTiles) {
            if (preferredCollisionsPrimary.TryGetValue(blockset1, out preference)) return preference[tile];
         } else {
            if (preferredCollisionsSecondary.TryGetValue(blockset2, out preference)) return preference[tile];
         }
         return -1;
      }
   }

   public record CollapseProbability(int Block) { public int Count { get; set; } };
   public record WaveNeighbors(List<CollapseProbability> Left, List<CollapseProbability> Right, List<CollapseProbability> Up, List<CollapseProbability> Down);
   public record WaveCell(IList<CollapseProbability> Probabilities, Func<int, int> GetCollision) {
      public int Collapse(Random rnd) {
         var totalOptions = Probabilities.Sum(cp => cp.Count);
         var selection = rnd.Next(totalOptions);
         var index = 0;
         while (selection > Probabilities[index].Count) {
            selection -= Probabilities[index].Count;
            index += 1;
         }
         var block = Probabilities[index].Block;
         var collision = GetCollision(block);
         return (collision << 10) | block;
      }
   }

   public enum SelectionInteractionResult { None, ShowMenu }

   public enum EventCreationType { None, Object, Warp, Script, Signpost, Fly, WaveFunction }

   [Flags]
   public enum PrimaryInteractionStart {
      None = 0,
      Click = 1,
      DoubleClick = 2,
      ControlClick = 4,
      ShiftClick = 8,
   }
   public enum PrimaryInteractionType { None, Draw, Event, RectangleDraw, Draw9Grid }

   public record BlocksetCache(ObservableCollection<BlocksetOption> Primary, ObservableCollection<BlocksetOption> Secondary) {
   }

   public enum MapDisplayOptions { AllEvents, ObjectEvents, NoEvents }

   public record TileSelection(int[]Tiles, int Width);
}
