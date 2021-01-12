using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class BitElement : ViewModelCore, IEquatable<BitElement> {
      private string bitLabel;
      public string BitLabel {
         get => bitLabel;
         set => TryUpdate(ref bitLabel, value);
      }

      private bool isChecked;
      public bool IsChecked {
         get => isChecked;
         set => TryUpdate(ref isChecked, value);
      }

      public bool Equals(BitElement other) => bitLabel == other.bitLabel && isChecked == other.isChecked;
   }

   public class BitListArrayElementViewModel : ViewModelCore, IReadOnlyList<BitElement>, IArrayElementViewModel {
      private readonly ChangeHistory<ModelDelta> history;
      private readonly IDataModel model;
      private readonly List<BitElement> children = new List<BitElement>();
      private readonly ArrayRunElementSegment segment;
      private readonly ArrayRun rotatedBitArray;

      private int start;

      public string Name { get; }

      public bool IsInError => !string.IsNullOrEmpty(ErrorText);
      public string ErrorText { get; private set; }

      public ICommand LinkCommand { get; }

      private bool visible = true;
      public bool Visible { get => visible; set => Set(ref visible, value); }

      private EventHandler dataChanged;
      public event EventHandler DataChanged { add => dataChanged += value; remove => dataChanged -= value; }

      private StubCommand selectAll, unselectAll;
      public ICommand SelectAll => StubCommand(ref selectAll, () => SetAll(true));
      public ICommand UnselectAll => StubCommand(ref unselectAll, () => SetAll(false));
      private void SetAll(bool value) {
         deferChanges = true;
         foreach (var element in this) element.IsChecked = value;
         deferChanges = false;
         UpdateModelFromView();
      }

      public BitListArrayElementViewModel(Selection selection, ChangeHistory<ModelDelta> history, IDataModel model, string name, int start) {
         this.history = history;
         this.model = model;
         Name = name;
         this.start = start;

         var array = (ITableRun)model.GetNextRun(start);
         var offset = array.ConvertByteOffsetToArrayOffset(start);
         segment = array.ElementContent[offset.SegmentIndex];
         if (segment is ArrayRunBitArraySegment bitSegment) {
            var optionSource = FillBodyFromBitArraySegment(bitSegment);

            LinkCommand = new StubCommand {
               CanExecute = arg => optionSource != Pointer.NULL,
               Execute = arg => selection.GotoAddress(optionSource),
            };
         } else if (segment is ArrayRunEnumSegment) {
            rotatedBitArray = FillBodyRotated();

            LinkCommand = new StubCommand {
               CanExecute = arg => true,
               Execute = arg => selection.GotoAddress(rotatedBitArray.Start),
            };
         } else {
            throw new NotImplementedException();
         }

         UpdateViewFromModel();
      }

      private int FillBodyFromBitArraySegment(ArrayRunBitArraySegment bitSegment) {
         // get all the bits of this segment and turn them into BitElements
         var optionSource = model.GetAddressFromAnchor(history.CurrentChange, -1, bitSegment.SourceArrayName);
         var bits = model.ReadMultiByteValue(start, bitSegment.Length);
         var names = bitSegment.GetOptions(model)?.ToArray() ?? new string[0];
         Debug.Assert(names.Length > 0, "The user is using a source for a bit array that either doesn't exist or has no length. This is probably not what the user wanted.");
         for (int i = 0; i < names.Length; i++) {
            var element = new BitElement { BitLabel = names[i] };
            children.Add(element);
            element.PropertyChanged += ChildChanged;
         }

         return optionSource;
      }

      private ArrayRun FillBodyRotated() {
         // get 1 bit of each segment and turn them into BitElements
         var array = (ITableRun)model.GetNextRun(start);
         var anchor = model.GetAnchorFromAddress(-1, array.Start);
         ArrayRun rotatedBitArray = null;

         foreach (var option in model.Arrays) {
            var segment = option.ElementContent[0]; // TODO should I be using enumSegment?
            if (segment is ArrayRunBitArraySegment match && match.SourceArrayName == anchor && option.ElementContent.Count == 1) {
               rotatedBitArray = option;
               for (int i = 0; i < option.ElementCount; i++) {
                  var name = option.ElementNames.Count > i ? option.ElementNames[i] : i.ToString();
                  var element = new BitElement { BitLabel = name };
                  children.Add(element);
                  element.PropertyChanged += ChildChanged;
               }
               break;
            }
            if (rotatedBitArray != null) break;
         }
         if (rotatedBitArray == null) throw new NotImplementedException($"Trying to Rotate {anchor}:{Name} at {start:X6}, but couldn't find a matching array.");

         return rotatedBitArray;
      }

      #region IReadOnlyList<BitElement> Implementation

      public int Count => children.Count;
      public BitElement this[int index] => children[index];
      public IEnumerator<BitElement> GetEnumerator() => children.GetEnumerator();
      IEnumerator IEnumerable.GetEnumerator() => children.GetEnumerator();

      #endregion

      public void UpdateModelFromView() {
         if (rotatedBitArray == null) {
            for (int i = 0; i < segment.Length; i++) {
               byte result = 0;
               for (int j = 0; j < 8 && children.Count > i * 8 + j; j++) result += (byte)(children[i * 8 + j].IsChecked ? (1 << j) : 0);
               if (model[start + i] != result) history.CurrentChange.ChangeData(model, start + i, result);
            }
         } else {
            var array = (ArrayRun)model.GetNextRun(start);
            var offset = array.ConvertByteOffsetToArrayOffset(start);
            var bitIndex = offset.ElementIndex;
            var (byteShift, bitShift) = (bitIndex / 8, bitIndex % 8);
            for (int i = 0; i < rotatedBitArray.ElementCount; i++) {
               var elementStart = rotatedBitArray.Start + rotatedBitArray.ElementLength * i + byteShift;
               var bits = model[elementStart];
               if (children[i].IsChecked) {
                  bits |= (byte)(1 << bitShift);
               } else {
                  bits &= (byte)~(1 << bitShift);
               }
               if (bits != model[elementStart]) history.CurrentChange.ChangeData(model, elementStart, bits);
            }
         }
         dataChanged?.Invoke(this, EventArgs.Empty);
      }

      public void UpdateViewFromModel() {
         if (rotatedBitArray == null) {
            for (int i = 0; i < segment.Length; i++) {
               var bits = model[start + i];
               for (int j = 0; j < 8; j++) {
                  if (children.Count <= i * 8 + j) break;
                  children[i * 8 + j].PropertyChanged -= ChildChanged;
                  children[i * 8 + j].IsChecked = ((bits >> j) & 1) != 0;
                  children[i * 8 + j].PropertyChanged += ChildChanged;
               }
            }
         } else {
            var array = (ArrayRun)model.GetNextRun(start);
            var offset = array.ConvertByteOffsetToArrayOffset(start);
            var bitIndex = offset.ElementIndex;
            var (byteShift, bitShift) = (bitIndex / 8, bitIndex % 8);
            for (int i = 0; i < rotatedBitArray.ElementCount; i++) {
               var elementStart = rotatedBitArray.Start + rotatedBitArray.ElementLength * i + byteShift;
               var bits = model[elementStart];
               children[i].PropertyChanged -= ChildChanged;
               children[i].IsChecked = ((bits >> bitShift) & 1) != 0;
               children[i].PropertyChanged += ChildChanged;
            }
         }
      }

      public bool TryCopy(IArrayElementViewModel other) {
         if (!(other is BitListArrayElementViewModel bitList)) return false;
         if (Name != bitList.Name) return false;
         if (segment != bitList.segment) return false;
         if (rotatedBitArray != bitList.rotatedBitArray) return false;
         if (!children.Select(child => child.BitLabel).SequenceEqual(bitList.children.Select(child => child.BitLabel))) return false;

         start = bitList.start;
         Visible = other.Visible;
         for (int i = 0; i < children.Count; i++) {
            children[i].PropertyChanged -= ChildChanged;
            children[i].IsChecked = bitList.children[i].IsChecked;
            children[i].PropertyChanged += ChildChanged;
         }
         dataChanged = bitList.dataChanged;

         return true;
      }

      private bool deferChanges;
      private void ChildChanged(object sender, PropertyChangedEventArgs e) {
         if (deferChanges) return;
         UpdateModelFromView();
      }
   }
}
