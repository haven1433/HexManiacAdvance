using HavenSoft.HexManiac.Core.Models.Runs.Compressed;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using System;
using System.Collections.Generic;

// if a sprite-element is first in the list, it'll render wrong (using the previously loaded palette, because of the order the viewmodels are loaded)
// but when the palette comes along, it'll make it render right.
// if a palette-element is first in the list, it'll look for sprites to update, and update the old ones.
// but when the sprite comes along, it'll render itself right based on the previously added palette.

// this is not super performant, but it allows whoever is loaded last to fix everything.
// the other way to make this more efficient would be to make images/palettes not try to update based on things below them in the list.
// but that's a performance improvement for later.

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class SpriteElementViewModel : PagedElementViewModel, IPagedViewModel, IPixelViewModel {
      private SpriteFormat format;
      private string paletteHint;

      public short[] PixelData { get; private set; }
      public int PixelWidth => format.TileWidth * 8;
      public int PixelHeight => format.TileHeight * 8;

      public SpriteElementViewModel(ViewPort viewPort, SpriteFormat format, int itemAddress) : base(viewPort, itemAddress) {
         this.format = format;
         var destination = ViewPort.Model.ReadPointer(Start);
         var run = ViewPort.Model.GetNextRun(destination) as ISpriteRun;
         Pages = run.Pages;
         UpdateTiles();
      }

      /// <summary>
      /// Note that this method runs _before_ changes are copied from the baseclass
      /// So if we want to update tiles based on the new start point,
      /// Then UpdateColors can't rely on our internal start point
      /// </summary>
      protected override bool TryCopy(PagedElementViewModel other) {
         if (!(other is SpriteElementViewModel that)) return false;
         format = that.format;
         UpdateTiles(that.Start, CurrentPage);
         return true;
      }

      protected override void PageChanged() => UpdateTiles(CurrentPage, paletteHint);

      public void UpdateTiles(int? pageOption = null, string hint = null) {
         // TODO support multiple layers
         paletteHint = hint;
         int page = pageOption ?? CurrentPage;
         UpdateTiles(Start, page);
      }

      private void UpdateTiles(int start, int page) {
         var destination = ViewPort.Model.ReadPointer(start);
         var run = ViewPort.Model.GetNextRun(destination) as ISpriteRun;
         var pixels = run.GetPixels(ViewPort.Model, page);
         var palette = GetDesiredPalette(start, paletteHint, page);
         PixelData = SpriteTool.Render(pixels, palette);
         NotifyPropertyChanged(nameof(PixelWidth));
         NotifyPropertyChanged(nameof(PixelHeight));
         NotifyPropertyChanged(nameof(PixelData));
      }

      /// <summary>
      /// If the hint is a table name, only match palettes from that table.
      /// </summary>
      private IReadOnlyList<short> GetDesiredPalette(int start, string hint, int page) {
         IReadOnlyList<short> first = null;
         // fast version
         foreach (var viewModel in ViewPort.Tools.TableTool.Children) {
            if (!(viewModel is PaletteElementViewModel pevm)) continue;
            first = first ?? pevm.Colors;
            if (!string.IsNullOrEmpty(hint)) break;
            if (pevm.TableName != hint) continue;
            return pevm.Colors;
         }

         return first;
      }
   }

   public class TileViewModel : ViewModelCore {
      public byte[] DataStore { get; }
      public int Start { get; }

      /// <summary>
      /// Encoded as 5,6,5 bits for r,g,b
      /// </summary>
      public IReadOnlyList<short> Palette { get; private set; }

      public TileViewModel(byte[] data, int start, int byteLength, IReadOnlyList<short> palette) {
         DataStore = data;
         Start = start;
         Palette = palette;

         if (palette != null) return;
         
         var defaultPalette = new List<short>();
         int desiredCount = (int)Math.Pow(2, byteLength / 8);
         Palette = CreateDefaultPalette(desiredCount);
      }

      // TODO include horizontal/vertical flip information

      public static short[] CreateDefaultPalette(int desiredCount) {
         var palette = new short[desiredCount];
         for (int i = 0; i < desiredCount; i++) {
            var shade = 0b11111 * i / (desiredCount - 1);
            var color = (shade << 10) | (shade << 5) | shade;
            palette[i] = (short)color;
         }
         return palette;
      }
   }
}
