using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public enum ElementContentViewModelType {
      TextField,
      NumericField,
      Address,
      HexField,
      ComboBox,
   }

   public interface IArrayElementViewModel : INotifyPropertyChanged {
      event EventHandler DataChanged;
      bool IsInError { get; }
      string ErrorText { get; }
   }

   public class SplitterArrayElementViewModel : ViewModelCore, IArrayElementViewModel {
      event EventHandler IArrayElementViewModel.DataChanged { add { } remove { } }

      public bool IsInError => !string.IsNullOrEmpty(ErrorText);
      public string ErrorText { get; }
      public string SectionName { get; }
      public SplitterArrayElementViewModel(string sectionName) => SectionName = sectionName;
   }

   public interface IFieldArrayElementViewModelStrategy {
      ElementContentViewModelType Type { get; }
      void UpdateModelFromViewModel(FieldArrayElementViewModel viewModel);
      string UpdateViewModelFromModel(FieldArrayElementViewModel viewModel);
   }

   public class FieldArrayElementViewModel : ViewModelCore, IArrayElementViewModel {
      private readonly IFieldArrayElementViewModelStrategy strategy;

      public event EventHandler DataChanged;

      public ChangeHistory<ModelDelta> History { get; }
      public IDataModel Model { get; }
      public string Name { get; }
      public int Start { get; }
      public int Length { get; }

      public ElementContentViewModelType Type => strategy.Type;

      public bool IsInError => errorText != string.Empty;

      string errorText;
      public string ErrorText {
         get => errorText;
         set {
            if (TryUpdate(ref errorText, value)) NotifyPropertyChanged(nameof(IsInError));
         }
      }

      string content;
      public string Content {
         get => content;
         set {
            if (TryUpdate(ref content, value)) {
               strategy.UpdateModelFromViewModel(this);
               DataChanged?.Invoke(this, EventArgs.Empty);
            }
         }
      }

      public FieldArrayElementViewModel(ChangeHistory<ModelDelta> history, IDataModel model, string name, int start, int length, IFieldArrayElementViewModelStrategy strategy) {
         this.strategy = strategy;
         (History, Model, Name, Start, Length) = (history, model, name, start, length);
         content = strategy.UpdateViewModelFromModel(this);
      }

      public void RefreshControlFromModelChange() {
         TryUpdate(ref content, strategy.UpdateViewModelFromModel(this), nameof(Content));
      }
   }

   public class TextFieldStratgy : IFieldArrayElementViewModelStrategy {
      public ElementContentViewModelType Type => ElementContentViewModelType.TextField;

      public void UpdateModelFromViewModel(FieldArrayElementViewModel viewModel) {
         var textBytes = PCSString.Convert(viewModel.Content);
         while (textBytes.Count < viewModel.Length) textBytes.Add(0x00);
         if (textBytes.Count > viewModel.Length) textBytes[viewModel.Length - 1] = 0xFF;
         for (int i = 0; i < viewModel.Length; i++) {
            viewModel.History.CurrentChange.ChangeData(viewModel.Model, viewModel.Start + i, textBytes[i]);
         }
      }

      public string UpdateViewModelFromModel(FieldArrayElementViewModel viewModel) {
         var text = PCSString.Convert(viewModel.Model, viewModel.Start, viewModel.Length)?.Trim() ?? string.Empty;

         // take off quotes
         if (text.StartsWith("\"")) text = text.Substring(1);
         if (text.EndsWith("\"")) text = text.Substring(0, text.Length - 1);

         return text;
      }
   }

   public class NumericFieldStrategy : IFieldArrayElementViewModelStrategy {
      public ElementContentViewModelType Type => ElementContentViewModelType.NumericField;

      public void UpdateModelFromViewModel(FieldArrayElementViewModel viewModel) {
         if (int.TryParse(viewModel.Content, out int content)) {
            viewModel.Model.WriteMultiByteValue(viewModel.Start, viewModel.Length, viewModel.History.CurrentChange, content);
         } else {
            viewModel.ErrorText = $"{viewModel.Name} must be an integer.";
         }
      }

      public string UpdateViewModelFromModel(FieldArrayElementViewModel viewModel) {
         int number = viewModel.Model.ReadMultiByteValue(viewModel.Start, viewModel.Length);
         return number.ToString();
      }
   }

   public class AddressFieldStratgy : IFieldArrayElementViewModelStrategy {
      public ElementContentViewModelType Type => ElementContentViewModelType.Address;

      public void UpdateModelFromViewModel(FieldArrayElementViewModel viewModel) {
         var content = viewModel.Content;
         if (content.StartsWith(PointerRun.PointerStart.ToString())) content = content.Substring(1);
         if (content.EndsWith(PointerRun.PointerEnd.ToString())) content = content.Substring(0, content.Length - 1);

         int address;
         if (!int.TryParse(content, NumberStyles.HexNumber, CultureInfo.CurrentCulture.NumberFormat, out address)) {
            address = viewModel.Model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, content);
         }

         if (address != Pointer.NULL) {
            viewModel.Model.WritePointer(viewModel.History.CurrentChange, viewModel.Start, address);
         } else {
            viewModel.ErrorText = "Address should be hexidecimal or an anchor.";
         }
      }

      public string UpdateViewModelFromModel(FieldArrayElementViewModel viewModel) {
         var value = viewModel.Model.ReadPointer(viewModel.Start);
         var text = value.ToString("X6");
         return $"{PointerRun.PointerStart}{text}{PointerRun.PointerEnd}";
      }
   }

   public class HexFieldStratgy : IFieldArrayElementViewModelStrategy {
      public ElementContentViewModelType Type => ElementContentViewModelType.HexField;

      public void UpdateModelFromViewModel(FieldArrayElementViewModel viewModel) {
         if (int.TryParse(viewModel.Content, NumberStyles.HexNumber, CultureInfo.CurrentCulture.NumberFormat, out int hexValue)) {
            viewModel.Model.WriteMultiByteValue(viewModel.Start, viewModel.Length, viewModel.History.CurrentChange, hexValue);
         } else {
            viewModel.ErrorText = "Value should be hexidecimal.";
         }
      }

      public string UpdateViewModelFromModel(FieldArrayElementViewModel viewModel) {
         int number = viewModel.Model.ReadMultiByteValue(viewModel.Start, viewModel.Length);
         var text = number.ToString("X2");
         return text;
      }
   }

   public class ComboBoxArrayElementViewModel : ViewModelCore, IArrayElementViewModel {
      private readonly ChangeHistory<ModelDelta> history;

      public event EventHandler DataChanged;

      public IDataModel Model { get; }
      public string Name { get; }
      public int Start { get; }
      public int Length { get; }
      public ICommand GotoSource { get; }

      public ElementContentViewModelType Type => ElementContentViewModelType.ComboBox;

      public bool IsInError => errorText != string.Empty;

      string errorText;
      public string ErrorText {
         get => errorText;
         set {
            if (TryUpdate(ref errorText, value)) NotifyPropertyChanged(nameof(IsInError));
         }
      }

      private bool containsUniqueOption;
      public ObservableCollection<string> Options { get; }

      private int selectedIndex;

      public int SelectedIndex {
         get => selectedIndex;
         set {
            if (!TryUpdate(ref selectedIndex, value)) return;
            var run = (ITableRun)Model.GetNextRun(Start);
            var offsets = run.ConvertByteOffsetToArrayOffset(Start);
            var segment = (ArrayRunEnumSegment)run.ElementContent[offsets.SegmentIndex];

            // special case: the last option might be a weird value that came in, not normally available in the enum
            if (containsUniqueOption && selectedIndex == Options.Count - 1 && int.TryParse(Options[selectedIndex], out var parsedValue)) {
               value = parsedValue;
            }

            Model.WriteMultiByteValue(Start, Length, history.CurrentChange, value);
            DataChanged?.Invoke(this, EventArgs.Empty);
         }
      }

      public ComboBoxArrayElementViewModel(Selection selection, ChangeHistory<ModelDelta> history, IDataModel model, string name, int start, int length) {
         (this.history, Model, Name, Start, Length) = (history, model, name, start, length);
         var run = (ITableRun)Model.GetNextRun(Start);
         var offsets = run.ConvertByteOffsetToArrayOffset(start);
         var segment = (ArrayRunEnumSegment)run.ElementContent[offsets.SegmentIndex];
         var optionSource = model.GetAddressFromAnchor(history.CurrentChange, -1, segment.EnumName);
         Options = new ObservableCollection<string>(segment.GetOptions(model));
         var value = model.ReadMultiByteValue(start, length);
         if (value >= Options.Count) {
            Options.Add(value.ToString());
            selectedIndex = Options.Count - 1;
            containsUniqueOption = true;
         } else {
            selectedIndex = model.ReadMultiByteValue(start, length);
         }
         GotoSource = new StubCommand {
            CanExecute = arg => optionSource != Pointer.NULL,
            Execute = arg => selection.GotoAddress(optionSource),
         };
      }
   }

   public class StreamArrayElementViewModel : ViewModelCore, IArrayElementViewModel {
      private readonly ViewPort viewPort;
      private readonly FieldArrayElementViewModel matchingField;
      private readonly IDataModel model;
      private readonly string name;
      private readonly int start;

      public bool IsInError => !string.IsNullOrEmpty(ErrorText);
      public string ErrorText { get; private set; }
      public event EventHandler DataChanged;
      public event EventHandler<(int originalStart, int newStart)> DataMoved;

      string content;
      public string Content {
         get => content;
         set {
            if (TryUpdate(ref content, value)) {
               using (ModelCacheScope.CreateScope(model)) {
                  var destination = model.ReadPointer(start);
                  var run = (IStreamRun)model.GetNextRun(destination);
                  var newRun = run.DeserializeRun(content, viewPort.CurrentChange);
                  model.ObserveRunWritten(viewPort.CurrentChange, newRun);
                  if (run.Start != newRun.Start) {
                     DataMoved?.Invoke(this, (run.Start, newRun.Start));
                     matchingField.RefreshControlFromModelChange();
                  }
                  DataChanged?.Invoke(this, EventArgs.Empty);
               }
            }
         }
      }

      public string Message { get; }
      private bool canRepoint;
      public bool CanRepoint { get => canRepoint; set => TryUpdate(ref canRepoint, value); }
      public ICommand Repoint { get; }

      public StreamArrayElementViewModel(ViewPort viewPort, FieldArrayElementViewModel matchingField, IDataModel model, string name, int start) {
         this.viewPort = viewPort;
         this.matchingField = matchingField;
         this.model = model;
         this.name = name;
         this.start = start;

         var destination = model.ReadPointer(start);

         // by the time we get this far, we're nearly guaranteed that this will be a IStreamRun.
         // if it's not an IStreamRun, it's because the pointer in the array doesn't actually point to a valid stream.
         // at which point, we don't want to display any content.
         var run = model.GetNextRun(destination) as IStreamRun;
         if (run != null) {
            content = run?.SerializeRun() ?? string.Empty;
            var sourceCount = run.PointerSources.Count;
            if (sourceCount > 1) {
               CanRepoint = true;
               Message = $"This is used by {sourceCount} pointers.";
               Repoint = new StubCommand {
                  CanExecute = arg => CanRepoint,
                  Execute = arg => {
                     var originalDestination = model.ReadPointer(start);
                     viewPort.RepointToNewCopy(start);
                     var newDestination = model.ReadPointer(start);
                     matchingField.RefreshControlFromModelChange();
                     CanRepoint = false;
                  },
               };
            }
         }
      }
   }

   public class BitListArrayElementViewModel : ViewModelCore, IReadOnlyList<BitElement>, IArrayElementViewModel {
      private readonly ChangeHistory<ModelDelta> history;
      private readonly IDataModel model;
      private readonly int start;
      private readonly List<BitElement> children = new List<BitElement>();
      private readonly ArrayRunElementSegment segment;
      private readonly ArrayRun rotatedBitArray;

      public string Name { get; }

      public bool IsInError => !string.IsNullOrEmpty(ErrorText);
      public string ErrorText { get; private set; }

      public ICommand LinkCommand { get; }

      public event EventHandler DataChanged;

      public BitListArrayElementViewModel(Selection selection, ChangeHistory<ModelDelta> history, IDataModel model, string name, int start) {
         this.history = history;
         this.model = model;
         Name = name;
         this.start = start;

         var array = (ITableRun)model.GetNextRun(start);
         var anchor = model.GetAnchorFromAddress(-1, array.Start);
         var offset = array.ConvertByteOffsetToArrayOffset(start);
         segment = array.ElementContent[offset.SegmentIndex];
         if (segment is ArrayRunBitArraySegment bitSegment) {
            // get all the bits of this segment and turn them into BitElements
            var optionSource = model.GetAddressFromAnchor(history.CurrentChange, -1, bitSegment.SourceArrayName);
            var bits = model.ReadMultiByteValue(start, bitSegment.Length);
            var names = bitSegment.GetOptions(model);
            for (int i = 0; i < names.Count; i++) {
               var element = new BitElement { BitLabel = names[i] };
               children.Add(element);
               element.PropertyChanged += ChildChanged;
            }

            LinkCommand = new StubCommand {
               CanExecute = arg => optionSource != Pointer.NULL,
               Execute = arg => selection.GotoAddress(optionSource),
            };
         } else if (segment is ArrayRunEnumSegment enumSegment) {
            // get 1 bit of each segment and turn them into BitElements
            foreach (var option in model.Arrays) {
               if (option.ElementNames.Count != option.ElementCount) continue;
               var segment = option.ElementContent[0];
               if (segment is ArrayRunBitArraySegment match && match.SourceArrayName == anchor && option.ElementContent.Count == 1) {
                  rotatedBitArray = option;
                  for (int i = 0; i < option.ElementCount; i++) {
                     var element = new BitElement { BitLabel = option.ElementNames[i] };
                     children.Add(element);
                     element.PropertyChanged += ChildChanged;
                  }
                  break;
               }
               if (rotatedBitArray != null) break;
            }
            if (rotatedBitArray == null) throw new NotImplementedException();

            LinkCommand = new StubCommand {
               CanExecute = arg => true,
               Execute = arg => selection.GotoAddress(rotatedBitArray.Start),
            };
         } else {
            throw new NotImplementedException();
         }

         UpdateViewFromModel();
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
               history.CurrentChange.ChangeData(model, start + i, result);
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
         DataChanged?.Invoke(this, EventArgs.Empty);
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

      private void ChildChanged(object sender, PropertyChangedEventArgs e) {
         UpdateModelFromView();
      }
   }

   public class ButtonArrayElementViewModel : ViewModelCore, IArrayElementViewModel {
      public bool IsInError => false;

      public string ErrorText => string.Empty;

      public event EventHandler DataChanged;

      public string Text { get; }
      public ICommand Command { get; }

      public ButtonArrayElementViewModel(string text, Action action) {
         Text = text;
         Command = new StubCommand {
            CanExecute = arg => true,
            Execute = arg => action(),
         };
      }
   }

   public class BitElement : ViewModelCore {
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
   }
}
