using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;

namespace HavenSoft.HexManiac.Core.ViewModels {
   // all x/y terms are in 'pixels' from the center of the image viewing area.
   // cursor size is in terms of destination pixels (1x1, 2x2, 4x4, 8x8)
   // cursor sprite position is in terms of the sprite (ranging from 0,0 to width,height)

   public class ImageEditorViewModelCreationException : Exception {
      public ImageEditorViewModelCreationException(string message) : base(message) { }
   }

   public class ImageEditorViewModel : ViewModelCore, ITabContent, IPixelViewModel, IRaiseMessageTab {
      public const int MaxZoom = 24;

      public readonly ChangeHistory<ModelDelta> history;
      public readonly IDataModel model;
      public int[,] pixels;

      public bool withinInteraction, withinDropperInteraction, withinPanInteraction;
      public Point interactionStart;

      public bool[,] selectedPixels;

      #region ITabContent Properties

      public string Name => "Image Editor";
      public string FullFileName { get; }
      public bool SpartanMode { get; set; }
      public bool IsMetadataOnlyChange => false;
      public bool CanDuplicate => false;
      public void Duplicate() { }
      public event EventHandler<string> OnError;
      public event EventHandler<string> OnMessage;
      public event EventHandler ClearMessage;
      public event EventHandler Closed;
      public event EventHandler<TabChangeRequestedEventArgs> RequestTabChange;
      public event EventHandler<Action> RequestDelayedWork;
      public event EventHandler RequestMenuClose;
      public event EventHandler<Direction> RequestDiff;
      event EventHandler<CanPatchEventArgs> ITabContent.RequestCanCreatePatch { add { } remove { } }
      event EventHandler<CanPatchEventArgs> ITabContent.RequestCreatePatch { add { } remove { } }
      public event EventHandler<CanDiffEventArgs> RequestCanDiff;
      event EventHandler ITabContent.RequestRefreshGotoShortcuts { add { } remove { } }

      public bool CanIpsPatchRight => false;
      public bool CanUpsPatchRight => false;
      public void IpsPatchRight() { }
      public void UpsPatchRight() { }

      public void RaiseMessage(string message) => OnMessage?.Invoke(this, message);

      public void RaiseError(string message) => OnError?.Invoke(this, message);

      #endregion

      #region Pages
      public int spritePage, palettePage;
      public int SpritePages => SpritePageOptions.Count;
      public int PalettePages => PalettePageOptions.Count;
      public bool HasMultipleSpritePages => SpritePages > 1;
      public bool HasMultiplePalettePages => PalettePages > 1;
      public ObservableCollection<SelectionViewModel> SpritePageOptions { get; } = new ObservableCollection<SelectionViewModel>();
      public ObservableCollection<SelectionViewModel> PalettePageOptions { get; } = new ObservableCollection<SelectionViewModel>();
      #endregion

      #region EditOptions
      // while the pages section handles a single sprite/palette with multiple available pages,
      // the EditOptions section handles how a single sprite can be rendered with multiple palettes (like pokemon)
      //     or how multiple sprites can be considered 'connected' (like pokemon front/back sprites)

      public bool HasMultipleEditOptions => EditOptions.Count > 1;

      public ObservableCollection<EditOption> EditOptions { get; } = new ObservableCollection<EditOption>();

      public int selectedEditOption;

      #endregion

      #region Tilemap Editing

      public bool CanEditTilePalettes {
         get {
            var spriteAddress = model.ReadPointer(SpritePointer);
            return
               HasMultiplePalettePages &&
               model.GetNextRun(spriteAddress) is ITilemapRun tilemap &&
               tilemap.Start == spriteAddress &&
               model.GetNextRun( tilemap.FindMatchingTileset(model)) is ITilesetRun tileset &&
               tileset.TilesetFormat.BitsPerPixel == 4 &&
               tilemap.BytesPerTile == 2;
         }
      }

      public int TileWidth => PixelWidth / 8;
      public int TileHeight => PixelHeight / 8;
      public double FontSize => SpriteScale * 8;

      public ObservableCollection<int> TilePalettes { get; } = new ObservableCollection<int>();

