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
      private int spritePointerAddress;
      private int palettePointerAddress;
      private int[,] pixels;

      private bool withinInteraction;
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

      #region Draw Rect info
      private Point drawPoint;
      private int drawSize;
      #endregion

      #region Selection Rect info
      private Point selectionStart;
      private int selectionWidth, selectionHeight;
      private int[,] underPixels; // the pixels that are 'under' the current selection. As the selection moves, this changes.
      #endregion

      private ImageEditorTools selectedTool;
      public ImageEditorTools SelectedTool {
         get => selectedTool;
         set {
            if (TryUpdateEnum(ref selectedTool, value)) {
               underPixels = null; // too changed, clear selection cache
            }
         }
      }
      private StubCommand selectTool;
      public ICommand SelectTool => StubCommand<ImageEditorTools>(ref selectTool, arg => SelectedTool = arg);

      private int cursorSize, cursorSpritePositionX, cursorSpritePositionY, xOffset, yOffset, width, height, selectedColor, selectedPage;
      public int CursorSize { get => cursorSize; private set => Set(ref cursorSize, value); }
      public int CursorDrawPositionX => (cursorSpritePositionX - width / 2) * (int)spriteScale + xOffset;
      public int CursorDrawPositionY => (cursorSpritePositionY - height / 2) * (int)spriteScale + yOffset;
      public int CursorSpritePositionX { get => cursorSpritePositionX; private set => Set(ref cursorSpritePositionX, value); }
      public int CursorSpritePositionY { get => cursorSpritePositionY; private set => Set(ref cursorSpritePositionY, value); }
      public int XOffset { get => xOffset; private set => Set(ref xOffset, value); }
      public int YOffset { get => yOffset; private set => Set(ref yOffset, value); }
      public int PixelWidth { get => width; private set => Set(ref width, value); }
      public int PixelHeight { get => height; private set => Set(ref height, value); }
      public int SelectedColor { get => selectedColor; private set => Set(ref selectedColor, value); }
      public int SelectedPage { get => selectedPage; private set => Set(ref selectedPage, value); }

      public short[] PixelData { get; private set; }

      private double spriteScale = 1;
      public double SpriteScale { get => spriteScale; set => Set(ref spriteScale, value); }

      private PaletteCollection palette;
      public PaletteCollection Palette {
         get => palette;
         set { palette = value; NotifyPropertyChanged(); }
      }

      public int SpritePointer => spritePointerAddress;

      public ImageEditorViewModel(ChangeHistory<ModelDelta> history, IDataModel model, int address) {
         this.history = history;
         this.model = model;
         var inputRun = model.GetNextRun(address);
         var spriteRun = inputRun as ISpriteRun;
         var palRun = inputRun as IPaletteRun;
         if (spriteRun == null) spriteRun = palRun.FindDependentSprites(model).First();
         if (palRun == null) palRun = spriteRun.FindRelatedPalettes(model).First();
         spritePointerAddress = spriteRun.PointerSources[0];
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
         interactionStart = point;

         if (selectedTool == ImageEditorTools.Draw) {
            Hover(point);
         } else if (selectedTool == ImageEditorTools.Pan) {
         } else if (selectedTool == ImageEditorTools.Fill) {
         } else if (selectedTool == ImageEditorTools.Select) {
            var hoverPoint = ToSpriteSpace(point);
            if (selectionStart.X <= hoverPoint.X && selectionStart.Y <= hoverPoint.Y && selectionStart.X + selectionWidth > hoverPoint.X && selectionStart.Y + selectionHeight > hoverPoint.Y) {
               // tool down over an existing selection
               if (underPixels == null) {
                  underPixels = new int[selectionWidth, selectionHeight];
                  for (int x = 0; x < selectionWidth; x++) for (int y = 0; y < selectionHeight; y++) {
                        underPixels[x, y] = 0;
                     }
               }
            } else {
               underPixels = null; // old selection lost
               selectionStart = hoverPoint;
               selectionWidth = selectionHeight = 0;
            }
         } else {
            throw new NotImplementedException();
         }
      }

      public void Hover(Point point) {
         if (withinInteraction) {
            Drag(point);
            return;
         }

         if (selectedTool == ImageEditorTools.Draw) {
            point = ToSpriteSpace(point);
            if (WithinImage(point)) {
               drawPoint = point;
               drawSize = 1;
            } else {
               drawPoint = default;
               drawSize = 0;
            }
         }
      }

      private void Drag(Point point) {
         if (selectedTool == ImageEditorTools.Draw) {
            Debug.WriteLine($"Draw: {point}");
            var element = (Palette.Elements.FirstOrDefault(sc => sc.Selected) ?? Palette.Elements[0]);
            point = ToSpriteSpace(point);
            if (WithinImage(point)) {
               PixelData[PixelIndex(point)] = element.Color;
               pixels[point.X, point.Y] = element.Index;
               NotifyPropertyChanged(nameof(PixelData));
            }
         } else if (selectedTool == ImageEditorTools.Pan) {
            Debug.WriteLine($"Pan: {interactionStart} to {point}");
            var xRange = (int)(PixelWidth * SpriteScale / 2);
            var yRange = (int)(PixelWidth * SpriteScale / 2);
            var (originalX, originalY) = (xOffset, yOffset);
            XOffset = (XOffset + point.X - interactionStart.X).LimitToRange(-xRange, xRange);
            YOffset = (YOffset + point.Y - interactionStart.Y).LimitToRange(-yRange, yRange);
            interactionStart = new Point(interactionStart.X + XOffset - originalX, interactionStart.Y + YOffset - originalY);
         } else if (selectedTool == ImageEditorTools.Fill) {

         } else if (selectedTool == ImageEditorTools.Select) {
            if (underPixels != null) {
               var previousPoint = ToSpriteSpace(interactionStart);
               var currentPoint = ToSpriteSpace(point);
               if (previousPoint == currentPoint) return;
               if (!WithinImage(currentPoint)) return;
               var delta = currentPoint - previousPoint;
               if (!WithinImage(selectionStart + delta)) return;
               if (!WithinImage(selectionStart + delta + new Point(selectionWidth, selectionHeight))) return;

               SwapUnderPixelsWithCurrentPixels();
               selectionStart += delta;
               SwapUnderPixelsWithCurrentPixels();
               NotifyPropertyChanged(nameof(PixelData));

               interactionStart = point;
            } else {
               point = ToSpriteSpace(point);
               if (WithinImage(point)) {
                  selectionWidth = point.X - selectionStart.X;
                  selectionHeight = point.Y - selectionStart.Y;
               }
            }
         }
      }

      public void ToolUp(Point point) {
         if (selectedTool == ImageEditorTools.Draw) {
            UpdateSpriteModel();
         } else if (selectedTool == ImageEditorTools.Fill) {
            FillSpace(interactionStart, point);
         } else if (selectedTool == ImageEditorTools.Select) {
            if (underPixels != null) {
               UpdateSpriteModel();
            } else {
               if (selectionWidth < 0) {
                  selectionStart = new Point(selectionStart.X + selectionWidth, selectionStart.Y);
                  selectionWidth = -selectionWidth;
               }
               if (selectionHeight < 0) {
                  selectionStart = new Point(selectionStart.X, selectionStart.Y + selectionHeight);
                  selectionHeight = -selectionHeight;
               }
               selectionWidth += 1;
               selectionHeight += 1;
            }
         }
         withinInteraction = false;
      }

      public void EyeDropperDown(Point point) { }

      public void EyeDropperUp(Point point) {
         point = ToSpriteSpace(point);
         if (!WithinImage(point)) return;
         var index = pixels[point.X, point.Y];
         Palette.SelectionStart = index;
      }

      public bool ShowSelectionRect(Point point) {
         if (selectedTool == ImageEditorTools.Draw) {
            var x = point.X / (int)SpriteScale;
            var y = point.Y / (int)SpriteScale;

            if (x < drawPoint.X) return false;
            if (y < drawPoint.Y) return false;
            if (x >= drawPoint.X + drawSize) return false;
            if (y >= drawPoint.Y + drawSize) return false;

            return true;
         } else if (selectedTool == ImageEditorTools.Select) {
            var x = point.X / (int)SpriteScale;
            var y = point.Y / (int)SpriteScale;

            if (x < selectionStart.X) return false;
            if (y < selectionStart.Y) return false;
            if (x >= selectionStart.X + selectionWidth) return false;
            if (y >= selectionStart.Y + selectionHeight) return false;

            return true;
         } else {
            return false;
         }
      }

      public void Refresh() { }

      public int PixelIndex(Point spriteSpace) => spriteSpace.Y * PixelWidth + spriteSpace.X;

      private void WriteImage() {

      }

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
         var spriteAddress = model.ReadPointer(spritePointerAddress);
         var paletteAddress = model.ReadPointer(palettePointerAddress);

         var spriteRun = (ISpriteRun)model.GetNextRun(spriteAddress);
         var palRun = (IPaletteRun)model.GetNextRun(paletteAddress);

         PixelWidth = spriteRun.SpriteFormat.TileWidth * 8;
         PixelHeight = spriteRun.SpriteFormat.TileHeight * 8;
         PixelData = SpriteTool.Render(pixels, palRun.AllColors(model), palRun.PaletteFormat.InitialBlankPages, 0);
         NotifyPropertyChanged(nameof(PixelData));
      }

      private void UpdateSpriteModel() {
         var spriteAddress = model.ReadPointer(spritePointerAddress);
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

      private void SwapUnderPixelsWithCurrentPixels() {
         for (int x = 0; x < selectionWidth; x++) {
            for (int y = 0; y < selectionHeight; y++) {
               var (xx, yy) = (selectionStart.X + x, selectionStart.Y + y);
               (underPixels[x, y], pixels[xx, yy]) = (pixels[xx, yy], underPixels[x, y]);

               var color = Palette.Elements[pixels[xx, yy]].Color;
               PixelData[PixelIndex(new Point(xx, yy))] = color;
            }
         }
      }
   }

   public enum ImageEditorTools {
      Pan,        // a
      Select,     // s
      Draw,       // d
      Fill,       // f
      EyeDropper, // e
   }
}
