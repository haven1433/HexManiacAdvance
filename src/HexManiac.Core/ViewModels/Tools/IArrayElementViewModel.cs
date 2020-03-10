using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
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
      bool TryCopy(IArrayElementViewModel other);
   }

   public class SplitterArrayElementViewModel : ViewModelCore, IArrayElementViewModel {
      event EventHandler IArrayElementViewModel.DataChanged { add { } remove { } }

      private string sectionName;

      public bool IsInError => !string.IsNullOrEmpty(ErrorText);
      public string ErrorText { get; }
      public string SectionName { get => sectionName; set => TryUpdate(ref sectionName, value); }
      public SplitterArrayElementViewModel(string sectionName) => SectionName = sectionName;

      public bool TryCopy(IArrayElementViewModel other) {
         if (!(other is SplitterArrayElementViewModel splitter)) return false;
         SectionName = splitter.SectionName;
         return true;
      }
   }

   public interface IFieldArrayElementViewModelStrategy {
      ElementContentViewModelType Type { get; }
      void UpdateModelFromViewModel(FieldArrayElementViewModel viewModel);
      string UpdateViewModelFromModel(FieldArrayElementViewModel viewModel);
   }

   public class FieldArrayElementViewModel : ViewModelCore, IArrayElementViewModel {
      private readonly IFieldArrayElementViewModelStrategy strategy;

      private string name;
      private int start, length;

      private EventHandler dataChanged;
      public event EventHandler DataChanged { add => dataChanged += value; remove => dataChanged -= value; }

      public ViewPort ViewPort { get; }
      public IDataModel Model { get; }
      public string Name { get => name; set => TryUpdate(ref name, value); }
      public int Start { get => start; set => TryUpdate(ref start, value); }
      public int Length { get => length; set => TryUpdate(ref length, value); }

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
               using (ModelCacheScope.CreateScope(Model)) {
                  ErrorText = string.Empty;
                  strategy.UpdateModelFromViewModel(this);
                  if (!IsInError) {
                     dataChanged?.Invoke(this, EventArgs.Empty);
                  }
               }
            }
         }
      }

      public FieldArrayElementViewModel(ViewPort viewPort, string name, int start, int length, IFieldArrayElementViewModelStrategy strategy) {
         this.strategy = strategy;
         (ViewPort, Model, Name, Start, Length) = (viewPort, viewPort.Model, name, start, length);
         content = strategy.UpdateViewModelFromModel(this);
      }

      public bool TryCopy(IArrayElementViewModel other) {
         if (!(other is FieldArrayElementViewModel field)) return false;
         if (strategy.Type != field.strategy.Type) return false;

         Name = field.Name;
         Start = field.Start;
         Length = field.Length;
         TryUpdate(ref content, field.Content, nameof(Content));
         ErrorText = field.ErrorText;
         dataChanged = field.dataChanged;
         return true;
      }
   }

   public class TextFieldStratgy : IFieldArrayElementViewModelStrategy {
      public ElementContentViewModelType Type => ElementContentViewModelType.TextField;

      public void UpdateModelFromViewModel(FieldArrayElementViewModel viewModel) {
         var textBytes = PCSString.Convert(viewModel.Content);
         while (textBytes.Count < viewModel.Length) textBytes.Add(0x00);
         if (textBytes.Count > viewModel.Length) textBytes[viewModel.Length - 1] = 0xFF;
         for (int i = 0; i < viewModel.Length; i++) {
            viewModel.ViewPort.CurrentChange.ChangeData(viewModel.Model, viewModel.Start + i, textBytes[i]);
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
            viewModel.Model.WriteMultiByteValue(viewModel.Start, viewModel.Length, viewModel.ViewPort.CurrentChange, content);
            var run = (ITableRun)viewModel.Model.GetNextRun(viewModel.Start);
            var offsets = run.ConvertByteOffsetToArrayOffset(viewModel.Start);
            var error = run.NotifyChildren(viewModel.Model, viewModel.ViewPort.CurrentChange, offsets.ElementIndex, offsets.SegmentIndex);
            if (error.IsWarning) viewModel.ViewPort.RaiseMessage(error.ErrorMessage);
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
         var content = viewModel.Content.Trim();
         if (content.StartsWith(PointerRun.PointerStart.ToString())) content = content.Substring(1);
         if (content.EndsWith(PointerRun.PointerEnd.ToString())) content = content.Substring(0, content.Length - 1);

         int address;
         if (!int.TryParse(content, NumberStyles.HexNumber, CultureInfo.CurrentCulture.NumberFormat, out address)) {
            address = viewModel.Model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, content);
            if (address == Pointer.NULL && content != "null") {
               viewModel.ErrorText = "Address should be hexidecimal or an anchor.";
               return;
            }
         }

         viewModel.Model.WritePointer(viewModel.ViewPort.CurrentChange, viewModel.Start, address);
      }

      public string UpdateViewModelFromModel(FieldArrayElementViewModel viewModel) {
         var value = viewModel.Model.ReadPointer(viewModel.Start);
         var text = value.ToString("X6");
         if (value == Pointer.NULL) text = "null";
         return $"{PointerRun.PointerStart}{text}{PointerRun.PointerEnd}";
      }
   }

   public class HexFieldStratgy : IFieldArrayElementViewModelStrategy {
      public ElementContentViewModelType Type => ElementContentViewModelType.HexField;

      public void UpdateModelFromViewModel(FieldArrayElementViewModel viewModel) {
         if (int.TryParse(viewModel.Content, NumberStyles.HexNumber, CultureInfo.CurrentCulture.NumberFormat, out int hexValue)) {
            viewModel.Model.WriteMultiByteValue(viewModel.Start, viewModel.Length, viewModel.ViewPort.CurrentChange, hexValue);
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
      private readonly Selection selection;

      private string name;
      private int start, length;

      private EventHandler dataChanged;
      public event EventHandler DataChanged { add => dataChanged += value; remove => dataChanged -= value; }

      public IDataModel Model { get; }
      public string Name { get => name; set => TryUpdate(ref name, value); }
      public int Start {
         get => start; set {
            if (!TryUpdate(ref start, value)) return;
         }
      }
      public int Length { get => length; set => TryUpdate(ref length, value); }
      public ICommand GotoSource { get; private set; }

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
      public List<ComboOption> Options { get; private set; }

      private int selectedIndex;

      public int SelectedIndex {
         get => selectedIndex;
         set {
            using (ModelCacheScope.CreateScope(Model)) {
               if (!TryUpdate(ref selectedIndex, value)) return;
               var run = (ITableRun)Model.GetNextRun(Start);
               var offsets = run.ConvertByteOffsetToArrayOffset(Start);
               var segment = (ArrayRunEnumSegment)run.ElementContent[offsets.SegmentIndex];

               // special case: the last option might be a weird value that came in, not normally available in the enum
               if (containsUniqueOption && selectedIndex == Options.Count - 1 && int.TryParse(Options[selectedIndex].Text, out var parsedValue)) {
                  value = parsedValue;
               }

               Model.WriteMultiByteValue(Start, Length, history.CurrentChange, value);
               run.NotifyChildren(Model, history.CurrentChange, offsets.ElementIndex, offsets.SegmentIndex);
               dataChanged?.Invoke(this, EventArgs.Empty);
            }
         }
      }

      public ComboBoxArrayElementViewModel(Selection selection, ChangeHistory<ModelDelta> history, IDataModel model, string name, int start, int length) {
         (this.selection, this.history, Model, Name, Start, Length) = (selection, history, model, name, start, length);
         var run = (ITableRun)Model.GetNextRun(Start);
         var offsets = run.ConvertByteOffsetToArrayOffset(start);
         var segment = run.ElementContent[offsets.SegmentIndex] as ArrayRunEnumSegment;
         int optionSource = Pointer.NULL;
         Debug.Assert(segment != null);
         if (segment != null) {
            optionSource = Model.GetAddressFromAnchor(history.CurrentChange, -1, segment.EnumName);
            Options = new List<ComboOption>(segment.GetOptions(Model).Select(option => new ComboOption(option)));
         } else {
            Options = new List<ComboOption>();
         }
         var modelValue = Model.ReadMultiByteValue(start, length);
         if (modelValue >= Options.Count) {
            Options.Add(modelValue.ToString());
            selectedIndex = Options.Count - 1;
            containsUniqueOption = true;
         } else {
            selectedIndex = Model.ReadMultiByteValue(start, length);
         }
         GotoSource = new StubCommand {
            CanExecute = arg => optionSource != Pointer.NULL,
            Execute = arg => selection.GotoAddress(optionSource),
         };
      }

      public bool TryCopy(IArrayElementViewModel other) {
         if (!(other is ComboBoxArrayElementViewModel comboBox)) return false;
         Name = comboBox.Name;
         Length = comboBox.Length;
         Start = comboBox.Start;

         // only update the options if they're different
         if (!Options.Select(option => option.Text).SequenceEqual(comboBox.Options.Select(option => option.Text))) {
            selectedIndex = -1; // changing options will make the UIElement update the SelectedIndex automatically. Set it first so that we don't cause a data change.
            Options = comboBox.Options;
            NotifyPropertyChanged(nameof(Options));
         }

         containsUniqueOption = comboBox.containsUniqueOption;
         TryUpdate(ref selectedIndex, comboBox.SelectedIndex, nameof(SelectedIndex));
         ErrorText = comboBox.ErrorText;
         GotoSource = comboBox.GotoSource;
         NotifyPropertyChanged(nameof(GotoSource));
         dataChanged = comboBox.dataChanged;

         return true;
      }
   }

   /// <summary>
   /// This exists to wrap a string, just so that WPF doesn't mess up the combo-box selection in the case of multiple indexes having the same text.
   /// </summary>
   public class ComboOption {
      public string Text { get; }
      public ComboOption(string text) { Text = text; }
      public static implicit operator ComboOption(string text) => new ComboOption(text);
   }

   public class StreamArrayElementViewModel : ViewModelCore, IStreamArrayElementViewModel {
      private readonly ViewPort viewPort;
      private readonly IDataModel model;

      private string name;
      private int start;
      private EventHandler dataChanged;
      private EventHandler<(int originalStart, int newStart)> dataMoved;

      public bool IsInError => !string.IsNullOrEmpty(ErrorText);
      public string ErrorText { get; private set; }
      public event EventHandler DataChanged { add => dataChanged += value; remove => dataChanged -= value; }
      public event EventHandler<(int originalStart, int newStart)> DataMoved { add => dataMoved += value; remove => dataMoved -= value; }

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
                     dataMoved?.Invoke(this, (run.Start, newRun.Start));
                  }
                  overrideCopyAttempt = true;
                  using (new StubDisposable { Dispose = () => overrideCopyAttempt = false }) {
                     dataChanged?.Invoke(this, EventArgs.Empty);
                  }
               }
            }
         }
      }

      public string Message { get; private set; }
      private bool canRepoint, canCreateNew;
      public bool CanRepoint { get => canRepoint; set => TryUpdate(ref canRepoint, value); }
      public bool CanCreateNew {
         get => canCreateNew;
         set {
            if (TryUpdate(ref canCreateNew, value)) {
               NotifyPropertyChanged(nameof(ShowContent));
            }
         }
      }
      public bool ShowContent => !CanCreateNew;
      public ICommand Repoint { get; private set; }
      public ICommand CreateNew { get; private set; }

      public StreamArrayElementViewModel(ViewPort viewPort, IDataModel model, string name, int start) {
         this.viewPort = viewPort;
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
               Message = $"{sourceCount}";
            }
         }
         if (destination == Pointer.NULL) {
            content = string.Empty;
            CanCreateNew = true;
         }
         Repoint = new StubCommand {
            CanExecute = arg => CanRepoint,
            Execute = arg => {
               using (ModelCacheScope.CreateScope(this.model)) {
                  var originalDestination = this.model.ReadPointer(this.start);
                  this.viewPort.RepointToNewCopy(this.start);
                  CanRepoint = false;
                  using (new StubDisposable { Dispose = () => overrideCopyAttempt = false }) {
                     dataChanged?.Invoke(this, EventArgs.Empty);
                  }
               }
            },
         };
         CreateNew = new StubCommand {
            CanExecute = arg => CanCreateNew,
            Execute = arg => {
               using (ModelCacheScope.CreateScope(this.model)) {
                  this.viewPort.RepointToNewCopy(this.start);
                  CanCreateNew = false;
                  using (new StubDisposable { Dispose = () => overrideCopyAttempt = false }) {
                     dataChanged?.Invoke(this, EventArgs.Empty);
                  }
               }
            },
         };
      }

      // if the source of the copy attempt is a datachange that I triggered myself, then ignore the copy and keep the same contents.
      private bool overrideCopyAttempt;
      public bool TryCopy(IArrayElementViewModel other) {
         if (!(other is StreamArrayElementViewModel stream)) return false;
         if (overrideCopyAttempt) return true;
         name = stream.name;
         start = stream.start;
         TryUpdate(ref content, stream.content, nameof(Content));
         Message = stream.Message;
         NotifyPropertyChanged(nameof(Message));
         if (CanRepoint != stream.CanRepoint) {
            CanRepoint = stream.CanRepoint;
            ((StubCommand)Repoint).CanExecuteChanged?.Invoke(this, EventArgs.Empty);
         }
         if (CanCreateNew != stream.CanCreateNew) {
            CanCreateNew = stream.CanCreateNew;
            ((StubCommand)CreateNew).CanExecuteChanged?.Invoke(this, EventArgs.Empty);
         }
         dataChanged = stream.dataChanged;
         dataMoved = stream.dataMoved;
         return true;
      }
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

      private EventHandler dataChanged;
      public event EventHandler DataChanged { add => dataChanged += value; remove => dataChanged -= value; }

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
         } else if (segment is ArrayRunEnumSegment enumSegment) {
            rotatedBitArray = FillBodyFromEnumSegment(enumSegment);

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
         var names = bitSegment.GetOptions(model) ?? new string[0];
         Debug.Assert(names.Count > 0, "The user is using a source for a bit array that either doesn't exist or has no length. This is probably not what the user wanted.");
         for (int i = 0; i < names.Count; i++) {
            var element = new BitElement { BitLabel = names[i] };
            children.Add(element);
            element.PropertyChanged += ChildChanged;
         }

         return optionSource;
      }

      private ArrayRun FillBodyFromEnumSegment(ArrayRunEnumSegment enumSegment) {
         // get 1 bit of each segment and turn them into BitElements
         var array = (ITableRun)model.GetNextRun(start);
         var anchor = model.GetAnchorFromAddress(-1, array.Start);
         ArrayRun rotatedBitArray = null;

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
         for (int i = 0; i < children.Count; i++) {
            children[i].PropertyChanged -= ChildChanged;
            children[i].IsChecked = bitList.children[i].IsChecked;
            children[i].PropertyChanged += ChildChanged;
         }
         dataChanged = bitList.dataChanged;

         return true;
      }

      private void ChildChanged(object sender, PropertyChangedEventArgs e) {
         using (ModelCacheScope.CreateScope(model)) {
            UpdateModelFromView();
         }
      }
   }

   public class ButtonArrayElementViewModel : ViewModelCore, IArrayElementViewModel {
      public bool IsInError => false;

      public string ErrorText => string.Empty;

      event EventHandler IArrayElementViewModel.DataChanged { add { } remove { } }

      public string Text { get; private set; }
      public ICommand Command { get; private set; }

      public ButtonArrayElementViewModel(string text, Action action) {
         Text = text;
         Command = new StubCommand {
            CanExecute = arg => true,
            Execute = arg => action(),
         };
      }

      public bool TryCopy(IArrayElementViewModel other) {
         if (!(other is ButtonArrayElementViewModel button)) return false;
         if (Text != button.Text) return false;
         Command = button.Command;
         NotifyPropertyChanged(nameof(Command));
         return true;
      }
   }

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
}
