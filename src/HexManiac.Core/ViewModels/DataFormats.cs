// Data Formats are simple types that provide limited meta-data that can vary based on the format.
// Data Formats use the Visitor design pattern to allow things like rendering of the data

using HavenSoft.HexManiac.Core.Models.Runs;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.HexManiac.Core.ViewModels.DataFormats {
   public interface IDataFormat : IEquatable<IDataFormat> {
      void Visit(IDataFormatVisitor visitor, byte data);
   }

   public interface IDataFormatVisitor {
      void Visit(Undefined dataFormat, byte data);
      void Visit(None dataFormat, byte data);
      void Visit(UnderEdit dataFormat, byte data);
      void Visit(Pointer pointer, byte data);
      void Visit(Anchor anchor, byte data);
      void Visit(PCS pcs, byte data);
      void Visit(EscapedPCS pcs, byte data);
      void Visit(ErrorPCS pcs, byte data);
      void Visit(Ascii ascii, byte data);
      void Visit(Integer integer, byte data);
      void Visit(IntegerEnum integer, byte data);
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
      public int EditWidth { get; }
      public UnderEdit(IDataFormat original, string text, int editWidth = 1) => (OriginalFormat, CurrentText, EditWidth) = (original, text, editWidth);

      public void Visit(IDataFormatVisitor visitor, byte data) => visitor.Visit(this, data);
      public bool Equals(IDataFormat format) {
         if (!(format is UnderEdit that)) return false;

         if (!OriginalFormat.Equals(that.OriginalFormat)) return false;
         if (EditWidth != that.EditWidth) return false;
         return CurrentText == that.CurrentText;
      }
   }
   public static class UnderEditExtensions {
      public static UnderEdit Edit(this IDataFormat format, string text) {
         if (format is UnderEdit underEdit) {
            return new UnderEdit(underEdit.OriginalFormat, underEdit.CurrentText + text, underEdit.EditWidth);
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

   public class PCS : IDataFormat {
      public int Source { get; }
      public int Position { get; }
      public string FullString { get; }
      public string ThisCharacter { get; }

      public PCS(int source, int position, string full, string character) => (Source, Position, FullString, ThisCharacter) = (source, position, full, character);

      public bool Equals(IDataFormat other) {
         if (!(other is PCS pcs)) return false;
         return pcs.Source == Source && pcs.Position == Position && pcs.FullString == FullString && pcs.ThisCharacter == ThisCharacter;
      }

      public void Visit(IDataFormatVisitor visitor, byte data) => visitor.Visit(this, data);
   }

   public class EscapedPCS : IDataFormat {
      public int Source { get; }
      public int Position { get; }
      public string FullString { get; }
      public byte ThisValue { get; }

      public EscapedPCS(int source, int position, string full, byte value) => (Source, Position, FullString, ThisValue) = (source, position, full, value);

      public bool Equals(IDataFormat other) {
         if (!(other is EscapedPCS pcs)) return false;
         return pcs.Source == Source && pcs.Position == Position && pcs.FullString == FullString && pcs.ThisValue == ThisValue;
      }

      public void Visit(IDataFormatVisitor visitor, byte data) => visitor.Visit(this, data);
   }

   public class ErrorPCS : IDataFormat {
      public int Source { get; }
      public int Position { get; }
      public string FullString { get; }
      public byte ThisValue { get; }

      public ErrorPCS(int source, int position, string full, byte value) => (Source, Position, FullString, ThisValue) = (source, position, full, value);

      public bool Equals(IDataFormat other) {
         if (!(other is EscapedPCS pcs)) return false;
         return pcs.Source == Source && pcs.Position == Position && pcs.FullString == FullString && pcs.ThisValue == ThisValue;
      }

      public void Visit(IDataFormatVisitor visitor, byte data) => visitor.Visit(this, data);
   }

   public class Ascii : IDataFormat {
      public int Source { get; }
      public int Position { get; }
      public char ThisCharacter { get; }

      public Ascii(int source, int position, char value) => (Source, Position, ThisCharacter) = (source, position, value);

      public bool Equals(IDataFormat other) {
         if (!(other is Ascii ascii)) return false;
         return ascii.Source == Source && ascii.Position == Position && ascii.ThisCharacter == ThisCharacter;
      }

      public void Visit(IDataFormatVisitor visitor, byte data) => visitor.Visit(this, data);
   }

   public class Integer : IDataFormat {
      public int Source { get; }
      public int Position { get; }
      public int Value { get; }
      public int Length { get; } // number of bytes used by this integer

      public Integer(int source, int position, int value, int length) => (Source, Position, Value, Length) = (source, position, value, length);

      public bool Equals(IDataFormat other) {
         if (!(other is Integer that)) return false;
         return Source == that.Source && Position == that.Position && Value == that.Value && Length == that.Length;
      }

      public virtual bool CanStartWithCharacter(char input) {
         return char.IsNumber(input);
      }

      public virtual void Visit(IDataFormatVisitor visitor, byte data) => visitor.Visit(this, data);
   }

   public class IntegerEnum : Integer {
      public new string Value { get; }
      public IntegerEnum(int source, int position, string value, int length) : base(source, position, -1, length) => Value = value;

      public bool Equals(IDataFormat other) {
         if (!(other is IntegerEnum that)) return false;
         return Value == that.Value && base.Equals(other);
      }

      public override bool CanStartWithCharacter(char input) {
         return char.IsLetterOrDigit(input) ||
            input == PCSRun.StringDelimeter ||
            "?-".Contains(input);
      }

      public override void Visit(IDataFormatVisitor visitor, byte data) => visitor.Visit(this, data);
   }
}
