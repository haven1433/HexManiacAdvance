using HavenSoft.HexManiac.Core.Models;
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
      private readonly IDataModel model;
      private readonly ChangeHistory<ModelDelta> history;
      private readonly Singletons singletons;

      private BlockMapViewModel primaryMap;

      public ObservableCollection<BlockMapViewModel> VisibleMaps { get; } = new();

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

      public string Name => "Map";
      public bool IsMetadataOnlyChange => false;
      public ICommand Save => null;
      public ICommand SaveAs => null;
      public ICommand ExportBackup => null;
      public ICommand Undo => StubCommand(ref undo, () => history.Undo.Execute(), () => history.Undo.CanExecute(default));
      public ICommand Redo => StubCommand(ref redo, () => history.Redo.Execute(), () => history.Redo.CanExecute(default));
      public ICommand Copy => null;
      public ICommand DeepCopy => null;
      public ICommand Clear => null;
      public ICommand SelectAll => null;
      public ICommand Goto => null;
      public ICommand ResetAlignment => null;
      public ICommand Back => null;
      public ICommand Forward => null;
      public ICommand Close => StubCommand(ref close, () => Closed?.Invoke(this, EventArgs.Empty));
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
      public event PropertyChangedEventHandler? PropertyChanged;

      public void Duplicate() { }
      public void Refresh() { }
      public bool TryImport(LoadedFile file, IFileSystem fileSystem) => false;

      #endregion

      public MapEditorViewModel(IDataModel model, ChangeHistory<ModelDelta> history, Singletons singletons) {
         (this.model, this.history, this.singletons) = (model, history, singletons);
         var map = new BlockMapViewModel(model, 3, 19) { IncludeBorders = true, SpriteScale = .5 };
         VisibleMaps.Add(map);
         UpdatePrimaryMap(map);
      }

      private void UpdatePrimaryMap(BlockMapViewModel map) {
         if (primaryMap != map) {
            primaryMap = map;
            NotifyPropertyChanged(nameof(Blocks));
         }
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

      public void LeftDown(double x, double y) {
         (cursorX, cursorY) = (x, y);
         (deltaX, deltaY) = (0, 0);

         var map = MapUnderCursor(x, y);
         if (map != null) UpdatePrimaryMap(map);
      }

      public void LeftMove(double x, double y) {
         deltaX += x - cursorX;
         deltaY += y - cursorY;
         (cursorX, cursorY) = (x, y);
         foreach (var map in VisibleMaps) {
            map.LeftEdge += (int)deltaX;
            map.TopEdge += (int)deltaY;
         }
         deltaX -= (int)deltaX;
         deltaY -= (int)deltaY;
      }

      public void LeftUp(double x, double y) {

      }

      public void Zoom(double x, double y, bool enlarge) {
         var map = MapUnderCursor(x, y);
         if (map == null) return;
         map.Scale(x, y, enlarge);
         UpdatePrimaryMap(map);
      }

      public void SelectBlock(int x, int y) {
         DrawBlockIndex = y * BlockMapViewModel.BlocksPerRow + x;
      }

      private BlockMapViewModel MapUnderCursor(double x, double y) {
         foreach(var map in VisibleMaps) {
            if (map.LeftEdge < x && x < map.LeftEdge + map.PixelWidth * map.SpriteScale) {
               if (map.TopEdge < y && y < map.TopEdge + map.PixelHeight * map.SpriteScale) {
                  return map;
               }
            }
         }
         return null;
      }

      #endregion
   }
}
