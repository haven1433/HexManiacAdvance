using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HavenSoft.HexManiac.WPF.Controls {
   public class SelectionRender : Image {
      private ImageEditorViewModel ViewModel => DataContext as ImageEditorViewModel;

      public SelectionRender() {
         DataContextChanged += (sender, e) => UpdateDataContext(e);
         Stretch = Stretch.None;
         RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
      }
      private void UpdateDataContext(DependencyPropertyChangedEventArgs e) {
         var oldValue = e.OldValue as ImageEditorViewModel;
         if (oldValue != null) {
            oldValue.PropertyChanged -= HandleDataContextPropertyChanged;
            oldValue.RefreshSelection -= HandleRefreshSelection;
         }
         var newValue = e.NewValue as ImageEditorViewModel;
         if (newValue != null) {
            newValue.PropertyChanged += HandleDataContextPropertyChanged;
            newValue.RefreshSelection += HandleRefreshSelection;
         }
         UpdateSource();
      }
      private void HandleDataContextPropertyChanged(object sender, PropertyChangedEventArgs e) {
         if (!e.PropertyName.IsAny(
            nameof(ViewModel.SpriteScale)
         )) {
            return;
         }
         UpdateSource();
      }
      private void HandleRefreshSelection(object sender, EventArgs e) => UpdateSource();

      public void UpdateSource() {
         if (ViewModel == null) return;
         var desiredWidth = ViewModel.PixelWidth * (int)ViewModel.SpriteScale + 2;
         var desiredHeight = ViewModel.PixelHeight * (int)ViewModel.SpriteScale + 2;

         int stride = desiredWidth + 2;
         var pixels = new byte[stride * desiredHeight];
         var format = PixelFormats.Indexed8;
         Width = desiredWidth;
         Height = desiredHeight;
         FillSelection(pixels, desiredWidth, desiredHeight, stride);

         if (!(Source is WriteableBitmap wSource) ||
            wSource.PixelWidth != desiredWidth ||
            wSource.PixelHeight != desiredHeight ||
            wSource.Format != format) {
            Source = new WriteableBitmap(desiredWidth, desiredHeight, 96, 96, format, new BitmapPalette(new List<Color> {
               Colors.Transparent,
               Colors.White,
            }));
         }

         var source = (WriteableBitmap)Source;
         var rect = new Int32Rect(0, 0, desiredWidth, desiredHeight);
         source.WritePixels(rect, pixels, stride, 0);
      }

      private const byte FULL = 1;
      private void FillSelection(byte[] pixels, int width, int height, int stride) {
         for (int x = 0; x < width - 2; x++) {
            for (int y = 0; y < height - 2; y++) {
               if (!ViewModel.ShowSelectionRect(x, y)) continue;
               if (!ViewModel.ShowSelectionRect(x - 1, y - 1)) pixels[x + 0 + stride * (y + 0)] = FULL;
               if (!ViewModel.ShowSelectionRect(x - 1, y + 1)) pixels[x + 0 + stride * (y + 2)] = FULL;
               if (!ViewModel.ShowSelectionRect(x + 1, y - 1)) pixels[x + 2 + stride * (y + 0)] = FULL;
               if (!ViewModel.ShowSelectionRect(x + 1, y + 1)) pixels[x + 2 + stride * (y + 2)] = FULL;
               if (!ViewModel.ShowSelectionRect(x - 1, y - 0)) pixels[x + 0 + stride * (y + 1)] = FULL;
               if (!ViewModel.ShowSelectionRect(x - 0, y - 1)) pixels[x + 1 + stride * (y + 0)] = FULL;
               if (!ViewModel.ShowSelectionRect(x + 1, y + 0)) pixels[x + 2 + stride * (y + 1)] = FULL;
               if (!ViewModel.ShowSelectionRect(x + 0, y + 1)) pixels[x + 1 + stride * (y + 2)] = FULL;
            }
         }
      }
   }
}
