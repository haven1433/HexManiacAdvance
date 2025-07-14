using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.QuickEditItems;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;

namespace HavenSoft.HexManiac.Core.ViewModels {
   public class DexReorderTab : ViewModelCore, ITabContent {
      public readonly IFileSystem fs;
      public readonly ChangeHistory<ModelDelta> history;
      public readonly IDataModel model;
      public readonly string dexOrder, dexInfo;
      public readonly bool isNational;

      public ObservableCollection<SortablePokemon> Elements { get; } = new ObservableCollection<SortablePokemon>();

      public string Name => "Adjust Dex Order";

      public string FullFileName { get; }

      public bool SpartanMode { get; set; }

      public IDataModel Model => model;

      public string filter = string.Empty;

      public bool IsMetadataOnlyChange => false;
      public bool CanDuplicate => false;
      public void Duplicate() { }

      public event EventHandler<string> OnError;
      public event EventHandler Closed;
      event EventHandler<string> ITabContent.OnMessage { add { } remove { } }
      event EventHandler ITabContent.ClearMessage { add { } remove { } }
      event EventHandler<TabChangeRequestedEventArgs> ITabContent.RequestTabChange { add { } remove { } }
      event EventHandler<Action> ITabContent.RequestDelayedWork { add { } remove { } }
      event EventHandler ITabContent.RequestMenuClose { add { } remove { } }
      event EventHandler<Direction> ITabContent.RequestDiff { add { } remove { } }
      event EventHandler<CanDiffEventArgs> ITabContent.RequestCanDiff { add { } remove { } }
      event EventHandler<CanPatchEventArgs> ITabContent.RequestCanCreatePatch { add { } remove { } }
      event EventHandler<CanPatchEventArgs> ITabContent.RequestCreatePatch { add { } remove { } }
      event EventHandler ITabContent.RequestRefreshGotoShortcuts { add { } remove { } }

      public bool CanIpsPatchRight => false;
      public bool CanUpsPatchRight => false;
      public void IpsPatchRight() { }
      public void UpsPatchRight() { }

      public double spriteScale = 1;

      public int selectionStart;
      
      public int selectionEnd;
      
      public bool TryImport(LoadedFile file, IFileSystem fileSystem) => false;
   }

   public class SortablePokemon : ViewModelCore, IPixelViewModel {
      public readonly IList<string> filterTerms;
      public readonly List<int> extraIndices;

      public int CanonicalIndex { get; }
      public IReadOnlyList<int> ExtraIndices => extraIndices;
      public short Transparent => -1;
      public int PixelWidth { get; }
      public int PixelHeight { get; }
      public short[] PixelData { get; }
      public double spriteScale;
      public double SpriteScale { get => spriteScale; set => Set(ref spriteScale, value); }

      public string Name { get; }

      public bool isFilteredOut;
      public bool IsFilteredOut { get => isFilteredOut; set => TryUpdate(ref isFilteredOut, value); }

      public bool selected;
      public bool Selected { get => selected; set => TryUpdate(ref selected, value); }

      public void AddSource(int canonicalIndex) {
         extraIndices.Add(canonicalIndex);
      }

      public void MatchToFilter(string filter) {
         IsFilteredOut = false;
         if (filter == string.Empty) return;

         foreach (var term in filterTerms) {
            if (term.MatchesPartial(filter)) return;
         }

         IsFilteredOut = true;
      }
   }
}