      public TilePaletteMode tilePaletteMode;
      public TilePaletteMode TilePaletteMode { get => tilePaletteMode; set => SetEnum(ref tilePaletteMode, value); }

      public bool tilePaletteRefreshing;
      public void RefreshTilePalettes() {
         var spriteAddress = model.ReadPointer(SpritePointer);
         if (!(model.GetNextRun(spriteAddress) is ITilemapRun tilemapRun)) return;
         using (Scope(ref tilePaletteRefreshing, true, value => tilePaletteRefreshing = value)) {
            TilePalettes.Clear();
            var runData = tilemapRun.GetTilemapData();
            var pal = ReadPalette();
            for (int i = 0; i < runData.Length / tilemapRun.BytesPerTile; i++) {
               var (paletteIndex, _, _, _) = LzTilemapRun.ReadTileData(runData, i, tilemapRun.BytesPerTile);
               if (tilemapRun.BytesPerTile == 1) paletteIndex = pal.initialBlankPages;
               TilePalettes.Add(paletteIndex);
            }
         }
      }

      #endregion

      public IImageToolStrategy toolStrategy;
      public readonly PanTool panStrategy; // stored separately because of center-click
      public ImageEditorTools selectedTool;
      public BlockPreview BlockPreview { get; }

      public event EventHandler RefreshSelection;

      /// <param name="toSelect">Points range from (0,0) to (PixelWidth, PixelHeight) </param>
      public void RaiseRefreshSelection(params Point[] toSelect) {
         selectedPixels = new bool[PixelWidth, PixelHeight];
         foreach (var s in toSelect) {
            if (WithinImage(s)) selectedPixels[s.X, s.Y] = true;
         }
         RefreshSelection?.Invoke(this, EventArgs.Empty);
      }

      public int xOffset, yOffset, width, height;
      public int XOffset { get => xOffset; set => Set(ref xOffset, value); }
      public int YOffset { get => yOffset; set => Set(ref yOffset, value); }
      public short Transparent => -1;
      public int PixelWidth { get => width; set => Set(ref width, value, old => RaiseRefreshSelection()); }
      public int PixelHeight { get => height; set => Set(ref height, value, old => RaiseRefreshSelection()); }

      public short[] PixelData { get; set; }

      public double spriteScale = 4;
      public double SpriteScale { get => spriteScale; set => Set(ref spriteScale, value, arg => NotifyPropertyChanged(nameof(FontSize))); }

      public PaletteCollection Palette { get; }

      public int SpritePointer { get; set; }
      public int PalettePointer { get; set; }

      public int cursorSize = 1;
      public int CursorSize { get => cursorSize; set => Set(ref cursorSize, value, arg => BlockPreview.Clear()); }

      #region Tileset Editing

      public bool CanEditTilesetWidth { get; set; }
      public int MinimumTilesetWidth { get; set; }
      public int MaximumTilesetWidth { get; set; }

      public int currentTilesetWidth;
      #endregion

      public static (IReadOnlyList<short> colors, int pages, int initialBlankPages) ReadPalette(IDataModel model, int palettePointer, int spriteBits) {
         if (palettePointer == Pointer.NULL) {
            return (TileViewModel.CreateDefaultPalette((int)Math.Pow(2, spriteBits)), 1, 0);
         }

         var paletteAddress = model.ReadPointer(palettePointer);
         var palette = model.GetNextRun(paletteAddress) as IPaletteRun;
         if (palette == null) {
            return (TileViewModel.CreateDefaultPalette((int)Math.Pow(2, spriteBits)), 1, 0);
         }
         return (palette.AllColors(model), palette.Pages, palette.PaletteFormat.InitialBlankPages);
      }

      public (IReadOnlyList<short> colors, int pages, int initialBlankPages) ReadPalette() {
         var sprite = (ISpriteRun)model.GetNextRun(model.ReadPointer(SpritePointer));
         return ReadPalette(model, PalettePointer, sprite.SpriteFormat.BitsPerPixel);
      }

      public int ReadRawPixel(int x, int y) => pixels[x, y];

