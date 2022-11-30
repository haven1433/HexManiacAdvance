using HavenSoft.HexManiac.Core.Models.Map;
using System;
using System.Linq;

namespace HavenSoft.HexManiac.Core.ViewModels.Map {
   public class SurfConnectionViewModel : ViewModelCore {
      private readonly IEditableViewPort viewPort;
      private readonly int group, map;

      private bool canWarp;
      public bool CanWarp { get => canWarp; set => Set(ref canWarp, value, old => NotifyPropertyChanged(nameof(CanCreate))); }

      private bool canEmerge;
      public bool CanEmerge { get => canEmerge; set => Set(ref canEmerge, value); }

      private bool CanCreate => !CanWarp;

      public event EventHandler<ChangeMapEventArgs> RequestChangeMap;

      public SurfConnectionViewModel(IEditableViewPort viewPort, int group, int map) {
         this.viewPort = viewPort;
         (this.group, this.map) = (group, map);
         var allmaps = AllMapsModel.Create(viewPort.Model, () => viewPort.ChangeHistory.CurrentChange);
         var connections = allmaps[group][map].Connections;
         var connection = connections.FirstOrDefault(c => c.Direction.IsAny(MapDirection.Dive, MapDirection.Emerge));
         if (connection == null) {
            canWarp = false;
         } else {
            canWarp = true;
            canEmerge = connection.Direction == MapDirection.Emerge;
         }
      }

      public void FollowConnection() {
         var allmaps = AllMapsModel.Create(viewPort.Model, () => viewPort.ChangeHistory.CurrentChange);
         var connections = allmaps[group][map].Connections;
         var connection = connections.First(c => c.Direction.IsAny(MapDirection.Dive, MapDirection.Emerge));
         RequestChangeMap?.Invoke(this, new(connection.MapGroup, connection.MapNum));
      }

      #region Create New Connection

      public void DiveExisting() {

      }

      public void EmergeExisting() {

      }

      public void DiveNew() {

      }

      public void EmergeNew() {

      }

      #endregion
   }
}
