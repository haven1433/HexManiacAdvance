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
      void Visit(UnderEdit dataFormat, byte data);
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
      public static None Instance { get; } = new None();
      private None() { }

      public void Visit(IDataFormatVisitor visitor, byte data) => visitor.Visit(this, data);
      public bool Equals(IDataFormat format) => format is None;
   }

   public class UnderEdit : IDataFormat {
      public IDataFormat OriginalFormat { get; }
      public string CurrentText { get; }
      public UnderEdit(IDataFormat original, string text) => (OriginalFormat, CurrentText) = (original, text);

      public void Visit(IDataFormatVisitor visitor, byte data) => visitor.Visit(this, data);
      public bool Equals(IDataFormat format) {
         var that = format as UnderEdit;
         if (that == null) return false;

         if (!OriginalFormat.Equals(that.OriginalFormat)) return false;
         return CurrentText == that.CurrentText;
      }
   }
   public static class UnderEditExtensions {
      public static UnderEdit Edit(this IDataFormat format, string text) {
         if (format is UnderEdit underEdit) {
            return new UnderEdit(underEdit.OriginalFormat, underEdit.CurrentText + text);
         }

         return new UnderEdit(format, text);
      }
   }
}
