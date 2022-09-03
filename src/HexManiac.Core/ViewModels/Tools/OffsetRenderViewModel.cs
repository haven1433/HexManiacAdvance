using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class OffsetRenderViewModel : ViewModelCore, IArrayElementViewModel, IPixelViewModel {
      private readonly IEditableViewPort viewPort;
      private ArrayRunOffsetRenderSegment segment;
      private int itemAddress;

      private bool visible = true;
      public bool Visible { get => visible; set => Set(ref visible, value); }

      public bool IsInError => false;
      public string ErrorText => string.Empty;
      public int ZIndex => 0;

      #region IPixelViewModel

      public IPixelViewModel PixelViewModel { get; private set; }

      public short Transparent => PixelViewModel?.Transparent ?? -1;
      public int PixelWidth => PixelViewModel?.PixelWidth ?? 0;
      public int PixelHeight => PixelViewModel?.PixelHeight ?? 0;
      public short[] PixelData => PixelViewModel?.PixelData;
      public double SpriteScale => PixelViewModel?.SpriteScale ?? 0;

      #endregion

      public event EventHandler DataChanged;
      public event EventHandler DataSelected;

      public OffsetRenderViewModel(IEditableViewPort viewPort, ArrayRunOffsetRenderSegment segment, int itemAddress) {
         this.viewPort = viewPort;
         var model = viewPort.Model;
         this.segment = segment;
         this.itemAddress = itemAddress;
         if (model.GetNextRun(itemAddress) is not ITableRun offsetTable) return;
         var segmentOffset = offsetTable.ConvertByteOffsetToArrayOffset(itemAddress);
         int xOffset = 0, yOffset = 0;
         if (offsetTable.ElementContent.Select(field => field.Name).Contains(segment.TargetFieldX)) {
            xOffset = offsetTable.ReadValue(model, segmentOffset.ElementIndex, segment.TargetFieldX);
         }
         yOffset = ParseContent(offsetTable, segment.TargetFieldY, itemAddress);

         // find/crop background
         if (model.GetNextRun(model.GetAddressFromAnchor(new(), -1, segment.Background)) is not ISpriteRun spriteRun) return;
         PixelViewModel = SpriteDecorator.BuildSprite(model, spriteRun);
         PixelViewModel = ReadonlyPixelViewModel.Crop(PixelViewModel, segment.BackgroundX, segment.BackgroundY, segment.BackgroundWidth, segment.BackgroundHeight); // (gba screen width, height of pokemon battle background)

         // find/render foreground
         var foregroundTable = model.GetTable(segment.Foreground);
         var imageAddress = foregroundTable?.ReadPointer(model, segmentOffset.ElementIndex) ?? Pointer.NULL;
         if (model.GetNextRun(imageAddress) is not ISpriteRun foregroundRun) return;
         var foreground = ReadonlyPixelViewModel.Create(model, foregroundRun, true);
         PixelViewModel = ReadonlyPixelViewModel.Render(PixelViewModel, foreground, segment.X + xOffset, segment.Y + yOffset);
      }

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

      public bool TryCopy(IArrayElementViewModel other) {
         if (other is not OffsetRenderViewModel that) return false;

         segment = that.segment;
         itemAddress = that.itemAddress;
         Visible = that.Visible;

         var properties = new List<string>();
         if (Transparent != that.Transparent) properties.Add(nameof(Transparent));
         if (PixelWidth != that.PixelWidth) properties.Add(nameof(PixelWidth));
         if (PixelHeight != that.PixelHeight) properties.Add(nameof(PixelHeight));
         if (PixelData != that.PixelData) properties.Add(nameof(PixelData));
         if (SpriteScale != that.SpriteScale) properties.Add(nameof(SpriteScale));
         PixelViewModel = that.PixelViewModel;
         NotifyPropertyChanged(nameof(PixelViewModel));
         properties.ForEach(NotifyPropertyChanged);

         return true;
      }

      public void ShiftDelta(int x, int y) {
         var model = viewPort.Model;
         if (model.GetNextRun(itemAddress) is not ITableRun offsetTable) return;
         var segmentOffset = offsetTable.ConvertByteOffsetToArrayOffset(itemAddress);

         if (offsetTable.ElementContent.Select(field => field.Name).Contains(segment.TargetFieldX)) {
            var offset = offsetTable.ReadValue(model, segmentOffset.ElementIndex, segment.TargetFieldX);
            offset = (offset + x).LimitToRange(0, segment.BackgroundWidth);
            offsetTable.WriteValue(offset, model, viewPort.ChangeHistory.CurrentChange, segmentOffset.ElementIndex, segment.TargetFieldX);
         }

         if (offsetTable.ElementContent.Select(field => field.Name).Contains(segment.TargetFieldY)) {
            var offset = offsetTable.ReadValue(model, segmentOffset.ElementIndex, segment.TargetFieldY);
            offset = (offset + y).LimitToRange(0, segment.BackgroundWidth);
            offsetTable.WriteValue(offset, model, viewPort.ChangeHistory.CurrentChange, segmentOffset.ElementIndex, segment.TargetFieldY);
         }

         DataChanged?.Invoke(this, EventArgs.Empty);
      }
   }
}
