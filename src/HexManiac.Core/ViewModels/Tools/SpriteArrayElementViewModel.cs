using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public interface IPagedViewModel : IStreamArrayElementViewModel {
      bool HasMultiplePages { get; }
      int Pages { get; }
      int CurrentPage { get; set; }
      ICommand PreviousPage { get; }
      ICommand NextPage { get; }
   }

   public class SpriteArrayElementViewModel : ViewModelCore, IPagedViewModel {
      private readonly ViewPort viewPort;
      private SpriteFormat format;
      private byte[] data;

      public event EventHandler<(int originalStart, int newStart)> DataMoved;
      public event EventHandler DataChanged;

      public string Name { get; private set; }
      public int Start { get; private set; } // a pointer to the sprite's compressed data

      public bool IsInError => !string.IsNullOrEmpty(ErrorText);

      public int PixelWidth => format.TileWidth * 8;
      public int PixelHeight => format.TileHeight * 8;

      #region Pages

      public bool HasMultiplePages => Pages > 1;

      private int currentPage;
      public int CurrentPage {
         get => currentPage;
         set {
            if (!TryUpdate(ref currentPage, value)) return;
            UpdateTiles();
         }
      }

      public int Pages { get; private set; }

      private readonly StubCommand previousPage = new StubCommand();
      public ICommand PreviousPage => previousPage;

      private readonly StubCommand nextPage = new StubCommand();
      public ICommand NextPage => nextPage;

      public void UpdateOtherPagedViewModels() {
         foreach (var child in viewPort.Tools.TableTool.Children) {
            if (!(child is IPagedViewModel pvm)) continue;
            pvm.CurrentPage = pvm.Pages > CurrentPage ? CurrentPage : 0;
         }
      }

      #endregion

      public ObservableCollection<TileViewModel> Tiles { get; } = new ObservableCollection<TileViewModel>();

      private string errorText;
      public string ErrorText {
         get => errorText;
         private set {
            if (TryUpdate(ref errorText, value)) NotifyPropertyChanged(nameof(IsInError));
         }
      }

      public SpriteArrayElementViewModel(ViewPort viewPort, SpriteFormat format, string name, int itemAddress) {
         this.viewPort = viewPort;
         this.format = format;
         Name = name;
         Start = itemAddress;
         DecodeData();
         UpdateTiles();

         previousPage.CanExecute = arg => currentPage > 0;
         previousPage.Execute = arg => { CurrentPage -= 1; UpdateOtherPagedViewModels(); };
         nextPage.CanExecute = arg => currentPage < Pages - 1;
         nextPage.Execute = arg => { CurrentPage += 1; UpdateOtherPagedViewModels(); };
      }

      public bool TryCopy(IArrayElementViewModel other) {
         if (!(other is SpriteArrayElementViewModel that)) return false;
         Name = that.Name;
         Start = that.Start;
         format = that.format;
         ErrorText = that.ErrorText;
         data = that.data;
         currentPage = that.currentPage;
         Pages = that.Pages;
         NotifyPropertyChanged(nameof(Name));
         NotifyPropertyChanged(nameof(Start));
         NotifyPropertyChanged(nameof(ErrorText));
         NotifyPropertyChanged(nameof(PixelWidth));
         NotifyPropertyChanged(nameof(PixelHeight));
         NotifyPropertyChanged(nameof(Pages));
         NotifyPropertyChanged(nameof(HasMultiplePages));
         UpdateTiles();
         return true;
      }

      public void UpdateTiles(string hint = null) {
         // TODO support multiple layers
         var tileSize = 8 * format.BitsPerPixel;
         Tiles.Clear();
         var palette = GetDesiredPalette(hint);
         for (int y = 0; y < format.TileHeight; y++) {
            for (int x = 0; x < format.TileWidth; x++) {
               var tileIndex = CurrentPage;
               tileIndex = tileIndex * format.TileHeight + y;
               tileIndex = tileIndex * format.TileWidth + x;
               Tiles.Add(new TileViewModel(data, tileIndex * tileSize, tileSize, palette));
            }
         }

         previousPage.CanExecuteChanged.Invoke(previousPage, EventArgs.Empty);
         nextPage.CanExecuteChanged.Invoke(nextPage, EventArgs.Empty);
      }

      private void DecodeData() {
         var destination = viewPort.Model.ReadPointer(Start);
         data = LZRun.Decompress(viewPort.Model, destination);
         if (format.BitsPerPixel == 4) FixPixelByteOrder(data);
         Debug.Assert(data.Length % format.ExpectedByteLength == 0);
         Pages = data.Length / format.ExpectedByteLength;
         if (currentPage >= Pages) CurrentPage = 0;
      }

      /// <summary>
      /// If the hint is a table name, only match palettes from that table.
      /// </summary>
      private IReadOnlyList<short> GetDesiredPalette(string hint) {
         // fast version
         foreach (var viewModel in viewPort.Tools.TableTool.Children) {
            if (!(viewModel is PaletteArrayElementViewModel paevm)) continue;
            if (!string.IsNullOrEmpty(hint) && paevm.QualifiedName != hint) continue;
            return paevm.Colors;
         }

         // slow version, if no palettes are loaded yet
         var myRun = viewPort.Model.GetNextRun(Start) as ArrayRun;
         if (myRun == null) return null;
         var offset = myRun.ConvertByteOffsetToArrayOffset(Start);
         foreach (var array in viewPort.Model.GetRelatedArrays(myRun)) {
            if (!string.IsNullOrEmpty(hint) && viewPort.Model.GetAnchorFromAddress(-1, array.Start) != hint) {
               continue;
            }

            int segmentOffset = 0;
            foreach (var segment in array.ElementContent) {
               if (segment is ArrayRunPointerSegment pointerSegment) {
                  if (PaletteRun.TryParsePaletteFormat(pointerSegment.InnerFormat, out var paletteFormat)) {
                     var source = array.Start + array.ElementLength * offset.ElementIndex + segmentOffset;
                     var destination = viewPort.Model.ReadPointer(source);
                     var paletteRun = viewPort.Model.GetNextRun(destination) as PaletteRun;
                     if (paletteRun != null) {
                        if (LZRun.TryDecompress(viewPort.Model, destination, out var data)) {
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
