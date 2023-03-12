using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Map;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using Microsoft.Scripting.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels.Map {
   /// <summary>
   /// Represents the entire map editor tab, with all visible controls, maps, edit boxes, etc
   /// </summary>
   public class MapEditorViewModel : ViewModelCore, ITabContent {
      private readonly Format format;
      private readonly IFileSystem fileSystem;
      private readonly IEditableViewPort viewPort;
      private readonly IDataModel model;
      private readonly ChangeHistory<ModelDelta> history;
      private readonly Singletons singletons;
      private readonly EventTemplate templates;
      private readonly Random rnd = new();

      public IViewPort ViewPort => viewPort;
      public IFileSystem FileSystem => fileSystem;
      public Format Format => format;

      private long blockset;
      private int blockset1, blockset2;
      private BlockMapViewModel primaryMap;
      public BlockMapViewModel PrimaryMap {
         get => primaryMap;
         set {
            if (primaryMap == value) return;
            UpdatePrimaryMap(value);
         }
      }

      private bool showHeaderPanel;
      public bool ShowHeaderPanel {
         get => showHeaderPanel;
         set => Set(ref showHeaderPanel, value);
      }

      public void ToggleHeaderPanel() {
         ShowHeaderPanel = !ShowHeaderPanel;
         Tutorials.Complete(Tutorial.ToolbarButton_EditMapHeader);
      }

      private IEventViewModel selectedEvent;
      public IEventViewModel SelectedEvent {
         get => selectedEvent;
         set {
            if (selectedEvent == value) return;
            selectedEvent = value;
            NotifyPropertyChanged();
            NotifyPropertyChanged(nameof(ShowEventPanel));
            ShowHeaderPanel = false;
            if (selectedEvent == null) primaryMap.DeselectEvent();
            primaryMap.BlockEditor.ShowTiles = false;
            DrawBlockIndex = -1;
            CollisionIndex = -1;
         }
      }

      private bool showTemplateSettings;
      public bool ShowTemplateSettings {
         get => showTemplateSettings;
         set => Set(ref showTemplateSettings, value, old => {
            if (showTemplateSettings) Tutorials.Complete(Tutorial.ToolbarTemplate_ConfigureObject);
         });
      }

      public bool ShowEventPanel {
         get => selectedEvent != null;
         set { // so that the UI can hide the panel
            if (value) return;
            SelectedEvent = null;
         }
      }

      public ObservableCollection<BlockMapViewModel> VisibleMaps { get; } = new();

      public ObservableCollection<MapSlider> MapButtons { get; } = new();

      public ObservableCollection<MenuCommand> ContextItems { get; } = new();

      public EventTemplate Templates => templates;

      private int PrimaryBlocks { get; }
      private int PrimaryTiles { get; }

      private string hoverPoint, zoomLevel;
      public string HoverPoint { get => hoverPoint; set => Set(ref hoverPoint, value); }
      public string ZoomLevel { get => zoomLevel; set => Set(ref zoomLevel, value); }

      public MapTutorialsViewModel Tutorials { get; }

      #region Block Picker

      public IPixelViewModel Blocks => primaryMap?.BlockPixels;

      private int drawBlockIndex = -10;
      public int DrawBlockIndex {
         get => drawBlockIndex;
         set {
            Set(ref drawBlockIndex, value, old => {
               DrawMultipleTiles = false;
               if (drawBlockIndex >= 0) BlockEditorVisible = true;
               PrimaryMap.BlockEditor.BlockIndex = drawBlockIndex;
               drawBlockIndex = Math.Min(drawBlockIndex, PrimaryMap.BlockEditor.BlockIndex);
               selectionFromBlock = false;
               NotifyPropertiesChanged(nameof(HighlightBlockX), nameof(HighlightBlockY), nameof(HighlightBlockWidth), nameof(HighlightBlockHeight));
            });
         }
      }

      private bool autoUpdateCollision = true;
      public bool AutoUpdateCollision {
         get => autoUpdateCollision;
         set => Set(ref autoUpdateCollision, value);
      }

      private int collisionIndex = -1;
      public int CollisionIndex {
         get => collisionIndex;
         set {
            Set(ref collisionIndex, value);
            foreach (var map in VisibleMaps)
               map.CollisionHighlight = value;
            DrawMultipleTiles = false;
         }
      }

      public ObservableCollection<string> CollisionOptions { get; } = new();

      public bool BlockSelectionToggle { get; private set; }
      public double HighlightBlockX => (drawBlockIndex % BlockMapViewModel.BlocksPerRow) * Blocks.SpriteScale * 16;
      public double HighlightBlockY => (drawBlockIndex / BlockMapViewModel.BlocksPerRow) * Blocks.SpriteScale * 16;
      public double HighlightBlockWidth => 16 * Blocks.SpriteScale * (selectionFromBlock ? tilesToDraw.GetLength(0) : 1) + 2;
      public double HighlightBlockHeight => 16 * Blocks.SpriteScale * (selectionFromBlock ? tilesToDraw.GetLength(1) : 1) + 2;

      private void AnimateBlockSelection() {
         BlockSelectionToggle = !BlockSelectionToggle;
         NotifyPropertyChanged(nameof(BlockSelectionToggle));
      }

      #endregion

      #region ITabContent

      private StubCommand close, undo, redo, backCommand, forwardCommand;

      public string Name => (primaryMap?.FullName ?? "Map") + (history.HasDataChange ? "*" : string.Empty);
      public string FullFileName => viewPort.FullFileName;
      public bool IsMetadataOnlyChange => false;
      public ICommand Save => viewPort.Save;
      public ICommand SaveAs => viewPort.SaveAs;
      public ICommand ExportBackup => viewPort.ExportBackup;
      public ICommand Undo => StubCommand(ref undo,
         () => {
            history.Undo.Execute();
            Tutorials.Complete(Tutorial.ToolbarUndo_Undo);
            Refresh();
         },
         () => history.Undo.CanExecute(default));
      public ICommand Redo => StubCommand(ref redo,
         () => {
            history.Redo.Execute();
            Refresh();
         },
         () => history.Redo.CanExecute(default));
      public ICommand Copy => null;
      public ICommand DeepCopy => null;
      public ICommand Clear => null;
      public ICommand SelectAll => null;
      public ICommand Goto => null;
      public ICommand ResetAlignment => null;
      public ICommand Back => StubCommand(ref backCommand, ExecuteBack, CanExecuteBack);
      public ICommand Forward => StubCommand(ref forwardCommand, ExecuteForward, CanExecuteForward);

      public ICommand Close => StubCommand(ref close, () => Closed.Raise(this));

      public ICommand Diff => null;
      public ICommand DiffLeft => null;
      public ICommand DiffRight => null;
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

      public void Duplicate() => Duplicate(PrimaryMap.MapID / 1000, PrimaryMap.MapID % 1000);
      private void Duplicate(int bank, int map) {
         var dup = viewPort.CreateDuplicate();
         // don't check for the viewPort initilazition workload until the model is initialized (for map duplication, this should never really be an issue)
         model.InitializationWorkload.ContinueWith(t => {
            // don't try to use the to tab's map editor until it's been fully initialized.
            dup.InitializationWorkload.ContinueWith(task => {
               singletons.WorkDispatcher.BlockOnUIWork(() => {
                  dup.MapEditor.UpdatePrimaryMap(new BlockMapViewModel(FileSystem, Tutorials, viewPort, Format, bank, map));
                  RequestTabChange?.Invoke(this, new(dup.MapEditor));
               });
            }, TaskContinuationOptions.ExecuteSynchronously);
         }, TaskContinuationOptions.ExecuteSynchronously);
      }

      public void Refresh() {
         format.Refresh();
         primaryMap.ClearCaches();
         UpdatePrimaryMap(primaryMap);
      }
      public bool TryImport(LoadedFile file, IFileSystem fileSystem) => false;

      private readonly List<int> forwardStack = new(), backStack = new();
      private bool CanExecuteBack() => backStack.Count > 0;
      private bool CanExecuteForward() => forwardStack.Count > 0;
      private void ExecuteBack() {
         if (backStack.Count == 0) return;
         var previous = backStack[backStack.Count - 1];
         backStack.RemoveAt(backStack.Count - 1);
         if (previous == -1) {
            var request = new TabChangeRequestedEventArgs(viewPort);
            RequestTabChange(this, request);
            if (request.RequestAccepted) (viewPort as ViewPort).SetJumpForwardTab(this);
            return;
         }
         if (primaryMap != null) forwardStack.Add(primaryMap.MapID);
         if (forwardStack.Count == 1) forwardCommand.RaiseCanExecuteChanged();
         if (backStack.Count == 0) backCommand.RaiseCanExecuteChanged();
         NavigateTo(previous, trackChange: false);
         Tutorials.Complete(Tutorial.BackButton_GoBack);
      }
      private void ExecuteForward() {
         if (forwardStack.Count == 0) return;
         var next = forwardStack[forwardStack.Count - 1];
         forwardStack.RemoveAt(forwardStack.Count - 1);
         if (next == -1) {
            var request = new TabChangeRequestedEventArgs(viewPort);
            RequestTabChange(this, request);
            if (request.RequestAccepted) (viewPort as ViewPort).SetJumpBackTab(this);
            return;
         }
         if (primaryMap != null) backStack.Add(primaryMap.MapID);
         if (backStack.Count == 1) backCommand.RaiseCanExecuteChanged();
         if (forwardStack.Count == 0) forwardCommand.RaiseCanExecuteChanged();
         NavigateTo(next, trackChange: false);
      }
      private void NavigateTo(int mapID, bool trackChange = true) => NavigateTo(mapID / 1000, mapID % 1000, trackChange);
      private void NavigateTo(int bank, int map, bool trackChange = true) {
         // special case
         if (bank == 127 && map == 127) {
            ExecuteBack();
            return;
         }

         if (trackChange && primaryMap != null) {
            backStack.Add(primaryMap.MapID);
            if (backStack.Count == 1) backCommand.RaiseCanExecuteChanged();
            if (forwardStack.Count > 0) {
               forwardStack.Clear();
               forwardCommand.RaiseCanExecuteChanged();
            }
         }

         var template = VisibleMaps.FirstOrDefault(vm => vm.MapID == (bank * 1000 + map));
         VisibleMaps.Clear(); // need to clear in case the navigation takes us to a nearby map
         var newMap = new BlockMapViewModel(fileSystem, Tutorials, viewPort, format, bank, map) {
            IncludeBorders = primaryMap?.IncludeBorders ?? true,
            SpriteScale = primaryMap?.SpriteScale ?? .5,
         };
         if (template != null) (newMap.LeftEdge, newMap.TopEdge) = (template.LeftEdge, template.TopEdge);
         UpdatePrimaryMap(newMap);
      }

      #endregion

      public event EventHandler AutoscrollTiles;

      #region Constructor

      public bool IsValidState { get; private set; }

      public static bool TryCreateMapEditor(IFileSystem fileSystem, IEditableViewPort viewPort, Singletons singletons, MapTutorialsViewModel tutorials, out MapEditorViewModel editor) {
         editor = null;
         var maps = viewPort.Model.GetTable(HardcodeTablesModel.MapBankTable);
         if (maps == null) return false;
         editor = new MapEditorViewModel(fileSystem, viewPort, singletons, tutorials);
         return editor.IsValidState;
      }

      private MapEditorViewModel(IFileSystem fileSystem, IEditableViewPort viewPort, Singletons singletons, MapTutorialsViewModel tutorials) {
         (this.fileSystem, this.viewPort) = (fileSystem, viewPort);
         (this.singletons, Tutorials) = (singletons, tutorials);
         (model, history) = (viewPort.Model, viewPort.ChangeHistory);
         history.Undo.CanExecuteChanged += (sender, e) => undo.RaiseCanExecuteChanged();
         history.Redo.CanExecuteChanged += (sender, e) => redo.RaiseCanExecuteChanged();
         history.Bind(nameof(history.HasDataChange), (sender, e) => NotifyPropertyChanged(nameof(Name)));
         var isFRLG = model.IsFRLG();
         PrimaryTiles = isFRLG ? 640 : 512;
         PrimaryBlocks = PrimaryTiles;
         this.format = new Format(model);

         var map = new BlockMapViewModel(fileSystem, Tutorials, viewPort, format, 3, 0);
         templates = new(model, viewPort.Tools.CodeTool.ScriptParser, map.AllOverworldSprites);
         UpdatePrimaryMap(map);
         for (int i = 0; i < 0x40; i++) CollisionOptions.Add(i.ToString("X2"));

         ZoomLevel = "1x Zoom";
      }

      #endregion

      public void SwitchToViewPortOnNextBackNavigation() {
         backStack.Add(-1);
         if (backStack.Count == 1) backCommand.RaiseCanExecuteChanged();
         if (forwardStack.Count > 0) {
            forwardStack.Clear();
            forwardCommand.RaiseCanExecuteChanged();
         }
      }

      public void AddBackNavigation(int id) {
         backStack.Add(id);
         if (forwardStack.Count > 0) {
            forwardStack.Clear();
            forwardCommand.RaiseCanExecuteChanged();
         }
         if (backStack.Count == 1) backCommand.RaiseCanExecuteChanged();
      }

      public void AddForwardNavigation(int id) {
         forwardStack.Add(id);
         if (backStack.Count > 0) {
            backStack.Clear();
            backCommand.RaiseCanExecuteChanged();
         }
         if (forwardStack.Count == 1) forwardCommand.RaiseCanExecuteChanged();
      }

      private void UpdatePrimaryMap(BlockMapViewModel map) {
         if (!map.IsValidMap) {
            if (primaryMap != null && primaryMap.IsValidMap) return;
            map = VisibleMaps.FirstOrDefault(vis => vis.IsValidMap);
            if (map == null) {
               OnError.Raise(this, "Couldn't find a valid visible map.");
               Close.Execute();
               return;
            }
         }
         blockset = UpdateBlockset(map.MapID);
         if (blockset == -1) return;
         if (blockset == Pointer.NULL) return;
         // update the primary map
         if (VisibleMaps.FirstOrDefault(existing => existing.UpdateFrom(map)) is BlockMapViewModel loadedMap) map = loadedMap;
         if (primaryMap != map) {
            if (primaryMap != null) {
               primaryMap.NeighborsChanged -= PrimaryMapNeighborsChanged;
               primaryMap.PropertyChanged -= PrimaryMapPropertyChanged;
               primaryMap.CollisionHighlight = -1;
               primaryMap.AutoscrollTiles -= HandleAutoscrollTiles;
               primaryMap.HideSidePanels -= HandleHideSidePanels;
               primaryMap.RequestChangeMap -= HandleMapChangeRequest;
               primaryMap.RequestClearMapCaches -= HandleClearMapCaches;
               primaryMap.DeselectEvent();
               primaryMap.ShowBeneath = false;
               primaryMap.IsSelected = false;
            }
            primaryMap = map;
            primaryMap.BlockEditor.BlockIndex = drawBlockIndex;
            NotifyPropertyChanged(nameof(PrimaryMap));
            if (primaryMap != null) {
               primaryMap.NeighborsChanged += PrimaryMapNeighborsChanged;
               primaryMap.PropertyChanged += PrimaryMapPropertyChanged;
               primaryMap.CollisionHighlight = collisionIndex;
               primaryMap.AutoscrollTiles += HandleAutoscrollTiles;
               primaryMap.HideSidePanels += HandleHideSidePanels;
               primaryMap.RequestChangeMap += HandleMapChangeRequest;
               primaryMap.RequestClearMapCaches += HandleClearMapCaches;
               primaryMap.ShowBeneath = ShowBeneath;
               primaryMap.IsSelected = true;
            }
            selectedEvent = null;
            NotifyPropertiesChanged(nameof(Blocks), nameof(Name), nameof(ShowEventPanel));
         }

         // update the neighbor maps
         var oldMaps = VisibleMaps.ToList();
         var neighborDepth = 1;
         if (map.SpriteScale <= .5) neighborDepth = 2;
         if (map.SpriteScale <= .25) neighborDepth = 3;
         if (map.SpriteScale <= .125) neighborDepth = 4;
         if (map.SpriteScale <= .0625) neighborDepth = 5;
         var newMaps = GetMapNeighbors(map, neighborDepth).ToList();
         newMaps.Add(map);
         var mapDict = new Dictionary<int, BlockMapViewModel>();
         newMaps.ForEach(m => mapDict[m.MapID] = m);
         newMaps = mapDict.Values.ToList();
         foreach (var oldM in oldMaps) if (!newMaps.Any(newM => oldM.MapID == newM.MapID)) VisibleMaps.Remove(oldM);
         foreach (var newM in newMaps) {
            bool match = false;
            foreach (var existingM in VisibleMaps) {
               if (existingM.UpdateFrom(newM)) { 
                  match = true;
                  break;
               }
            }
            if (!match) VisibleMaps.Add(newM);
         }

         // refresh connection buttons
         var newButtons = primaryMap.GetMapSliders().ToList();
         for (int i = 0; i < MapButtons.Count && i < newButtons.Count; i++) {
            if (!MapButtons[i].TryUpdate(newButtons[i])) MapButtons[i] = newButtons[i];
         }
         for (int i = MapButtons.Count; i < newButtons.Count; i++) MapButtons.Add(newButtons[i]);
         while (MapButtons.Count > newButtons.Count) MapButtons.RemoveAt(MapButtons.Count - 1);

         UpdateBlockBagVisual();
         IsValidState = true;
      }

      private void PrimaryMapNeighborsChanged(object? sender, EventArgs e) {
         UpdatePrimaryMap(primaryMap);
      }

      private void PrimaryMapPropertyChanged(object sender, PropertyChangedEventArgs e) {
         var map = (BlockMapViewModel)sender;
         if (e.PropertyName == nameof(BlockMapViewModel.SelectedEvent)) {
            if (map.SelectedEvent != selectedEvent) {
               SelectedEvent = map.SelectedEvent;
            }
         } else if (e.PropertyName == nameof(BlockMapViewModel.FullName)) {
            NotifyPropertyChanged(nameof(Name));
         } else if (e.PropertyName == nameof(BlockMapViewModel.BlockPixels)) {
            NotifyPropertyChanged(nameof(Blocks));
         } else if (e.PropertyName == nameof(BlockMapViewModel.BlockRenders)) {
            FillMultiTileRender();
         }
      }

      private IEnumerable<BlockMapViewModel> GetMapNeighbors(BlockMapViewModel map, int recursionLevel) {
         if (recursionLevel < 1) return new BlockMapViewModel[0];
         var directions = new List<MapDirection> {
            MapDirection.Up, MapDirection.Down, MapDirection.Left, MapDirection.Right, MapDirection.Dive, MapDirection.Emerge,
         };

         var newMaps = new Dictionary<int, BlockMapViewModel>();
         foreach (var child in directions.SelectMany(map.GetNeighbors)) {
            if (newMaps.ContainsKey(child.MapID)) continue;
            newMaps.Add(child.MapID, child);
         }
         if (recursionLevel > 1) {
            foreach (var key in newMaps.Keys.ToList()) {
               if (newMaps[key].ZIndex < 0) continue;
               foreach (var child in GetMapNeighbors(newMaps[key], recursionLevel - 1)) {
                  if (newMaps.ContainsKey(child.MapID)) continue;
                  newMaps.Add(child.MapID, child);
               }
            }
         }
         return newMaps.Values.ToList();

         //var newMaps = new List<BlockMapViewModel>(directions.SelectMany(map.GetNeighbors));
         //foreach (var m in newMaps) {
         //   yield return m;
         //   if (recursionLevel > 1 && m.ZIndex >= 0) {
         //      foreach (var mm in GetMapNeighbors(m, recursionLevel - 1)) yield return mm;
         //   }
         //}
      }

      #region Map Interaction

      private double cursorX, cursorY, deltaX, deltaY;
      public event EventHandler AutoscrollBlocks;

      private bool showHighlightCursor;
      public bool ShowHighlightCursor { get => showHighlightCursor; set => Set(ref showHighlightCursor, value); }

      private double highlightCursorX, highlightCursorY, highlightCursorWidth, highlightCursorHeight;
      public double HighlightCursorX { get => highlightCursorX; set => Set(ref highlightCursorX, value); }
      public double HighlightCursorY { get => highlightCursorY; set => Set(ref highlightCursorY, value); }
      public double HighlightCursorWidth { get => highlightCursorWidth; set => Set(ref highlightCursorWidth, value); }
      public double HighlightCursorHeight { get => highlightCursorHeight; set => Set(ref highlightCursorHeight, value); }

      private bool showBeneath;
      public bool ShowBeneath { get => showBeneath; set => Set(ref showBeneath, value, old => PrimaryMap.ShowBeneath = ShowBeneath); }

      // if it returns an empty array: no hover tip to display
      // if it returns null: continue displaying previous hover tip
      // if it returns content: display that as the new hover tip
      private static readonly object[] EmptyTooltip = new object[0]; 
      public object Hover(double x, double y) {
         var map = MapUnderCursor(x, y);
         if (map == null) return EmptyTooltip;
         if (drawMultipleTiles && tilesToDraw != null) {
            var p = ToBoundedMapTilePosition(map, x, y, tilesToDraw.GetLength(0), tilesToDraw.GetLength(1));
            if (interactionType == PrimaryInteractionType.Draw) {
               while (Math.Abs(p.X - drawSource.X) % tilesToDraw.GetLength(0) != 0) p -= new Point(1, 0);
               while (Math.Abs(p.Y - drawSource.Y) % tilesToDraw.GetLength(1) != 0) p -= new Point(0, 1);
            }
            UpdateHover(p.X, p.Y, tilesToDraw.GetLength(0), tilesToDraw.GetLength(1));
            HoverPoint = string.Empty;
         } else {
            var p = ToBoundedMapTilePosition(map, x, y, 1, 1);
            map.HoverPoint = ToPixelPosition(x, y);
            if (UpdateHover(p.X, p.Y, 1, 1)) {
               HoverPoint = $"({p.X}, {p.Y})";
               if (interactionType == PrimaryInteractionType.None && map.EventUnderCursor(x, y, false) is BaseEventViewModel ev) {
                  return ShowEventHover(ev);
               } else {
                  return EmptyTooltip;
               }
            }
            return null;
         }
         return EmptyTooltip;
      }

      /// <summary>
      /// returns true if the hover changed
      /// </summary>
      private bool UpdateHover(int left, int top, int width, int height) {
         var (prevX, prevY) = (highlightCursorX, highlightCursorY);
         var (prevW, prevH) = (highlightCursorWidth, highlightCursorHeight);

         var border = primaryMap.GetBorderThickness();
         if (border == null) return false;
         ShowHighlightCursor = true;
         HighlightCursorX = (left + border.West + width / 2.0) * 16 * primaryMap.SpriteScale + primaryMap.LeftEdge;
         HighlightCursorY = (top + border.North + height / 2.0) * 16 * primaryMap.SpriteScale + primaryMap.TopEdge;
         HighlightCursorWidth = width * 16 * primaryMap.SpriteScale + 4;
         HighlightCursorHeight = height * 16 * primaryMap.SpriteScale + 4;

         if (prevX != highlightCursorX) return true;
         if (prevY != highlightCursorY) return true;
         if (prevW != highlightCursorWidth) return true;
         return prevH != highlightCursorHeight;
      }

      public void DragDown(double x, double y) {
         if (primaryMap.BlockEditor != null) PrimaryMap.BlockEditor.ShowTiles = false;
         PrimaryMap.BorderEditor.ShowBorderPanel = false;
         (cursorX, cursorY) = (x, y);
         (deltaX, deltaY) = (0, 0);

         var map = MapUnderCursor(x, y);
         if (map != null) UpdatePrimaryMap(map);
      }

      public void DragMove(double x, double y, bool isMiddleClickMap) {
         deltaX += x - cursorX;
         deltaY += y - cursorY;
         var (intX, intY) = ((int)deltaX, (int)deltaY);
         deltaX -= intX;
         deltaY -= intY;
         (cursorX, cursorY) = (x, y);
         Pan(intX, intY);
         if ((intX != 0 || intY != 0) && isMiddleClickMap) Tutorials.Complete(Tutorial.MiddleClick_PanMap);
         if (isMiddleClickMap) Hover(x, y);
      }

      public void DragUp(double x, double y) { }

      #region Primary Interaction (left-click)

      private PrimaryInteractionType interactionType;
      public void PrimaryDown(double x, double y, PrimaryInteractionStart click) {
         PrimaryMap.BlockEditor.ShowTiles = false;
         PrimaryMap.BorderEditor.ShowBorderPanel = false;
         var map = MapUnderCursor(x, y);
         if (map == null) {
            interactionType = PrimaryInteractionType.None;
            return;
         }
         PrimaryMap = map;

         var prevEvent = SelectedEvent;
         var ev = map.EventUnderCursor(x, y);
         if (ev != null) {
            EventDown(x, y, ev, click);
            return;
         } else {
            if (prevEvent != null) Tutorials.Complete(Tutorial.ClickMap_UnselectEvent);
            SelectedEvent = null;
         }

         ShowHeaderPanel = false;
         if (click == PrimaryInteractionStart.DoubleClick && ShowBeneath) {
            PrimaryMap.SurfConnection.FollowConnection();
            return;
         }
         DrawDown(x, y, click);
      }

      public void PrimaryMove(double x, double y) {
         if (interactionType == PrimaryInteractionType.Draw) DrawMove(x, y);
         if (interactionType == PrimaryInteractionType.RectangleDraw) RectangleDrawMove(x, y);
         if (interactionType == PrimaryInteractionType.Draw9Grid) Draw9Grid(x, y);
         if (interactionType == PrimaryInteractionType.Event) EventMove(x, y);
      }

      public void PrimaryUp(double x, double y) {
         if (interactionType == PrimaryInteractionType.Draw) DrawUp(x, y);
         if (interactionType == PrimaryInteractionType.RectangleDraw) DrawUp(x, y);
         if (interactionType == PrimaryInteractionType.Draw9Grid) DrawUp(x, y);
         if (interactionType == PrimaryInteractionType.Event) EventUp(x, y);
         interactionType = PrimaryInteractionType.None;
      }

      Point drawSource, lastDraw;
      private void DrawDown(double x, double y, PrimaryInteractionStart click) {
         interactionType = PrimaryInteractionType.Draw;
         if ((click & PrimaryInteractionStart.ControlClick) != 0) interactionType = PrimaryInteractionType.RectangleDraw;
         if (click == PrimaryInteractionStart.ShiftClick) interactionType = PrimaryInteractionType.Draw9Grid;
         var map = MapUnderCursor(x, y);
         if ((click & PrimaryInteractionStart.DoubleClick) != 0) {
            if (drawBlockIndex < 0 && collisionIndex < 0) {
               // nothing to paint
               interactionType = PrimaryInteractionType.None;
            } else {
               if (interactionType == PrimaryInteractionType.RectangleDraw && map != null) {
                  map.PaintWaveFunction(history.CurrentChange, x, y, RunWaveFunctionCollapseWithCollision);
               } else if (map != null && !drawMultipleTiles) {
                  if (blockBag.Contains(drawBlockIndex)) {
                     map.PaintBlockBag(history.CurrentChange, blockBag, collisionIndex, x, y);
                  } else {
                     map.PaintBlock(history.CurrentChange, drawBlockIndex, collisionIndex, x, y);
                  }
                  Tutorials.Complete(Tutorial.DoubleClick_PaintBlock);
               }
            }
            Hover(x, y);
         } else {
            drawSource = ToBoundedMapTilePosition(map, x, y, 1, 1);
            lastDraw = drawSource;
            if (click == PrimaryInteractionStart.ControlClick) RectangleDrawMove(x, y);
            if (click == PrimaryInteractionStart.Click) DrawMove(x, y);
            if (click == PrimaryInteractionStart.ShiftClick) Draw9Grid(x, y);
         }
      }

      private void DrawMove(double x, double y) {
         var map = MapUnderCursor(x, y);
         if (map != null) {
            using (map.DeferPropertyNotifications()) {
               if (drawMultipleTiles && tilesToDraw != null) {
                  var tilePosition = ToBoundedMapTilePosition(map, x, y, tilesToDraw.GetLength(0), tilesToDraw.GetLength(1));
                  map.DrawBlocks(history.CurrentChange, tilesToDraw, drawSource, tilePosition);
               } else {
                  var tilePosition = ToBoundedMapTilePosition(map, x, y, 1, 1);
                  if (drawBlockIndex < 0 && collisionIndex < 0) {
                     if (lastDraw != tilePosition) {
                        ResetFromRectangleBackup();
                        lastDraw = tilePosition;
                        FillBackup();
                        SwapBlocks(lastDraw, drawSource);
                     }
                  } else if (blockBag.Contains(drawBlockIndex)) {
                     map.DrawBlock(history.CurrentChange, rnd.From(blockBag), collisionIndex, x, y);
                  } else {
                     map.DrawBlock(history.CurrentChange, drawBlockIndex, collisionIndex, x, y);
                  }
               }
            }
         }
         Hover(x, y);
      }

      private void RectangleDrawMove(double x, double y) {
         var map = MapUnderCursor(x, y);
         if (tilesToDraw == null && drawBlockIndex < 0 && collisionIndex < 0) {
            interactionType = PrimaryInteractionType.None;
         } else if (map != null) {
            ResetFromRectangleBackup();
            lastDraw = ToBoundedMapTilePosition(map, x, y, 1, 1);
            if (lastDraw != drawSource) Tutorials.Complete(Tutorial.ControlClick_FillRect);
            FillBackup();
            FillRect();
            UpdateHover(Math.Min(drawSource.X, lastDraw.X), Math.Min(drawSource.Y, lastDraw.Y), Math.Abs(drawSource.X - lastDraw.X) + 1, Math.Abs(drawSource.Y - lastDraw.Y) + 1);
         }
      }

      private void Draw9Grid(double x, double y) {
         var map = MapUnderCursor(x, y);
         map.Draw9Grid(history.CurrentChange, drawBlockIndex, collisionIndex, x, y);
         Hover(x, y);
      }

      private void DrawUp(double x, double y) {
         rectangleBackup = null;
         history.ChangeCompleted();
         interactionType = PrimaryInteractionType.None;
         primaryMap.RedrawEvents(); // editing blocks in a map can draw over events, we need to redraw events now that we have new blocks
      }

      private void EventDown(double x, double y, IEventViewModel ev, PrimaryInteractionStart click) {
         SelectedEvent = ev;
         if (SelectedEvent is WarpEventViewModel warp && click == PrimaryInteractionStart.DoubleClick) {
            var banks = AllMapsModel.Create(warp.Element.Model, default);
            interactionType = PrimaryInteractionType.None;
            if (warp.Bank == 127 && warp.Map == 127) { // special case -> 'warp back'
               NavigateTo(warp.Bank, warp.Map);
               Tutorials.Complete(Tutorial.DoubleClick_FollowWarp);
            } else if (banks[warp.Bank] == null) {
               OnError.Raise(this, $"Could not load map bank {warp.Bank}");
            } else if (warp.Bank < banks.Count) {
               NavigateTo(warp.Bank, warp.Map);
               Tutorials.Complete(Tutorial.DoubleClick_FollowWarp);
            }
         } else if (click == PrimaryInteractionStart.DoubleClick && SelectedEvent is ObjectEventViewModel obj) {
            if (0 <= obj.ScriptAddress && obj.ScriptAddress < model.Count) {
               viewPort.Goto.Execute(obj.ScriptAddress);
               RequestTabChange?.Invoke(this, new(viewPort));
            } else {
               OnError.Raise(this, "Not a valid script address.");
            }
            Tutorials.Complete(Tutorial.DoubleClickEvent_SeeScript);
         } else if (click == PrimaryInteractionStart.DoubleClick && SelectedEvent is ScriptEventViewModel script) {
            if (0 <= script.ScriptAddress && script.ScriptAddress < model.Count) {
               viewPort.Goto.Execute(script.ScriptAddress);
            } else {
               OnError.Raise(this, "Not a valid script address.");
            }
            Tutorials.Complete(Tutorial.DoubleClickEvent_SeeScript);
         } else if (
            click == PrimaryInteractionStart.DoubleClick &&
            SelectedEvent is SignpostEventViewModel signpost &&
            signpost.ShowPointer &&
            signpost.Pointer >= 0 &&
            signpost.Pointer < model.Count
         ) {
            viewPort.Goto.Execute(signpost.Pointer);
            Tutorials.Complete(Tutorial.DoubleClickEvent_SeeScript);
         } else {
            Tutorials.Complete(Tutorial.LeftClick_SelectEvent);
            interactionType = PrimaryInteractionType.Event;
         }
      }

      private void EventMove(double x, double y) {
         var map = MapUnderCursor(x, y);
         if (map == primaryMap) {
            CreateEventForCreationInteraction(eventCreationType);
            map.UpdateEventLocation(selectedEvent, x, y);
            Tutorials.Complete(Tutorial.DragEvent_MoveEvent);
         }
         Hover(x, y);
      }

      private void EventUp(double x, double y) {
         history.ChangeCompleted();
         if (!withinEventCreationInteraction) return;
         withinEventCreationInteraction = false;
         UpdatePrimaryMap(primaryMap); // re-add neighbors
         if (eventCreationType == EventCreationType.Object) {
            // User clicked on the event creation button,
            // but then didn't drag to anywhere.
            // Open the popup.
            ShowTemplateSettings = true;
            eventCreationType = EventCreationType.None;
         }
      }

      #region Rectangle-Drawing helper methods

      // this will hold whatever blocks were originally in the map, so we can put them back as the user moves the rect around.
      int[,] rectangleBackup;

      /// <summary>
      /// Use the values of drawSource and lastDraw to fill the map from the backup
      /// </summary>
      private void ResetFromRectangleBackup() {
         if (rectangleBackup == null) return;
         var (left, right) = (lastDraw.X, drawSource.X);
         var (top, bottom) = (lastDraw.Y, drawSource.Y);
         if (left > right) (left, right) = (right, left);
         if (top > bottom) (top, bottom) = (bottom, top);
         var (width, height) = (right - left + 1, bottom - top + 1);
         primaryMap.RepeatBlocks(() => history.CurrentChange, rectangleBackup, left, top, width, height, false);
      }

      /// <summary>
      /// Use the values of drawSource and lastDraw to fill the backup from the current map
      /// </summary>
      private void FillBackup() {
         var (left, right) = (lastDraw.X, drawSource.X);
         var (top, bottom) = (lastDraw.Y, drawSource.Y);
         if (left > right) (left, right) = (right, left);
         if (top > bottom) (top, bottom) = (bottom, top);
         var (width, height) = (right - left + 1, bottom - top + 1);
         rectangleBackup = primaryMap.ReadRectangle(left, top, width, height);
      }

      /// <summary>
      /// Fill the map from drawSource, lastDraw, drawBlockIndex, drawCollisionIndex, and tilesToDraw
      /// </summary>
      private void FillRect() {
         var (left, right) = (lastDraw.X, drawSource.X);
         var (top, bottom) = (lastDraw.Y, drawSource.Y);
         if (left > right) (left, right) = (right, left);
         if (top > bottom) (top, bottom) = (bottom, top);
         var (width, height) = (right - left + 1, bottom - top + 1);

         if (tilesToDraw == null) {
            primaryMap.RepeatBlock(() => history.CurrentChange, drawBlockIndex, collisionIndex, left, top, width, height, true);
         } else {
            primaryMap.RepeatBlocks(() => history.CurrentChange, tilesToDraw, left, top, width, height, true);
         }
      }

      private void SwapBlocks(Point a, Point b) {
         var p1 = primaryMap.ReadRectangle(a.X, a.Y, 1, 1);
         var p2 = primaryMap.ReadRectangle(b.X, b.Y, 1, 1);
         primaryMap.RepeatBlocks(() => history.CurrentChange, p1, b.X, b.Y, 1, 1, false);
         primaryMap.RepeatBlocks(() => history.CurrentChange, p2, a.X, a.Y, 1, 1, true);
         if (a != b) Tutorials.Complete(Tutorial.DragMap_SwapBlock);
      }

      #endregion

      #endregion

      private bool withinEventCreationInteraction = false;
      private EventCreationType eventCreationType;
      public void StartEventCreationInteraction(EventCreationType type) {
         interactionType = PrimaryInteractionType.Event;
         VisibleMaps.Clear();
         VisibleMaps.Add(primaryMap);
         MapButtons.Clear();
         eventCreationType = type;
         withinEventCreationInteraction = true;
      }

      private void CreateEventForCreationInteraction(EventCreationType type) {
         if (type == EventCreationType.None) return;
         eventCreationType = EventCreationType.None;
         if (type == EventCreationType.Object) {
            var objectEvent = primaryMap.CreateObjectEvent(0, Pointer.NULL);
            if (objectEvent == null) return;
            templates.ApplyTemplate(objectEvent, history.CurrentChange);
            SelectedEvent = objectEvent;
            if (objectEvent.ScriptAddress != Pointer.NULL) primaryMap.InformCreate(new("Object-Event", objectEvent.ScriptAddress));
            Tutorials.Complete(Tutorial.ToolbarTemplate_CreateObject);
         } else if (type == EventCreationType.Warp) {
            var desiredMap = (bank: primaryMap.MapID / 1000, map: primaryMap.MapID % 1000);
            if (backStack.Count > 0) {
               var last = backStack[backStack.Count - 1];
               if (last >= 0) {
                  desiredMap = (bank: last / 1000, map: last % 1000);
               }
            }
            SelectedEvent = primaryMap.CreateWarpEvent(desiredMap.bank, desiredMap.map);
            if (SelectedEvent == null) return;
            Tutorials.Complete(Tutorial.ToolbarTemplate_CreateEvent);
         } else if (type == EventCreationType.Script) {
            SelectedEvent = primaryMap.CreateScriptEvent();
            if (SelectedEvent == null) return;
            Tutorials.Complete(Tutorial.ToolbarTemplate_CreateEvent);
         } else if (type == EventCreationType.Signpost) {
            var signpost = primaryMap.CreateSignpostEvent();
            templates.ApplyTemplate(signpost, history.CurrentChange);
            SelectedEvent = signpost;
            if (SelectedEvent == null) return;
            Tutorials.Complete(Tutorial.ToolbarTemplate_CreateEvent);
            // TODO primaryMap.InformCreate if we created a script
         } else if (type == EventCreationType.Fly) {
            var flySpot = primaryMap.CreateFlyEvent();
            if (flySpot != null) SelectedEvent = flySpot;
         } else {
            throw new NotImplementedException();
         }
      }

      #region Selection (right-click)

      int[,] tilesToDraw;
      Point selectDownPosition;
      private bool drawMultipleTiles;
      public bool DrawMultipleTiles {
         get => drawMultipleTiles;
         private set => Set(ref drawMultipleTiles, value, arg => { if (!drawMultipleTiles) tilesToDraw = null; });
      }

      private bool blockEditorVisible;
      public bool BlockEditorVisible { get => blockEditorVisible; private set => Set(ref blockEditorVisible, value); }

      private IPixelViewModel multiTileDrawRender;
      public IPixelViewModel MultiTileDrawRender {
         get => multiTileDrawRender;
         set {
            multiTileDrawRender = value;
            NotifyPropertyChanged();
         }
      }

      public SelectionInteractionResult SelectDown(double x, double y) {
         PrimaryMap.BlockEditor.ShowTiles = false;
         PrimaryMap.BorderEditor.ShowBorderPanel = false;
         var map = MapUnderCursor(x, y);
         if (map == null) return SelectionInteractionResult.None;
         PrimaryMap = map;
         ShowHeaderPanel = false;
         var (blockIndex, collisionIndex) = map.GetBlock(x, y);
         SelectedEvent = null;
         selectDownPosition = ToBoundedMapTilePosition(map, x, y, 1, 1);
         UpdateHover(selectDownPosition.X, selectDownPosition.Y, 1, 1);
         var ev = map.EventUnderCursor(x, y);
         if (ev != null) {
            ShowEventContextMenu(ev);
            return SelectionInteractionResult.ShowMenu;
         }
         if (blockIndex >= 0) {
            DrawBlockIndex = blockIndex;
            AutoscrollBlocks.Raise(this);
         }
         if (collisionIndex >= 0) CollisionIndex = collisionIndex;
         return SelectionInteractionResult.None;
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
         UpdateHover(left, top, width, height);
      }

      public void SelectUp(double x, double y) {
         interactionType = PrimaryInteractionType.None;
         var map = MapUnderCursor(x, y);
         if (map != primaryMap) return;
         if (x < map.LeftEdge || x > map.LeftEdge + map.PixelWidth * map.SpriteScale) return;
         if (y < map.TopEdge || y > map.TopEdge + map.PixelHeight * map.SpriteScale) return;
         var selectMovePosition = ToBoundedMapTilePosition(map, x, y, 1, 1);
         var left = Math.Min(selectDownPosition.X, selectMovePosition.X);
         var top = Math.Min(selectDownPosition.Y, selectMovePosition.Y);
         var width = Math.Abs(selectDownPosition.X - selectMovePosition.X) + 1;
         var height = Math.Abs(selectDownPosition.Y - selectMovePosition.Y) + 1;
         tilesToDraw = new int[width, height];
         if (width == 1 && height == 1) {
            SelectSingleBlock();
            UpdateHover(left, top, 1, 1);
            Tutorials.Complete(Tutorial.RightClickMap_SelectBlock);
            return;
         }

         bool fillError = false;
         for (int xx = 0; xx < width; xx++) {
            for (int yy = 0; yy < height; yy++) {
               var (tX, tY) = ToMapPosition(left + xx, top + yy);
               var block = primaryMap.GetBlock(tX, tY);
               if (block.blockIndex == -1 || block.collisionIndex == -1) {
                  fillError = true;
                  break;
               }
               tilesToDraw[xx, yy] = (block.collisionIndex << 10) | block.blockIndex;
            }
         }
         if (fillError) {
            DrawMultipleTiles = false;
            tilesToDraw = null;
            UpdateHover(selectMovePosition.X, selectMovePosition.Y, 1, 1);
         } else {
            Tutorials.Complete(Tutorial.RightDragMap_SelectBlocks);
            FillMultiTileRender();
            DrawMultipleTiles = true;
            BlockEditorVisible = false;
            PrimaryMap.BlockEditor.ShowTiles = false;
            UpdateHover(left, top, width, height);
         }
      }

      private void SelectSingleBlock() {
         DrawMultipleTiles = false;
         BlockEditorVisible = true;
         if (tilesToDraw == null) tilesToDraw = new int[1, 1];
         tilesToDraw[0, 0] = drawBlockIndex | (collisionIndex << 10);
         FillMultiTileRender();
         AnimateBlockSelection();
      }

      private void FillMultiTileRender() {
         if (tilesToDraw == null) return;
         var (width, height) = (tilesToDraw.GetLength(0), tilesToDraw.GetLength(1));
         var scale = (width < 4 && height < 4) ? 2 : 1;
         var canvas = new CanvasPixelViewModel(width * 16, height * 16) { SpriteScale = scale };
         for (int xx = 0; xx < tilesToDraw.GetLength(0); xx++) {
            for (int yy = 0; yy < tilesToDraw.GetLength(1); yy++) {
               var index = tilesToDraw[xx, yy] & 0x3FF;
               if (index >= primaryMap.BlockRenders.Count) index = 0;
               canvas.Draw(primaryMap.BlockRenders[index], xx * 16, yy * 16);
            }
         }
         MultiTileDrawRender = canvas;
      }

      private Point ToTilePosition(double x, double y) {
         (x, y) = ((x - primaryMap.LeftEdge) / primaryMap.SpriteScale / 16, (y - primaryMap.TopEdge) / primaryMap.SpriteScale / 16);
         var borders = primaryMap.GetBorderThickness();
         var position = new Point((int)Math.Floor(x) - borders.West, (int)Math.Floor(y) - borders.North);
         return position;
      }

      private Point ToBoundedMapTilePosition(BlockMapViewModel map, double x, double y, int selectionWidth, int selectionHeight) {
         (x, y) = ((x - map.LeftEdge) / map.SpriteScale / 16, (y - map.TopEdge) / map.SpriteScale / 16);
         var borders = map.GetBorderThickness();
         if (borders == null) return new(0, 0);
         var position = new Point((int)Math.Floor(x) - borders.West, (int)Math.Floor(y) - borders.North);

         // limit to within the content of this map
         var width = map.PixelWidth / 16 - borders.West - borders.East;
         var height = map.PixelHeight / 16 - borders.North - borders.South;
         position = new(position.X.LimitToRange(0, width - selectionWidth), position.Y.LimitToRange(0, height - selectionHeight));

         // offset based on primary map
         var primarySize = primaryMap.GetBorderThickness();
         var mapLeft = (int)((map.LeftEdge - primaryMap.LeftEdge) / map.SpriteScale / 16);
         var mapTop = (int)((map.TopEdge - primaryMap.TopEdge) / map.SpriteScale / 16);
         position = new(position.X + mapLeft + borders.West - primarySize.West, position.Y + mapTop + borders.North - primarySize.North);

         return position;
      }

      private ImageLocation ToPixelPosition(double x, double y) {
         (x, y) = ((x - primaryMap.LeftEdge) / primaryMap.SpriteScale, (y - primaryMap.TopEdge) / primaryMap.SpriteScale);
         return new ImageLocation(x / primaryMap.PixelWidth, y / primaryMap.PixelHeight);
      }

      private (double, double) ToMapPosition(int x, int y) {
         var borders = primaryMap.GetBorderThickness();
         var pX = (x + borders.West) * 16 * primaryMap.SpriteScale + primaryMap.LeftEdge;
         var pY = (y + borders.North) * 16 * primaryMap.SpriteScale + primaryMap.TopEdge;
         return (pX, pY);
      }

      #endregion

      #region Event Hover / Context Menus

      private IEventViewModel eventContext;

      private void ShowEventContextMenu(IEventViewModel ev) {
         interactionType = PrimaryInteractionType.None;
         eventContext = ev;
         ContextItems.Clear();

         if (ev is ObjectEventViewModel objEvent) {
            ContextItems.Add(new MenuCommand("Delete Object", DeleteCurrentEvent));
         } else if (ev is WarpEventViewModel warp) {
            ContextItems.Add(new MenuCommand("Create New Map", CreateMapForWarp));
            ContextItems.Add(new MenuCommand("Open in New Tab", () => Duplicate(warp.Bank, warp.Map)));
            ContextItems.Add(new MenuCommand("Delete Warp", DeleteCurrentEvent));
         } else if (ev is ScriptEventViewModel script) {
            ContextItems.Add(new MenuCommand("Delete Script", DeleteCurrentEvent));
         } else if (ev is SignpostEventViewModel signpost) {
            ContextItems.Add(new MenuCommand("Delete Signpost", DeleteCurrentEvent));
         } else if (ev is FlyEventViewModel fly) {
            ContextItems.Add(new MenuCommand("Delete Fly Spot", DeleteCurrentEvent));
         }
      }

      private object[] ShowEventHover(BaseEventViewModel ev) {
         var tips = new List<object>(); // ReadOnlyPixelViewModel and string
         if (ev is WarpEventViewModel warp) {
            tips.Add(warp.TargetMapName);
            var banks = AllMapsModel.Create(warp.Element.Model, default);
            if (banks[warp.Bank] == null) return tips.ToArray();
            if (warp.Bank < banks.Count && warp.Map < banks[warp.Bank].Count) {
               var blockmap = new BlockMapViewModel(FileSystem, Tutorials, viewPort, format, warp.Bank, warp.Map) { AllOverworldSprites = primaryMap.AllOverworldSprites, IncludeBorders = false };
               var image = blockmap.AutoCrop(warp.WarpID - 1);
               if (image != null) {
                  tips.Add(new ReadonlyPixelViewModel(image.PixelWidth, image.PixelHeight, image.PixelData));
               }
            }
         } else if (ev is ObjectEventViewModel obj) {
            tips.AddRange(SummarizeScript(obj.ScriptAddress));
         } else if (ev is ScriptEventViewModel script) {
            tips.AddRange(SummarizeScript(script.ScriptAddress));
         } else if (ev is SignpostEventViewModel signpost) {
            if (signpost.CanGotoScript) tips.AddRange(SummarizeScript(signpost.Pointer));
            if (signpost.ShowHiddenItemProperties) {
               var options = model.GetOptions(HardcodeTablesModel.ItemsTableName);
               var item = signpost.ItemID;
               if (item > 0 && item < options.Count) tips.Add(options[item]);
            }
         }

         return tips.ToArray();
      }

      public const int TextSummaryLimit = 60;
      private IEnumerable<object> SummarizeScript(int address) {
         var renderedAddresses = new HashSet<int>();
         var (parser, startPoints) = (viewPort.Tools.CodeTool.ScriptParser, new[] { address });
         var scriptSpots = Flags.GetAllScriptSpots(model, parser, startPoints, 0x0F, 0x67, 0x1A, 0x5C, 0x86); // loadpointer, preparemsg, copyvarifnotzero, trainerbattle, mart
         var tips = new List<object>();

         var trainerTable = model.GetTableModel(HardcodeTablesModel.TrainerTableName);
         var icons = model.GetTableModel(HardcodeTablesModel.PokeIconsTable);
         var trainerSprites = model.GetTableModel(HardcodeTablesModel.TrainerSpritesName);
         var itemSprites = model.GetTableModel(HardcodeTablesModel.ItemImagesTableName);
         var itemStats = model.GetTableModel(HardcodeTablesModel.ItemsTableName);
         foreach (var spot in scriptSpots) {
            if (renderedAddresses.Contains(spot.Address)) continue;
            if (model[spot.Address] == 0x0F) {
               // loadpointer (text)
               var textStart = model.ReadPointer(spot.Address + 2);
               if (0 <= textStart && textStart < model.Count) {
                  var text = model.TextConverter.Convert(model, textStart, TextSummaryLimit);
                  if (text.Length >= TextSummaryLimit) text += "...";
                  tips.Add(text);
                  renderedAddresses.Add(spot.Address);
               }
            } else if (model[spot.Address] == 0x67) {
               // preparemsg (text)
               var textStart = model.ReadPointer(spot.Address + 1);
               if (0 <= textStart && textStart < model.Count) {
                  var text = model.TextConverter.Convert(model, textStart, TextSummaryLimit);
                  if (text.Length >= TextSummaryLimit) text += "...";
                  tips.Add(text);
                  renderedAddresses.Add(spot.Address);
               }
            } else if (model[spot.Address] == 0x1A && itemStats != null) {
               // copyvarifnotzero (item)
               var itemAddress = EventTemplate.GetItemAddress(model, spot.Address);
               if (itemAddress == Pointer.NULL) continue;
               var itemID = model.ReadMultiByteValue(itemAddress, 2);
               if (itemID < 0 || itemID >= itemStats.Count || !itemStats[0].HasField("name")) continue;
               tips.Add(itemStats[itemID].GetStringValue("name"));
               renderedAddresses.Add(spot.Address);
            } else if (
               model[spot.Address] == 0x5C &&
               trainerTable != null &&
               trainerTable[0].HasField("sprite") &&
               trainerSprites != null &&
               icons != null &&
               icons[0].HasField("icon") &&
               trainerSprites[0].HasField("sprite")
            ) {
               // trainer
               if (spot.Address + 1 != 0x03) {
                  // 5C ** trainer: arg: playerwin<>
                  var textStart = model.ReadPointer(spot.Address + 6);
                  var text = model.TextConverter.Convert(model, textStart, TextSummaryLimit);
                  if (text.Length >= TextSummaryLimit) text += "...";
                  tips.Add(text);
                  renderedAddresses.Add(spot.Address);
               }

               var trainerID = model.ReadMultiByteValue(spot.Address + 2, 2);
               var pokemon = new List<IPixelViewModel>();
               var items = new List<IPixelViewModel>();
               var trainer = AggregateTrainerImages(trainerID, pokemon, items);
               if (trainer == null) continue;
               tips.Add(BuildTrainerSummaryImage(trainer, pokemon, items));
               renderedAddresses.Add(spot.Address);

               if (spot.Address + 1 == 0x03) {
                  // 5C ** trainer: arg: playerwin<>
                  var textStart = model.ReadPointer(spot.Address + 6);
                  if (textStart >= 0 && textStart < model.Count) {
                     var text = model.TextConverter.Convert(model, textStart, TextSummaryLimit);
                     if (text.Length >= TextSummaryLimit) text += "...";
                     tips.Add(text + Environment.NewLine);
                     renderedAddresses.Add(spot.Address);
                  }
               } else {
                  // 5C ** trainer: arg: start<> playerwin<>
                  var textStart = model.ReadPointer(spot.Address + 10);
                  if (textStart >= 0 && textStart < model.Count) {
                     var text = model.TextConverter.Convert(model, textStart, TextSummaryLimit);
                     if (text.Length >= TextSummaryLimit) text += "...";
                     tips.Add(text + Environment.NewLine);
                     renderedAddresses.Add(spot.Address);
                  }
               }
            } else if (model[spot.Address] == 0x86 && itemStats != null) {
               // mart
               var tableRun = model.GetNextRun(model.ReadPointer(spot.Address + 1)) as ITableRun;
               if (tableRun == null) continue;
               var table = new ModelTable(model, tableRun);
               int itemCount = 0;
               foreach (var item in table) {
                  if (itemCount == 5) { tips.Add("..."); break; }
                  var id = item.GetValue(0);
                  if (id < 0 || id >= itemStats.Count) continue;
                  var name = itemStats[id].GetStringValue("name");
                  tips.Add(name);
                  renderedAddresses.Add(spot.Address);
                  itemCount++;
               }
            }
         }
         if (address == Pointer.NULL) tips.Add("(no script)");
         return tips;
      }

      private IPixelViewModel AggregateTrainerImages(int trainerID, IList<IPixelViewModel> pokemon, IList<IPixelViewModel> items) {
         var trainerTable = model.GetTableModel(HardcodeTablesModel.TrainerTableName);
         var pokemonSprites = model.GetTableModel(HardcodeTablesModel.PokeIconsTable);
         var trainerSprites = model.GetTableModel(HardcodeTablesModel.TrainerSpritesName);
         var itemSprites = model.GetTableModel(HardcodeTablesModel.ItemImagesTableName);
         if (trainerTable == null || pokemonSprites == null || trainerSprites == null || itemSprites == null) return null;

         if (trainerID < 0 || trainerID >= trainerTable.Count) return null;
         var trainerSpriteID = trainerTable[trainerID].GetValue("sprite");
         var trainerSpriteStart = trainerSprites[trainerSpriteID].GetAddress("sprite");
         var trainerSpriteRun = model.GetNextRun(trainerSpriteStart) as ISpriteRun;
         if (trainerSpriteRun == null) return null;
         var trainerSprite = ReadonlyPixelViewModel.Create(model, trainerSpriteRun, true);

         var pokemonData = trainerTable[trainerID].GetSubTable("pokemon");
         if (pokemonData != null) {
            foreach (var mon in pokemonData) {
               if (!mon.TryGetValue("mon", out var species)) continue;
               if (species < 0 || species >= pokemonSprites.Count) continue;
               var iconStart = pokemonSprites[species].GetAddress("icon");
               var iconRun = model.GetNextRun(iconStart) as ISpriteRun;
               if (iconRun == null) continue;
               var p = ReadonlyPixelViewModel.Create(model, iconRun, true);
               p = ReadonlyPixelViewModel.Crop(p, 0, 0, 32, 32);
               pokemon.Add(p);
            }
         }

         // item sprites are optional (they don't even exist for R/S)
         if (itemSprites != null && itemSprites[0].HasField("sprite")) {
            var itemIDs = new List<int>();
            if (trainerTable[trainerID].TryGetValue("item1", out var itemID) && itemID > 0) itemIDs.Add(itemID);
            if (trainerTable[trainerID].TryGetValue("item2", out itemID) && itemID > 0) itemIDs.Add(itemID);
            if (trainerTable[trainerID].TryGetValue("item3", out itemID) && itemID > 0) itemIDs.Add(itemID);
            if (trainerTable[trainerID].TryGetValue("item4", out itemID) && itemID > 0) itemIDs.Add(itemID);
            foreach (var item in itemIDs) {
               if (itemSprites.Count <= item) continue;
               var itemStart = itemSprites[item].GetAddress("sprite");
               var itemRun = model.GetNextRun(itemStart) as ISpriteRun;
               if (itemRun == null) continue;
               var p = ReadonlyPixelViewModel.Create(model, itemRun, true);
               items.Add(p);
            }
         }

         return trainerSprite;
      }

      private ReadonlyPixelViewModel BuildTrainerSummaryImage(IPixelViewModel trainer, IReadOnlyList<IPixelViewModel> pokemon, IReadOnlyList<IPixelViewModel> items) {
         var pokemonColumns = pokemon.Count > 2 ? 2 : 1;
         var width = 64 + pokemonColumns * 32;
         var height = 64;
         var itemOffset = items.Count > 0 ? (40 / items.Count) : 0;
         var pokeOffset1 = pokemon.Count > 4 ? 16 : 32;
         var pokeOffset2 = pokemon.Count > 5 ? 16 : 32;
         var pokeCount2 = pokemon.Count > 2 ? pokemon.Count / 2 : 0;
         var pokeCount1 = pokemon.Count - pokeCount2;
         var data = new short[width * height];
         for (int i = 0; i < data.Length; i++) data[i] = -2;
         var canvas = new CanvasPixelViewModel(width, height, data) { Transparent = -2 };

         canvas.Draw(trainer, 0, 0);
         for (int i = 0; i < items.Count; i++) canvas.Draw(items[i], i * itemOffset, 40);
         for (int i = 0; i < pokeCount1; i++) canvas.Draw(pokemon[i], 64, i * pokeOffset1);
         for (int i = 0; i < pokeCount2; i++) canvas.Draw(pokemon[i + pokeCount1], 96, i * pokeOffset2);

         return new ReadonlyPixelViewModel(width, height, canvas.PixelData, canvas.Transparent);
      }

      public void CreateMapForWarp() {
         if (eventContext is not WarpEventViewModel warpContext) return;
         eventContext = null;
         Tutorials.Complete(Tutorial.RightClick_WarpNewMap);
         BlockMapViewModel newMap = primaryMap.CreateMapForWarp(warpContext);
         if (newMap == null) return;
         NavigateTo(newMap.MapID);
         history.ChangeCompleted();
      }

      public void DeleteCurrentEvent() {
         selectedEvent = eventContext;
         eventContext = null;
         Delete();
      }

      #endregion

      public void Zoom(double x, double y, bool enlarge) {
         var map = MapUnderCursor(x, y);
         if (map == null) map = primaryMap;
         map.Scale(x, y, enlarge);
         map.IncludeBorders = map.SpriteScale <= 3;
         UpdatePrimaryMap(map);
         if (map.SpriteScale >= 1) ZoomLevel = $"{(int)map.SpriteScale}x Zoom";
         else ZoomLevel = $"1/{(int)Math.Round(1 / map.SpriteScale)}x Zoom";
         Tutorials.Complete(Tutorial.Wheel_ZoomMap);
      }

      public void ResetZoom() {
         while (primaryMap.SpriteScale > 1) Zoom(0, 0, false);
         while (primaryMap.SpriteScale < 1) Zoom(0, 0, true);
      }

      private StubCommand panCommand, zoomCommand, deleteCommand, cancelCommand;
      public ICommand PanCommand => StubCommand<MapDirection>(ref panCommand, Pan);
      public ICommand ZoomCommand => StubCommand<ZoomDirection>(ref zoomCommand, Zoom);
      public ICommand DeleteCommand => StubCommand(ref deleteCommand, Delete);
      public ICommand CancelCommand => StubCommand(ref cancelCommand, Cancel);

      public void Pan(MapDirection direction) {
         int intX = 0, intY = 0;
         if (direction == MapDirection.Left) intX = -1;
         if (direction == MapDirection.Right) intX = 1;
         if (direction == MapDirection.Up) intY = -1;
         if (direction == MapDirection.Down) intY = 1;
         Pan(intX * -32, intY * -32);
         var map = MapUnderCursor(0, 0);
         Tutorials.Complete(Tutorial.ArrowKeys_PanMap);
         if (map != null) UpdatePrimaryMap(map);
      }

      public void Zoom(ZoomDirection direction) => Zoom(0, 0, direction == ZoomDirection.Enlarge);

      public void Delete() {
         if (selectedEvent == null) return;
         var success = selectedEvent.Delete();
         if (success) {
            SelectedEvent = null;
            primaryMap.RedrawEvents();
         } else {
            OnError?.Raise(this, "Event is hard-coded.");
         }
         history.ChangeCompleted();
      }

      public void Cancel() {
         SelectedEvent = null;
         DrawMultipleTiles = false;
         BlockEditorVisible = false;
         tilesToDraw = null;
         if (DrawBlockIndex != -1) Tutorials.Complete(Tutorial.EscapeKey_UnselectBlock);
         DrawBlockIndex = -1;
         CollisionIndex = -1;
         ShowHeaderPanel = false;
         HighlightCursorX -= (HighlightCursorWidth - (16 * primaryMap.SpriteScale + 4)) / 2;
         HighlightCursorY -= (HighlightCursorHeight - (16 * primaryMap.SpriteScale + 4)) / 2;
         HighlightCursorWidth = 16 * primaryMap.SpriteScale + 4;
         HighlightCursorHeight = 16 * primaryMap.SpriteScale + 4;
      }

      public void ClearSelection() {
         ShowHeaderPanel = false;
         DrawBlockIndex = -1;
         CollisionIndex = -1;
         SelectedEvent = null;
         DrawMultipleTiles = false;
         BlockEditorVisible = false;
         tilesToDraw = null;
         if (primaryMap != null) {
            if (primaryMap.BlockEditor != null) primaryMap.BlockEditor.ShowTiles = false;
            primaryMap.BorderEditor.ShowBorderPanel = false;
         }
      }

      private void Pan(int intX, int intY) {
         foreach (var map in VisibleMaps) {
            map.LeftEdge += intX;
            map.TopEdge += intY;
         }
         foreach (var button in MapButtons) {
            button.AnchorPositionX += button.AnchorLeftEdge ? intX : -intX;
            button.AnchorPositionY += button.AnchorTopEdge ? intY : -intY;
         }
      }

      private BlockMapViewModel MapUnderCursor(double x, double y) {
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

      private readonly List<int> blockBag = new();
      private bool selectionFromBlock = false;
      private Point blockInteractionStart;

      public void SelectBlock(int x, int y) {
         while (y * BlockMapViewModel.BlocksPerRow + x > PrimaryMap.BlockRenders.Count) y -= 1;
         blockInteractionStart = new(x, y);
         selectionFromBlock = false;
         drawBlockIndex = y * BlockMapViewModel.BlocksPerRow + x;
         NotifyPropertiesChanged(nameof(HighlightBlockX), nameof(HighlightBlockY), nameof(HighlightBlockWidth), nameof(HighlightBlockHeight));
         Tutorials.Complete(Tutorial.LeftClickBlock_SelectBlock);
      }

      public void DragBlock(int x, int y) {
         selectionFromBlock = true;
         while (y * BlockMapViewModel.BlocksPerRow + x > PrimaryMap.BlockRenders.Count) y -= 1;
         var (xx, right) = (x, blockInteractionStart.X);
         if (xx > right) (xx, right) = (right, xx);
         var (yy, bottom) = (y, blockInteractionStart.Y);
         if (yy > bottom) (yy, bottom) = (bottom, yy);
         if (bottom * BlockMapViewModel.BlocksPerRow + right > PrimaryMap.BlockRenders.Count) return;
         var (width, height) = (right - xx + 1, bottom - yy + 1);
         tilesToDraw = new int[width, height];
         for (int dx = 0; dx < width; dx++) {
            for (int dy = 0; dy < height; dy++) {
               tilesToDraw[dx, dy] = (yy + dy) * BlockMapViewModel.BlocksPerRow + xx + dx;
               int preferredCollision = Math.Max(collisionIndex, 0);
               if (autoUpdateCollision) preferredCollision = GetPreferredCollision(tilesToDraw[dx, dy]);
               tilesToDraw[dx, dy] |= preferredCollision << 10;
            }
         }
         drawBlockIndex = yy * BlockMapViewModel.BlocksPerRow + xx;
         NotifyPropertiesChanged(nameof(HighlightBlockX), nameof(HighlightBlockY), nameof(HighlightBlockWidth), nameof(HighlightBlockHeight));
         Tutorials.Complete(Tutorial.DragBlocks_SelectBlocks);
      }

      public void ReleaseBlock(int x, int y) {
         var (xx, right) = (x, blockInteractionStart.X);
         if (xx > right) (xx, right) = (right, xx);
         var (yy, bottom) = (y, blockInteractionStart.Y);
         if (yy > bottom) (yy, bottom) = (bottom, yy);
         if (bottom * BlockMapViewModel.BlocksPerRow + right > PrimaryMap.BlockRenders.Count) return;
         var (width, height) = (right - xx + 1, bottom - yy + 1);
         if (width == 1 && height == 1) {
            DrawMultipleTiles = false;
            BlockEditorVisible = true;
            drawBlockIndex = -1; // we want to run a full update on DrawBlockIndex
            DrawBlockIndex = y * BlockMapViewModel.BlocksPerRow + x;
            tilesToDraw = new int[width, height];
            tilesToDraw[0, 0] = drawBlockIndex;
            FillMultiTileRender();
            if (autoUpdateCollision) {
               var prefferredCollision = GetPreferredCollision(DrawBlockIndex);
               if (prefferredCollision >= 0) CollisionIndex = prefferredCollision;
            }
            if (collisionIndex >= 0) tilesToDraw[0, 0] |= collisionIndex << 10;
            return;
         }
         PrimaryMap.BlockEditor.ShowTiles = false;
         FillMultiTileRender();
         DrawMultipleTiles = true;
         BlockEditorVisible = false;
      }

      public void ToggleBlockInBag(int x, int y) {
         while (y * BlockMapViewModel.BlocksPerRow + x > PrimaryMap.BlockRenders.Count) y -= 1;
         var blockIndex = y * BlockMapViewModel.BlocksPerRow + x;
         if (blockBag.Contains(blockIndex)) {
            blockBag.Remove(blockIndex);
         } else {
            blockBag.Add(blockIndex);
         }
         UpdateBlockBagVisual();
      }

      public void ClearBlockBag() {
         blockBag.Clear();
         UpdateBlockBagVisual();
      }

      public IPixelViewModel BlockBag { get; private set; }
      private void UpdateBlockBagVisual() {
         var canvas = new CanvasPixelViewModel(16 * blockBag.Count, 16);
         var offset = 0;
         foreach (var block in blockBag) {
            var render = PrimaryMap.BlockRenders[block];
            canvas.Draw(render, offset, 0);
            offset += 16;
         }
         BlockBag = canvas;
         NotifyPropertyChanged(nameof(BlockBag));
      }

      #endregion

      #region ShiftInteraction

      private MapSlider shiftButton;

      public void ShiftDown(double x, double y) {
         (cursorX, cursorY) = (x, y);
         (deltaX, deltaY) = (0, 0);
         shiftButton = ButtonUnderCursor(x, y);
         HighlightCursorWidth = HighlightCursorHeight = 0;
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

      public void ShiftUp(double x, double y) => history.ChangeCompleted();

      private MapSlider ButtonUnderCursor(double x, double y) {
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

      #region Border Interaction

      public void DrawBorder(double x, double y) {
         if (!PrimaryMap.BorderEditor.ShowBorderPanel) return;
         if (DrawMultipleTiles) {
            if (tilesToDraw != null) PrimaryMap.BorderEditor.Draw(tilesToDraw, x, y);
         } else {
            if (drawBlockIndex != -1) PrimaryMap.BorderEditor.Draw(DrawBlockIndex, x, y);
         }
      }

      public void CompleteBorderDraw() => PrimaryMap.ViewPort.ChangeHistory.ChangeCompleted();

      public void ReadBorderBlock(double x, double y) {
         DrawBlockIndex = PrimaryMap.BorderEditor.GetBlock(x, y);
         tilesToDraw = new int[1, 1];
         AutoscrollBlocks.Raise(this);
         SelectSingleBlock();
      }

      #endregion

      #region Wave Function Collapse

      Dictionary<long, WaveNeighbors[]> waveFunctionPrimary;
      Dictionary<long, WaveNeighbors[]> waveFunctionSecondary;
      Dictionary<long, WaveNeighbors[]> waveFunctionMixed;

      private void CalculateWaveCollapseProbabilities() {
         waveFunctionPrimary = new();
         waveFunctionSecondary = new();
         waveFunctionMixed = new();
         foreach (var bank in AllMapsModel.Create(model, () => history.CurrentChange)) {
            foreach (var map in bank) {
               var cells = map.Blocks;
               var primary = map.Layout.PrimaryBlockset.Start;
               var secondary = map.Layout.SecondaryBlockset.Start;
               var mixed = ((long)primary << 32) + secondary;
               var primaryWaveNeighbors = waveFunctionPrimary.Ensure(primary, () => new WaveNeighbors[PrimaryBlocks]);
               var secondaryWaveNeighbors = waveFunctionSecondary.Ensure(secondary, () => new WaveNeighbors[TotalBlocks - PrimaryBlocks]);
               var mixedWaveNeighbors = waveFunctionMixed.Ensure(mixed, () => new WaveNeighbors[TotalBlocks]);

               for (int x = 0; x < cells.Width; x++) {
                  for (int y = 0; y < cells.Height; y++) {
                     var center = cells[x, y];
                     if (IsPrimaryBlock(center)) {
                        CheckPrimary(cells, x - 1, y, center, primaryWaveNeighbors, mixedWaveNeighbors, wn => wn.Left);
                        CheckPrimary(cells, x + 1, y, center, primaryWaveNeighbors, mixedWaveNeighbors, wn => wn.Right);
                        CheckPrimary(cells, x, y - 1, center, primaryWaveNeighbors, mixedWaveNeighbors, wn => wn.Up);
                        CheckPrimary(cells, x, y + 1, center, primaryWaveNeighbors, mixedWaveNeighbors, wn => wn.Down);
                     } else {
                        CheckSecondary(cells, x - 1, y, center, secondaryWaveNeighbors, mixedWaveNeighbors, wn => wn.Left);
                        CheckSecondary(cells, x + 1, y, center, secondaryWaveNeighbors, mixedWaveNeighbors, wn => wn.Right);
                        CheckSecondary(cells, x, y - 1, center, secondaryWaveNeighbors, mixedWaveNeighbors, wn => wn.Up);
                        CheckSecondary(cells, x, y + 1, center, secondaryWaveNeighbors, mixedWaveNeighbors, wn => wn.Down);
                     }
                  }
               }
            }
         }
      }

      private WaveCell RunWaveFunctionCollapseWithCollision(int xx, int yy) {
         var probabilities = RunWaveFunctionCollapse(xx, yy);
         return new(probabilities, GetPreferredCollision);
      }

      // wave function collapse algorithm:
      // for every block that we want to collapse, set the block to 0 and the collapse options to null (no restrictions)
      // for every block that has neighbors, find restrictions/odds based on those neighbors
      // repeate:
      //    find the current block with the tightest restriction, and pick an option based on the (weighted) options
      //    propogate new restrictions to neighboring unset blocks

      // if we ever get to a block that's restricted to 0 options, skip it until the end. Once there's onl 0-options choices left,
      // recalculate available options but only accounting for 3 sides at random, then 2 sides, then 1 side.
      // if we still can't find a neighbor after that, leave it blank.

      private IList<CollapseProbability> RunWaveFunctionCollapse(int xx, int yy) {
         if (waveFunctionPrimary == null) CalculateWaveCollapseProbabilities();
         var layout = new LayoutModel(PrimaryMap.GetLayout());
         var primary = layout.PrimaryBlockset.Start;
         var secondary = layout.SecondaryBlockset.Start;
         var mixed = ((long)primary << 32) + secondary;
         var primaryWaveNeighbors = waveFunctionPrimary.Ensure(primary, () => new WaveNeighbors[PrimaryBlocks]);
         var secondaryWaveNeighbors = waveFunctionSecondary.Ensure(secondary, () => new WaveNeighbors[TotalBlocks - PrimaryBlocks]);
         var mixedWaveNeighbors = waveFunctionMixed.Ensure(mixed, () => new WaveNeighbors[TotalBlocks]);

         var cells = layout.BlockMap;
         var probabilities = new List<List<CollapseProbability>>();
         AddProbabilities(probabilities, cells, xx - 1, yy, primaryWaveNeighbors, secondaryWaveNeighbors, mixedWaveNeighbors, wv => wv.Right);
         AddProbabilities(probabilities, cells, xx + 1, yy, primaryWaveNeighbors, secondaryWaveNeighbors, mixedWaveNeighbors, wv => wv.Left);
         AddProbabilities(probabilities, cells, xx, yy - 1, primaryWaveNeighbors, secondaryWaveNeighbors, mixedWaveNeighbors, wv => wv.Down);
         AddProbabilities(probabilities, cells, xx, yy + 1, primaryWaveNeighbors, secondaryWaveNeighbors, mixedWaveNeighbors, wv => wv.Up);

         // remove all the empty probability sets (they're not adding any restrictions)
         for (int i = probabilities.Count - 1; i >= 0; i--) {
            if (probabilities[i].Count == 0) probabilities.RemoveAt(i);
         }

         // the block is constricted in options based on its neighbors
         // if the neighbor can only be A/B based on the left and only B/C based on the top, it must be B.
         while (probabilities.Count > 1) {
            var last = probabilities[probabilities.Count - 1];
            var next = probabilities[probabilities.Count - 2];
            probabilities.RemoveAt(probabilities.Count - 1);
            probabilities.RemoveAt(probabilities.Count - 1);

            var merged = new List<CollapseProbability>();
            foreach (var cp1 in last) {
               var cp2 = next.FirstOrDefault(match => match.Block == cp1.Block);
               if (cp2 == null) continue; // no match
               merged.Add(new(cp1.Block) { Count = cp1.Count + cp2.Count });
            }
            if (merged.Count == 0) {
               // couldn't find a match
               // matching one is better than matching neither
               probabilities.Add(rnd.Next(2) == 0 ? last : next);
            } else {
               probabilities.Add(merged);
            }
         }

         if (probabilities.Count == 0 || probabilities[0].Count == 0) {
            // no restriction, pick any block
            var (availableBlocks, _) = PrimaryMap.MapRepointer.EstimateBlockCount(layout.Element, true);
            return availableBlocks.Range().Select(i => new CollapseProbability(i)).ToList();
         }

         // old version that returned just a single block
         //else if (probabilities[0].Count == 1) {
         //   // only one option, go with that
         //   return probabilities[0][0].Block;
         //}
         // pick one block from among the available blocks at random (weighted based on use)
         //var totalOptions = probabilities[0].Sum(cp => cp.Count);
         //var selection = rnd.Next(totalOptions);
         //var index = 0;
         //while (selection > probabilities[0][index].Count) {
         //   selection -= probabilities[0][index].Count;
         //   index += 1;
         //}
         //return probabilities[0][index].Block;

         // new version that returns the current probabilities, which need collapsing
         return probabilities[0];
      }

      private void AddProbabilities(List<List<CollapseProbability>> probabilities, BlockCells cells, int xx, int yy, WaveNeighbors[] primaryWaveNeighbors, WaveNeighbors[] secondaryWaveNeighbors, WaveNeighbors[] mixedWaveNeighbors, Func<WaveNeighbors, List<CollapseProbability>> reverse) {
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

      private void CheckPrimary(BlockCells cells, int x, int y, BlockCell center, WaveNeighbors[] primaryWaveNeighbors, WaveNeighbors[] mixedWaveNeighbors, Func<WaveNeighbors, List<CollapseProbability>> direction) {
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

      private void CheckSecondary(BlockCells cells, int x, int y, BlockCell center, WaveNeighbors[] secondaryWaveNeighbors, WaveNeighbors[] mixedWaveNeighbors, Func<WaveNeighbors, List<CollapseProbability>> direction) {
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

      private bool IsPrimaryBlock(BlockCell cell) => cell.Tile < PrimaryBlocks;
      private int TotalBlocks => 1024;

      #endregion

      private Dictionary<long, int[]> preferredCollisionsPrimary;
      private Dictionary<long, int[]> preferredCollisionsSecondary;
      private void CountCollisionForBlocks() {
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

      private long UpdateBlockset(int mapID) {
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

      private void HandleAutoscrollTiles(object sender, EventArgs e) => AutoscrollTiles.Raise(this);

      private void HandleHideSidePanels(object sender, EventArgs e) {
         SelectedEvent = null;
         ShowHeaderPanel = false;
      }

      private void HandleMapChangeRequest(object sender, ChangeMapEventArgs e) {
         NavigateTo(e.Bank, e.Map);
      }

      private void HandleClearMapCaches(object sender, EventArgs e) {
         foreach (var map in VisibleMaps) map.ClearCaches();
      }

      private int GetPreferredCollision(int tile) {
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

   public enum EventCreationType { None, Object, Warp, Script, Signpost, Fly }

   [Flags]
   public enum PrimaryInteractionStart {
      None = 0,
      Click = 1,
      DoubleClick = 2,
      ControlClick = 4,
      ShiftClick = 8,
   }
   public enum PrimaryInteractionType { None, Draw, Event, RectangleDraw, Draw9Grid }

   public record BlocksetCache(ObservableCollection<string> Primary, ObservableCollection<string> Secondary) {
      public void CalculateBlocksetOptions(IDataModel model) {
         Primary.Clear();
         Secondary.Clear();

         var primary = new HashSet<int>();
         var secondary = new HashSet<int>();

         foreach (var bank in AllMapsModel.Create(model, null)) {
            foreach (var map in bank) {
               if (map == null) continue;
               var layout = map.Layout;
               if (layout == null) continue;
               if (layout.PrimaryBlockset != null) primary.Add(layout.PrimaryBlockset.Start);
               if (layout.SecondaryBlockset != null) secondary.Add(layout.SecondaryBlockset.Start);
            }
         }

         Primary.AddRange(primary.OrderBy(s => s).Select(s => s.ToAddress()));
         Secondary.AddRange(secondary.OrderBy(s => s).Select(s => s.ToAddress()));
      }
   }

   public record TileSelection(int[]Tiles, int Width);
}
