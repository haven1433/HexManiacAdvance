using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Compressed;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using System;
using System.Collections.Generic;

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
         UpdateTiles(that.Start, CurrentPage, true);
         return true;
      }

      protected override void PageChanged() => UpdateTiles(CurrentPage, paletteHint);

      public void UpdateTiles(int? pageOption = null, string hint = null) {
         // TODO support multiple layers
         paletteHint = hint ?? paletteHint;
         int page = pageOption ?? CurrentPage;
         UpdateTiles(Start, page, false);
      }

      private int[,] lastPixels;
      private IReadOnlyList<short> lastColors;
      private void UpdateTiles(int start, int page, bool exitPaletteSearchEarly) {
         var destination = ViewPort.Model.ReadPointer(start);
         var run = ViewPort.Model.GetNextRun(destination) as ISpriteRun;
         var pixels = run.GetPixels(ViewPort.Model, page);
         var palette = GetDesiredPalette(start, paletteHint, page, exitPaletteSearchEarly);
         if (pixels == lastPixels && palette == lastColors) return;
         lastPixels = pixels;
         lastColors = palette;
         PixelData = SpriteTool.Render(pixels, palette);
         NotifyPropertyChanged(nameof(PixelWidth));
         NotifyPropertyChanged(nameof(PixelHeight));
         NotifyPropertyChanged(nameof(PixelData));
      }

      /// <summary>
      /// If the hint is a table name, only match palettes from that table.
      /// </summary>
      private IReadOnlyList<short> GetDesiredPalette(int start, string hint, int page, bool exitEarly) {
         hint = format.PaletteHint ?? hint; // if there's a paletteHint, that takes precendence

         // search for hint matches in other comboboxes in the viewmodel
         foreach (var viewModel in ViewPort.Tools.TableTool.Children) {
            if (viewModel is ComboBoxArrayElementViewModel comboBox) {
               if (comboBox.TableName != hint) continue;
               if (TryGetPaletteFromComboBoxInMatchingTable(start, comboBox, page, out var colors)) return colors;
            }
         }

         // search for hint matches in other palettes in the viewmodel
         IReadOnlyList<short> first = null;
         foreach (var viewModel in ViewPort.Tools.TableTool.Children) {
            if (viewModel == this && exitEarly) break;
            if (!(viewModel is PaletteElementViewModel pevm)) continue;
            first = first ?? pevm.Colors;
            if (string.IsNullOrEmpty(hint)) break;
            if (pevm.TableName != hint) continue;
            return pevm.Colors;
         }

         return first;
      }

      /// <summary>
      /// If the hint-name matches the name of a table of a combobox also loaded as a parallel table,
      /// then there isn't a 1-to-1 mapping between sprites and palettes.
      /// Instead, each sprite has an index (from the combobox) that matches a palette.
      /// In this case, the combobox will know which list of palettes it's pulling from.
      /// </summary>
      private bool TryGetPaletteFromComboBoxInMatchingTable(int start, ComboBoxArrayElementViewModel comboBox, int page, out IReadOnlyList<short> colors) {
         colors = null;

         // figure out the current index into the table
         var myRun = (ITableRun)Model.GetNextRun(start);
         var arrayIndex = (start - myRun.Start) / myRun.ElementLength;

         // figure out the name of the palette table
         var indexRun = (ITableRun)Model.GetNextRun(comboBox.Start);
         var offsets = indexRun.ConvertByteOffsetToArrayOffset(comboBox.Start);
         if (offsets.SegmentOffset != 0) return false; // for now, require that 
         var segment = indexRun.ElementContent[offsets.SegmentIndex] as ArrayRunEnumSegment;
         if (segment == null) return false;
         var paletteTable = segment.EnumName;

         // figure out which element into the palette run from the value in the index run
         var paletteIndex = Model.ReadMultiByteValue(indexRun.Start + indexRun.ElementLength * arrayIndex, segment.Length);

         // find the pointer to our palette
         var array = Model.GetNextRun(Model.GetAddressFromAnchor(ViewPort.CurrentChange, -1, paletteTable)) as ArrayRun;
         if (array == null) return false;
         if (array.ElementContent[0].Type != ElementContentType.Pointer) return false;
         var pointer = array.Start + array.ElementLength * paletteIndex;

         // go get the palette that we want
         var paletteAddress = Model.ReadPointer(pointer);
         var palRun = Model.GetNextRun(paletteAddress) as IPaletteRun;
         if (palRun == null) return false;

         // return the values from the palette
         colors = palRun.GetPalette(Model, page);
         return true;
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
