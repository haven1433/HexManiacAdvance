using HavenSoft.HexManiac.Core.Models.Runs;
using System;
using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.Models {
   /// <summary>
   /// A number that the game's code uses directly: the Pokémon of the speech, a song number... Thumb code has no room for big numbers in most places,
   /// so each kind of place can hold a different range: a literal holds anything, 'movs r, #imm' holds 0 to 255, and 'movs' followed by a second
   /// instruction ('lsls' or 'adds') holds more.
   /// </summary>
   public abstract class IntroConstant {
      /// <summary>The first instruction of the number, or the place where the number itself is stored.</summary>
      public int Address { get; }

      protected IntroConstant(int address) => Address = address;

      public abstract int Read(byte[] data);

      /// <summary>True if the place can store this number (the number is never negative).</summary>
      public abstract bool CanHold(int value);

      /// <summary>What the place can store, for messages ("0 to 510").</summary>
      public abstract string Limit { get; }

      public abstract void Write(IDataModel model, ModelDelta token, int value);

      /// <summary>The largest number this place can store (what the number boxes and dropdowns are limited by).</summary>
      public abstract int MaxValue { get; }

      public abstract string Describe(byte[] data);
   }

   /// <summary>A number in code made with 'movs r, #imm', optionally followed by 'lsls r, r, #k' (imm times a power of 2) or 'adds r, #imm' (a sum).</summary>
   public class IntroImmediate : IntroConstant {
      public const int SecondNone = 0, SecondShift = 1, SecondAdd = 2;

      public int Register { get; }
      /// <summary>The 'lsls' or 'adds' that follows the 'movs' (maybe with other instructions in between), or -1.</summary>
      public int SecondAddress { get; }
      public int SecondKind { get; }

      public IntroImmediate(int movAddress, int register, int secondAddress, int secondKind) : base(movAddress) {
         Register = register;
         SecondAddress = secondKind == SecondNone ? -1 : secondAddress;
         SecondKind = secondKind;
      }

      public override int Read(byte[] data) {
         var first = IntroThumb.Half(data, Address) & 0xFF;
         if (SecondKind == SecondNone) return first;
         // the second instruction is whatever is in the ROM now: writing a number can turn an 'lsls' into an 'adds' and back
         var second = IntroThumb.Half(data, SecondAddress);
         return IsAdd(second) ? first + (second & 0xFF) : first << ((second >> 6) & 31);
      }

      /// <summary>
      /// Finds 'imm' and 'shift' with imm shifted left by shift equal to the number (the largest shift is used): not all numbers have one.
      /// </summary>
      public static bool TryShift(int value, out int imm, out int shift) {
         imm = 0; shift = 0;
         if (value < 0) return false;
         if (value == 0) return true;
         // the largest shift whose remainder is exact and fits in a byte
         for (shift = 30; shift >= 0; shift--) {
            if ((value & ((1 << shift) - 1)) == 0 && (value >> shift) <= 255 && (value >> shift) > 0) { imm = value >> shift; return true; }
         }
         shift = 0;
         return false;
      }

      private static bool IsAdd(int half) => (half >> 11) == 6;

      public override bool CanHold(int value) {
         if (value < 0) return false;
         if (SecondKind == SecondNone) return value <= 255;
         if (value <= 510) return true;
         return TryShift(value, out _, out _);
      }

      public override string Limit => SecondKind == SecondNone ? "0 to 255" : "0 to 510, or a number made from a byte times a power of two";

      public override int MaxValue => SecondKind == SecondNone ? 255 : 510;

      public override void Write(IDataModel model, ModelDelta token, int value) {
         if (!CanHold(value)) throw new ArgumentOutOfRangeException(nameof(value), $"{value} does not fit in the instructions at {Address:X6}.");
         int first, second;
         if (SecondKind == SecondNone) {
            first = value;
            second = 0;
         } else if (value <= 510) {
            first = Math.Min(value, 255);
            second = 0x3000 | (Register << 8) | (value - first);             // adds r, #rest (a rest of 0 keeps the original two-instruction shape)
         } else {
            TryShift(value, out first, out var shift);
            second = (shift << 6) | (Register << 3) | Register;             // lsls r, r, #shift
         }
         WriteHalf(model, token, Address, 0x2000 | (Register << 8) | first);
         if (SecondKind != SecondNone) WriteHalf(model, token, SecondAddress, second);
      }

      private static void WriteHalf(IDataModel model, ModelDelta token, int address, int half) {
         token.ChangeData(model, address, new byte[] { (byte)half, (byte)(half >> 8) });
      }

      public override string Describe(byte[] data) {
         var text = $"movs r{Register}, #{IntroThumb.Half(data, Address) & 0xFF} at {Address:X6}";
         if (SecondKind != SecondNone) {
            var second = IntroThumb.Half(data, SecondAddress);
            text += IsAdd(second) ? $", adds #{second & 0xFF} at {SecondAddress:X6}" : $", lsls #{(second >> 6) & 31} at {SecondAddress:X6}";
         }
         return text;
      }
   }

   /// <summary>A number in code that is loaded from the literal pool next to the function: 'ldr r, [pc, #imm]'. It can hold any number.</summary>
   public class IntroLiteral : IntroConstant {
      /// <summary>The 'ldr' that loads the literal.</summary>
      public int LoadAddress { get; }

      /// <summary>How many 'ldr' instructions load this literal (more than one means that other code shares the number).</summary>
      public int Loads { get; set; } = 1;

      public IntroLiteral(int literalAddress, int loadAddress) : base(literalAddress) => LoadAddress = loadAddress;

      public override int Read(byte[] data) => IntroThumb.Word(data, Address);

      public override bool CanHold(int value) => value >= 0 && value <= 0xFFFF;

      public override string Limit => "0 to 65535";

      public override int MaxValue => 0xFFFF;

      public override void Write(IDataModel model, ModelDelta token, int value) {
         if (!CanHold(value)) throw new ArgumentOutOfRangeException(nameof(value), $"{value} does not fit in the literal at {Address:X6}.");
         model.WriteValue(token, Address, value);
      }

      public override string Describe(byte[] data) => $"literal {IntroThumb.Word(data, Address)} at {Address:X6} (loaded at {LoadAddress:X6})";
   }

   /// <summary>
   /// An address in code that points into a table of fixed-size entries at the entry of the number (FireRed's intro points at the picture and the palette of its Pokémon by table entry).
   /// The number is the entry's index; changing it moves the pointer to another entry.
   /// </summary>
   public class IntroTablePointer : IntroConstant {
      public int TableStart { get; }
      public int Stride { get; }
      public int Count { get; }

      public IntroTablePointer(int literalAddress, int tableStart, int stride, int count) : base(literalAddress) => (TableStart, Stride, Count) = (tableStart, stride, count);

      public override int Read(byte[] data) {
         var pointer = IntroThumb.Word(data, Address) - IntroThumb.RomBase - TableStart;
         return pointer < 0 || pointer % Stride != 0 ? -1 : pointer / Stride;
      }

      public override bool CanHold(int value) => value >= 0 && value < Count;

      public override string Limit => $"0 to {Count - 1}";

      public override int MaxValue => Count - 1;

      public override void Write(IDataModel model, ModelDelta token, int value) {
         if (!CanHold(value)) throw new ArgumentOutOfRangeException(nameof(value), $"{value} is not an entry of the table at {TableStart:X6}.");
         IntroEdit.Repoint(model, token, Address, TableStart + Stride * value);
      }

      public override string Describe(byte[] data) => $"pointer to table entry {Read(data)} at {Address:X6}";
   }

   /// <summary>Writing pointers the way the rest of the editor does, so the pointer bookkeeping (what points where) stays right.</summary>
   public static class IntroEdit {
      /// <summary>Points the 4 bytes at 'source' to a new destination. The old destination forgets this pointer and the new one learns it.</summary>
      public static void Repoint(IDataModel model, ModelDelta token, int source, int destination) {
         model.ClearFormat(token, source, 4);
         model.WritePointer(token, source, destination);
         model.ObserveRunWritten(token, new PointerRun(source));
      }

      /// <summary>Finds room for new data (or makes room at the end of the ROM) and returns its address. The room is not written.</summary>
      public static int AllocateData(IDataModel model, ModelDelta token, int length) {
         var address = model.FindFreeSpace(model.FreeSpaceStart, length);
         if (address < 0) {
            address = model.Count;
            model.ExpandData(token, model.Count + length + 0x10);
         }
         return address;
      }
   }
}
