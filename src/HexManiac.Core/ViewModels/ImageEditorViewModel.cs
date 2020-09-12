using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
      private int spriteAddress;
      private int paletteAddress;
      private int[,] pixels;

      private bool withinInteraction;
      private Point interactionStart;

      #region ITabContent Properties

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
      public ICommand Close => null;
      public event EventHandler<string> OnError;
      public event EventHandler<string> OnMessage;
      public event EventHandler ClearMessage;
      public event EventHandler Closed;
      public event EventHandler<ITabContent> RequestTabChange;
      public event EventHandler<Action> RequestDelayedWork;
      public event EventHandler RequestMenuClose;

      public void RaiseMessage(string message) => OnMessage?.Invoke(this, message);

      #endregion

      private Tools selectedTool;
      public Tools SelectedTool { get => selectedTool; set => TryUpdateEnum(ref selectedTool, value); }

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
      public PaletteCollection Palette { get; set; }

      public ImageEditorViewModel(ChangeHistory<ModelDelta> history, IDataModel model, int address) {
         this.history = history;
         this.model = model;
         var inputRun = model.GetNextRun(address);
         var spriteRun = inputRun as ISpriteRun;
         var palRun = inputRun as IPaletteRun;
         if (spriteRun == null) spriteRun = palRun.FindDependentSprites(model).First();
         if (palRun == null) palRun = spriteRun.FindRelatedPalettes(model).First();
         spriteAddress = spriteRun.Start;
         paletteAddress = palRun.Start;
         pixels = spriteRun.GetPixels(model, 0);

         Render();
         Palette = new PaletteCollection(this, model, history) { SourcePalette = paletteAddress };
         RefreshPaletteColors();
      }

      public void ZoomIn(Point point) {
         if (SpriteScale > 15) return;
         var (x, y) = (point.X, point.Y);
         xOffset -= x;
         yOffset -= y;
         var xPartial  = xOffset / SpriteScale;
         var yPartial = yOffset / SpriteScale;
         SpriteScale += 1;
         XOffset = (int)(xPartial * SpriteScale) + x;
         YOffset = (int)(yPartial * SpriteScale) + y;
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

         if (selectedTool == Tools.Draw) {
            Hover(point);
         } else if (selectedTool == Tools.Pan) {
         } else if (selectedTool == Tools.Fill) {
         } else {
            throw new NotImplementedException();
         }
      }

      public void Hover(Point point) {
         if (!withinInteraction) return;
         if (selectedTool == Tools.Draw) {
            var element = (Palette.Elements.FirstOrDefault(sc => sc.Selected) ?? Palette.Elements[0]);
            point = ToSpriteSpace(point);
            PixelData[PixelIndex(point)] = element.Color;
            pixels[point.X, point.Y] = element.Index;
         } else if (selectedTool == Tools.Pan) {
            var xRange = (int)(PixelWidth * SpriteScale / 2);
            var yRange = (int)(PixelWidth * SpriteScale / 2);
            var (originalX, originalY) = (xOffset, yOffset);
            XOffset = (XOffset + point.X - interactionStart.X).LimitToRange(-xRange, xRange);
            YOffset = (YOffset + point.Y - interactionStart.Y).LimitToRange(-yRange, yRange);
            interactionStart = new Point(interactionStart.X + XOffset - originalX, interactionStart.Y + YOffset - originalY);
         } else if (selectedTool == Tools.Fill) {

         }
      }

      public void ToolUp(Point point) {
         if (selectedTool == Tools.Draw) {
            UpdateSpriteModel();
         } else if (selectedTool == Tools.Fill) {
            FillSpace(interactionStart, point);
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

      public void Refresh() { }

      public int PixelIndex(Point spriteSpace) => spriteSpace.Y * PixelWidth + spriteSpace.X;

      private void WriteImage() {

      }

      private Point ToSpriteSpace(Point point) {
         var x = point.X;
         var y = point.Y;
         x += PixelWidth / 2 - (int)(xOffset / SpriteScale);
         y += PixelHeight / 2 - (int)(yOffset / SpriteScale);
         return new Point(x, y);
      }
      private Point FromSpriteScale(Point spriteScale) {
         var x = spriteScale.X;
         var y = spriteScale.Y;
         x -= PixelWidth / 2 - (int)(xOffset / SpriteScale);
         y -= PixelHeight / 2 - (int)(yOffset / SpriteScale);
         return new Point(x, y);
      }

      private void RefreshPaletteColors() {
         var palRun = (IPaletteRun)model.GetNextRun(paletteAddress);
         Palette.SetContents(palRun.GetPalette(model, 0));
         foreach (var e in Palette.Elements) {
            e.PropertyChanged += (sender, args) => {
               var sc = (SelectableColor)sender;
               switch (args.PropertyName) {
                  case nameof(sc.Selected):
                     if (sc.Selected) {
                        SelectedTool = Tools.Draw;
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
         var spriteRun = (ISpriteRun)model.GetNextRun(spriteAddress);
         var palRun = (IPaletteRun)model.GetNextRun(paletteAddress);

         PixelWidth = spriteRun.SpriteFormat.TileWidth * 8;
         PixelHeight = spriteRun.SpriteFormat.TileHeight * 8;
         PixelData = SpriteTool.Render(pixels, palRun.AllColors(model), palRun.PaletteFormat.InitialBlankPages, 0);
      }

      private void UpdateSpriteModel() {
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

      public enum Tools {
         Pan,        // a
         Select,     // s
         Draw,       // d
         Fill,       // f
         EyeDropper, // e
      }

      #endregion
   }
}
