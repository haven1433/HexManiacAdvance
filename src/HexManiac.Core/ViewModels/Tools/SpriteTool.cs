using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public interface IPixelViewModel : INotifyPropertyChanged {
      int PixelWidth { get; }
      int PixelHeight { get; }
      short[] PixelData { get; }
      double SpriteScale { get; }
   }

   public class SpriteTool : ViewModelCore, IToolViewModel, IPixelViewModel {
      public const int MaxSpriteWidth = 265; // From UI
      private readonly ViewPort viewPort;
      private readonly ChangeHistory<ModelDelta> history;
      private readonly IDataModel model;

      private bool paletteWasSetMoreRecently;

      private double spriteScale;
      private int spritePages = 1, palPages = 1, spritePage = 0, palPage = 0;
      private int[,] pixels;
      private short[] palette;
      private PaletteFormat paletteFormat;

      public string Name => "Image";

      public double SpriteScale { get => spriteScale; set => TryUpdate(ref spriteScale, value); }

      #region Sprite Properties

      private bool showNoSpriteAnchorMessage = true;
      public bool ShowNoSpriteAnchorMessage { get => showNoSpriteAnchorMessage; set => Set(ref showNoSpriteAnchorMessage, value); }

      public bool ShowSpriteProperties => !showNoSpriteAnchorMessage;

      private string spriteWidthHeight;
      public string SpriteWidthHeight { get => spriteWidthHeight; set => Set(ref spriteWidthHeight, value, oldValue => UpdateSpriteFormat()); }

      private bool spriteIs256Color;
      public bool SpriteIs256Color { get => spriteIs256Color; set => Set(ref spriteIs256Color, value, oldValue => UpdateSpriteFormat()); }

      private bool spriteIsTilemap;
      public bool SpriteIsTilemap { get => spriteIsTilemap; set => Set(ref spriteIsTilemap, value, oldValue => {
         var split = spriteWidthHeight.ToUpper().Trim().Split("X");
         if (split.Length == 2 && int.TryParse(split[0], out int width) && int.TryParse(split[1], out int height)) {
            if (spriteIsTilemap) {
               width *= 4;
               height *= 4;
            } else {
               width = Math.Max(1, width / 4);
               height = Math.Max(1, height / 4);
            }
            Set(ref spriteWidthHeight, $"{width}x{height}", nameof(SpriteWidthHeight));
         }
         UpdateSpriteFormat();
      }); }

      private string spritePaletteHint = string.Empty;
      public string SpritePaletteHint { get => spritePaletteHint; set => Set(ref spritePaletteHint, value, oldValue => UpdateSpriteFormat()); }

      private void UpdateSpriteFormat() {
         var spriteRun = model.GetNextRun(spriteAddress) as ISpriteRun;
         if (spriteRun == null || spriteRun.Start != spriteAddress) return;
         var bits = SpriteIs256Color ? 8 : 4;

         if (spriteWidthHeight.ToLower().Trim() == "tiles") {
            if (spriteRun is LZRun lzRun && lzRun.DecompressedLength % (8 * bits) == 0) {
               model.ObserveRunWritten(history.CurrentChange, new LzTilesetRun(new TilesetFormat(bits, spritePaletteHint), model, spriteAddress));
               viewPort.Refresh();
               LoadSprite();
            }
         }

         var split = spriteWidthHeight.ToUpper().Trim().Split("X");
         if (split.Length > 2 || split.Length < 1) return;
         var availableLength = model.GetNextAnchor(spriteRun.Start + spriteRun.Length).Start - spriteRun.Start;
         if (split.Length == 1 && int.TryParse(split[0], out int tiles)) {
            var desiredLength = tiles * 8 * bits;
            if (availableLength < desiredLength) {
               viewPort.RaiseError($"Need {desiredLength} bytes, but only {availableLength} bytes available.");
            } else {
               model.ObserveRunWritten(history.CurrentChange, new TilesetRun(new TilesetFormat(bits, tiles, spritePaletteHint), model, spriteAddress));
               viewPort.Refresh();
               LoadSprite();
            }
            return;
         }
         if (!int.TryParse(split[0], out int width) || !int.TryParse(split[1], out int height)) return;

         var newFormat = new SpriteFormat(bits, width, height, spritePaletteHint);

         var desiredUncompressedLength = newFormat.TileWidth * newFormat.TileHeight * 8 * newFormat.BitsPerPixel;
         if (spriteIsTilemap) desiredUncompressedLength /= newFormat.BitsPerPixel * 4;
         if (spriteRun is LZRun) availableLength = LZRun.Decompress(model, spriteRun.Start).Length;
         if (availableLength < desiredUncompressedLength) {
            viewPort.RaiseError($"Need {desiredUncompressedLength} bytes, but only {availableLength} bytes available.");
         } else if (spriteIsTilemap && spriteRun is LZRun) {
            split = spritePaletteHint.Split("|");
            var tileset = split[0];
            string tilesetTableMember = null;
            if (split.Length > 1) tilesetTableMember = split[1];
            var tilemapFormat = new TilemapFormat(bits, width, height, tileset, tilesetTableMember);
            model.ObserveRunWritten(history.CurrentChange, new LzTilemapRun(tilemapFormat, model, spriteRun.Start, spriteRun.PointerSources));
            viewPort.Refresh();
            LoadSprite();
         } else if (spriteIsTilemap && !(spriteRun is LZRun)) {
            // uncompressed tilemaps are not currently supported, so just no-op.
         } else {
            model.ObserveRunWritten(history.CurrentChange, spriteRun.Duplicate(newFormat));
            viewPort.Refresh();
            LoadSprite();
         }
      }

      private StubCommand gotoSpriteAddress;
      public ICommand GotoSpriteAddress => StubCommand(ref gotoSpriteAddress, ExecuteGotoSpriteAddress);
      private void ExecuteGotoSpriteAddress() {
         var run = model.GetNextRun(spriteAddress);
         viewPort.Goto.Execute(spriteAddress);

         if (run is ISpriteRun && run.Start == spriteAddress) { LoadSprite(); UpdateSpriteProperties(); }
         if ((!(run is NoInfoRun) && !(run is PCSRun)) || run.Start != spriteAddress) return;

         var decompressed = LZRun.Decompress(model, run.Start);
         if (decompressed == null) {
            var nextRun = model.GetNextAnchor(spriteAddress + 1);
            var length = nextRun.Start - spriteAddress;
            if (length % 32 != 0) {
               viewPort.RaiseError("Could not autodetect an uncompressed sprite at that address.");
               return;
            }
            var tileCount = length / 32;
            var width = (int)Math.Sqrt(tileCount);
            var height = width;
            model.ObserveRunWritten(history.CurrentChange, new SpriteRun(spriteAddress, new SpriteFormat(4, width, height, null)));
         } else {
            var pixelCount = decompressed.Length;
            if (pixelCount % 32 != 0) {
               viewPort.RaiseError("Could not autodetect a compressed sprite at that address.");
               return;
            }
            var tileCount = pixelCount / 32;
            var width = (int)Math.Sqrt(tileCount);
            var height = width;
            model.ObserveRunWritten(history.CurrentChange, new LzSpriteRun(new SpriteFormat(4, width, height, null), model, spriteAddress));
         }

         viewPort.Refresh();
         LoadSprite();
         UpdateSpriteProperties();
      }

      private void UpdateSpriteProperties() {
         if (model.GetNextRun(spriteAddress) is ISpriteRun run && run.Start == spriteAddress) {
            var format = run.SpriteFormat;
            ShowNoSpriteAnchorMessage = false;
            spriteWidthHeight = format.TileWidth + "x" + format.TileHeight;
            if (run is LzTilesetRun) spriteWidthHeight = "tiles";
            if (run is LzTilemapRun mapRun) {
               spriteIsTilemap = true;
               spritePaletteHint = mapRun.Format.MatchingTileset + (string.IsNullOrEmpty(mapRun.Format.TilesetTableMember) ? string.Empty : "|" + mapRun.Format.TilesetTableMember);
            } else {
               spriteIsTilemap = false;
               spritePaletteHint = format.PaletteHint ?? string.Empty;
            }
            spriteIs256Color = format.BitsPerPixel == 8;
            NotifyPropertyChanged(nameof(SpriteWidthHeight));
            NotifyPropertyChanged(nameof(SpriteIs256Color));
            NotifyPropertyChanged(nameof(SpriteIsTilemap));
            NotifyPropertyChanged(nameof(SpritePaletteHint));
         } else {
            ShowNoSpriteAnchorMessage = true;
         }
         NotifyPropertyChanged(nameof(ShowSpriteProperties));
      }

      #endregion

      #region Palette Properties

      private bool showNoPaletteAnchorMessage = true;
      public bool ShowNoPaletteAnchorMessage { get => showNoPaletteAnchorMessage; set => Set(ref showNoPaletteAnchorMessage, value); }

      public bool ShowPaletteProperties => !showNoPaletteAnchorMessage;

      private bool paletteIs256Color;
      public bool PaletteIs256Color { get => paletteIs256Color; set => Set(ref paletteIs256Color, value, oldValue => UpdatePaletteFormat()); }

      private string palettePages;
      public string PalettePages { get => palettePages; set => Set(ref palettePages, value, oldValue => UpdatePaletteFormat()); }

      private void UpdatePaletteFormat() {
         var palRun = model.GetNextRun(paletteAddress) as IPaletteRun;
         if (palRun == null || palRun.Start != paletteAddress) return;
         var bits = paletteIs256Color ? 8 : 4;
         if (!PaletteRun.TryParsePaletteFormat($"{bits}:{PalettePages}", out var newFormat)) return;
         model.ObserveRunWritten(history.CurrentChange, palRun.Duplicate(newFormat));
         viewPort.Refresh();
         LoadPalette();
      }

      private StubCommand gotoPaletteAddress;
      public ICommand GotoPaletteAddress => StubCommand(ref gotoPaletteAddress, ExecuteGotoPaletteAddress);
      private void ExecuteGotoPaletteAddress() {
         var run = model.GetNextRun(paletteAddress);
         viewPort.Goto.Execute(paletteAddress);
         if (run is IPaletteRun && run.Start == paletteAddress) { LoadPalette();UpdatePaletteProperties(); }
         if ((!(run is NoInfoRun) && !(run is PCSRun)) || run.Start != paletteAddress) return;

         var decompressed = LZRun.Decompress(model, run.Start);
         if (decompressed == null) {
            var nextRun = model.GetNextAnchor(paletteAddress + 1);
            var length = nextRun.Start - paletteAddress;
            if (length % 32 != 0) {
               viewPort.RaiseError("Could not autodetect an uncompressed palette at that address.");
               return;
            }
            var pages = Math.Min(length / 32, 16);
            model.ObserveRunWritten(history.CurrentChange, new PaletteRun(paletteAddress, new PaletteFormat(4, pages)));
         } else {
            var byteCount = decompressed.Length;
            if (byteCount % 32 != 0) {
               viewPort.RaiseError("Could not autodetect a compressed sprite at that address.");
               return;
            }
            var pages = Math.Min(byteCount / 32, 16);
            model.ObserveRunWritten(history.CurrentChange, new LzPaletteRun(new PaletteFormat(4, pages), model, paletteAddress));
         }

         viewPort.Refresh();
         LoadPalette();
         UpdatePaletteProperties();
      }

      private void UpdatePaletteProperties() {
         if (model.GetNextRun(paletteAddress) is IPaletteRun palRun && palRun.Start == paletteAddress) {
            var format = palRun.PaletteFormat;
            Set(ref paletteIs256Color, format.Bits == 8, nameof(PaletteIs256Color));
            Set(ref palettePages, PaletteRun.GetPalettePages(format), nameof(PalettePages));
            ShowNoPaletteAnchorMessage = false;
         } else {
            ShowNoPaletteAnchorMessage = true;
         }
         NotifyPropertyChanged(nameof(ShowPaletteProperties));
      }

      #endregion

      private readonly StubCommand
         importPair = new StubCommand(),
         exportPair = new StubCommand(),
         prevSpritePage = new StubCommand(),
         nextSpritePage = new StubCommand(),
         prevPalPage = new StubCommand(),
         nextPalPage = new StubCommand();

      private int spriteAddress;
      public int SpriteAddress {
         get => spriteAddress;
         set {
            var run = model.GetNextRun(value) as ISpriteRun;
            if (!TryUpdate(ref spriteAddress, value)) {
               if (paletteWasSetMoreRecently) PaletteAddress = FindMatchingPalette(model, run, PaletteAddress);
               paletteWasSetMoreRecently = false;
               if (!RunPropertiesChanged(run)) return;
            }

            importPair.CanExecuteChanged.Invoke(importPair, EventArgs.Empty);
            exportPair.CanExecuteChanged.Invoke(exportPair, EventArgs.Empty);

            if (run == null || run.Start != spriteAddress) {
               spritePages = 1;
               spritePage = 0;
            } else {
               spritePages = run.Pages;
               if (spritePage >= spritePages) spritePage = 0;
               NotifyPropertyChanged(nameof(HasMultipleSpritePages));
            }
            UpdateSpriteProperties();
            LoadSprite();
            PaletteAddress = FindMatchingPalette(model, run, PaletteAddress);
         }
      }

      private int paletteAddress;
      public int PaletteAddress {
         get => paletteAddress;
         set {
            if (!TryUpdate(ref paletteAddress, value)) return;
            importPair.CanExecuteChanged.Invoke(importPair, EventArgs.Empty);
            exportPair.CanExecuteChanged.Invoke(exportPair, EventArgs.Empty);
            paletteWasSetMoreRecently = true;
            var paletteRun = model.GetNextRun(value) as IPaletteRun;
            if (paletteRun == null) {
               palPages = 1;
               palPage = 0;
            } else {
               palPages = paletteRun.Pages;
               if (palPage >= palPages) palPage = 0;
               NotifyPropertyChanged(nameof(HasMultiplePalettePages));
            }
            UpdatePaletteProperties();
            LoadPalette();
         }
      }

      public bool HasMultipleSpritePages => spritePages > 1;
      public bool HasMultiplePalettePages => palPages > 1;

      public ICommand ImportPair => importPair;
      public ICommand ExportPair => exportPair;
      public ICommand PreviousSpritePage => prevSpritePage;
      public ICommand NextSpritePage => nextSpritePage;
      public ICommand PreviousPalettePage => prevPalPage;
      public ICommand NextPalettePage => nextPalPage;

      public int PixelWidth { get; private set; }
      public int PixelHeight { get; private set; }
      public int PaletteWidth { get; private set; }
      public int PaletteHeight { get; private set; }

      public short[] PixelData { get; private set; }

      // TODO propogate changes back to the paletteAddress in the model
      // public ObservableCollection<short> Palette { get; private set; } = new ObservableCollection<short>();
      public PaletteCollection Colors { get; }

      public bool IsReadOnly => true;

      public SpriteTool(ViewPort viewPort, ChangeHistory<ModelDelta> history) {
         this.viewPort = viewPort;
         this.history = history;
         Colors = new PaletteCollection(viewPort, history);
         model = viewPort?.Model;
         spriteAddress = Pointer.NULL;
         paletteAddress = Pointer.NULL;

         importPair.CanExecute = arg => paletteAddress >= 0 && spriteAddress >= 0;
         exportPair.CanExecute = arg => paletteAddress >= 0 && spriteAddress >= 0;
         prevSpritePage.CanExecute = arg => spritePage > 0;
         nextSpritePage.CanExecute = arg => spritePage < spritePages - 1;
         prevPalPage.CanExecute = arg => palPage > 0;
         nextPalPage.CanExecute = arg => palPage < palPages - 1;

         importPair.Execute = arg => ImportSpriteAndPalette((IFileSystem)arg);
         exportPair.Execute = arg => ExportSpriteAndPalette((IFileSystem)arg);
         prevSpritePage.Execute = arg => { spritePage -= 1; LoadSprite(); };
         nextSpritePage.Execute = arg => { spritePage += 1; LoadSprite(); };
         prevPalPage.Execute = arg => { palPage -= 1; LoadPalette(); };
         nextPalPage.Execute = arg => { palPage += 1; LoadPalette(); };

         LoadPalette();
      }

      public void DataForCurrentRunChanged() {
         LoadSprite();
         LoadPalette();
      }

      public static short[] Render(int[,] pixels, IReadOnlyList<short> palette, PaletteFormat format, int spritePage) {
         if (pixels == null) return new short[0];
         if (palette == null) palette = TileViewModel.CreateDefaultPalette(16);
         var data = new short[pixels.Length];
         var width = pixels.GetLength(0);
         var palettePageOffset = format.InitialBlankPages << 4;
         var spritePageOffset = (spritePage << 4) + palettePageOffset;
         for (int i = 0; i < data.Length; i++) {
            var pixel = pixels[i % width, i / width];
            while (pixel < spritePageOffset) pixel += spritePageOffset;
            var pixelIntoPalette = Math.Max(0, pixel - palettePageOffset);
            data[i] = palette[pixelIntoPalette % palette.Count];
         }
         return data;
      }

      private void LoadSprite() {
         var run = model.GetNextRun(spriteAddress) as ISpriteRun;
         if (run == null) {
            pixels = null;
            PixelWidth = 0;
            PixelHeight = 0;
         } else {
            pixels = run.GetPixels(model, spritePage);
            PixelWidth = pixels.GetLength(0);
            PixelHeight = pixels.GetLength(1);
         }
         var renderPalette = GetRenderPalette(run);
         PixelData = Render(pixels, renderPalette, paletteFormat, spritePage);
         NotifyPropertyChanged(nameof(PixelWidth));
         NotifyPropertyChanged(nameof(PixelHeight));
         prevSpritePage.CanExecuteChanged.Invoke(prevSpritePage, EventArgs.Empty);
         nextSpritePage.CanExecuteChanged.Invoke(nextSpritePage, EventArgs.Empty);
         NotifyPropertyChanged(nameof(PixelData));

         // update scale
         if (PixelWidth > MaxSpriteWidth) {
            SpriteScale = .5;
         } else if (PixelWidth < MaxSpriteWidth / 2) {
            SpriteScale = 2;
         } else {
            SpriteScale = 1;
         }
      }

      private IReadOnlyList<short> GetRenderPalette(ISpriteRun sprite) {
         if (sprite == null) return palette;
         if (sprite.SpriteFormat.BitsPerPixel == 8) {
            if (model.GetNextRun(paletteAddress) is IPaletteRun paletteRun) return paletteRun.AllColors(model);
         }
         if (!(sprite is LzTilemapRun tmRun)) return palette;
         tmRun.FindMatchingTileset(model);
         if (model.GetNextRun(paletteAddress) is IPaletteRun palRun) return palRun.AllColors(model);
         return palette;
      }

      private void LoadPalette() {
         var run = model?.GetNextRun(paletteAddress) as IPaletteRun;
         if (run == null) {
            palette = TileViewModel.CreateDefaultPalette(0x10);
         } else {
            palette = run.GetPalette(model, palPage).ToArray();
            paletteFormat = run.PaletteFormat;
         }

         PaletteWidth = (int)Math.Sqrt(palette.Length);
         PaletteHeight = (int)(Math.Ceiling((double)palette.Length / PaletteWidth));
         NotifyPropertyChanged(nameof(PaletteWidth));
         NotifyPropertyChanged(nameof(PaletteHeight));
         prevPalPage.CanExecuteChanged.Invoke(prevPalPage, EventArgs.Empty);
         nextPalPage.CanExecuteChanged.Invoke(nextPalPage, EventArgs.Empty);

         Colors.SourcePalette = paletteAddress;
         Colors.SetContents(palette);
         Colors.Page = palPage;
         Colors.HasMultiplePages = palPages > 1;
         PixelData = Render(pixels, GetRenderPalette(model?.GetNextRun(spriteAddress) as ISpriteRun), paletteFormat, spritePage);
         NotifyPropertyChanged(nameof(PixelData));
      }

      public static int FindMatchingPalette(IDataModel model, ISpriteRun spriteRun, int defaultAddress) {
         var hint = spriteRun?.SpriteFormat.PaletteHint;
         if (hint == null) return defaultAddress;
         var hintRun = model.GetNextRun(model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, hint));

         // easy case: the hint is the address of a palette
         if (hintRun is IPaletteRun) {
            return hintRun.Start;
         }

         // harder case: the hint is a table
         if (!(hintRun is ITableRun hintTableRun)) return defaultAddress;
         if ((spriteRun.PointerSources?.Count ?? 0) == 0) return defaultAddress;
         var spritePointer = spriteRun.PointerSources[0];
         var spriteTable = model.GetNextRun(spritePointer) as ITableRun;
         if (spriteTable == null) return defaultAddress;
         int spriteIndex = (spritePointer - spriteTable.Start) / spriteTable.ElementLength;

         // easy case: hint table is pointers to palettes
         var hintTableElementStart = hintTableRun.Start + hintTableRun.ElementLength * spriteIndex;
         int segmentOffset = 0;
         for (int i = 0; i < hintTableRun.ElementContent.Count; i++) {
            if (hintTableRun.ElementContent[i].Type == ElementContentType.Pointer) {
               var paletteAddress = model.ReadPointer(hintTableElementStart + segmentOffset);
               if (model.GetNextRun(paletteAddress) is IPaletteRun) return paletteAddress;
            }
            segmentOffset += hintTableRun.ElementContent[i].Length;
         }

         // harder case: hint table is index into a different table
         if (!(hintTableRun.ElementContent[0] is ArrayRunEnumSegment segment)) return defaultAddress;
         var paletteTableAddress = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, segment.EnumName);
         var paletteTableRun = model.GetNextRun(paletteTableAddress) as ITableRun;
         if (paletteTableRun == null) return defaultAddress;
         if (paletteTableRun.ElementContent[0].Type != ElementContentType.Pointer) return defaultAddress;
         var index = model.ReadMultiByteValue(hintTableElementStart, segment.Length);
         if (paletteTableRun.ElementCount <= index) return defaultAddress;
         var paletteTableElementStart = paletteTableRun.Start + paletteTableRun.ElementLength * index;
         var indexedPaletteAddress = model.ReadPointer(paletteTableElementStart);
         if (!(model.GetNextRun(indexedPaletteAddress) is IPaletteRun)) return defaultAddress;
         return indexedPaletteAddress;
      }

      private bool RunPropertiesChanged(ISpriteRun run) {
         if (run == null) return false;
         if (run.SpriteFormat.TileWidth * 8 != PixelWidth) return true;
         if (run.SpriteFormat.TileHeight * 8 != PixelHeight) return true;
         if (run.Pages != spritePages) return true;
         return false;
      }

      private void ImportSpriteAndPalette(IFileSystem fileSystem) {
         (short[] image, short[] paletteHint, int width) = fileSystem.LoadImage();
         if (image == null) {
            if (width % 8 != 0) viewPort.RaiseError("The width/height of the loaded image be a multiple of 8!");
            return;
         }
         int height = image.Length / width;
         if (width != PixelWidth || height != PixelHeight) {
            viewPort.RaiseError("The width/height of the loaded image must match the current width/height!");
            return;
         }

         var spriteRun = model.GetNextRun(spriteAddress) as ISpriteRun;
         var palRun = model.GetNextRun(paletteAddress) as IPaletteRun;
         if (spriteRun == null || palRun == null) {
            viewPort.RaiseError("The sprite/palette addresses are not valid!");
            return;
         }

         // extract palette
         var renderPalette = GetRenderPalette(model?.GetNextRun(spriteAddress) as ISpriteRun).ToArray();
         paletteHint = paletteHint ?? renderPalette;
         image = ReduceColors(image, renderPalette);
         var newPalette = image.Distinct().ToList();

         // sort palette based on the previous palette
         if (newPalette.All(paletteHint.Contains)) {
            newPalette = paletteHint.ToList();
         } else {
            for (int i = 0; i < paletteHint.Length && i < newPalette.Count; i++) {
               var color = paletteHint[i];
               var index = newPalette.Skip(i).ToList().IndexOf(color) + i;
               if (index == i - 1) continue;
               newPalette.RemoveAt(index);
               newPalette.Insert(i, color);
            }
         }
         while (newPalette.Count < renderPalette.Length) newPalette.Add(paletteHint.Except(newPalette).FirstOrDefault());

         var tiles = Tilize(image, width);
         var palettes = palRun.PaletteFormat.Bits == 4 ? SplitPalettes(newPalette) : new short[][] { newPalette.ToArray() };
         var tilePixels = tiles.Select(tile => ExtractPixelsForTile(tile, palettes, palRun.PaletteFormat.InitialBlankPages)).ToArray();
         var newPixels = Detilize(tilePixels, width / 8);

         WriteSpriteAndPalette(spriteRun, newPixels, palRun, palettes, newPalette);
      }

      private void WriteSpriteAndPalette(ISpriteRun spriteRun, int[,] newPixels, IPaletteRun palRun, short[][]palettes, IReadOnlyList<short> newPalette) {
         IFormattedRun newRun = spriteRun.SetPixels(model, viewPort.CurrentChange, spritePage, newPixels);
         bool spriteMoved = newRun.Start != spriteRun.Start;

         var initialPalletteRun = palRun;
         if (palettes.Length == palRun.Pages) {
            for (int i = 0; i < palettes.Length; i++) palRun = palRun.SetPalette(model, viewPort.CurrentChange, i, palettes[i]);
         } else {
            palRun = palRun.SetPalette(model, viewPort.CurrentChange, palPage, newPalette);
         }
         bool paletteMoved = initialPalletteRun.Start != palRun.Start;

         if (spriteMoved && !paletteMoved) {
            viewPort.Goto.Execute(newRun.Start);
            viewPort.RaiseMessage($"Sprite moved to {newRun.Start:X6}. Pointers have been updated.");
         } else if (paletteMoved && !spriteMoved) {
            viewPort.Goto.Execute(palRun.Start);
            viewPort.RaiseMessage($"Palette moved to {palRun.Start:X6}. Pointers have been updated.");
         } else if (paletteMoved && spriteMoved) {
            viewPort.Goto.Execute(newRun.Start);
            viewPort.RaiseMessage($"Sprite and palette moved to {newRun.Start:X6} and {palRun.Start:X6}.");
         } else {
            viewPort.Refresh();
         }

         LoadPalette();
         LoadSprite();
      }

      public static short[] ReduceColors(short[] initialImage, short[] examplePalette) {
         var targetPaletteLength = examplePalette.Length;

         var initialColorsAndWeight = new Dictionary<short, int>();
         foreach (var color in initialImage) {
            if (initialColorsAndWeight.ContainsKey(color)) initialColorsAndWeight[color] += 1;
            else initialColorsAndWeight[color] = 1;
         }

         if (initialColorsAndWeight.Count <= targetPaletteLength) return initialImage;

         // organize the initial colors based on their usage
         var masses = new List<ColorMass>();
         foreach (var color in initialColorsAndWeight.Keys) {
            masses.Add(new ColorMass(color, initialColorsAndWeight[color]));
         }

         // use a 'gravity' metric to reduce the number of colors
         while (masses.Count > targetPaletteLength) {
            var mostAttractedIndexPair = (first: 0, second: 0);
            double greatestAttraction = -1;
            for (int i = 0; i < masses.Count; i++) {
               for (int j = i + 1; j < masses.Count; j++) {
                  var attractor = masses[i] * masses[j];
                  if (attractor <= greatestAttraction) continue;
                  mostAttractedIndexPair = (i, j);
                  greatestAttraction = attractor;
               }
            }
            var first = masses[mostAttractedIndexPair.first];
            var second = masses[mostAttractedIndexPair.second];
            masses.RemoveAt(mostAttractedIndexPair.second);
            masses.RemoveAt(mostAttractedIndexPair.first);
            masses.Add(first + second);
         }

         // build a mapping from the full color set to the reduced color set
         var sourceToTarget = new Dictionary<short, short>();
         foreach(var mass in masses) {
            var resultColor = mass.ResultColor;
            foreach (var color in mass.OriginalColors.Keys) sourceToTarget[color] = resultColor;
         }

         // replace initial colors with reduced color set
         var resultImage = new short[initialImage.Length];
         for (int i = 0; i < initialImage.Length; i++) resultImage[i] = sourceToTarget[initialImage[i]];
         return resultImage;
      }

      private short[][] SplitPalettes(IReadOnlyList<short> colors) {
         Debug.Assert(colors.Count % 16 == 0);
         var result = new short[colors.Count / 16][];
         for (int i = 0; i < result.Length; i++) result[i] = colors.Skip(i * 16).Take(16).ToArray();
         return result;
      }

      private short[][] Tilize(short[] image, int width) {
         var height = image.Length / width;
         if (width % 8 != 0 || height % 8 != 0) throw new NotSupportedException("You can only tilize an image if width/height are multiples of 8!");
         int tileWidth = width / 8, tileHeight = height / 8;
         var result = new short[tileWidth * tileHeight][];
         for (int y = 0; y < tileHeight; y++) {
            for (int x = 0; x < tileWidth; x++) {
               var tileIndex = y * tileWidth + x;
               result[tileIndex] = new short[64];
               for(int yy = 0; yy < 8; yy++) {
                  for(int xx = 0; xx < 8; xx++) {
                     var yIndex = y * 8 + yy;
                     var xIndex = x * 8 + xx;
                     var index = yIndex * width + xIndex;
                     result[tileIndex][yy * 8 + xx] = image[index];
                  }
               }
            }
         }
         return result;
      }

      private int WhichPalette(short[] tile, short[][] palettes) {
         for (int i = 0; i < palettes.Length; i++) {
            if (tile.All(palettes[i].Contains)) return i;
         }
         return -1;
      }

      private int[,] ExtractPixelsForTile(short[] tile, short[][]palettes, int paletteOffset) {
         var paletteIndex = WhichPalette(tile, palettes);
         var palette = palettes[paletteIndex];
         paletteIndex = (paletteIndex + paletteOffset) << 4;
         var palIndex = new Dictionary<short, int>();
         var result = new int[8, 8];
         for (int i = 0; i < palette.Length; i++) palIndex[palette[i]] = i;
         for (int y = 0; y < 8; y++) {
            for (int x = 0; x < 8; x++) {
               var pixel = tile[y * 8 + x];
               result[x, y] = palIndex[pixel] + paletteIndex;
            }
         }
         return result;
      }

      private int[,] Detilize(int[][,] tiles, int tileWidth) {
         Debug.Assert(tiles.Length % tileWidth == 0);
         int tileHeight = tiles.Length / tileWidth;
         var result = new int[tileWidth * 8, tiles.Length / tileWidth * 8];

         for (int y = 0; y < tileHeight; y++) {
            var yStart = y * 8;
            for (int x = 0; x < tileWidth; x++) {
               var tile = tiles[y * tileWidth + x];
               var xStart = x * 8;
               for (int yy = 0; yy < 8; yy++) {
                  for (int xx = 0; xx < 8; xx++) {
                     result[xStart + xx, yStart + yy] = tile[xx, yy];
                  }
               }
            }
         }

         return result;
      }

      private void ExportSpriteAndPalette(IFileSystem fileSystem) {
         var renderPalette = GetRenderPalette(model?.GetNextRun(spriteAddress) as ISpriteRun).ToArray();
         fileSystem.SaveImage(PixelData, renderPalette, PixelWidth);
      }
   }

   public class ColorMass {
      public double R { get; private set; }
      public double G { get; private set; }
      public double B { get; private set; }
      public int Mass { get; private set; }

      private readonly Dictionary<short, int> originalColors = new Dictionary<short, int>();
      public IReadOnlyDictionary<short, int> OriginalColors => originalColors;

      public short ResultColor => CombineRGB((int)R, (int)G, (int)B);

      private ColorMass() { }
      public ColorMass(short color, int count) {
         originalColors[color] = count;
         (R, G, B) = SplitRGB(color);
         Mass = count;
      }

      /// <summary>
      /// Returns a new color mass that accounts for the positions/masses of the original two.
      /// </summary>
      public static ColorMass operator +(ColorMass a, ColorMass b) {
         var mass = a.Mass + b.Mass;
         var result = new ColorMass {
            Mass = mass,
            R = (a.R * a.Mass + b.R * b.Mass) / mass,
            G = (a.G * a.Mass + b.G * b.Mass) / mass,
            B = (a.B * a.Mass + b.B * b.Mass) / mass,
         };
         foreach (var key in a.originalColors.Keys) result.originalColors[key] = a.originalColors[key];
         foreach (var key in b.originalColors.Keys) {
            if (result.originalColors.ContainsKey(key)) result.originalColors[key] += b.originalColors[key];
            else result.originalColors[key] = b.originalColors[key];
         }
         return result;
      }

      /// <summary>
      /// Returns a gravity factor, accounting for the distance/mass between the two color masses.
      /// </summary>
      public static double operator *(ColorMass a, ColorMass b) {
         var distanceR = a.R - b.R;
         var distanceG = a.G - b.G;
         var distanceB = a.B - b.B;
         var distanceSquared = distanceR * distanceR + distanceG * distanceG + distanceB * distanceB;
         var mass = a.Mass + b.Mass;
         return mass / distanceSquared;
      }

      public static (int, int, int) SplitRGB(short color) => (color >> 10, (color >> 5) & 0x1F, color & 0x1F);

      public static short CombineRGB(int r, int g, int b) => (short)((r << 10) | (g << 5) | b);
   }
}
