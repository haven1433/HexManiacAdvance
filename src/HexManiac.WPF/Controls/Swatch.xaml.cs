using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.ViewModels;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HavenSoft.HexManiac.WPF.Controls {
   public partial class Swatch : UserControl {

      private (double hue, double sat, double bright) hsb;

      #region Result

      public static readonly DependencyProperty ResultProperty = DependencyProperty.Register("Result", typeof(string), typeof(Swatch), new FrameworkPropertyMetadata("#000000", ResultPropertyChanged));

      public string Result {
         get => (string)GetValue(ResultProperty);
         set => SetValue(ResultProperty, value);
      }

      public event EventHandler<string> ResultChanged;

      private static void ResultPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
         var self = (Swatch)d;
         self.OnResultPropertyChanged(e);
      }

      private void OnResultPropertyChanged(DependencyPropertyChangedEventArgs e) {
         if (!Theme.TryConvertColor(Result, out var rgb)) return;
         hsb = Theme.ToHSB(rgb.r, rgb.g, rgb.b);
         UpdateSBPickerHue();
         UpdateSelections();
         ResultChanged?.Invoke(this, (string)e.OldValue);
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

   /// <summary>
   /// Lifted from https://www.codeproject.com/Articles/374887/Eye-Dropper-control-in-WPF
   /// </summary>
   public class DesktopColorPicker {
      public static Color GrabMousePixelColorFromScreen() {
         System.Drawing.Point point = System.Windows.Forms.Control.MousePosition;
         var screenimage = CaptureDesktop();
         int stride = (screenimage.PixelWidth * screenimage.Format.BitsPerPixel + 7) / 8;
         var pixels = new byte[4];
         point.X = point.X.LimitToRange(0, screenimage.PixelWidth - 1);
         point.Y = point.Y.LimitToRange(0, screenimage.PixelHeight - 1);
         screenimage.CopyPixels(new Int32Rect(point.X, point.Y, 1, 1), pixels, stride, 0);
         return Color.FromRgb(pixels[2], pixels[1], pixels[0]);
      }

      public static BitmapSource CaptureDesktop() {
         return CaptureRegion(
            GetDesktopWindow(),
            (int)SystemParameters.VirtualScreenLeft,
            (int)SystemParameters.VirtualScreenTop,
            (int)SystemParameters.PrimaryScreenWidth,
            (int)SystemParameters.PrimaryScreenHeight);
      }

      public const int SRCCOPY = 0xCC0020;

      [DllImport("user32.dll")]
      public static extern IntPtr GetDesktopWindow();

      // http://msdn.microsoft.com/en-us/library/dd144871(VS.85).aspx
      [DllImport("user32.dll")]
      public static extern IntPtr GetDC(IntPtr hwnd);

      // http://msdn.microsoft.com/en-us/library/dd183370(VS.85).aspx
      [DllImport("gdi32.dll")]
      [return: MarshalAs(UnmanagedType.Bool)]
      public static extern bool BitBlt(IntPtr hDestDC, int x, int y, int nWidth, int nHeight, IntPtr hSrcDC, int xSrc, int ySrc, Int32 dwRop);

      // http://msdn.microsoft.com/en-us/library/dd183488(VS.85).aspx
      [DllImport("gdi32.dll")]
      public static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

      // http://msdn.microsoft.com/en-us/library/dd183489(VS.85).aspx
      [DllImport("gdi32.dll", SetLastError = true)]
      public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

      // http://msdn.microsoft.com/en-us/library/dd162957(VS.85).aspx
      [DllImport("gdi32.dll", ExactSpelling = true, PreserveSig = true, SetLastError = true)]
      public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

      // http://msdn.microsoft.com/en-us/library/dd183539(VS.85).aspx
      [DllImport("gdi32.dll")]
      public static extern bool DeleteObject(IntPtr hObject);

      // http://msdn.microsoft.com/en-us/library/dd162920(VS.85).aspx
      [DllImport("user32.dll")]
      public static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);

      public static BitmapSource CaptureRegion(IntPtr hWnd, int x, int y, int width, int height) {
         IntPtr sourceDC = IntPtr.Zero;
         IntPtr targetDC = IntPtr.Zero;
         IntPtr compatibleBitmapHandle = IntPtr.Zero;
         BitmapSource bitmap = null;

         PresentationSource source = PresentationSource.FromVisual(Application.Current.MainWindow);
         if (source != null) {
            var dpiScale = source.CompositionTarget.TransformToDevice.M11;
            width = (int)(width * dpiScale);
            height = (int)(height * dpiScale);
         }

         try {
            // gets the main desktop and all open windows
            sourceDC = GetDC(GetDesktopWindow());

            //sourceDC = User32.GetDC(hWnd);
            targetDC = CreateCompatibleDC(sourceDC);

            // create a bitmap compatible with our target DC
            compatibleBitmapHandle = CreateCompatibleBitmap(sourceDC, width, height);

            // gets the bitmap into the target device context
            SelectObject(targetDC, compatibleBitmapHandle);

            // copy from source to destination
            BitBlt(targetDC, 0, 0, width, height, sourceDC, x, y, SRCCOPY);

            // Here's the WPF glue to make it all work. It converts from an
            // hBitmap to a BitmapSource. Love the WPF interop functions
            bitmap = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                compatibleBitmapHandle, IntPtr.Zero, Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

         } catch (Exception) {

         } finally {
            DeleteObject(compatibleBitmapHandle);
            ReleaseDC(IntPtr.Zero, sourceDC);
            ReleaseDC(IntPtr.Zero, targetDC);
         }

         return bitmap;
      }
   }
}