      // convenience methods
      public void ZoomIn(int x, int y) => ZoomIn(new Point(x, y));
      public void ZoomOut(int x, int y) => ZoomOut(new Point(x, y));
      public void PanDown(int x, int y) => PanDown(new Point(x, y));
      public void PanUp(int x, int y) => PanUp(new Point(x, y));
      public bool ShowSelectionRect(int x, int y) => ShowSelectionRect(new Point(x, y));

      public void ZoomIn(Point point) {
         if (SpriteScale > MaxZoom - 1) return;
         Debug.WriteLine($"Zoom In: {point}");
         var (x, y) = (point.X, point.Y);
         xOffset -= x;
         yOffset -= y;
         var xPartial  = xOffset / SpriteScale;
         var yPartial = yOffset / SpriteScale;
         SpriteScale += 1;
         var xRange = (int)(PixelWidth * SpriteScale / 2);
         var yRange = (int)(PixelHeight * SpriteScale / 2);
         xOffset = (int)(xPartial * SpriteScale) + x;
         yOffset = (int)(yPartial * SpriteScale) + y;
         xOffset = xOffset.LimitToRange(-xRange, xRange);
         yOffset = yOffset.LimitToRange(-yRange, yRange);
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
         var yRange = (int)(PixelHeight * SpriteScale / 2);
         XOffset = ((int)(xPartial * SpriteScale) + x).LimitToRange(-xRange, xRange);
         YOffset = ((int)(yPartial * SpriteScale) + y).LimitToRange(-yRange, yRange);
      }

      public void PanDown(Point point) {
         withinInteraction = withinPanInteraction = true;
         interactionStart = point;
         panStrategy.ToolDown(point, altBehavior: false);
      }

      public void PanUp(Point point) {
         panStrategy.ToolUp(point);
         withinInteraction = withinPanInteraction = false;
      }

      public bool ShowSelectionRect(Point spriteSpace) {
         if (spriteSpace.X < 0 || spriteSpace.X >= PixelWidth || spriteSpace.Y < 0 || spriteSpace.Y >= PixelHeight) return false;
         if (spriteSpace.X >= selectedPixels.GetLength(0) || spriteSpace.Y >= selectedPixels.GetLength(1)) return false;
         return selectedPixels[spriteSpace.X, spriteSpace.Y];
      }

      public int PixelIndex(int x, int y) => PixelIndex(new Point(x, y));
      public int PixelIndex(Point spriteSpace) => spriteSpace.Y * PixelWidth + spriteSpace.X;

      public Point ToSpriteSpace(Point point) {
         var x = point.X;
         var y = point.Y;
         x = (int)Math.Floor((x - xOffset) / SpriteScale) + PixelWidth / 2;
         y = (int)Math.Floor((y - yOffset) / SpriteScale) + PixelHeight / 2;
         return new Point(x, y);
      }

      public Point FromSpriteSpace(Point spriteSpace) {
         var x = spriteSpace.X;
         var y = spriteSpace.Y;
         x = (x - PixelWidth / 2) * (int)SpriteScale + xOffset;
         y = (y - PixelHeight / 2) * (int)SpriteScale + yOffset;
         return new Point(x, y);
      }

      public bool WithinImage(Point p) => p.X >= 0 && p.X < PixelWidth && p.Y >= 0 && p.Y < PixelHeight;

      public bool SpriteOnlyExpects16Colors() {
         var spriteAddress = model.ReadPointer(SpritePointer);
         var spriteRun = (ISpriteRun)model.GetNextRun(spriteAddress);
         if (spriteRun is ITilesetRun tileset && tileset.TilesetFormat.BitsPerPixel == 4) return true;
         if (spriteRun is ITilemapRun) return false;
         if (spriteRun.SpriteFormat.BitsPerPixel < 8 && spriteRun.Pages == 1) return true;
         return false;
      }

      #region Nested Types

      public interface IImageToolStrategy {
         void ToolDown(Point screenPosition, bool altBehavior);
         void ToolHover(Point screenPosition);
         void ToolDrag(Point screenPosition);
         void ToolUp(Point screenPosition);
      }

      public class PanTool : IImageToolStrategy {
         public readonly ImageEditorViewModel parent;
         public PanTool(ImageEditorViewModel parent) => this.parent = parent;

         public void ToolDown(Point screenPosition, bool altBehavior) { }

