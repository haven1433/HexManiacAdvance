using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;


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
      public int PixelWidth { get; set; }
      public int PixelHeight { get; set; }
      public short[] PixelData { get; set; }
      public double SpriteScale { get; init; } = 1;
      public override bool DisplayAsText => false;
      public bool DisplayIndex { get; set; }

      public VisualComboOption(string text, int index) : base(text, index) { PixelData = new short[0]; }
      public static VisualComboOption CreateFromSprite(string text, short[] pixelData, int width, int index, double scale = 1, bool displayIndex = false) => new VisualComboOption(text, index) {
         PixelData = pixelData,
         PixelWidth = width,
         PixelHeight = pixelData.Length / width,
         DisplayIndex = displayIndex,
         SpriteScale = scale
      };
   }

   public class ComboBoxArrayElementViewModel : ViewModelCore, IMultiEnabledArrayElementViewModel {
      public string name, enumName;
      public int start, length;

      public EventHandler dataChanged, dataSelected;
      public event EventHandler DataChanged { add => dataChanged += value; remove => dataChanged -= value; }

      public event EventHandler DataSelected { add => dataSelected += value; remove => dataSelected -= value; }

      public string TableName { get; set; }
      public string Name { get => name; set => TryUpdate(ref name, value); }
      public int Start {
         get => start; set {
            if (!TryUpdate(ref start, value)) return;
         }
      }
      public int Length { get => length; set => TryUpdate(ref length, value); }

      public ElementContentViewModelType Type => ElementContentViewModelType.ComboBox;

      public string theme; public string Theme { get => theme; set => Set(ref theme, value); }
      public bool IsInError => errorText != string.Empty;

      string errorText;
      public string ErrorText {
         get => errorText;
         set {
            if (TryUpdate(ref errorText, value)) NotifyPropertyChanged(nameof(IsInError));
         }
      }

      public int ZIndex => 0;

      public bool visible = true;
      public bool Visible { get => visible; set => Set(ref visible, value); }

      public FilteringComboOptions FilteringComboOptions { get; } = new();

      public bool copying = false;
      public void Focus() => dataSelected?.Invoke(this, EventArgs.Empty);

      // legacy pass-through members for tests
      public string FilterText { get => FilteringComboOptions.DisplayText; set => FilteringComboOptions.DisplayText = value; }
      public IList<ComboOption> Options => FilteringComboOptions.FilteredOptions;
      public int SelectedIndex { get => FilteringComboOptions.SelectedIndex; set => FilteringComboOptions.SelectedIndex = value; }
   }
}
