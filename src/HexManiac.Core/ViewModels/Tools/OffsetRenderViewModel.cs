using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class OffsetRenderViewModel : ViewModelCore, IArrayElementViewModel, IPixelViewModel {
      public readonly IEditableViewPort viewPort;
      public ArrayRunOffsetRenderSegment segment;
      public int itemAddress;

      public bool visible = true;
      public bool Visible { get => visible; set => Set(ref visible, value); }

      public string theme; public string Theme { get => theme; set => Set(ref theme, value); }
      public bool IsInError => false;
      public string ErrorText => string.Empty;
      public int ZIndex => 0;

      #region IPixelViewModel

      public IPixelViewModel PixelViewModel { get; set; }

      public short Transparent => PixelViewModel?.Transparent ?? -1;
      public int PixelWidth => PixelViewModel?.PixelWidth ?? 0;
      public int PixelHeight => PixelViewModel?.PixelHeight ?? 0;
      public double SpriteScale => PixelViewModel?.SpriteScale ?? 0;

      #endregion

      public event EventHandler DataChanged;
      public event EventHandler DataSelected;

      public int ParseContent(ITableRun defaultTable, string text, int itemAddress) {
         var parts = text.Split("-");
         if (parts.Length == 2) {
            return ParseContent(defaultTable, parts[0], itemAddress) - ParseContent(defaultTable, parts[1], itemAddress);
         }

         var segmentOffset = defaultTable.ConvertByteOffsetToArrayOffset(itemAddress);

         parts = text.Split("/");
         if (parts.Length == 2) {
            var table = viewPort.Model.GetTable(parts[0]);
            if (table != null) {
               itemAddress = table.Start + table.ElementLength * segmentOffset.ElementIndex;
               return ParseContent(table, parts[1], itemAddress);
            }
         }

         if (defaultTable.ElementContent.Select(field => field.Name).Contains(text)) {
            return defaultTable.ReadValue(viewPort.Model, segmentOffset.ElementIndex, text);
         }

         return 0;
      }
   }
}
