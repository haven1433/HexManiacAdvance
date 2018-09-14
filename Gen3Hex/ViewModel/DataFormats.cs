// Data Formats are simple types that provide limited meta-data that can vary based on the format.
// Data Formats use the Visitor design pattern to allow things like rendering of the data

namespace HavenSoft.ViewModel.DataFormats {
   public interface IDataFormat {
      void Visit(IDataFormatVisitor visitor, byte data);
   }

   public interface IDataFormatVisitor {
      void Visit(Undefined dataFormat, byte data);
      void Visit(None dataFormat, byte data);
   }

   public class Undefined : IDataFormat {
      public static Undefined Instance { get; } = new Undefined();
      private Undefined() { }

      public void Visit(IDataFormatVisitor visitor, byte data) => visitor.Visit(this, data);
   }

   public class None : IDataFormat {
      public bool IsUnused { get; }
      public None(byte value) => IsUnused = value == 0x00 || value == 0xFF;

      public void Visit(IDataFormatVisitor visitor, byte data) => visitor.Visit(this, data);
   }
}