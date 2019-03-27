using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public enum ElementContentViewModelType {
      TextField,
      NumericField,
      Address,
      HexField,
      ComboBox,
   }

   public interface IArrayElementViewModel : INotifyPropertyChanged {
      ElementContentViewModelType Type { get; }
      bool IsInError { get; }
      string ErrorText { get; }
   }

   public interface IFieldArrayElementViewModelStrategy {
      ElementContentViewModelType Type { get; }
      void UpdateModelFromViewModel(FieldArrayElementViewModel viewModel);
      string UpdateViewModelFromModel(FieldArrayElementViewModel viewModel);
   }

   public class FieldArrayElementViewModel : ViewModelCore, IArrayElementViewModel {
      private readonly IFieldArrayElementViewModelStrategy strategy;

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
            }
         }
      }

      public FieldArrayElementViewModel(ChangeHistory<ModelDelta> history, IDataModel model, string name, int start, int length, IFieldArrayElementViewModelStrategy strategy) {
         this.strategy = strategy;
         (History, Model, Name, Start, Length) = (history, model, name, start, length);
         content = strategy.UpdateViewModelFromModel(this);
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
         var text = PCSString.Convert(viewModel.Model, viewModel.Start, viewModel.Length).Trim();
         text = text.Substring(1, text.Length - 2); // take off quotes
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
         int address;
         if (!int.TryParse(viewModel.Content, NumberStyles.HexNumber, CultureInfo.CurrentCulture.NumberFormat, out address)) {
            address = viewModel.Model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, viewModel.Content);
         }

         if (address != Pointer.NULL) {
            viewModel.Model.WritePointer(viewModel.History.CurrentChange, address, viewModel.Start);
         } else {
            viewModel.ErrorText = "Address should be hexidecimal or an anchor.";
         }
      }

      public string UpdateViewModelFromModel(FieldArrayElementViewModel viewModel) {
         var value = viewModel.Model.ReadPointer(viewModel.Start);
         var text = value.ToString("X2");
         while (text.Length < 6) text = "0" + text;
         return text;
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

      public IDataModel Model { get; }
      public string Name { get; }
      public int Start { get; }
      public int Length { get; }

      public ElementContentViewModelType Type => ElementContentViewModelType.ComboBox;

      public bool IsInError => errorText != string.Empty;

      string errorText;
      public string ErrorText {
         get => errorText;
         set {
            if (TryUpdate(ref errorText, value)) NotifyPropertyChanged(nameof(IsInError));
         }
      }

      public ObservableCollection<string> Options { get; }

      private int selectedIndex;

      public int SelectedIndex {
         get => selectedIndex;
         set {
            if (!TryUpdate(ref selectedIndex, value)) return;
            var run = (ArrayRun)Model.GetNextRun(Start);
            var offsets = run.ConvertByteOffsetToArrayOffset(Start);
            var segment = (ArrayRunEnumSegment)run.ElementContent[offsets.SegmentIndex];
            Model.WriteMultiByteValue(Start, Length, history.CurrentChange, value);
         }
      }

      public ComboBoxArrayElementViewModel(ChangeHistory<ModelDelta> history, IDataModel model, string name, int start, int length) {
         (this.history, Model, Name, Start, Length) = (history, model, name, start, length);
         var run = (ArrayRun)Model.GetNextRun(Start);
         var offsets = run.ConvertByteOffsetToArrayOffset(start);
         var segment = (ArrayRunEnumSegment)run.ElementContent[offsets.SegmentIndex];
         Options = new ObservableCollection<string>(segment.GetOptions(model));
         selectedIndex = model.ReadMultiByteValue(start, length);
      }
   }
}
