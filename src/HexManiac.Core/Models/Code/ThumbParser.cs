using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HavenSoft.HexManiac.Core.Models.Code {
   public class LabelLibrary {
      private readonly IDataModel model;
      private readonly IDictionary<string, int> labels;
      public LabelLibrary(IDataModel data, IDictionary<string, int> additionalLabels) => (model, labels) = (data, additionalLabels);
      public int ResolveLabel(string label) {
         if (labels.TryGetValue(label, out int result)) return result;
         return model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, label);
      }
   }

   public class ThumbParser {
      private readonly List<ConditionCode> conditionalCodes = new List<ConditionCode>();
      private readonly List<IInstruction> instructionTemplates = new List<IInstruction>();
      public ThumbParser(Singletons singletons) {
         conditionalCodes.AddRange(singletons.ThumbConditionalCodes);
         instructionTemplates.AddRange(singletons.ThumbInstructionTemplates);
         instructionTemplates.Add(new WordInstruction());
      }

      private readonly StringBuilder parseResult = new StringBuilder();
      private readonly List<string> parsedLines = new List<string>();
      public string Parse(IDataModel data, int start, int length) {
         if (data.Count < start + length) return string.Empty;
         parseResult.Clear();
         parsedLines.Clear();
         int initialStart = start;
         var interestingAddresses = new HashSet<int> { start };
         var wordLocations = new HashSet<int>();
         var sectionEndLocations = new HashSet<int>();

         // part 1: convert all the instructions and find all interesting addresses
         while (length >= 2) {
            var template = instructionTemplates.FirstOrDefault(instruction => instruction.Matches(data, start));
            if (template == null) {
               parsedLines.Add(data.ReadMultiByteValue(start, 2).ToString("X4"));
               length -= 2;
               start += 2;
            } else {
               var line = template.Disassemble(data, start, conditionalCodes);
               parsedLines.AddRange(line.Split(Environment.NewLine));
               var tokens = line.ToLower().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
               if (tokens.Length > 0 && (tokens[0] == "b" || tokens[0] == "bx")) {
                  sectionEndLocations.Add(start);
               }
               if (tokens.Length > 1 && tokens[0] == "pop" && tokens[1] == "pc,") {
                  sectionEndLocations.Add(start);
               }
               if (tokens.Length > 1 && tokens[0] == "push" && tokens[1] == "lr,") {
                  interestingAddresses.Add(start); // push lr always signifies the start of a function. That makes it worth noting.
               }
               if (line.Contains("<") && line.Contains(">")) {
                  var address = int.Parse(line.Split('<')[1].Split('>')[0], NumberStyles.HexNumber);
                  interestingAddresses.Add(address);
                  if (tokens.Length > 1 && tokens[0] == "ldr" && tokens[1].StartsWith("r")) {
                     wordLocations.Add(address);
                  }
               }
               length -= template.ByteLength;
               start += template.ByteLength;
            }
         }

         // part 2: insert all interesting addresses
         for (int address = Math.Min(interestingAddresses.Concat(sectionEndLocations).Max(), initialStart + parsedLines.Count * 2 - 2); address >= initialStart; address -= 2) {
            var index = (address - initialStart) / 2;

            // check if it's a word
            if (wordLocations.Contains(address)) {
               parsedLines[index] = $"    .word {data.ReadValue(address):X8}";
               if (index < parsedLines.Count - 1) parsedLines.RemoveAt(index + 1);
               // remove anything in this area after the word until the next address of interest (denoted by starting with no spaces)
               while (index < parsedLines.Count - 1 && parsedLines[index + 1].StartsWith(" ")) parsedLines.RemoveAt(index + 1);
            }

            // check if it's the end of a section
            if (sectionEndLocations.Contains(address)) {
               // remove any code in this area until the next address of interest (denoted by starting with no spaces)
               while (index < parsedLines.Count - 1 && parsedLines[index + 1].StartsWith(" ")) parsedLines.RemoveAt(index + 1);
            }

            // check if it's a blank line
            if (string.IsNullOrEmpty(parsedLines[index])) parsedLines.RemoveAt(index);

            if (interestingAddresses.Contains(address)) {
               parsedLines.Insert(index, address.ToString("X6") + ":");
            }
         }

         // part 3: aggregate / return
         foreach (var line in parsedLines) parseResult.AppendLine(line);
         return parseResult.ToString();
      }

      private static readonly IReadOnlyCollection<byte> nop = new byte[] { 0, 0 };
      public IReadOnlyList<byte> Compile(IDataModel model, int start, params string[] lines) {
         var result = new List<byte>();
         lines = lines.Select(line => line.ToLower().Split('@')[0].Trim()).ToArray();

         // first pass: look for labels
         var labels = new Dictionary<string, int>();
         int position = start;
         foreach (var line in lines) {
            if (line == string.Empty) continue;
            if (line.EndsWith(":")) {
               var label = line.Substring(0, line.Length - 1);
               if (!labels.ContainsKey(label)) labels.Add(label, position);
            } else {
               position += 2;
               if (line.StartsWith("bl ")) position += 2;   // branch-links are double-wide
               if (line.StartsWith(".word")) position += 2; // .word elements are double-wide
               if (line.StartsWith(".word") && position % 4 != 0) {
                  // alignment is off! words have to be 4-byte aligned.
                  position += 2;
                  var labelToFix = labels.Keys.SingleOrDefault(key => labels[key] == position - 6);
                  if (labelToFix != null) labels[labelToFix] = position - 4;
               }
            }
         }

         var labelLibrary = new LabelLibrary(model, labels);

         foreach (var line in lines) {
            if (line == string.Empty) continue; // don't compile blank lines
            if (line.EndsWith(":")) continue;   // don't compile labels
            bool foundMatch = false;
            foreach (var instruction in instructionTemplates) {
               if (!instruction.TryAssemble(line, conditionalCodes, start + result.Count, labelLibrary, out byte[] code)) continue;
               if (instruction.RequiresAlignment && result.Count % 4 != 0) result.AddRange(nop);
               result.AddRange(code);
               foundMatch = true;
               break;
            }
            if (!foundMatch) result.AddRange(nop);
         }
         return result;
      }
   }

   public class ConditionCode {
      public byte Code { get; }       // 4 bits long
      public string Mnemonic { get; } // 2 characters long

      #region Constructor

      private ConditionCode(string mnemonic, string bits) {
         Mnemonic = mnemonic;
         if (bits[0] == '1') Code += 0x8;
         if (bits[1] == '1') Code += 0x4;
         if (bits[2] == '1') Code += 0x2;
         if (bits[3] == '1') Code += 0x1;
      }

      public static bool TryLoadConditionCode(string line, out ConditionCode ccode) {
         ccode = null;
         line = line.Split('@')[0].Trim();
         var parts = line.Split('=');
         if (parts.Length != 2) return false;
         parts[0] = parts[0].Trim();
         if (parts[0].Length != 2) return false;
         parts[1] = parts[1].Trim();
         if (parts[1].Length != 4) return false;

         ccode = new ConditionCode(parts[0], parts[1]);
         return true;
      }

      #endregion
   }

   public enum InstructionArgType {
      OpCode,    // code is the bits for the opcode. They must match exactly.
      Register,
      Numeric,   // if code is non-zero, the high 8 bits is a multiplier and the low 8 bits is an addition offset.
                 // then add that whole thing to the current pc offset and display that
      HighRegister,
      List,
      ReverseList,  // used for push
      Condition,
   }

   public struct InstructionPart {
      public InstructionArgType Type { get; }
      public ushort Code { get; }
      public int Length { get; }
      public string Name { get; }

      public InstructionPart(InstructionArgType type, ushort code, int length, string name = "") {
         Type = type;
         Code = code;
         Length = length;
         Name = name;
      }
   }

   public interface IInstruction {
      int ByteLength { get; }
      bool RequiresAlignment { get; }
      bool Matches(IDataModel data, int index);
      string Disassemble(IDataModel data, int address, IReadOnlyList<ConditionCode> conditionalCodes);
      bool TryAssemble(string line, IReadOnlyList<ConditionCode> conditionCodes, int address, LabelLibrary labels, out byte[] results);
   }

   [System.Diagnostics.DebuggerDisplay("{template}")]
   public class Instruction : IInstruction {
      private readonly List<InstructionPart> instructionParts = new List<InstructionPart>();
      private readonly string template;

      public int ByteLength { get; } = 2;
      public bool RequiresAlignment => false;

      #region Constructor

      private Instruction(string compiled, string script) {
         var parts = compiled.ToLower().Split(' ');
         foreach (var part in parts) {
            if (part.StartsWith("0") || part.StartsWith("1")) {
               var code = ToBits(part);
               instructionParts.Add(new InstructionPart(InstructionArgType.OpCode, code, part.Length));
            } else if (part == "lr") {
               instructionParts.Add(new InstructionPart(InstructionArgType.Register, 0, 3, part));
            } else if (part.StartsWith("r")) {
               instructionParts.Add(new InstructionPart(InstructionArgType.Register, 0, 3, part));
            } else if (part.StartsWith("#")) {
               ushort code = 0;
               if (script.Contains("#=pc+#*")) {
                  // Code contains 2 8-bit sections: the number to multiply by, followed by the number to add.
                  // Note that the number to multiply by also gives us a number to mod by.
                  var encoding = script.Split("#=pc+#*")[1].Split('+');
                  code = byte.TryParse(encoding[0], out var mult) ? mult : default;
                  code <<= 8;
                  code |= byte.TryParse(encoding[1].Trim(']'), out var add) ? add : default;
               }
               int length = 0;
               if (part.Length > 1) int.TryParse(part.Substring(1), out length);
               instructionParts.Add(new InstructionPart(InstructionArgType.Numeric, code, length));
            } else if (part == "h") {
               instructionParts.Add(new InstructionPart(InstructionArgType.HighRegister, 0, 1));
            } else if (part == "list") {
               instructionParts.Add(new InstructionPart(InstructionArgType.List, 0, 8));
            } else if (part == "tsil") {
               instructionParts.Add(new InstructionPart(InstructionArgType.ReverseList, 0, 8));
            } else if (part == "cond") {
               instructionParts.Add(new InstructionPart(InstructionArgType.Condition, 0, 4));
            }
         }

         var totalLength = instructionParts.Sum(part => part.Length);
         if (totalLength > 16) ByteLength = totalLength / 8;
         var remainingLength = ByteLength * 8 - totalLength;
         for (int i = 0; i < instructionParts.Count && remainingLength > 0; i++) {
            if (instructionParts[i].Type != InstructionArgType.Numeric) continue;
            instructionParts[i] = new InstructionPart(InstructionArgType.Numeric, instructionParts[i].Code, remainingLength);
            totalLength += remainingLength;
         }

         if (totalLength % 16 != 0) throw new ArgumentException($"There were {totalLength} bits in the command, but commands must be a multiple of 16 bits long!");

         template = script.ToLower();
         if (template.StartsWith("bl ")) ByteLength = 4;
      }

      public static bool TryLoadInstruction(string line, out Instruction instruction) {
         instruction = null;
         line = line.Split('@')[0].Trim();
         var parts = line.Split('|');
         if (parts.Length != 2) return false;

         try {
            instruction = new Instruction(parts[0].Trim(), parts[1].Trim());
         } catch (ArgumentException) {
            Debugger.Break();
            return false;
         }

         return true;
      }

      #endregion

      public static ushort ToBits(string bits) {
         return (ushort)bits.Aggregate(0, (a, b) => a * 2 + b - '0');
      }

      public static ushort GrabBits(uint value, int start, int length) {
         value >>= start;
         var mask = (1 << length) - 1;
         value &= (ushort)mask;
         return (ushort)value;
      }

      public override string ToString() => template;

      public bool Matches(IDataModel data, int index) {
         if (data.Count < index + ByteLength) return false;
         var remainingBits = ByteLength * 8;
         var assembled = (uint)data.ReadMultiByteValue(index, ByteLength);
         foreach (var part in instructionParts) {
            remainingBits -= part.Length;
            if (part.Type != InstructionArgType.OpCode) continue;
            var code = GrabBits(assembled, remainingBits, part.Length);
            if (code != part.Code) return false;
         }
         return true;
      }

      public string Disassemble(IDataModel data, int pcAddress, IReadOnlyList<ConditionCode> conditionCodes) {
         var instruction = template;
         var assembled = (uint)data.ReadMultiByteValue(pcAddress, ByteLength);
         var highStack = new List<bool>();
         var remainingBits = ByteLength * 8;
         foreach (var part in instructionParts) {
            remainingBits -= part.Length;
            var bits = GrabBits(assembled, remainingBits, part.Length);
            if (part.Type == InstructionArgType.HighRegister) {
               highStack.Add(bits != 0);
            } else if (part.Type == InstructionArgType.List) {
               instruction = instruction.Replace("list", ParseRegisterList(bits));
            } else if (part.Type == InstructionArgType.ReverseList) {
               instruction = instruction.Replace("tsil", ParseRegisterReverseList(bits));
            } else if (part.Type == InstructionArgType.Condition) {
               var suffix = conditionCodes.First(code => code.Code == bits).Mnemonic;
               instruction = instruction.Replace("{cond}", suffix);
            } else if (part.Type == InstructionArgType.Numeric) {
               if (part.Code != 0) {
                  instruction = CalculatePcRelativeAddress(instruction, pcAddress, part, bits);
               } else {
                  instruction = instruction.Replace("#", $"#{bits}");
               }
            } else if (part.Type == InstructionArgType.Register) {
               if (highStack.Count > 0) {
                  if (highStack.Last()) bits += 8;
                  highStack.RemoveAt(highStack.Count - 1);
               }
               instruction = instruction.Replace(part.Name, "r" + bits);
            }
         }

         for (int i = 2; i < ByteLength; i += 2) instruction += Environment.NewLine;
         return "    " + instruction;
      }

      private static string CalculatePcRelativeAddress(string instruction, int pcAddress, InstructionPart part, ushort bits) {
         var mult = GrabBits(part.Code, 8, 8);
         var add = GrabBits(part.Code, 0, 8);
         var numeric = (short)bits;
         numeric <<= 16 - part.Length;
         numeric >>= 16 - part.Length; // get all the extra bits set to one so that the numeric value is correct
         if (instruction.Contains("#")) {
            // this is the first # in this instruction.
            var address = pcAddress - (pcAddress % mult) + numeric * mult + add;
            var end = instruction.EndsWith("]") ? "]" : string.Empty;
            instruction = instruction.Split("#=")[0] + "#" + end;
            instruction = instruction.Replace("#", $"<{address:X6}>");
         } else {
            // this is an additional # in the same instruction.
            // decode back from the old one
            var address = int.Parse(instruction.Split('<')[1].Split('>')[0], NumberStyles.HexNumber);
            address -= pcAddress - (pcAddress % mult) + add;
            address /= mult;
            address = (address & ((1 << part.Length) - 1)); // drop the high bits, keep only the data bits. This makes it lose the sign.
            // concat the new numeric
            address += bits << part.Length;   // the new numeric is the higher bits
            // shift to get arithmetic sign bits
            address <<= 32 - part.Length * 2;
            address >>= 32 - part.Length * 2;   // since address is a signed int, C# right-shift will carry 1-bits down if the high bit is set. This is what we want to happen.
            // encode again
            address *= mult;
            address += pcAddress - (pcAddress % mult) + add;
            instruction = instruction.Split('<')[0] + $"<{address:X6}>" + (instruction + " ").Split('>')[1].Trim(); // extra space / trim let's us get everything after the '>', even if it's empty
         }

         return instruction;
      }

      public bool TryAssemble(string line, IReadOnlyList<ConditionCode> conditionCodes, int codeLocation, LabelLibrary labels, out byte[] results) {
         line = line.ToLower();
         uint result = 0;
         results = new byte[ByteLength];
         var thisTemplate = template;

         // setup ConditionCode if there is one
         ConditionCode ccode = null;
         if (thisTemplate.Contains("{cond}")) {
            var condIndex = thisTemplate.IndexOf("{cond}");
            if (thisTemplate.Substring(0, condIndex) != line.Substring(0, condIndex)) return false;
            if (condIndex + 2 > line.Length) return false;
            var condition = line.Substring(condIndex, 2);
            ccode = conditionCodes.FirstOrDefault(code => code.Mnemonic == condition);
            if (ccode == null) return false;
            var start = thisTemplate.Substring(0, condIndex + 6);
            var newStart = line.Substring(0, condIndex + 2);
            thisTemplate = thisTemplate.Replace(start, newStart);
         }

         // check that the command matches
         var commandToken = line.Split(' ')[0] + " ";
         if (!thisTemplate.StartsWith(commandToken)) return false;
         if (commandToken.Length > line.Length) return false;
         line = line.Substring(commandToken.Length);
         thisTemplate = thisTemplate.Substring(commandToken.Length);

         var registersValues = new SortedList<int, int>();
         if (!MatchLinePartsToTemplateParts(line, thisTemplate, registersValues, labels, out var numeric, out var list)) return false;

         var remainingBits = ByteLength * 8;
         var registerListForHighCheck = registersValues.ToList();
         var registerListForRegisters = registersValues.ToList();
         bool firstNumeric = true;
         foreach (var part in instructionParts) {
            remainingBits -= part.Length;
            result <<= part.Length;
            if (part.Type == InstructionArgType.OpCode) {
               result |= part.Code;
            } else if (part.Type == InstructionArgType.Condition) {
               result |= ccode.Code;
            } else if (part.Type == InstructionArgType.HighRegister) {
               if (registerListForHighCheck.Last().Value > 7) result |= 1;
               registerListForHighCheck.RemoveAt(registerListForHighCheck.Count - 1);
            } else if (part.Type == InstructionArgType.Numeric) {
               if (part.Code != 0 && firstNumeric) {
                  var mult = (byte)(part.Code >> 8);
                  var add = (byte)part.Code;
                  numeric -= codeLocation - codeLocation % mult;  // offset based on code starting point
                  numeric -= add;                                 // offset from bias
                  numeric /= mult;
                  firstNumeric = false;
               }
               var mask = (1 << part.Length) - 1;
               result |= (ushort)(numeric & mask);
               numeric >>= part.Length;
            } else if (part.Type == InstructionArgType.Register) {
               result |= (ushort)(registerListForRegisters[0].Value & 7);
               registerListForRegisters.RemoveAt(0);
            } else if (part.Type == InstructionArgType.List) {
               result |= list;
            }
         }

         results[0] = (byte)result;
         results[1] = (byte)(result >> 8);
         if (results.Length == 4) {
            results[2] = (byte)(result >> 16);
            results[3] = (byte)(result >> 24);
         }
         return true;
      }

      private bool MatchLinePartsToTemplateParts(string line, string template, SortedList<int, int> registerValues, LabelLibrary labels, out int numeric, out ushort list) {
         numeric = 0;
         list = 0;
         while (line.Length > 0 && template.Length > 0) {
            // make sure that the basic format matches where it should
            if (template[0] == ' ') {
               template = template.Substring(1);
               continue;
            }
            if (line[0] == ' ') {
               line = line.Substring(1);
               continue;
            }
            if (template[0] == ',') {
               if (line[0] != ',') return false;
               template = template.Substring(1);
               line = line.Substring(1);
               continue;
            }
            if (template[0] == '[') {
               if (line[0] != '[') return false;
               template = template.Substring(1);
               line = line.Substring(1);
               continue;
            }
            if (template[0] == ']') {
               if (line[0] != ']') return false;
               template = template.Substring(1);
               line = line.Substring(1);
               continue;
            }

            // read a register
            if (template[0] == 'r') {
               if (line.StartsWith("lr")) line = "r14" + line.Substring(2);
               if (line.StartsWith("pc")) line = "r15" + line.Substring(2);
               if (line[0] != 'r') return false;
               var name = "r" + template[1];
               var instruction = instructionParts.Single(i => i.Name == name);
               var index = instructionParts.IndexOf(instruction);
               if (int.TryParse(line.Split(',', ']')[0].Substring(1), out int value)) {
                  registerValues[index] = value;
                  if (value > 7 && !instructionParts.Any(part => part.Type == InstructionArgType.HighRegister)) return false;
               }
               template = template.Substring(2);
               var register = "r" + value;
               if (register.Length > line.Length) return false;
               line = line.Substring(("r" + value).Length);
               continue;
            }

            // read a pointer
            if (template.StartsWith("#=pc")) {
               if (line[0] != '<') return false;
               if (!line.Contains('>')) return false;
               var content = line.Substring(1).Split('>')[0];
               numeric = labels.ResolveLabel(content);
               if (numeric == Pointer.NULL && !int.TryParse(content, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out numeric)) return false;
               if (line.Length < content.Length + 2) return false;
               line = line.Substring(content.Length + 2);
               template = template.Substring(template.IndexOf('+') + 1);
               template = template.Substring(template.IndexOf('+') + 2);
               continue;
            }

            // read a number
            if (template[0] == '#') {
               if (line[0] != '#') return false;
               if (!int.TryParse(line.Split(',', ']')[0].Substring(1), out numeric)) return false;
               template = template.Substring(1);
               line = line.Substring(("#" + numeric).Length);
               continue;
            }

            // read list
            if (template[0] == '{') {
               if (line[0] != '{') return false;
               var listEnd = line.IndexOf('}');
               if (listEnd == -1) return false;
               list = ParseList(line.Substring(1, listEnd - 1));
               line = line.Substring(listEnd + 1);
               template = template.Substring(6);
               continue;
            }

            // read fixed register
            if (template.Length >= 2 && line.Length >= 2 && template.Substring(0, 2) == line.Substring(0, 2)) {
               template = template.Substring(2);
               line = line.Substring(2);
               continue;
            }

            // fail
            return false;
         }

         // Completed parsing the line. Should've used the entire template.
         return template.Length == 0 && line.Length == 0;
      }

      private static ushort ParseList(string list) {
         ushort result = 0;
         int start = 0;
         while (list.Length > start) {
            if (list[start] == ',' || list[start] == ' ') {
               start++;
               continue;
            }
            if (list.Length > start + 4 && list[start + 2] == '-') {
               var subStart = list[start + 1] - '0';
               var subEnd = list[start + 4] - '0';
               for (int i = subStart; i <= subEnd; i++) result |= (ushort)(1 << i);
               start += 5;
               continue;
            }
            if (list.Length > start + 1) {
               var index = list[start + 1] - '0';
               result |= (ushort)(1 << index);
               start += 2;
               continue;
            }
            return 0;
         }
         return result;
      }

      public static string ParseRegisterList(ushort registerList) {
         var result = string.Empty;
         for (int bit = 0; bit < 8; bit++) {
            // only write if the current bit is on
            if ((registerList & (1 << bit)) == 0) continue;
            // if there's no previous bit or the previous bit is off
            if (bit == 0 || (registerList & (1 << (bit - 1))) == 0) {
               if (result.Length > 0) result += ", ";
               result += "r" + bit;
               if ((registerList & (1 << (bit + 1))) != 0) result += "-";
               continue;
            }
            // if there is no next bit or the next bit is off
            if (bit == 7 || (registerList & (1 << (bit + 1))) == 0) {
               result += "r" + bit;
               continue;
            }
         }
         return result;
      }

      public static string ParseRegisterReverseList(ushort registerList) {
         var result = string.Empty;
         for (int bit = 7; bit >= 0; bit--) {
            // only write if the current bit is on
            if ((registerList & (1 << bit)) == 0) continue;
            // if there's no previous bit or the previous bit is off
            if (bit == 7 || (registerList & (1 << (bit + 1))) == 0) {
               if (result.Length > 0) result += ", ";
               result += "r" + (7 - bit);
               if ((registerList & (1 << (bit - 1))) != 0) result += "-";
               continue;
            }
            // if there is no next bit or the next bit is off
            if (bit == 0 || (registerList & (1 << (bit - 1))) == 0) {
               result += "r" + (7 - bit);
               continue;
            }
         }
         return result;
      }
   }

   public class WordInstruction : IInstruction {
      public int ByteLength => 4;
      public bool RequiresAlignment => true;

      public string Disassemble(IDataModel data, int address, IReadOnlyList<ConditionCode> conditionalCodes) => throw new NotImplementedException();

      public bool Matches(IDataModel data, int index) => false;

      public bool TryAssemble(string line, IReadOnlyList<ConditionCode> conditionCodes, int address, LabelLibrary labels, out byte[] results) {
         line = line.Replace(".word", " ").Trim();
         int result;
         results = default;
         if (line.StartsWith("<") && line.EndsWith(">")) {
            line = line.Substring(1, line.Length - 2);
            result = labels.ResolveLabel(line);
            if (result == Pointer.NULL && !int.TryParse(line, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result)) return false;
            result -= Pointer.NULL;
         } else {
            if (!int.TryParse(line, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out result)) return false;
         }
         results = new[] {
            (byte)result,
            (byte)(result>>8),
            (byte)(result>>16),
            (byte)(result>>24),
         };
         return true;
      }
   }
}
