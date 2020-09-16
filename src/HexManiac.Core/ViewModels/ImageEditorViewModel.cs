using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels {
   // TODO add ability to swap to another palette used by this sprite
   // TODO add the ability to swap to another sprite used by this palette
   // TODO add the ability to swap to another page of this sprite/palette
   // TODO add the ability to show all sprites condensed to one sprite, horizontal or vertical
   // TODO add the ability to show all pages condensed to one sprite, horizontal or vertical

   // all x/y terms are in 'pixels' from the center of the image viewing area.
   // cursor size is in terms of destination pixels (1x1, 2x2, 4x4, 8x8)
   // cursor sprite position is in terms of the sprite (ranging from 0,0 to width,height)

   public class ImageEditorViewModel : ViewModelCore, ITabContent, IPixelViewModel, IRaiseMessageTab {
      private readonly ChangeHistory<ModelDelta> history;
      private readonly IDataModel model;
      private int palettePointerAddress;
      private int[,] pixels;

      private bool withinInteraction, withinDropperInteraction;
      private Point interactionStart;

      #region ITabContent Properties

      private StubCommand close;

      public string Name => "Image Editor";
      public ICommand Save => null;
      public ICommand SaveAs => null;
      public ICommand Undo => history.Undo;
      public ICommand Redo => history.Redo;
      public ICommand Copy => null;
      public ICommand DeepCopy => null;
      public ICommand Clear => null;
      public ICommand SelectAll => null;
      public ICommand Goto => null;
      public ICommand ResetAlignment => null;
      public ICommand Back => null;
      public ICommand Forward => null;
      public ICommand Close => StubCommand(ref close, () => Closed?.Invoke(this, EventArgs.Empty));
      public event EventHandler<string> OnError;
      public event EventHandler<string> OnMessage;
      public event EventHandler ClearMessage;
      public event EventHandler Closed;
      public event EventHandler<ITabContent> RequestTabChange;
      public event EventHandler<Action> RequestDelayedWork;
      public event EventHandler RequestMenuClose;

      public void RaiseMessage(string message) => OnMessage?.Invoke(this, message);

      #endregion

      private IImageToolStrategy toolStrategy;
      private EyeDropperTool eyeDropperStrategy; // stored separately because of right-click
      private ImageEditorTools selectedTool;
      public ImageEditorTools SelectedTool {
         get => selectedTool;
         set {
            if (TryUpdateEnum(ref selectedTool, value)) {
               toolStrategy = selectedTool == ImageEditorTools.Draw ? new DrawTool(this)
                            : selectedTool == ImageEditorTools.Select ? new SelectionTool(this)
                            : selectedTool == ImageEditorTools.Pan ? new PanTool(this)
                            : selectedTool == ImageEditorTools.Fill ? new FillTool(this)
                            : selectedTool == ImageEditorTools.EyeDropper ? eyeDropperStrategy
                            : (IImageToolStrategy)default;
            }
         }
      }
      private StubCommand selectTool;
      public ICommand SelectTool => StubCommand<ImageEditorTools>(ref selectTool, arg => SelectedTool = arg);

      private int xOffset, yOffset, width, height;
      public int XOffset { get => xOffset; private set => Set(ref xOffset, value); }
      public int YOffset { get => yOffset; private set => Set(ref yOffset, value); }
      public int PixelWidth { get => width; private set => Set(ref width, value); }
      public int PixelHeight { get => height; private set => Set(ref height, value); }

      public short[] PixelData { get; private set; }

      private double spriteScale = 1;
      public double SpriteScale { get => spriteScale; set => Set(ref spriteScale, value); }

      private PaletteCollection palette;
      public PaletteCollection Palette {
         get => palette;
         set { palette = value; NotifyPropertyChanged(); }
      }

      public int SpritePointer { get; }

      public ImageEditorViewModel(ChangeHistory<ModelDelta> history, IDataModel model, int address) {
         this.history = history;
         this.model = model;
         this.toolStrategy = new PanTool(this);
         this.eyeDropperStrategy = new EyeDropperTool(this);
         var inputRun = model.GetNextRun(address);
         var spriteRun = inputRun as ISpriteRun;
         var palRun = inputRun as IPaletteRun;
         if (spriteRun == null) spriteRun = palRun.FindDependentSprites(model).First();
         if (palRun == null) palRun = spriteRun.FindRelatedPalettes(model).First();
         SpritePointer = spriteRun.PointerSources[0];
         palettePointerAddress = palRun.PointerSources[0];
         pixels = spriteRun.GetPixels(model, 0);

         Render();
         Palette = new PaletteCollection(this, model, history) { SourcePalette = palRun.Start };
         RefreshPaletteColors();
      }

      // convenience methods
      public void ZoomIn(int x, int y) => ZoomIn(new Point(x, y));
      public void ZoomOut(int x, int y) => ZoomOut(new Point(x, y));
      public void ToolDown(int x, int y) => ToolDown(new Point(x, y));
      public void Hover(int x, int y) => Hover(new Point(x, y));
      public void ToolUp(int x, int y) => ToolUp(new Point(x, y));
      public void EyeDropperDown(int x, int y) => EyeDropperDown(new Point(x, y));
      public void EyeDropperUp(int x, int y) => EyeDropperUp(new Point(x, y));
      public bool ShowSelectionRect(int x, int y) => ShowSelectionRect(new Point(x, y));

      public void ZoomIn(Point point) {
         if (SpriteScale > 15) return;
         Debug.WriteLine($"Zoom In: {point}");
         var (x, y) = (point.X, point.Y);
         xOffset -= x;
         yOffset -= y;
         var xPartial  = xOffset / SpriteScale;
         var yPartial = yOffset / SpriteScale;
         SpriteScale += 1;
         xOffset = (int)(xPartial * SpriteScale) + x;
         yOffset = (int)(yPartial * SpriteScale) + y;
         NotifyPropertyChanged(nameof(XOffset));
         NotifyPropertyChanged(nameof(YOffset));
      }

      public void ZoomOut(Point point) {
         if (SpriteScale < 2) return;
         var (x, y) = (point.X, point.Y);
         xOffset -= x;
         yOffset -= y;
         var xPartial = xOffset / SpriteScale;
         var yPartial = yOffset / SpriteScale;
         SpriteScale -= 1;
         var xRange = (int)(PixelWidth * SpriteScale / 2);
         var yRange = (int)(PixelWidth * SpriteScale / 2);
         XOffset = ((int)(xPartial * SpriteScale) + x).LimitToRange(-xRange, xRange);
         YOffset = ((int)(yPartial * SpriteScale) + y).LimitToRange(-yRange, yRange);
      }

      public void ToolDown(Point point) {
         withinInteraction = true;
         withinDropperInteraction = false;
         interactionStart = point;
         toolStrategy.ToolDown(point);
      }

      public void Hover(Point point) {
         if (!withinInteraction) {
            toolStrategy.ToolHover(point);
         } else if (!withinDropperInteraction) {
            toolStrategy.ToolDrag(point);
         } else {
            eyeDropperStrategy.ToolDrag(point);
         }
      }

      public void ToolUp(Point point) {
         toolStrategy.ToolUp(point);
         withinInteraction = false;
      }

      public void EyeDropperDown(Point point) {
         withinInteraction = withinDropperInteraction = true;
         interactionStart = point;
         eyeDropperStrategy.ToolDown(point);
      }

      public void EyeDropperUp(Point point) {
         eyeDropperStrategy.ToolUp(point);
         withinInteraction = false;
      }

      public bool ShowSelectionRect(Point point) {
         if (withinInteraction && withinDropperInteraction) {
            return eyeDropperStrategy.ShowSelectionRect(point);
         } else {
            return toolStrategy.ShowSelectionRect(point);
         }
      }

      public void Refresh() { }

      public int PixelIndex(int x, int y) => PixelIndex(new Point(x, y));
      public int PixelIndex(Point spriteSpace) => spriteSpace.Y * PixelWidth + spriteSpace.X;

      private Point ToSpriteSpace(Point point) {
         var x = point.X;
         var y = point.Y;
         var scale = (int)SpriteScale;
         x = (x - xOffset) / scale + PixelWidth / 2;
         y = (y - yOffset) / scale + PixelHeight / 2;
         return new Point(x, y);
      }

      private void RefreshPaletteColors() {
         var paletteAddress = model.ReadPointer(palettePointerAddress);
         var palRun = (IPaletteRun)model.GetNextRun(paletteAddress);
         Palette.SetContents(palRun.GetPalette(model, 0));
         foreach (var e in Palette.Elements) {
            e.PropertyChanged += (sender, args) => {
               var sc = (SelectableColor)sender;
               switch (args.PropertyName) {
                  case nameof(sc.Selected):
                     if (sc.Selected) {
                        SelectedTool = ImageEditorTools.Draw;
                     }
                     break;
                  case nameof(sc.Color):
                     Palette.PushColorsToModel();
                     Render();
                     break;
               }
            };
         }
      }

      private bool WithinImage(Point p) => p.X >= 0 && p.X < PixelWidth && p.Y >= 0 && p.Y < PixelHeight;

      private void Render() {
         var spriteAddress = model.ReadPointer(SpritePointer);
         var paletteAddress = model.ReadPointer(palettePointerAddress);

         var spriteRun = (ISpriteRun)model.GetNextRun(spriteAddress);
         var palRun = (IPaletteRun)model.GetNextRun(paletteAddress);

         PixelWidth = spriteRun.SpriteFormat.TileWidth * 8;
         PixelHeight = spriteRun.SpriteFormat.TileHeight * 8;
         PixelData = SpriteTool.Render(pixels, palRun.AllColors(model), palRun.PaletteFormat.InitialBlankPages, 0);
         NotifyPropertyChanged(nameof(PixelData));
      }

      private void UpdateSpriteModel() {
         var spriteAddress = model.ReadPointer(SpritePointer);
         var spriteRun = (ISpriteRun)model.GetNextRun(spriteAddress);
         spriteRun.SetPixels(model, history.CurrentChange, 0, pixels);
      }

      private void FillSpace(Point a, Point b) {
         a = ToSpriteSpace(a);
         b = ToSpriteSpace(b);
         if (a != b) throw new NotImplementedException();
         var element = (Palette.Elements.FirstOrDefault(sc => sc.Selected) ?? Palette.Elements[0]);
         int originalColorIndex = pixels[a.X, a.Y];
         var targetColorIndex = element.Index;

         var toProcess = new Queue<Point>(new[] { a });
         var processed = new HashSet<Point>();
         while (toProcess.Count > 0) {
            var current = toProcess.Dequeue();
            processed.Add(current);
            if (pixels[current.X, current.Y] != originalColorIndex) continue;

            pixels[current.X, current.Y] = targetColorIndex;
            PixelData[PixelIndex(current)] = element.Color;
            foreach (var next in new[]{
               new Point(current.X - 1, current.Y),
               new Point(current.X + 1, current.Y),
               new Point(current.X, current.Y - 1),
               new Point(current.X, current.Y + 1) }
            ) {
               if (WithinImage(next) && !processed.Contains(next)) toProcess.Enqueue(next);
            }
         }

         UpdateSpriteModel();
      }

      #region Nested Types
      private interface IImageToolStrategy {
         void ToolDown(Point screenPosition);
         void ToolHover(Point screenPosition);
         void ToolDrag(Point screenPosition);
         void ToolUp(Point screenPosition);
         bool ShowSelectionRect(Point subPixelPosition);
      }

      private class DrawTool : IImageToolStrategy {
         private readonly ImageEditorViewModel parent;

         private Point drawPoint;
         private int drawSize;

         public DrawTool(ImageEditorViewModel parent) => this.parent = parent;

         public void ToolDown(Point point) {
            ToolDrag(point);
         }

         public void ToolDrag(Point point) {
            Debug.WriteLine($"Draw: {point}");
            var element = (parent.Palette.Elements.FirstOrDefault(sc => sc.Selected) ?? parent.Palette.Elements[0]);
            point = parent.ToSpriteSpace(point);
            if (parent.WithinImage(point)) {
               var tile = parent.eyeDropperStrategy.Tile;
               if (tile == null) {
                  parent.PixelData[parent.PixelIndex(point)] = element.Color;
                  parent.pixels[point.X, point.Y] = element.Index;
               } else {
                  drawSize = tile.GetLength(0);
                  drawPoint = new Point(point.X - point.X % drawSize, point.Y - point.Y % drawSize);
                  for (int x = 0; x < drawSize; x++) {
                     for (int y = 0; y < drawSize; y++) {
                        var (xx, yy) = (drawPoint.X + x, drawPoint.Y + y);
                        parent.PixelData[parent.PixelIndex(xx, yy)] = parent.palette.Elements[tile[x, y]].Color;
                        parent.pixels[xx, yy] = tile[x, y];
                     }
                  }
               }
               parent.NotifyPropertyChanged(nameof(PixelData));
            }
         }

         public void ToolHover(Point point) {
            point = parent.ToSpriteSpace(point);
            if (parent.WithinImage(point)) {
               var tile = parent.eyeDropperStrategy.Tile;
               if (tile == null) {
                  drawPoint = point;
                  drawSize = 1;
               } else {
                  drawSize = tile.GetLength(0);
                  drawPoint = new Point(point.X - point.X % drawSize, point.Y - point.Y % drawSize);
               }
            } else {
               drawPoint = default;
               drawSize = 0;
            }
         }

         public void ToolUp(Point screenPosition) {
            parent.UpdateSpriteModel();
         }

         public bool ShowSelectionRect(Point point) {
            var x = point.X / (int)parent.SpriteScale;
            var y = point.Y / (int)parent.SpriteScale;

            if (x < drawPoint.X) return false;
            if (y < drawPoint.Y) return false;
            if (x >= drawPoint.X + drawSize) return false;
            if (y >= drawPoint.Y + drawSize) return false;

            return true;
         }
      }

      private class SelectionTool : IImageToolStrategy {
         private readonly ImageEditorViewModel parent;

         private Point selectionStart;
         private int selectionWidth, selectionHeight;
         private int[,] underPixels; // the pixels that are 'under' the current selection. As the selection moves, this changes.

         public SelectionTool(ImageEditorViewModel parent) => this.parent = parent;

         public void ToolDown(Point point) {
            var hoverPoint = parent.ToSpriteSpace(point);
            if (selectionStart.X > hoverPoint.X ||
               selectionStart.Y > hoverPoint.Y ||
               selectionStart.X + selectionWidth <= hoverPoint.X ||
               selectionStart.Y + selectionHeight <= hoverPoint.Y
            ) {
               underPixels = null; // old selection lost
               selectionStart = hoverPoint;
               selectionWidth = selectionHeight = 0;
            }
         }

         public void ToolDrag(Point point) {
            if (underPixels != null) {
               var previousPoint = parent.ToSpriteSpace(parent.interactionStart);
               var currentPoint = parent.ToSpriteSpace(point);
               if (previousPoint == currentPoint) return;
               if (!parent.WithinImage(currentPoint)) return;
               var delta = currentPoint - previousPoint;
               if (!parent.WithinImage(selectionStart + delta)) return;
               if (!parent.WithinImage(selectionStart + delta + new Point(selectionWidth, selectionHeight))) return;

               SwapUnderPixelsWithCurrentPixels();
               selectionStart += delta;
               SwapUnderPixelsWithCurrentPixels();
               parent.NotifyPropertyChanged(nameof(PixelData));

               parent.interactionStart = point;
            } else {
               point = parent.ToSpriteSpace(point);
               if (parent.WithinImage(point)) {
                  selectionWidth = point.X - selectionStart.X;
                  selectionHeight = point.Y - selectionStart.Y;
               }
            }
         }

         public void ToolHover(Point screenPosition) { }

         public void ToolUp(Point point) {
            if (underPixels != null) {
               parent.UpdateSpriteModel();
            } else {
               (selectionStart, selectionWidth, selectionHeight) = BuildRect(selectionStart, selectionWidth, selectionHeight);

               underPixels = new int[selectionWidth, selectionHeight];
               for (int x = 0; x < selectionWidth; x++) for (int y = 0; y < selectionHeight; y++) {
                  underPixels[x, y] = 0;
               }
            }
         }

         public bool ShowSelectionRect(Point point) {
            var x = point.X / (int)parent.SpriteScale;
            var y = point.Y / (int)parent.SpriteScale;

            var (start, width, height) = (selectionStart, selectionWidth, selectionHeight);

            if (parent.withinInteraction && underPixels == null) {
               (start, width, height) = BuildRect(selectionStart, selectionWidth, selectionHeight);
            }

            if (x < start.X) return false;
            if (y < start.Y) return false;
            if (x >= start.X + width) return false;
            if (y >= start.Y + height) return false;

            return true;
         }

         public static (Point point, int width, int height) BuildRect(Point start, int dragX, int dragY) {
            if (dragX < 0) {
               start += new Point(dragX, 0);
               dragX = -dragX;
            }
            if (dragY < 0) {
               start += new Point(0, dragY);
               dragY = -dragY;
            }

            return (start, dragX + 1, dragY + 1);
         }

         private void SwapUnderPixelsWithCurrentPixels() {
            for (int x = 0; x < selectionWidth; x++) {
               for (int y = 0; y < selectionHeight; y++) {
                  var (xx, yy) = (selectionStart.X + x, selectionStart.Y + y);
                  (underPixels[x, y], parent.pixels[xx, yy]) = (parent.pixels[xx, yy], underPixels[x, y]);

                  var color = parent.Palette.Elements[parent.pixels[xx, yy]].Color;
                  parent.PixelData[parent.PixelIndex(new Point(xx, yy))] = color;
               }
            }
         }
      }

      private class PanTool : IImageToolStrategy {
         private readonly ImageEditorViewModel parent;
         public PanTool(ImageEditorViewModel parent) => this.parent = parent;

         public void ToolDown(Point screenPosition) { }

         public void ToolDrag(Point point) {
            Debug.WriteLine($"Pan: {parent.interactionStart} to {point}");
            var xRange = (int)(parent.PixelWidth * parent.SpriteScale / 2);
            var yRange = (int)(parent.PixelWidth * parent.SpriteScale / 2);
            var (originalX, originalY) = (parent.xOffset, parent.yOffset);
            parent.XOffset = (parent.XOffset + point.X - parent.interactionStart.X).LimitToRange(-xRange, xRange);
            parent.YOffset = (parent.YOffset + point.Y - parent.interactionStart.Y).LimitToRange(-yRange, yRange);
            parent.interactionStart = new Point(parent.interactionStart.X + parent.XOffset - originalX, parent.interactionStart.Y + parent.YOffset - originalY);
         }

         public void ToolHover(Point screenPosition) { }

         public void ToolUp(Point screenPosition) { }

         public bool ShowSelectionRect(Point subPixelPosition) => false;
      }

      private class FillTool : IImageToolStrategy {
         private readonly ImageEditorViewModel parent;
         public FillTool(ImageEditorViewModel parent) => this.parent = parent;

         public void ToolDown(Point screenPosition) { }

         public void ToolDrag(Point screenPosition) { }

         public void ToolHover(Point screenPosition) { }

         public void ToolUp(Point point) {
            parent.FillSpace(parent.interactionStart, point);
         }

         public bool ShowSelectionRect(Point subPixelPosition) => false;
      }

      // TODO make this able to display the selected tile
      private class EyeDropperTool : IImageToolStrategy {
         private readonly ImageEditorViewModel parent;

         private Point selectionStart;
         private int selectionWidth, selectionHeight;
         private int[,] underPixels; // the pixels that are 'under' the current selection. As the selection moves, this changes.

         public int[,] Tile {
            get {
               if (selectionWidth < 2) return null;
               return underPixels;
            }
         }

         public EyeDropperTool(ImageEditorViewModel parent) => this.parent = parent;

         public bool ShowSelectionRect(Point subPixelPosition) => false;

         public void ToolDown(Point point) {
            underPixels = null; // old selection lost
            selectionStart = parent.ToSpriteSpace(point);
            selectionWidth = selectionHeight = 0;
         }

         public void ToolDrag(Point point) {
            point = parent.ToSpriteSpace(point);
            if (parent.WithinImage(point)) {
               selectionWidth = point.X - selectionStart.X;
               selectionHeight = point.Y - selectionStart.Y;
            }
         }

         public void ToolHover(Point screenPosition) { }

         public void ToolUp(Point point) {
            (selectionStart, selectionWidth, selectionHeight) = SelectionTool.BuildRect(selectionStart, selectionWidth, selectionHeight);

            // make sure the selection is a power-of-2 box
            selectionWidth = Math.Min(selectionWidth, selectionHeight);
            var log = (int)Math.Log(selectionWidth, 2);
            selectionWidth = (int)Math.Pow(2, log);
            selectionHeight = selectionWidth;

            if (selectionWidth == 1 && selectionHeight == 1) {
               point = parent.ToSpriteSpace(point);
               if (!parent.WithinImage(point)) return;
               var index = parent.pixels[point.X, point.Y];
               parent.Palette.SelectionStart = index;
            } else {
               underPixels = new int[selectionWidth, selectionHeight];
               for (int x = 0; x < selectionWidth; x++) for (int y = 0; y < selectionHeight; y++) {
                  underPixels[x, y] = parent.pixels[selectionStart.X + x, selectionStart.Y + y];
               }
            }
         }
      }

      #endregion
   }

   public enum ImageEditorTools {
      Pan,        // a
      Select,     // s
      Draw,       // d
      Fill,       // f
      EyeDropper, // e
   }
}
