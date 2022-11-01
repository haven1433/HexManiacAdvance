using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Map;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
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

      public bool ShowEventPanel => selectedEvent != null;

      public ObservableCollection<BlockMapViewModel> VisibleMaps { get; } = new();

      public ObservableCollection<MapSlider> MapButtons { get; } = new();

      public ObservableCollection<MenuCommand> ContextItems { get; } = new();

      public EventTemplate Templates => templates;

      private int PrimaryTiles { get; }

      private string hoverPoint;
      public string HoverPoint { get => hoverPoint; set => Set(ref hoverPoint, value); }

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
            Set(ref collisionIndex, value, old => {
               primaryMap.CollisionHighlight = value;
               DrawMultipleTiles = false;
            });
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
      public ICommand ExportBackup => null;
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

      private GameReferenceTables RefTable => singletons.GameReferenceTables.TryGetValue(model.GetGameCode(), out var refTable) ? refTable : null;
      public ICommand Close => StubCommand(ref close, () => {
         if (!history.IsSaved) {
            var metadata = model.ExportMetadata(RefTable, singletons.MetadataInfo);
            var result = fileSystem.TrySavePrompt(new LoadedFile(viewPort.FileName, model.RawData));
            if (result == null) return;
            if (result == true) {
               fileSystem.SaveMetadata(viewPort.FileName, metadata?.Serialize());
               history.TagAsSaved();
            }
         }
         Closed.Raise(this);
      });

      public ICommand Diff => null;
      public ICommand DiffLeft => null;
      public ICommand DiffRight => null;
      public bool CanDuplicate => true;

      public event EventHandler<string> OnError;
      public event EventHandler<string> OnMessage;
      public event EventHandler ClearMessage;
      public event EventHandler Closed;
      public event EventHandler<TabChangeRequestedEventArgs> RequestTabChange;
      public event EventHandler<Action> RequestDelayedWork;
      public event EventHandler RequestMenuClose;
      public event EventHandler<Direction> RequestDiff;
      public event EventHandler<CanDiffEventArgs> RequestCanDiff;
      public event EventHandler<CanPatchEventArgs> RequestCanCreatePatch;
      public event EventHandler<CanPatchEventArgs> RequestCreatePatch;

      public void Duplicate() => Duplicate(PrimaryMap.MapID / 1000, PrimaryMap.MapID % 1000);
      private void Duplicate(int bank, int map) {
         var newEditor = new MapEditorViewModel(FileSystem, viewPort, singletons, Tutorials);
         newEditor.UpdatePrimaryMap(new BlockMapViewModel(FileSystem, Tutorials, viewPort, Format, bank, map));
         RequestTabChange?.Invoke(this, new(newEditor));
      }

      public void Refresh() {
         VisibleMaps.Clear();
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
            RequestTabChange(this, new(viewPort));
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
         if (primaryMap != null) backStack.Add(primaryMap.MapID);
         if (backStack.Count == 1) backCommand.RaiseCanExecuteChanged();
         if (forwardStack.Count == 0) forwardCommand.RaiseCanExecuteChanged();
         NavigateTo(next, trackChange: false);
      }
      private void NavigateTo(int mapID, bool trackChange = true) => NavigateTo(mapID / 1000, mapID % 1000, trackChange);
      private void NavigateTo(int bank, int map, bool trackChange = true) {
         if (trackChange && primaryMap != null) {
            backStack.Add(primaryMap.MapID);
            if (backStack.Count == 1) backCommand.RaiseCanExecuteChanged();
            if (forwardStack.Count > 0) {
               forwardStack.Clear();
               forwardCommand.RaiseCanExecuteChanged();
            }
         }

         UpdatePrimaryMap(new BlockMapViewModel(fileSystem, Tutorials, viewPort, format, bank, map) {
            IncludeBorders = primaryMap?.IncludeBorders ?? true,
            SpriteScale = primaryMap?.SpriteScale ?? .5,
         });
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
         this.format = new Format(!isFRLG);

         var map = new BlockMapViewModel(fileSystem, Tutorials, viewPort, format, 3, 19);
         templates = new(model, viewPort.Tools.CodeTool.ScriptParser, map.AllOverworldSprites);
         UpdatePrimaryMap(map);
         for (int i = 0; i < 0x40; i++) CollisionOptions.Add(i.ToString("X2"));
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

      private void UpdatePrimaryMap(BlockMapViewModel map) {
         if (!map.IsValidMap) return;
         blockset = UpdateBlockset(map.MapID);
         if (blockset == Pointer.NULL) return;
         // update the primary map
         if (primaryMap != map) {
            if (primaryMap != null) {
               primaryMap.NeighborsChanged -= PrimaryMapNeighborsChanged;
               primaryMap.PropertyChanged -= PrimaryMapPropertyChanged;
               primaryMap.CollisionHighlight = -1;
               primaryMap.AutoscrollTiles -= HandleAutoscrollTiles;
               primaryMap.HideSidePanels -= HandleHideSidePanels;
               primaryMap.RequestChangeMap -= HandleMapChangeRequest;
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
            }
            selectedEvent = null;
            NotifyPropertiesChanged(nameof(Blocks), nameof(Name), nameof(ShowEventPanel));
         }

         // update the neighbor maps
         var oldMaps = VisibleMaps.ToList();
         var newMaps = GetMapNeighbors(map, map.SpriteScale < .5 ? 2 : 1).ToList();
         newMaps.Add(map);
         var mapDict = new Dictionary<int, BlockMapViewModel>();
         newMaps.ForEach(m => mapDict[m.MapID] = m);
         newMaps = mapDict.Values.ToList();
         foreach (var oldM in oldMaps) if (!newMaps.Any(newM => oldM.MapID == newM.MapID)) VisibleMaps.Remove(oldM);
         foreach (var newM in newMaps) {
            bool match = false;
            foreach (var existingM in VisibleMaps) {
               if (existingM.MapID == newM.MapID) {
                  match = true;
                  existingM.IncludeBorders = newM.IncludeBorders;
                  existingM.SpriteScale = newM.SpriteScale;
                  existingM.LeftEdge = newM.LeftEdge;
                  existingM.TopEdge = newM.TopEdge;
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
         }
      }

      private IEnumerable<BlockMapViewModel> GetMapNeighbors(BlockMapViewModel map, int recursionLevel) {
         if (recursionLevel < 1) yield break;
         var directions = new List<MapDirection> {
            MapDirection.Up, MapDirection.Down, MapDirection.Left, MapDirection.Right
         };
         var newMaps = new List<BlockMapViewModel>(directions.SelectMany(map.GetNeighbors));
         foreach (var m in newMaps) {
            yield return m;
            if (recursionLevel > 1) {
               foreach (var mm in GetMapNeighbors(m, recursionLevel - 1)) yield return mm;
            }
         }
      }

      #region Map Interaction

      private double cursorX, cursorY, deltaX, deltaY;
      public event EventHandler AutoscrollBlocks;

      private double highlightCursorX, highlightCursorY, highlightCursorWidth, highlightCursorHeight;
      public double HighlightCursorX { get => highlightCursorX; set => Set(ref highlightCursorX, value); }
      public double HighlightCursorY { get => highlightCursorY; set => Set(ref highlightCursorY, value); }
      public double HighlightCursorWidth { get => highlightCursorWidth; set => Set(ref highlightCursorWidth, value); }
      public double HighlightCursorHeight { get => highlightCursorHeight; set => Set(ref highlightCursorHeight, value); }

      public void Hover(double x, double y) {
         if (drawMultipleTiles) {
            var p = ToTilePosition(x, y);
            if (interactionType == PrimaryInteractionType.Draw) {
               while (Math.Abs(p.X - drawSource.X) % tilesToDraw.GetLength(0) != 0) p -= new Point(1, 0);
               while (Math.Abs(p.Y - drawSource.Y) % tilesToDraw.GetLength(1) != 0) p -= new Point(0, 1);
            }
            UpdateHover(p.X, p.Y, tilesToDraw.GetLength(0), tilesToDraw.GetLength(1));
         } else {
            var map = MapUnderCursor(x, y);
            if (map == null) return;
            var p = ToTilePosition(x, y);
            UpdateHover(p.X, p.Y, 1, 1);
            HoverPoint = $"({p.X}, {p.Y})";
         }
      }

      private void UpdateHover(int left, int top, int width, int height) {
         var border = primaryMap.GetBorderThickness();
         HighlightCursorX = (left + border.West + width / 2.0) * 16 * primaryMap.SpriteScale + primaryMap.LeftEdge;
         HighlightCursorY = (top + border.North + height / 2.0) * 16 * primaryMap.SpriteScale + primaryMap.TopEdge;
         HighlightCursorWidth = width * 16 * primaryMap.SpriteScale + 4;
         HighlightCursorHeight = height * 16 * primaryMap.SpriteScale + 4;
      }

      public void DragDown(double x, double y) {
         PrimaryMap.BlockEditor.ShowTiles = false;
         PrimaryMap.BorderEditor.ShowBorderPanel = false;
         (cursorX, cursorY) = (x, y);
         (deltaX, deltaY) = (0, 0);

         var map = MapUnderCursor(x, y);
         if (map != null) UpdatePrimaryMap(map);
      }

      public void DragMove(double x, double y) {
         deltaX += x - cursorX;
         deltaY += y - cursorY;
         var (intX, intY) = ((int)deltaX, (int)deltaY);
         deltaX -= intX;
         deltaY -= intY;
         (cursorX, cursorY) = (x, y);
         Pan(intX, intY);
         if (intX != 0 || intY != 0) Tutorials.Complete(Tutorial.MiddleClick_PanMap);
         Hover(x, y);
      }

      public void DragUp(double x, double y) { }

      #region Primary Interaction (left-click)

      private PrimaryInteractionType interactionType;
      public void PrimaryDown(double x, double y, int clickCount) {
         PrimaryMap.BlockEditor.ShowTiles = false;
         PrimaryMap.BorderEditor.ShowBorderPanel = false;
         var map = MapUnderCursor(x, y);
         if (map == null) {
            interactionType = PrimaryInteractionType.None;
            return;
         }

         var ev = map.EventUnderCursor(x, y);
         if (ev != null) {
            EventDown(x, y, ev, clickCount);
            return;
         } else {
            primaryMap.DeselectEvent();
            SelectedEvent = null;
            Tutorials.Complete(Tutorial.ClickMap_UnselectEvent);
         }

         ShowHeaderPanel = false;
         DrawDown(x, y, clickCount);
      }

      public void PrimaryMove(double x, double y) {
         if (interactionType == PrimaryInteractionType.Draw) DrawMove(x, y);
         if (interactionType == PrimaryInteractionType.Event) EventMove(x, y);
      }

      public void PrimaryUp(double x, double y) {
         if (interactionType == PrimaryInteractionType.Draw) DrawUp(x, y);
         if (interactionType == PrimaryInteractionType.Event) EventUp(x, y);
         interactionType = PrimaryInteractionType.None;
      }

      Point drawSource;
      private void DrawDown(double x, double y, int clickCount) {
         interactionType = PrimaryInteractionType.Draw;
         var map = MapUnderCursor(x, y);
         if (clickCount == 2) {
            if (drawBlockIndex < 0 && collisionIndex < 0) {
               // nothing to paint
               interactionType = PrimaryInteractionType.None;
            } else {
               if (map != null) {
                  map.PaintBlock(history.CurrentChange, drawBlockIndex, collisionIndex, x, y);
                  Tutorials.Complete(Tutorial.DoubleClick_PaintBlock);
               }
            }
            Hover(x, y);
         } else {
            PrimaryMap = map;
            drawSource = ToTilePosition(x, y);
            DrawMove(x, y);
         }
      }

      private void DrawMove(double x, double y) {
         var map = MapUnderCursor(x, y);
         if (map != null) {
            if (drawMultipleTiles) {
               var tilePosition = ToTilePosition(x, y);
               map.DrawBlocks(history.CurrentChange, tilesToDraw, drawSource, tilePosition);
            } else {
               if (drawBlockIndex < 0 && collisionIndex < 0) {
                  interactionType = PrimaryInteractionType.None;
               } else {
                  map.DrawBlock(history.CurrentChange, drawBlockIndex, collisionIndex, x, y);
               }
            }
         }
         Hover(x, y);
      }

      private void DrawUp(double x, double y) {
         history.ChangeCompleted();
         interactionType = PrimaryInteractionType.None;
         primaryMap.RedrawEvents(); // editing blocks in a map can draw over events, we need to redraw events now that we have new blocks
      }

      private void EventDown(double x, double y, IEventViewModel ev, int clickCount) {
         SelectedEvent = ev;
         if (SelectedEvent is WarpEventViewModel warp && clickCount == 2) {
            NavigateTo(warp.Bank, warp.Map);
            Tutorials.Complete(Tutorial.DoubleClick_FollowWarp);
         } else if (clickCount == 2 && SelectedEvent is ObjectEventViewModel obj) {
            viewPort.Goto.Execute(obj.ScriptAddress);
            Tutorials.Complete(Tutorial.DoubleClickEvent_SeeScript);
         } else if (clickCount == 2 && SelectedEvent is ScriptEventViewModel script) {
            viewPort.Goto.Execute(script.ScriptAddress);
            Tutorials.Complete(Tutorial.DoubleClickEvent_SeeScript);
         } else if (
            clickCount == 2 &&
            SelectedEvent is SignpostEventViewModel signpost &&
            signpost.ShowPointer &&
            signpost.Pointer >= 0 &&
            signpost.Pointer < model.Count
         ) {
            viewPort.Goto.Execute(signpost.Pointer);
         } else {
            Tutorials.Complete(Tutorial.LeftClick_SelectEvent);
            interactionType = PrimaryInteractionType.Event;
         }
      }

      private void EventMove(double x, double y) {
         var map = MapUnderCursor(x, y);
         if (map != null) {
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
            templates.ApplyTemplate(objectEvent, history.CurrentChange);
            SelectedEvent = objectEvent;
            if (objectEvent.ScriptAddress != Pointer.NULL) primaryMap.InformCreate(new("Object-Event", objectEvent.ScriptAddress));
            Tutorials.Complete(Tutorial.ToolbarTemplate_CreateObject);
         } else if (type == EventCreationType.Warp) {
            var desiredMap = (bank: 0, map: 0);
            if (backStack.Count > 0) {
               var last = backStack[backStack.Count - 1];
               if (last >= 0) {
                  desiredMap = (bank: last / 1000, map: last % 1000);
               }
            }
            SelectedEvent = primaryMap.CreateWarpEvent(desiredMap.bank, desiredMap.map);
            Tutorials.Complete(Tutorial.ToolbarTemplate_CreateEvent);
         } else if (type == EventCreationType.Script) {
            SelectedEvent = primaryMap.CreateScriptEvent();
            Tutorials.Complete(Tutorial.ToolbarTemplate_CreateEvent);
         } else if (type == EventCreationType.Signpost) {
            var signpost = primaryMap.CreateSignpostEvent();
            templates.ApplyTemplate(signpost, history.CurrentChange);
            SelectedEvent = signpost;
            Tutorials.Complete(Tutorial.ToolbarTemplate_CreateEvent);
            // TODO primaryMap.InformCreate if we created a script
         } else if (type == EventCreationType.Fly) {
            var flySpot = primaryMap.CreateFlyEvent();
            if (flySpot != null) {
               SelectedEvent = flySpot;
            }
         } else {
            throw new NotImplementedException();
         }
      }

      #region Selection (right-click)

      int[,] tilesToDraw;
      Point selectDownPosition;
      private bool drawMultipleTiles;
      public bool DrawMultipleTiles { get => drawMultipleTiles; private set => Set(ref drawMultipleTiles, value); }

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
         selectDownPosition = ToTilePosition(x, y);
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
         var selectMovePosition = ToTilePosition(x, y);
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
         var selectMovePosition = ToTilePosition(x, y);
         var left = Math.Min(selectDownPosition.X, selectMovePosition.X);
         var top = Math.Min(selectDownPosition.Y, selectMovePosition.Y);
         var width = Math.Abs(selectDownPosition.X - selectMovePosition.X) + 1;
         var height = Math.Abs(selectDownPosition.Y - selectMovePosition.Y) + 1;
         if (width == 1 && height == 1) {
            DrawMultipleTiles = false;
            BlockEditorVisible = true;
            AnimateBlockSelection();
            UpdateHover(left, top, width, height);
            Tutorials.Complete(Tutorial.RightClickMap_SelectBlock);
            return;
         }

         tilesToDraw = new int[width, height];
         var scale = (width < 4 && height < 4) ? 2 : 1;
         var canvas = new CanvasPixelViewModel(width * 16, height * 16) { SpriteScale = scale };
         for (int xx = 0; xx < width; xx++) {
            for (int yy = 0; yy < height; yy++) {
               var (tX, tY) = ToMapPosition(left + xx, top + yy);
               var block = primaryMap.GetBlock(tX, tY);
               tilesToDraw[xx, yy] = (block.collisionIndex << 10) | block.blockIndex;
               canvas.Draw(primaryMap.BlockRenders[block.blockIndex], xx * 16, yy * 16);
            }
         }
         Tutorials.Complete(Tutorial.RightDragMap_SelectBlocks);
         MultiTileDrawRender = canvas;
         DrawMultipleTiles = true;
         BlockEditorVisible = false;
         PrimaryMap.BlockEditor.ShowTiles = false;
         UpdateHover(left, top, width, height);
      }

      private Point ToTilePosition(double x, double y) {
         (x, y) = ((x - primaryMap.LeftEdge) / primaryMap.SpriteScale / 16, (y - primaryMap.TopEdge) / primaryMap.SpriteScale / 16);
         
         var borders = primaryMap.GetBorderThickness();
         return new Point((int)Math.Floor(x) - borders.West, (int)Math.Floor(y) - borders.North);
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

      private void ShowEventHover(BaseEventModel ev) {
         // TODO
      }

      public void CreateMapForWarp() {
         if (eventContext is not WarpEventViewModel warpContext) return;
         eventContext = null;
         Tutorials.Complete(Tutorial.RightClick_WarpNewMap);
         BlockMapViewModel newMap = primaryMap.CreateMapForWarp(warpContext);
         if (newMap == null) return;
         NavigateTo(newMap.MapID);
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
         map.IncludeBorders = map.SpriteScale <= 1;
         UpdatePrimaryMap(map);
         Tutorials.Complete(Tutorial.Wheel_ZoomMap);
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
         selectedEvent.Delete();
         SelectedEvent = null;
         primaryMap.DeselectEvent();
         primaryMap.RedrawEvents();
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

      private bool selectionFromBlock = false;
      private Point blockInteractionStart;

      public void SelectBlock(int x, int y) {
         while (y * BlockMapViewModel.BlocksPerRow + x > PrimaryMap.BlockRenders.Count) y -= 1;
         blockInteractionStart = new(x, y);
         DrawBlockIndex = y * BlockMapViewModel.BlocksPerRow + x;
         if (autoUpdateCollision) {
            var prefferredCollision = GetPreferredCollision(DrawBlockIndex);
            if (prefferredCollision >= 0) CollisionIndex = prefferredCollision;
         }
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
         DrawMultipleTiles = true;
         Tutorials.Complete(Tutorial.DragBlocks_SelectBlocks);
      }

      public void ReleaseBlock(int x, int y) {
         var (xx, right) = (x, blockInteractionStart.X);
         if (xx > right) (xx, right) = (right, xx);
         var (yy, bottom) = (y, blockInteractionStart.Y);
         if (yy > bottom) (yy, bottom) = (bottom, yy);
         if (bottom * BlockMapViewModel.BlocksPerRow + right > PrimaryMap.BlockRenders.Count) return;
         var (width, height) = (right - xx + 1, bottom - yy + 1);
         if (width == 1 && height == 1) return;
         var scale = (width < 4 && height < 4) ? 2 : 1;
         var canvas = new CanvasPixelViewModel(width * 16, height * 16) { SpriteScale = scale };
         for (x = 0; x < width; x++) {
            for (y = 0; y < height; y++) {
               canvas.Draw(primaryMap.BlockRenders[tilesToDraw[x, y] & 0x3FF], x * 16, y * 16);
            }
         }
         PrimaryMap.BlockEditor.ShowTiles = false;
         MultiTileDrawRender = canvas;
         DrawMultipleTiles = true;
         BlockEditorVisible = false;
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
      }

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
               var map = entry.GetSubTable("map")[0];
               if (map == null) continue;
               var layout = map.GetSubTable("layout")[0];
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
         var layout = banks[mapID / 1000].GetSubTable("maps")[mapID % 1000].GetSubTable("map")[0].GetSubTable(Format.Layout)[0];
         var address1 = layout.GetAddress(Format.PrimaryBlockset);
         var address2 = layout.GetAddress(Format.SecondaryBlockset);
         if (address1 == Pointer.NULL || address2 == Pointer.NULL) return Pointer.NULL;
         var combined = (address1 << 7) | address2;
         (blockset1, blockset2) = (address1, address2);
         return combined;
      }

      private void HandleAutoscrollTiles(object sender, EventArgs e) => AutoscrollTiles.Raise(this);

      private void HandleHideSidePanels(object sender, EventArgs e) => SelectedEvent = null;

      private void HandleMapChangeRequest(object sender, ChangeMapEventArgs e) {
         NavigateTo(e.Bank, e.Map);
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

   public enum SelectionInteractionResult { None, ShowMenu }

   public enum EventCreationType { None, Object, Warp, Script, Signpost, Fly }

   public enum PrimaryInteractionType { None, Draw, Event }

   public record TileSelection(int[]Tiles, int Width);
}
