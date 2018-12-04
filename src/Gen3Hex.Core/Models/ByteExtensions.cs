using System.Collections.Generic;

namespace HavenSoft.Gen3Hex.Core.Models {
   public static class ByteExtensions {
      public static void Write(this byte[] array, int index, int word) {
         array[index + 0] = (byte)(word >> 0);
         array[index + 1] = (byte)(word >> 8);
         array[index + 2] = (byte)(word >> 16);
         array[index + 3] = (byte)(word >> 24);
      }

      public static void WritePointer(this byte[] array, int index, int word) => array.Write(index, word + 0x08000000);

      public static int ReadWord(this IReadOnlyList<byte> array, int index) {
         int word = 0;
         word |= array[index + 0] << 0;
         word |= array[index + 1] << 8;
         word |= array[index + 2] << 16;
         word |= array[index + 3] << 24;
         return word;
      }

      public static int ReadAddress(this IReadOnlyList<byte> array, int index) => array.ReadWord(index) - 0x08000000;
   }
}
