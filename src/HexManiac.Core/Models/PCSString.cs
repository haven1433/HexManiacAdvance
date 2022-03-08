using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace HavenSoft.HexManiac.Core.Models {
   public class PCSString {
      public static IReadOnlyList<string> PCS;
      public static IReadOnlyList<byte> Newlines;

      public static readonly byte DynamicEscape = 0xF7;
      public static readonly byte FunctionEscape = 0xFC;
      public static readonly byte ButtonEscape = 0xF8;
      public static readonly byte Escape = 0xFD;

      static PCSString() {
         var pcs = new string[0x100];
         pcs[0] = " ";
         Fill(pcs, "ÀÁÂÇÈÉÊËÌ", 0x01);
         Fill(pcs, "ÎÏÒÓÔ", 0x0B);
         Fill(pcs, "ŒÙÚÛÑßàá", 0x10);
         Fill(pcs, "çèéêëì", 0x19);
         Fill(pcs, "îïòóôœùúûñºª", 0x20);
         pcs[0x2C] = "\\e";
         Fill(pcs, "& \\+", 0x2D);
         Fill(pcs, "\\Lv = ;", 0x34);


         pcs[0x48] = "\\r"; // right?

         Fill(pcs, "¿ ¡ \\pk \\mn \\Po \\Ke \\Bl \\Lo \\Ck Í", 0x51);

         Fill(pcs, "%()", 0x5B);

         pcs[0x68] = "â";
         pcs[0x6F] = "í";

         Fill(pcs, "\\au \\ad \\al \\ar", 0x79); // arrows

         pcs[0x84] = "\\d";
         Fill(pcs, "\\< \\>", 0x85);

         Fill(pcs, "0123456789", 0xA1);
         // \. -> ellipsis   \qo \qc -> quote open/close    \sm \sf -> male/female symbols
         Fill(pcs, "! ? . - ‧ \\. \\qo \\qc ‘ ' \\sm \\sf $ , * /", 0xAB);

         Fill(pcs, "ABCDEFGHIJKLMNOPQRSTUVWXYZ", 0XBB);
         Fill(pcs, "abcdefghijklmnopqrstuvwxyz", 0xD5);

         Fill(pcs, ":ÄÖÜäöü", 0xF0);

         pcs[0xF7] = "\\?"; // decomp 'dynamic', another placeholder mechanism. Expects a single-byte argument
         pcs[0xF8] = "\\btn"; // button escape: next byte is read as a button ID. order is [A, B, L, R, START, SELECT, UP, DOWN, LEFT, RIGHT, UP_DOWN, LEFT_RIGHT, DPAD]
         pcs[0xF9] = "\\9";
         pcs[0xFA] = "\\l";
         pcs[0xFB] = "\\pn";
         pcs[0xFC] = "\\CC"; // control code: next byte is a control code (function escape), followed by some number of argument bytes
         pcs[0xFD] = "\\\\"; // escape character: next byte is interpreted raw
         pcs[0xFE] = "\\n";
         pcs[0xFF] = "\"";

         PCS = pcs;

         Newlines = new byte[] { 0xFA, 0xFB, 0xFE };
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
            if (currentByte == 0xFA || currentByte == 0xFB || currentByte == 0xFE) result.Append(Environment.NewLine);

            if (length == 1) break;

            if ((currentByte == Escape || currentByte == DynamicEscape || currentByte == ButtonEscape) && i < length - 1) {
               result.Append(data[startIndex + i + 1].ToString("X2"));
               i++;
            }
            if (currentByte == FunctionEscape) {
               var escapeLength = GetLengthForControlCode(data[startIndex + i + 1]);
               for (int j = 0; j < escapeLength; j++) result.Append(data[startIndex + i + 1 + j].ToString("X2"));
               i += escapeLength;
            }

            if (currentByte == 0xFF) break;
         }
         return result.ToString();
      }

      public static List<byte> Convert(string input) => Convert(input, out var _);
      public static List<byte> Convert(string input, out bool containsBadCharacters) {
         if (input.StartsWith("\"")) input = input.Substring(1); // trim leading " at start of string
         var result = new List<byte>();
         containsBadCharacters = false;

         int index = 0;
         while (index < input.Length) {
            bool foundMatch = false;
            for (int i = 0; i < 0x100; i++) {
               if (PCS[i] == null) continue;
               var checkCharacter = PCS[i];
               if (input.Length < index + checkCharacter.Length) continue;
               var checkInput = input.Substring(index, checkCharacter.Length);
               if (checkCharacter.StartsWith("\\")) {
                  // escape sequences don't care about case
                  checkCharacter = checkCharacter.ToUpper();
                  checkInput = checkInput.ToUpper();
               }
               if (checkInput != checkCharacter) continue;
               result.Add((byte)i);
               index += PCS[i].Length - 1;
               if ((i == Escape || i == DynamicEscape || i == ButtonEscape) && input.Length > index + 2) {
                  if (byte.TryParse(input.Substring(index + 1, 2), NumberStyles.HexNumber, CultureInfo.CurrentCulture, out byte parsed)) {
                     result.Add(parsed);
                  }
                  index += 2;
               }
               if (i == FunctionEscape && input.Length > index + 2) {
                  if (byte.TryParse(input.Substring(index + 1, 2), NumberStyles.HexNumber, CultureInfo.CurrentCulture, out byte parsed)) {
                     var extraEscapedBytesCount = GetLengthForControlCode(parsed);
                     result.Add(parsed);
                     for (int j = 1; j < extraEscapedBytesCount; j++) {
                        if (input.Length > index + 4 && byte.TryParse(input.Substring(index + 3, 2), NumberStyles.HexNumber, CultureInfo.CurrentCulture, out parsed)) {
                           result.Add(parsed);
                        }
                        index += 2;
                     }
                  }
                  index += 2;
               }
               foundMatch = true;
               break;
            }
            containsBadCharacters |= !foundMatch;
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
            else if (data[start + length] == DynamicEscape) length++;
            else if (data[start + length] == ButtonEscape) length++;
            else if (data[start + length] == FunctionEscape) {
               length += GetLengthForControlCode(data[start + length + 1]);
            }
            length++;
         }
         return -1;
      }

      private static int GetLengthForControlCode(byte code) {
         if (code == 0x09) return 1; // pause : no variables
         if (code == 0x0A) return 1; // wait for sound effect
         if (code == 0x0B) return 3; // play background music : 1 variable, but it takes 2 bytes
         if (code == 0x10) return 3; // play sound effects : 2 variables
         if (code > 0x14) return 1;  // single-byte functions : no variables
         return 2;                   // most functions have a 1 byte code and a 1 byte variable
      }

      public static bool IsEscaped(IReadOnlyList<byte> data, int index) {
         if (index == 0) return false;
         if (data[index - 1].IsAny(Escape, DynamicEscape, ButtonEscape, FunctionEscape)) return true;
         if (index == 1) return false;
         if (index > 1 && data[index - 2] == FunctionEscape && GetLengthForControlCode(data[index - 1]) >= 2) return true;
         if (index > 2 && data[index - 3] == FunctionEscape && GetLengthForControlCode(data[index - 2]) >= 3) return true;
         return false;
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
