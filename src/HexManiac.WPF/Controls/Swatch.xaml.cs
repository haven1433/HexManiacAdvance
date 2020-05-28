using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace HavenSoft.HexManiac.WPF.Controls {
   public partial class Swatch : UserControl {

      private (double hue, double sat, double bright) hsb;

      #region Result

      public static readonly DependencyProperty ResultProperty = DependencyProperty.Register("Result", typeof(string), typeof(Swatch), new FrameworkPropertyMetadata("#000000", ResultPropertyChanged));

      public string Result {
         get => (string)GetValue(ResultProperty);
         set => SetValue(ResultProperty, value);
      }

      private static void ResultPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
         var self = (Swatch)d;
         self.OnResultPropertyChanged(e);
      }

      private void OnResultPropertyChanged(DependencyPropertyChangedEventArgs e) {
         if (!Theme.TryConvertColor(Result, out var rgb)) return;
         hsb = Theme.ToHSB(rgb.r, rgb.g, rgb.b);
         UpdateSBPickerHue();
         UpdateSelections();
      }

      #endregion

      public Swatch() {
         InitializeComponent();
         var stops = new GradientStopCollection();
         for (int i = 0; i < 100; i++) {
            var (red, green, blue) = Theme.FromHSB(i / 100.0, 1, 1);
            var color = Color.FromRgb(red, green, blue);
            var stop = new GradientStop(color, i / 100.0);
            stops.Add(stop);
         }
         var brush = new LinearGradientBrush(stops, 90);
         HuePicker.Background = brush;
      }

      protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo) {
         base.OnRenderSizeChanged(sizeInfo);
         UpdateSelections();
      }

      private void UpdateSBPickerHue() {
         var (red, green, blue) = Theme.FromHSB(hsb.hue, 1, 1);
         var color = Color.FromRgb(red, green, blue);
         SBPickerContainer.Background = new SolidColorBrush(color);
      }

      private void SBPickerDown(object sender, MouseEventArgs e) {
         if (e.LeftButton == MouseButtonState.Released) return;
         e.Handled = true;
         SBPicker.CaptureMouse();
         var hueBackup = hsb.hue;
         var position = e.GetPosition(SBPicker);
         var saturation = position.X / SBPicker.ActualWidth;
         var brightness = 1 - position.Y / SBPicker.ActualHeight;
         hsb.sat = saturation.LimitToRange(0, 1);
         hsb.bright = brightness.LimitToRange(0, 1);
         UpdateSelections();
         Result = Theme.FromHSB(hsb.hue, hsb.sat, hsb.bright).ToHexString();
         hsb.hue = hueBackup;
         UpdateSBPickerHue();
         UpdateSelections();
      }

      private void HuePickerDown(object sender, MouseEventArgs e) {
         if (e.LeftButton == MouseButtonState.Released) return;
         e.Handled = true;
         HuePicker.CaptureMouse();
         var position = e.GetPosition(HuePicker);
         var hue = position.Y / HuePicker.ActualHeight;
         while (hue > 1) hue -= 1;
         while (hue < 0) hue += 1;
         hsb.hue = hue;
         UpdateSelections();
         Result = Theme.FromHSB(hsb.hue, hsb.sat, hsb.bright).ToHexString();
         UpdateSBPickerHue();
      }

      private void UpdateSelections() {
         SBSelector.X = hsb.sat * SBPicker.ActualWidth;
         SBSelector.Y = (1 - hsb.bright) * SBPicker.ActualHeight;
         HueSelector.Y = hsb.hue * HuePicker.ActualHeight;
      }

      private void PickerUp(object sender, MouseButtonEventArgs e) {
         var element = (FrameworkElement)sender;
         element.ReleaseMouseCapture();
      }
   }
}
