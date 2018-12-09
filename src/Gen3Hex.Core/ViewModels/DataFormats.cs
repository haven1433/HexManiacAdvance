// Data Formats are simple types that provide limited meta-data that can vary based on the format.
// Data Formats use the Visitor design pattern to allow things like rendering of the data

using System;
using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.Gen3Hex.Core.ViewModels.DataFormats {
   public interface IDataFormat : IEquatable<IDataFormat> {
      void Visit(IDataFormatVisitor visitor, byte data);
   }

   public interface IDataFormatVisitor {
      void Visit(Undefined dataFormat, byte data);
      void Visit(None dataFormat, byte data);
      void Visit(UnderEdit dataFormat, byte data);
      void Visit(Pointer pointer, byte data);
      void Visit(Anchor anchor, byte data);
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

   public class Pointer : IDataFormat {
      public const int NULL = -0x08000000;
      public int Source { get; }      // 6 hex digits
      public int Position { get; }    // 0 through 3
      public int Destination { get; } // 6 hex digits
      public string DestinationName { get; } // null if there is no name for that anchor

      public Pointer(int source, int positionInPointer, int destination, string destinationName) {
         Source = source;
         Position = positionInPointer;
         Destination = destination;
         DestinationName = destinationName;
      }

      public bool Equals(IDataFormat other) {
         if (!(other is Pointer pointer)) return false;
         return pointer.Source == Source && pointer.Position == Position && pointer.Destination == Destination;
      }

      public void Visit(IDataFormatVisitor visitor, byte data) => visitor.Visit(this, data);
   }

   public class Anchor : IDataFormat {
      public IDataFormat OriginalFormat { get; }
      public string Name { get; }
      public string Format { get; }
      public IReadOnlyList<int> Sources { get; }

      public Anchor(IDataFormat original, string name, string format, IReadOnlyList<int> sources) => (OriginalFormat, Name, Format, Sources) = (original, name, format, sources);

      public bool Equals(IDataFormat other) {
         if (!(other is Anchor anchor)) return false;
         return anchor.Name == Name && anchor.Format == Format && anchor.Sources.SequenceEqual(Sources) && anchor.OriginalFormat.Equals(OriginalFormat);
      }

      public void Visit(IDataFormatVisitor visitor, byte data) => visitor.Visit(this, data);
   }
}
