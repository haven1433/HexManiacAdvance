using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Map;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using HexManiac.Core.Models.Runs.Sprites;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   ///// <summary>
   ///// Sometimes notifying after every change is too noisy.
   ///// Custom <see cref="INotifyCollectionChanged"/> implementation that allows delayed notifications.
   ///// </summary>
   public class ObservableList<T> : List<T>, INotifyCollectionChanged {
      public event NotifyCollectionChangedEventHandler? CollectionChanged;

      public ObservableList() : base() { }
      public ObservableList(IEnumerable<T> items) : base(items) { }

      public void RaiseRefresh() => CollectionChanged?.Invoke(this, new(NotifyCollectionChangedAction.Reset));
   }

   public class TableGroupViewModel : ViewModelCore {
      public const string DefaultName = "Other";

      public bool isOpen;
      public int currentMember; // used with open/close when refreshing the collection

      public string groupName;
      public bool DisplayHeader => GroupName != DefaultName;
      public string GroupName { get => groupName; set => Set(ref groupName, value, old => NotifyPropertyChanged(nameof(DisplayHeader))); }

      public ObservableCollection<IArrayElementViewModel> Members { get; } = new();

      public Action<IStreamArrayElementViewModel> ForwardModelChanged { get; init; }
      public Action<IStreamArrayElementViewModel> ForwardModelDataMoved { get; init; }

      public bool IsOpen => isOpen;

      public void Open() {
         if (isOpen) return;
         currentMember = 0;
         isOpen = true;
      }

      public bool useMultiFieldFeature = false;
      public bool UseMultiFieldFeature { get => useMultiFieldFeature; set => Set(ref useMultiFieldFeature, value); }

      public MultiFieldArrayElementViewModel multiInProgress;
   }
}
