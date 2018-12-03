using HavenSoft.Gen3Hex.Core.ViewModels.DataFormats;

namespace HavenSoft.Gen3Hex.Core.ViewModels {
   public class HexElement {
      public static readonly HexElement Undefined = new HexElement(0, DataFormats.Undefined.Instance);
      public byte Value { get; }
      public IDataFormat Format { get; }

      public HexElement(byte value, IDataFormat format) => (Value, Format) = (value, format);
   }
}
