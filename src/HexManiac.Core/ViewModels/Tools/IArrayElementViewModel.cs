using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Globalization;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public enum ElementContentViewModelType {
      TextField,
      NumericField,
      Address,
      HexField,
      ComboBox,
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
}
