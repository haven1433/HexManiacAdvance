using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Visitors;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public interface IPixelViewModel : INotifyPropertyChanged {
      short Transparent { get; }
      int PixelWidth { get; }
      int PixelHeight { get; }
      short[] PixelData { get; }
      double SpriteScale { get; }
   }

   public class ReadonlyPixelViewModel : ViewModelCore, IPixelViewModel {
      public short Transparent { get; }
      public int PixelWidth { get; }
      public int PixelHeight { get; }
      public short[] PixelData { get; }
      public double SpriteScale => 1;

      public ReadonlyPixelViewModel(SpriteFormat sf, short[] data, short transparent = -1) {
         (PixelWidth, PixelHeight, PixelData) = (sf.TileWidth * 8, sf.TileHeight * 8, data);
         Transparent = transparent;
      }
      private ReadonlyPixelViewModel(int width, int height, short[] data) {
         (PixelWidth, PixelHeight, PixelData) = (width, height, data);
         Transparent = -1;
      }

      public static IPixelViewModel Create(IDataModel model, ISpriteRun sprite, bool useTransparency = false) {
         return SpriteDecorator.BuildSprite(model, sprite, useTransparency);
      }

      public static IPixelViewModel Crop(IPixelViewModel pixels, int x, int y, int width, int height) {
         return TilemapTableRun.Crop(pixels, x, y, Math.Max(0, pixels.PixelWidth - width - x), Math.Max(0, pixels.PixelHeight - height - y));
      }

      public static IPixelViewModel Render(IPixelViewModel background, IPixelViewModel foreground, int x, int y) {
         var data = new short[background.PixelData.Length];
         Array.Copy(background.PixelData, data, background.PixelData.Length);

         for (int yy = 0; yy < foreground.PixelHeight; yy++) {
            for (int xx = 0; xx < foreground.PixelWidth; xx++) {
               var pixel = foreground.PixelData[foreground.PixelWidth * yy + xx];
               if (pixel == foreground.Transparent) continue;
               if (x + xx >= background.PixelWidth || y + yy >= background.PixelHeight) continue;
               int offset = background.PixelWidth * (y + yy) + (x + xx);
               data[offset] = pixel;
            }
         }

         return new ReadonlyPixelViewModel(background.PixelWidth, background.PixelHeight, data);
      }
   }

   public class SpriteTool : ViewModelCore, IToolViewModel, IPixelViewModel {
      public const int MaxSpriteWidth = 275 - 17; // From UI: Panel Width - Scroll Bar Width
      private readonly ViewPort viewPort;
      private readonly ChangeHistory<ModelDelta> history;
      private readonly IDataModel model;

      private double spriteScale;
      private int spritePages = 1, palPages = 1, spritePage = 0, palPage = 0;
      private int[,] pixels;
      private short[] palette;
      private PaletteFormat paletteFormat;

      public string Name => "Image";

      public int SpritePage {
         get => spritePage;
         set => Set(ref spritePage, value, arg => LoadSprite());
      }

      public int PalettePage {
         get => palPage;
         set => Set(ref palPage, value, arg => LoadPalette());
      }

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
            } else {
               int tileSize = bits * 8;
               var tileCount = Math.Max(1, spriteRun.Length / tileSize);
               model.ObserveRunWritten(history.CurrentChange, new TilesetRun(new TilesetFormat(bits, tileCount, -1, spritePaletteHint), model, spriteAddress));
               viewPort.Refresh();
               LoadSprite();
            }
         }

         var split = spriteWidthHeight.ToUpper().Trim().Split("X");
         var availableLength = model.GetNextAnchor(spriteRun.Start + spriteRun.Length).Start - spriteRun.Start;
         if (split.Length == 1 && !(spriteRun is LZRun) && int.TryParse(split[0], out int tiles)) return;
         if (split.Length != 2) return;
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

      private StubCommand gotoSpriteAddress, isSprite;
      public ICommand IsSprite => StubCommand(ref isSprite, ExecuteIsSprite);
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
            var newRun = new SpriteRun(model, spriteAddress, new SpriteFormat(4, width, height, null));
            if (string.IsNullOrEmpty(model.GetAnchorFromAddress(-1, spriteAddress))) {
               model.ObserveAnchorWritten(history.CurrentChange, $"{HardcodeTablesModel.DefaultSpriteNamespace}.{spriteAddress:X6}", newRun);
            } else {
               model.ObserveRunWritten(history.CurrentChange, newRun);
            }
         } else {
            int tileCount = decompressed.Length / 32;
            bool isTilemap = false;
            if (decompressed.Length % 32 != 0) {
               if (decompressed.Length % 2 == 0) {
                  // not a sprite, but it is a tileset
                  tileCount = decompressed.Length / 2;
                  isTilemap = true;
               } else {
                  viewPort.RaiseError("Could not autodetect a compressed sprite at that address.");
                  return;
               }
            }
            var width = (int)Math.Sqrt(tileCount);
            var height = width;
            while (isTilemap && width * height != tileCount) { // tilemaps need to use all the existing data space to allow us to give them a name
               if (width * height < tileCount) {
                  width += 1;
               } else {
                  height -= 1;
               }
               if (height == 1) width = tileCount;
            }
            ISpriteRun spriteRun;
            string newRunName;
            if (isTilemap) {
               newRunName = $"{HardcodeTablesModel.DefaultTilemapNamespace}.{spriteAddress:X6}";
               spriteRun = new LzTilemapRun(new TilemapFormat(4, width, height, string.Empty), model, spriteAddress, run.PointerSources);
            } else {
               newRunName = $"{HardcodeTablesModel.DefaultSpriteNamespace}.{spriteAddress:X6}";
               spriteRun = new LzSpriteRun(new SpriteFormat(4, width, height, null), model, spriteAddress, run.PointerSources);
            }
            var existingName = model.GetAnchorFromAddress(-1, spriteAddress);
            if (!string.IsNullOrEmpty(existingName)) newRunName = existingName;
            model.ClearFormat(history.CurrentChange, spriteRun.Start, spriteRun.Length);
            model.ObserveAnchorWritten(history.CurrentChange, newRunName, spriteRun);
         }

         viewPort.Refresh();
         LoadSprite();
         UpdateSpriteProperties();
      }
      private void ExecuteIsSprite() {
         var initialStart = viewPort.ConvertViewPointToAddress(viewPort.SelectionStart);
         var checkAddress = initialStart;
         while (model.GetNextRun(checkAddress).Start > checkAddress && checkAddress > initialStart - 0x20) checkAddress--;
         SpriteAddress = checkAddress;
         ExecuteGotoSpriteAddress();
         viewPort.Tools.SelectedIndex = viewPort.Tools.IndexOf(this);
      }

      public void UpdateSpriteProperties() {
         if (model.GetNextRun(spriteAddress) is ISpriteRun run && run.Start == spriteAddress) {
            var format = run.SpriteFormat;
            ShowNoSpriteAnchorMessage = false;
            spriteWidthHeight = format.TileWidth + "x" + format.TileHeight;
            if (run is ITilesetRun) spriteWidthHeight = "tiles";
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
            importPair.RaiseCanExecuteChanged();
            exportPair.RaiseCanExecuteChanged();
            openInImageTab.RaiseCanExecuteChanged();
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

      private StubCommand gotoPaletteAddress, isPalette;
      public ICommand IsPalette => StubCommand(ref isPalette, ExecuteIsPalette);
      public ICommand GotoPaletteAddress => StubCommand(ref gotoPaletteAddress, ExecuteGotoPaletteAddress);
      private void ExecuteGotoPaletteAddress() {
         var run = model.GetNextRun(paletteAddress);
         viewPort.Goto.Execute(paletteAddress);
         if (run is IPaletteRun && run.Start == paletteAddress) { LoadPalette(); UpdatePaletteProperties(); }
         if ((!(run is NoInfoRun) && !(run is PCSRun)) || run.Start != paletteAddress) return;

         var existingName = model.GetAnchorFromAddress(-1, paletteAddress);
         var newName = $"{HardcodeTablesModel.DefaultPaletteNamespace}.{paletteAddress:X6}";
         if (!string.IsNullOrEmpty(existingName)) newName = existingName;
         var decompressed = LZRun.Decompress(model, run.Start);
         if (decompressed == null) {
            var nextRun = model.GetNextAnchor(paletteAddress + 1);
            var length = nextRun.Start - paletteAddress;
            if (length % 32 != 0) {
               viewPort.RaiseError("Could not autodetect an uncompressed palette at that address.");
               return;
            }
            var pages = Math.Min(length / 32, 16);

            for (int i = 0; i < pages * 16; i++) {
               if (model.ReadMultiByteValue(run.Start + i * 2, 2) >= 0x8000) {
                  viewPort.RaiseError($"Palette colors only use 15 bits, but the high bit it set at {run.Start + i * 2 + 1:X6}.");
                  return;
               }
            }

            model.ObserveAnchorWritten(history.CurrentChange, newName, new PaletteRun(paletteAddress, new PaletteFormat(4, pages)));
         } else {
            var byteCount = decompressed.Length;
            if (byteCount % 32 != 0) {
               viewPort.RaiseError("Could not autodetect a compressed sprite at that address.");
               return;
            }
            var pages = Math.Min(byteCount / 32, 16);
            model.ObserveAnchorWritten(history.CurrentChange, newName, new LzPaletteRun(new PaletteFormat(4, pages), model, paletteAddress));
         }

         viewPort.Refresh();
         LoadPalette();
         UpdatePaletteProperties();
      }
      private void ExecuteIsPalette() {
         var initialStart = viewPort.ConvertViewPointToAddress(viewPort.SelectionStart);
         var checkAddress = initialStart;
         while (model.GetNextRun(checkAddress).Start > checkAddress && checkAddress > initialStart - 0x20) checkAddress--;
         PaletteAddress = checkAddress;
         ExecuteGotoPaletteAddress();
         viewPort.Tools.SelectedIndex = viewPort.Tools.IndexOf(this);
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

      #region Export Many

      public bool CanExportMany => HasMultipleSpritePages && ExportPair.CanExecute(default);

      private StubCommand exportMany;
      public ICommand ExportMany => StubCommand<IFileSystem>(ref exportMany, ExecuteExportMany, fs => CanExportMany);

      private void ExecuteExportMany(IFileSystem fs) {
         var choice = fs.ShowOptions("Export Multi-Page Image", "How would you like to arrange the pages?",
            null,
            new VisualOption { Index = 0, Option = "Horizontal", ShortDescription = "Left-Right", Description = "Stack the pages from left to right." },
            new VisualOption { Index = 1, Option = "Vertical", ShortDescription = "Up-Down", Description = "Stack the pages from top to bottom." });

         int[,] manyPixels;
         if (choice == 0) {
            manyPixels = new int[PixelWidth * spritePages, PixelHeight];
         } else if (choice == 1) {
            manyPixels = new int[PixelWidth, PixelHeight * spritePages];
         } else {
            return;
         }

         var run = model.GetNextRun(spriteAddress) as ISpriteRun;
         var renderPalette = GetRenderPalette(run);
         for (int i = 0; i < spritePages; i++) {
            var (xPageOffset, yPageOffset) = choice == 0 ? (i * PixelWidth, 0) : (0, i * PixelHeight);
            var pagePixels = run.GetPixels(model, i);
            for (int x = 0; x < PixelWidth; x++) {
               for (int y = 0; y < PixelHeight; y++) {
                  manyPixels[xPageOffset + x, yPageOffset + y] = pagePixels[x, y];
               }
            }
         }

         var rendered = Render(manyPixels, renderPalette, paletteFormat.InitialBlankPages, spritePage);
         fs.SaveImage(rendered, manyPixels.GetLength(0));
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
               if (!RunPropertiesChanged(run)) {
                  openInImageTab.RaiseCanExecuteChanged();
                  return;
               }
            }

            SpriteAddressText = spriteAddress.ToAddress();
            importPair.RaiseCanExecuteChanged();
            exportPair.RaiseCanExecuteChanged();
            openInImageTab.RaiseCanExecuteChanged();

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
            PaletteAddressText = paletteAddress.ToAddress();
            importPair.CanExecuteChanged.Invoke(importPair, EventArgs.Empty);
            exportPair.CanExecuteChanged.Invoke(exportPair, EventArgs.Empty);
            openInImageTab?.CanExecuteChanged.Invoke(openInImageTab, EventArgs.Empty);
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

      private static readonly char[] allowableCharacters = "0123456789ABCDEF<>NUL".ToCharArray();
      private static readonly char[] toLower = "NUL".ToCharArray();
      private static string SanitizeAddressText(string address) {
         var characters = address.ToUpper().Where(c => c.IsAny(allowableCharacters)).ToArray();
         for (int i = 0; i < characters.Length; i++) {
            foreach (char c in toLower) {
               if (characters[i] == c) characters[i] += (char)('a' - 'A');
            }
         }
         return new string(characters);
      }
      private string spriteAddressText, paletteAddressText;
      public string SpriteAddressText {
         get => spriteAddressText;
         set {
            if (spriteAddressText == value) return;
            var newSpriteAddressText = SanitizeAddressText(value);
            value = new string(newSpriteAddressText.Where(c => !c.IsAny("<>".ToCharArray())).ToArray());
            if (!int.TryParse(value, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out int address)) address = Pointer.NULL;
            if (SpriteAddress != address) SpriteAddress = address;
            spriteAddressText = newSpriteAddressText;
            NotifyPropertyChanged();
         }
      }
      public string PaletteAddressText {
         get => paletteAddressText;
         set {
            if (paletteAddressText == value) return;
            var newPaletteAddressText = SanitizeAddressText(value);
            value = new string(newPaletteAddressText.Where(c => !c.IsAny("<>".ToCharArray())).ToArray());
            if (!int.TryParse(value, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out int address)) address = Pointer.NULL;
            if (PaletteAddress != address) PaletteAddress = address;
            paletteAddressText = newPaletteAddressText;
            NotifyPropertyChanged();
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

      private StubCommand openInImageTab;
      public ICommand OpenInImageTab => StubCommand(ref openInImageTab, () => viewPort.OpenImageEditorTab(spriteAddress, spritePage, palPage), () => {
         if (!(model.GetNextRun(spriteAddress) is ISpriteRun spriteRun)) return false;
         if (!spriteRun.SupportsEdit) return false;
         if (spriteRun.Start != spriteAddress) return false;
         if (spriteRun.PointerSources == null || spriteRun.PointerSources.Count == 0) return false;
         if (!exportPair.CanExecute(null)) return false;
         if (spriteRun.SpriteFormat.BitsPerPixel < 4) return true;
         if (!(model.GetNextRun(paletteAddress) is IPaletteRun palRun)) return false;
         if (palRun.Start != paletteAddress) return false;
         return palRun.PointerSources != null && palRun.PointerSources.Count > 0;
      });

      public int PixelWidth { get; private set; }
      public int PixelHeight { get; private set; }
      public short Transparent => -1;
      public int PaletteWidth { get; private set; }
      public int PaletteHeight { get; private set; }

      public short[] PixelData { get; private set; }

      public PaletteCollection Colors { get; }

      public bool IsReadOnly => true;

      public SpriteTool(ViewPort viewPort, ChangeHistory<ModelDelta> history) {
         this.viewPort = viewPort;
         this.history = history;
         Colors = new PaletteCollection(viewPort, viewPort.Model, history);
         Colors.ColorsChanged += (sender, e) => { LoadPalette(); LoadSprite(); };
         Colors.PaletteRepointed += (sender, newPaletteAddress) => HandlePaletteRepoint(newPaletteAddress);
         model = viewPort?.Model;
         spriteAddress = Pointer.NULL;
         paletteAddress = Pointer.NULL;

         importPair.CanExecute = arg => {
            if (spriteAddress < 0) return false;
            if (!(model.GetNextRun(spriteAddress) is ISpriteRun spriteRun)) return false;
            if (spriteRun.SpriteFormat.BitsPerPixel < 4) return spriteRun.SupportsImport;
            if (paletteAddress < 0) return false;
            return spriteRun.SupportsImport;
         };
         exportPair.CanExecute = arg => {
            if (spriteAddress < 0) return false;
            if (!(model.GetNextRun(spriteAddress) is ISpriteRun spriteRun)) return false;
            if (spriteRun.SpriteFormat.BitsPerPixel < 4) return true;
            if (paletteAddress < 0) return false;
            return true;
         };
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

      private void HandlePaletteRepoint(int address) {
         PaletteAddress = address;
         viewPort.RaiseMessage($"Palette moved to {address:X6}. Pointers were updated.");
      }

      public void DataForCurrentRunChanged() {
         LoadSprite();
         LoadPalette();
         UpdateSpriteProperties();
         UpdatePaletteProperties();
      }

      public static short[] Render(int[,] pixels, IReadOnlyList<short> palette, int initialBlankPages, int palettePage) {
         if (pixels == null) return new short[0];
         if (palette == null || palette.Count == 0) palette = TileViewModel.CreateDefaultPalette(16);
         var data = new short[pixels.Length];
         var width = pixels.GetLength(0);
         var initialPageOffset = initialBlankPages << 4;
         var palettePageOffset = (palettePage << 4) + initialPageOffset;
         for (int i = 0; i < data.Length; i++) {
            var pixel = pixels[i % width, i / width];
            while (pixel < palettePageOffset) pixel += palettePageOffset;
            var pixelIntoPalette = Math.Max(0, pixel - initialPageOffset);
            data[i] = palette[pixelIntoPalette % palette.Count];
         }
         return data;
      }

      public static IReadOnlyList<short> CreatePaletteWithUniqueTransparentColor(IReadOnlyList<short> palette) {
         var copy = palette.ToList();
         var otherColors = palette.Skip(1).ToList();
         while (otherColors.Contains(copy[0])) copy[0] = (short)((copy[0] + 1) % 0x8000);
         return copy;
      }

      private void LoadSprite() {
         var run = model.GetNextRun(spriteAddress) as ISpriteRun;
         if (run == null) {
            pixels = null;
            PixelWidth = 0;
            PixelHeight = 0;
         } else {
            pixels = run.GetPixels(model, spritePage);
            PixelWidth = pixels?.GetLength(0) ?? 0;
            PixelHeight = pixels?.GetLength(1) ?? 0;
         }
         var renderPalette = GetRenderPalette(run);
         PixelData = Render(pixels, renderPalette, paletteFormat.InitialBlankPages, spritePage);
         NotifyPropertyChanged(nameof(PixelWidth));
         NotifyPropertyChanged(nameof(PixelHeight));
         prevSpritePage.RaiseCanExecuteChanged();
         nextSpritePage.RaiseCanExecuteChanged();
         NotifyPropertyChanged(nameof(PixelData));
         NotifyPropertyChanged(nameof(CanExportMany));
         exportMany.RaiseCanExecuteChanged();

         // update scale
         if (PixelWidth > MaxSpriteWidth) {
            SpriteScale = .5;
         } else if (PixelWidth * 2 < MaxSpriteWidth) {
            SpriteScale = 2;
         } else {
            SpriteScale = 1;
         }
      }

      private IReadOnlyList<short> GetRenderPalette(ISpriteRun sprite) {
         if (sprite == null) return palette;
         if (sprite.SpriteFormat.BitsPerPixel == 1) {
            return new short[] { 0, 0b0_11111_11111_11111 };
         }
         if (sprite.SpriteFormat.BitsPerPixel == 2) {
            return TileViewModel.CreateDefaultPalette(4);
         }
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
            palPages = run.Pages;
            palPage = Math.Min(palPage, palPages - 1);
            palette = run.GetPalette(model, palPage).ToArray();
            paletteFormat = run.PaletteFormat;
         }

         PaletteWidth = (int)Math.Sqrt(palette.Length);
         PaletteHeight = (int)(Math.Ceiling((double)palette.Length / PaletteWidth));
         NotifyPropertyChanged(nameof(PaletteWidth));
         NotifyPropertyChanged(nameof(PaletteHeight));
         prevPalPage.CanExecuteChanged.Invoke(prevPalPage, EventArgs.Empty);
         nextPalPage.CanExecuteChanged.Invoke(nextPalPage, EventArgs.Empty);

         Colors.SourcePalettePointer = run?.PointerSources?.FirstOrDefault() ?? Pointer.NULL;
         Colors.SetContents(palette);
         Colors.Page = palPage;
         Colors.HasMultiplePages = palPages > 1;
         PixelData = Render(pixels, GetRenderPalette(model?.GetNextRun(spriteAddress) as ISpriteRun), paletteFormat.InitialBlankPages, spritePage);
         NotifyPropertyChanged(nameof(HasMultiplePalettePages));
         NotifyPropertyChanged(nameof(PixelData));
      }

      public static int FindMatchingPalette(IDataModel model, ISpriteRun spriteRun, int defaultAddress) {
         var palettes = spriteRun.FindRelatedPalettes(model);
         if (palettes.Select(run => run.Start).Contains(defaultAddress)) return defaultAddress;
         if (palettes.Count > 0) return palettes[0].Start;
         return defaultAddress;
      }

      private bool RunPropertiesChanged(ISpriteRun run) {
         if (run == null) return false;
         if (run.SpriteFormat.TileWidth * 8 != PixelWidth) return true;
         if (run.SpriteFormat.TileHeight * 8 != PixelHeight) return true;
         if (run.Pages != spritePages) return true;
         return false;
      }

      private void ImportSpriteAndPalette(IFileSystem fileSystem) {
         (short[] image, int width) = fileSystem.LoadImage();
         if (image == null) return;
         if (!TryValidate(image, out var spriteRun, out var paletteRun)) return;
         int height = image.Length / width;
         var relatedSprites = paletteRun?.FindDependentSprites(model) ?? new List<ISpriteRun>();
         var relatedPalettes = spriteRun.FindRelatedPalettes(model);
         int relatedImageCount = relatedSprites.Count * relatedPalettes.Count;
         if (width == PixelWidth && height == PixelHeight) {
            ImportSinglePageSpriteAndPalette(fileSystem, image, spriteRun, paletteRun);
         } else if (width == PixelWidth * spritePages && height == PixelHeight) {
            ImportWideSpriteAndPalette(fileSystem, image, spriteRun, paletteRun);
         } else if (width == PixelWidth && height == PixelHeight * spritePages) {
            ImportTallSpriteAndPalette(fileSystem, image, spriteRun, paletteRun);
         } else if (width == PixelWidth * relatedImageCount && height == PixelHeight) {
            var images = SplitHorizontally(image, width, relatedImageCount);
            var desiredImportType = ImportType.Greedy;
            for (int i = 0; i < relatedSprites.Count; i++) {
               var allowSpriteEdits = true;
               for (int j = 0; j < relatedPalettes.Count; j++) {
                  var imageIndex = relatedPalettes.Count * i + j;
                  var failurePoint = ImportSinglePageSpriteAndPalette(fileSystem, images[imageIndex], relatedSprites[i], relatedPalettes[j], desiredImportType, allowSpriteEdits);

                  // import may have failed if it's was trying to do a Greedy import with no sprite edits allowed
                  if (failurePoint.X < PixelWidth || failurePoint.Y < PixelHeight) {
                     viewPort.RaiseError($"Failed multi-image import: Image {i}, Palette {j} didn't match at pixel {failurePoint}.");
                  }
                  // still try to do the rest of the import

                  // import may have repointed sprites/palettes: refresh the related sprites/palettes cached locations.
                  spriteRun = model.GetNextRun(spriteAddress) as ISpriteRun;
                  paletteRun = model.GetNextRun(paletteAddress) as IPaletteRun;
                  relatedSprites = paletteRun.FindDependentSprites(model);
                  relatedPalettes = spriteRun.FindRelatedPalettes(model);
                  foreach (var s in relatedSprites) Debug.Assert(model.GetNextRun(s.Start) is ISpriteRun, $"Expected a Sprite at {s.Start:X6}.");
                  foreach (var p in relatedPalettes) Debug.Assert(model.GetNextRun(p.Start) is IPaletteRun, $"Expected a Palette at {p.Start:X6}.");
                  allowSpriteEdits = false;
               }
               desiredImportType = ImportType.Cautious;
            }
         } else {
            var displayWidth = PixelWidth;
            var displayHeight = PixelHeight;
            if (Math.Abs(displayWidth * spritePages - width) < Math.Abs(displayWidth - width)) displayWidth *= spritePages;
            if (Math.Abs(displayHeight * spritePages - height) < Math.Abs(displayHeight - height)) displayHeight *= spritePages;
            viewPort.RaiseError($"The imported width/height ({width}/{height}) doesn't match the image ({displayWidth}/{displayHeight})!");
         }
      }

      private short[][] SplitHorizontally(short[] image, int width, int count) {
         var images = new short[count][];
         var newWidth = width / count;
         for (var i = 0; i < count; i++) images[i] = new short[image.Length / count];
         for (int i = 0; i < image.Length; i++) {
            var j = (i / newWidth) % count;
            var y = i / width;
            var x = i % newWidth;
            images[j][newWidth * y + x] = image[i];
         }
         return images;
      }

      public static (int index, int page) GetTarget(IReadOnlyList<ISpriteRun> sprites, int target) {
         var (totalCount, initialTarget) = (0, target);
         for (int i = 0; i < sprites.Count; i++) {
            if (sprites[i].Pages > target) return (i, target);
            var pages = sprites[i].Pages;
            target -= pages;
            totalCount += pages;
         }
         throw new IndexOutOfRangeException($"Looking for page {initialTarget}, but there were only {totalCount} available pages among {sprites.Count} sprites!");
      }

      private ImportType GetImportType(IFileSystem fileSystem, int dependentPageCount, IReadOnlyList<ISpriteRun> dependentSprites, string imageType, IPaletteRun palette, ImportType type, out IReadOnlyList<int> usablePalettePages) {
         usablePalettePages = palette.Pages.Range().ToList();
         if (type != ImportType.Unknown) return type;
         var palFormat = palette.PaletteFormat;
         var imageDetails = new List<object>();
         var detailsLength = Math.Min(3, dependentPageCount);
         for (int i = 0; i < detailsLength; i++) {
            var targetIndex = i * dependentPageCount / detailsLength;
            var (dependentIndex, dependentPage) = GetTarget(dependentSprites, targetIndex);
            imageDetails.Add(ToolTipContentVisitor.BuildContentForRun(model, -1, dependentSprites[dependentIndex].Start, dependentSprites[dependentIndex], palette.Start, dependentPage));
         }

         var palDetails = new List<FlagViewModel>();
         for (int i = 0; i < palette.Pages; i++) {
            var n = i + palFormat.InitialBlankPages;
            palDetails.Add(new FlagViewModel($"Use palette {n}"));
         }

         var allDetails = new List<List<object>>();
         allDetails.Add(imageDetails);
         if (palDetails.Count > 1) allDetails.Add(palDetails.Cast<object>().ToList());

         var chosenOption = fileSystem.ShowOptions("Import Sprite",
            $"This palette is used by {dependentPageCount} {imageType}. How do you want to handle the import?",
            allDetails.ToArray(),
            new VisualOption {
               Option = "Smart", Index = (int)ImportType.Smart,
               ShortDescription = "Fix Incompatibilites Automatically",
               Description = "Balance the look of the new image with other images already using this palette.",
            },
            new VisualOption {
               Option = "Greedy", Index = (int)ImportType.Greedy,
               ShortDescription = "Ignore other sprites",
               Description = "Ignore other images that use this palette. They'll probably look broken and that's ok.",
            },
            new VisualOption {
               Option = "Cautious", Index = (int)ImportType.Cautious,
               ShortDescription = "Don't change palette",
               Description = "Match the new image to the existing palette as closely as possible.",
            });

         usablePalettePages = palDetails.Count.Range().Where(i => palDetails[i].IsSet).ToList();
         return (ImportType)chosenOption;
      }

      private Point ImportSinglePageSpriteAndPalette(IFileSystem fileSystem, short[] image, ISpriteRun spriteRun, IPaletteRun paletteRun, ImportType importType = ImportType.Unknown, bool allowSpriteEdits = true) {
         var result = new Point(spriteRun.SpriteFormat.TileWidth * 8, spriteRun.SpriteFormat.TileHeight * 8);

         var dependentSprites = paletteRun?.FindDependentSprites(model) ?? new List<ISpriteRun>();
         if (dependentSprites.Count == 0 || // no sprites are associated with this palette. So just use the currently loaded sprite.
            (dependentSprites.Count == 1 && dependentSprites[0].Start == spriteRun.Start && (spriteRun.Pages == 1 || spriteRun.Pages == paletteRun.Pages)) || // 'I am the only sprite' case
            (dependentSprites.Count == 1 && dependentSprites[0] is ITilesetRun && spriteRun is ITilemapRun) // 'My tileset is the only sprite' case
            ) {
            // easy case: a single sprite. Sprite uses the palette.
            // there may be other palettes, but we can leave them be.
            WriteSpriteAndPalette(spriteRun, paletteRun, image, paletteRun?.Pages.Range().ToList());
            LoadSprite();
            LoadPalette();
            return result;
         }

         // there are multiple sprites
         var dependentPageCount = dependentSprites.Sum(s => s.Pages);
         if (paletteRun.Pages > 1) dependentPageCount = dependentSprites.Count; // for multi-page palettes, only count each sprite once.
         string imageType = "images";
         if (dependentSprites.Any(ds => ds is ITilesetRun)) imageType = "tilesets";
         var choice = GetImportType(fileSystem, dependentPageCount, dependentSprites, imageType, paletteRun, importType, out var usablePalPages);

         if (choice == ImportType.Smart) {
            var otherSprites = dependentSprites.Except(new[] { spriteRun }).ToList();
            WriteSpriteAndBalancePalette(spriteRun, paletteRun, otherSprites, image, usablePalPages);
         } else if (choice == ImportType.Greedy) {
            result = WriteSpriteAndPalette(spriteRun, paletteRun, image, usablePalPages, !allowSpriteEdits);
         } else if (choice == ImportType.Cautious) {
            WriteSpriteWithoutPalette(spriteRun, paletteRun, image, usablePalPages);
         }

         LoadSprite();
         LoadPalette();
         return result;
      }

      private void ImportWideSpriteAndPalette(IFileSystem fileSystem, short[] image, ISpriteRun spriteRun, IPaletteRun paletteRun) {
         var imageForPage = new short[spritePages][];
         for (int i = 0; i < spritePages; i++) {
            imageForPage[i] = new short[PixelWidth * PixelHeight];
            for (int y = 0; y < PixelHeight; y++) {
               var yStart = y * PixelWidth;
               Array.Copy(image, i * PixelWidth + yStart * spritePages, imageForPage[i], yStart, PixelWidth);
            }
         }

         var dependentSprites = paletteRun?.FindDependentSprites(model) ?? new List<ISpriteRun>();
         if (dependentSprites.Count == 0 || // no sprites are associated with this palette. So just use the currently loaded sprite.
            (dependentSprites.Count == 1 && dependentSprites[0].Start == spriteRun.Start) || // 'I am the only sprite' case
            (dependentSprites.Count == 1 && dependentSprites[0] is ITilesetRun && spriteRun is ITilemapRun) // 'My tileset is the only sprite' case
            ) {
            // easy case: a single sprite. Sprite uses the palette.
            // there may be other palettes, but we can leave them be.
            WriteSpritesAndPalette(spriteRun, paletteRun, imageForPage, paletteRun?.Pages.Range().ToList());
            LoadSprite();
            LoadPalette();
            return;
         }

         // there are multiple sprites
         var dependentPageCount = dependentSprites.Sum(s => s.Pages);
         if (paletteRun.Pages > 1) dependentPageCount = dependentSprites.Count; // for multi-page palettes, only count each sprite once.
         string imageType = "images";
         if (dependentSprites.Any(ds => ds is ITilesetRun)) imageType = "tilesets";
         var choice = GetImportType(fileSystem, dependentPageCount, dependentSprites, imageType, paletteRun, ImportType.Unknown, out var usablePalPages);

         if (choice == ImportType.Smart) {
            var otherSprites = dependentSprites.Except(new[] { spriteRun }).ToList();
            WriteSpritesAndBalancePalette(spriteRun, paletteRun, otherSprites, imageForPage, usablePalPages);
         } else if (choice == ImportType.Greedy) {
            WriteSpritesAndPalette(spriteRun, paletteRun, imageForPage, usablePalPages);
         } else if (choice == ImportType.Cautious) {
            WriteSpritesWithoutPalette(spriteRun, paletteRun, imageForPage, usablePalPages);
         }

         LoadSprite();
         LoadPalette();
      }

      private void ImportTallSpriteAndPalette(IFileSystem fileSystem, short[] image, ISpriteRun spriteRun, IPaletteRun paletteRun) {
         var imageForPage = new short[spritePages][];
         for (int i = 0; i < spritePages; i++) {
            imageForPage[i] = new short[PixelWidth * PixelHeight];
            Array.Copy(image, i * PixelWidth * PixelHeight, imageForPage[i], 0, PixelWidth * PixelHeight);
         }

         var dependentSprites = paletteRun?.FindDependentSprites(model) ?? new List<ISpriteRun>();
         if (dependentSprites.Count == 0 || // no sprites are associated with this palette. So just use the currently loaded sprite.
            (dependentSprites.Count == 1 && dependentSprites[0].Start == spriteRun.Start) || // 'I am the only sprite' case
            (dependentSprites.Count == 1 && dependentSprites[0] is ITilesetRun && spriteRun is ITilemapRun) // 'My tileset is the only sprite' case
            ) {
            // easy case: a single sprite. Sprite uses the palette.
            // there may be other palettes, but we can leave them be.
            WriteSpritesAndPalette(spriteRun, paletteRun, imageForPage, paletteRun?.Pages.Range().ToList());
            LoadSprite();
            LoadPalette();
            return;
         }

         // there are multiple sprites
         var dependentPageCount = dependentSprites.Sum(s => s.Pages);
         if (paletteRun.Pages > 1) dependentPageCount = dependentSprites.Count; // for multi-page palettes, only count each sprite once.
         string imageType = "images";
         if (dependentSprites.Any(ds => ds is ITilesetRun)) imageType = "tilesets";
         var choice = GetImportType(fileSystem, dependentPageCount, dependentSprites, imageType, paletteRun, ImportType.Unknown, out var usablePalPages);

         if (choice == ImportType.Smart) {
            var otherSprites = dependentSprites.Except(new[] { spriteRun }).ToList();
            WriteSpritesAndBalancePalette(spriteRun, paletteRun, otherSprites, imageForPage, usablePalPages);
         } else if (choice == ImportType.Greedy) {
            WriteSpritesAndPalette(spriteRun, paletteRun, imageForPage, usablePalPages);
         } else if (choice == ImportType.Cautious) {
            WriteSpritesWithoutPalette(spriteRun, paletteRun, imageForPage, usablePalPages);
         }

         LoadSprite();
         LoadPalette();
      }

      private bool TryValidate(short[] image, out ISpriteRun spriteRun, out IPaletteRun paletteRun) {
         spriteRun = model.GetNextRun(spriteAddress) as ISpriteRun;
         paletteRun = model.GetNextRun(paletteAddress) as IPaletteRun;
         if (spriteRun.SpriteFormat.BitsPerPixel < 4) paletteRun = null;

         // check 0: we actually have a sprite/palette to import on top of
         if (spriteRun == null) {
            viewPort.RaiseError("The sprite address is not valid!");
            return false;
         }

         if (!spriteRun.SpriteFormat.BitsPerPixel.IsAny(1, 2) && paletteRun == null) {
            viewPort.RaiseError("The palette address is not valid!");
            return false;
         }

         // check 1: we're allowed to import to this sprite
         if (!spriteRun.SupportsImport) {
            viewPort.RaiseError("This format does not support importing.");
            return false;
         }

         // check 2: image was actually loaded
         if (image == null) return false;

         return true;
      }

      private void WriteSpritesAndPalette(ISpriteRun spriteRun, IPaletteRun paletteRun, short[][] images, IReadOnlyList<int> usablePalPages) {
         var tiles = images.Select(image => Tilize(image, PixelWidth)).ToArray();
         var allTiles = tiles.SelectMany(tilesForImage => tilesForImage).ToArray();
         var expectedPalettePages = usablePalPages?.Count ?? 1;
         if (spriteRun.Pages == expectedPalettePages) expectedPalettePages = 1; // handle the Castform case
         if (expectedPalettePages == 0 && paletteRun != null) {
            viewPort.RaiseError("You must select at least one palette.");
            return;
         }

         // figure out the new palettes, using only the usable palettes. Leave the other palettes alone.
         var palettes = paletteRun?.Pages.Range().Select(i => paletteRun.GetPalette(model, i)).ToArray();
         if (palettes != null && usablePalPages.All(upp => palettes.Length > upp)) {
            var newPalettes = usablePalPages.Select(i => palettes[i]).ToArray();
            newPalettes = DiscoverPalettes(allTiles.ToArray(), paletteRun?.PaletteFormat.Bits ?? 1, expectedPalettePages, newPalettes);
            for (int i = 0; i < usablePalPages.Count; i++) palettes[usablePalPages[i]] = newPalettes[usablePalPages[i]];
         }

         var newSprite = spriteRun;
         for (int page = 0; page < images.Length; page++) {
            var indexedTiles = new int[tiles[page].Length][,];
            for (int i = 0; i < indexedTiles.Length; i++) indexedTiles[i] = Index(tiles[page][i], palettes, usablePalPages, spriteRun.SpriteFormat.BitsPerPixel, paletteRun?.PaletteFormat.InitialBlankPages ?? 0);
            var sprite = Detilize(indexedTiles, spriteRun.SpriteFormat.TileWidth);
            newSprite = newSprite.SetPixels(model, viewPort.CurrentChange, page, sprite);
            if (newSprite is ITilemapRun tilemap && model.GetNextRun(tilemap.FindMatchingTileset(model)) is ITilesetRun tileset && model.ReadMultiByteValue(tileset.Start + 1, 3) / (tileset.SpriteFormat.BitsPerPixel * 8) == 1024) {
               viewPort.RaiseError("Maxed out number of available tiles. Simplify your image.");
            }
         }

         var newPalette = paletteRun;
         if (newPalette != null) {
            if (palettes.Length == paletteRun.Pages) {
               for (int i = 0; i < palettes.Length; i++) newPalette = newPalette.SetPalette(model, viewPort.CurrentChange, i, palettes[i]);
            } else {
               newPalette = newPalette.SetPalette(model, viewPort.CurrentChange, palPage, palettes[0]);
            }
         }

         ExplainMoves(spriteRun, newSprite, paletteRun, newPalette);
      }

      private void WriteSpritesAndBalancePalette(ISpriteRun spriteRun, IPaletteRun paletteRun, List<ISpriteRun> otherSprites, short[][] images, IReadOnlyList<int> usablePalPages) {
         var tiles = images.Select(image => Tilize(image, PixelWidth)).ToArray();
         var allTiles = tiles.SelectMany(tilesForImage => tilesForImage).ToArray();

         if (usablePalPages.Count == 0) {
            viewPort.RaiseError("You must select at least one palette.");
            return;
         }

         // figure out the new palettes, using only the usable palettes. Leave the other palettes alone.
         var palettes = paletteRun.Pages.Range().Select(i => paletteRun.GetPalette(model, i)).ToArray();
         if (spriteRun.Pages == paletteRun.Pages) palettes = new[] { palettes[palPage] };
         var initialBlankPages = paletteRun.PaletteFormat.InitialBlankPages;
         var bits = paletteRun.PaletteFormat.Bits;

         // part 1: weigh everything using the current palettes, so we know how much force it should resist
         var weightedPalettes = palettes.Select(p => new WeightedPalette(p, p.Count)).ToList();
         foreach (var spriteSet in otherSprites) {
            IReadOnlyList<ISpriteRun> spritesToWeigh = new List<ISpriteRun> { spriteSet };
            if (spriteSet is ITilesetRun tileset) spritesToWeigh = tileset.FindDependentTilemaps(model);
            foreach (var sprite in spritesToWeigh) {
               if (sprite.Pages == paletteRun.Pages) {
                  // weigh only the current page
                  weightedPalettes[0] = weightedPalettes[0].Merge(WeightedPalette.Weigh(sprite.GetPixels(model, palPage), palettes[0], palettes[0].Count), out var _);
               } else {
                  // weigh all pages
                  // we generally expect to either only have one sprite page, and want to weigh every palette it uses...
                  // ... or have one palette page, and want to weight every sprite page that uses it.
                  for (int page = 0; page < sprite.Pages; page++) {
                     var newWeights = WeightedPalette.Weigh(sprite.GetPixels(model, page), palettes, bits, initialBlankPages);
                     for (int j = 0; j < weightedPalettes.Count; j++) weightedPalettes[j] = weightedPalettes[j].Merge(newWeights[j], out var _);
                  }
               }
            }
         }
         if (spriteRun.Pages != paletteRun.Pages) {
            for (int page = 0; page < spriteRun.Pages; page++) {
               if (page == spritePage) continue;
               var newWeights = WeightedPalette.Weigh(spriteRun.GetPixels(model, page), palettes, bits, initialBlankPages);
               for (int j = 0; j < weightedPalettes.Count; j++) weightedPalettes[j] = weightedPalettes[j].Merge(newWeights[j], out var _);
            }
         }

         // part 2: build palettes for the new tiles
         var expectedPalettePages = paletteRun.Pages;
         if (spriteRun.Pages == paletteRun.Pages) expectedPalettePages = 1; // handle the Castfrom case

         var newPalettes = new IReadOnlyList<short>[palettes.Length];
         var tempPalettes = usablePalPages.Select(i => palettes[i]).ToArray();
         tempPalettes = DiscoverPalettes(allTiles, bits, palettes.Length, tempPalettes);
         for (int i = 0; i < palettes.Length; i++) {
            if (i.IsAny(usablePalPages.ToArray())) newPalettes[i] = tempPalettes[usablePalPages.IndexOf(i)];
            else newPalettes[i] = palettes[i];
         }
         var allIndexedTiles = new int[allTiles.Length][,];
         for (int i = 0; i < allIndexedTiles.Length; i++) allIndexedTiles[i] = Index(allTiles[i], newPalettes, usablePalPages, spriteRun.SpriteFormat.BitsPerPixel, initialBlankPages);

         var spriteData = Detilize(allIndexedTiles, spriteRun.SpriteFormat.TileWidth);
         var newWeightedPalettes = WeightedPalette.Weigh(spriteData, newPalettes, bits, initialBlankPages);

         // part 3: merge the new desired palettes with the existing palettes
         weightedPalettes = WeightedPalette.MergeLists(weightedPalettes, newWeightedPalettes).ToList();
         newPalettes = weightedPalettes.Select(wp => wp.Palette).ToArray();

         // part 4: update all the other sprites to use the new palettes
         bool otherSpritesMoved = false;
         foreach (var spriteSet in otherSprites) {
            IReadOnlyList<ISpriteRun> spritesToWeigh = new List<ISpriteRun> { spriteSet };
            if (spriteSet is ITilesetRun tileset) spritesToWeigh = tileset.FindDependentTilemaps(model);
            foreach (var sprite in spritesToWeigh) {
               if (sprite == spriteRun) continue; // don't update the current sprite, we're going to do that later
               if (WeightedPalette.Update(model, viewPort.CurrentChange, sprite, palettes, newPalettes, initialBlankPages).Start != sprite.Start) otherSpritesMoved = true;
            }
         }
         var currentSpriteRun = spriteRun;
         if (spriteRun.Pages != paletteRun.Pages) {
            for (int page = 0; page < spriteRun.Pages; page++) {
               if (page == spritePage) continue;
               currentSpriteRun = WeightedPalette.Update(model, viewPort.CurrentChange, currentSpriteRun, palettes, newPalettes, initialBlankPages, page);
            }
         }

         // part 5: update the current sprite to use the new palette
         for (int page = 0; page < images.Length; page++) {
            var indexedTiles = new int[tiles[page].Length][,];
            for (int i = 0; i < indexedTiles.Length; i++) indexedTiles[i] = Index(tiles[page][i], newPalettes, usablePalPages, spriteRun.SpriteFormat.BitsPerPixel, initialBlankPages);
            spriteData = Detilize(indexedTiles, spriteRun.SpriteFormat.TileWidth);
            currentSpriteRun = currentSpriteRun.SetPixels(model, viewPort.CurrentChange, page, spriteData);
         }

         // part 6: update the palette
         var newPalette = paletteRun;
         if (palettes.Length == paletteRun.Pages) {
            for (int i = 0; i < palettes.Length; i++) newPalette = newPalette.SetPalette(model, viewPort.CurrentChange, i, newPalettes[i]);
         } else {
            newPalette = newPalette.SetPalette(model, viewPort.CurrentChange, palPage, palettes[0]);
         }

         if (otherSpritesMoved) {
            viewPort.RaiseMessage($"Other sprites using this palette were moved. Pointers have been updated.");
         }
         ExplainMoves(spriteRun, currentSpriteRun, paletteRun, newPalette);
      }

      private void WriteSpritesWithoutPalette(ISpriteRun spriteRun, IPaletteRun paletteRun, short[][] images, IReadOnlyList<int> usablePalPages) {
         if (usablePalPages.Count == 0 && paletteRun != null) {
            viewPort.RaiseError("You must select at least one palette.");
            return;
         }

         var tiles = images.Select(image => Tilize(image, PixelWidth)).ToArray();

         IReadOnlyList<short>[] palettes;
         if (spriteRun.Pages == paletteRun.Pages) {
            palettes = new IReadOnlyList<short>[1];
            palettes[0] = paletteRun.GetPalette(model, palPage);
         } else {
            palettes = new IReadOnlyList<short>[paletteRun.Pages];
            for (int i = 0; i < palettes.Length; i++) palettes[i] = paletteRun.GetPalette(model, i);
         }

         var newSprite = spriteRun;
         for (int page = 0; page < images.Length; page++) {
            var indexedTiles = new int[tiles[page].Length][,];
            for (int i = 0; i < indexedTiles.Length; i++) indexedTiles[i] = Index(tiles[page][i], palettes, usablePalPages, spriteRun.SpriteFormat.BitsPerPixel, paletteRun.PaletteFormat.InitialBlankPages);
            var sprite = Detilize(indexedTiles, spriteRun.SpriteFormat.TileWidth);

            newSprite = newSprite.SetPixels(model, viewPort.CurrentChange, page, sprite);
         }

         ExplainMoves(spriteRun, newSprite, paletteRun, paletteRun);
      }

      private Point WriteSpriteAndPalette(ISpriteRun spriteRun, IPaletteRun paletteRun, short[] image, IReadOnlyList<int> usablePalPages, bool noSpriteEdits = false) {
         var tiles = Tilize(image, PixelWidth);
         var expectedPalettePages = paletteRun?.Pages ?? 1;
         bool palettePerSprite = false;
         if (spriteRun.Pages == expectedPalettePages && expectedPalettePages > 1) {
            palettePerSprite = true;
            expectedPalettePages = 1; // handle the Castform case
         }

         if (expectedPalettePages == 0 && paletteRun != null) {
            viewPort.RaiseError("You must select at least one palette.");
            return new Point(PixelWidth, PixelHeight);
         }

         // figure out the new palettes, using only the usable palettes. Leave the other palettes alone.
         var palettes = paletteRun?.Pages.Range().Select(i => paletteRun.GetPalette(model, i)).ToArray();
         if (palettes != null) {
            var newPalettes = usablePalPages.Select(i => palettes[i]).ToArray();
            var allColors = palettes.SelectMany(p => p).ToList();
            if (spriteRun.SpriteFormat.BitsPerPixel == 8 && paletteRun.PaletteFormat.Bits == 4 && usablePalPages.Count == palettes.Length && image.All(allColors.Contains)) {
               // this is an 8-bit sprite with a 4-bit palette
               // if the original palette works, then don't change it.
            } else {
               newPalettes = DiscoverPalettes(tiles, spriteRun.SpriteFormat.BitsPerPixel, usablePalPages.Count, newPalettes);
               for (int i = 0; i < usablePalPages.Count; i++) palettes[usablePalPages[i]] = newPalettes[i];
            }
         }

         var needWriteSpriteData = true;
         var problemPixel = TryReorderPalettesFromMatchingSprite(palettes, image, spriteRun.GetPixels(model, spritePage));
         if (problemPixel.X == PixelWidth && problemPixel.Y == PixelHeight) {
            // palette is reordered, everything matches up with the original data. No need to write sprite data.
            needWriteSpriteData = false;
         } else if (noSpriteEdits) {
            // we were requested to not make sprite edits, return the pixel that prevents us from safely importing this way
            return problemPixel;
         }

         var indexedTiles = new int[tiles.Length][,];
         for (int i = 0; i < indexedTiles.Length; i++) indexedTiles[i] = Index(tiles[i], palettes, usablePalPages, spriteRun.SpriteFormat.BitsPerPixel, paletteRun?.PaletteFormat.InitialBlankPages ?? 0, palettePerSprite);
         var sprite = Detilize(indexedTiles, spriteRun.SpriteFormat.TileWidth);

         var newSprite = needWriteSpriteData ? spriteRun.SetPixels(model, viewPort.CurrentChange, spritePage, sprite) : spriteRun;
         if (newSprite is ITilemapRun tilemap && model.GetNextRun(tilemap.FindMatchingTileset(model)) is ITilesetRun tileset && model.ReadMultiByteValue(tileset.Start + 1, 3) / (tileset.SpriteFormat.BitsPerPixel * 8) == 1024) {
            viewPort.RaiseError("Maxed out number of available tiles. Simplify your image.");
         }

         var newPalette = paletteRun;
         if (newPalette != null) {
            if (palettes.Length == paletteRun.Pages) {
               for (int i = 0; i < palettes.Length; i++) newPalette = newPalette.SetPalette(model, viewPort.CurrentChange, i, palettes[i]);
            } else {
               newPalette = newPalette.SetPalette(model, viewPort.CurrentChange, palPage, palettes[0]);
            }
         }

         ExplainMoves(spriteRun, newSprite, paletteRun, newPalette);
         return new Point(PixelWidth, PixelHeight);
      }

      private void WriteSpriteWithoutPalette(ISpriteRun spriteRun, IPaletteRun paletteRun, short[] image, IReadOnlyList<int> usablePalPages) {
         if (usablePalPages.Count == 0 && paletteRun != null) {
            viewPort.RaiseError("You must select at least one palette.");
            return;
         }

         var tiles = Tilize(image, PixelWidth);
         IReadOnlyList<short>[] palettes;
         if (spriteRun.Pages == paletteRun.Pages) {
            palettes = new IReadOnlyList<short>[1];
            palettes[0] = paletteRun.GetPalette(model, palPage);
         } else {
            palettes = new IReadOnlyList<short>[paletteRun.Pages];
            for (int i = 0; i < palettes.Length; i++) palettes[i] = paletteRun.GetPalette(model, i);
         }
         var indexedTiles = new int[tiles.Length][,];
         for (int i = 0; i < indexedTiles.Length; i++) indexedTiles[i] = Index(tiles[i], palettes, usablePalPages, spriteRun.SpriteFormat.BitsPerPixel, paletteRun.PaletteFormat.InitialBlankPages);
         var sprite = Detilize(indexedTiles, spriteRun.SpriteFormat.TileWidth);

         var newSprite = spriteRun.SetPixels(model, viewPort.CurrentChange, spritePage, sprite);

         ExplainMoves(spriteRun, newSprite, paletteRun, paletteRun);
      }

      private void WriteSpriteAndBalancePalette(ISpriteRun spriteRun, IPaletteRun paletteRun, IList<ISpriteRun> otherSprites, short[] image, IReadOnlyList<int> usablePalPages) {
         if (usablePalPages.Count == 0) {
            viewPort.RaiseError("You must select at least one palette.");
            return;
         }

         var tiles = Tilize(image, PixelWidth);
         var palettes = paletteRun.Pages.Range().Select(i => paletteRun.GetPalette(model, i)).ToArray();
         if (spriteRun.Pages == paletteRun.Pages) {
            palettes = new[] { palettes[palPage] };
            usablePalPages = new List<int> { 0 };
         }
         var initialBlankPages = paletteRun.PaletteFormat.InitialBlankPages;
         var bits = paletteRun.PaletteFormat.Bits;

         // part 1: weigh everything using the current palettes, so we know how much force it should resist
         var weightedPalettes = palettes.Select(p => new WeightedPalette(p, p.Count)).ToList();
         foreach (var spriteSet in otherSprites) {
            IReadOnlyList<ISpriteRun> spritesToWeigh = new List<ISpriteRun> { spriteSet };
            if (spriteSet is ITilesetRun tileset) spritesToWeigh = tileset.FindDependentTilemaps(model);
            foreach (var sprite in spritesToWeigh) {
               if (sprite.Pages == paletteRun.Pages) {
                  // weigh only the current page
                  weightedPalettes[0] = weightedPalettes[0].Merge(WeightedPalette.Weigh(sprite.GetPixels(model, palPage), palettes[0], palettes[0].Count), out var _);
               } else {
                  // weigh all pages
                  // we generally expect to either only have one sprite page, and want to weigh every palette it uses...
                  // ... or have one palette page, and want to weight every sprite page that uses it.
                  for (int page = 0; page < sprite.Pages; page++) {
                     var newWeights = WeightedPalette.Weigh(sprite.GetPixels(model, page), palettes, bits, initialBlankPages);
                     for (int j = 0; j < weightedPalettes.Count; j++) weightedPalettes[j] = weightedPalettes[j].Merge(newWeights[j], out var _);
                  }
               }
            }
         }
         if (spriteRun.Pages != paletteRun.Pages) {
            for (int page = 0; page < spriteRun.Pages; page++) {
               if (page == spritePage) continue;
               var newWeights = WeightedPalette.Weigh(spriteRun.GetPixels(model, page), palettes, bits, initialBlankPages);
               for (int j = 0; j < weightedPalettes.Count; j++) weightedPalettes[j] = weightedPalettes[j].Merge(newWeights[j], out var _);
            }
         }

         // part 2: build palettes for the new tiles
         var expectedPalettePages = paletteRun.Pages;
         if (spriteRun.Pages == paletteRun.Pages) expectedPalettePages = 1; // handle the Castfrom case

         var newPalettes = new IReadOnlyList<short>[palettes.Length];
         var tempPalettes = usablePalPages.Select(i => palettes[i]).ToArray();
         tempPalettes = DiscoverPalettes(tiles, bits, palettes.Length, tempPalettes);
         for (int i = 0; i < palettes.Length; i++) {
            if (i.IsAny(usablePalPages.ToArray())) newPalettes[i] = tempPalettes[usablePalPages.IndexOf(i)];
            else newPalettes[i] = palettes[i];
         }

         TryReorderPalettesFromMatchingSprite(newPalettes, image, spriteRun.GetPixels(model, spritePage));
         var indexedTiles = new int[tiles.Length][,];
         for (int i = 0; i < indexedTiles.Length; i++) indexedTiles[i] = Index(tiles[i], newPalettes, usablePalPages, spriteRun.SpriteFormat.BitsPerPixel, initialBlankPages);
         var spriteData = Detilize(indexedTiles, spriteRun.SpriteFormat.TileWidth);
         var newWeightedPalettes = WeightedPalette.Weigh(spriteData, newPalettes, bits, initialBlankPages);

         // part 3: merge the new desired palettes with the existing palettes
         weightedPalettes = WeightedPalette.MergeLists(weightedPalettes, newWeightedPalettes).ToList();
         newPalettes = weightedPalettes.Select(wp => wp.Palette).ToArray();

         // part 4: update all the other sprites to use the new palettes
         bool otherSpritesMoved = false;
         foreach (var spriteSet in otherSprites) {
            IReadOnlyList<ISpriteRun> spritesToWeigh = new List<ISpriteRun> { spriteSet };
            if (spriteSet is ITilesetRun tileset) spritesToWeigh = tileset.FindDependentTilemaps(model);
            foreach (var sprite in spritesToWeigh) {
               if (sprite == spriteRun) continue; // don't update the current sprite, we're going to do that later
               if (sprite.Pages == paletteRun.Pages) {
                  if (WeightedPalette.Update(model, viewPort.CurrentChange, sprite, palettes, newPalettes, initialBlankPages, palPage).Start != sprite.Start) otherSpritesMoved = true;
               } else {
                  if (WeightedPalette.Update(model, viewPort.CurrentChange, sprite, palettes, newPalettes, initialBlankPages).Start != sprite.Start) otherSpritesMoved = true;
               }
            }
         }
         var currentSpriteRun = spriteRun;
         if (spriteRun.Pages != paletteRun.Pages) {
            for (int page = 0; page < currentSpriteRun.Pages; page++) {
               if (page == spritePage) continue;
               currentSpriteRun = WeightedPalette.Update(model, viewPort.CurrentChange, currentSpriteRun, palettes, newPalettes, initialBlankPages, page);
            }
         }

         // part 5: update the current sprite to use the new palette
         for (int i = 0; i < indexedTiles.Length; i++) indexedTiles[i] = Index(tiles[i], newPalettes, usablePalPages, spriteRun.SpriteFormat.BitsPerPixel, initialBlankPages);
         spriteData = Detilize(indexedTiles, spriteRun.SpriteFormat.TileWidth);
         currentSpriteRun = currentSpriteRun.SetPixels(model, viewPort.CurrentChange, spritePage, spriteData);

         // part 6: update the palette
         var newPalette = paletteRun;
         if (palettes.Length == paletteRun.Pages) {
            for (int i = 0; i < palettes.Length; i++) newPalette = newPalette.SetPalette(model, viewPort.CurrentChange, i, newPalettes[i]);
         } else {
            newPalette = newPalette.SetPalette(model, viewPort.CurrentChange, palPage, newPalettes[0]);
         }

         if (otherSpritesMoved) {
            viewPort.RaiseMessage($"Other sprites using this palette were moved. Pointers have been updated.");
         }
         ExplainMoves(spriteRun, currentSpriteRun, paletteRun, newPalette);
      }

      private static IReadOnlyList<short>[] DiscoverPalettes(short[][] tiles, int bitness, int paletteCount, IReadOnlyList<short>[] existingPalettes) {
         if (bitness == 1) return new[] { TileViewModel.CreateDefaultPalette(2) };
         if (bitness == 2) return new[] { TileViewModel.CreateDefaultPalette(4) };
         var targetColors = Math.Min((int)Math.Pow(2, bitness), existingPalettes.Sum(pal => pal.Count));

         // special case: we don't need to run palette discovery if the existing palette works
         bool allTilesFitExistingPalettes = true;
         foreach (var tile in tiles) {
            if (existingPalettes?.Any(pal => tile.All(pal.Contains)) ?? false) continue;
            allTilesFitExistingPalettes = false;
            break;
         }
         if (allTilesFitExistingPalettes && paletteCount == existingPalettes.Length) return existingPalettes;

         int palettePageCount = paletteCount;
         if (bitness == 8) paletteCount = 1;
         var palettes = new WeightedPalette[paletteCount];
         for (int i = 0; i < paletteCount; i++) palettes[i] = WeightedPalette.Reduce(tiles[i], targetColors);
         for (int i = paletteCount; i < tiles.Length; i++) {
            var newPalette = WeightedPalette.Reduce(tiles[i], targetColors);
            var (a, b) = WeightedPalette.CheapestMerge(palettes, newPalette);
            if (b < paletteCount) {
               palettes[a] = palettes[a].Merge(palettes[b], out var _);
               palettes[b] = newPalette;
            } else {
               palettes[a] = palettes[a].Merge(newPalette, out var _);
            }
         }

         // if this is an 8-bit image using 16-color palettes, split the resulting colors into arbitrary palettes.
         if (bitness == 8 && palettePageCount > 1) {
            var result = new List<IReadOnlyList<short>>();
            for (int i = 0; i < palettePageCount; i++) {
               result.Add(palettes[0].Palette.Skip(16 * i).Take(16).ToList());
            }
            return result.ToArray();
         }

         return palettes.Select(p => p.Palette).ToArray();
      }

      public static int[,] Index(short[] tile, IReadOnlyList<short>[] palettes, IReadOnlyList<int> usablePalPages, int bitness, int initialPageIndex, bool palettePerSprite = false) {
         var cheapestIndex = 0;
         if (bitness == 8 && usablePalPages.Count == palettes.Length) {
            palettes = new[] { palettes.SelectMany(pal => pal).ToList() };
            usablePalPages = new[] { 0 };
         } else if (bitness < 4) {
            // no palette: build a default palette
            palettes = new[] { TileViewModel.CreateDefaultPalette(bitness * 2) };
         }
         var cheapest = (usablePalPages != null && usablePalPages.Contains(0)) ? WeightedPalette.CostToUse(tile, palettes[0]) : double.PositiveInfinity;
         for (int i = 1; i < palettes.Length; i++) {
            if (cheapest == 0) break;
            if (usablePalPages != null && !usablePalPages.Contains(i)) continue;
            var cost = WeightedPalette.CostToUse(tile, palettes[i]);
            if (cost >= cheapest) continue;
            cheapest = cost;
            cheapestIndex = i;
         }
         var index = WeightedPalette.Index(tile, palettes[cheapestIndex]);
         for (int x = 0; x < index.GetLength(0); x++) {
            for (int y = 0; y < index.GetLength(1); y++) {
               // special case: 256-color sprites are still allowed to use the transparent color
               if (bitness == 8 && index[x, y] == 0) continue;
               if (palettePerSprite) continue; // special case: don't add the palette page offset if this is a bank of sprite-palette pairs
               index[x, y] += (initialPageIndex + cheapestIndex) << 4;
            }
         }
         return index;
      }

      private void ExplainMoves(ISpriteRun originalSprite, ISpriteRun newSprite, IPaletteRun originalPalette, IPaletteRun newPalette) {
         bool spriteMoved = originalSprite.Start != newSprite.Start;
         bool paletteMoved = originalPalette?.Start != newPalette?.Start;

         if (spriteMoved && !paletteMoved) {
            viewPort.Goto.Execute(newSprite.Start);
            viewPort.RaiseMessage($"Sprite moved to {newSprite.Start:X6}. Pointers have been updated.");
         } else if (paletteMoved && !spriteMoved) {
            viewPort.Goto.Execute(newPalette.Start);
            viewPort.RaiseMessage($"Palette moved to {newPalette.Start:X6}. Pointers have been updated.");
         } else if (spriteMoved && paletteMoved) {
            viewPort.Goto.Execute(newSprite.Start);
            viewPort.RaiseMessage($"Sprite and palette moved to {newSprite.Start:X6} and {newPalette.Start:X6}.");
         } else {
            viewPort.Refresh();
         }
      }

      public static short[][] Tilize(short[] image, int width) {
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

      public static int[,] Detilize(int[][,] tiles, int tileWidth) {
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

      /// <summary>
      /// Given a set of indexed pixels and a new image,
      /// Edit a palette to be equal to the colors needed to map
      /// the indexed pixels into the new image.
      /// Note that if the image doesn't fit the indexed pixels, this method will return the pixels that caused the failure.
      /// This method will return (-1, -1) if some other issue is encountered, and return (width, height) if everything worked.
      /// </summary>
      public static Point TryReorderPalettesFromMatchingSprite(IReadOnlyList<short>[] palettes, short[] image, int[,] pixels) {
         if (palettes == null || palettes.Length != 1) return new Point(-1, -1);
         if (palettes[0].Count > 16) return new Point(-1, -1);
         var newPalette = new short[16];
         for (int i = 0; i < newPalette.Length; i++) newPalette[i] = -1;

         var width = pixels.GetLength(0);
         var height = pixels.GetLength(1);
         for (int x = 0; x < width; x++) {
            for (int y = 0; y < height; y++) {
               var currentColor = image[y * width + x];
               var currentIndex = pixels[x, y] % newPalette.Length;
               if (newPalette[currentIndex] == -1) newPalette[currentIndex] = currentColor;
               if (newPalette[currentIndex] != currentColor) return new Point(x, y);
            }
         }

         // if there were any unused indexes, set thier color to black
         for (int i = 0; i < newPalette.Length; i++) {
            if (newPalette[i] == -1) newPalette[i] = 0;
         }

         palettes[0] = newPalette;
         return new Point(width, height);
      }

      private void ExportSpriteAndPalette(IFileSystem fileSystem) {
         fileSystem.SaveImage(PixelData, PixelWidth);
      }
   }

   public enum ImportType {
      Unknown = -1,
      Smart = 0,
      Greedy = 1,
      Cautious = 2,
   }

   public class FlagViewModel : ViewModelCore {
      private string name;
      public string Name { get => name; set => Set(ref name, value); }
      private bool isSet;
      public bool IsSet { get => isSet; set => Set(ref isSet, value); }

      public FlagViewModel(string name) => (Name, IsSet) = (name, true);
   }

   public class WeightedPalette {
      public int TargetLength { get; }
      public IReadOnlyList<int> Weight { get; }
      public IReadOnlyList<short> Palette { get; }

      public WeightedPalette(IReadOnlyList<short> palette, int targetLength) : this(palette, new int[palette.Count], targetLength) { }

      private WeightedPalette(IReadOnlyList<short> palette, IReadOnlyList<int> weight, int targetLength) => (Palette, Weight, TargetLength) = (palette, weight, targetLength);

      public static WeightedPalette Weigh(int[,] sprite, IReadOnlyList<short> palette, int targetLength) {
         var weight = new int[targetLength];
         foreach (var num in sprite) weight[num % targetLength]++;
         return new WeightedPalette(palette, weight, targetLength);
      }

      /// <summary>
      /// Build a set of weighted palettes from a sprite that uses a set fo palettes.
      /// </summary>
      public static IReadOnlyList<WeightedPalette> Weigh(int[,] sprite, IReadOnlyList<short>[] palettes, int bitness, int pageOffset) {
         var targetLength = (int)Math.Pow(2, bitness);
         var weights = new int[palettes.Length][];
         for (int i = 0; i < palettes.Length; i++) weights[i] = new int[targetLength];

         for (int x = 0; x < sprite.GetLength(0); x++) {
            for (int y = 0; y < sprite.GetLength(1); y++) {
               var index = sprite[x, y] - (pageOffset << 4);
               var paletteIndex = (index >> 4) % palettes.Length;
               if (index < 0) continue; // we don't care about weighing colors from a palette that's out-of-range
               index -= paletteIndex << 4;
               if (index > weights[paletteIndex].Length) continue; // we don't care about weighing colors from a palette that's out-of-range
               weights[paletteIndex][index]++;
            }
         }

         var results = new List<WeightedPalette>();
         for (int i = 0; i < palettes.Length; i++) {
            results.Add(new WeightedPalette(palettes[i], weights[i], targetLength));
         }
         return results;
      }

      /// <summary>
      /// Given two sets of n palettes, merge them into a single set of n palettes
      /// </summary>
      public static IReadOnlyList<WeightedPalette> MergeLists(IReadOnlyList<WeightedPalette> set1, IReadOnlyList<WeightedPalette> set2) {
         Debug.Assert(set1.Count == set2.Count);
         var result = set1.ToList();
         foreach (var palette in set2) {
            var bestMerge = result[0].Merge(palette, out var bestCost);
            var bestIndex = 0;
            for (int i = 1; i < result.Count; i++) {
               if (bestCost == 0) break;
               var currentMerge = result[i].Merge(palette, out var currentCost);
               if (currentCost >= bestCost) continue;
               bestCost = currentCost;
               bestMerge = currentMerge;
               bestIndex = i;
            }
            result[bestIndex] = bestMerge;
         }
         return result;
      }

      public static ISpriteRun Update(IDataModel model, ModelDelta token, ISpriteRun spriteRun, IReadOnlyList<short>[] originalPalettes, IReadOnlyList<short>[] newPalettes, int initialPaletteOffset) {
         for (int i = 0; i < spriteRun.Pages; i++) {
            spriteRun = Update(model, token, spriteRun, originalPalettes, newPalettes, initialPaletteOffset, i);
         }
         return spriteRun;
      }

      /// <summary>
      /// Update a sprite to use a new palette
      /// </summary>
      public static ISpriteRun Update(IDataModel model, ModelDelta token, ISpriteRun spriteRun, IReadOnlyList<short>[] originalPalettes, IReadOnlyList<short>[] newPalettes, int initialPaletteOffset, int page) {
         var pixels = spriteRun.GetPixels(model, page);

         if (spriteRun is LzTilemapRun tileMap) {
            var image = SpriteTool.Render(pixels, originalPalettes.SelectMany(s => s).ToList(), initialPaletteOffset, page);
            var tiles = SpriteTool.Tilize(image, spriteRun.SpriteFormat.TileWidth * 8);
            var indexedTiles = new int[tiles.Length][,];
            for (int i = 0; i < indexedTiles.Length; i++) indexedTiles[i] = SpriteTool.Index(tiles[i], newPalettes, null, spriteRun.SpriteFormat.BitsPerPixel, initialPaletteOffset);
            pixels = SpriteTool.Detilize(indexedTiles, spriteRun.SpriteFormat.TileWidth);
         } else {
            var indexMapping = CreatePixelMapping(originalPalettes, newPalettes, initialPaletteOffset);

            for (int x = 0; x < pixels.GetLength(0); x++) {
               for (int y = 0; y < pixels.GetLength(1); y++) {
                  var pixel = pixels[x, y];
                  while (pixel < (initialPaletteOffset << 4)) pixel += initialPaletteOffset << 4; // handle tiles mapped to no palette. Map them to the first palette.
                  pixels[x, y] = pixel >= indexMapping.Count ? pixel : indexMapping[pixel];  // don't remap any pixel that's using an unknown palette
               }
            }
         }

         spriteRun = spriteRun.SetPixels(model, token, page, pixels);
         return spriteRun;
      }

      public static IDictionary<int, int> CreatePixelMapping(IReadOnlyList<short>[] originalPalettes, IReadOnlyList<short>[] newPalettes, int initialPaletteOffset) {
         var result = new Dictionary<int, int>();
         for (int i = 0; i < originalPalettes.Length; i++) {
            var originalPaletteGroup = (i + initialPaletteOffset) << 4;
            var originalPalette = originalPalettes[i];
            var newPaletteIndex = TargetPalette(newPalettes, originalPalette);
            var newPalette = newPalettes[newPaletteIndex];
            var newPaletteGroup = (newPaletteIndex + initialPaletteOffset) << 4;
            for (int j = 0; j < originalPalette.Count; j++) {
               var colorIndex = BestMatch(originalPalette[j], newPalette);
               result[originalPaletteGroup + j] = newPaletteGroup + colorIndex;
            }
         }
         return result;
      }

      public static int TargetPalette(IReadOnlyList<short>[] options, IReadOnlyList<short> oldPalette) {
         var bestDistance = double.PositiveInfinity;
         var bestIndex = -1;
         for (int i = 0; i < options.Length; i++) {
            var currentDistance = 0.0;
            for (int j = 0; j < oldPalette.Count; j++) {
               var index = BestMatch(oldPalette[j], options[i]);
               currentDistance += Distance(options[i][index], oldPalette[j]);
            }
            if (bestDistance <= currentDistance) continue;
            bestDistance = currentDistance;
            bestIndex = i;
         }
         return bestIndex;
      }

      /// <summary>
      /// Creates a WeightedPalette from a non-indexed tile
      /// </summary>
      public static WeightedPalette Reduce(short[] rawColors, int targetColors) {
         var allColors = new Dictionary<short, ColorMass>();
         for (int i = 0; i < rawColors.Length; i++) {
            var color = rawColors[i];
            if (!allColors.ContainsKey(color)) allColors[color] = new ColorMass(color, 0);
            allColors[color] = new ColorMass(color, allColors[color].Mass + 1);
         }
         var palette = allColors.Values.ToList();
         return Reduce(palette, targetColors, out var _);
      }

      /// <summary>
      /// Reduces a number of colors to a target length or shorter, and reports how much force was needed to do it.
      /// </summary>
      public static WeightedPalette Reduce(IList<ColorMass> palette, int targetColors, out double cost) {
         cost = 0;
         if (palette.Count <= targetColors) return new WeightedPalette(palette.Select(p => p.ResultColor).ToList(), palette.Select(p => p.Mass).ToList(), targetColors);
         while (palette.Count > targetColors) {
            var bestPair = CheapestMerge(palette, out var bestCost);
            var combine1 = palette[bestPair.Item1];
            var combine2 = palette[bestPair.Item2];
            palette.RemoveAt(bestPair.Item2);
            palette[bestPair.Item1] = combine1 + combine2;
            cost += bestCost;
         }
         var resultPalette = palette.Select(p => p.ResultColor).ToList();
         var resultMasses = palette.Select(p => p.Mass).ToList();
         return new WeightedPalette(resultPalette, resultMasses, targetColors);
      }

      public static (int a, int b) CheapestMerge(IList<ColorMass> palette, out double cost) {
         var bestPair = (0, 1);
         var bestCost = double.PositiveInfinity;
         for (int i = 0; i < palette.Count - 1; i++) {
            for (int j = i + 1; j < palette.Count; j++) {
               var currentCost = palette[i] * palette[j];
               if (currentCost >= bestCost) continue;
               bestCost = currentCost;
               bestPair = (i, j);
            }
         }
         cost = bestCost;
         return bestPair;
      }

      public static (int a, int b) CheapestMerge(IReadOnlyList<WeightedPalette> palettes, WeightedPalette newPalette) {
         if (palettes.Count == 1) return (0, 1);

         var bestPair = (0, 0);
         double bestCost = double.PositiveInfinity;

         for (int i = 0; i < palettes.Count; i++) {
            for (int j = i + 1; j < palettes.Count; j++) {
               if (bestCost == 0) break;
               palettes[i].Merge(palettes[j], out var currentCost);
               if (currentCost >= bestCost) continue;
               bestCost = currentCost;
               bestPair = (i, j);
            }
            if (bestCost == 0) break;
            palettes[i].Merge(newPalette, out var newCost);
            if (newCost >= bestCost) continue;
            bestCost = newCost;
            bestPair = (i, palettes.Count);
         }

         return bestPair;
      }

      public static int[,] Index(short[] rawColors, IReadOnlyList<short> palette) {
         Debug.Assert(rawColors.Length == 64, "Expected to index a tile, but was not handed 64 pixels!");
         var result = new int[8, 8];
         for (int i = 0; i < rawColors.Length; i++) {
            int x = i % 8, y = i / 8;
            result[x, y] = BestMatch(rawColors[i], palette);
         }
         return result;
      }

      public static double CostToUse(short[] rawColors, IReadOnlyList<short> palette) {
         var allColors = new Dictionary<short, ColorMass>();
         for (int i = 0; i < rawColors.Length; i++) {
            var color = rawColors[i];
            if (!allColors.ContainsKey(color)) allColors[color] = new ColorMass(color, 0);
            allColors[color] = new ColorMass(color, allColors[color].Mass + 1);
         }

         double total = 0;
         foreach (var key in allColors.Keys) {
            var bestDistance = double.PositiveInfinity;
            for (int i = 0; i < palette.Count; i++) {
               var currentDistance = Distance(key, palette[i]);
               if (currentDistance < bestDistance) bestDistance = currentDistance;
               if (bestDistance == 0) break;
            }
            total += bestDistance * allColors[key].Mass;
         }

         return total;
      }

      public static int BestMatch(short input, IReadOnlyList<short> palette) {
         // TODO use best-match caching to reduce the number of distance calculations
         var bestIndex = 0;
         var bestDistance = Distance(input, palette[0]);
         for (int i = 1; i < palette.Count; i++) {
            if (bestDistance == 0) return bestIndex;
            var currentDistance = Distance(input, palette[i]);
            if (bestDistance <= currentDistance) continue;
            bestIndex = i;
            bestDistance = currentDistance;
         }
         return bestIndex;
      }

      public static double Distance(short a, short b) {
         var channel1A = a >> 10;
         var channel2A = (a >> 5) & 0x1F;
         var channel3A = a & 0x1F;

         var channel1B = b >> 10;
         var channel2B = (b >> 5) & 0x1F;
         var channel3B = b & 0x1F;

         var diff1 = channel1A - channel1B;
         var diff2 = channel2A - channel2B;
         var diff3 = channel3A - channel3B;

         return Math.Sqrt(diff1 * diff1 + diff2 * diff2 + diff3 * diff3);
      }

      public WeightedPalette Merge(WeightedPalette other, out double cost) {
         var masses = new List<ColorMass>();
         for (int i = 0; i < Palette.Count; i++) masses.Add(new ColorMass(Palette[i], Weight[i]));
         for (int i = 0; i < other.Palette.Count; i++) {
            var perfectMatchFound = false;
            for (int j = 0; j < masses.Count; j++) {
               if (masses[j].ResultColor != other.Palette[i]) continue;
               perfectMatchFound = true;
               masses[j] = new ColorMass(other.Palette[i], other.Weight[i] + masses[j].Mass);
               break;
            }
            if (!perfectMatchFound) {
               masses.Add(new ColorMass(other.Palette[i], other.Weight[i]));
            }
         }
         return Reduce(masses, TargetLength, out cost);
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
         if (a.Mass == 0) return b;
         if (b.Mass == 0) return a;
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
      /// Returns an inverse gravity factor, accounting for the distance/mass between the two color masses.
      /// Smaller numbers mean that it's a smaller change to merge the two. Larger numbers mean merging them should be avoided.
      /// </summary>
      public static double operator *(ColorMass a, ColorMass b) {
         if (a.Mass == 0 || b.Mass == 0) return 0;
         var dR = a.R - b.R;
         var dG = a.G - b.G;
         var dB = a.B - b.B;
         var distanceSquared = dR * dR + dG * dG + dB * dB;

         if (distanceSquared == 0) return 0; // no distance: merging is free

         // as the masses get farther apart, the force needed to merge them grows
         // as the masses get heavier, the force needed to merge them grows
         var force = a.Mass * b.Mass * distanceSquared;
         return force;
      }

      public static (int, int, int) SplitRGB(short color) => (color >> 10, (color >> 5) & 0x1F, color & 0x1F);

      public static short CombineRGB(int r, int g, int b) => (short)((r << 10) | (g << 5) | b);

      public override string ToString() => $"{Mass}, {R}:{G}:{B}";
   }
}
