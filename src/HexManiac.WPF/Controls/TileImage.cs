using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HavenSoft.HexManiac.WPF.Controls {
   public class TileImage : Image {

      private TileViewModel ViewModel => (TileViewModel)DataContext;

      public TileImage() {
         DataContextChanged += (sender, e) => UpdateDataContext(e);
         Stretch = Stretch.None;
      }

      private void UpdateDataContext(DependencyPropertyChangedEventArgs e) {
         var oldValue = e.OldValue as INotifyPropertyChanged;
         if (oldValue != null) oldValue.PropertyChanged -= HandleDataContextPropertyChanged;
         var newValue = e.NewValue as INotifyPropertyChanged;
         if (newValue != null) newValue.PropertyChanged += HandleDataContextPropertyChanged;
         UpdateSource();
      }

      private void HandleDataContextPropertyChanged(object sender, PropertyChangedEventArgs e) => UpdateSource();

      public void UpdateSource() {
         var bitsPerPixel = 4; // TODO
         var pixels = new byte[8 * bitsPerPixel];
         Array.Copy(ViewModel.DataStore, ViewModel.Start, pixels, 0, pixels.Length);
         var palette = CreatePalette();
         int stride = 4; // Width(8) * bytesPerPixel(.5)
         var format = PixelFormats.Indexed4; // 16 possible colors
         var source = BitmapSource.Create(8, 8, 96, 96, format, palette, pixels, stride);
         Source = source;
      }

      public BitmapPalette CreatePalette() {
         var colors = ViewModel.Palette.Select(Convert16BitColor).ToList();
         return new BitmapPalette(colors);
      }

      public static Color Convert16BitColor(short color) {
         byte b = (byte)((color >> 0) & 0b11111);
         byte g = (byte)((color >> 5) & 0b11111);
         byte r = (byte)((color >> 10) & 0b11111);

         return Color.FromArgb(255, ScaleUp(r), ScaleUp(g), ScaleUp(b));
      }

      public static byte ScaleUp(byte channel) => (byte)((channel * 255) / 31);

      public static short Convert16BitColor(Color color) {
         byte r = (byte)(color.R >> 3);
         byte g = (byte)(color.G >> 3);
         byte b = (byte)(color.B >> 3);

         return (short)((r << 10) | (g << 5) | b);
      }
   }

   public class PaletteColorConverter : IValueConverter {
      public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
         var color = (short)value;
         var wpfColor = TileImage.Convert16BitColor(color);
         return new SolidColorBrush(wpfColor);
      }

      public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
         throw new NotImplementedException();
      }
   }

   public class TilePaletteHueConverter : IValueConverter {
      private static readonly Color[] colors = new[] {
         Color.FromArgb(64,   0,   0,   0),
         Color.FromArgb(64, 255,   0,   0),
         Color.FromArgb(64, 255, 128,   0),
         Color.FromArgb(64, 255, 255,   0),

         Color.FromArgb(64, 128, 255,   0),
         Color.FromArgb(64,   0, 255,   0),
         Color.FromArgb(64,   0, 255, 128),
         Color.FromArgb(64,   0, 255, 255),

         Color.FromArgb(64,   0, 128, 255),
         Color.FromArgb(64,   0,   0, 255),
         Color.FromArgb(64, 128,   0, 255),
         Color.FromArgb(64, 255,   0, 255),

         Color.FromArgb(64, 255,   0, 128),
         Color.FromArgb(64,  85,  85,  85),
         Color.FromArgb(64, 170, 170, 170),
         Color.FromArgb(64, 255, 255, 255),
      };

      private static readonly SolidColorBrush[] brushes = colors.Select(c => new SolidColorBrush(c).Fluent(brush => brush.Freeze())).ToArray();

      public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
         var source = (int)value;
         return brushes[source];
      }

      public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
         throw new NotImplementedException();
      }
   }

   public class EqualityToBooleanConverter : IValueConverter {
      public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
         return Equals(value.ToString(), parameter.ToString());
      }

      public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
         if (parameter is bool b && b == false) return !(bool)value;
         throw new NotImplementedException();
      }
   }

   public class BooleanToVisibilityConverter : IValueConverter {
      private readonly System.Windows.Controls.BooleanToVisibilityConverter core = new System.Windows.Controls.BooleanToVisibilityConverter();
      public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
         if (value is bool b) {
            if (parameter == null) return b ? Visibility.Visible : Visibility.Collapsed;
            if (parameter is Visibility v && v == Visibility.Hidden) return b ? Visibility.Visible : Visibility.Hidden;
         }
         return core.Convert(value, targetType, parameter, culture);
      }
      public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
         if (value is Visibility vis) {
            return vis == Visibility.Visible;
         }
         return core.ConvertBack(value, targetType, parameter, culture);
      }
   }

   public class PixelImage : Image {
      private IPixelViewModel ViewModel => DataContext as IPixelViewModel;

      private bool showDebugGrid;
      public bool ShowDebugGrid {
         get => showDebugGrid;
         set {
            showDebugGrid = value;
            UpdateSource();
         }
      }

      #region TransparentBrush

      public static readonly DependencyProperty TransparentBrushProperty = DependencyProperty.Register(nameof(TransparentBrush), typeof(Brush), typeof(PixelImage), new FrameworkPropertyMetadata(Brushes.Transparent, TransparentBrushChanged));

      public Brush TransparentBrush {
         get => (Brush)GetValue(TransparentBrushProperty);
         set => SetValue(TransparentBrushProperty, value);
      }

      private static void TransparentBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
         var self = (PixelImage)d;
         self.OnTransparentBrushChanged(e);
      }

      protected virtual void OnTransparentBrushChanged(DependencyPropertyChangedEventArgs e) => UpdateSource();

      #endregion

      public PixelImage() {
         DataContextChanged += (sender, e) => UpdateDataContext(e);
         SnapsToDevicePixels = true;
         Stretch = Stretch.None;
         RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
         var transform = new ScaleTransform();
         BindingOperations.SetBinding(transform, ScaleTransform.ScaleXProperty, new Binding(nameof(IPixelViewModel.SpriteScale)));
         BindingOperations.SetBinding(transform, ScaleTransform.ScaleYProperty, new Binding(nameof(IPixelViewModel.SpriteScale)));
         LayoutTransform = transform;
      }
      private void UpdateDataContext(DependencyPropertyChangedEventArgs e) {
         if (e.OldValue is INotifyPropertyChanged oldValue) oldValue.PropertyChanged -= HandleDataContextPropertyChanged;
         if (e.NewValue is INotifyPropertyChanged newValue) newValue.PropertyChanged += HandleDataContextPropertyChanged;
         UpdateSource();
      }
      private void HandleDataContextPropertyChanged(object sender, PropertyChangedEventArgs e) {
         if (!e.PropertyName.IsAny(
            nameof(ViewModel.PixelWidth),
            nameof(ViewModel.PixelHeight),
            nameof(ViewModel.PixelData)
         )) return;
         UpdateSource();
      }
      public void UpdateSource() {
         if (ViewModel == null) return;
         var pixels = ViewModel.PixelData;
         pixels = ConvertTransparentPixels(pixels);
         if (pixels == null) return;
         var expectedLength = ViewModel.PixelWidth * ViewModel.PixelHeight;
         if (pixels.Length < expectedLength || pixels.Length == 0) { Source = null; return; }
         int stride = ViewModel.PixelWidth * 2;
         var format = PixelFormats.Bgr555;
         if (ShowDebugGrid) { pixels = pixels.ToArray(); DrawDebugGrid(pixels, ViewModel.PixelWidth); }

         if (!(Source is WriteableBitmap wSource) ||
            wSource.PixelWidth != ViewModel.PixelWidth ||
            wSource.PixelHeight != ViewModel.PixelHeight ||
            wSource.Format != format) {
            Source = new WriteableBitmap(ViewModel.PixelWidth, ViewModel.PixelHeight, 96, 96, format, null);
         }

         var source = (WriteableBitmap)Source;
         var rect = new Int32Rect(0, 0, ViewModel.PixelWidth, ViewModel.PixelHeight);
         source.WritePixels(rect, pixels, stride, 0);
      }

      private short[] ConvertTransparentPixels(short[] pixels) {
         if (ViewModel.Transparent == -1) return pixels;
         if (!(TransparentBrush is SolidColorBrush colorBrush)) return pixels;
         pixels = pixels.ToArray();
         short newColor = (short)((colorBrush.Color.B >> 3) << 10);
         newColor += (short)((colorBrush.Color.G >> 3) << 5);
         newColor += (short)(colorBrush.Color.R >> 3);
         for (int i = 0; i < pixels.Length; i++) {
            if (pixels[i] == ViewModel.Transparent) pixels[i] = newColor;
         }
         return pixels;
      }

      public static WriteableBitmap WriteOnce(IPixelViewModel viewModel) {
         var pixels = viewModel.PixelData;
         if (pixels == null) return null;
         var expectedLength = viewModel.PixelWidth * viewModel.PixelHeight;
         if (pixels.Length < expectedLength || pixels.Length == 0) return null;
         int stride = viewModel.PixelWidth * 2;
         var format = PixelFormats.Bgr555;

         var source = new WriteableBitmap(viewModel.PixelWidth, viewModel.PixelHeight, 96, 96, format, null);
         var rect = new Int32Rect(0, 0, viewModel.PixelWidth, viewModel.PixelHeight);
         source.WritePixels(rect, pixels, stride, 0);
         return source;
      }

      private void DrawDebugGrid(short[] pixels, int width) {
         for (int y = 0; y < pixels.Length; y += width * 8) {
            for (int x = 0; x < width; x += 2) pixels[y + x] = 0b_10000_10000_10000;
         }
         for (int x = 0; x < width; x += 8) {
            for (int y = 0; y < pixels.Length; y += width * 2) pixels[y + x] = 0b_10000_10000_10000;
         }
      }
   }
}
