using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HavenSoft.HexManiac.Core.Models.Code {
   public class ThumbParser {
      private readonly List<ConditionCode> conditionalCodes = new List<ConditionCode>();
      private readonly List<Instruction> instructionTemplates = new List<Instruction>(); 
      public ThumbParser(string[] engineLines) {
         foreach(var line in engineLines) {
            if (ConditionCode.TryLoadConditionCode(line, out var condition)) conditionalCodes.Add(condition);
            else if (Instruction.TryLoadInstruction(line, out var instruction)) instructionTemplates.Add(instruction);
         }
      }

      private StringBuilder parseResult = new StringBuilder();
      private List<string> parsedLines = new List<string>();
      public string Parse(IReadOnlyList<byte> data, int start, int length) {
         parseResult.Clear();
         parsedLines.Clear();
         int initialStart = start;
         var interestingAddresses = new HashSet<int> { start };

         // part 1: convert all the instructions and find all interesting addresses
         while (length >= 2) {
            var compiledCode = Instruction.Convert(data, start);
            var template = instructionTemplates.FirstOrDefault(instruction => instruction.Matches(compiledCode));
            if (template == null) {
               parsedLines.Add(compiledCode.ToString("X4"));
            } else {
               var line = template.Disassemble(start, compiledCode, conditionalCodes);
               parsedLines.Add(line);
               if (line.Contains("<") && line.Contains(">")) {
                  var address = int.Parse(line.Split('<')[1].Split('>')[0], System.Globalization.NumberStyles.HexNumber);
                  interestingAddresses.Add(address);
               }
            }
            length -= 2;
            start += 2;
         }

         // part 2: insert all interesting addresses
         foreach (var address in interestingAddresses.OrderByDescending(i => i)) {
            var index = (address - initialStart) / 2;
            if (index >= parsedLines.Count) continue;
            parsedLines.Insert(index, address.ToString("X6") + ":");
         }

         // part 3: aggregate / return
         foreach (var line in parsedLines) parseResult.AppendLine(line);
         return parseResult.ToString();
      }

      public IReadOnlyList<byte> Compile(string[] lines) {
         var result = new List<byte>();
         foreach (var line in lines) {
            foreach (var instruction in instructionTemplates) {
               if (!instruction.TryAssemble(line, conditionalCodes, out ushort code)) continue;
               result.Add((byte)code);
               result.Add((byte)(code >> 8));
               break;
            }
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

   [System.Diagnostics.DebuggerDisplay("{template}")]
   public class Instruction {
      private readonly List<InstructionPart> instructionParts = new List<InstructionPart>();
      private readonly string template;

      #region Constructor

      private Instruction(string compiled, string script) {
         var parts = compiled.ToLower().Split(' ');
         foreach (var part in parts) {
            if (part.StartsWith("0") || part.StartsWith("1")) {
               var code = ToBits(part);
               instructionParts.Add(new InstructionPart(InstructionArgType.OpCode, code, part.Length));
            } else if (part.StartsWith("r")) {
               instructionParts.Add(new InstructionPart(InstructionArgType.Register, 0, 3, part));
            } else if (part.StartsWith("#")) {
               ushort code = 0;
               if (script.Contains("#=pc+#*")) {
                  var encoding = script.Split("#=pc+#*")[1].Split('+');
                  code = byte.TryParse(encoding[0], out var mult) ? mult : default;
                  code <<= 8;
                  code |= byte.TryParse(encoding[1], out var add) ? add : default;
               }
               var iPart = new InstructionPart(InstructionArgType.Numeric, code, 0);
               instructionParts.Add(iPart);
            } else if (part == "h") {
               instructionParts.Add(new InstructionPart(InstructionArgType.HighRegister, 0, 1));
            } else if (part == "list") {
               instructionParts.Add(new InstructionPart(InstructionArgType.List, 0, 8));
            } else if (part == "cond") {
               instructionParts.Add(new InstructionPart(InstructionArgType.Condition, 0, 4));
            }
         }

         var totalLength = instructionParts.Sum(part => part.Length);
         var remainingLength = 16 - totalLength;
         for (int i = 0; i < instructionParts.Count; i++) {
            if (instructionParts[i].Type != InstructionArgType.Numeric) continue;
            instructionParts[i] = new InstructionPart(InstructionArgType.Numeric, instructionParts[i].Code, remainingLength);
         }

         template = script.ToLower();
      }

      public static bool TryLoadInstruction(string line, out Instruction instruction) {
         instruction = null;
         line = line.Split('@')[0].Trim();
         var parts = line.Split('|');
         if (parts.Length != 2) return false;

         instruction = new Instruction(parts[0].Trim(), parts[1].Trim());
         return true;
      }

      #endregion

      public static ushort ToBits(string bits) {
         return (ushort)bits.Aggregate(0, (a, b) => a * 2 + b - '0');
      }

      public static ushort Convert(IReadOnlyList<byte> data, int index) {
         return (ushort)((data[index + 1] << 8) + data[index]);
      }

      public static ushort GrabBits(ushort value, int start, int length) {
         value >>= start;
         var mask = (1 << length) - 1;
         value &= (ushort)mask;
         return value;
      }

      public bool Matches(ushort assembled) {
         var remainingBits = 16;
         foreach (var part in instructionParts) {
            remainingBits -= part.Length;
            if (part.Type != InstructionArgType.OpCode) continue;
            var code = GrabBits(assembled, remainingBits, part.Length);
            if (code != part.Code) return false;
         }
         return true;
      }

      public string Disassemble(int pcAddress, ushort assembled, IReadOnlyList<ConditionCode> conditionCodes) {
         var instruction = template;
         var highQueue = new List<bool>();
         var remainingBits = 16;
         foreach (var part in instructionParts) {
            remainingBits -= part.Length;
            var bits = GrabBits(assembled, remainingBits, part.Length);
            if (part.Type == InstructionArgType.HighRegister) {
               highQueue.Add(bits != 0);
            } else if (part.Type == InstructionArgType.List) {
               instruction = instruction.Replace("list", ParseRegisterList(bits));
            } else if (part.Type == InstructionArgType.Condition) {
               var suffix = conditionCodes.First(code => code.Code == bits).Mnemonic;
               instruction = instruction.Replace("{cond}", suffix);
            } else if (part.Type == InstructionArgType.Numeric) {
               if (part.Code != 0) {
                  var mult = GrabBits(part.Code, 8, 8);
                  var add = GrabBits(part.Code, 0, 8);
                  var numeric = (sbyte)bits;
                  var address = pcAddress + numeric * mult + add;
                  var end = instruction.EndsWith("]") ? "]" : string.Empty;
                  instruction = instruction.Split("#=")[0] + "#" + end;
                  instruction = instruction.Replace("#", $"<{address:X6}>");
               } else {
                  instruction = instruction.Replace("#", $"#{bits}");
               }
            } else if (part.Type == InstructionArgType.Register) {
               if (highQueue.Count > 0) {
                  if (highQueue[0]) bits += 8;
                  highQueue.RemoveAt(0);
               }
               instruction = instruction.Replace(part.Name, "r" + bits);
            }
         }
         return "    " + instruction;
      }

      public bool TryAssemble(string line, IReadOnlyList<ConditionCode> conditionCodes, out ushort result) {
         line = line.ToLower();
         result = 0;
         var thisTemplate = template;

         // setup ConditionCode if there is one
         ConditionCode ccode = null;
         if (thisTemplate.Contains("{cond}")) {
            var condIndex = thisTemplate.IndexOf("{cond}");
            if (thisTemplate.Substring(0, condIndex) != line.Substring(0, condIndex)) return false;
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
         line = line.Substring(commandToken.Length);
         thisTemplate = thisTemplate.Substring(commandToken.Length);

         var registersValues = new SortedList<int, int>();
         if (!MatchLinePartsToTemplateParts(line, thisTemplate, registersValues, out var numeric, out var list)) return false;

         var remainingBits = 16;
         var registerListForHighCheck = registersValues.ToList();
         var registerListForRegisters = registersValues.ToList();
         foreach (var part in instructionParts) {
            remainingBits -= part.Length;
            result <<= part.Length;
            if (part.Type == InstructionArgType.OpCode) {
               result |= part.Code;
            } else if (part.Type == InstructionArgType.Condition) {
               result |= ccode.Code;
            } else if (part.Type == InstructionArgType.HighRegister) {
               if (registerListForHighCheck[0].Value > 7) result |= 1;
               registerListForHighCheck.RemoveAt(0);
            } else if (part.Type == InstructionArgType.Numeric) {
               var mask = (1 << part.Length) - 1;
               result |= (ushort)(numeric & mask);
            } else if (part.Type == InstructionArgType.Register) {
               result |= (ushort)(registerListForRegisters[0].Value & 7);
               registerListForRegisters.RemoveAt(0);
            } else if (part.Type == InstructionArgType.List) {
               result |= list;
            }
         }
         return true;
      }

      private bool MatchLinePartsToTemplateParts(string line, string template, SortedList<int, int> registerValues, out int numeric, out ushort list) {
         numeric = 0;
         list = 0;
         while (line.Length > 0) {
            // make sure that the basic format matches where it should
            if (template[0] == ',') {
               if (line[0] != ',') return false;
               template = template.Substring(1);
               line = line.Substring(0);
               continue;
            }
            if (template[0] == '[') {
               if (line[0] != '[') return false;
               template = template.Substring(1);
               line = line.Substring(0);
               continue;
            }
            if (template[0] == ']') {
               if (line[0] != ']') return false;
               template = template.Substring(1);
               line = line.Substring(0);
               continue;
            }
            if (template[0] == ' ') {
               template = template.Substring(1);
               continue;
            }
            if (line[0] == ' ') {
               line = line.Substring(1);
               continue;
            }

            // read a register
            if (template[0] == 'r') {
               if (line[0] != 'r') return false;
               var name = "r" + template[1];
               var instruction = instructionParts.Single(i => i.Name == name);
               var index = instructionParts.IndexOf(instruction);
               if (int.TryParse(line.Substring(1), out int value)) {
                  registerValues[index] = value;
               }
               template = template.Substring(2);
               line = line.Substring(("r" + value).Length);
               continue;
            }

            // read a number
            if (template[0] == '#') {
               if (line[0] != '#') return false;
               if (!int.TryParse(line.Substring(1), out numeric)) return false;
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
            }

            // read fixed register
            if (template.Substring(2) == line.Substring(2)) {
               template = template.Substring(2);
               line = line.Substring(2);
               continue;
            }

            // fail
            return false;
         }

         return true;
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
}
