using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;

namespace HavenSoft.HexManiac.Core.ViewModels {
   /// <summary>
   /// Given a data format, decide how to best display that as text
   /// </summary>
   public class ConvertCellToText : IDataFormatVisitor {
      private readonly IDataModel buffer;
      private readonly int index;

      public string Result { get; private set; }

      public ConvertCellToText(IDataModel buffer, int index) {
         this.buffer = buffer;
         this.index = index;
      }

      public void Visit(Undefined dataFormat, byte data) { }

      public void Visit(None dataFormat, byte data) => Result = data.ToString("X2");

      public void Visit(UnderEdit dataFormat, byte data) {
         throw new NotImplementedException();
      }

      public void Visit(Pointer pointer, byte data) {
         var destination = pointer.Destination.ToString("X6");
         Result = $"<{destination}>";
         if (!string.IsNullOrEmpty(pointer.DestinationName)) Result = $"<{pointer.DestinationName}>";
      }

      public void Visit(Anchor anchor, byte data) => anchor.OriginalFormat.Visit(this, data);

      public void Visit(PCS pcs, byte data) {
         Result = pcs.ThisCharacter;
      }

      public void Visit(EscapedPCS pcs, byte data) => Visit((None)null, data);

      public void Visit(ErrorPCS pcs, byte data) => Visit((None)null, data);

      public void Visit(Ascii ascii, byte data) => Result = ((char)data).ToString();

      public void Visit(Integer integer, byte data) => Result = integer.Value.ToString();

      public void Visit(IntegerEnum integerEnum, byte data) => Result = integerEnum.Value;

      public void Visit(EggSection section, byte data) => Result = section.SectionName;

      public void Visit(EggItem item, byte data) => Result = item.ItemName;
   }
}
