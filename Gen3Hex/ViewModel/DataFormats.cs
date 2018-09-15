// Data Formats are simple types that provide limited meta-data that can vary based on the format.
// Data Formats use the Visitor design pattern to allow things like rendering of the data

using System;

namespace HavenSoft.ViewModel.DataFormats {
   public interface IDataFormat : IEquatable<IDataFormat> {
      void Visit(IDataFormatVisitor visitor, byte data);
   }

   public interface IDataFormatVisitor {
      void Visit(Undefined dataFormat, byte data);
      void Visit(None dataFormat, byte data);
   }

   /// <summary>
   /// Used for locations where there is no data.
   /// As in the location is out of range of the file.
   /// </summary>
   public class Undefined : IDataFormat {
      public static Undefined Instance { get; } = new Undefined();
      private Undefined() { }

      public void Visit(IDataFormatVisitor visitor, byte data) => visitor.Visit(this, data);
      public bool Equals(IDataFormat format) => format is Undefined;
   }

   /// <summary>
   /// Used for locations where the format is unknown, or the data is unused.
   /// Basically everything is 'None' unless we have special information about it.
   /// </summary>
   public class None : IDataFormat {
      public bool IsUnused { get; }
      public None(byte value) => IsUnused = value == 0x00 || value == 0xFF;

      public void Visit(IDataFormatVisitor visitor, byte data) => visitor.Visit(this, data);
      public bool Equals(IDataFormat format) {
         if (format is None that) {
            return this.IsUnused == that.IsUnused;
         }

         return false;
      }
   }
}