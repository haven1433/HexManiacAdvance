using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System.Collections.ObjectModel;

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

      public void Visit(Pointer pointer, byte data) => Content.Add(pointer.DestinationAsText);

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
            Content.Add(desiredToolTip);
         }
      }

      public void Visit(IntegerEnum integer, byte data) => Content.Add(integer.DisplayValue);

      public void Visit(IntegerHex integer, byte data) { }

      public void Visit(EggSection section, byte data) { }

      public void Visit(EggItem item, byte data) { }

      public void Visit(PlmItem item, byte data) { }

      public void Visit(BitArray array, byte data) { }

      public void Visit(MatchedWord word, byte data) => Content.Add(word.Name);

      public void Visit(EndStream stream, byte data) { }

      public void Visit(LzMagicIdentifier lz, byte data) { }

      public void Visit(LzGroupHeader lz, byte data) { }

      public void Visit(LzCompressed lz, byte data) { }

      public void Visit(LzUncompressed lz, byte data) { }

      public void Visit(UncompressedPaletteColor color, byte data) { }
   }
}
