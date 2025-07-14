using HavenSoft.HexManiac.Core.Models.Runs;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class TupleArrayElementViewModel : ViewModelCore, IArrayElementViewModel {
      public bool visible;
      public bool Visible { get => visible; set => Set(ref visible, value); }
      public string theme; public string Theme { get => theme; set => Set(ref theme, value); }
      public bool IsInError => false;
      public string ErrorText => string.Empty;
      public int ZIndex => 0;
      public event EventHandler DataChanged;
      public event EventHandler DataSelected;
      public void RaiseDataChanged() => DataChanged?.Invoke(this, EventArgs.Empty);
      public void RaiseDataSelected() => DataSelected?.Invoke(this, EventArgs.Empty);

      public string Name { get; }
      public ObservableCollection<ITupleElementViewModel> Children { get; }
   }

   public interface ITupleElementViewModel : ICanSilencePropertyNotifications {
      string Name { get; }
      int BitOffset { get; }
      int BitLength { get; }
      int Start { get; }
   }

   public class NumericTupleElementViewModel : ViewModelCore, ITupleElementViewModel {
      public readonly TupleSegment seg;
      public string Name => seg.Name;
      public int BitOffset { get; }
      public int BitLength => seg.BitWidth;
      public int Start { get; set; }
      public Action RaiseDataChanged { get; }
      public Action RaiseDataSelected { get; }

      public void Focus() => RaiseDataSelected?.Invoke();
   }

   public class CheckBoxTupleElementViewModel : ViewModelCore, ITupleElementViewModel {
      public readonly TupleSegment seg;
      public Action RaiseDataChanged { get; }
      public string Name => seg.Name;
      public int BitOffset { get; }
      public int BitLength => 1;
      public int Start { get; set; }
   }

   public class EnumTupleElementViewModel : ViewModelCore, ITupleElementViewModel {
      public readonly TupleSegment seg;
      public string Name => seg.Name;
      public int BitOffset { get; }
      public int BitLength => seg.BitWidth;
      public int Start { get; set; }
      public string EnumName => seg.SourceName;
      public Action RaiseDataChanged { get; }
      public Action RaiseDataSelected { get; }

      public FilteringComboOptions FilteringComboOptions { get; } = new();

      public bool copying;
      public bool TryCopy(ITupleElementViewModel other) {
         if (!(other is EnumTupleElementViewModel that)) return false;
         if (EnumName != that.EnumName) return false;
         if (Name != that.Name) return false;
         if (BitOffset != that.BitOffset) return false;
         if (BitLength != that.BitLength) return false;

         Start = that.Start;
         using (Scope(ref copying, true, old => copying = old)) {
            FilteringComboOptions.Update(that.FilteringComboOptions.AllOptions, that.FilteringComboOptions.SelectedIndex);
         }
         NotifyPropertyChanged(nameof(Start));
         
         return true;
      }

      public void Focus() => RaiseDataSelected?.Invoke();
   }
}
