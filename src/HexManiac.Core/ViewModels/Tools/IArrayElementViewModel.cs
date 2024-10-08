using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

   public class FieldArrayElementViewModel : ViewModelCore, IMultiEnabledArrayElementViewModel {
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

      private string theme; public string Theme { get => theme; set => Set(ref theme, value); }
      public bool IsInError => errorText != string.Empty;

      private string errorText;
      public string ErrorText {
         get => errorText;
         set {
            if (TryUpdate(ref errorText, value)) NotifyPropertyChanged(nameof(IsInError));
         }
      }

      public int ZIndex => 0;

      bool protectContentChange;
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
                  // prevent blapping over 'content' if raising dataChanged causes a TryCopy
                  using (Scope(ref protectContentChange, true, old => protectContentChange = old))
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
         if (!protectContentChange) TryUpdate(ref content, field.Content, nameof(Content));
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

      public void ResetContent() => Content = strategy.UpdateViewModelFromModel(this);

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
         if (viewModel.Content.TryParseInt(out int content)) {
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

   public class SignedFieldStrategy : IFieldArrayElementViewModelStrategy {
      public static SignedFieldStrategy Instance { get; } = new SignedFieldStrategy();
      public ElementContentViewModelType Type => ElementContentViewModelType.NumericField;

      public void UpdateModelFromViewModel(FieldArrayElementViewModel viewModel) {
         if (viewModel.Content.TryParseInt(out int content)) {
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
         if (viewModel.Length == 1) number = (sbyte)number;
         if (viewModel.Length == 2) number = (short)number;

         return number.ToString();
      }
   }

   public class AddressFieldStrategy : IFieldArrayElementViewModelStrategy {
      public static AddressFieldStrategy Instance { get; } = new AddressFieldStrategy();
      public ElementContentViewModelType Type => ElementContentViewModelType.Address;

      public void UpdateModelFromViewModel(FieldArrayElementViewModel viewModel) {
         var content = viewModel.Content;

         var start = viewModel.Start;
         var change = viewModel.ViewPort.CurrentChange;
         var model = viewModel.Model;

         if (!TryParse(content, out int address)) {
            address = viewModel.Model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, content);
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
         return ConvertAddressToText(value);
      }

      public static bool TryParse(string content, out int value) {
         content = content.Trim(PointerRun.PointerStart, PointerRun.PointerEnd, ' ');

         if (!content.TryParseHex(out value)) {
            value = Pointer.NULL;
            if (content.ToLower() != "null") return false;
         }

         return true;
      }

      public static string ConvertAddressToText(int address) {
         var text = address.ToString("X6");
         if (address == Pointer.NULL) text = "null";
         return $"{PointerRun.PointerStart}{text}{PointerRun.PointerEnd}";
      }
   }

   public class HexFieldStrategy : IFieldArrayElementViewModelStrategy {
      public static HexFieldStrategy Instance { get; } = new HexFieldStrategy();
      public ElementContentViewModelType Type => ElementContentViewModelType.HexField;

      public void UpdateModelFromViewModel(FieldArrayElementViewModel viewModel) {
         if (viewModel.Content.TryParseInt(out int hexValue)) {
            viewModel.Model.WriteMultiByteValue(viewModel.Start, viewModel.Length, viewModel.ViewPort.CurrentChange, hexValue);
         } else {
            viewModel.ErrorText = "Value should be hexidecimal.";
         }
      }

      public string UpdateViewModelFromModel(FieldArrayElementViewModel viewModel) {
         int number = viewModel.Model.ReadMultiByteValue(viewModel.Start, viewModel.Length);
         var text = number.ToString($"X{viewModel.Length * 2}");
         return "0x" + text;
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

   public class MultiFieldArrayElementViewModel : ViewModelCore, IArrayElementViewModel {
      private string theme, content;
      private bool visible = true;
      private List<IMultiEnabledArrayElementViewModel> fields = new();

      public string Theme { get => theme; set => Set(ref theme, value); }
      public bool Visible { get => visible; set => Set(ref visible, value); }
      public bool IsInError => false;
      public string ErrorText => string.Empty;
      public int ZIndex => 0;

      public event EventHandler DataChanged;
      public event EventHandler DataSelected;

      public ICommand Undo { get; }
      public ICommand Redo { get; }

      public MultiFieldArrayElementViewModel(ViewPort port) => (Undo, Redo) = (port.Undo, port.Redo);

      public void Add(IMultiEnabledArrayElementViewModel field) {
         fields.Add(field);
         RecalculateBody();
      }

      // consider hiding fields that don't match the filter
      public bool Filter(string filter) {
         foreach (var element in fields) {
            if (element.Name.MatchesPartial(filter)) return true;
         }
         return false;
      }

      public string Content {
         get => content;
         set => Set(ref content, value, oldValue => {
            var lines = content.SplitLines();
            for (int i = 0; i < fields.Count; i++) {
               if (i >= lines.Length) break;
               var parts = lines[i].Split(':', 2);
               if (parts.Length == 1) continue;
               var content = parts[1].Trim();
               if (fields[i] is FieldArrayElementViewModel field) {
                  field.Content = content;
               } else if (fields[i] is ComboBoxArrayElementViewModel combo) {
                  combo.FilteringComboOptions.DisplayText = content;
                  combo.FilteringComboOptions.SelectConfirm();
               }
            }
            DataChanged?.Invoke(this, EventArgs.Empty);
         });
      }

      public bool TryCopy(IArrayElementViewModel other) {
         if (other is not MultiFieldArrayElementViewModel that) return false;
         return content == that.content && fields.Count == that.fields.Count && fields.Count.Range().All(i => fields[i].TryCopy(that.fields[i]));
      }

      private void RecalculateBody() {
         var longestName = fields.Select(f => f.Name.Length).Max();
         var content = new StringBuilder();
         bool first = true;
         foreach (var field in fields) {
            if (!first) content.AppendLine();
            else first = false;

            content.Append(field.Name.PadRight(longestName, ' '));
            content.Append(" : ");
            if (field is FieldArrayElementViewModel field1) {
               content.Append(field1.Content);
            } else if (field is ComboBoxArrayElementViewModel combo) {
               content.Append(combo.FilteringComboOptions.DisplayText);
            }
         }
         this.content = content.ToString();
         NotifyPropertyChanged(nameof(Content));
      }

      public IReadOnlyList<AutocompleteItem> GetAutoComplete(string line, int caretLineIndex, int caretCharacterIndex) {
         var parts = line.Split(':', 2);
         if (parts.Length != 2) return Array.Empty<AutocompleteItem>();
         var name = parts[0].Trim();
         var field = fields.FirstOrDefault(f => f.Name == name);
         if (field == null) return Array.Empty<AutocompleteItem>();

         // right now, the field is always something like free text, free number, or free color
         // no auto-complete makes sense
         // but this method is here as a stub in case we add combobox content into the MultiField.
         if (field is ComboBoxArrayElementViewModel combo) {
            var content = parts[1].Trim();
            combo.FilteringComboOptions.DisplayText = content;
            return combo.FilteringComboOptions.FilteredOptions
               .Select(option => new AutocompleteItem(option.Text, parts[0] + ": " + option.Text) { CharacterOffset = parts[0].Length })
               .ToArray();
         }

         return Array.Empty<AutocompleteItem>();
      }
   }
}
