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
   }

   // TODO use the hint on the format (if there is one) to find a matching palette
   public class SpriteTool : ViewModelCore, IToolViewModel, IPixelViewModel {
      private readonly ViewPort viewPort;
      private readonly IDataModel model;

      private int spritePages = 1, palPages = 1, spritePage = 0, palPage = 0;
      private int[,] pixels;
      private short[] palette;

      public string Name => "Image";

      private readonly StubCommand
         prevSpritePage = new StubCommand(),
         nextSpritePage = new StubCommand(),
         prevPalPage = new StubCommand(),
         nextPalPage = new StubCommand();

      private int spriteAddress;
      public int SpriteAddress {
         get => spriteAddress;
         set {
            if (!TryUpdate(ref spriteAddress, value)) return;
            var run = model.GetNextRun(value) as ISpriteRun;
            if (run == null) {
               spritePages = 1;
               spritePage = 0;
            } else {
               spritePages = run.Pages;
               if (spritePage >= spritePages) spritePage = 0;
               NotifyPropertyChanged(nameof(HasMultipleSpritePages));
            }
            LoadSprite();
            FindMatchingPalette(run);
         }
      }

      private int paletteAddress;
      public int PaletteAddress {
         get => paletteAddress;
         set {
            if (!TryUpdate(ref paletteAddress, value)) return;
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

      public bool IsReadOnly => throw new NotImplementedException();

      public SpriteTool(ViewPort viewPort) {
         this.viewPort = viewPort;
         model = viewPort?.Model;
         spriteAddress = Pointer.NULL;
         paletteAddress = Pointer.NULL;

         prevSpritePage.CanExecute = arg => spritePage > 0;
         nextSpritePage.CanExecute = arg => spritePage < spritePages - 1;
         prevPalPage.CanExecute = arg => palPage > 0;
         nextPalPage.CanExecute = arg => palPage < palPages - 1;

         prevSpritePage.Execute = arg => { spritePage -= 1; LoadSprite(); };
         nextSpritePage.Execute = arg => { spritePage += 1; LoadSprite(); };
         prevPalPage.Execute = arg => { palPage -= 1; LoadPalette(); };
         nextPalPage.Execute = arg => { palPage += 1; LoadPalette(); };

         LoadPalette();
      }

      public static short[] Render(int[,] pixels, IReadOnlyList<short> palette) {
         if (pixels == null) return new short[0];
         if (palette == null) palette = TileViewModel.CreateDefaultPalette(16); // TODO be able to create default palette for 256 colors
         var data = new short[pixels.Length];
         var width = pixels.GetLength(0);
         for (int i = 0; i < data.Length; i++) {
            var pixel = pixels[i % width, i / width];
            data[i] = palette[pixel];
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
         PixelData = Render(pixels, palette);
         NotifyPropertyChanged(nameof(PixelWidth));
         NotifyPropertyChanged(nameof(PixelHeight));
         prevSpritePage.CanExecuteChanged.Invoke(prevSpritePage, EventArgs.Empty);
         nextSpritePage.CanExecuteChanged.Invoke(nextSpritePage, EventArgs.Empty);
         NotifyPropertyChanged(PixelData);
      }

      private void LoadPalette() {
         var run = model?.GetNextRun(paletteAddress) as IPaletteRun;
         if (run == null) {
            palette = TileViewModel.CreateDefaultPalette(0x10);
         } else {
            palette = run.GetPalette(model, palPage).ToArray();
         }

         PaletteWidth = (int)Math.Sqrt(palette.Length);
         PaletteHeight = (int)(Math.Ceiling((double)palette.Length / PaletteWidth));
         NotifyPropertyChanged(nameof(PaletteWidth));
         NotifyPropertyChanged(nameof(PaletteHeight));
         prevPalPage.CanExecuteChanged.Invoke(prevPalPage, EventArgs.Empty);
         nextPalPage.CanExecuteChanged.Invoke(nextPalPage, EventArgs.Empty);

         Palette.Clear();
         foreach (var color in palette) Palette.Add(color);
         PixelData = Render(pixels, palette);
         NotifyPropertyChanged(PixelData);
      }

      private void FindMatchingPalette(ISpriteRun spriteRun) {
         var hint = spriteRun?.SpriteFormat.PaletteHint;
         if (hint == null) return;
         var hintRun = model.GetNextRun(model.GetAddressFromAnchor(viewPort.CurrentChange, -1, hint));

         // easy case: the hint is the address of a palette
         if (hintRun is IPaletteRun) {
            PaletteAddress = hintRun.Start;
            return;
         }

         // harder case: the hint is a table
         if (!(hintRun is ITableRun hintTableRun)) return;
         if ((spriteRun.PointerSources?.Count ?? 0) == 0) return;
         var spritePointer = spriteRun.PointerSources[0];
         var spriteTable = model.GetNextRun(spritePointer) as ITableRun;
         if (spriteTable == null) return;
         int spriteIndex = (spritePointer - spriteTable.Start) / spriteTable.ElementLength;

         // easy case: hint table is pointers to palettes
         var hintTableElementStart = hintTableRun.Start + hintTableRun.ElementLength * spriteIndex;
         if (hintTableRun.ElementContent[0].Type == ElementContentType.Pointer) {
            var paletteAddress = model.ReadPointer(hintTableElementStart);
            if (!(model.GetNextRun(paletteAddress) is IPaletteRun)) return;
            PaletteAddress = paletteAddress;
            return;
         }

         // harder case: hint table is index into a different table
         var segment = hintTableRun.ElementContent[0] as ArrayRunEnumSegment;
         if (segment == null) return;
         var paletteTableAddress = model.GetAddressFromAnchor(viewPort.CurrentChange, -1, segment.EnumName);
         var paletteTableRun = model.GetNextRun(paletteTableAddress) as ITableRun;
         if (paletteTableRun == null) return;
         if (paletteTableRun.ElementContent[0].Type != ElementContentType.Pointer) return;
         var index = model.ReadMultiByteValue(hintTableElementStart, segment.Length);
         if (paletteTableRun.ElementCount <= index) return;
         var paletteTableElementStart = paletteTableRun.Start + paletteTableRun.ElementLength * index;
         var indexedPaletteAddress = model.ReadPointer(paletteTableElementStart);
         if (!(model.GetNextRun(indexedPaletteAddress) is IPaletteRun)) return;
         PaletteAddress = indexedPaletteAddress;
      }
   }
}
