using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
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

   public class PointConverter : IValueConverter {
      public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
         var p = (ImageLocation)value;
         return new Point(p.X, p.Y);
      }

      public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
         var p = (Point)value;
         return new ImageLocation(p.X, p.Y);
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

      #region UseTrueTransparency

      public static readonly DependencyProperty UseTrueTransparencyProperty = DependencyProperty.Register(nameof(UseTrueTransparency), typeof(bool), typeof(PixelImage), new FrameworkPropertyMetadata(false, UseTrueTransparencyChanged));

      public bool UseTrueTransparency {
         get { return (bool)GetValue(UseTrueTransparencyProperty); }
         set { SetValue(UseTrueTransparencyProperty, value); }
      }

      private static void UseTrueTransparencyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
         var self = (PixelImage)d;
         self.OnUseTrueTransparencyChanged(e);
      }

      protected virtual void OnUseTrueTransparencyChanged(DependencyPropertyChangedEventArgs e) => UpdateSource();

      #endregion

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

      #region DataContext Changed

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

      #endregion

      private static readonly List<WriteableBitmap> wbCache = new();
      private static readonly List<IPixelViewModel> pvmCache = new();

      public static WriteableBitmap WriteOnce(IPixelViewModel viewModel) {
         var cacheIndex = pvmCache.IndexOf(viewModel);
         if (cacheIndex >= 0) return wbCache[cacheIndex];

         var pixels = viewModel.PixelData;
         if (pixels == null) return null;
         var expectedLength = viewModel.PixelWidth * viewModel.PixelHeight;
         if (pixels.Length < expectedLength || pixels.Length == 0) return null;
         int stride = viewModel.PixelWidth * 2;
         var format = PixelFormats.Bgr555;

         var source = new WriteableBitmap(viewModel.PixelWidth, viewModel.PixelHeight, 96, 96, format, null);
         var rect = new Int32Rect(0, 0, viewModel.PixelWidth, viewModel.PixelHeight);
         source.WritePixels(rect, pixels, stride, 0);

         if (wbCache.Count == 20) { wbCache.RemoveAt(0); pvmCache.RemoveAt(0); }
         wbCache.Add(source);
         pvmCache.Add(viewModel);

         return source;
      }

      // Map images are rendered on a background thread. While painting on a map, the view model can ask for a
      // redraw many times per second; rendering each request (and writing a full-size bitmap for each one on the UI thread)
      // is what makes the map editor feel sluggish. Instead, only one render is in flight at a time,
      // and any requests that arrive while it runs are folded into a single follow-up render.
      private bool updateInProgress, updateRequested;

      public async void UpdateSource() {
         if (!Dispatcher.CheckAccess()) {
            // property changes can come from background work: the image itself can only be touched on the UI thread
            Dispatcher.BeginInvoke(new Action(UpdateSource));
            return;
         }
         var vm = ViewModel;
         if (vm == null || vm.PixelWidth < 0 || vm.PixelHeight < 0) return;
         short[] pixels;
         if (vm is not BlockMapViewModel) {
            pixels = vm.PixelData;
         } else {
            if (updateInProgress) { updateRequested = true; return; }
            updateInProgress = true;
            try {
               pixels = await Task.Run(() => vm.PixelData);
            } catch (Exception ex) {
               // a failed render shouldn't take down the application: just leave the previous image in place
               updateInProgress = false;
               Debug.WriteLine($"Map render failed: {ex}");
               return;
            }
            updateInProgress = false;
            if (updateRequested || vm != ViewModel) {
               // something changed while rendering: render again with the latest state
               updateRequested = false;
               UpdateSource();
               return;
            }
         }
         if (pixels == null || ViewModel == null) return;
         // true transparency: the see-through pixels get an alpha of 0 in the bitmap itself (see AlphaPixels).
         // (It used to be an OpacityMask the size of the picture. WPF smooths that mask when the picture is zoomed, which left a halo of the see-through colour around every sprite.)
         var transparent = ViewModel.Transparent;
         var useAlpha = UseTrueTransparency && transparent != -1;
         if (!UseTrueTransparency) pixels = ConvertTransparentPixels(pixels);
         if (OpacityMask is VisualBrush) OpacityMask = null; // (a mask left by an older version of this control)
         var expectedLength = ViewModel.PixelWidth * ViewModel.PixelHeight;
         if (pixels.Length < expectedLength || pixels.Length == 0) { Source = null; return; }
         if (ShowDebugGrid) { pixels = pixels.ToArray(); DrawDebugGrid(pixels, ViewModel.PixelWidth); }
         var format = useAlpha ? PixelFormats.Pbgra32 : PixelFormats.Bgr555;
         int stride = ViewModel.PixelWidth * (useAlpha ? 4 : 2);
         Array data = pixels;
         if (useAlpha) data = AlphaPixels.ToPremultipliedBgra(pixels, expectedLength, transparent);

         if (!(Source is WriteableBitmap wSource) ||
            wSource.PixelWidth != ViewModel.PixelWidth ||
            wSource.PixelHeight != ViewModel.PixelHeight ||
            wSource.Format != format) {
            Source = new WriteableBitmap(ViewModel.PixelWidth, ViewModel.PixelHeight, 96, 96, format, null);
         }

         var source = (WriteableBitmap)Source;
         var rect = new Int32Rect(0, 0, ViewModel.PixelWidth, ViewModel.PixelHeight);
         source.WritePixels(rect, data, stride, 0);
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

      private void DrawDebugGrid(short[] pixels, int width) {
         for (int y = 0; y < pixels.Length; y += width * 8) {
            for (int x = 0; x < width; x += 2) pixels[y + x] = 0b_10000_10000_10000;
         }
         for (int x = 0; x < width; x += 8) {
            for (int y = 0; y < pixels.Length; y += width * 2) pixels[y + x] = 0b_10000_10000_10000;
         }
      }
   }

   public class GridDecorator : Decorator {
      protected override void OnRender(DrawingContext surface) {
         base.OnRender(surface);
         if (DataContext is not IPixelViewModel vm) return;
         if (vm.SpriteScale < 4) return;
         var pen = new Pen(Brush(nameof(Theme.Secondary)), 1);
         for (double x = 0; x < ActualWidth; x += 8 * vm.SpriteScale) {
            surface.DrawLine(pen, new(x, 0), new(x, ActualHeight));
         }
         for (double y = 0; y < ActualHeight; y += 8 * vm.SpriteScale) {
            surface.DrawLine(pen, new(0, y), new(ActualWidth, y));
         }
      }

      private readonly Dictionary<string, SolidColorBrush> cachedBrushes = new();
      private SolidColorBrush Brush(string name) {
         if (name == null) return null;
         if (cachedBrushes.TryGetValue(name, out var brush)) return brush;
         cachedBrushes[name] = (SolidColorBrush)Application.Current.Resources.MergedDictionaries[0][name];
         return cachedBrushes[name];
      }
   }
}
