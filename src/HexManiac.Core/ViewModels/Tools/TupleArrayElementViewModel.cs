using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
            if (tupleItem.Elements[i].BitWidth == 1) {
               Children.Add(new CheckBoxTupleElementViewModel(viewPort, start, bitOffset, tupleItem.Elements[i].Name));
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
      protected ViewPort ViewPort { get; }
      public string Name { get; }
      public int BitOffset { get; }
      public int BitLength { get; }
      public int Start { get; private set; }

      public int Content {
         get {
            var requiredByteLength = (BitOffset + BitLength + 7) / 8;
            if (requiredByteLength > 4) return 0;
            var bitArray = ViewPort.Model.ReadMultiByteValue(Start, requiredByteLength);
            bitArray >>= BitOffset;
            bitArray &= (1 << BitLength) - 1;
            return bitArray;
         }
         set {
            var requiredByteLength = (BitOffset + BitLength + 7) / 8;
            if (requiredByteLength > 4) return;
            var bitArray = ViewPort.Model.ReadMultiByteValue(Start, requiredByteLength);
            var mask = (1 << BitLength) - 1;
            value &= mask;
            bitArray &= ~(mask << BitOffset);
            bitArray |= value << BitOffset;
            ViewPort.Model.WriteMultiByteValue(Start, requiredByteLength, ViewPort.CurrentChange, bitArray);
            NotifyPropertyChanged();
         }
      }

      public NumericTupleElementViewModel(ViewPort viewPort, int start, int bitOffset, TupleSegment segment) {
         ViewPort = viewPort;
         (Name, Start, BitOffset, BitLength) = (segment.Name, start, bitOffset, segment.BitWidth);
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
      private readonly ViewPort viewPort;
      public string Name { get; }
      public int BitOffset { get; }
      public int BitLength => 1;
      public int Start { get; private set; }

      public bool IsChecked {
         get {
            var requiredByteLength = (BitOffset + BitLength + 7) / 8;
            if (requiredByteLength > 4) return false;
            var bitArray = viewPort.Model.ReadMultiByteValue(Start, requiredByteLength);
            bitArray >>= BitOffset;
            return (bitArray & 1) == 1;
         }
         set {
            var requiredByteLength = (BitOffset + BitLength + 7) / 8;
            if (requiredByteLength > 4) return;
            var bitArray = viewPort.Model.ReadMultiByteValue(Start, requiredByteLength);
            if (value) {
               bitArray |= 1 << BitOffset;
            } else {
               bitArray &= ~(1 << BitOffset);
            }
            viewPort.Model.WriteMultiByteValue(Start, requiredByteLength, viewPort.CurrentChange, bitArray);
            NotifyPropertyChanged();
         }
      }

      public CheckBoxTupleElementViewModel(ViewPort viewPort, int start, int bitOffset, string name) {
         this.viewPort = viewPort;
         (Name, Start, BitOffset) = (name, start, bitOffset);
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

   public class EnumTupleElementViewModel : NumericTupleElementViewModel {
      public string EnumName { get; }

      public IReadOnlyList<string> Options => ArrayRunEnumSegment.GetOptions(ViewPort.Model, EnumName).ToList();

      public int SelectedIndex {
         get => Content;
         set {
            Content = value;
            NotifyPropertyChanged();
         }
      }

      public EnumTupleElementViewModel(ViewPort viewPort, int start, int bitOffset, TupleSegment segment) : base(viewPort, start, bitOffset, segment) {
         EnumName = segment.SourceName;
      }

      public override bool TryCopy(ITupleElementViewModel other) {
         if (!(other is EnumTupleElementViewModel that)) return false;
         if (EnumName != that.EnumName) return false;

         if (!base.TryCopy(other)) return false;

         NotifyPropertyChanged(nameof(SelectedIndex));
         return true;
      }
   }
}
