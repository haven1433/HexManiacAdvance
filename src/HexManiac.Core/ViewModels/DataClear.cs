using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;

namespace HavenSoft.HexManiac.Core.ViewModels {
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

      public void Visit(PCS pcs, byte data) => currentChange.ChangeData(buffer, index, 0xFF);

      public void Visit(EscapedPCS pcs, byte data) => currentChange.ChangeData(buffer, index, 0xFF);

      public void Visit(ErrorPCS pcs, byte data) => currentChange.ChangeData(buffer, index, 0xFF);

      public void Visit(Ascii ascii, byte data) => currentChange.ChangeData(buffer, index, 0xFF);

      public void Visit(Integer integer, byte data) => buffer.WriteValue(currentChange, index, 0);

      public void Visit(IntegerEnum integerEnum, byte data) => buffer.WriteValue(currentChange, index, 0);
   }
}