         public void ToolDrag(Point point) {
            Debug.WriteLine($"Pan: {parent.interactionStart} to {point}");
            var xRange = (int)(parent.PixelWidth * parent.SpriteScale / 2);
            var yRange = (int)(parent.PixelHeight * parent.SpriteScale / 2);
            var (originalX, originalY) = (parent.xOffset, parent.yOffset);
            parent.XOffset = (parent.XOffset + point.X - parent.interactionStart.X).LimitToRange(-xRange, xRange);
            parent.YOffset = (parent.YOffset + point.Y - parent.interactionStart.Y).LimitToRange(-yRange, yRange);
            parent.interactionStart = new Point(parent.interactionStart.X + parent.XOffset - originalX, parent.interactionStart.Y + parent.YOffset - originalY);
         }

         public void ToolHover(Point screenPosition) { }

         public void ToolUp(Point screenPosition) { }

         public bool ShowSelectionRect(Point subPixelPosition) => false;
      }

      public class PaletteCache {
         public IReadOnlyList<short> Colors { get; }
         public int Pages { get; }
         public int InitialBlankPages { get; }
         public PaletteCache(ImageEditorViewModel parent) => (Colors, Pages, InitialBlankPages) = parent.ReadPalette();
      }

      #endregion
   }

   public enum ImageEditorTools {
      Pan,         // arrange position
      Select,      // select section
      Draw,        // draw pixel
      Fill,        // fill area
      EyeDropper,  // grab color
      TilePalette, // draw/eye dropper palettes on tiles
   }

   public enum TilePaletteMode {
      Draw,
      Fill,
      EyeDropper,
   }

   public class BlockPreview : ViewModelCore, IPixelViewModel {
      public short Transparent => -1;

      public int width, height;
      public int PixelWidth { get => width; set => Set(ref width, value); }
      public int PixelHeight { get => height; set => Set(ref height, value); }

      public short[] PixelData { get; set; }

      public double scale;
      public double SpriteScale { get => scale; set => Set(ref scale, value); }

      public bool enabled;
      public bool Enabled { get => enabled; set => Set(ref enabled, value); }

      public void Set(short[] full, int fullWidth, Point start, int width, int height) {
         Enabled = true;
         PixelWidth = width;
         PixelHeight = height;

         var data = new short[width * height];
         for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
               data[y * width + x] = full[fullWidth * (start.Y + y) + start.X + x];
            }
         }
         PixelData = data;
         NotifyPropertyChanged(nameof(PixelData));

         SpriteScale = Math.Max(1, Math.Min(64 / width, 64 / height));
      }

      public void Clear() {
         Enabled = false;
      }
   }

   public class EditOption : ViewModelCore, IPixelViewModel {
      public readonly IDataModel model;

      public short Transparent => -1;
      public int PixelWidth { get; }
      public int PixelHeight { get; }
      public int SpritePointer { get; }
      public int PalettePointer { get; }
      public short[] PixelData { get; set; }

      public double SpriteScale => PixelWidth > 240 || PixelHeight > 160 ? .5 : 1;

      public EditOption(IDataModel model, int spritePointer, int palettePointer) {
         (this.model, SpritePointer, PalettePointer) = (model, spritePointer, palettePointer);
         var spriteAddress = model.ReadPointer(spritePointer);
         var sprite = model.GetNextRun(spriteAddress) as ISpriteRun;

         if (sprite != null) {
            PixelWidth = sprite.SpriteFormat.TileWidth * 8;
            PixelHeight = sprite.SpriteFormat.TileHeight * 8;
         } else {
            PixelData = new short[0];
         }

         Refresh();
      }

      public void Refresh() {
         var spriteAddress = model.ReadPointer(SpritePointer);
         var sprite = model.GetNextRun(spriteAddress) as ISpriteRun;
         if (sprite == null) return;

         var (colors, _, initialBlankPages) = ImageEditorViewModel.ReadPalette(model, PalettePointer, sprite.SpriteFormat.BitsPerPixel);

         var pixels = sprite.GetPixels(model, 0, -1);
         PixelData = SpriteTool.Render(pixels, colors, initialBlankPages, 0);
         NotifyPropertyChanged(nameof(PixelData));
      }
   }
}
