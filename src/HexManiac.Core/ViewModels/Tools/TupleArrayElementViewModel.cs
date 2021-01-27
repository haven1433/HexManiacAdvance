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
      public event EventHandler DataChanged;

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
               Children.Add(new EnumTupleElementViewModel(viewPort, start, bitOffset, tupleItem.Elements[i]));
            } else {
               Children.Add(new NumericTupleElementViewModel(viewPort, start, bitOffset, tupleItem.Elements[i]));
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

      public int Content {
         get => seg.Read(viewPort.Model, Start, BitOffset);
         set {
            seg.Write(viewPort.Model, viewPort.CurrentChange, Start, BitOffset, value);
            NotifyPropertyChanged();
         }
      }

      public NumericTupleElementViewModel(ViewPort viewPort, int start, int bitOffset, TupleSegment segment) {
         (this.viewPort, seg, Start, BitOffset) = (viewPort, segment, start, bitOffset);
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

      public IReadOnlyList<string> Options => ArrayRunEnumSegment.GetOptions(viewPort.Model, EnumName).ToList();

      public int SelectedIndex {
         get => seg.Read(viewPort.Model, Start, BitOffset);
         set {
            seg.Write(viewPort.Model, viewPort.CurrentChange, Start, BitOffset, value);
            NotifyPropertyChanged();
         }
      }

      public EnumTupleElementViewModel(ViewPort viewPort, int start, int bitOffset, TupleSegment segment) {
         (this.viewPort, Start, BitOffset, seg) = (viewPort, start, bitOffset, segment);
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
   }
}
