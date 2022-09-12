using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using IronPython.Modules;
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
      private readonly IEditableViewPort viewPort;
      private readonly IDataModel model;
      private readonly ChangeHistory<ModelDelta> history;
      private readonly Singletons singletons;

      private BlockMapViewModel primaryMap;
      public BlockMapViewModel PrimaryMap {
         get => primaryMap;
         set {
            if (primaryMap == value) return;
            primaryMap = value;
            NotifyPropertyChanged();
         }
      }

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

      public double HighlightBlockX => (drawBlockIndex % BlockMapViewModel.BlocksPerRow) * Blocks.SpriteScale * 16;
      public double HighlightBlockY => (drawBlockIndex / BlockMapViewModel.BlocksPerRow) * Blocks.SpriteScale * 16;
      public double HighlightBlockSize => 16 * Blocks.SpriteScale + 2;

      #endregion

      #region ITabContent

      private StubCommand close, undo, redo;

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
      public ICommand Back => null;
      public ICommand Forward => null;
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

      #endregion

      public MapEditorViewModel(IFileSystem fileSystem, IEditableViewPort viewPort, Singletons singletons) {
         (this.viewPort, this.model, this.history, this.singletons) = (viewPort, viewPort.Model, viewPort.ChangeHistory, singletons);
         history.Undo.CanExecuteChanged += (sender, e) => undo.RaiseCanExecuteChanged();
         history.Redo.CanExecuteChanged += (sender, e) => redo.RaiseCanExecuteChanged();
         history.Bind(nameof(history.HasDataChange), (sender, e) => NotifyPropertyChanged(nameof(Name)));
         var map = new BlockMapViewModel(fileSystem, model, () => history.CurrentChange, 3, 19) { IncludeBorders = true, SpriteScale = .5 };
         UpdatePrimaryMap(map);
      }

      private void UpdatePrimaryMap(BlockMapViewModel map) {
         // update the primary map
         if (primaryMap != map) {
            if (primaryMap != null) {
               primaryMap.NeighborsChanged -= PrimaryMapNeighborsChanged;
            }
            PrimaryMap = map;
            if (primaryMap != null) {
               primaryMap.NeighborsChanged += PrimaryMapNeighborsChanged;
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
      public void PrimaryDown(double x, double y) {
         var map = MapUnderCursor(x, y);
         if (map == null) {
            interactionType = PrimaryInteractionType.None;
            return;
         }

         var ev = map.EventUnderCursor(x, y);
         if (ev != null) {
            EventDown(x, y, ev);
            return;
         }

         DrawDown(x, y);
      }

      public void PrimaryMove(double x, double y) {
         if (interactionType == PrimaryInteractionType.Draw) DrawMove(x, y);
         if (interactionType == PrimaryInteractionType.Event) EventMove(x, y);
      }

      public void PrimaryUp(double x, double y) {
         if (interactionType == PrimaryInteractionType.Draw) DrawUp(x, y);
         if (interactionType == PrimaryInteractionType.Event) EventUp(x, y);
      }

      private void DrawDown(double x, double y) {
         interactionType = PrimaryInteractionType.Draw;
         DrawMove(x, y);
      }

      private void DrawMove(double x, double y) {
         var map = MapUnderCursor(x, y);
         if (map != null) map.DrawBlock(history.CurrentChange, drawBlockIndex, x, y);
         Hover(x, y);
      }

      private void DrawUp(double x, double y) => history.ChangeCompleted();

      private ObjectEventModel interactionEvent;
      private void EventDown(double x, double y, ObjectEventModel ev) {
         interactionType = PrimaryInteractionType.Event;
         interactionEvent = ev;
      }

      private void EventMove(double x,double y) {
         var map = MapUnderCursor(x, y);
         if (map != null) map.UpdateEventLocation(interactionEvent, x, y);
         Hover(x, y);
      }

      private void EventUp(double x, double y) => history.ChangeCompleted();

      #endregion

      public void SelectDown(double x, double y) {
         var map = MapUnderCursor(x, y);
         if (map == null) return;
         var index = map.GetBlock(x, y);
         if (index >= 0) DrawBlockIndex = index;
         AutoscrollBlocks.Raise(this);
      }

      public void SelectMove(double x, double y) { }

      public void SelectUp(double x, double y) { }

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

      private StubCommand panCommand, zoomCommand;
      public ICommand PanCommand => StubCommand<MapDirection>(ref panCommand, Pan);
      public ICommand ZoomCommand => StubCommand<ZoomDirection>(ref zoomCommand, Zoom);

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

      public void Zoom(ZoomDirection direction) => Zoom(0, 0, direction == ZoomDirection.Enlarge);

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

   public enum PrimaryInteractionType { None, Draw, Event }
}
