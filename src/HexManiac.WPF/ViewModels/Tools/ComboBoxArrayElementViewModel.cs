using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Images;
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
   public class ComboOption : INotifyPropertyChanged {
      event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged { add { } remove { } }
      public virtual bool DisplayAsText => true;
      public string Text { get; }

      public int Index { get; }

      public ComboOption(string text, int index) { Text = text; Index = index; }

      public override string ToString() => Text;

      public static IEnumerable<ComboOption> Convert(IEnumerable<string> options) {
         int count = 0;
         foreach (var option in options) {
            yield return new ComboOption(option, count);
            count++;
         }
      }
   }

   public class VisualComboOption : ComboOption, IPixelViewModel {
      public short Transparent => -1;
      public int PixelWidth { get; private set; }
      public int PixelHeight { get; private set; }
      public short[] PixelData { get; private set; }
      public double SpriteScale { get; init; } = 1;
      public override bool DisplayAsText => false;
      public bool DisplayIndex { get; private set; }

      private VisualComboOption(string text, int index) : base(text, index) { PixelData = new short[0]; }
      public static VisualComboOption CreateFromSprite(string text, short[] pixelData, int width, int index, double scale = 1, bool displayIndex = false) => new VisualComboOption(text, index) {
         PixelData = pixelData,
         PixelWidth = width,
         PixelHeight = pixelData.Length / width,
         DisplayIndex = displayIndex,
         SpriteScale = scale
      };
   }

   public class ComboBoxArrayElementViewModel : ViewModelCore, IMultiEnabledArrayElementViewModel {
      private string name, enumName;
      private int start, length;

      private EventHandler dataChanged, dataSelected;
      public event EventHandler DataChanged { add => dataChanged += value; remove => dataChanged -= value; }

      public event EventHandler DataSelected { add => dataSelected += value; remove => dataSelected -= value; }

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

      private string theme; public string Theme { get => theme; set => Set(ref theme, value); }
      public bool IsInError => errorText != string.Empty;

      string errorText;
      public string ErrorText {
         get => errorText;
         set {
            if (TryUpdate(ref errorText, value)) NotifyPropertyChanged(nameof(IsInError));
         }
      }

      public int ZIndex => 0;

      private bool visible = true;
      public bool Visible { get => visible; set => Set(ref visible, value); }

      public FilteringComboOptions FilteringComboOptions { get; } = new();

      public ComboBoxArrayElementViewModel(ViewPort viewPort, Selection selection, string name, int start, int length) {
         var fullOptions = new List<ComboOption>();
         AddSilentChild(FilteringComboOptions);
         int selectedIndex = 0;
         (ViewPort, Name, Start, Length) = (viewPort, name, start, length);
         var run = (ITableRun)ViewPort.Model.GetNextRun(Start);
         TableName = viewPort.Model.GetAnchorFromAddress(-1, run.Start);
         var offsets = run.ConvertByteOffsetToArrayOffset(start);
         var rawSegment = run.ElementContent[offsets.SegmentIndex];
         if (rawSegment is ArrayRunRecordSegment recordSegment) rawSegment = recordSegment.CreateConcrete(viewPort.Model, start);
         var segment = rawSegment as ArrayRunEnumSegment;
         enumName = segment.EnumName;
         var optionSource = new Lazy<int>(() => Pointer.NULL);
         Debug.Assert(segment != null);
         if (segment != null) {
            optionSource = new Lazy<int>(() => CalculateOptionSource(enumName));
            fullOptions = new List<ComboOption>(segment.GetComboOptions(ViewPort.Model));
         } else {
            fullOptions = new List<ComboOption>();
         }
         var modelValue = ViewPort.Model.ReadMultiByteValue(start, length) - segment.ValueOffset;
         if (fullOptions.All(option => option.Index != modelValue)) {
            var newOption = new ComboOption(modelValue.ToString(), modelValue - segment.ValueOffset);
            var insertIndex = fullOptions.Where(option => option.Index < newOption.Index).Count();
            fullOptions.Insert(insertIndex, newOption);
            selectedIndex = insertIndex;
         } else {
            selectedIndex = fullOptions.FindIndex(option => option.Index == modelValue);
         }

         FilteringComboOptions.Update(fullOptions, selectedIndex);
         GotoSource = new StubCommand {
            CanExecute = arg => optionSource.Value != Pointer.NULL,
            Execute = arg => {
               var modelValue = ViewPort.Model.ReadMultiByteValue(Start, Length) - segment.ValueOffset;
               var indexSource = (viewPort.Model.GetNextRun(optionSource.Value) is ITableRun optionSourceTable && modelValue < optionSourceTable.ElementCount) ?
                  optionSourceTable.Start + optionSourceTable.ElementLength * modelValue :
                  optionSource.Value;
               selection.GotoAddress(indexSource);
            },
         };

         FilteringComboOptions.Bind(nameof(FilteringComboOptions.ModelValue), (options, args) => {
            if (copying) return;
            var run = (ITableRun)ViewPort.Model.GetNextRun(Start);
            var offsets = run.ConvertByteOffsetToArrayOffset(Start);
            var rawSegment = run.ElementContent[offsets.SegmentIndex];
            if (rawSegment is ArrayRunRecordSegment recordSegment) rawSegment = recordSegment.CreateConcrete(ViewPort.Model, Start);
            var segment = (ArrayRunEnumSegment)rawSegment;

            ViewPort.Model.WriteMultiByteValue(Start, Length, ViewPort.ChangeHistory.CurrentChange, options.ModelValue + segment.ValueOffset);
            var info = run.NotifyChildren(ViewPort.Model, ViewPort.ChangeHistory.CurrentChange, offsets.ElementIndex, offsets.SegmentIndex);
            if (info.HasError && info.IsWarning) ViewPort.RaiseMessage(info.ErrorMessage);
            else if (info.HasError) ViewPort.RaiseError(info.ErrorMessage);
            dataChanged?.Invoke(this, EventArgs.Empty);
         });
      }

      private bool copying = false;
      public bool TryCopy(IArrayElementViewModel other) {
         if (!(other is ComboBoxArrayElementViewModel comboBox)) return false;
         if (comboBox.FilteringComboOptions.AllOptions?.FirstOrDefault()?.DisplayAsText != FilteringComboOptions.AllOptions?.FirstOrDefault()?.DisplayAsText) {
            return false;
         }
         Name = comboBox.Name;
         TableName = comboBox.TableName;
         Length = comboBox.Length;
         Start = comboBox.Start;
         Visible = comboBox.Visible;
         using (Scope(ref copying, true, old => copying = old)) {
            FilteringComboOptions.Update(comboBox.FilteringComboOptions.AllOptions, comboBox.FilteringComboOptions.SelectedIndex);
         }

         ErrorText = comboBox.ErrorText;
         if (enumName != comboBox.enumName) {
            enumName = comboBox.enumName;
            GotoSource = comboBox.GotoSource;
            NotifyPropertyChanged(nameof(GotoSource));
         }
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

      // legacy pass-through members for tests
      public string FilterText { get => FilteringComboOptions.DisplayText; set => FilteringComboOptions.DisplayText = value; }
      public IList<ComboOption> Options => FilteringComboOptions.FilteredOptions;
      public int SelectedIndex { get => FilteringComboOptions.SelectedIndex; set => FilteringComboOptions.SelectedIndex = value; }
   }
}
