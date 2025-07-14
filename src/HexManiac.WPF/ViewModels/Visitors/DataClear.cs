using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Security.Principal;

namespace HavenSoft.HexManiac.Core.ViewModels.Visitors {
   /// <summary>
   /// How we clear data depends on what type of data we're clearing.
   /// For example, cleared pointers get replaced with NULL (0x00000000).
   /// For example, cleared data with no known format gets 0xFF.
   /// </summary>
   public class DataClear : IDataFormatVisitor {
      private readonly IDataModel buffer;
      private readonly ModelDelta currentChange;
      private readonly int index;

      public DataClear(IDataModel data, ModelDelta delta, int index) {
         buffer = data;
         currentChange = delta;
         this.index = index;
      }

      public void Visit(Undefined dataFormat, byte data) { }

      public void Visit(None dataFormat, byte data) => currentChange.ChangeData(buffer, index, 0xFF);

      public void Visit(UnderEdit dataFormat, byte data) => throw new NotImplementedException();

      public void Visit(Pointer pointer, byte data) {
         int start = index - pointer.Position;
         buffer.WriteValue(currentChange, start, 0);
      }

      public void Visit(Anchor anchor, byte data) => anchor.OriginalFormat.Visit(this, data);

      public void Visit(SpriteDecorator sprite, byte data) => sprite.OriginalFormat.Visit(this, data);

      public void Visit(StreamEndDecorator decorator, byte data) => decorator.OriginalFormat.Visit(this, data);

      public void Visit(PCS pcs, byte data) => currentChange.ChangeData(buffer, index, 0xFF);

      public void Visit(EscapedPCS pcs, byte data) => currentChange.ChangeData(buffer, index, 0xFF);

      public void Visit(ErrorPCS pcs, byte data) => currentChange.ChangeData(buffer, index, 0xFF);

      public void Visit(Ascii ascii, byte data) => currentChange.ChangeData(buffer, index, 0xFF);

      public void Visit(Braille braille, byte data) => currentChange.ChangeData(buffer, index, 0xFF);

      public void Visit(Integer integer, byte data) => buffer.WriteMultiByteValue(index, integer.Length, currentChange, 0);

      public void Visit(IntegerEnum integerEnum, byte data) => buffer.WriteMultiByteValue(index, integerEnum.Length, currentChange, 0);

      public void Visit(IntegerHex integerHex, byte data) => buffer.WriteMultiByteValue(index, integerHex.Length, currentChange, 0);

      public void Visit(EggSection section, byte data) => buffer.WriteMultiByteValue(index, 2, currentChange, EggMoveRun.MagicNumber);

      public void Visit(EggItem item, byte data) => buffer.WriteMultiByteValue(index, 2, currentChange, 0x0000);

      public void Visit(PlmItem item, byte data) => buffer.WriteMultiByteValue(index, 2, currentChange, 0x0200);

      public void Visit(BitArray array, byte data) => buffer.WriteMultiByteValue(index, array.Length, currentChange, 0x00);

      public void Visit(MatchedWord word, byte data) => Visit((None)null, data);

      public void Visit(EndStream endStream, byte data) {
         for (int i = 0; i < endStream.Length; i++) {
            currentChange.ChangeData(buffer, endStream.Source + i, 0xFF);
         }
      }

      public void Visit(LzMagicIdentifier lz, byte data) => Visit((None)null, data);

      public void Visit(LzGroupHeader lz, byte data) => Visit((None)null, data);

      public void Visit(LzCompressed lz, byte data) => buffer.WriteMultiByteValue(index, 2, currentChange, 0xFFFF);

      public void Visit(LzUncompressed lz, byte data) => Visit((None)null, data);

      public void Visit(UncompressedPaletteColor color, byte data) => buffer.WriteMultiByteValue(index, 2, currentChange, 0xFFFF);

      public void Visit(DataFormats.Tuple tuple, byte data) => buffer.WriteMultiByteValue(index, tuple.Length, currentChange, -1);
   }
}
