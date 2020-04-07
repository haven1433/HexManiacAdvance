using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace HavenSoft.HexManiac.Core.Models {
   public class PCSString {
      public static IReadOnlyList<string> PCS;
      public static IReadOnlyList<byte> Newlines;

      public static readonly byte DoubleEscape = 0xFC;
      public static readonly byte Escape = 0xFD;

      static PCSString() {
         var pcs = new string[0x100];
         pcs[0] = " ";
         Fill(pcs, "ÀÁÂÇÈÉÊËÌ", 0x01);
         Fill(pcs, "ÎÏÒÓÔŒÙÚÛÑßàáçèéêëì", 0x0B);
         Fill(pcs, "îïòóôœùúûñºª", 0x20);
         Fill(pcs, "& \\+", 0x2D);
         Fill(pcs, "=;", 0x35);


         pcs[0x48] = "\\r"; // right?

         Fill(pcs, "¿ ¡ \\pk \\mn \\Po \\Ke \\Bl \\Lo \\Ck Í", 0x51);

         Fill(pcs, "%()", 0x5B);

         pcs[0x68] = "â";
         pcs[0x6F] = "í";

         Fill(pcs, "\\< \\>", 0x85);

         Fill(pcs, "0123456789", 0xA1);
         // \. -> ellipsis   \qo \qc -> quote open/close    \sm \sf -> male/female symbols
         Fill(pcs, "! ? . - ‧ \\. \\qo \\qc ‘ ' \\sm \\sf $ , * /", 0xAB);

         Fill(pcs, "ABCDEFGHIJKLMNOPQRSTUVWXYZ", 0XBB);
         Fill(pcs, "abcdefghijklmnopqrstuvwxyz", 0xD5);

         Fill(pcs, ":ÄÖÜäöü", 0xF0);

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
         var result = new StringBuilder("\"", length * 2);

         for (int i = 0; i < length; i++) {
            var currentByte = data[startIndex + i];
            if (PCS[currentByte] == null) {
               if (i == 0) return null;
               if (data[startIndex + i - 1] == 0xFF) return result.ToString();
            }
            result.Append(PCS[currentByte]);

            // this line optimized for maximum speed. Otherwise would like to use the Newlines array.
            if (currentByte == 0xFB || currentByte == 0xFE) result.Append(Environment.NewLine);

            if (currentByte == Escape) {
               result.Append(data[startIndex + i + 1].ToString("X2"));
               i++;
            }

            if (currentByte == 0xFF) break;
         }
         return result.ToString();
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
         int repeatCount = 0;
         int spacesCount = 0;
         while (start + length < data.Count && length <= maxLength) {
            if (data[start + length] == recent) {
               repeatCount++;
            } else {
               repeatCount = 1;
               recent = data[start + length];
            }
            if (data[start + length] == 0x00) spacesCount++;
            if (repeatCount > 3 && !allowCharacterRepeates) return -1; // not a string if it has more than 3 of the same character in a row.
            if (PCS[recent] == null) return -1; // not valid string data
            if (data[start + length] == 0xFF) {
               if (spacesCount * 2 > length + 1 && spacesCount != length) return -1; // over half the string is whitespace. Probably a false string.
               return length + 1;  // end of string. Add one extra space for the end-of-stream byte
            }
            if (data[start + length] == Escape) length++;               // escape character, skip the next byte
            else if (data[start + length] == DoubleEscape) length += 2; // double-escape character, skip the next 2 bytes
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
