using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
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

      /// <summary>
      /// Resolve the pixel-width of a input string,
      /// if the string is shown in a textbox.
      /// </summary>
      IReadOnlyList<TextSegment> GetOverflow(string input, int maxWidth);
   }

   public class PCSConverter : ITextConverter {
      private readonly string gameCode;
      private readonly IDataModel model;
      private Lazy<int[]> fontWidth;

      public PCSConverter(string gameCode, IDataModel model) {
         this.gameCode = gameCode;
         if (this.gameCode.Length > 4) this.gameCode = gameCode.Substring(4);
         this.model = model;
         fontWidth = new(UpdateFontWidthCache);
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

      public IReadOnlyList<TextSegment> GetOverflow(string input, int maxWidth) {
         if (!PCSString.TextMacros.TryGetValue(gameCode, out var macros)) macros = new Dictionary<string, byte[]>();
         if (fontWidth == null) UpdateFontWidthCache();
         return PCSString.GetOverflow(macros, fontWidth.Value, input, maxWidth);
      }

      private int[] UpdateFontWidthCache() {
         if (model == null) return Array.Empty<int>();
         var table = model.GetTable(HardcodeTablesModel.FontWidthTable) ?? model.GetTable(HardcodeTablesModel.BackupFontWidthTable);
         if (table == null) return Array.Empty<int>();
         var result = new int[table.ElementCount];
         for (int i = 0; i < result.Length; i++) result[i] = model[table.Start + i];
         return result;
      }
   }

   public static class PCSString {
      private const string TextReferenceFileName = "resources/pcsReference.txt";

      public static IReadOnlyList<string> PCS;
      public static IReadOnlySet<string> ValidInProgressEscapes;
      public static IReadOnlyList<byte> Newlines;
      public static IReadOnlyDictionary<byte, byte> ControlCodeLengths;
      public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, byte[]>> TextMacros;
      public static IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, byte[]>>> TextMacrosIndex;

      public static readonly byte DynamicEscape = 0xF7;
      public static readonly byte FunctionEscape = 0xFC;
      public static readonly byte ButtonEscape = 0xF8;
      public static readonly byte SpecialCharacterEscape = 0xF9;
      public static readonly byte Escape = 0xFD;

      static PCSString() {
         if (File.Exists(TextReferenceFileName)) {
            var referenceText = File.ReadAllLines(TextReferenceFileName);
            PCS = GetPCSFromReference(referenceText);
            ControlCodeLengths = GetControlCodeLengthsFromReference(referenceText);
            TextMacros = GetTextMacrosFromReference(referenceText);
            TextMacrosIndex = BuildTextMacrosIndex(TextMacros);
         } else {
            PCS = GetDefaultPCS();
            ControlCodeLengths = GetDefaultControlCodeLengths();
            TextMacros = new Dictionary<string, IReadOnlyDictionary<string, byte[]>>();
            TextMacrosIndex = BuildTextMacrosIndex(TextMacros);
         }

         ValidInProgressEscapes = new HashSet<string>(PCS
            .Where(c => c != null && c.StartsWith("\\"))
            .SelectMany(c => (c.Length - 1).Range(i => c.Substring(0, i + 1))));

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

      private static IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, byte[]>>> BuildTextMacrosIndex(IReadOnlyDictionary<string, IReadOnlyDictionary<string, byte[]>> allMacros) {
         var allIndex = new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, byte[]>>>();
         foreach (var macroKvp in allMacros) {
            var index = new IReadOnlyDictionary<string, byte[]>[256];
            foreach(var macro in macroKvp.Value) {
               var (text, bytes) = macro;
               if (bytes.Length < 1) continue;
               var lead = bytes[0];
               if (index[lead] == null) index[lead] = new Dictionary<string, byte[]>();
               ((Dictionary<string, byte[]>)index[lead])[text] = bytes;
            }
            allIndex[macroKvp.Key] = index;
         }
         return allIndex;
      }

      private static IReadOnlyDictionary<string, byte[]> GetTextMacrosFromReference(string[] reference, string gameCode) {
         bool isInMacroSection = true;
         var results = new Dictionary<string, byte[]>();

         for (int i = 0; i < reference.Length; i++) {
            if (reference[i].Trim().StartsWith("@!game(")) isInMacroSection = reference[i].Contains(gameCode);
            if (!isInMacroSection) continue;
            if (!reference[i].StartsWith("[")) continue;
            var endOfMacro = reference[i].IndexOf("]");
            var bytes = reference[i].Substring(endOfMacro + 1).Split("#")[0].Trim().ToByteArray();
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
         if (!TextMacrosIndex.TryGetValue(macroSet, out var textMacros)) textMacros = null;

         var nextExpectedNewline = NewlineMode.Wrap;

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

            // this line optimized for maximum speed. Otherwise would like to use the Newlines array.
            if (length > 1 && (currentByte == 0xFA || currentByte == 0xFB || currentByte == 0xFE)) {
               if (currentByte == 0xFB) {
                  result.AppendLine(Environment.NewLine);
                  nextExpectedNewline = NewlineMode.Wrap;
               } else if (currentByte == 0xFE && nextExpectedNewline == NewlineMode.Wrap) {
                  result.AppendLine();
                  nextExpectedNewline = NewlineMode.Feed;
               } else if (currentByte == 0xFA && nextExpectedNewline == NewlineMode.Feed) {
                  result.AppendLine();
                  nextExpectedNewline = NewlineMode.Feed;
               } else {
                  result.AppendLine(PCS[currentByte]);
                  nextExpectedNewline = NewlineMode.Feed;
               }
            } else if (PCS[currentByte] == null) {
               result.Append("\\!" + currentByte.ToHexString());
            } else {
               result.Append(PCS[currentByte]);
            }

            if (length == 1) break;

            if ((currentByte == Escape || currentByte == DynamicEscape || currentByte == ButtonEscape || currentByte == SpecialCharacterEscape) && i < length - 1) {
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

      /// <summary>
      /// If it's the first line, there's been no newline.
      /// If the user species an explicit newline, then no auto-newline is needed.
      /// If there are 2 newlines in a row, read it as a Paragraph.
      /// If the most recent newline was a paragraph, the next line should be Wrap.
      /// If the most recent newline was Wrap or Feed, the next line should be Feed.
      /// </summary>
      private enum NewlineMode { None, Explicit, Wrap, Feed, Paragraph }
      private static readonly string doubleNewline = Environment.NewLine + Environment.NewLine;

      public static List<byte> Convert(string input) => Convert(input, out var _);
      public static List<byte> Convert(string input, out bool containsBadCharacters) => Convert(input, string.Empty, out containsBadCharacters);
      public static List<byte> Convert(string input, string macroSet, out bool containsBadCharacters) {
         if (input.StartsWith("\"")) input = input.Substring(1); // trim leading " at start of string
         var result = new List<byte>();
         containsBadCharacters = false;

         var lastNewLine = NewlineMode.None;
         var nextNewline = NewlineMode.Wrap;

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

            // check raw escape code \!XX (hex byte)
            if (index < input.Length - 4 && input[index] == '\\' && input[index + 1] == '!') {
               var hex = new string(new[] { input[index + 2], input[index + 3] });
               if (hex.TryParseHex(out int value)) {
                  result.Add((byte)value);
               } else {
                  result.Add(0);
               }
               index += 4;
               continue;
            }

            // check newlines
            if (index < input.Length - doubleNewline.Length) {
               var isNewline = input[index..].StartsWith(Environment.NewLine);
               if (isNewline && nextNewline == NewlineMode.Explicit) {
                  // don't add anything to result
                  // lastNewline still says what the most recent newline was
                  // nextNewline will get reset after the next character
               } else if (isNewline) {
                  if (input[index..].StartsWith(doubleNewline)) {
                     (lastNewLine, nextNewline) = (NewlineMode.Paragraph, NewlineMode.Wrap);
                     result.Add(0xFB); // paragraph feed
                     index += doubleNewline.Length; ;
                     continue;
                  } else if (nextNewline == NewlineMode.Wrap) {
                     (lastNewLine, nextNewline) = (NewlineMode.Wrap, NewlineMode.Feed);
                     result.Add(0xFE); // wrap
                     index += Environment.NewLine.Length;
                     continue;
                  } else {
                     (lastNewLine, nextNewline) = (NewlineMode.Feed, NewlineMode.Feed);
                     result.Add(0xFA); // feed
                     index += Environment.NewLine.Length;
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
               if (checkCharacter.Length > 0 && checkCharacter[0] == '\\' && !ValidInProgressEscapes.Contains(checkInput)) {
                  // escape sequences don't care about case (if no sequences seem to match the current case)
                  checkCharacter = checkCharacter.ToUpper();
                  checkInput = checkInput.ToUpper();
               }
               if (checkInput != checkCharacter) continue;
               if (i != 0xFF || index == input.Length - 1) result.Add((byte)i); // don't allow adding " to the middle
               index += PCS[i].Length - 1;
               if ((i == Escape || i == DynamicEscape || i == ButtonEscape || i == SpecialCharacterEscape) && input.Length > index + 2) {
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

               if (i == 0xFA || i == 0xFB || i == 0xFE) {
                  // explicit newline
                  nextNewline = NewlineMode.Explicit;
                  lastNewLine = i switch {
                     0xFA => NewlineMode.Feed,
                     0xFB => NewlineMode.Wrap,
                     0xFE => NewlineMode.Paragraph,
                     _ => default
                  };
               } else if (nextNewline == NewlineMode.Explicit) {
                  // refresh implicit newline
                  nextNewline = lastNewLine == NewlineMode.Paragraph ? NewlineMode.Wrap : NewlineMode.Feed;
               }

               foundMatch = true;
               break;
            }
            containsBadCharacters |= !foundMatch;

            index++; // always increment by one, even if the character was not found.
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
            else if (data[start + length] == SpecialCharacterEscape) length++;
            else if (data[start + length] == FunctionEscape) {
               length += GetLengthForControlCode(data[start + length + 1]);
            }
            length++;
         }
         return -1;
      }

      private const int DefaultCharacterWidth = 6;
      public static IReadOnlyList<TextSegment> GetOverflow(IReadOnlyDictionary<string, byte[]> textMacros, int[] fontWidth, ReadOnlySpan<char> input, int maxWidth) {
         var results = new List<TextSegment>();
         int currentLineWidth = 0;
         var length = input.Length;
         if (input.StartsWith("\"")) input = input[1..]; // trim leading " at start of string
         var lineNumber = 0;
         var lineStart = 0;

         int index = 0;
         while (index < input.Length) {
            int initialIndex = index;

            // check macros
            if (input[index] == '[' && textMacros != null) {
               var closeMacro = input[index..].IndexOf(']') + index;
               if (closeMacro > index) {
                  var candidate = input.Slice(index, closeMacro + 1 - index).ToString();
                  if (textMacros.TryGetValue(candidate, out var bytes)) {
                     index += candidate.Length;
                     if (candidate.IsAny("[player]", "[rival]", "[buffer1]", "[buffer2]", "[buffer3]")) {
                        // guess that the character name is 9 characters long
                        currentLineWidth += DefaultCharacterWidth * 9;
                     } else {
                        // most macros don't actually make the text longer
                     }

                     continue;
                  }
               }
            }

            // check raw escape code \!XX (hex byte)
            if (index < input.Length - 4 && input[index] == '\\' && input[index + 1] == '!') {
               var hex = new string(new[] { input[index + 2], input[index + 3] });

               // raw hex, unknown length, just guess
               currentLineWidth += DefaultCharacterWidth;
               index += 4;

               continue;
            }

            // check newline
            var isNewline = input[index..].StartsWith(Environment.NewLine);
            if (isNewline) {
               currentLineWidth = 0;
               lineNumber++;
               index += Environment.NewLine.Length;
               lineStart = index;
               continue;
            }

            // check characters and escape codes
            for (int i = 0; i < 0x100; i++) {
               if (PCS[i] == null) continue;
               var checkCharacter = PCS[i];
               if (input.Length < index + checkCharacter.Length) continue;
               var checkInput = input.Slice(index, checkCharacter.Length).ToString();
               if (checkCharacter.Length > 0 && checkCharacter[0] == '\\' && !ValidInProgressEscapes.Contains(checkInput)) {
                  // escape sequences don't care about case (if no sequences seem to match the current case)
                  checkCharacter = checkCharacter.ToUpper();
                  checkInput = checkInput.ToUpper();
               }
               if (checkInput != checkCharacter) continue;

               index += PCS[i].Length - 1;
               if (i == 0xFA || i == 0xFB || i == 0xFE) {
                  // line end
                  currentLineWidth = 0;
               } else if ((i == Escape || i == DynamicEscape || i == ButtonEscape || i == SpecialCharacterEscape) && input.Length > index + 2) {
                  if (byte.TryParse(input.Slice(index + 1, 2), NumberStyles.HexNumber, CultureInfo.CurrentCulture, out byte parsed)) {
                     currentLineWidth += 6;
                  }
                  index += 2;
               } else if (i == FunctionEscape && input.Length > index + 2) {
                  // function escape code don't add any length to the current line
                  if (byte.TryParse(input.Slice(index + 1, 2), NumberStyles.HexNumber, CultureInfo.CurrentCulture, out byte parsed)) {
                     var extraEscapedBytesCount = GetLengthForControlCode(parsed);
                     for (int j = 1; j < extraEscapedBytesCount; j++) {
                        index += 2;
                     }
                  }
                  index += 2;
               } else if (fontWidth.Length > i) {
                  currentLineWidth += fontWidth[i];
               } else {
                  // unknown character
                  currentLineWidth += 6;
               }

               break;
            }

            index++; // always increment by one, even if the character was not found. This lets us skip past newlines and such.
            if (currentLineWidth > maxWidth) {
               var start = initialIndex - lineStart;
               if (results.Count > 0 && results[^1].Line == lineNumber) {
                  start = results[^1].Start;
                  results.RemoveAt(results.Count - 1);
               }
               results.Add(new(lineNumber, start, index - lineStart - start, SegmentType.Warning));
            }
         }

         return results;
      }

      private static string FindMacro(IReadOnlyList<IReadOnlyDictionary<string, byte[]>> macrosIndex, IReadOnlyList<byte> data, int index) {
         var macros = macrosIndex[data[index]];
         if (macros == null) return null;
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
         if (data[index - 1].IsAny(Escape, DynamicEscape, ButtonEscape, SpecialCharacterEscape, FunctionEscape)) return true;
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
