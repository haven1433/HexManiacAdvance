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
         FillSelection(pixels, stride);

         if (!(Source is WriteableBitmap wSource) ||
            wSource.PixelWidth != desiredWidth ||
            wSource.PixelHeight != desiredHeight ||
            wSource.Format != format) {
            Source = new WriteableBitmap(desiredWidth, desiredHeight, 96, 96, format, new BitmapPalette(new List<Color> {
               Colors.Transparent,
               Colors.Black,
               Colors.White,
            }));
         }

         var source = (WriteableBitmap)Source;
         var rect = new Int32Rect(0, 0, desiredWidth, desiredHeight);
         source.WritePixels(rect, pixels, stride, 0);
      }

      private const byte BLACK = 1;
      private const byte WHITE = 2;
      private void FillSelection(byte[] pixels, int stride) {
         byte currentEdgeColor;
         var zoom = (int)ViewModel.SpriteScale;
         void Line(int start, int next) {
            for (int i = 0; i < zoom; i++) {
               pixels[start] = currentEdgeColor;
               start += next;
            }
         }

         for (int x = 0; x < ViewModel.PixelWidth; x++) {
            for (int y = 0; y < ViewModel.PixelHeight; y++) {
               if (!ViewModel.ShowSelectionRect(x, y)) continue;
               var pixelColor = ViewModel.PixelData[y * ViewModel.PixelWidth + x];
               var grayScale = (pixelColor >> 10) + ((pixelColor >> 5) & 31) + (pixelColor & 31);
               currentEdgeColor = grayScale > 46 ? BLACK : WHITE;

               // each diagonal maps to a single pixel being placed
               if (!ViewModel.ShowSelectionRect(x - 1, y - 1)) pixels[(y * stride + x) * zoom] = currentEdgeColor;
               if (!ViewModel.ShowSelectionRect(x - 1, y + 1)) pixels[((y + 1) * zoom + 1) * stride + x * zoom] = currentEdgeColor;
               if (!ViewModel.ShowSelectionRect(x + 1, y - 1)) pixels[(y * stride + x + 1) * zoom + 1] = currentEdgeColor;
               if (!ViewModel.ShowSelectionRect(x + 1, y + 1)) pixels[((y + 1) * zoom + 1) * stride + (x + 1) * zoom + 1] = currentEdgeColor;

               // each edge maps to a line being placed
               if (!ViewModel.ShowSelectionRect(x - 1, y)) {
                  Line((y * zoom + 1) * stride + x * zoom, stride);
               }
               if (!ViewModel.ShowSelectionRect(x + 1, y)) {
                  Line((y * zoom + 1) * stride + (x + 1) * zoom + 1, stride);
               }
               if (!ViewModel.ShowSelectionRect(x, y - 1)) {
                  Line(y * zoom * stride + x * zoom + 1, 1);
               }
               if (!ViewModel.ShowSelectionRect(x, y + 1)) {
                  Line(((y + 1) * zoom + 1) * stride + x * zoom + 1, 1);
               }
            }
         }
      }
   }
}
