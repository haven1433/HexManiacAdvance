using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   /// <summary>
   /// This exists to wrap a string, just so that WPF doesn't mess up the combo-box selection in the case of multiple indexes having the same text.
   /// </summary>
   public class ComboOption : INotifyPropertyChanged {
      event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged { add { } remove { } }
      public virtual bool DisplayAsText => true;
      public string Text { get; }

      public int Index { get; }

      public ComboOption(string text, int index) { Text = text; Index = index; }

      public override string ToString() => Text;
   }

   public class VisualComboOption : ComboOption, IPixelViewModel {
      public short Transparent => -1;
      public int PixelWidth { get; private set; }
      public int PixelHeight { get; private set; }
      public short[] PixelData { get; private set; }
      public double SpriteScale => 1;
      public override bool DisplayAsText => false;

      private VisualComboOption(string text, int index) : base(text, index) { PixelData = new short[0]; }
      public static ComboOption CreateFromSprite(string text, short[] pixelData, int width, int index) => new VisualComboOption(text, index) {
         PixelData = pixelData,
         PixelWidth = width,
         PixelHeight = pixelData.Length / width,
      };
   }

   public class ComboBoxArrayElementViewModel : ViewModelCore, IArrayElementViewModel {
      private string name;
      private int start, length;

      private EventHandler dataChanged, dataSelected;
      public event EventHandler DataChanged { add => dataChanged += value; remove => dataChanged -= value; }

      public event EventHandler DataSelected { add => dataSelected += value; remove => dataSelected -= value; }

      private int recursionCheck;
      private bool isFiltering;
      public bool IsFiltering {
         get => isFiltering;
         set => Set(ref isFiltering, value, wasFiltering => {
            if (wasFiltering) SelectedIndex = 0; // reset selection
         });
      }

      public bool CanFilter => fullOptions[0].DisplayAsText;
      private string filterText;
      public string FilterText {
         get => filterText;
         set => Set(ref filterText, value, FilterTextChanged);
      }
      private void FilterTextChanged(string oldValue) {
         if (recursionCheck != 0 || !isFiltering) return;
         recursionCheck++;
         Options = new(fullOptions.Where(option => option.Text.MatchesPartial(filterText)));
         if (selectedIndex >= 0 && selectedIndex < fullOptions.Count && Options.Contains(fullOptions[selectedIndex])) {
            // selected index is already fine
         } else if (Options.Count > 0) {
            // based on typing filter text, we can change the selection
            selectedIndex = 0;
            var options = Options;
            SelectionChanged();
            Options = options;
         }
         NotifyPropertyChanged(nameof(Options));
         recursionCheck--;
      }

      public void ConfirmSelection() {
         SelectedIndex = 0;
      }

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

      public int ZIndex => 0;

      private bool containsUniqueOption;
      private List<ComboOption> fullOptions;
      public ObservableCollection<ComboOption> Options { get; private set; }

      private int selectedIndex;

      public int SelectedIndex {
         get => selectedIndex;
         set {
            if (recursionCheck != 0) return;
            recursionCheck++;
            using var _ = new StubDisposable { Dispose = () => recursionCheck-- };
            IsFiltering = false;
            if (Options.Count > value && value >= 0) {
               var selectedOption = Options[value];
               value = fullOptions.FindIndex(option => option.Index == selectedOption.Index);
               FilterText = selectedOption.Text;
            } else if (value >= 0) {
               FilterText = fullOptions[value].Text;
            }
            if (Options.Count != fullOptions.Count) {
               Options = new(fullOptions);
               NotifyPropertyChanged(nameof(Options));
            }

            Set(ref selectedIndex, value, prev => SelectionChanged());
         }
      }

      private void SelectionChanged() {
         using (ModelCacheScope.CreateScope(ViewPort.Model)) {
            var run = (ITableRun)ViewPort.Model.GetNextRun(Start);
            var offsets = run.ConvertByteOffsetToArrayOffset(Start);
            var rawSegment = run.ElementContent[offsets.SegmentIndex];
            if (rawSegment is ArrayRunRecordSegment recordSegment) rawSegment = recordSegment.CreateConcrete(ViewPort.Model, Start);
            var segment = (ArrayRunEnumSegment)rawSegment;

            // special case: the last option might be a weird value that came in, not normally available in the enum
            if (containsUniqueOption && selectedIndex == fullOptions.Count - 1 && int.TryParse(fullOptions[selectedIndex].Text, out var parsedValue)) {
               selectedIndex = parsedValue - segment.ValueOffset;
            }

            ViewPort.Model.WriteMultiByteValue(Start, Length, ViewPort.ChangeHistory.CurrentChange, fullOptions[selectedIndex].Index + segment.ValueOffset);
            var info = run.NotifyChildren(ViewPort.Model, ViewPort.ChangeHistory.CurrentChange, offsets.ElementIndex, offsets.SegmentIndex);
            if (info.HasError && info.IsWarning) ViewPort.RaiseMessage(info.ErrorMessage);
            else if (info.HasError) ViewPort.RaiseError(info.ErrorMessage);
            dataChanged?.Invoke(this, EventArgs.Empty);
         }
      }

      private bool visible = true;
      public bool Visible { get => visible; set => Set(ref visible, value); }

      public ComboBoxArrayElementViewModel(ViewPort viewPort, Selection selection, string name, int start, int length) {
         fullOptions = new List<ComboOption>();
         (ViewPort, Name, Start, Length) = (viewPort, name, start, length);
         var run = (ITableRun)ViewPort.Model.GetNextRun(Start);
         TableName = viewPort.Model.GetAnchorFromAddress(-1, run.Start);
         var offsets = run.ConvertByteOffsetToArrayOffset(start);
         var rawSegment = run.ElementContent[offsets.SegmentIndex];
         if (rawSegment is ArrayRunRecordSegment recordSegment) rawSegment = recordSegment.CreateConcrete(viewPort.Model, start);
         var segment = rawSegment as ArrayRunEnumSegment;
         var optionSource = new Lazy<int>(() => Pointer.NULL);
         Debug.Assert(segment != null);
         if (segment != null) {
            optionSource = new Lazy<int>(() => CalculateOptionSource(segment.EnumName));
            fullOptions = new List<ComboOption>(segment.GetComboOptions(ViewPort.Model));
         } else {
            fullOptions = new List<ComboOption>();
         }
         var modelValue = ViewPort.Model.ReadMultiByteValue(start, length) - segment.ValueOffset;
         if (fullOptions.All(option => option.Index != modelValue)) {
            var newOption = new ComboOption(modelValue.ToString(), modelValue);
            var insertIndex = fullOptions.Where(option => option.Index < newOption.Index).Count();
            fullOptions.Insert(insertIndex, newOption);
            selectedIndex = insertIndex;
            containsUniqueOption = true;
         } else {
            selectedIndex = fullOptions.FindIndex(option => option.Index == modelValue);
         }
         filterText = selectedIndex >= 0 && selectedIndex < fullOptions.Count ? fullOptions[selectedIndex].Text : string.Empty;
         Options = new(fullOptions);
         GotoSource = new StubCommand {
            CanExecute = arg => optionSource.Value != Pointer.NULL,
            Execute = arg => {
               var indexSource = (viewPort.Model.GetNextRun(optionSource.Value) is ITableRun optionSourceTable) ?
                  optionSourceTable.Start + optionSourceTable.ElementLength * selectedIndex :
                  optionSource.Value;
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
         FilterText = comboBox.filterText;

         // only update the options if they're different
         if (!fullOptions.Select(option => option.Text).SequenceEqual(comboBox.fullOptions.Select(option => option.Text))) {
            selectedIndex = -1; // changing options will make the UIElement update the SelectedIndex automatically. Set it first so that we don't cause a data change.
            fullOptions = comboBox.fullOptions;
            if (Options.Count == comboBox.Options.Count) {
               for (int i = 0; i < Options.Count; i++) {
                  if (Options[i].DisplayAsText && Options[i].Text == comboBox.Options[i].Text && Options[i].Index == comboBox.Options[i].Index) {
                     // all good, don't need to copy
                  } else {
                     Options[i] = comboBox.Options[i];
                  }
               }
            } else {
               Options = comboBox.Options;
               NotifyPropertyChanged(nameof(Options));
            }
            NotifyPropertyChanged(nameof(CanFilter));
         }

         containsUniqueOption = comboBox.containsUniqueOption;
         TryUpdate(ref selectedIndex, comboBox.SelectedIndex, nameof(SelectedIndex));
         FilterText = comboBox.filterText;
         ErrorText = comboBox.ErrorText;
         GotoSource = comboBox.GotoSource;
         NotifyPropertyChanged(nameof(GotoSource));
         dataChanged = comboBox.dataChanged;
         dataSelected = comboBox.dataSelected;

         return true;
      }

      public void Focus() => dataSelected?.Invoke(this, EventArgs.Empty);

      private int CalculateOptionSource(string name) {
         var model = ViewPort.Model;
         var initialGuess = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, name);
         if (initialGuess != Pointer.NULL) return initialGuess;
         // maybe it's a list?
         if (!model.TryGetList(name, out var list)) return Pointer.NULL;
         // look for tables with length based on that list
         var arrays = model.Arrays.Where(array => array.LengthFromAnchor == name).ToList();
         if (arrays.Count != 1) return Pointer.NULL;

         // there's a single array 
         return arrays[0].Start;
      }
   }
}
