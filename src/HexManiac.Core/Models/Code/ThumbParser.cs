using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;

namespace HavenSoft.HexManiac.Core.Models.Code {
   public class LabelLibrary {
      private readonly IDataModel model;
      private readonly IDictionary<string, int> labels;
      public LabelLibrary(IDataModel data, IDictionary<string, int> additionalLabels) => (model, labels) = (data, additionalLabels);
      public int ResolveLabel(string label) {
         var offset = 0;
         if (label.Split("+") is string[] parts && parts.Length == 2) {
            label = parts[0];
            int.TryParse(parts[1], NumberStyles.HexNumber, CultureInfo.CurrentCulture, out offset);
         }
         if (labels.TryGetValue(label, out int result)) return result + offset;
         var address = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, label);
         if (address == Pointer.NULL) return address;
         return address + offset;
      }
      public bool TryResolveLabel(string label, out int address) {
         address = ResolveLabel(label);
         return address >= 0;
      }
      public bool TryResolveValue(string title, out int value) {
         return model.TryGetUnmappedConstant(title, out value);
      }
   }

   public class ThumbParser {
      private readonly List<ConditionCode> conditionalCodes = new List<ConditionCode>();
      private readonly List<IInstruction> instructionTemplates = new List<IInstruction>();
      public ThumbParser(Singletons singletons) {
         conditionalCodes.AddRange(singletons.ThumbConditionalCodes);
         instructionTemplates.AddRange(singletons.ThumbInstructionTemplates);
         instructionTemplates.AddRange(new IInstruction[] {
            new WordInstruction(),
            new AlignInstruction(),
            new SkipInstruction(),
         });
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
               if (tokens.Length > 1 && tokens[0] == "pop" && tokens.Last().EndsWith("pc}")) {
                  sectionEndLocations.Add(start);
               }
               if (tokens.Length > 1 && tokens[0] == "push" && tokens.Last().EndsWith("lr}")) {
                  interestingAddresses.Add(start); // push lr always signifies the start of a function. That makes it worth noting.
               }
               if (line.Contains("<") && line.Contains(">")) {
                  var content = line.Split('<')[1].Split('>')[0];
                  var address = data.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, content);
                  if (address == Pointer.NULL) address = int.Parse(content, NumberStyles.HexNumber);
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
               var wordValue = data.ReadValue(address);
               parsedLines[index] = "    .word ";
               var anchor = data.GetAnchorFromAddress(-1, wordValue - BaseModel.PointerOffset);
               if (string.IsNullOrEmpty(anchor)) {
                  parsedLines[index] += $"0x{wordValue:X8}";
               } else {
                  parsedLines[index] += $"<{anchor}>";
               }
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

      /// <returns>
      /// If a thumb repoint is possible, returns the scratch register that can be safely used for the repoint. -1 if no thumb repoint is possible.
      /// </returns>
      public int CanRepoint(IDataModel data, int start, int length) {
         if (length != 8) return -1;
         if (start % 4 != 0) return -1;
         var content = Parse(data, start, length).Trim().SplitLines().Skip(1).ToArray(); // skip the header line
         if (content.Length != 4) return -1;

         // can't repoint if the included instructions care about other nearby addresses (load-pc and branch)
         if (content.Any(line => "bl b beq bne bhs blo bcs bcc bmi bpl bvs bvc bhi bls bge blt bgt ble bal bnv".Split(' ').Any(token => line.Contains($" {token} ")))) return -1;
         if (content.Any(line => line.Contains(" ldr ") && line.Contains(" [pc, "))) return -1;

         // can't repoint if some instructions were not decoded
         if (content.Any(line => line.Length == 4 && line.All(ViewPort.AllHexCharacters.Contains))) return -1;

         // verify that content contains a `mov` instruction
         for (int i = 0; i < 4; i++) {
            if (content[i].Contains("mov ") && content[i].Contains(", #")) {
               // verify that the register isn't used by an earlier instruction
               var register = content[i].Trim().Substring(4).Split(",")[0].Trim();
               if (content.Take(i).Any(line => line.Contains(register))) continue;
               if (int.TryParse(register.Substring(1), out int r)) return r;
            }
         }

         return -1;
      }

      public int Repoint(ModelDelta token, IDataModel model, int start, int register) {
         var scratchRegister = "r" + register;
         var existingCode = new byte[8];
         Array.Copy(model.RawData, start, existingCode, 0, 8);
         var newAddress = model.FindFreeSpace(0, 20);

         // TODO add optional push if there's no scratch register
         Compile(token, model, start, $"ldr {scratchRegister}, =<{newAddress+1:X6}>", $"bx {scratchRegister}");

         // TODO add optional pop version if there's no scratch register
         Compile(token, model, newAddress, $"ldr {scratchRegister}, [pc, <{newAddress + 0x10:X6}>]", $"mov lr, {scratchRegister}");

         token.ChangeData(model, newAddress + 4, existingCode);
         Compile(token, model, newAddress + 0xC, $"bx lr", "nop");
         model.WritePointer(token, newAddress + 0x10, start + 9);
         model.ObserveRunWritten(token, new PointerRun(newAddress + 0x10));

         return newAddress;
      }

      private static readonly IReadOnlyCollection<byte> nop = new byte[] { 0, 0 };

      /// <summary>
      /// If you give the ThumbParser a token, it will make the data changes and the metadata changes
      /// </summary>
      public IReadOnlyList<byte> Compile(ModelDelta token, IDataModel model, int start, params string[] lines) {
         var initialAnchor = model.GetAnchorFromAddress(-1, start);
         var result = Compile(model, start, out var newRuns, lines);

         // we want to clear the format since pointers may have moved
         // but be careful not to clear any pointers to this routine+1, because of the way bx instructions work
         model.ClearFormat(token, start, 1);
         if (result.Count > 1) model.ClearFormat(token, start + 1, result.Count - 1);
         // but we want to keep an initial anchor pointing to this code block if there was one.
         if (!string.IsNullOrEmpty(initialAnchor)) model.ObserveAnchorWritten(token, initialAnchor, new NoInfoRun(start));

         // update the data, and the format
         for (int i = 0; i < result.Count; i++) token.ChangeData(model, start + i, result[i]);
         foreach (var run in newRuns) model.ObserveRunWritten(token, run);
         return result;
      }

      /// <summary>
      /// If you don't give the ThumbParser a token, it will return a set of new Pointers that it expects you to add.
      /// </summary>
      public IReadOnlyList<byte> Compile(IDataModel model, int start, out IReadOnlyList<IFormattedRun> newRuns, params string[] lines) {
         var result = new List<byte>();
         var addedRuns = new List<IFormattedRun>();
         // labels are allowed to be on the same line as code, and code can end with comments.
         // remove excess whitespace/comments and splitting labels from code
         lines = lines.SelectMany(line => {
            line = line.ToLower().Split('@')[0].Trim();
            if (line == string.Empty) return Enumerable.Empty<string>();
            var parts = line.Split(":");
            if (parts.Length > 1 && parts[1].Length > 0) {
               return new[] { parts[0].Trim() + ":", parts[1].Trim() };
            } else {
               return new[] { line };
            }
         }).ToArray();

         // first pass: look for labels
         var skip = new SkipInstruction();
         var labels = new Dictionary<string, int>();
         var inlineWords = new Queue<DeferredLoadRegisterToken>();
         int position = start;
         foreach (var line in lines) {
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
                  var labelsToFix = labels.Keys.Where(key => labels[key] == position - 6).ToList();
                  foreach (var label in labelsToFix) labels[label] = position - 4;
               }
               if (line.StartsWith("ldr ") && line.Contains("=")) {
                  // We only care about the count during this step. But doing a full compile will keep us from double-counting duplicate words.
                  DeferredLoadRegisterToken.TryCompile(position - 2, line, new LabelLibrary(model, labels), inlineWords);
               }
               if (DeferredLoadRegisterToken.IsEndOfSection(line) && inlineWords.Count > 0) {
                  if (position % 4 != 0) position += 2;
                  position += 4 * inlineWords.Count;
                  inlineWords.Clear();
               }
               if (skip.TryAssemble(line, default, default, default, out var _)) {
                  position -= 2; // not an actual instruction
               }
            }
         }
         // any words that haven't been added yet get added to the end
         if (inlineWords.Count > 0) {
            if (position % 4 != 0) position += 2;
            position += 4 * inlineWords.Count;
            inlineWords.Clear();
         }

         var labelLibrary = new LabelLibrary(model, labels);

         foreach (var rawLine in lines) {
            if (rawLine.EndsWith(":")) continue;   // don't compile labels
            var line = PatchInstruction(rawLine);
            bool foundMatch = false;
            foreach (var instruction in instructionTemplates) {
               if (!instruction.TryAssemble(line, conditionalCodes, start + result.Count, labelLibrary, out byte[] code)) continue;
               if (instruction.RequiresAlignment && (result.Count + start) % 4 != 0) result.AddRange(nop);
               result.AddRange(code);
               foundMatch = true;
               if (instruction is WordInstruction) {
                  if (code[3] == 0x08 || code[3] == 0x09) {
                     addedRuns.Add(new PointerRun(start + result.Count - 4));
                  }
               }
               break;
            }
            if (!foundMatch) {
               if (DeferredLoadRegisterToken.TryCompile(result.Count, line, labelLibrary, inlineWords)) {
                  result.AddRange(nop); // we'll come back to this once we know the offset
               } else {
                  result.AddRange(nop);
               }
            } else if (DeferredLoadRegisterToken.IsEndOfSection(line) && inlineWords.Count > 0) {
               if ((result.Count + start) % 4 != 0) result.AddRange(nop);
               while (inlineWords.Count > 0) {
                  var token = inlineWords.Dequeue();
                  result.AddRange(new byte[] { 0, 0, 0, 0 }); // add space for the new word
                  token.Write(start, result, result.Count - 4);
                  var highByte = token.WordToLoad >> 24;
                  if (highByte == 0x08 || highByte == 0x09) addedRuns.Add(new PointerRun(start + result.Count - 4));
               }
            }
         }
         // any words that haven't been added yet get added to the end
         if (inlineWords.Count > 0) {
            if (position % 4 != 0) result.AddRange(nop);
            while (inlineWords.Count > 0) {
               var token = inlineWords.Dequeue();
               result.AddRange(new byte[] { 0, 0, 0, 0 }); // add space for the new word
               token.Write(start, result, result.Count - 4);
            }
         }

         newRuns = addedRuns;
         return result;
      }

      private string PatchInstruction(string line) {
         // patch `add sp, #-4` to `sub sp, #4`
         if (line.StartsWith("add ") && line.Contains(" sp, ") && line.Contains(" #-")) {
            line = line.Replace("add ", "sub ");
            line = line.Replace(" #-", " #");
         } else if (line.StartsWith("add ") && line.Contains(" sp,#-")) {
            line = line.Replace("add ", "sub ");
            line = line.Replace(" sp,#-", " sp,#");
         }

         // patch `push {lr,list}` or `push {list,lr}` to `push lr, {list}`
         if (line.StartsWith("push ") && line.Contains("{lr,")) {
            line = line.Replace("push ", "push lr, ");
            line = line.Replace("{lr,", "{");
         } else if (line.StartsWith("push ") && line.Contains(",lr}")) {
            line = line.Replace("push ", "push lr, ");
            line = line.Replace(",lr}", "}");
         }

         // patch `add r0, r1` to `add r0, r0, r1` (+= instruction is only allowed if one of the registers is high)
         var tokens = line.Replace(',', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries);
         if (tokens[0] == "add" && tokens.Length == 3 && tokens.Skip(1).All(token => token.StartsWith("r"))) {
            var r = tokens.Skip(1).Select(token => int.TryParse(token.Substring(1), out var index) ? index : -1).ToArray();
            if (r.All(i => i >= 0 && i < 8)) {
               // we need to patch this one
               line = $"add r{r[0]}, r{r[0]}, r{r[1]}";
            }
         }

         return line;
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
                 // Hack: if the multiplier and adder are both 4, then the number is unsigned.
      HighRegister,
      List,
      ReverseList,  // not used
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
               var listStart = instruction.IndexOf("list");
               instruction = instruction.Replace("list", SerializeRegisterList(bits));
               if (instruction.Length > listStart && instruction[listStart] == ',') instruction = instruction.Substring(0, listStart) + instruction.Substring(listStart + 1).Trim();
            } else if (part.Type == InstructionArgType.ReverseList) {
               instruction = instruction.Replace("tsil", SerializeRegisterReverseList(bits));
            } else if (part.Type == InstructionArgType.Condition) {
               var suffix = conditionCodes.First(code => code.Code == bits).Mnemonic;
               instruction = instruction.Replace("{cond}", suffix);
            } else if (part.Type == InstructionArgType.Numeric) {
               if (part.Code != 0) {
                  instruction = CalculatePcRelativeAddress(data, instruction, pcAddress, part, bits);
               } else if (instruction.Contains("#=#*")) {
                  var multiplierIndex = instruction.IndexOf("#=#*") + 4;
                  var multiplier = instruction[multiplierIndex] - '0';
                  instruction = instruction.Replace("#=#*" + instruction[multiplierIndex], $"#{bits * multiplier}");
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

      private static string CalculatePcRelativeAddress(IDataModel model, string instruction, int pcAddress, InstructionPart part, ushort bits) {
         var mult = GrabBits(part.Code, 8, 8);
         var add = GrabBits(part.Code, 0, 8);
         var numeric = (short)bits;

         // Add on the sign. Note that ldr (recognized by mult=4, add=4) are unsigned.
         if (mult != 4 || add != 4) {
            numeric <<= 16 - part.Length;
            numeric >>= 16 - part.Length; // signed-right-shift carries the sign
         }

         if (instruction.Contains("#")) {
            // this is the first # in this instruction.
            var address = pcAddress - (pcAddress % mult) + numeric * mult + add;
            var end = instruction.EndsWith("]") ? "]" : string.Empty;
            instruction = instruction.Split("#=")[0] + "#" + end;
            var addressText = model.GetAnchorFromAddress(-1, address);
            if (string.IsNullOrEmpty(addressText)) addressText = address.ToString("X6");
            instruction = instruction.Replace("#", $"<{addressText}>");
         } else {
            // this is an additional # in the same instruction.
            // decode back from the old one
            var content = instruction.Split('<')[1].Split('>')[0];
            var address = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, content);
            if (address == Pointer.NULL) address = int.Parse(content, NumberStyles.HexNumber);
            address -= pcAddress - (pcAddress % mult) + add;
            address /= mult;
            address &= ((1 << part.Length) - 1); // drop the high bits, keep only the data bits. This makes it lose the sign.
            // concat the new numeric
            address += bits << part.Length;   // the new numeric is the higher bits
            // shift to get arithmetic sign bits
            address <<= 32 - part.Length * 2;
            address >>= 32 - part.Length * 2;   // since address is a signed int, C# right-shift will carry 1-bits down if the high bit is set. This is what we want to happen.
            // encode again
            address *= mult;
            address += pcAddress - (pcAddress % mult) + add;
            var addressText = model.GetAnchorFromAddress(-1, address);
            if (string.IsNullOrEmpty(addressText)) addressText = address.ToString("X6");
            instruction = instruction.Split('<')[0] + $"<{addressText}>" + (instruction + " ").Split('>')[1].Trim(); // extra space / trim let's us get everything after the '>', even if it's empty
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
               if (line[0] != '[') {
                  if (template.StartsWith("[pc, ") && template.EndsWith("]") && labels.ResolveLabel(line) != Pointer.NULL) {
                     template = template.Substring(5);
                     template = template.Substring(0, template.Length - 1);
                     continue;
                  } else {
                     return false;
                  }
               }
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
               if (line.StartsWith("sp")) line = "r13" + line.Substring(2);
               if (line.StartsWith("lr")) line = "r14" + line.Substring(2);
               if (line.StartsWith("pc")) line = "r15" + line.Substring(2);
               if (line[0] != 'r') return false;
               var name = "r" + template[1];
               if (int.TryParse(line.Split(',', ']')[0].Substring(1), out int value)) {
                  if (value > 7 && !instructionParts.Any(part => part.Type == InstructionArgType.HighRegister)) return false;
                  for (int index = 0; index < instructionParts.Count; index++) {
                     var instruction = instructionParts[index];
                     if (instruction.Name != name) continue;
                     registerValues[index] = value;
                  }
               }
               template = template.Substring(2);
               var register = "r" + value;
               if (register.Length > line.Length) return false;
               line = line.Substring(("r" + value).Length);
               continue;
            }

            // read a pointer
            if (template.StartsWith("#=pc")) {
               if (line[0] == '<') line = line.Substring(1);
               var content = line;
               if (content.Contains('>')) content = content.Split('>')[0];
               int extraLength = 0;
               if (content.StartsWith("0x")) {
                  content = content.Substring(2);
                  extraLength += 2;
               }
               numeric = labels.ResolveLabel(content);
               if (numeric == Pointer.NULL && !int.TryParse(content, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out numeric)) return false;
               if (line.Length < content.Length) return false;
               if (numeric >= 0x08000000) numeric -= 0x08000000;
               line = line.Substring(content.Length + extraLength);
               if (line.StartsWith(">")) line = line.Substring(1);
               template = template.Substring(template.IndexOf('+') + 1);
               template = template.Substring(template.IndexOf('+') + 2);
               continue;
            }

            // read a number
            if (template[0] == '#') {
               if (line[0] == '#') line = line.Substring(1);
               var numberAsText = line.Split(',', ']')[0];
               if (numberAsText.StartsWith("0x")) {
                  if (!int.TryParse(numberAsText.Substring(2), NumberStyles.HexNumber, CultureInfo.CurrentCulture, out numeric)) return false;
               } else {
                  if (!int.TryParse(numberAsText, out numeric)) return false;
               }
               template = template.Substring(1);
               line = line.Substring(numberAsText.Length);
               if (template.StartsWith("=#*")) {
                  var multiplier = template[3] - '0';
                  template = template.Substring(4);
                  numeric /= multiplier;
               }
               continue;
            }

            // read list
            if (template[0] == '{') {
               var templateListLength = template.IndexOf("}") + 1;
               if (templateListLength == 0) return false;
               if (line[0] != '{') return false;
               var listEnd = line.IndexOf('}');
               if (listEnd == -1) return false;
               var serializedList = line.Substring(1, listEnd - 1);
               line = line.Substring(listEnd + 1);
               if (template.Contains("lr}") || template.Contains("{lr")) {
                  if (!serializedList.Contains("lr")) return false;
                  serializedList = serializedList.Replace("lr", string.Empty);
               } else if (template.Contains("pc}") || template.Contains("{pc")) {
                  if (!serializedList.Contains("pc")) return false;
                  serializedList = serializedList.Replace("pc", string.Empty);
               }
               if (serializedList.Contains("lr") || serializedList.Contains("pc")) return false;
               list = ParseList(serializedList);
               template = template.Substring(templateListLength);
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

      public static string SerializeRegisterList(ushort registerList) {
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

      public static string SerializeRegisterReverseList(ushort registerList) {
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
         } else if (labels.TryResolveValue(line, out result)) {
            // Parse successful! Nothing else to do.
         } else {
            if (line.StartsWith("0x")) line = line.Substring(2);
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

   public class AlignInstruction : IInstruction {
      public int ByteLength => 4;
      public bool RequiresAlignment => true;

      public string Disassemble(IDataModel data, int address, IReadOnlyList<ConditionCode> conditionalCodes) {
         throw new NotImplementedException();
      }

      public bool Matches(IDataModel data, int index) => false;

      public bool TryAssemble(string line, IReadOnlyList<ConditionCode> conditionCodes, int address, LabelLibrary labels, out byte[] results) {
         results = new byte[0];
         return line.StartsWith(".align");
      }
   }

   /// <summary>
   /// There are a variety of macros available in thumb that we just ignore. If you see one of these, just move along.
   /// </summary>
   public class SkipInstruction : IInstruction {
      public int ByteLength => 0;
      public bool RequiresAlignment => false;

      public string Disassemble(IDataModel data, int address, IReadOnlyList<ConditionCode> conditionalCodes) {
         throw new NotImplementedException();
      }

      public bool Matches(IDataModel data, int index) => false;

      public bool TryAssemble(string line, IReadOnlyList<ConditionCode> conditionCodes, int address, LabelLibrary labels, out byte[] results) {
         results = new byte[0];
         foreach (var start in ".text .thumb .end .thumb_func .global .align".Split(' ')) {
            if (line.StartsWith(start)) return true;
         }
         return false;
      }
   }

   /// <summary>
   /// Represents an instruction like `ldr r0, =(0x8000000)`
   /// The word to load and instruction address get cached, but nothing gets written.
   /// Later, after an unconditional branch statement (b, bx, or pop pc), the new address is used.
   /// </summary>
   public class DeferredLoadRegisterToken {
      private IList<int> InstructionAddresses { get; set; }
      public int Register { get; }
      public int WordToLoad { get; }

      public DeferredLoadRegisterToken(int address, int register, string word, LabelLibrary labels) {
         if (word.StartsWith("=")) word = word.Substring(1).Trim();
         if (word.StartsWith("(")) word = word.Substring(1).Trim();
         if (word.StartsWith("#")) word = word.Substring(1).Trim();
         if (word.EndsWith(")")) word = word.Substring(0, word.Length - 1).Trim();

         var more = 0;
         if (word.IndexOf("+") > word.IndexOf(">")) {
            var parts = word.Split("+");
            if (parts.Length == 2) int.TryParse(parts[1], out more);
            word = parts[0];
         }

         int wordValue;
         if (word.StartsWith("<") && word.EndsWith(">")) {
            word = word.Substring(1, word.Length - 2);
            if (!(int.TryParse(word, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out wordValue))) {
               wordValue = labels.ResolveLabel(word);
            }
            wordValue += 0x08000000;
         } else if (word.StartsWith("0x")) {
            int.TryParse(word.Substring(2), NumberStyles.HexNumber, CultureInfo.CurrentCulture, out wordValue);
         } else {
            int.TryParse(word, out wordValue);
         }

         InstructionAddresses = new List<int> { address };
         Register = register;
         WordToLoad = wordValue + more;
      }

      public void Write(int dataStart, IList<byte> data, int wordAddress) {
         //build instruction
         foreach (var instructionAddress in InstructionAddresses) {
            var instructionWord = instructionAddress;
            if ((dataStart + instructionWord) % 4 != 0) instructionWord -= 2;
            var offset = (wordAddress - instructionWord - 4) / 4;
            var instruction = 0b01001;
            instruction <<= 3;
            instruction |= Register;
            instruction <<= 8;
            instruction |= offset;

            // insert instruction
            data[instructionAddress] = (byte)instruction;
            data[instructionAddress + 1] = (byte)(instruction >> 8);

            // insert word
            for (int i = 0; i < 4; i++) data[wordAddress + i] = (byte)(WordToLoad >> 8 * i);
         }
      }

      public static bool IsEndOfSection(string line) {
         return line.StartsWith("b ") || line.StartsWith("bx ") || (line.StartsWith("pop ") && line.Contains("pc"));
      }

      public static bool TryCompile(int address, string line, LabelLibrary labels, Queue<DeferredLoadRegisterToken> inlineWords) {
         if (!line.StartsWith("ldr ")) return false;
         line = line.Substring(4);
         var parts = line.Split("=");
         if (parts.Length != 2) return false;
         var registerText = parts[0].Trim().Split(',')[0];
         if (registerText.Length != 2) return false;
         if (registerText[0] != 'r') return false;
         int register = registerText[1] - '0';
         if (register < 0 || register > 7) return false;
         var newToken = new DeferredLoadRegisterToken(address, register, parts[1].Trim(), labels);
         var existingToken = inlineWords.FirstOrDefault(token => token.WordToLoad == newToken.WordToLoad);
         if (existingToken != null) {
            existingToken.InstructionAddresses.Add(newToken.InstructionAddresses[0]);
         } else {
            inlineWords.Enqueue(newToken);
         }
         return true;
      }
   }
}
