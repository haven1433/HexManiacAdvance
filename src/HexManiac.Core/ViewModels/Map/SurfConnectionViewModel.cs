using HavenSoft.HexManiac.Core.Models.Map;
using System;
using System.Linq;

namespace HavenSoft.HexManiac.Core.ViewModels.Map {
   public class SurfConnectionViewModel : ViewModelCore {
      public readonly IEditableViewPort viewPort;
      public readonly int group, map;

      public bool canWarp;
      public bool CanWarp {
         get => canWarp;
         set => Set(ref canWarp, value, old => NotifyPropertiesChanged(nameof(CanCreate), nameof(CanRemoveConnection)));
      }

      public bool canEmerge;
      public bool CanEmerge { get => canEmerge; set => Set(ref canEmerge, value); }

      public bool CanCreate => !CanWarp;

      public event EventHandler<ChangeMapEventArgs> RequestChangeMap;
      public event EventHandler<ConnectionInfo> ConnectNewMap;
      public event EventHandler<ConnectionInfo> ConnectExistingMap;
      public event EventHandler RequestRemoveConnection;

      public bool CanRemoveConnection => CanWarp;
      public void RemoveConnection() {
         RequestRemoveConnection.Raise(this);
         CanWarp = false;
      }
   }
}
