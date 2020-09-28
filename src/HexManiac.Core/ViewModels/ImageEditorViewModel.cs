using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
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

      private bool withinInteraction, withinDropperInteraction, withinPanInteraction;
      private Point interactionStart;

      private bool[,] selectedPixels;

      #region ITabContent Properties

      private StubCommand close, undoWrapper, redoWrapper;

      public string Name => "Image Editor";
      public ICommand Save => null;
      public ICommand SaveAs => null;
      public ICommand Undo => StubCommand(ref undoWrapper, ExecuteUndo, () => history.Undo.CanExecute(default));
      public ICommand Redo => StubCommand(ref redoWrapper, ExecuteRedo, () => history.Redo.CanExecute(default));
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

      private void ExecuteUndo() {
         history.Undo.Execute();
         Refresh();
      }

      private void ExecuteRedo() {
         history.Redo.Execute();
         Refresh();
      }

      #endregion

      private IImageToolStrategy toolStrategy;
      private EyeDropperTool eyeDropperStrategy; // stored separately because of right-click
      private PanTool panStrategy; // stored separately because of center-click
      private ImageEditorTools selectedTool;
      public ImageEditorTools SelectedTool {
         get => selectedTool;
         set {
            if (TryUpdateEnum(ref selectedTool, value)) {
               toolStrategy = selectedTool == ImageEditorTools.Draw ? new DrawTool(this)
                            : selectedTool == ImageEditorTools.Select ? new SelectionTool(this)
                            : selectedTool == ImageEditorTools.Pan ? panStrategy
                            : selectedTool == ImageEditorTools.Fill ? new FillTool(this)
                            : selectedTool == ImageEditorTools.EyeDropper ? eyeDropperStrategy
                            : (IImageToolStrategy)default;
               RaiseRefreshSelection();
            }
         }
      }
      private StubCommand selectTool, selectColor, zoomInCommand, zoomOutCommand;
      public ICommand SelectTool => StubCommand<ImageEditorTools>(ref selectTool, arg => SelectedTool = arg);
      public ICommand SelectColor => StubCommand<string>(ref selectColor, arg => Palette.SelectionStart = int.Parse(arg));
      public ICommand ZoomInCommand => StubCommand(ref zoomInCommand, () => ZoomIn(0, 0));
      public ICommand ZoomOutCommand => StubCommand(ref zoomOutCommand, () => ZoomOut(0, 0));

      public BlockPreview BlockPreview { get; }

      public event EventHandler RefreshSelection;
      private void RaiseRefreshSelection(params Point[] toSelect) {
         selectedPixels = new bool[PixelWidth, PixelHeight];
         foreach (var s in toSelect) {
            if (WithinImage(s)) selectedPixels[s.X, s.Y] = true;
         }
         RefreshSelection?.Invoke(this, EventArgs.Empty);
      }

      private int xOffset, yOffset, width, height;
      public int XOffset { get => xOffset; private set => Set(ref xOffset, value); }
      public int YOffset { get => yOffset; private set => Set(ref yOffset, value); }
      public int PixelWidth { get => width; private set => Set(ref width, value); }
      public int PixelHeight { get => height; private set => Set(ref height, value); }

      public short[] PixelData { get; private set; }

      private double spriteScale = 1;
      public double SpriteScale { get => spriteScale; set => Set(ref spriteScale, value); }

      public PaletteCollection Palette { get; }

      public int SpritePointer { get; }

      private StubCommand setCursorSize;
      public ICommand SetCursorSize => StubCommand<string>(ref setCursorSize, arg => CursorSize = int.Parse(arg));
      private int cursorSize = 1;
      public int CursorSize { get => cursorSize; set => Set(ref cursorSize, value, arg => BlockPreview.Clear()); }

      public ImageEditorViewModel(ChangeHistory<ModelDelta> history, IDataModel model, int address) {
         this.history = history;
         this.model = model;
         this.toolStrategy = this.panStrategy = new PanTool(this);
         this.eyeDropperStrategy = new EyeDropperTool(this);
         var inputRun = model.GetNextRun(address);
         var spriteRun = inputRun as ISpriteRun;
         var palRun = inputRun as IPaletteRun;
         if (spriteRun == null) spriteRun = palRun.FindDependentSprites(model).First();
         if (palRun == null) palRun = spriteRun.FindRelatedPalettes(model).First();
         SpritePointer = spriteRun.PointerSources[0];
         palettePointerAddress = palRun.PointerSources[0];
         Palette = new PaletteCollection(this, model, history) { SourcePalette = palRun.Start };
         Palette.Bind(nameof(Palette.HoverIndex), UpdateSelectionFromPaletteHover);
         Refresh();
         selectedPixels = new bool[PixelWidth, PixelHeight];
         BlockPreview = new BlockPreview();
      }

      // convenience methods
      public void ZoomIn(int x, int y) => ZoomIn(new Point(x, y));
      public void ZoomOut(int x, int y) => ZoomOut(new Point(x, y));
      public void ToolDown(int x, int y) => ToolDown(new Point(x, y));
      public void Hover(int x, int y) => Hover(new Point(x, y));
      public void ToolUp(int x, int y) => ToolUp(new Point(x, y));
      public void EyeDropperDown(int x, int y) => EyeDropperDown(new Point(x, y));
      public void EyeDropperUp(int x, int y) => EyeDropperUp(new Point(x, y));
      public void PanDown(int x, int y) => PanDown(new Point(x, y));
      public void PanUp(int x, int y) => PanUp(new Point(x, y));
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
         history.ChangeCompleted();
         withinInteraction = true;
         interactionStart = point;
         toolStrategy.ToolDown(point);
      }

      public void Hover(Point point) {
         if (!withinInteraction) {
            toolStrategy.ToolHover(point);
         } else if (withinDropperInteraction) {
            eyeDropperStrategy.ToolDrag(point);
         } else if (withinPanInteraction) {
            panStrategy.ToolDrag(point);
         } else {
            toolStrategy.ToolDrag(point);
         }
      }

      public void ToolUp(Point point) {
         toolStrategy.ToolUp(point);
         withinInteraction = false;
         history.ChangeCompleted();
      }

      public void EyeDropperDown(Point point) {
         withinInteraction = withinDropperInteraction = true;
         interactionStart = point;
         eyeDropperStrategy.ToolDown(point);
      }

      public void EyeDropperUp(Point point) {
         eyeDropperStrategy.ToolUp(point);
         withinInteraction = withinDropperInteraction = false;
      }

      public void PanDown(Point point) {
         withinInteraction = withinPanInteraction = true;
         interactionStart = point;
         panStrategy.ToolDown(point);
      }

      public void PanUp(Point point) {
         panStrategy.ToolUp(point);
         withinInteraction = withinPanInteraction = false;
      }

      public bool ShowSelectionRect(Point point) {
         if (point.X < 0 || point.X >= PixelWidth || point.Y < 0 || point.Y >= PixelHeight) return false;
         return selectedPixels[point.X, point.Y];
      }

      public void Refresh() {
         var spriteAddress = model.ReadPointer(SpritePointer);
         var spriteRun = (ISpriteRun)model.GetNextRun(spriteAddress);
         pixels = spriteRun.GetPixels(model, 0);
         Render();
         RefreshPaletteColors();
      }

      public int PixelIndex(int x, int y) => PixelIndex(new Point(x, y));
      public int PixelIndex(Point spriteSpace) => spriteSpace.Y * PixelWidth + spriteSpace.X;

      private Point ToSpriteSpace(Point point) {
         var x = point.X;
         var y = point.Y;
         x = (int)Math.Floor((x - xOffset) / SpriteScale) + PixelWidth / 2;
         y = (int)Math.Floor((y - yOffset) / SpriteScale) + PixelHeight / 2;
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
                     if (sc.Selected && SelectedTool != ImageEditorTools.Fill) {
                        SelectedTool = ImageEditorTools.Draw;
                     }
                     BlockPreview.Clear();
                     if (CursorSize == 0) CursorSize = 1;
                     break;
                  case nameof(sc.Color):
                     Palette.PushColorsToModel(); // this causes a Render
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

      private void UpdateSelectionFromPaletteHover(PaletteCollection sender, PropertyChangedEventArgs e) {
         var matches = new List<Point>();
         for(int x = 0; x < PixelWidth; x++) {
            for(int y = 0; y < PixelHeight; y++) {
               if (pixels[x, y] != Palette.HoverIndex) continue;
               matches.Add(new Point(x, y));
            }
         }
         RaiseRefreshSelection(matches.ToArray());
      }

      #region Nested Types

      private interface IImageToolStrategy {
         void ToolDown(Point screenPosition);
         void ToolHover(Point screenPosition);
         void ToolDrag(Point screenPosition);
         void ToolUp(Point screenPosition);
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
               if (tile == null || !parent.BlockPreview.Enabled) {
                  drawSize = parent.CursorSize;
                  tile = new int[drawSize, drawSize];
                  for (int x = 0; x < drawSize; x++) for (int y = 0; y < drawSize; y++) tile[x, y] = element.Index;
               } else {
                  drawSize = tile.GetLength(0);
               }

               drawPoint = new Point(point.X - point.X % drawSize, point.Y - point.Y % drawSize);
               for (int x = 0; x < drawSize; x++) {
                  for (int y = 0; y < drawSize; y++) {
                     var (xx, yy) = (drawPoint.X + x, drawPoint.Y + y);
                     parent.PixelData[parent.PixelIndex(xx, yy)] = parent.Palette.Elements[tile[x, y]].Color;
                     parent.pixels[xx, yy] = tile[x, y];
                  }
               }
               parent.NotifyPropertyChanged(nameof(PixelData));
            }

            RaiseRefreshSelection();
         }

         public void ToolHover(Point point) {
            point = parent.ToSpriteSpace(point);
            if (parent.WithinImage(point)) {
               var tile = parent.eyeDropperStrategy.Tile;
               if (tile == null || !parent.BlockPreview.Enabled) {
                  drawSize = parent.CursorSize;
               } else {
                  drawSize = tile.GetLength(0);
               }

               drawPoint = new Point(point.X - point.X % drawSize, point.Y - point.Y % drawSize);
            } else {
               drawPoint = default;
               drawSize = 0;
            }

            RaiseRefreshSelection();
         }

         public void ToolUp(Point screenPosition) {
            parent.UpdateSpriteModel();
         }

         private void RaiseRefreshSelection() {
            var selectionPoints = new Point[drawSize * drawSize];
            for (int x = 0; x < drawSize; x++) for (int y = 0; y < drawSize; y++) selectionPoints[y * drawSize + x] = drawPoint + new Point(x, y);
            parent.RaiseRefreshSelection(selectionPoints);
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

            RaiseRefreshSelection();
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

         public static void RaiseRefreshSelection(ImageEditorViewModel parent, Point start, int width, int height) {
            var selectionPoints = new Point[width * height];
            for (int x = 0; x < width; x++) for (int y = 0; y < height; y++) selectionPoints[y * width + x] = start + new Point(x, y);
            parent.RaiseRefreshSelection(selectionPoints);
         }

         private void RaiseRefreshSelection() {
            var (start, width, height) = (selectionStart, selectionWidth, selectionHeight);

            if (parent.withinInteraction && underPixels == null) {
               (start, width, height) = BuildRect(selectionStart, selectionWidth, selectionHeight);
            }

            RaiseRefreshSelection(parent, start, width, height);
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

         public void ToolDrag(Point point) {
            point = parent.ToSpriteSpace(point);
            if (parent.WithinImage(point)) {
               parent.RaiseRefreshSelection(point);
            } else {
               parent.RaiseRefreshSelection();
            }
         }

         public void ToolHover(Point point) {
            point = parent.ToSpriteSpace(point);
            if (parent.WithinImage(point)) {
               parent.RaiseRefreshSelection(point);
            } else {
               parent.RaiseRefreshSelection();
            }
         }

         public void ToolUp(Point point) {
            FillSpace(parent.interactionStart, point);
         }

         private void FillSpace(Point a, Point b) {
            a = parent.ToSpriteSpace(a);
            b = parent.ToSpriteSpace(b);
            int originalColorIndex = parent.pixels[a.X, a.Y];
            var direction = Math.Sign(parent.Palette.SelectionEnd - parent.Palette.SelectionStart);
            var targetColors = new List<int> { parent.Palette.SelectionStart };
            for (int i = parent.Palette.SelectionStart + direction; i != parent.Palette.SelectionEnd; i++) {
               targetColors.Add(i);
            }
            if (parent.Palette.SelectionEnd != parent.Palette.SelectionStart) targetColors.Add(parent.Palette.SelectionEnd);

            var toProcess = new Queue<Point>(new[] { a });
            var processed = new HashSet<Point>();
            while (toProcess.Count > 0) {
               var current = toProcess.Dequeue();
               if (processed.Contains(current)) continue;
               processed.Add(current);
               if (parent.pixels[current.X, current.Y] != originalColorIndex) continue;

               var targetColorIndex = PickColorIndex(a, b, current, targetColors);

               parent.pixels[current.X, current.Y] = targetColorIndex;
               parent.PixelData[parent.PixelIndex(current)] = parent.Palette.Elements[targetColorIndex].Color;
               foreach (var next in new[]{
                  new Point(current.X - 1, current.Y),
                  new Point(current.X + 1, current.Y),
                  new Point(current.X, current.Y - 1),
                  new Point(current.X, current.Y + 1) }
               ) {
                  if (parent.WithinImage(next) && !processed.Contains(next)) toProcess.Enqueue(next);
               }
            }

            parent.UpdateSpriteModel();
            parent.NotifyPropertyChanged(nameof(PixelData));
         }

         private int PickColorIndex(Point a, Point b, Point current, List<int> options) {
            if (a == b) return options[0];

            // a is the center
            // b-a is the radius
            var d = b - a;
            var gradientRadius = Math.Sqrt(d.X * d.X + d.Y * d.Y);
            d = current - a;
            var pointRadius = Math.Sqrt(d.X * d.X + d.Y * d.Y);
            var index = Math.Round(pointRadius / gradientRadius * options.Count);
            return options[(int)Math.Min(index, options.Count - 1)];
         }
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

               var (start, width, height) = SelectionTool.BuildRect(selectionStart, selectionWidth, selectionHeight);

               MakeSquare(ref width, ref height);
               if (selectionHeight < 0) start -= new Point(0, selectionHeight + height - 1);
               if (selectionWidth < 0) start -= new Point(selectionWidth + width - 1, 0);

               SelectionTool.RaiseRefreshSelection(parent, start, width, height);
            }
         }

         public void ToolHover(Point point) {
            parent.RaiseRefreshSelection(parent.ToSpriteSpace(point));
         }

         public void ToolUp(Point point) {
            var (start, width, height) = SelectionTool.BuildRect(selectionStart, selectionWidth, selectionHeight);

            // make sure the selection is a power-of-2 box
            MakeSquare(ref width, ref height);
            if (selectionHeight < 0) start -= new Point(0, selectionHeight + height - 1);
            if (selectionWidth < 0) start -= new Point(selectionWidth + width - 1, 0);
            (selectionStart, selectionWidth, selectionHeight) = (start, width, height);

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
               parent.CursorSize = 0;
               parent.BlockPreview.Set(parent.PixelData, parent.PixelWidth, selectionStart, selectionWidth);
            }
         }

         private void MakeSquare(ref int width, ref int height) {
            width = Math.Min(width, height);
            var log = (int)Math.Log(width, 2);
            width = (int)Math.Pow(2, log);
            height = width;
         }
      }

      #endregion
   }

   public enum ImageEditorTools {
      Pan,        // arrange position
      Select,     // select section
      Draw,       // draw pixel
      Fill,       // fill area
      EyeDropper, // grab color
   }

   public class BlockPreview : ViewModelCore, IPixelViewModel {
      private int width, height;
      public int PixelWidth { get => width; private set => Set(ref width, value); }
      public int PixelHeight { get => height; private set => Set(ref height, value); }

      public short[] PixelData { get; private set; }

      private double scale;
      public double SpriteScale { get => scale; set => Set(ref scale, value); }

      private bool enabled;
      public bool Enabled { get => enabled; private set => Set(ref enabled, value); }

      public void Set(short[] full, int fullWidth, Point start, int size) {
         Enabled = true;
         PixelWidth = PixelHeight = size;

         var data = new short[size * size];
         for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
               data[y * width + x] = full[fullWidth * (start.Y + y) + start.X + x];
            }
         }
         PixelData = data;
         NotifyPropertyChanged(nameof(PixelData));

         SpriteScale = 64 / size;
      }

      public void Clear() {
         Enabled = false;
      }
   }
}
