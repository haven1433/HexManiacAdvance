using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace HavenSoft.HexManiac.Core.Models {
   public interface ITextConverter {
      List<byte> Convert(string text, out bool containsBadCharacters);
      string Convert(IReadOnlyList<byte> data, int startIndex, int length);
      bool AnyMacroStartsWith(string input);
   }

   public class PCSConverter : ITextConverter {
      private readonly string gameCode;
      public PCSConverter(string gameCode) {
         this.gameCode = gameCode;
         if (this.gameCode.Length > 4) this.gameCode = gameCode.Substring(4);
      }

      public List<byte> Convert(string text, out bool containsBadCharacters) {
         return PCSString.Convert(text, gameCode, out containsBadCharacters);
      }

      public string Convert(IReadOnlyList<byte> data, int startIndex, int length) {
         return PCSString.Convert(gameCode, data, startIndex, length);
      }

      public bool AnyMacroStartsWith(string input) {
         if (!PCSString.TextMacros.TryGetValue(gameCode, out var macros)) return false;
         return macros.Keys.Any(key => key.StartsWith(input));
      }
   }

   public static class PCSString {
      private const string TextReferenceFileName = "resources/pcsReference.txt";

      public static IReadOnlyList<string> PCS;
      public static IReadOnlyList<byte> Newlines;
      public static IReadOnlyDictionary<byte, byte> ControlCodeLengths;
      public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, byte[]>> TextMacros;

      public static readonly byte DynamicEscape = 0xF7;
      public static readonly byte FunctionEscape = 0xFC;
      public static readonly byte ButtonEscape = 0xF8;
      public static readonly byte Escape = 0xFD;

      static PCSString() {
         if (File.Exists(TextReferenceFileName)) {
            var referenceText = File.ReadAllLines(TextReferenceFileName);
            PCS = GetPCSFromReference(referenceText);
            ControlCodeLengths = GetControlCodeLengthsFromReference(referenceText);
            TextMacros = GetTextMacrosFromReference(referenceText);
         } else {
            PCS = GetDefaultPCS();
            ControlCodeLengths = GetDefaultControlCodeLengths();
            TextMacros = new Dictionary<string, IReadOnlyDictionary<string, byte[]>>();
         }

         Newlines = new byte[] { 0xFA, 0xFB, 0xFE };
      }

      private static IReadOnlyList<string> GetPCSFromReference(string[] reference) {
         var pcs = new string[0x100];

         for (int i = 0; i < reference.Length; i++) {
            if (!reference[i].StartsWith("0x")) continue;
            var line = reference[i].Substring(2).Split('#').First().Trim();
            var parts = line.Split(new[] { '=' }, 2);
            if (parts.Length != 2) continue;
            parts[0] = parts[0].Trim();
            parts[1] = parts[1].Trim();
            if (!int.TryParse(parts[0], NumberStyles.HexNumber, CultureInfo.CurrentCulture, out int start)) continue;
            if (parts[1].Length > 1 && parts[1].StartsWith("\"") && parts[1].EndsWith("\"")) {
               parts[1] = parts[1].Substring(1, parts[1].Length - 2);
               for (int j = 0; j < parts[1].Length; j++) pcs[start + j] = parts[1][j].ToString();
            } else {
               pcs[start] = parts[1];
            }
         }

         return pcs;
      }

      private static IReadOnlyDictionary<byte, byte> GetControlCodeLengthsFromReference(string[] reference) {
         var lengths = new Dictionary<byte, byte>();

         for (int i = 0; i < reference.Length; i++) {
            if (!reference[i].StartsWith("CC_")) continue;
            var line = reference[i].Substring(3).Split('#').First().Trim();
            var parts = line.Split(new[] { '=' }, 2);
            if (parts.Length != 2) continue;
            if (!byte.TryParse(parts[0].Trim(), NumberStyles.HexNumber, CultureInfo.CurrentCulture, out byte code)) continue;
            if (!byte.TryParse(parts[1].Trim(), out byte argCount)) continue;
            lengths[code] = (byte)(argCount + 1);
         }

         return lengths;
      }

      private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, byte[]>> GetTextMacrosFromReference(string[] reference) {
         var macros = new Dictionary<string, IReadOnlyDictionary<string, byte[]>>();
         foreach(var game in new[] { "AXVE", "AXPE", "BPRE", "BPGE", "BPEE" }) {
            macros.Add(game, GetTextMacrosFromReference(reference, game));
         }
         return macros;
      }

      private static IReadOnlyDictionary<string, byte[]> GetTextMacrosFromReference(string[] reference, string gameCode) {
         bool isInMacroSection = false;
         var results = new Dictionary<string, byte[]>();

         for (int i = 0; i < reference.Length; i++) {
            if (reference[i].Trim().StartsWith("@!game(")) isInMacroSection = reference[i].Contains(gameCode);
            if (!isInMacroSection) continue;
            if (!reference[i].StartsWith("[")) continue;
            var endOfMacro = reference[i].IndexOf("]");
            var bytes = reference[i].Substring(endOfMacro + 1).Trim().ToByteArray();
            var name = reference[i].Substring(0, endOfMacro + 1);
            results.Add(name, bytes);
         }

         return results;
      }

      private static IReadOnlyList<string> GetDefaultPCS() {
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

         return pcs;
      }

      private static IReadOnlyDictionary<byte, byte> GetDefaultControlCodeLengths() {
         var lengths = new Dictionary<byte, byte> {
            [0x04] = 4, // (text, shadow, highlight) -> 3 params
            [0x09] = 1, // pause : no variables
            [0x0A] = 1, // wait for sound effect
            [0x0B] = 3, // play background music : 1 variable, but it takes 2 bytes
            [0x10] = 3, // play sound effects : 2 variables
         };
         return lengths;
      }

      public static string Convert(IReadOnlyList<byte> data, int startIndex, int length) => Convert(string.Empty, data, startIndex, length);

      public static string Convert(string macroSet, IReadOnlyList<byte> data, int startIndex, int length) {
         var result = new StringBuilder("\"", length * 2);
         if (!TextMacros.TryGetValue(macroSet, out var textMacros)) textMacros = null;

         for (int i = 0; i < length; i++) {
            // check macros
            if (textMacros != null) {
               string macro = FindMacro(textMacros, data, startIndex + i);
               if (macro != null) {
                  result.Append(macro);
                  i += TextMacros[macroSet][macro].Length - 1;
                  continue;
               }
            }

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
      public static List<byte> Convert(string input, out bool containsBadCharacters) => Convert(input, string.Empty, out containsBadCharacters);
      public static List<byte> Convert(string input, string macroSet, out bool containsBadCharacters) {
         if (input.StartsWith("\"")) input = input.Substring(1); // trim leading " at start of string
         var result = new List<byte>();
         containsBadCharacters = false;

         if (!TextMacros.TryGetValue(macroSet, out var textMacros)) textMacros = null;

         int index = 0;
         while (index < input.Length) {
            bool foundMatch = false;

            // check macros
            if (input[index] == '[' && textMacros!=null) {
               var closeMacro = input.Substring(index).IndexOf(']') + index;
               if (closeMacro > index) {
                  var candidate = input.Substring(index, closeMacro + 1 - index);
                  if (textMacros.TryGetValue(candidate, out var bytes)) {
                     index += candidate.Length;
                     result.AddRange(bytes);
                     continue;
                  }
               }
            }

            // check characters and escape codes
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

      private static string FindMacro(IReadOnlyDictionary<string, byte[]> macros, IReadOnlyList<byte> data, int index) {
         foreach (var kvp in macros) {
            bool matches = true;
            for (int i = 0; i < kvp.Value.Length && matches; i++) {
               matches = data.Count > index + i && kvp.Value[i] == data[index + i];
            }
            if (matches) return kvp.Key;
         }
         return null;
      }

      private static int GetLengthForControlCode(byte code) {
         if (ControlCodeLengths.TryGetValue(code, out var length)) return length;
         if (code > 0x14) return 1;  // single-byte functions : no variables
         return 2;                   // most functions have a 1 byte code and a 1 byte variable
      }

      public static bool IsEscaped(IReadOnlyList<byte> data, int index) {
         if (index == 0) return false;
         if (data[index - 1].IsAny(Escape, DynamicEscape, ButtonEscape, FunctionEscape)) return true;
         if (index == 1) return false;
         for (int codeDist = 2; codeDist <= 4; codeDist++) {
            if (index >= codeDist && data[index - codeDist] == FunctionEscape && GetLengthForControlCode(data[index - codeDist + 1]) >= codeDist) return true;
         }
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
