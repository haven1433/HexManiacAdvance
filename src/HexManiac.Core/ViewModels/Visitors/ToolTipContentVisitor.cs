using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace HavenSoft.HexManiac.Core.ViewModels.Visitors {
   public class ToolTipContentVisitor : IDataFormatVisitor {
      private readonly IDataModel model;

      public ToolTipContentVisitor(IDataModel model) {
         this.model = model;
      }

      public ObservableCollection<object> Content { get; } = new ObservableCollection<object>();

      public void Visit(Undefined dataFormat, byte data) { }

      public void Visit(None dataFormat, byte data) { }

      public void Visit(UnderEdit dataFormat, byte data) { }

      public void Visit(Pointer pointer, byte data) {
         Content.Add(pointer.DestinationAsText);
         var destinationRun = model.GetNextRun(pointer.Destination);
         if (destinationRun is PCSRun pcs) {
            Content.Add(PCSString.Convert(model, pcs.Start, pcs.Length));
         } else if (destinationRun is ISpriteRun sprite) {
            var paletteRun = sprite.FindRelatedPalettes(model).FirstOrDefault();
            var pixels = sprite.GetPixels(model, 0);
            var colors = paletteRun.AllColors(model);
            var imageData = SpriteTool.Render(pixels, colors, paletteRun.PaletteFormat.InitialBlankPages, 0);
            Content.Add(new ReadonlyPixelViewModel(sprite.SpriteFormat, imageData));
         } else if (destinationRun is IPaletteRun paletteRun) {
            var colors = paletteRun.GetPalette(model, 0);
            Content.Add(new ReadonlyPaletteCollection(colors));
         }
      }

      public void Visit(Anchor anchor, byte data) { }

      public void Visit(PCS pcs, byte data) { }

      public void Visit(EscapedPCS pcs, byte data) { }

      public void Visit(ErrorPCS pcs, byte data) { }

      public void Visit(Ascii ascii, byte data) { }

      public void Visit(Integer integer, byte data) {
         if (model.GetNextRun(integer.Source) is WordRun wordRun) {
            var desiredToolTip = wordRun.SourceArrayName;
            if (wordRun.ValueOffset > 0) desiredToolTip += "+" + wordRun.ValueOffset;
            if (wordRun.ValueOffset < 0) desiredToolTip += wordRun.ValueOffset;
            if (!string.IsNullOrEmpty(wordRun.Note)) desiredToolTip += Environment.NewLine + wordRun.Note;
            Content.Add(desiredToolTip);
         }
      }

      public void Visit(IntegerEnum integer, byte data) => Content.Add(integer.DisplayValue);

      public void Visit(IntegerHex integer, byte data) { }

      public void Visit(EggSection section, byte data) { }

      public void Visit(EggItem item, byte data) { }

      public void Visit(PlmItem item, byte data) { }

      public void Visit(BitArray array, byte data) {
         using (ModelCacheScope.CreateScope(model)) {
            var table = (ITableRun)model.GetNextRun(array.Source);
            var offset = table.ConvertByteOffsetToArrayOffset(array.Source);
            var segment = (ArrayRunBitArraySegment)table.ElementContent[offset.SegmentIndex];
            var options = segment.GetOptions(model).ToList();

            for (int i = 0; i < array.Length; i++) {
               var group = i * 8;
               for (int j = 0; j < 8 && group + j < options.Count; j++) {
                  var bit = ((model[array.Source + i] >> j) & 1);
                  if (bit != 0) Content.Add(options[group + j]);
               }
            }

            if (Content.Count == 0) Content.Add("- None -");
         }
      }

      public void Visit(MatchedWord word, byte data) => Content.Add(word.Name);

      public void Visit(EndStream stream, byte data) { }

      public void Visit(LzMagicIdentifier lz, byte data) { }

      public void Visit(LzGroupHeader lz, byte data) { }

      public void Visit(LzCompressed lz, byte data) { }

      public void Visit(LzUncompressed lz, byte data) { }

      public void Visit(UncompressedPaletteColor color, byte data) { }
   }
}
