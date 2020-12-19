using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels {
   // all x/y terms are in 'pixels' from the center of the image viewing area.
   // cursor size is in terms of destination pixels (1x1, 2x2, 4x4, 8x8)
   // cursor sprite position is in terms of the sprite (ranging from 0,0 to width,height)

   public class ImageEditorViewModel : ViewModelCore, ITabContent, IPixelViewModel, IRaiseMessageTab {
      public const int MaxZoom = 24;

      private readonly ChangeHistory<ModelDelta> history;
      private readonly IDataModel model;
      private int[,] pixels;

      private bool withinInteraction, withinDropperInteraction, withinPanInteraction;
      private Point interactionStart;

      private bool[,] selectedPixels;

      #region ITabContent Properties

      private StubCommand close, undoWrapper, redoWrapper, pasteCommand, copyCommand, selectAllCommand;

      public string Name => "Image Editor";
      public string FullFileName { get; }
      public ICommand Save { get; }
      public ICommand SaveAs => null;
      public ICommand ExportBackup => null;
      public ICommand Undo => StubCommand(ref undoWrapper, ExecuteUndo, () => history.Undo.CanExecute(default));
      public ICommand Redo => StubCommand(ref redoWrapper, ExecuteRedo, () => history.Redo.CanExecute(default));
      public ICommand Copy => StubCommand<IFileSystem>(ref copyCommand, ExecuteCopy, fs => toolStrategy is SelectionTool selectTool && selectTool.HasSelection);
      public ICommand Paste => StubCommand<IFileSystem>(ref pasteCommand, ExecutePaste, fs => fs.CopyImage.width != 0);
      public ICommand SelectAll => StubCommand(ref selectAllCommand, ExecuteSelectAll, () => true);
      public ICommand DeepCopy => null;
      public ICommand Clear => null;
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
         var selectionStart = Palette.SelectionStart;
         var selectionEnd = Palette.SelectionEnd;

         history.Undo.Execute();
         undoWrapper.RaiseCanExecuteChanged();
         redoWrapper.RaiseCanExecuteChanged();
         Refresh();
         if (HasMultipleEditOptions) EditOptions[SelectedEditOption].Refresh();

         Palette.SelectionStart = selectionStart;
         Palette.SelectionEnd = selectionEnd;
      }

      private void ExecuteRedo() {
         history.Redo.Execute();
         undoWrapper.RaiseCanExecuteChanged();
         redoWrapper.RaiseCanExecuteChanged();
         Refresh();
         if (HasMultipleEditOptions) EditOptions[SelectedEditOption].Refresh();
      }

      private void ExecuteCopy(IFileSystem fs) {
         if (!(toolStrategy is SelectionTool tool)) return;
         tool.Copy(fs);
      }

      private void ExecutePaste(IFileSystem fs) {
         var sprite = fs.CopyImage;
         if (sprite.width == 0) return;
         var height = sprite.image.Length / sprite.width;
         if (height > PixelHeight || sprite.width > PixelWidth) {
            RaiseMessage("Image is too large to paste!");
            return;
         }

         SelectedTool = ImageEditorTools.Select;
         var tool = (SelectionTool)toolStrategy;
         tool.ClearSelection();
         var (x, y) = (PixelWidth / 2 - sprite.width / 2, PixelHeight / 2 - height / 2);
         ToolDown(FromSpriteSpace(new Point(x, y)));
         Hover(FromSpriteSpace(new Point(x + sprite.width - 1, y + height - 1)));
         ToolUp(FromSpriteSpace(new Point(x + sprite.width - 1, y + height - 1)));

         var paletteRun = model.GetNextRun(model.ReadPointer(PalettePointer)) as IPaletteRun;
         var fullPalette = paletteRun.AllColors(model);

         // make insertion more robust
         var newUnderPixels = new int[sprite.width, height];
         for (int xx = 0; xx < sprite.width; xx++) {
            for (int yy = 0; yy < height; yy++) {
               var i = PixelIndex(x + xx, y + yy);
               var targetColor = sprite.image[yy * sprite.width + xx];
               var paletteIndex = fullPalette.IndexOf(targetColor);
               if (paletteIndex < 0) paletteIndex = 0;
               paletteIndex %= Palette.Elements.Count;
               newUnderPixels[xx, yy] = paletteIndex;
            }
         }

         tool.SetUnderPixels(newUnderPixels);
         tool.SwapUnderPixelsWithCurrentPixels();

         UpdateSpriteModel();
         NotifyPropertyChanged(nameof(PixelData));
      }

      private void ExecuteSelectAll() {
         SelectedTool = ImageEditorTools.Select;
         ToolDown(FromSpriteSpace(default));
         Hover(FromSpriteSpace(new Point(PixelWidth - 1, PixelHeight - 1)));
         ToolUp(FromSpriteSpace(new Point(PixelWidth - 1, PixelHeight - 1)));
      }

      #endregion

      #region Pages
      private int spritePage, palettePage;
      public int SpritePage { get => spritePage; set => Set(ref spritePage, value, _ => Refresh()); }
      public int PalettePage { get => palettePage; set => Set(ref palettePage, value, _ => Refresh()); }
      public int SpritePages => SpritePageOptions.Count;
      public int PalettePages => PalettePageOptions.Count;
      public bool HasMultipleSpritePages => SpritePages > 1;
      public bool HasMultiplePalettePages => PalettePages > 1;
      public ObservableCollection<SelectionViewModel> SpritePageOptions { get; } = new ObservableCollection<SelectionViewModel>();
      public ObservableCollection<SelectionViewModel> PalettePageOptions { get; } = new ObservableCollection<SelectionViewModel>();
      private void SetupPageOptions() {
         int spritePages = ((ISpriteRun)model.GetNextRun(model.ReadPointer(SpritePointer))).Pages;
         SpritePageOptions.Clear();
         for (int i = 0; i < spritePages; i++) {
            var option = new SelectionViewModel { Selected = i == spritePage, Name = i.ToString(), Index = i };
            option.Bind(nameof(option.Selected), (sender, e) => { if (sender.Selected) SpritePage = sender.Index; });
            SpritePageOptions.Add(option);
         }
         NotifyPropertyChanged(nameof(SpritePages));
         NotifyPropertyChanged(nameof(HasMultipleSpritePages));

         var (_, palPages, initialBlankPalettePages) = ReadPalette();
         PalettePageOptions.Clear();
         for (int i = 0; i < palPages; i++) {
            var option = new SelectionViewModel { Selected = i == palettePage, Name = i.ToString(), Index = i };
            option.Bind(nameof(option.Selected), (sender, e) => { if (sender.Selected) PalettePage = sender.Index; });
            PalettePageOptions.Add(option);
         }
         if (initialBlankPalettePages != 0) {
            var option = new SelectionViewModel { Selected = -initialBlankPalettePages == palettePage, Name = "default" + Environment.NewLine + "Colors from this page are not recommended, but may be needed for transparency.", Index = -initialBlankPalettePages };
            option.Bind(nameof(option.Selected), (sender, e) => { if (sender.Selected) PalettePage = sender.Index; });
            PalettePageOptions.Insert(0, option);
         }
         NotifyPropertyChanged(nameof(PalettePages));
         NotifyPropertyChanged(nameof(HasMultiplePalettePages));
      }
      #endregion

      #region EditOptions
      // while the pages section handles a single sprite/palette with multiple available pages,
      // the EditOptions section handles how a single sprite can be rendered with multiple palettes (like pokemon)
      //     or how multiple sprites can be considered 'connected' (like pokemon front/back sprites)

      public bool HasMultipleEditOptions => EditOptions.Count > 1;

      public ObservableCollection<EditOption> EditOptions { get; } = new ObservableCollection<EditOption>();

      private int selectedEditOption;
      public int SelectedEditOption { get => selectedEditOption; set => Set(ref selectedEditOption, value, SelectedEditOptionChanged); }

      private void SelectedEditOptionChanged(int oldValue) {
         if (SelectedEditOption == -1) Set(ref selectedEditOption, oldValue, nameof(SelectedEditOption));
         var option = EditOptions[SelectedEditOption.LimitToRange(0, EditOptions.Count - 1)];
         SpritePointer = option.SpritePointer;
         PalettePointer = option.PalettePointer;
         PixelWidth = option.PixelWidth;
         PixelHeight = option.PixelHeight;
         NotifyPropertyChanged(nameof(SpritePointer));
         NotifyPropertyChanged(nameof(PalettePointer));
         NotifyPropertyChanged(nameof(PixelWidth));
         NotifyPropertyChanged(nameof(PixelHeight));
         Refresh();
         SetupPageOptions();
      }

      private void InitializeEditOptions() {
         EditOptions.Clear();
         var currentTable = model.GetNextRun(SpritePointer) as ArrayRun;
         if (currentTable == null) {
            EditOptions.Add(new EditOption(model, SpritePointer, PalettePointer));
            NotifyPropertyChanged(nameof(HasMultipleEditOptions));
            SelectedEditOption = 0;
            return;
         }

         var offset = currentTable.ConvertByteOffsetToArrayOffset(SpritePointer);
         foreach (var table in model.GetRelatedArrays(currentTable)) {
            if (!(table.ElementContent[0] is ArrayRunPointerSegment pSegment)) continue;
            var spritePointer = table.Start + table.ElementLength * offset.ElementIndex;
            var spriteAddress = model.ReadPointer(spritePointer);
            var spriteRun = model.GetNextRun(spriteAddress) as ISpriteRun;
            if (spriteRun == null || spriteRun.Start != spriteAddress || spriteRun.FormatString != pSegment.InnerFormat) continue;
            foreach (var palette in spriteRun.FindRelatedPalettes(model, spritePointer, includeAllTableIndex: true)) {
               EditOptions.Add(new EditOption(model, spritePointer, palette.PointerSources[0]));
            }
            if (spriteRun.SpriteFormat.BitsPerPixel < 4) {
               EditOptions.Add(new EditOption(model, spritePointer, Pointer.NULL));
            }
         }

         NotifyPropertyChanged(nameof(HasMultipleEditOptions));
         SelectedEditOption = 0;
      }

      #endregion

      #region Orient Selected Data Commands

      private StubCommand flipVerticalCommand, flipHorizontalCommand;

      public ICommand FlipVertical => StubCommand(ref flipVerticalCommand, ExecuteFlipVertical, CanExecuteOrientSelectedPixels);
      public ICommand FlipHorizontal => StubCommand(ref flipHorizontalCommand, ExecuteFlipHorizontal, CanExecuteOrientSelectedPixels);

      private bool CanExecuteOrientSelectedPixels() {
         if (!(toolStrategy is SelectionTool tool)) return false;
         return tool.HasSelection;
      }

      private void ExecuteFlipVertical() {
         if (!(toolStrategy is SelectionTool tool)) return;
         tool.FlipVertical();
         UpdateSpriteModel();
         Render();
      }

      private void ExecuteFlipHorizontal() {
         if (!(toolStrategy is SelectionTool tool)) return;
         tool.FlipHorizontal();
         UpdateSpriteModel();
         Render();
      }

      #endregion

      #region Tilemap Editing

      public bool CanEditTilePalettes {
         get {
            var spriteAddress = model.ReadPointer(SpritePointer);
            return HasMultiplePalettePages && model.GetNextRun(spriteAddress) is LzTilemapRun tilemap && tilemap.Start == spriteAddress;
         }
      }

      public int TileWidth => PixelWidth / 8;
      public int TileHeight => PixelHeight / 8;
      public double FontSize => SpriteScale * 8;

      public ObservableCollection<int> TilePalettes { get; } = new ObservableCollection<int>();

      private void RefreshTilePalettes() {
         var spriteAddress = model.ReadPointer(SpritePointer);
         if (!(model.GetNextRun(spriteAddress) is LzTilemapRun lzTilemapRun)) return;
         TilePalettes.Clear();
         var lzRunData = LZRun.Decompress(model, spriteAddress);
         for (int i = 0; i < lzRunData.Length / 2; i++) {
            var (paletteIndex, _, _, _) = LzTilemapRun.ReadTileData(lzRunData, i, lzTilemapRun.BytesPerTile);
            TilePalettes.Add(paletteIndex);
         }
      }

      private void PushTilePalettesToModel() {
         var spriteAddress = model.ReadPointer(SpritePointer);
         if (!(model.GetNextRun(spriteAddress) is LzTilemapRun tilemapRun)) return;
         var lzRunData = LZRun.Decompress(model, spriteAddress);
         for (int i = 0; i < lzRunData.Length / 2; i++) {
            var (paletteIndex, hFlip, vFlip, tileIndex) = LzTilemapRun.ReadTileData(lzRunData, i, tilemapRun.BytesPerTile);
            paletteIndex = TilePalettes[i];
            LzTilemapRun.WriteTileData(lzRunData, i, paletteIndex, hFlip, vFlip, tileIndex);
         }
         tilemapRun.ReplaceData(lzRunData, history.CurrentChange);
      }

      #endregion

      private IImageToolStrategy toolStrategy;
      private EyeDropperTool eyeDropperStrategy; // stored separately because of right-click
      private readonly PanTool panStrategy; // stored separately because of center-click
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
                            : SelectedTool == ImageEditorTools.TilePalette ? new TilePaletteTool(this)
                            : (IImageToolStrategy)default;
               RaiseRefreshSelection();
            }
         }
      }
      private StubCommand selectTool, selectColor, zoomInCommand, zoomOutCommand, deleteCommand;
      public ICommand SelectTool => StubCommand<ImageEditorTools>(ref selectTool, arg => {
         if (arg == ImageEditorTools.TilePalette) {
            var spriteAddress = model.ReadPointer(SpritePointer);
            if (!(model.GetNextRun(spriteAddress) is LzTilemapRun)) return;
         }
         SelectedTool = arg;
      });
      public ICommand SelectColor => StubCommand<string>(ref selectColor, arg => Palette.SelectionStart = int.Parse(arg));
      public ICommand ZoomInCommand => StubCommand(ref zoomInCommand, () => ZoomIn(0, 0));
      public ICommand ZoomOutCommand => StubCommand(ref zoomOutCommand, () => ZoomOut(0, 0));
      public ICommand DeleteCommand => StubCommand(ref deleteCommand, () => DeleteSelection(), () => toolStrategy is SelectionTool selector && selector.HasSelection);

      public BlockPreview BlockPreview { get; }

      public event EventHandler RefreshSelection;

      /// <param name="toSelect">Points range from (0,0) to (PixelWidth, PixelHeight) </param>
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
      public int PixelWidth { get => width; private set => Set(ref width, value, old => RaiseRefreshSelection()); }
      public int PixelHeight { get => height; private set => Set(ref height, value, old => RaiseRefreshSelection()); }

      public short[] PixelData { get; private set; }

      private double spriteScale = 4;
      public double SpriteScale { get => spriteScale; set => Set(ref spriteScale, value, arg => NotifyPropertyChanged(nameof(FontSize))); }

      public PaletteCollection Palette { get; }

      public int SpritePointer { get; private set; }
      public int PalettePointer { get; private set; }

      private StubCommand setCursorSize;
      public ICommand SetCursorSize => StubCommand<string>(ref setCursorSize, arg => CursorSize = int.Parse(arg));
      private int cursorSize = 1;
      public int CursorSize { get => cursorSize; set => Set(ref cursorSize, value, arg => BlockPreview.Clear()); }

      #region Tileset Editing

      public bool CanEditTilesetWidth { get; private set; }
      public int MinimumTilesetWidth { get; private set; }
      public int MaximumTilesetWidth { get; private set; }

      private int currentTilesetWidth;
      public int CurrentTilesetWidth {
         get => currentTilesetWidth;
         set => Set(ref currentTilesetWidth, value, old => Refresh());
      }

      private void SetupTilesetWidthControl() {
         var tileset = model.GetNextRun(model.ReadPointer(SpritePointer)) as LzTilesetRun;
         if (tileset == null) {
            CanEditTilesetWidth = false;
            return;
         }

         CanEditTilesetWidth = true;
         var defaultTileWidth = tileset.Width;
         if (defaultTileWidth > 1) {
            MinimumTilesetWidth = 2;
            MaximumTilesetWidth = defaultTileWidth * tileset.Height / 2;
            if (CurrentTilesetWidth == 0) Set(ref currentTilesetWidth, defaultTileWidth, nameof(CurrentTilesetWidth));
         }
      }

      #endregion

      public ImageEditorViewModel(ChangeHistory<ModelDelta> history, IDataModel model, int address, ICommand save = null) {
         this.history = history;
         this.model = model;
         using (ModelCacheScope.CreateScope(model)) FullFileName = ViewPort.BuildElementName(model, address);
         Save = save;
         this.toolStrategy = this.panStrategy = new PanTool(this);
         this.eyeDropperStrategy = new EyeDropperTool(this);
         var inputRun = model.GetNextRun(address);
         var spriteRun = inputRun as ISpriteRun;
         var palRun = inputRun as IPaletteRun;
         if (spriteRun == null) spriteRun = palRun.FindDependentSprites(model).First();
         if (palRun == null) palRun = spriteRun.FindRelatedPalettes(model).FirstOrDefault();
         SpritePointer = spriteRun.PointerSources[0];
         PalettePointer = palRun?.PointerSources[0] ?? Pointer.NULL;
         Palette = new PaletteCollection(this, model, history) {
            SpriteBitsPerPixel = spriteRun.SpriteFormat.BitsPerPixel,
            SourcePalettePointer = PalettePointer,
         };
         Palette.Bind(nameof(Palette.HoverIndex), UpdateSelectionFromPaletteHover);
         InitializeEditOptions();
         Refresh();
         selectedPixels = new bool[PixelWidth, PixelHeight];
         BlockPreview = new BlockPreview();
         SetupPageOptions();
         Palette.SelectionSet += (sender, e) => BlockPreview.Clear();
         RefreshTilePalettes();
         TilePalettes.CollectionChanged += (sender, e) => PushTilePalettesToModel();
      }

      public static (IReadOnlyList<short> colors, int pages, int initialBlankPages) ReadPalette(IDataModel model, int palettePointer, int spriteBits) {
         if (palettePointer == Pointer.NULL) {
            return (TileViewModel.CreateDefaultPalette((int)Math.Pow(2, spriteBits)), 1, 0);
         }

         var paletteAddress = model.ReadPointer(palettePointer);
         var palette = model.GetNextRun(paletteAddress) as IPaletteRun;
         return (palette.AllColors(model), palette.Pages, palette.PaletteFormat.InitialBlankPages);
      }

      public (IReadOnlyList<short> colors, int pages, int initialBlankPages) ReadPalette() {
         var sprite = (ISpriteRun)model.GetNextRun(model.ReadPointer(SpritePointer));
         return ReadPalette(model, PalettePointer, sprite.SpriteFormat.BitsPerPixel);
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

      public void DeleteSelection() {
         if (!(toolStrategy is SelectionTool selector)) return;
         if (!selector.HasSelection) return;
         selector.SwapUnderPixelsWithCurrentPixels();
         selector.ClearSelection();
         SelectionTool.RaiseRefreshSelection(this, default, 0, 0);
         UpdateSpriteModel();
         NotifyPropertyChanged(nameof(PixelData));
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
         undoWrapper.RaiseCanExecuteChanged();
         if (HasMultipleEditOptions) EditOptions[SelectedEditOption].Refresh();
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

      public bool ShowSelectionRect(Point spriteSpace) {
         if (spriteSpace.X < 0 || spriteSpace.X >= PixelWidth || spriteSpace.Y < 0 || spriteSpace.Y >= PixelHeight) return false;
         if (spriteSpace.X >= selectedPixels.GetLength(0) || spriteSpace.Y >= selectedPixels.GetLength(1)) return false;
         return selectedPixels[spriteSpace.X, spriteSpace.Y];
      }

      public void Refresh() {
         var spriteAddress = model.ReadPointer(SpritePointer);
         var spriteRun = (ISpriteRun)model.GetNextRun(spriteAddress);
         if (SpritePage >= spriteRun.Pages) SpritePage = spriteRun.Pages - 1;
         SetupTilesetWidthControl();

         // tilemap may have been repointed: recalculate
         if (spriteRun is LzTilemapRun tilemapRun) tilemapRun.FindMatchingTileset(model);

         pixels = (spriteRun is LzTilesetRun tsRun) ? tsRun.GetPixels(model, SpritePage, CurrentTilesetWidth) : spriteRun.GetPixels(model, SpritePage);
         Render();
         RefreshPaletteColors();
         SetupPageOptions();
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

      private Point FromSpriteSpace(Point spriteSpace) {
         var x = spriteSpace.X;
         var y = spriteSpace.Y;
         x = (x - PixelWidth / 2) * (int)SpriteScale + xOffset;
         y = (y - PixelHeight / 2) * (int)SpriteScale + yOffset;
         return new Point(x, y);
      }

      private void RefreshPaletteColors() {
         var palRun = ReadPalette();
         Palette.SourcePalettePointer = PalettePointer;
         Palette.Page = PalettePage;
         var desiredCount = (int)Math.Pow(2, Palette.SpriteBitsPerPixel);
         IReadOnlyList<short> palette = TileViewModel.CreateDefaultPalette(desiredCount);
         if (palRun.colors.Count > 16 && palRun.colors.Count < 256) palRun.colors = palRun.colors.Skip(Math.Max(0, palettePage) * 16).Take(16).ToArray();
         Palette.SetContents(palRun.colors);
         Palette.HasMultiplePages = palRun.pages > 1;
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
                     Palette.Page = palettePage;
                     Palette.PushColorsToModel(); // this causes a Render
                     break;
               }
            };
         }
      }

      private bool WithinImage(Point p) => p.X >= 0 && p.X < PixelWidth && p.Y >= 0 && p.Y < PixelHeight;

      private void Render() {
         var spriteAddress = model.ReadPointer(SpritePointer);
         var spriteRun = (ISpriteRun)model.GetNextRun(spriteAddress);
         var readPixels = (spriteRun is LzTilesetRun tsRun) ? tsRun.GetPixels(model, SpritePage, CurrentTilesetWidth) : spriteRun.GetPixels(model, SpritePage);

         var palRun = ReadPalette();

         PixelWidth = readPixels.GetLength(0);
         PixelHeight = readPixels.GetLength(1);
         if (palettePage >= palRun.pages) PalettePage = palRun.pages - 1;
         var renderPage = palettePage;
         if (spriteRun.SpriteFormat.BitsPerPixel == 8 || spriteRun is LzTilemapRun) renderPage = 0;
         PixelData = SpriteTool.Render(pixels, palRun.colors, palRun.initialBlankPages, renderPage);
         NotifyPropertyChanged(nameof(PixelData));

         if (HasMultipleEditOptions) EditOptions[SelectedEditOption].Refresh();
      }

      private void UpdateSpriteModel() {
         var spriteAddress = model.ReadPointer(SpritePointer);
         var spriteRun = (ISpriteRun)model.GetNextRun(spriteAddress);

         // tilemap may have been repointed: recalculate
         if (spriteRun is LzTilemapRun tilemapRun) tilemapRun.FindMatchingTileset(model);

         var newRun = spriteRun.SetPixels(model, history.CurrentChange, SpritePage, pixels);
         if (newRun.Start != spriteRun.Start) RaiseMessage("Sprite was move to " + newRun.Start.ToAddress());
      }

      /// <summary>
      /// Given an index of a color within a palette page, get the pixel value that contains both the page and index information.
      /// If no page is given, the current selected page is used
      /// </summary>
      private int ColorIndex(int paletteIndex, int page = int.MinValue) {
         if (page == int.MinValue) page = PalettePage;
         var blankPages = ReadPalette().initialBlankPages;
         var pageOffset = (blankPages + page) << 4;
         return paletteIndex + pageOffset;
      }

      /// <summary>
      /// Given a pixel including a palette page and color index, return just the index within that palette page (assuming the selected page).
      /// </summary>
      private int PaletteIndex(int colorIndex, int page = int.MinValue) {
         if (page == int.MinValue) page = PalettePage;
         var blankPages = ReadPalette().initialBlankPages;
         var pageOffset = (blankPages + page) << 4;
         return colorIndex - pageOffset;
      }

      private void UpdateSelectionFromPaletteHover(PaletteCollection sender, PropertyChangedEventArgs e) {
         int paletteStart = ReadPalette().initialBlankPages * 16;
         paletteStart += PalettePage * 16;
         var matches = new List<Point>();
         if (Palette.HoverIndex >= 0) {
            for (int x = 0; x < PixelWidth; x++) {
               for (int y = 0; y < PixelHeight; y++) {
                  if (pixels[x, y] != Palette.HoverIndex + paletteStart) continue;
                  matches.Add(new Point(x, y));
               }
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
         private int drawWidth, drawHeight;

         public DrawTool(ImageEditorViewModel parent) => this.parent = parent;

         public void ToolDown(Point point) {
            ToolDrag(point);
         }

         public void ToolDrag(Point point) {
            Debug.WriteLine($"Draw: {point}");
            var element = (parent.Palette.Elements.FirstOrDefault(sc => sc.Selected) ?? parent.Palette.Elements[0]);
            point = parent.ToSpriteSpace(point);

            bool validHoverLocation = parent.WithinImage(point);
            if (validHoverLocation) {
               var initialBlankPages = parent.ReadPalette().initialBlankPages;
               if (parent.CanEditTilePalettes) {
                  var hoverTilesPalette = parent.TilePalettes[point.Y / 8 * parent.TileWidth + point.X / 8];
                  validHoverLocation = initialBlankPages + parent.PalettePage == hoverTilesPalette;
                  validHoverLocation &= parent.palettePage >= 0;
               }
            }

            if (validHoverLocation) {
               var tile = parent.eyeDropperStrategy.Tile;
               if (tile == null || !parent.BlockPreview.Enabled) {
                  drawWidth = drawHeight = parent.CursorSize;
                  tile = new int[drawWidth, drawHeight];
                  var colorIndex = parent.ColorIndex(element.Index);
                  for (int x = 0; x < drawWidth; x++) for (int y = 0; y < drawHeight; y++) tile[x, y] = colorIndex;
               } else {
                  drawWidth = tile.GetLength(0);
                  drawHeight = tile.GetLength(1);
               }

               drawPoint = new Point(point.X - point.X % drawWidth, point.Y - point.Y % drawHeight);

               // allow editing the selected palette to match the tile if a tile is selected
               var pageChange = (int)Math.Floor((float) parent.PaletteIndex(tile[0, 0]) / parent.Palette.Elements.Count);
               if (drawWidth == 8 && drawHeight == 8 && pageChange != 0) {
                  parent.PalettePage += pageChange;
                  pageChange = 0;
               }

               // only draw if the paletteIndex is reasonable
               if (pageChange == 0) {
                  for (int x = 0; x < drawWidth; x++) {
                     for (int y = 0; y < drawHeight; y++) {
                        var (xx, yy) = (drawPoint.X + x, drawPoint.Y + y);
                        var paletteIndex = parent.PaletteIndex(tile[x, y]);
                        if (xx >= parent.PixelWidth || yy >= parent.PixelHeight) continue;
                        parent.PixelData[parent.PixelIndex(xx, yy)] = parent.Palette.Elements[paletteIndex].Color;
                        parent.pixels[xx, yy] = tile[x, y];
                     }
                  }
                  parent.NotifyPropertyChanged(nameof(PixelData));
               }
            }

            RaiseRefreshSelection();
         }

         public void ToolHover(Point point) {
            point = parent.ToSpriteSpace(point);
            bool validHoverLocation = parent.WithinImage(point);
            if (validHoverLocation) {
               if (parent.CanEditTilePalettes && parent.model.GetNextRun(parent.model.ReadPointer(parent.PalettePointer)) is IPaletteRun palRun) {
                  var hoverTilesPalette = parent.TilePalettes[point.Y / 8 * parent.TileWidth + point.X / 8];
                  validHoverLocation = parent.CursorSize == 8 || palRun.PaletteFormat.InitialBlankPages + parent.PalettePage == hoverTilesPalette;
               }
            }

            if (validHoverLocation) {
               var tile = parent.eyeDropperStrategy.Tile;
               if (tile == null || !parent.BlockPreview.Enabled) {
                  drawWidth = drawHeight = Math.Max(parent.CursorSize, 1);
               } else {
                  drawWidth = tile.GetLength(0);
                  drawHeight = tile.GetLength(1);
               }

               drawPoint = new Point(point.X - point.X % drawWidth, point.Y - point.Y % drawHeight);
            } else {
               drawPoint = default;
               drawWidth = drawHeight = 0;
            }

            RaiseRefreshSelection();
         }

         public void ToolUp(Point screenPosition) {
            parent.UpdateSpriteModel();
         }

         private void RaiseRefreshSelection() {
            var selectionPoints = new Point[drawWidth * drawHeight];
            for (int x = 0; x < drawWidth; x++) for (int y = 0; y < drawHeight; y++) selectionPoints[y * drawWidth + x] = drawPoint + new Point(x, y);
            parent.RaiseRefreshSelection(selectionPoints);
         }
      }

      private class SelectionTool : IImageToolStrategy {
         private readonly ImageEditorViewModel parent;

         private Point selectionStart;
         private int selectionWidth, selectionHeight;
         private int[,] underPixels; // the pixels that are 'under' the current selection. As the selection moves, this changes.

         public bool HasSelection => underPixels != null;

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
               var palRun = parent.ReadPalette();
               var maxReasonablePage = palRun.pages + palRun.initialBlankPages;
               var delta = currentPoint - previousPoint;
               delta = new Point(
                  delta.X.LimitToRange(-selectionStart.X, parent.PixelWidth - selectionWidth - selectionStart.X),
                  delta.Y.LimitToRange(-selectionStart.Y, parent.PixelHeight - selectionHeight - selectionStart.Y));

               if (parent.CanEditTilePalettes) {
                  for (int x = 0; x < selectionWidth; x += 8) {
                     var tileX = (selectionStart.X + delta.X + x) / 8;
                     for (int y = 0; y < selectionHeight; y += 8) {
                        var tileY = (selectionStart.Y + delta.Y + y) / 8;
                        var currentPalette = parent.TilePalettes[tileY * parent.TileWidth + tileX];
                        if (currentPalette >= maxReasonablePage) return;
                     }
                  }
               }

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
               if (selectionWidth > 1 || selectionHeight > 1) {
                  underPixels = new int[selectionWidth, selectionHeight];
               } else {
                  selectionWidth = selectionHeight = 0;
               }
               parent.flipHorizontalCommand.RaiseCanExecuteChanged();
               parent.flipVerticalCommand.RaiseCanExecuteChanged();
            }

            RaiseRefreshSelection(parent, selectionStart, selectionWidth, selectionHeight);
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

         public void ClearSelection() {
            underPixels = null;
            selectionWidth = selectionHeight = 0;
         }

         public void Copy(IFileSystem fs) {
            if (underPixels == null) return;
            var result = new short[selectionWidth * selectionHeight];
            for (int x = 0; x < selectionWidth; x++) {
               for (int y = 0; y < selectionHeight; y++) {
                  var index = parent.PixelIndex(selectionStart + new Point(x, y));
                  result[y * selectionWidth + x] = parent.PixelData[index];
               }
            }
            fs.CopyImage = (result, selectionWidth);
         }

         private void RaiseRefreshSelection() {
            var (start, width, height) = (selectionStart, selectionWidth, selectionHeight);

            if (parent.withinInteraction && underPixels == null) {
               (start, width, height) = BuildRect(selectionStart, selectionWidth, selectionHeight);
            }

            RaiseRefreshSelection(parent, start, width, height);
         }

         public void SwapUnderPixelsWithCurrentPixels() {
            var (fullPalette, _, pageOffset) = parent.ReadPalette();

            for (int x = 0; x < selectionWidth; x++) {
               for (int y = 0; y < selectionHeight; y++) {
                  var (xx, yy) = (selectionStart.X + x, selectionStart.Y + y);

                  var page = 0;
                  if (parent.CanEditTilePalettes) {
                     var (pX, pY) = (xx / 8, yy / 8);
                     page = parent.TilePalettes[pY * parent.TileWidth + pX] - pageOffset;
                  }

                  var newUnder = parent.PaletteIndex(parent.pixels[xx, yy], page);
                  var newOver = parent.ColorIndex(underPixels[x, y], page);
                  var index = Math.Max(0, newOver - pageOffset * 16);
                  if (parent.CanEditTilesetWidth) {
                     // tilesets don't have palette information
                     newUnder = parent.pixels[xx, yy];
                     newOver = underPixels[x, y];
                     index = newOver;
                  }

                  underPixels[x, y] = newUnder;
                  parent.pixels[xx, yy] = newOver;

                  if (index < fullPalette.Count) {
                     var color = fullPalette[index];
                     parent.PixelData[parent.PixelIndex(xx, yy)] = color;
                  }
               }
            }
         }

         public void FlipVertical() {
            var cache = CachePixels();
            var paletteRun = parent.model.GetNextRun(parent.model.ReadPointer(parent.PalettePointer)) as IPaletteRun;
            var pageOffset = (paletteRun?.PaletteFormat.InitialBlankPages) ?? 0;
            int inputPage = 0, outputPage = 0;

            for (int x = 0; x < selectionWidth; x++) {
               for (int y = 0; y < selectionHeight; y++) {
                  if (parent.CanEditTilePalettes) {
                     var (pX, pY) = ((selectionStart.X + x) / 8, (selectionStart.Y + selectionHeight - y - 1) / 8);
                     inputPage = parent.TilePalettes[pY * parent.TileWidth + pX] - pageOffset;
                     (pX, pY) = ((selectionStart.X + x) / 8, (selectionStart.Y + y) / 8);
                     outputPage = parent.TilePalettes[pY * parent.TileWidth + pX] - pageOffset;
                  }

                  var index = parent.PaletteIndex(cache[x, selectionHeight - y - 1], inputPage);
                  parent.pixels[selectionStart.X + x, selectionStart.Y + y] = parent.ColorIndex(index, outputPage);
               }
            }
         }

         public void FlipHorizontal() {
            var cache = CachePixels();
            var paletteRun = parent.model.GetNextRun(parent.model.ReadPointer(parent.PalettePointer)) as IPaletteRun;
            var pageOffset = (paletteRun?.PaletteFormat.InitialBlankPages) ?? 0;
            int inputPage = 0, outputPage = 0;

            for (int x = 0; x < selectionWidth; x++) {
               for (int y = 0; y < selectionHeight; y++) {
                  if (parent.CanEditTilePalettes) {
                     var (pX, pY) = ((selectionStart.X + selectionWidth - x - 1) / 8, (selectionStart.Y + y) / 8);
                     inputPage = parent.TilePalettes[pY * parent.TileWidth + pX] - pageOffset;
                     (pX, pY) = ((selectionStart.X + x) / 8, (selectionStart.Y + y) / 8);
                     outputPage = parent.TilePalettes[pY * parent.TileWidth + pX] - pageOffset;
                  }

                  var index = parent.PaletteIndex(cache[selectionWidth - x - 1, y], inputPage);
                  parent.pixels[selectionStart.X + x, selectionStart.Y + y] = parent.ColorIndex(index, outputPage);
               }
            }
         }

         public void SetUnderPixels(int[,] values) {
            Debug.Assert(underPixels.GetLength(0) == values.GetLength(0));
            Debug.Assert(underPixels.GetLength(1) == values.GetLength(1));
            underPixels = values;
         }

         private int[,] CachePixels() {
            var cache = new int[selectionWidth, selectionHeight];
            for (int x = 0; x < selectionWidth; x++) {
               for (int y = 0; y < selectionHeight; y++) {
                  cache[x, y] = parent.pixels[selectionStart.X + x, selectionStart.Y + y];
               }
            }
            return cache;
         }
      }

      private class PanTool : IImageToolStrategy {
         private readonly ImageEditorViewModel parent;
         public PanTool(ImageEditorViewModel parent) => this.parent = parent;

         public void ToolDown(Point screenPosition) { }

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
            if (!parent.WithinImage(a) || !parent.WithinImage(b)) return;
            var paletteInfo = parent.ReadPalette();
            int pageStart = paletteInfo.initialBlankPages * 16;
            pageStart += parent.PalettePage * 16;
            int originalColorIndex = parent.pixels[a.X, a.Y];
            var originalPaletteIndex = parent.PaletteIndex(originalColorIndex);
            if (originalPaletteIndex < 0 || originalPaletteIndex >= parent.Palette.Elements.Count) return;
            if (parent.PalettePage < 0) return;
            var direction = Math.Sign(parent.Palette.SelectionEnd - parent.Palette.SelectionStart);
            var targetColors = new List<int> { parent.Palette.SelectionStart };
            for (int i = parent.Palette.SelectionStart + direction; i != parent.Palette.SelectionEnd; i += direction) {
               targetColors.Add(i);
            }
            if (parent.Palette.SelectionEnd != parent.Palette.SelectionStart) targetColors.Add(parent.Palette.SelectionEnd);
            var targetColorsWithinPalettePage = targetColors.Select(tc => parent.ColorIndex(tc)).ToList();

            var toProcess = new Queue<Point>(new[] { a });
            var processed = new HashSet<Point>();
            while (toProcess.Count > 0) {
               var current = toProcess.Dequeue();
               if (processed.Contains(current)) continue;
               processed.Add(current);
               if (parent.pixels[current.X, current.Y] != originalColorIndex) continue;

               var targetColorIndex = PickColorIndex(a, b, current, targetColors);
               var targetColorWithinPalettePageIndex = PickColorIndex(a, b, current, targetColorsWithinPalettePage);

               parent.pixels[current.X, current.Y] = targetColorWithinPalettePageIndex;
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
            if (parent.WithinImage(point) && !parent.CanEditTilePalettes) {
               selectionWidth = point.X - selectionStart.X;
               selectionHeight = point.Y - selectionStart.Y;

               var (start, width, height) = SelectionTool.BuildRect(selectionStart, selectionWidth, selectionHeight);

               // MakeSquare(ref width, ref height);
               if (selectionHeight < 0) start -= new Point(0, selectionHeight + height - 1);
               if (selectionWidth < 0) start -= new Point(selectionWidth + width - 1, 0);

               SelectionTool.RaiseRefreshSelection(parent, start, width, height);
            }
         }

         public void ToolHover(Point point) {
            selectionStart = parent.ToSpriteSpace(point);
            RaiseRefreshSelection();
         }

         public void ToolUp(Point point) {
            var (start, width, height) = SelectionTool.BuildRect(selectionStart, selectionWidth, selectionHeight);

            if (parent.selectedTool == ImageEditorTools.TilePalette) {
               point = parent.ToSpriteSpace(point);
               var lineNumber = point.Y / 8;
               var lineTileWidth = parent.PixelWidth / 8;
               var rowNumber = point.X / 8;
               var tileIndex = lineNumber * lineTileWidth + rowNumber;
               var desiredPalettePage = parent.TilePalettes[tileIndex];
               var paletteAddress = parent.model.ReadPointer(parent.PalettePointer);
               if (parent.model.GetNextRun(paletteAddress) is IPaletteRun palRun) desiredPalettePage -= palRun.PaletteFormat.InitialBlankPages;
               parent.PalettePage = desiredPalettePage;
               return;
            }

            if (selectionHeight < 0) start -= new Point(0, selectionHeight + height - 1);
            if (selectionWidth < 0) start -= new Point(selectionWidth + width - 1, 0);
            (selectionStart, selectionWidth, selectionHeight) = (start, width, height);

            if (selectionWidth == 1 && selectionHeight == 1 && parent.SelectedTool != ImageEditorTools.Fill) {
               var (xx, yy) = selectionStart;
               xx -= xx % parent.cursorSize;
               yy -= yy % parent.cursorSize;
               selectionStart = new Point(xx, yy);
               selectionWidth = selectionHeight = parent.cursorSize;
            }

            if (selectionWidth == 1 && selectionHeight == 1) {
               point = parent.ToSpriteSpace(point);
               if (!parent.WithinImage(point)) return;
               var index = parent.pixels[point.X, point.Y];
               if (parent.Palette.CanEditColors) {
                  var paletteAddress = parent.model.ReadPointer(parent.PalettePointer);
                  if (parent.model.GetNextRun(paletteAddress) is IPaletteRun palRun && palRun.Start == paletteAddress && palRun.PaletteFormat.Bits == 4) {
                     index -= palRun.PaletteFormat.InitialBlankPages << 4;
                     parent.PalettePage = index / 16;
                     index %= 16;
                  }
               }

               parent.Palette.SelectionStart = index;
            } else {
               underPixels = new int[selectionWidth, selectionHeight];
               for (int x = 0; x < selectionWidth; x++) for (int y = 0; y < selectionHeight; y++) {
                  underPixels[x, y] = parent.pixels[selectionStart.X + x, selectionStart.Y + y];
               }

               parent.BlockPreview.Set(parent.PixelData, parent.PixelWidth, selectionStart, selectionWidth, selectionHeight);
            }
         }

         private void MakeSquare(ref int width, ref int height) {
            width = Math.Min(width, height);
            var log = (int)Math.Log(width, 2);
            width = (int)Math.Pow(2, log);
            height = width;
         }

         private void RaiseRefreshSelection() {
            var size = parent.cursorSize;
            var (xx, yy) = selectionStart;
            var drawPoint = new Point(xx - xx % size, yy - yy % size);
            var selectionPoints = new Point[size * size];
            for (int x = 0; x < size; x++) for (int y = 0; y < size; y++) selectionPoints[y * size + x] = drawPoint + new Point(x, y);
            parent.RaiseRefreshSelection(selectionPoints);
         }
      }

      private class TilePaletteTool : IImageToolStrategy {
         private readonly ImageEditorViewModel parent;

         public TilePaletteTool(ImageEditorViewModel parent) => this.parent = parent;

         public void ToolDown(Point screenPosition) => ToolDrag(screenPosition);

         public void ToolDrag(Point screenPosition) {
            var point = parent.ToSpriteSpace(screenPosition);
            var rowNumber = point.Y / 8;
            var colNumber = point.X / 8;

            if (parent.WithinImage(point)) {
               var lineTileWidth = parent.PixelWidth / 8;
               var tileIndex = rowNumber * lineTileWidth + colNumber;
               var paletteAddress = parent.model.ReadPointer(parent.PalettePointer);
               var currentSelectedPage = parent.PalettePage;
               if (parent.model.GetNextRun(paletteAddress) is IPaletteRun paletteRun) currentSelectedPage += paletteRun.PaletteFormat.InitialBlankPages;
               var spriteAddress = parent.model.ReadPointer(parent.SpritePointer);
               if (parent.TilePalettes[tileIndex] != currentSelectedPage && parent.model.GetNextRun(spriteAddress) is LzTilemapRun tilemapRun) {
                  parent.TilePalettes[tileIndex] = currentSelectedPage;

                  // tilemap may have been repointed: recalculate
                  spriteAddress = parent.model.ReadPointer(parent.SpritePointer);
                  tilemapRun = (LzTilemapRun)parent.model.GetNextRun(spriteAddress);
                  tilemapRun.FindMatchingTileset(parent.model);

                  parent.pixels = tilemapRun.GetPixels(parent.model, parent.SpritePage);
                  parent.Render();
               }
            }

            RaiseRefreshSelection(rowNumber, colNumber);
         }

         public void ToolHover(Point screenPosition) {
            var point = parent.ToSpriteSpace(screenPosition);
            var rowNumber = point.Y / 8;
            var colNumber = point.X / 8;
            RaiseRefreshSelection(rowNumber, colNumber);
         }

         public void ToolUp(Point screenPosition) { }

         private void RaiseRefreshSelection(int rowNumber, int colNumber) {
            var drawPoint = new Point(colNumber * 8, rowNumber * 8);
            var selectionPoints = new Point[8 * 8];
            for (int x = 0; x < 8; x++) for (int y = 0; y < 8; y++) selectionPoints[y * 8 + x] = drawPoint + new Point(x, y);
            parent.RaiseRefreshSelection(selectionPoints);
         }
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

   public class BlockPreview : ViewModelCore, IPixelViewModel {
      private int width, height;
      public int PixelWidth { get => width; private set => Set(ref width, value); }
      public int PixelHeight { get => height; private set => Set(ref height, value); }

      public short[] PixelData { get; private set; }

      private double scale;
      public double SpriteScale { get => scale; set => Set(ref scale, value); }

      private bool enabled;
      public bool Enabled { get => enabled; private set => Set(ref enabled, value); }

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

         SpriteScale = 64 / width;
      }

      public void Clear() {
         Enabled = false;
      }
   }

   public class EditOption : ViewModelCore, IPixelViewModel {
      private readonly IDataModel model;

      public int PixelWidth { get; }
      public int PixelHeight { get; }
      public int SpritePointer { get; }
      public int PalettePointer { get; }
      public short[] PixelData { get; private set; }

      public double SpriteScale => 1;

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

         var pixels = sprite.GetPixels(model, 0);
         PixelData = SpriteTool.Render(pixels, colors, initialBlankPages, 0);
         NotifyPropertyChanged(nameof(PixelData));
      }
   }
}
