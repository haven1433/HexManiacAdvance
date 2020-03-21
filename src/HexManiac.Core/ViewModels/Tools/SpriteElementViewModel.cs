using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class SpriteElementViewModel : PagedElementViewModel, IPagedViewModel {
      private SpriteFormat format;
      private byte[] data;
      private string paletteHint;

      public int PixelWidth => format.TileWidth * 8;
      public int PixelHeight => format.TileHeight * 8;

      public ObservableCollection<TileViewModel> Tiles { get; } = new ObservableCollection<TileViewModel>();

      public SpriteElementViewModel(ViewPort viewPort, SpriteFormat format, int itemAddress) : base(viewPort, itemAddress) {
         this.format = format;
         DecodeData();
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
         data = that.data;

         NotifyPropertyChanged(nameof(PixelWidth));
         NotifyPropertyChanged(nameof(PixelHeight));
         UpdateTiles(that.CurrentPage, paletteHint);
         return true;
      }

      protected override void PageChanged() => UpdateTiles(CurrentPage, paletteHint);

      private void DecodeData() {
         var destination = ViewPort.Model.ReadPointer(Start);
         data = LZRun.Decompress(ViewPort.Model, destination);
         if (format.BitsPerPixel == 4) FixPixelByteOrder(data);
         Debug.Assert(data.Length % format.ExpectedByteLength == 0);
         Pages = data.Length / format.ExpectedByteLength;
      }

      public void UpdateTiles(int? pageOption = null, string hint = null) {
         // TODO support multiple layers
         paletteHint = hint;
         int page = pageOption ?? CurrentPage;
         var tileSize = 8 * format.BitsPerPixel;
         Tiles.Clear();
         var palette = GetDesiredPalette(paletteHint);
         for (int y = 0; y < format.TileHeight; y++) {
            for (int x = 0; x < format.TileWidth; x++) {
               var tileIndex = page;
               tileIndex = tileIndex * format.TileHeight + y;
               tileIndex = tileIndex * format.TileWidth + x;
               Tiles.Add(new TileViewModel(data, tileIndex * tileSize, tileSize, palette));
            }
         }
      }

      /// <summary>
      /// If the hint is a table name, only match palettes from that table.
      /// </summary>
      private IReadOnlyList<short> GetDesiredPalette(string hint) {
         // fast version
         foreach (var viewModel in ViewPort.Tools.TableTool.Children) {
            if (!(viewModel is PaletteElementViewModel pevm)) continue;
            if (!string.IsNullOrEmpty(hint) && pevm.TableName != hint) continue;
            return pevm.Colors;
         }

         // slow version, if no palettes are loaded yet
         var myRun = ViewPort.Model.GetNextRun(Start) as ArrayRun;
         if (myRun == null) return null;
         var offset = myRun.ConvertByteOffsetToArrayOffset(Start);
         foreach (var array in ViewPort.Model.GetRelatedArrays(myRun)) {
            if (!string.IsNullOrEmpty(hint) && ViewPort.Model.GetAnchorFromAddress(-1, array.Start) != hint) {
               continue;
            }

            int segmentOffset = 0;
            foreach (var segment in array.ElementContent) {
               if (segment is ArrayRunPointerSegment pointerSegment) {
                  if (PaletteRun.TryParsePaletteFormat(pointerSegment.InnerFormat, out var paletteFormat)) {
                     var source = array.Start + array.ElementLength * offset.ElementIndex + segmentOffset;
                     var destination = Model.ReadPointer(source);
                     var paletteRun = Model.GetNextRun(destination) as PaletteRun;
                     if (paletteRun != null) {
                        if (LZRun.TryDecompress(Model, destination, out var data)) {
                           return Enumerable.Range(0, data.Length / 2)
                              .Select(i => (short)data.ReadMultiByteValue(i * 2, 2)).ToList();
                        }
                     }
                  }
               }
               segmentOffset += segment.Length;
            }
         }

         return null;
      }

      // the gba expects the high bits to be the first pixel. WPF expects the low bits to be the first pixel.
      private static void FixPixelByteOrder(byte[] data) {
         for (int i = 0; i < data.Length; i++) {
            data[i] = (byte)((data[i] >> 4) | (data[i] << 4));
         }
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
         for (int i = 0; i < desiredCount; i++) {
            var shade = 0b11111 * i / (desiredCount - 1);
            var color = (shade << 10) | (shade << 5) | shade;
            defaultPalette.Add((short)color);
         }
         Palette = defaultPalette;
      }

      // TODO include horizontal/vertical flip information
   }
}
