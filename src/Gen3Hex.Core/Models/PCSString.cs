using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HavenSoft.Gen3Hex.Core.Models {
   public class PCSString {
      public static IReadOnlyList<string> PCS;
      public static IReadOnlyList<byte> Newlines;

      public static readonly byte Escape = 0xFD;

      static PCSString() {
         var pcs = new string[0x100];
         pcs[0] = " ";

         pcs[0x1B] = "é";
         pcs[0x2D] = "&";

         Fill(pcs, "\\pk \\mn \\Po \\Ke \\Bl \\Lo \\Ck", 0x53);

         Fill(pcs, "%()", 0x5B);
         Fill(pcs, "0123456789", 0xA1);
         // \. -> ellipsis   \qo \qc -> quote open/close    \sm \sf -> male/female symbols
         Fill(pcs, "! ? . - ‧ \\. \\qo \\qc ‘ ' \\sm \\sf $ , * /", 0xAB);

         Fill(pcs, "ABCDEFGHIJKLMNOPQRSTUVWXYZ", 0XBB);
         Fill(pcs, "abcdefghijklmnopqrstuvwxyz", 0xD5);

         pcs[0xF0] = ":";
         pcs[0xF9] = "\\9";
         pcs[0xFA] = "\\l";
         pcs[0xFB] = "\\pn";
         pcs[0xFC] = "\\CC";
         pcs[0xFD] = "\\\\"; // escape character: next byte is interpreted raw
         pcs[0xFE] = "\\n";
         pcs[0xFF] = "\"";

         PCS = pcs;

         Newlines = new byte[] { 0xFB, 0xFE };
      }

      public static string Convert(IReadOnlyList<byte> data, int startIndex, int length) {
         var result = "\"";

         for (int i = 0; i < length; i++) {
            if (PCS[data[startIndex + i]] == null) return null;
            result += PCS[data[startIndex + i]];
            if (Newlines.Contains(data[startIndex + i])) result += Environment.NewLine;
            if (data[startIndex + i] == Escape) {
               result += data[startIndex + i + 1].ToString("X2");
               i++;
            }
         }
         return result;
      }

      public static List<byte> Convert(string input) {
         if (input.StartsWith("\"")) input = input.Substring(1); // trim leading " at start of string
         var result = new List<byte>();

         int index = 0;
         while (index < input.Length) {
            for (int i = 0; i < 0x100; i++) {
               if (PCS[i] == null) continue;
               if (!input.Substring(index).StartsWith(PCS[i])) continue;
               result.Add((byte)i);
               index += PCS[i].Length - 1;
               if (i == Escape && input.Length > index + 2) {
                  result.Add(byte.Parse(input.Substring(index + 1, 2), NumberStyles.HexNumber));
                  index += 2;
               }
               break;
            }
            index++; // always increment by one, even if the character was not found. This lets us skip past newlines and such.
         }

         // make sure it ends with the 0xFF end-of-string byte
         if (result.Count == 0 || result[result.Count - 1] != 0xFF) result.Add(0xFF);

         return result;
      }

      /// <summary>
      /// Figure out the length of a string starting at a given location in the data.
      /// If the data doesn't represent a string, return -1.
      /// </summary>
      public static int ReadString(IReadOnlyList<byte> data, int start, bool allowCharacterRepeates, int maxLength = int.MaxValue) {
         int length = 0;
         byte recent = data[start];
         int count = 0;
         while (start + length < data.Count && length <= maxLength) {
            if (data[start + length] == recent) {
               count++;
            } else {
               count = 1;
               recent = data[start + length];
            }
            if (count > 3 && !allowCharacterRepeates) return -1; // not a string if it has more than 3 of the same character in a row.
            if (PCS[recent] == null) return -1; // not valid string data
            if (data[start + length] == 0xFF) return length + 1;  // end of string. Add one extra space for the end-of-stream byte
            if (data[start + length] == Escape) length++;     // escape character, skip the next byte
            length++;
         }
         return -1;
      }

      private static void Fill(string[] array, string characters, int startIndex) {
         if (characters.Contains(" ")) {
            foreach (var part in characters.Split(' ')) {
               array[startIndex] = part;
               startIndex++;
            }
         } else {
            for (int i = 0; i < characters.Length; i++) {
               array[startIndex + i] = characters.Substring(i, 1);
            }
         }
      }
   }
}
