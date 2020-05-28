using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   /// <summary>
   /// This exists to wrap a string, just so that WPF doesn't mess up the combo-box selection in the case of multiple indexes having the same text.
   /// </summary>
   public class ComboOption : IPixelViewModel {
      event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged { add { } remove { } }
      public bool DisplayAsText => Text != null;
      public string Text { get; }

      public int PixelWidth { get; private set; }
      public int PixelHeight { get; private set; }
      public short[] PixelData { get; private set; }
      public double SpriteScale => 1;

      public ComboOption(string text) { Text = text; PixelData = new short[0]; }
      public static implicit operator ComboOption(string text) => new ComboOption(text);
      public static ComboOption CreateFromSprite(short[] pixelData, int width) => new ComboOption(null) {
         PixelData = pixelData,
         PixelWidth = width,
         PixelHeight = pixelData.Length / width,
      };
   }

   public class ComboBoxArrayElementViewModel : ViewModelCore, IArrayElementViewModel {
      private readonly ChangeHistory<ModelDelta> history;

      private string name;
      private int start, length;

      private EventHandler dataChanged;
      public event EventHandler DataChanged { add => dataChanged += value; remove => dataChanged -= value; }

      public IDataModel Model { get; }
      public string TableName { get; private set; }
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
         (this.history, Model, Name, Start, Length) = (history, model, name, start, length);
         var run = (ITableRun)Model.GetNextRun(Start);
         TableName = model.GetAnchorFromAddress(-1, run.Start);
         var offsets = run.ConvertByteOffsetToArrayOffset(start);
         var segment = run.ElementContent[offsets.SegmentIndex] as ArrayRunEnumSegment;
         int optionSource = Pointer.NULL;
         Debug.Assert(segment != null);
         if (segment != null) {
            optionSource = Model.GetAddressFromAnchor(history.CurrentChange, -1, segment.EnumName);
            Options = new List<ComboOption>(segment.GetComboOptions(Model));
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
         TableName = comboBox.TableName;
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
}
