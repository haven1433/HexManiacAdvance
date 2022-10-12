using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using System;

namespace HavenSoft.HexManiac.Core.ViewModels.Map {
   public class BorderEditor : ViewModelCore {
      private readonly ChangeHistory<ModelDelta> changeHistory;
      private readonly IDataModel model;
      private readonly BlockMapViewModel blockmap;

      public event EventHandler BorderChanged;

      private bool showBorder;
      public bool ShowBorderPanel { get => showBorder; set => Set(ref showBorder, value); }

      private CanvasPixelViewModel borderRender;
      public IPixelViewModel BorderRender {
         get {
            if (borderRender != null) return borderRender;
            var layout = blockmap.GetLayout();
            RenderBorder(layout);
            return borderRender;
         }
      }

      #region Border Width/Height

      public bool HasBorderDimensions { get; private set; }

      private int width = -1, height = -1;

      public int Width {
         get {
            if (width != -1) return width;
            width = HasBorderDimensions ? blockmap.GetLayout().GetValue("borderwidth") : 2;
            return width;
         }
         set {
            if (!HasBorderDimensions) return;
            var oldWidth = Width;
            if (oldWidth == value) return;
            width = value;
            var layout = blockmap.GetLayout();
            layout.SetValue("borderwidth", value);
            Resize(layout, oldWidth, Height, width, Height);
         }
      }

      public int Height {
         get {
            if (height != -1) return height;
            height = HasBorderDimensions ? blockmap.GetLayout().GetValue("borderheight") : 2;
            return height;
         }
         set {
            if (!HasBorderDimensions) return;
            var oldHeight = Height;
            if (oldHeight == value) return;
            height = value;
            var layout = blockmap.GetLayout();
            layout.SetValue("borderheight", value);
            Resize(layout, Width, oldHeight, width, Height);
         }
      }

      private void Resize(ModelArrayElement layout, int oldWidth, int oldHeight, int newWidth, int newHeight) {
         var existingData = new int[oldWidth, oldHeight];
         var oldStart = layout.GetAddress("borderblock");
         var considerRepoint = oldWidth * oldHeight < newWidth * newHeight;
         var token = changeHistory.CurrentChange;
         for (int y = 0; y < oldHeight; y++) {
            for (int x = 0; x < oldWidth; x++) {
               existingData[x, y] = model.ReadMultiByteValue(oldStart + (y * oldWidth + x) * 2, 2);
               if (considerRepoint) model.WriteMultiByteValue(oldStart + (y * oldWidth + x) * 2, 2, token, 0xFFFF);
            }
         }
         var newStart = oldStart;
         if (considerRepoint) {
            var run = model.GetNextRun(oldStart);
            run = model.RelocateForExpansion(token, run, newWidth * newHeight * 2);
            newStart = run.Start;
         }
         for (int y = 0; y < newHeight; y++) {
            for (int x = 0; x < newWidth; x++) {
               model.WriteMultiByteValue(newStart + (y * newWidth + x) * 2, 2, token, x < existingData.GetLength(0) && y < existingData.GetLength(1) ? existingData[x, y] : 0);
            }
         }
         RenderBorder(layout);
         NotifyPropertyChanged(nameof(BorderRender));
         BorderChanged.Raise(this);
      }

      #endregion

      public BorderEditor(BlockMapViewModel blockmap) {
         this.blockmap = blockmap;
         this.model = blockmap.ViewPort.Model;
         this.changeHistory = blockmap.ViewPort.ChangeHistory;
         var layout = blockmap.GetLayout();
         HasBorderDimensions = layout.HasField("borderwidth") && layout.HasField("borderheight");
      }

      public void Draw(int blockIndex, double x, double y) {
         if (blockIndex < 0 || blockIndex >= blockmap.BlockRenders.Count) return;
         int xx = (int)(x / 16);
         int yy = (int)(y / 16);
         var layout = blockmap.GetLayout();
         int width = Width;
         var address = layout.GetAddress("borderblock");
         address += (yy * width + xx) * 2;
         var existingValue = model.ReadMultiByteValue(address, 2);
         if (blockIndex == existingValue) return;
         model.WriteMultiByteValue(address, 2, changeHistory.CurrentChange, blockIndex);
         RenderBorder(layout);
         NotifyPropertyChanged(nameof(BorderRender));
         BorderChanged.Raise(this);
      }

      public void Draw(int[,] tiles, double x, double y) {
         int xx = (int)(x / 16);
         int yy = (int)(y / 16);
         var layout = blockmap.GetLayout();
         var (width, height) = (Width, Height);
         var startAddress = layout.GetAddress("borderblock");
         bool redraw = false;
         for (int dx = 0; dx < tiles.GetLength(0); dx++) {
            for (int dy = 0; dy < tiles.GetLength(1); dy++) {
               if (yy + dy >= height) continue;
               if (xx + dx >= width) continue;
               var address = startAddress + ((yy + dy) * width + xx + dx) * 2;
               var existingValue = model.ReadMultiByteValue(address, 2);
               if (tiles[dx, dy] == existingValue) continue;
               model.WriteMultiByteValue(address, 2, changeHistory.CurrentChange, tiles[dx, dy]);
               redraw = true;
            }
         }
         if (!redraw) return;
         RenderBorder(layout);
         NotifyPropertyChanged(nameof(BorderRender));
         BorderChanged.Raise(this);
      }

      public int GetBlock(double x, double y) {
         int xx = (int)(x / 16);
         int yy = (int)(y / 16);
         var layout = blockmap.GetLayout();
         int width = Width;
         var address = layout.GetAddress("borderblock");
         address += (yy * width + xx) * 2;
         var existingValue = model.ReadMultiByteValue(address, 2);
         return existingValue;
      }

      private void RenderBorder(ModelArrayElement layout) {
         int width = Width, height = Height;
         var canvas = new CanvasPixelViewModel(width * 16, height * 16) { SpriteScale = 3 };
         var borderStart = layout.GetAddress("borderblock");
         for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
               var index = model.ReadMultiByteValue(borderStart + (y * width + x) * 2, 2) & 0x3FF;
               if (index >= blockmap.BlockRenders.Count) continue;
               canvas.Draw(blockmap.BlockRenders[index], x * 16, y * 16);
            }
         }
         if (borderRender == null || borderRender.PixelWidth != canvas.PixelWidth || borderRender.PixelHeight != canvas.PixelHeight) {
            borderRender = canvas;
         } else {
            borderRender.Draw(canvas, 0, 0);
         }
      }
   }
}
