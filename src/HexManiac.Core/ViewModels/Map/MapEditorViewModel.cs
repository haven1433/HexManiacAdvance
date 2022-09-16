using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels.Map {
   /// <summary>
   /// Represents the entire map editor tab, with all visible controls, maps, edit boxes, etc
   /// </summary>
   public class MapEditorViewModel : ViewModelCore, ITabContent {
      private readonly IFileSystem fileSystem;
      private readonly IEditableViewPort viewPort;
      private readonly IDataModel model;
      private readonly ChangeHistory<ModelDelta> history;
      private readonly Singletons singletons;

      public IViewPort ViewPort => viewPort;
      public IFileSystem FileSystem => fileSystem;

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

      private IEventModel selectedEvent;
      public IEventModel SelectedEvent {
         get => selectedEvent;
         set {
            selectedEvent = value;
            NotifyPropertyChanged();
            NotifyPropertyChanged(nameof(ShowEventPanel));
            ShowHeaderPanel = false;
         }
      }

      public bool ShowEventPanel => selectedEvent != null;

      public ObservableCollection<BlockMapViewModel> VisibleMaps { get; } = new();

      public ObservableCollection<MapSlider> MapButtons { get; } = new();

      #region Block Picker

      public IPixelViewModel Blocks => primaryMap?.BlockPixels;

      private int drawBlockIndex = -10;
      public int DrawBlockIndex {
         get => drawBlockIndex;
         set {
            Set(ref drawBlockIndex, value, old => {
               NotifyPropertyChanged(nameof(HighlightBlockX));
               NotifyPropertyChanged(nameof(HighlightBlockY));
               NotifyPropertyChanged(nameof(HighlightBlockSize));
            });
         }
      }

      private int collisionIndex = -1;
      public int CollisionIndex {
         get => collisionIndex;
         set {
            Set(ref collisionIndex, value, old => {
               primaryMap.CollisionHighlight = value;
            });
         }
      }

      public ObservableCollection<string> CollisionOptions { get; } = new();

      public double HighlightBlockX => (drawBlockIndex % BlockMapViewModel.BlocksPerRow) * Blocks.SpriteScale * 16;
      public double HighlightBlockY => (drawBlockIndex / BlockMapViewModel.BlocksPerRow) * Blocks.SpriteScale * 16;
      public double HighlightBlockSize => 16 * Blocks.SpriteScale + 2;

      #endregion

      #region ITabContent

      private StubCommand close, undo, redo, backCommand, forwardCommand;

      public string Name => (primaryMap?.Name ?? "Map") + (history.HasDataChange ? "*" : string.Empty);
      public string FullFileName => viewPort.FullFileName;
      public bool IsMetadataOnlyChange => false;
      public ICommand Save => viewPort.Save;
      public ICommand SaveAs => viewPort.SaveAs;
      public ICommand ExportBackup => null;
      public ICommand Undo => StubCommand(ref undo,
         () => {
            history.Undo.Execute();
            Refresh();
         },
         () => history.Undo.CanExecute(default));
      public ICommand Redo => StubCommand(ref redo, () => history.Redo.Execute(), () => history.Redo.CanExecute(default));
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
      public bool CanDuplicate => false;

      public event EventHandler<string> OnError;
      public event EventHandler<string> OnMessage;
      public event EventHandler ClearMessage;
      public event EventHandler Closed;
      public event EventHandler<ITabContent> RequestTabChange;
      public event EventHandler<Action> RequestDelayedWork;
      public event EventHandler RequestMenuClose;
      public event EventHandler<Direction> RequestDiff;
      public event EventHandler<CanDiffEventArgs> RequestCanDiff;
      public event EventHandler<CanPatchEventArgs> RequestCanCreatePatch;
      public event EventHandler<CanPatchEventArgs> RequestCreatePatch;

      public void Duplicate() { }
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
         if (primaryMap != null) forwardStack.Add(primaryMap.MapID);
         if (forwardStack.Count == 1) forwardCommand.RaiseCanExecuteChanged();
         if (backStack.Count == 0) backCommand.RaiseCanExecuteChanged();
         NavigateTo(previous, trackChange: false);
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

         UpdatePrimaryMap(new BlockMapViewModel(fileSystem, viewPort, bank, map) {
            IncludeBorders = primaryMap?.IncludeBorders ?? true,
            SpriteScale = primaryMap?.SpriteScale ?? .5,
         });
      }

      #endregion

      public static bool TryCreateMapEditor(IFileSystem fileSystem, IEditableViewPort viewPort, Singletons singletons, out MapEditorViewModel editor) {
         editor = null;
         var maps = viewPort.Model.GetTable(HardcodeTablesModel.MapBankTable);
         if (maps == null) return false;
         editor = new MapEditorViewModel(fileSystem, viewPort, singletons);
         return true;
      }

      public MapEditorViewModel(IFileSystem fileSystem, IEditableViewPort viewPort, Singletons singletons) {
         (this.viewPort, this.model, this.history, this.singletons, this.fileSystem) = (viewPort, viewPort.Model, viewPort.ChangeHistory, singletons, fileSystem);
         history.Undo.CanExecuteChanged += (sender, e) => undo.RaiseCanExecuteChanged();
         history.Redo.CanExecuteChanged += (sender, e) => redo.RaiseCanExecuteChanged();
         history.Bind(nameof(history.HasDataChange), (sender, e) => NotifyPropertyChanged(nameof(Name)));

         var map = new BlockMapViewModel(fileSystem, viewPort, 3, 19) { IncludeBorders = true };
         UpdatePrimaryMap(map);
         for (int i = 0; i < 0x40; i++) CollisionOptions.Add(i.ToString("X2"));
      }

      private void UpdatePrimaryMap(BlockMapViewModel map) {
         // update the primary map
         if (primaryMap != map) {
            if (primaryMap != null) {
               primaryMap.NeighborsChanged -= PrimaryMapNeighborsChanged;
               primaryMap.PropertyChanged -= PrimaryMapPropertyChanged;
               primaryMap.CollisionHighlight = -1;
            }
            primaryMap = map;
            NotifyPropertyChanged(nameof(PrimaryMap));
            if (primaryMap != null) {
               primaryMap.NeighborsChanged += PrimaryMapNeighborsChanged;
               primaryMap.PropertyChanged += PrimaryMapPropertyChanged;
               primaryMap.CollisionHighlight = collisionIndex;
            }
            NotifyPropertyChanged(nameof(Blocks));
            NotifyPropertyChanged(nameof(Name));
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

      private double highlightCursorX, highlightCursorY, highlightCursorSize;
      public double HighlightCursorX { get => highlightCursorX; set => Set(ref highlightCursorX, value); }
      public double HighlightCursorY { get => highlightCursorY; set => Set(ref highlightCursorY, value); }
      public double HighlightCursorSize { get => highlightCursorSize; set => Set(ref highlightCursorSize, value); }

      public void Hover(double x, double y) {
         var map = MapUnderCursor(x, y);
         if (map == null) return;
         var dx = (int)((x - map.LeftEdge) / 16 / map.SpriteScale) + .5;
         var dy = (int)((y - map.TopEdge) / 16 / map.SpriteScale) + .5;
         (HighlightCursorX, HighlightCursorY) = (map.LeftEdge + dx * 16 * map.SpriteScale, map.TopEdge + dy * 16 * map.SpriteScale);
         HighlightCursorSize = 16 * map.SpriteScale + 4;
      }

      public void DragDown(double x, double y) {
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
         Hover(x, y);
      }

      public void DragUp(double x, double y) { }

      #region Primary Interaction (left-click)

      private PrimaryInteractionType interactionType;
      public void PrimaryDown(double x, double y, int clickCount) {
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

      private void DrawDown(double x, double y, int clickCount) {
         interactionType = PrimaryInteractionType.Draw;
         if (clickCount == 2) {
            var map = MapUnderCursor(x, y);
            if (map != null) map.PaintBlock(history.CurrentChange, drawBlockIndex, collisionIndex, x, y);
            Hover(x, y);
         } else {
            DrawMove(x, y);
         }
      }

      private void DrawMove(double x, double y) {
         var map = MapUnderCursor(x, y);
         if (map != null) map.DrawBlock(history.CurrentChange, drawBlockIndex, collisionIndex, x, y);
         Hover(x, y);
      }

      private void DrawUp(double x, double y) {
         history.ChangeCompleted();
         interactionType = PrimaryInteractionType.None;
      }

      private void EventDown(double x, double y, IEventModel ev, int clickCount) {
         SelectedEvent = ev;
         if (SelectedEvent is WarpEventModel warp && clickCount == 2) {
            NavigateTo(warp.Bank, warp.Map);
         } else {
            interactionType = PrimaryInteractionType.Event;
            DrawBlockIndex = -1;
            CollisionIndex = -1;
         }
      }

      private void EventMove(double x, double y) {
         var map = MapUnderCursor(x, y);
         if (map != null) map.UpdateEventLocation(selectedEvent, x, y);
         Hover(x, y);
      }

      private void EventUp(double x, double y) {
         history.ChangeCompleted();
         if (!withinEventCreationInteraction) return;
         withinEventCreationInteraction = false;
         UpdatePrimaryMap(primaryMap); // re-add neighbors
      }

      #endregion

      private bool withinEventCreationInteraction = false;
      public void StartEventCreationInteraction(EventCreationType type) {
         if (type == EventCreationType.Object) {
            SelectedEvent = primaryMap.CreateObjectEvent(0, Pointer.NULL);
         } else if (type == EventCreationType.Warp) {
            var desiredMap = (bank: 0, map: 0);
            if (backStack.Count > 0) {
               var last = backStack[backStack.Count - 1];
               desiredMap = (bank: last / 1000, map: last % 1000);
            }
            SelectedEvent = primaryMap.CreateWarpEvent(desiredMap.bank, desiredMap.map);
         } else if (type == EventCreationType.Script) {
            SelectedEvent = primaryMap.CreateScriptEvent();
         } else if (type == EventCreationType.Signpost) {
            SelectedEvent = primaryMap.CreateSignpostEvent();
         } else {
            throw new NotImplementedException();
         }
         interactionType = PrimaryInteractionType.Event;
         VisibleMaps.Clear();
         VisibleMaps.Add(primaryMap);
         MapButtons.Clear();
         withinEventCreationInteraction = true;
      }

      public void SelectDown(double x, double y) {
         var map = MapUnderCursor(x, y);
         if (map == null) return;
         ShowHeaderPanel = false;
         var (blockIndex, collisionIndex) = map.GetBlock(x, y);
         if (blockIndex >= 0) {
            DrawBlockIndex = blockIndex;
            AutoscrollBlocks.Raise(this);
         }
         if (collisionIndex >= 0) CollisionIndex = collisionIndex;
      }

      public void SelectMove(double x, double y) { }

      public void SelectUp(double x, double y) {
         interactionType = PrimaryInteractionType.None;
      }

      public void Zoom(double x, double y, bool enlarge) {
         var map = MapUnderCursor(x, y);
         if (map == null) map = primaryMap;
         map.Scale(x, y, enlarge);
         map.IncludeBorders = map.SpriteScale <= 1;
         UpdatePrimaryMap(map);
      }

      public void SelectBlock(int x, int y) {
         DrawBlockIndex = y * BlockMapViewModel.BlocksPerRow + x;
      }

      private StubCommand panCommand, zoomCommand, deleteCommand;
      public ICommand PanCommand => StubCommand<MapDirection>(ref panCommand, Pan);
      public ICommand ZoomCommand => StubCommand<ZoomDirection>(ref zoomCommand, Zoom);
      public ICommand DeleteCommand => StubCommand(ref deleteCommand, Delete);

      public void Pan(MapDirection direction) {
         int intX = 0, intY = 0;
         if (direction == MapDirection.Left) intX = -1;
         if (direction == MapDirection.Right) intX = 1;
         if (direction == MapDirection.Up) intY = -1;
         if (direction == MapDirection.Down) intY = 1;
         Pan(intX * -32, intY * -32);
         var map = MapUnderCursor(0, 0);
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
         foreach (var map in VisibleMaps) {
            if (map.LeftEdge < x && x < map.LeftEdge + map.PixelWidth * map.SpriteScale) {
               if (map.TopEdge < y && y < map.TopEdge + map.PixelHeight * map.SpriteScale) {
                  return map;
               }
            }
         }
         return null;
      }

      #endregion

      #region ShiftInteraction

      private MapSlider shiftButton;

      public void ShiftDown(double x, double y) {
         (cursorX, cursorY) = (x, y);
         (deltaX, deltaY) = (0, 0);
         shiftButton = ButtonUnderCursor(x, y);
         HighlightCursorSize = 0;
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
   }

   public enum EventCreationType { Object, Warp, Script, Signpost }

   public enum PrimaryInteractionType { None, Draw, Event }
}
