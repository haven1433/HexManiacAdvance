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
      private string name;
      private int start, length;

      private EventHandler dataChanged;
      public event EventHandler DataChanged { add => dataChanged += value; remove => dataChanged -= value; }

      public ViewPort ViewPort { get; }
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
            using (ModelCacheScope.CreateScope(ViewPort.Model)) {
               if (!TryUpdate(ref selectedIndex, value)) return;
               var run = (ITableRun)ViewPort.Model.GetNextRun(Start);
               var offsets = run.ConvertByteOffsetToArrayOffset(Start);
               var segment = (ArrayRunEnumSegment)run.ElementContent[offsets.SegmentIndex];

               // special case: the last option might be a weird value that came in, not normally available in the enum
               if (containsUniqueOption && selectedIndex == Options.Count - 1 && int.TryParse(Options[selectedIndex].Text, out var parsedValue)) {
                  value = parsedValue;
               }

               ViewPort.Model.WriteMultiByteValue(Start, Length, ViewPort.ChangeHistory.CurrentChange, value);
               var info = run.NotifyChildren(ViewPort.Model, ViewPort.ChangeHistory.CurrentChange, offsets.ElementIndex, offsets.SegmentIndex);
               if (info.HasError && info.IsWarning) ViewPort.RaiseMessage(info.ErrorMessage);
               else if (info.HasError) ViewPort.RaiseError(info.ErrorMessage);
               dataChanged?.Invoke(this, EventArgs.Empty);
            }
         }
      }

      private bool visible = true;
      public bool Visible { get => visible; set => Set(ref visible, value); }

      public ComboBoxArrayElementViewModel(ViewPort viewPort, Selection selection, string name, int start, int length) {
         (ViewPort, Name, Start, Length) = (viewPort, name, start, length);
         var run = (ITableRun)ViewPort.Model.GetNextRun(Start);
         TableName = viewPort.Model.GetAnchorFromAddress(-1, run.Start);
         var offsets = run.ConvertByteOffsetToArrayOffset(start);
         var segment = run.ElementContent[offsets.SegmentIndex] as ArrayRunEnumSegment;
         int optionSource = Pointer.NULL;
         Debug.Assert(segment != null);
         if (segment != null) {
            optionSource = ViewPort.Model.GetAddressFromAnchor(ViewPort.ChangeHistory.CurrentChange, -1, segment.EnumName);
            Options = new List<ComboOption>(segment.GetComboOptions(ViewPort.Model));
         } else {
            Options = new List<ComboOption>();
         }
         var modelValue = ViewPort.Model.ReadMultiByteValue(start, length);
         if (modelValue >= Options.Count) {
            Options.Add(modelValue.ToString());
            selectedIndex = Options.Count - 1;
            containsUniqueOption = true;
         } else {
            selectedIndex = ViewPort.Model.ReadMultiByteValue(start, length);
         }
         GotoSource = new StubCommand {
            CanExecute = arg => optionSource != Pointer.NULL,
            Execute = arg => {
               var indexSource = (viewPort.Model.GetNextRun(optionSource) is ITableRun optionSourceTable) ?
                  optionSourceTable.Start + optionSourceTable.ElementLength * selectedIndex :
                  optionSource;
               selection.GotoAddress(indexSource);
            },
         };
      }

      public bool TryCopy(IArrayElementViewModel other) {
         if (!(other is ComboBoxArrayElementViewModel comboBox)) return false;
         Name = comboBox.Name;
         TableName = comboBox.TableName;
         Length = comboBox.Length;
         Start = comboBox.Start;
         Visible = other.Visible;

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
