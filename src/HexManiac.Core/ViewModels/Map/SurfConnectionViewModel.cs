using HavenSoft.HexManiac.Core.Models.Map;
using System;
using System.Linq;

namespace HavenSoft.HexManiac.Core.ViewModels.Map {
   public class SurfConnectionViewModel : ViewModelCore {
      private readonly IEditableViewPort viewPort;
      private readonly int group, map;

      private bool canWarp;
      public bool CanWarp {
         get => canWarp;
         set => Set(ref canWarp, value, old => NotifyPropertiesChanged(nameof(CanCreate), nameof(CanRemoveConnection)));
      }

      private bool canEmerge;
      public bool CanEmerge { get => canEmerge; set => Set(ref canEmerge, value); }

      public bool CanCreate => !CanWarp;

      public event EventHandler<ChangeMapEventArgs> RequestChangeMap;
      public event EventHandler<ConnectionInfo> ConnectNewMap;
      public event EventHandler<ConnectionInfo> ConnectExistingMap;
      public event EventHandler RequestRemoveConnection;

      public SurfConnectionViewModel(IEditableViewPort viewPort, int group, int map) {
         this.viewPort = viewPort;
         (this.group, this.map) = (group, map);
         var allmaps = AllMapsModel.Create(viewPort.Model, () => viewPort.ChangeHistory.CurrentChange);
         var connections = allmaps[group][map].Connections;
         var connection = connections?.FirstOrDefault(c => c.Direction.IsAny(MapDirection.Dive, MapDirection.Emerge));
         if (connection == null) {
            canWarp = false;
         } else {
            canWarp = true;
            canEmerge = connection.Direction == MapDirection.Emerge;
         }
      }

      public void FollowConnection() {
         var allmaps = AllMapsModel.Create(viewPort.Model, () => viewPort.ChangeHistory.CurrentChange);
         var connections = allmaps[group][map].Connections; // (null for a map that has no connections at all)
         var connection = connections?.FirstOrDefault(c => c.Direction.IsAny(MapDirection.Dive, MapDirection.Emerge));
         if (connection == null) return;
         RequestChangeMap?.Invoke(this, new(connection.MapGroup, connection.MapNum));
      }

      #region Create New Connection

      public void DiveExisting() {
         var info = Make(MapDirection.Dive);
         ConnectExistingMap.Raise(this, info);
         RefreshFromModel();
      }

      public void EmergeExisting() {
         var info = Make(MapDirection.Emerge);
         ConnectExistingMap.Raise(this, info);
         RefreshFromModel();
      }

      public void DiveNew() {
         var info = Make(MapDirection.Dive);
         ConnectNewMap.Raise(this, info);
         RefreshFromModel();
      }

      public void EmergeNew() {
         var info = Make(MapDirection.Emerge);
         ConnectNewMap.Raise(this, info);
         RefreshFromModel();
      }

      /// <summary>
      /// The connection may not have been made (the person can cancel the question), or may have been made or removed since (undo, redo), so ask the map.
      /// </summary>
      public void RefreshFromModel() {
         var allmaps = AllMapsModel.Create(viewPort.Model, () => viewPort.ChangeHistory.CurrentChange);
         var connection = allmaps[group]?[map]?.Connections?.FirstOrDefault(c => c.Direction.IsAny(MapDirection.Dive, MapDirection.Emerge));
         CanWarp = connection != null;
         CanEmerge = connection != null && connection.Direction == MapDirection.Emerge;
      }

      private ConnectionInfo Make(MapDirection direction) {
         var allmaps = AllMapsModel.Create(viewPort.Model, () => viewPort.ChangeHistory.CurrentChange);
         var layout = allmaps[group][map].Layout;
         var (width, height) = (layout.Width, layout.Height);
         return new ConnectionInfo(width, height, direction);
      }

      #endregion

      public bool CanRemoveConnection => CanWarp;
      public void RemoveConnection() {
         RequestRemoveConnection.Raise(this);
         CanWarp = false;
      }
   }
}
