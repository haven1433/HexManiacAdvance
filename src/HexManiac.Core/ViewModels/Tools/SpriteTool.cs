using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public interface IPixelViewModel : INotifyPropertyChanged {
      int PixelWidth { get; }
      int PixelHeight { get; }
      short[] PixelData { get; }
      double SpriteScale { get; }
   }

   // TODO use the hint on the format (if there is one) to find a matching palette
   public class SpriteTool : ViewModelCore, IToolViewModel, IPixelViewModel {
      public const int MaxSpriteWidth = 265; // From UI
      private readonly ViewPort viewPort;
      private readonly IDataModel model;

      private bool paletteWasSetMoreRecently;

      private double spriteScale;
      private int spritePages = 1, palPages = 1, spritePage = 0, palPage = 0;
      private int[,] pixels;
      private short[] palette;
      private PaletteFormat paletteFormat;

      public string Name => "Image";

      public double SpriteScale { get => spriteScale; set => TryUpdate(ref spriteScale, value); }

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

            if (run == null) {
               spritePages = 1;
               spritePage = 0;
            } else {
               spritePages = run.Pages;
               if (spritePage >= spritePages) spritePage = 0;
               NotifyPropertyChanged(nameof(HasMultipleSpritePages));
            }
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

      public event EventHandler<string> OnMessage;

      public int PixelWidth { get; private set; }
      public int PixelHeight { get; private set; }
      public int PaletteWidth { get; private set; }
      public int PaletteHeight { get; private set; }

      public short[] PixelData { get; private set; }

      // TODO propogate changes back to the paletteAddress in the model
      public ObservableCollection<short> Palette { get; private set; } = new ObservableCollection<short>();

      public bool IsReadOnly => true;

      public SpriteTool(ViewPort viewPort) {
         this.viewPort = viewPort;
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

      public static short[] Render(int[,] pixels, IReadOnlyList<short> palette, PaletteFormat format) {
         if (pixels == null) return new short[0];
         if (palette == null) palette = TileViewModel.CreateDefaultPalette(16);
         var data = new short[pixels.Length];
         var width = pixels.GetLength(0);
         var pixelOffset = format.InitialBlankPages << 4;
         for (int i = 0; i < data.Length; i++) {
            var pixel = pixels[i % width, i / width];
            while (pixel < pixelOffset) pixel += pixelOffset;
            var pixelIntoPalette = Math.Max(0, pixel - pixelOffset);
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
         PixelData = Render(pixels, renderPalette, paletteFormat);
         NotifyPropertyChanged(nameof(PixelWidth));
         NotifyPropertyChanged(nameof(PixelHeight));
         prevSpritePage.CanExecuteChanged.Invoke(prevSpritePage, EventArgs.Empty);
         nextSpritePage.CanExecuteChanged.Invoke(nextSpritePage, EventArgs.Empty);
         NotifyPropertyChanged(PixelData);

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

         Palette.Clear();
         foreach (var color in palette) Palette.Add(color);
         PixelData = Render(pixels, GetRenderPalette(model?.GetNextRun(spriteAddress) as ISpriteRun), paletteFormat);
         NotifyPropertyChanged(PixelData);
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
         var segment = hintTableRun.ElementContent[0] as ArrayRunEnumSegment;
         if (segment == null) return defaultAddress;
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
         paletteHint = paletteHint ?? palette;
         var newPalette = image.Distinct().ToList();
         if (newPalette.Count > palette.Length) {
            viewPort.RaiseError("The loaded image uses too many colors!");
            return;
         }

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
            while (newPalette.Count < palette.Length) newPalette.Add(paletteHint.Except(newPalette).FirstOrDefault());
         }
         var palIndex = new Dictionary<short, int>();
         for (int i = 0; i < newPalette.Count; i++) palIndex[newPalette[i]] = i;

         // extract pixels
         int[,] newPixels = new int[width, height];
         for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
               var pixel = image[y * width + x];
               newPixels[x, y] = palIndex[pixel];
            }
         }

         spriteRun.SetPixels(model, viewPort.CurrentChange, spritePage, newPixels);
         palRun.SetPalette(model, viewPort.CurrentChange, palPage, newPalette);
         viewPort.Refresh();
         LoadPalette();
         LoadSprite();
      }

      private void ExportSpriteAndPalette(IFileSystem fileSystem) {
         fileSystem.SaveImage(PixelData, palette, PixelWidth);
      }
   }
}
