using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Globalization;
using System.Runtime.Serialization.Formatters;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public enum ElementContentViewModelType {
      TextField,
      NumericField,
      Address,
      HexField,
      ColorField,
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

      private EventHandler dataSelected;
      public event EventHandler DataSelected { add => dataSelected += value; remove => dataSelected -= value; }

      public ViewPort ViewPort { get; }
      public IDataModel Model { get; }
      public string Name { get => name; set => TryUpdate(ref name, value); }
      public int Start { get => start; set => TryUpdate(ref start, value); }
      public int Length { get => length; set => TryUpdate(ref length, value); }

      private bool visible = true;
      public bool Visible { get => visible; set => Set(ref visible, value); }

      public ElementContentViewModelType Type => strategy.Type;

      public bool IsInError => errorText != string.Empty;

      string errorText;
      public string ErrorText {
         get => errorText;
         set {
            if (TryUpdate(ref errorText, value)) NotifyPropertyChanged(nameof(IsInError));
         }
      }

      public int ZIndex => 0;

      string content;
      public string Content {
         get => content;
         set {
            if (TryUpdate(ref content, value)) {
               ErrorText = string.Empty;
               strategy.UpdateModelFromViewModel(this);
               if (!IsInError) {
                  if (Model.GetNextRun(Start) is ITableRun table) {
                     var offsets = table.ConvertByteOffsetToArrayOffset(Start);
                     var info = table.NotifyChildren(Model, ViewPort.CurrentChange, offsets.ElementIndex, offsets.SegmentIndex);
                     ViewPort.HandleErrorInfo(info);
                  }
                  dataChanged?.Invoke(this, EventArgs.Empty);
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
         if (!TryCopy(field)) return false;
         if (strategy.Type != field.strategy.Type) return false;

         Name = field.Name;
         Start = field.Start;
         Length = field.Length;
         Visible = other.Visible;
         TryUpdate(ref content, field.Content, nameof(Content));
         ErrorText = field.ErrorText;
         dataChanged = field.dataChanged;
         dataSelected = field.dataSelected;
         CanAcceptChanged?.Invoke(this, EventArgs.Empty);
         return true;
      }

      protected virtual bool TryCopy(FieldArrayElementViewModel other) => true;

      public event EventHandler CanAcceptChanged;

      public void Accept() {
         // If we add more accept commands, move this logic into the strategy classes.
         // right now, don't bother, since there's just one.
         if (Type == ElementContentViewModelType.Address) {
            ViewPort.Goto.Execute(Content);
         }
      }

      public bool CanAccept() {
         return Type == ElementContentViewModelType.Address && ViewPort.Goto.CanExecute(Content);
      }

      public void Focus() => dataSelected?.Invoke(this, EventArgs.Empty);

      #region Increment/Decrement

      public void IncrementValue() {
         if (strategy is not NumericFieldStrategy numeric) return;
         if (!int.TryParse(Content, out var value)) return;
         value += name.ToLower() == "yoffset" ? -1 : 1;
         Content = value.ToString();
      }

      public void DecrementValue() {
         if (strategy is not NumericFieldStrategy numeric) return;
         if (!int.TryParse(Content, out var value)) return;
         value += name.ToLower() == "yoffset" ? 1 : -1;
         Content = value.ToString();
      }

      #endregion
   }

   public class ColorFieldArrayElementViewModel : FieldArrayElementViewModel {
      private short color;
      public short Color { get => color; set => Set(ref color, value, HandleColorChange); }

      public ColorFieldArrayElementViewModel(ViewPort viewPort, string name, int start) : base(viewPort, name, start, 2, ColorFieldStrategy.Instance) {
         Color = (short)viewPort.Model.ReadMultiByteValue(start, 2);
      }

      protected override bool TryCopy(FieldArrayElementViewModel other) {
         if (!(other is ColorFieldArrayElementViewModel field)) return false;
         Set(ref color, field.color, nameof(Color));
         return true;
      }

      private void HandleColorChange(short oldValue) => Content = UncompressedPaletteColor.Convert(Color);
   }

   public class TextFieldStrategy : IFieldArrayElementViewModelStrategy {
      public static TextFieldStrategy Instance { get; } = new TextFieldStrategy();
      public ElementContentViewModelType Type => ElementContentViewModelType.TextField;

      public void UpdateModelFromViewModel(FieldArrayElementViewModel viewModel) {
         var textBytes = viewModel.Model.TextConverter.Convert(viewModel.Content, out var _);
         while (textBytes.Count < viewModel.Length) textBytes.Add(0x00);
         if (textBytes.Count > viewModel.Length) textBytes[viewModel.Length - 1] = 0xFF;
         for (int i = 0; i < viewModel.Length; i++) {
            viewModel.ViewPort.CurrentChange.ChangeData(viewModel.Model, viewModel.Start + i, textBytes[i]);
         }
      }

      public string UpdateViewModelFromModel(FieldArrayElementViewModel viewModel) {
         var text = viewModel.Model.TextConverter.Convert(viewModel.Model, viewModel.Start, viewModel.Length)?.Trim() ?? string.Empty;

         // take off quotes
         if (text.StartsWith("\"")) text = text.Substring(1);
         if (text.EndsWith("\"")) text = text.Substring(0, text.Length - 1);

         return text;
      }
   }

   public class NumericFieldStrategy : IFieldArrayElementViewModelStrategy {
      public static NumericFieldStrategy Instance { get; } = new NumericFieldStrategy();
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
      public static AddressFieldStratgy Instance { get; } = new AddressFieldStratgy();
      public ElementContentViewModelType Type => ElementContentViewModelType.Address;

      public void UpdateModelFromViewModel(FieldArrayElementViewModel viewModel) {
         var content = viewModel.Content.Trim();
         if (content.StartsWith(PointerRun.PointerStart.ToString())) content = content.Substring(1);
         if (content.EndsWith(PointerRun.PointerEnd.ToString())) content = content.Substring(0, content.Length - 1);

         var start = viewModel.Start;
         var change = viewModel.ViewPort.CurrentChange;
         var model = viewModel.Model;

         if (!int.TryParse(content, NumberStyles.HexNumber, CultureInfo.CurrentCulture.NumberFormat, out var address)) {
            address = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, content);
            if (address == Pointer.NULL && content != "null") {
               viewModel.ErrorText = "Address should be hexidecimal or an anchor.";
               return;
            }
         }

         var run = model.GetNextRun(start) as ITableRun;
         var offsets = run.ConvertByteOffsetToArrayOffset(start);
         var segment = run.ElementContent[offsets.SegmentIndex];
         if (segment is ArrayRunPointerSegment pointerSegment) {
            if (!pointerSegment.DestinationDataMatchesPointerFormat(model, change, offsets.SegmentStart, address, run.ElementContent, -1)) {
               viewModel.ErrorText = $"This pointer must point to {pointerSegment.InnerFormat} data.";
            }
         }

         model.UpdateArrayPointer(change, segment, run.ElementContent, offsets.ElementIndex, start, address);
      }

      public string UpdateViewModelFromModel(FieldArrayElementViewModel viewModel) {
         var value = viewModel.Model.ReadPointer(viewModel.Start);
         var text = value.ToString("X6");
         if (value == Pointer.NULL) text = "null";
         return $"{PointerRun.PointerStart}{text}{PointerRun.PointerEnd}";
      }
   }

   public class HexFieldStratgy : IFieldArrayElementViewModelStrategy {
      public static HexFieldStratgy Instance { get; } = new HexFieldStratgy();
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

   public class ColorFieldStrategy : IFieldArrayElementViewModelStrategy {
      public static ColorFieldStrategy Instance { get; } = new ColorFieldStrategy();
      public ElementContentViewModelType Type => ElementContentViewModelType.ColorField;

      public void UpdateModelFromViewModel(FieldArrayElementViewModel viewModel) {
         var colors = PaletteCollection.ParseColor(viewModel.Content);
         if (colors.Count == 0) return;
         viewModel.Model.WriteMultiByteValue(viewModel.Start, viewModel.Length, viewModel.ViewPort.CurrentChange, colors[0]);
      }

      public string UpdateViewModelFromModel(FieldArrayElementViewModel viewModel) {
         var color = (short)viewModel.Model.ReadMultiByteValue(viewModel.Start, viewModel.Length);
         var text = UncompressedPaletteColor.Convert(color);
         return text;
      }
   }
}
