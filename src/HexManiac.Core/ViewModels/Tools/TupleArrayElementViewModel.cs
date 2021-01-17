using HavenSoft.HexManiac.Core.Models.Runs;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class TupleArrayElementViewModel : ViewModelCore, IArrayElementViewModel {
      private bool visible;
      public bool Visible { get => visible; set => Set(ref visible, value); }
      public bool IsInError => false;
      public string ErrorText => string.Empty;
      public event EventHandler DataChanged;

      private readonly ViewPort viewPort;
      public int Start { get; private set; }
      public ObservableCollection<ITupleElementViewModel> Children { get; }

      public TupleArrayElementViewModel(ViewPort viewPort, ArrayRunTupleSegment tupleItem, int start) {
         Children = new ObservableCollection<ITupleElementViewModel>();
         this.viewPort = viewPort;
         Start = start;
         var bitOffset = 0;
         for (int i = 0; i < tupleItem.Elements.Count; i++) {
            Children.Add(new NumericTupleElementViewModel(viewPort, Start, bitOffset, tupleItem.Elements[i]));
         }
      }

      public bool TryCopy(IArrayElementViewModel other) {
         throw new NotImplementedException();
      }
   }

   public interface ITupleElementViewModel : INotifyPropertyChanged {
      string Name { get; }
      int Start { get; }
      int BitOffset { get; }
      int BitLength { get; }
   }

   public class NumericTupleElementViewModel : ViewModelCore, ITupleElementViewModel {
      private readonly ViewPort viewPort;
      public string Name { get; }
      public int Start { get; }
      public int BitOffset { get; }
      public int BitLength { get; }
      public int Value { get; }
      public NumericTupleElementViewModel(ViewPort viewPort, int start, int bitOffset, TupleSegment segment) {
         this.viewPort = viewPort;
         (Name, Start, BitOffset, BitLength) = (segment.Name, start, bitOffset, segment.BitWidth);
      }
   }
}
