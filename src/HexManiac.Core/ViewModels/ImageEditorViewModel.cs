using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
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

   public class ImageEditorViewModel : ViewModelCore, ITabContent, IPixelViewModel {
      private readonly ChangeHistory<ModelDelta> history;
      private readonly IDataModel model;
      private int spriteAddress;
      private int paletteAddress;

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

      private double spriteScale;
      public double SpriteScale { get => spriteScale; set => Set(ref spriteScale, value); }

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

         var pixels = spriteRun.GetPixels(model, 0);
         PixelData = SpriteTool.Render(pixels, palRun.AllColors(model), palRun.PaletteFormat.InitialBlankPages, 0);
      }

      public void Hover(Point point) { }
      public void ZoomIn(Point point) { }
      public void ZoomOut(Point point) { }
      public void ToolDown(Point point) { }
      public void ToolUp(Point point) { }
      public void EyeDropperDown(Point point) { }
      public void EyeDropperUp(Point point) { }

      public void Refresh() { }

      private void WriteImage() {

      }

      public enum Tools {
         Pan,        // a
         Select,     // s
         Draw,       // d
         Fill,       // f
         EyeDropper, // e
      }
   }
}
