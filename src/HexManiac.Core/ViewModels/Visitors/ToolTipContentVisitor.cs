using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using HexManiac.Core.Models.Runs.Sprites;

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

      public void Visit(PCS pcs, byte data) { }

      public void Visit(EscapedPCS pcs, byte data) { }

      public void Visit(ErrorPCS pcs, byte data) { }

      public void Visit(Ascii ascii, byte data) { }

      public void Visit(Braille braille, byte data) { }

      public void Visit(Integer integer, byte data) {
         if (model.GetNextRun(integer.Source) is WordRun wordRun) {
            var desiredToolTip = wordRun.SourceArrayName;
            if (wordRun.MultOffset != 1) desiredToolTip += "*" + wordRun.MultOffset;
            if (wordRun.ValueOffset > 0) desiredToolTip += "+" + wordRun.ValueOffset;
            if (wordRun.ValueOffset < 0) desiredToolTip += wordRun.ValueOffset;
            if (!string.IsNullOrEmpty(wordRun.Note)) desiredToolTip += Environment.NewLine + wordRun.Note;
            desiredToolTip += Environment.NewLine + "Changing one copy of a constant will automatically update all other copies.";
            Content.Add(desiredToolTip);
         }
      }

      public void Visit(IntegerEnum integer, byte data) {
         Content.Add(integer.Value);

         if (model.GetNextRun(integer.Source) is not ITableRun containingTable) return;
         var offset = containingTable.ConvertByteOffsetToArrayOffset(integer.Source);
         var enumValue = model.ReadMultiByteValue(offset.SegmentStart, containingTable.ElementContent[offset.SegmentIndex].Length);
         var segment = containingTable.ElementContent[offset.SegmentIndex];
         if (segment is ArrayRunRecordSegment recordSegment) segment = recordSegment.CreateConcrete(model, integer.Source);
         if (segment is not ArrayRunEnumSegment enumSeg) return;
         var parentAddress = model.GetAddressFromAnchor(new(), -1, enumSeg.EnumName);
         if (model.GetNextRun(parentAddress) is not ArrayRun parentArray) return;
         GetEnumImage(model, Content, enumValue, parentArray);
      }

      public static IPixelViewModel GetEnumImage(IDataModel model, int enumValue, ArrayRun parentArray) {
         if (parentArray == null) return null;
         foreach (var array in model.GetRelatedArrays(parentArray)) {
            int segOffset = 0;
            foreach (var seg in array.ElementContent) {
               var itemIndex = enumValue + array.ParentOffset.BeginningMargin;
               var source = array.Start + array.ElementLength * itemIndex + segOffset;
               if (source < 0 || source >= model.Count) continue;
               var destination = model.ReadPointer(array.Start + array.ElementLength * itemIndex + segOffset);
               segOffset += seg.Length;
               if (seg.Type != ElementContentType.Pointer) continue;
               if (model.GetNextRun(destination) is not ISpriteRun spriteRun) continue;
               var paletteRuns = spriteRun.FindRelatedPalettes(model, array.Start + array.ElementLength * itemIndex);
               return ReadonlyPixelViewModel.Create(model, spriteRun, paletteRuns.FirstOrDefault(), true);
            }
         }
         return null;
      }

      public static void GetEnumImage(IDataModel model, ObservableCollection<object> content, int enumValue, ArrayRun parentArray) {
         var result = GetEnumImage(model, enumValue, parentArray);
         if (result != null) content.Add(result);
      }

      public void Visit(IntegerHex integer, byte data) { }

      public void Visit(EggSection section, byte data) { }

      public void Visit(EggItem item, byte data) { }

      public void Visit(PlmItem item, byte data) => Content.Add(item.ToString());

      public void Visit(BitArray array, byte data) {
         using (ModelCacheScope.CreateScope(model)) {
            var table = (ITableRun)model.GetNextRun(array.Source);
            var offset = table.ConvertByteOffsetToArrayOffset(array.Source);
            var segment = table.ElementContent[offset.SegmentIndex] as ArrayRunBitArraySegment;
            var options = segment?.GetOptions(model)?.ToList();
            if (options == null) return;

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

      public void Visit(LzMagicIdentifier lz, byte data) {
         Content.Add("This byte marks the start of an LZ compressed data section.");
         Content.Add("After the identifier byte and the length, compressed data consists of 3 types of tokens:");
         Content.Add("(1) a 1 byte section header, telling you which of the next 8 tokens are compressed.");
         Content.Add("(2) A raw uncompressed byte.");
         Content.Add("(3) A 2-byte token representing anywhere from 3 to 18 compressed bytes.");
      }

      public void Visit(LzGroupHeader lz, byte data) { }

      public void Visit(LzCompressed lz, byte data) { }

      public void Visit(LzUncompressed lz, byte data) { }

      public void Visit(UncompressedPaletteColor color, byte data) { }

      public void Visit(DataFormats.Tuple tuple, byte data) => Content.Add(tuple.ToString());

      public static string EllipsedLines(string[] lines) {
         const int MaxLength = 70;

         for(int i = 0; i < lines.Length; i++) {
            if (lines[i].Length > MaxLength) lines[i] = lines[i].Substring(0, MaxLength - 3) + "...";
         }

         return Environment.NewLine.Join(lines);
      }
   }
}
