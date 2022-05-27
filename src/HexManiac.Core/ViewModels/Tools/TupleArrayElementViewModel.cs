using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class TupleArrayElementViewModel : ViewModelCore, IArrayElementViewModel {
      private bool visible;
      public bool Visible { get => visible; set => Set(ref visible, value); }
      public bool IsInError => false;
      public string ErrorText => string.Empty;
      public int ZIndex => 0;
      public event EventHandler DataChanged;
      public event EventHandler DataSelected;
      private void RaiseDataChanged() => DataChanged?.Invoke(this, EventArgs.Empty);
      private void RaiseDataSelected() => DataSelected?.Invoke(this, EventArgs.Empty);

      public string Name { get; }
      public ObservableCollection<ITupleElementViewModel> Children { get; }

      public TupleArrayElementViewModel(ViewPort viewPort, ArrayRunTupleSegment tupleItem, int start) {
         Children = new ObservableCollection<ITupleElementViewModel>();
         Name = tupleItem.Name;
         var bitOffset = 0;
         for (int i = 0; i < tupleItem.Elements.Count; i++) {
            if (string.IsNullOrEmpty(tupleItem.Elements[i].Name)) {
               // don't make a viewmodel for unnamed tuple item elements
            } else if (tupleItem.Elements[i].BitWidth == 1) {
               Children.Add(new CheckBoxTupleElementViewModel(viewPort, start, bitOffset, tupleItem.Elements[i]));
            } else if (!string.IsNullOrEmpty(tupleItem.Elements[i].SourceName)) {
               Children.Add(new EnumTupleElementViewModel(viewPort, start, bitOffset, tupleItem.Elements[i], RaiseDataChanged, RaiseDataSelected));
            } else {
               Children.Add(new NumericTupleElementViewModel(viewPort, start, bitOffset, tupleItem.Elements[i], RaiseDataSelected));
            }
            bitOffset += tupleItem.Elements[i].BitWidth;
         }
      }

      public bool TryCopy(IArrayElementViewModel other) {
         if (!(other is TupleArrayElementViewModel that)) return false;
         if (that.Children.Count != Children.Count) return false;
         if (Name != that.Name) return false;
         for (int i = 0; i < Children.Count; i++) {
            if (!Children[i].TryCopy(that.Children[i])) return false;
         }

         Visible = that.Visible;
         return true;
      }
   }

   public interface ITupleElementViewModel : INotifyPropertyChanged {
      string Name { get; }
      int BitOffset { get; }
      int BitLength { get; }
      int Start { get; }
      bool TryCopy(ITupleElementViewModel other);
   }

   public class NumericTupleElementViewModel : ViewModelCore, ITupleElementViewModel {
      private readonly TupleSegment seg;
      private readonly ViewPort viewPort;
      public string Name => seg.Name;
      public int BitOffset { get; }
      public int BitLength => seg.BitWidth;
      public int Start { get; private set; }
      private Action RaiseDataSelected { get; }

      public int Content {
         get => seg.Read(viewPort.Model, Start, BitOffset);
         set {
            seg.Write(viewPort.Model, viewPort.CurrentChange, Start, BitOffset, value);
            NotifyPropertyChanged();
         }
      }

      public NumericTupleElementViewModel(ViewPort viewPort, int start, int bitOffset, TupleSegment segment, Action raiseDataSelected) {
         (this.viewPort, seg, Start, BitOffset) = (viewPort, segment, start, bitOffset);
         RaiseDataSelected = raiseDataSelected;
      }

      public virtual bool TryCopy(ITupleElementViewModel other) {
         if (!(other is NumericTupleElementViewModel that)) return false;
         if (Name != that.Name) return false;
         if (BitOffset != that.BitOffset) return false;
         if (BitLength != that.BitLength) return false;

         Start = that.Start;
         NotifyPropertyChanged(nameof(Start));
         NotifyPropertyChanged(nameof(Content));
         return true;
      }

      public void Focus() => RaiseDataSelected?.Invoke();
   }

   public class CheckBoxTupleElementViewModel : ViewModelCore, ITupleElementViewModel {
      private readonly TupleSegment seg;
      private readonly ViewPort viewPort;
      public string Name => seg.Name;
      public int BitOffset { get; }
      public int BitLength => 1;
      public int Start { get; private set; }

      public bool IsChecked {
         get => seg.Read(viewPort.Model, Start, BitOffset) == 1;
         set {
            var bit = value ? 1 : 0;
            seg.Write(viewPort.Model, viewPort.CurrentChange, Start, BitOffset, bit);
            NotifyPropertyChanged();
         }
      }

      public CheckBoxTupleElementViewModel(ViewPort viewPort, int start, int bitOffset, TupleSegment segment) {
         Debug.Assert(segment.BitWidth == 1, "Checkboxes should not be constructed from segments with BitWidth other than 1!");
         this.viewPort = viewPort;
         (seg, Start, BitOffset) = (segment, start, bitOffset);
      }

      public bool TryCopy(ITupleElementViewModel other) {
         if (!(other is CheckBoxTupleElementViewModel that)) return false;
         if (Name != that.Name) return false;
         if (BitOffset != that.BitOffset) return false;

         Start = that.Start;
         NotifyPropertyChanged(nameof(Start));
         NotifyPropertyChanged(nameof(IsChecked));
         return true;
      }
   }

   public class EnumTupleElementViewModel : ViewModelCore, ITupleElementViewModel {
      private readonly TupleSegment seg;
      private readonly ViewPort viewPort;
      public string Name => seg.Name;
      public int BitOffset { get; }
      public int BitLength => seg.BitWidth;
      public int Start { get; private set; }
      public string EnumName => seg.SourceName;
      private Action RaiseDataChanged { get; }
      private Action RaiseDataSelected { get; }

      public IReadOnlyList<string> Options {
         get {
            var fullOptions = ArrayRunEnumSegment.GetOptions(viewPort.Model, EnumName).ToList();
            if (!isFiltering) return fullOptions;
            return fullOptions.Where(option => option.MatchesPartial(filterText, onlyCheckLettersAndDigits: true)).ToList();
         }
      }

      public void ConfirmSelection() => SelectedIndex = 0;

      public int SelectedIndex {
         get => seg.Read(viewPort.Model, Start, BitOffset);
         set {
            if (recursionCheck != 0) return;
            recursionCheck++;
            using var _ = new StubDisposable { Dispose = () => recursionCheck-- };
            var filteredOptions = Options;
            var fullOptions = ArrayRunEnumSegment.GetOptions(viewPort.Model, EnumName).ToList();
            var currentOption = (0 <= value && value < filteredOptions.Count) ? filteredOptions[value] : fullOptions[0];
            IsFiltering = false;
            if (0 <= value && value < fullOptions.Count) value = fullOptions.IndexOf(currentOption);
            if (0 <= value && value < fullOptions.Count) FilterText = fullOptions[value];
            if (filteredOptions.Count != fullOptions.Count) {
               NotifyPropertyChanged(nameof(Options));
            }

            seg.Write(viewPort.Model, viewPort.CurrentChange, Start, BitOffset, value);
            RaiseDataChanged();
            NotifyPropertyChanged();
         }
      }

      #region Filtering

      private int recursionCheck;

      private bool isFiltering;
      public bool IsFiltering {
         get => isFiltering;
         set => Set(ref isFiltering, value, wasFiltering => {
            if (wasFiltering) SelectedIndex = 0; // reset selection
         });
      }

      private string filterText;
      public string FilterText {
         get => filterText;
         set => Set(ref filterText, value, FilterTextChanged);
      }

      private void FilterTextChanged(string oldValue) {
         if (recursionCheck != 0 || !isFiltering) return;
         recursionCheck++;
         var fullOptions = ArrayRunEnumSegment.GetOptions(viewPort.Model, EnumName).ToList();
         var options = fullOptions.Where(option => option.MatchesPartial(filterText, onlyCheckLettersAndDigits: true)).ToList();
         if (SelectedIndex >= 0 && SelectedIndex < fullOptions.Count && Options.Contains(fullOptions[SelectedIndex])) {
            // selected index is already fine
         } else if (options.Count > 0) {
            // based on typing filter text, we can change the selection
            SelectedIndex = fullOptions.IndexOf(options[0]);
         }
         NotifyPropertyChanged(nameof(Options));
         recursionCheck--;
      }

      #endregion

      public EnumTupleElementViewModel(ViewPort viewPort, int start, int bitOffset, TupleSegment segment, Action raiseDataChanged, Action raiseDataSelected) {
         (this.viewPort, Start, BitOffset, seg) = (viewPort, start, bitOffset, segment);
         var selectedIndex = SelectedIndex;
         var fullOptions = ArrayRunEnumSegment.GetOptions(viewPort.Model, EnumName).ToList();
         filterText = selectedIndex >= 0 && selectedIndex < fullOptions.Count ? fullOptions[selectedIndex] : selectedIndex.ToString();
         RaiseDataChanged = raiseDataChanged;
         RaiseDataSelected = raiseDataSelected;
      }

      public bool TryCopy(ITupleElementViewModel other) {
         if (!(other is EnumTupleElementViewModel that)) return false;
         if (EnumName != that.EnumName) return false;
         if (Name != that.Name) return false;
         if (BitOffset != that.BitOffset) return false;
         if (BitLength != that.BitLength) return false;

         Start = that.Start;
         NotifyPropertyChanged(nameof(Start));
         NotifyPropertyChanged(nameof(SelectedIndex));
         return true;
      }

      public void Focus() => RaiseDataSelected?.Invoke();
   }
}
