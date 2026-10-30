using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
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

      public void Visit(Pointer pointer, byte data) => Result = pointer.DestinationAsText;

      public void Visit(Anchor anchor, byte data) => anchor.OriginalFormat.Visit(this, data);

      public void Visit(SpriteDecorator sprite, byte data) => sprite.OriginalFormat.Visit(this, data);

      public void Visit(StreamEndDecorator decorator, byte data) => decorator.OriginalFormat.Visit(this, data);

      public void Visit(PCS pcs, byte data) {
         Result = pcs.ThisCharacter;
      }

      public void Visit(EscapedPCS pcs, byte data) => Visit((None)null, data);

      public void Visit(ErrorPCS pcs, byte data) => Visit((None)null, data);

      public void Visit(Ascii ascii, byte data) => Result = ((char)data).ToString();

      public void Visit(Braille braille, byte data){
         if (data == 0xFF) Result = "\"";
         else if (BrailleRun.Encoding.TryGetValue(data, out var value)) Result = value.ToString();
         else Result = " ";
      }

      public void Visit(Integer integer, byte data) => Result = integer.Value.ToString();

      public void Visit(IntegerEnum integerEnum, byte data) => Result = integerEnum.Value;

      public void Visit(IntegerHex integerHex, byte data) => Result = integerHex.ToString();

      public void Visit(EggSection section, byte data) => Result = section.SectionName;

      public void Visit(EggItem item, byte data) => Result = item.ItemName;

      public void Visit(PlmItem item, byte data) => Result = item.ToString();

      public void Visit(BitArray array, byte data) => Visit((None)null, data);

      public void Visit(MatchedWord word, byte data) => Visit((None)null, data);

      public void Visit(EndStream endStream, byte data) => Result = new string(new[] { ArrayRun.ArrayStart, ArrayRun.ArrayEnd });

      public void Visit(LzMagicIdentifier lz, byte data) => Result = "lz";

      public void Visit(LzGroupHeader lz, byte data) => Visit((None)null, data);

      public void Visit(LzCompressed lz, byte data) {
         var start = index;
         (int runLength, int runOffset) = LZRun.ReadCompressedToken(buffer, ref start);
         Result = $"{runLength}:{runOffset}";
      }

      public void Visit(LzUncompressed lz, byte data) => Visit((None)null, data);

      public void Visit(UncompressedPaletteColor color, byte data) => Result = color.ToString();

      public void Visit(DataFormats.Tuple tuple, byte data) => Result = tuple.ToString();
   }
}
