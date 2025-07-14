using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


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
      string UpdateViewModelFromModel(FieldArrayElementViewModel viewModel);
   }

   public class FieldArrayElementViewModel : ViewModelCore, IMultiEnabledArrayElementViewModel {
      public readonly IFieldArrayElementViewModelStrategy strategy;

      public string name;
      public int start, length;

      public EventHandler dataChanged;
      public event EventHandler DataChanged { add => dataChanged += value; remove => dataChanged -= value; }

      public EventHandler dataSelected;
      public event EventHandler DataSelected { add => dataSelected += value; remove => dataSelected -= value; }

      public IDataModel Model { get; }
      public string Name { get => name; set => TryUpdate(ref name, value); }
      public int Start { get => start; set => TryUpdate(ref start, value); }
      public int Length { get => length; set => TryUpdate(ref length, value); }

      public bool visible = true;
      public bool Visible { get => visible; set => Set(ref visible, value); }

      public ElementContentViewModelType Type => strategy.Type;

      public string theme; public string Theme { get => theme; set => Set(ref theme, value); }
      public bool IsInError => errorText != string.Empty;

      public string errorText;
      public string ErrorText {
         get => errorText;
         set {
            if (TryUpdate(ref errorText, value)) NotifyPropertyChanged(nameof(IsInError));
         }
      }

      public int ZIndex => 0;

      public bool protectContentChange;
      public string content;

      protected virtual bool TryCopy(FieldArrayElementViewModel other) => true;

      public event EventHandler CanAcceptChanged;

      public void Focus() => dataSelected?.Invoke(this, EventArgs.Empty);

   }

   public class ColorFieldArrayElementViewModel : FieldArrayElementViewModel {
      public short color;
   }

   public class TextFieldStrategy : IFieldArrayElementViewModelStrategy {
      public static TextFieldStrategy Instance { get; } = new TextFieldStrategy();
      public ElementContentViewModelType Type => ElementContentViewModelType.TextField;

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

      public string UpdateViewModelFromModel(FieldArrayElementViewModel viewModel) {
         int number = viewModel.Model.ReadMultiByteValue(viewModel.Start, viewModel.Length);

         return number.ToString();
      }
   }

   public class SignedFieldStrategy : IFieldArrayElementViewModelStrategy {
      public static SignedFieldStrategy Instance { get; } = new SignedFieldStrategy();
      public ElementContentViewModelType Type => ElementContentViewModelType.NumericField;

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

      public string UpdateViewModelFromModel(FieldArrayElementViewModel viewModel) {
         int number = viewModel.Model.ReadMultiByteValue(viewModel.Start, viewModel.Length);
         var text = number.ToString($"X{viewModel.Length * 2}");
         return "0x" + text;
      }
   }

   public class ColorFieldStrategy : IFieldArrayElementViewModelStrategy {
      public static ColorFieldStrategy Instance { get; } = new ColorFieldStrategy();
      public ElementContentViewModelType Type => ElementContentViewModelType.ColorField;

      public string UpdateViewModelFromModel(FieldArrayElementViewModel viewModel) {
         var color = (short)viewModel.Model.ReadMultiByteValue(viewModel.Start, viewModel.Length);
         var text = UncompressedPaletteColor.Convert(color);
         return text;
      }
   }

   public class MultiFieldArrayElementViewModel : ViewModelCore, IArrayElementViewModel {
      public string theme, content;
      public bool visible = true;
      public List<IMultiEnabledArrayElementViewModel> fields = new();

      public string Theme { get => theme; set => Set(ref theme, value); }
      public bool Visible { get => visible; set => Set(ref visible, value); }
      public bool IsInError => false;
      public string ErrorText => string.Empty;
      public int ZIndex => 0;

      public event EventHandler DataChanged;
      public event EventHandler DataSelected;

      // consider hiding fields that don't match the filter
      public bool Filter(string filter) {
         foreach (var element in fields) {
            if (element.Name.MatchesPartial(filter)) return true;
         }
         return false;
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
