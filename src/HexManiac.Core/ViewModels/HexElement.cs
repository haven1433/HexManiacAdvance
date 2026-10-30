using HavenSoft.HexManiac.Core.ViewModels.DataFormats;

namespace HavenSoft.HexManiac.Core.ViewModels {
   public class HexElement {
      public static readonly HexElement Undefined = new HexElement(0, false, DataFormats.Undefined.Instance);
      public byte Value { get; }
      public bool Edited { get; }
      public IDataFormat Format { get; }

      public HexElement(byte value, bool edited, IDataFormat format) => (Value, Edited, Format) = (value, edited, format);
      public HexElement(HexElement source, IDataFormat newFormat) => (Value, Edited, Format) = (source.Value, source.Edited, newFormat);
   }
}
