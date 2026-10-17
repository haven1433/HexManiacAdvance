using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace HavenSoft.HexManiac.Core.Models.Sound {
   /// <summary>
   /// Constants from MPlayDef.s (the m4a/"Sappy" sequence command names used by mid2agb .s files).
   /// </summary>
   public static class MPlayDef {
      public static readonly int[] ClockTable = {
         0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24,
         28, 30, 32, 36, 40, 42, 44, 48, 52, 54, 56, 60, 64, 66, 68, 72, 76, 78, 80, 84, 88, 90, 92, 96,
      };

      private static readonly string[] noteNames = { "Cn", "Cs", "Dn", "Ds", "En", "Fn", "Fs", "Gn", "Gs", "An", "As", "Bn" };
      private static readonly string[] octaveNames = { "M2", "M1", "0", "1", "2", "3", "4", "5", "6", "7", "8" };

      // must come after the arrays above: static initializers run in declaration order
      public static readonly IReadOnlyDictionary<string, int> Symbols = Build();

      public static string NoteName(int note) {
         if (note < 0 || note > 127) return note.ToString();
         var octave = note / 12;
         return octave < octaveNames.Length ? noteNames[note % 12] + octaveNames[octave] : note.ToString();
      }

      public static string WaitName(int clockIndex) => clockIndex >= 0 && clockIndex < ClockTable.Length ? $"W{ClockTable[clockIndex]:D2}" : null;
      public static string NoteLengthName(int clockIndex) => clockIndex == 0 ? "TIE" : clockIndex > 0 && clockIndex < ClockTable.Length ? $"N{ClockTable[clockIndex]:D2}" : null;

      private static Dictionary<string, int> Build() {
         var d = new Dictionary<string, int>();
         for (int i = 0; i < ClockTable.Length; i++) d[$"W{ClockTable[i]:D2}"] = 0x80 + i;
         d["FINE"] = 0xB1; d["GOTO"] = 0xB2; d["PATT"] = 0xB3; d["PEND"] = 0xB4; d["REPT"] = 0xB5;
         d["MEMACC"] = 0xB9; d["PRIO"] = 0xBA; d["TEMPO"] = 0xBB; d["KEYSH"] = 0xBC; d["VOICE"] = 0xBD;
         d["VOL"] = 0xBE; d["PAN"] = 0xBF; d["BEND"] = 0xC0; d["BENDR"] = 0xC1; d["LFOS"] = 0xC2; d["LFODL"] = 0xC3;
         d["MOD"] = 0xC4; d["MODT"] = 0xC5; d["TUNE"] = 0xC8; d["XCMD"] = 0xCD; d["xIECV"] = 0x08; d["xIECL"] = 0x09;
         d["EOT"] = 0xCE; d["TIE"] = 0xCF;
         for (int i = 1; i < ClockTable.Length; i++) d[$"N{ClockTable[i]:D2}"] = 0xCF + i;
         d["mxv"] = 0x7F; d["c_v"] = 0x40;
         for (int note = 0; note < 128; note++) {
            var name = NoteName(note);
            if (!d.ContainsKey(name)) d[name] = note;
         }
         for (int v = 0; v < 128; v++) d[$"v{v:D3}"] = v;
         d["gtp1"] = 1; d["gtp2"] = 2; d["gtp3"] = 3;
         d["mod_vib"] = 0; d["mod_tre"] = 1; d["mod_pan"] = 2;
         var mem = new[] { "mem_set", "mem_add", "mem_sub", "mem_mem_set", "mem_mem_add", "mem_mem_sub", "mem_beq", "mem_bne", "mem_bhi", "mem_bhs", "mem_bls", "mem_blo", "mem_mem_beq", "mem_mem_bne", "mem_mem_bhi", "mem_mem_bhs", "mem_mem_bls", "mem_mem_blo" };
         for (int i = 0; i < mem.Length; i++) d[mem[i]] = i;
         d["reverb_set"] = 0x80;
         d["PAM"] = 0xBF;
         return d;
      }
   }

   /// <summary>
   /// The header of an m4a song: { trackCount, blockCount, priority, reverb, voicegroup pointer, track pointers... }
   /// </summary>
   public class SongHeader {
      public int Address { get; init; }
      public int TrackCount { get; init; }
      public int BlockCount { get; init; }
      public int Priority { get; init; }
      public int Reverb { get; init; }
      public int Voicegroup { get; init; }
      public IReadOnlyList<int> Tracks { get; init; }
      public int Length => 8 + 4 * TrackCount;

      public static bool TryRead(IDataModel model, int address, out SongHeader header) {
         header = null;
         if (address < 0 || address + 8 > model.Count) return false;
         int trackCount = model[address];
         if (trackCount > 16 || address + 8 + 4 * trackCount > model.Count) return false;
         var voicegroup = model.ReadPointer(address + 4);
         if (trackCount > 0 && (voicegroup < 0 || voicegroup >= model.Count)) return false;
         var tracks = new List<int>();
         for (int i = 0; i < trackCount; i++) {
            var track = model.ReadPointer(address + 8 + 4 * i);
            if (track < 0 || track >= model.Count) return false;
            tracks.Add(track);
         }
         header = new SongHeader {
            Address = address,
            TrackCount = trackCount,
            BlockCount = model[address + 1],
            Priority = model[address + 2],
            Reverb = model[address + 3],
            Voicegroup = voicegroup,
            Tracks = tracks,
         };
         return true;
      }
   }

   /// <summary>
   /// Turns a song in the ROM back into a mid2agb-style .s file.
   /// </summary>
   public class SongDisassembler {
      private readonly IDataModel model;
      public SongDisassembler(IDataModel model) => this.model = model;

      /// <summary>
      /// Walk a track and return the addresses of its commands, its end (exclusive), and any GOTO/PATT targets.
      /// Tracks are read linearly until FINE; pattern targets outside the linear range are followed as extra blocks.
      /// </summary>
      public string Disassemble(SongHeader header, string name) {
         var text = new StringBuilder();
         text.AppendLine("\t.include \"MPlayDef.s\"");
         text.AppendLine();
         // the address is only right for this ROM (and this build of it); the name lets the file find its voicegroup in another version of the ROM
         var voicegroupName = VoicegroupNameAt(header.Voicegroup);
         text.AppendLine($"\t.equ\t{name}_grp, 0x{header.Voicegroup + BaseModel.PointerOffset:X8}\t@ voicegroup{(voicegroupName == null ? string.Empty : " " + voicegroupName)}");
         text.AppendLine($"\t.equ\t{name}_pri, {header.Priority}");
         text.AppendLine(header.Reverb >= 0x80 ? $"\t.equ\t{name}_rev, reverb_set+{header.Reverb - 0x80}" : $"\t.equ\t{name}_rev, {header.Reverb}");
         text.AppendLine($"\t.equ\t{name}_mvl, 127");
         text.AppendLine($"\t.equ\t{name}_key, 0");
         text.AppendLine($"\t.equ\t{name}_tbs, 1");
         text.AppendLine($"\t.equ\t{name}_exg, 0");
         text.AppendLine($"\t.equ\t{name}_cmp, 1");
         text.AppendLine();
         text.AppendLine("\t.section .rodata");
         text.AppendLine($"\t.global\t{name}");
         text.AppendLine("\t.align\t2");
         text.AppendLine();

         var labels = new Dictionary<int, string>();
         var blocks = new List<(int start, int end, int track)>();
         for (int i = 0; i < header.TrackCount; i++) {
            labels[header.Tracks[i]] = $"{name}_{i + 1}";
         }
         // first pass: find block extents and label targets
         for (int i = 0; i < header.TrackCount; i++) {
            var pending = new Queue<(int address, bool isPattern)>();
            pending.Enqueue((header.Tracks[i], false));
            var visited = new HashSet<int>();
            int gotoCount = 0, pattCount = 0;
            while (pending.Count > 0) {
               var (start, isPattern) = pending.Dequeue();
               if (!visited.Add(start)) continue;
               if (blocks.Any(b => b.start <= start && start < b.end)) continue;
               var end = Walk(start, isPattern, (address, command) => {
                  if (command == 0xB2 || command == 0xB3 || command == 0xB5) {
                     var target = model.ReadPointer(address + (command == 0xB5 ? 2 : 1));
                     if (target < 0 || target >= model.Count) return;
                     if (!labels.ContainsKey(target)) {
                        labels[target] = command == 0xB3 ? $"{name}_{i + 1}_{++pattCount:D3}" : $"{name}_{i + 1}_B{++gotoCount}";
                     }
                     pending.Enqueue((target, command == 0xB3));
                  }
               });
               blocks.Add((start, end, i));
            }
         }

         // second pass: print each track's blocks in address order
         for (int i = 0; i < header.TrackCount; i++) {
            text.AppendLine($"@**************** Track {i + 1} (Midi-Chn.{i + 1}) ****************@");
            text.AppendLine();
            foreach (var (start, end, track) in blocks.Where(b => b.track == i).OrderBy(b => b.start)) {
               PrintBlock(text, start, end, labels, name);
               text.AppendLine();
            }
         }

         text.AppendLine("@******************************************************@");
         text.AppendLine("\t.align\t2");
         text.AppendLine();
         text.AppendLine($"{name}:");
         text.AppendLine($"\t.byte\t{header.TrackCount}\t@ NumTrks");
         text.AppendLine($"\t.byte\t{header.BlockCount}\t@ NumBlks");
         text.AppendLine($"\t.byte\t{name}_pri\t@ Priority");
         text.AppendLine($"\t.byte\t{name}_rev\t@ Reverb.");
         text.AppendLine();
         text.AppendLine($"\t.word\t{name}_grp");
         text.AppendLine();
         for (int i = 0; i < header.TrackCount; i++) text.AppendLine($"\t.word\t{name}_{i + 1}");
         text.AppendLine();
         text.AppendLine("\t.end");
         return text.ToString();
      }

      /// <summary>The name of the voicegroup that starts at an address (the part of its anchor name after 'voicegroups.'), or null.</summary>
      private string VoicegroupNameAt(int address) {
         if (address < 0 || address >= model.Count) return null;
         var anchor = model.GetAnchorFromAddress(-1, address);
         return VoicegroupCatalog.IsVoicegroupAnchor(anchor, out var shortName) && shortName.All(c => c < 128 && (char.IsLetterOrDigit(c) || c == '_' || c == '.')) ? shortName : null;
      }

      /// <summary>
      /// The voices (the numbers a voicegroup is indexed by) that the song's tracks select with VOICE commands, including the ones in patterns and in jumped-to blocks.
      /// A track is followed the way the sound engine plays it: to its FINE, a GOTO ends the walk of that block (the command after it can't be reached from it), and PATT / GOTO / REPT targets are walked as well.
      /// A PEND only ends a walk that was started by a PATT: a PEND outside of a pattern does nothing in the engine (mid2agb writes the first copy of a repeated phrase in the middle of the track and
      /// closes it with a PEND, then the track plays on), so the walk of a track goes on after it. Walks that started in a pattern and walks of the track itself keep their own list of visited addresses,
      /// because the same bytes are met both ways.
      /// </summary>
      public SortedSet<int> FindVoices(SongHeader header) {
         var voices = new SortedSet<int>();
         if (header == null) return voices;
         var visitedInTrack = new HashSet<int>();
         var visitedInPattern = new HashSet<int>();
         var pending = new Queue<(int address, bool pattern)>();
         foreach (var track in header.Tracks) pending.Enqueue((track, false));
         while (pending.Count > 0) {
            var (address, pattern) = pending.Dequeue();
            var visited = pattern ? visitedInPattern : visitedInTrack;
            int runningStatus = 0;
            int guard = 0x40000;
            while (address >= 0 && address < model.Count && guard-- > 0) {
               if (!visited.Add(address)) break;
               int command = model[address];
               int length = CommandLength(address, ref runningStatus, out int effective);
               int argStart = command < 0x80 ? address : address + 1;
               int argCount = address + length - argStart;
               if (effective == 0xBD && argCount > 0 && model[argStart] < 0x80) voices.Add(model[argStart]);
               if (effective == 0xB2 || effective == 0xB3 || effective == 0xB5) {
                  var target = model.ReadPointer(argStart + (effective == 0xB5 ? 1 : 0));
                  if (target >= 0 && target < model.Count) pending.Enqueue((target, effective == 0xB3));
               }
               address += length;
               if (effective == 0xB1 || effective == 0xB2) break; // FINE, GOTO: nothing after them is played from here
               if (effective == 0xB4 && pattern) break;           // PEND: the end of a pattern (outside of a pattern the engine ignores it)
            }
         }
         return voices;
      }

      /// <summary>
      /// Returns the address just after the FINE command (or after the last readable byte).
      /// </summary>
      private int Walk(int start, bool stopAtPatternEnd, Action<int, int> onCommand) {
         int address = start;
         int runningStatus = 0;
         int guard = 0x40000;
         while (address < model.Count && guard-- > 0) {
            int command = model[address];
            int length = CommandLength(address, ref runningStatus, out _);
            if (command >= 0x80) onCommand(address, command);
            address += length;
            if (command == 0xB1) break;
            if (stopAtPatternEnd && command == 0xB4) break;
         }
         return address;
      }

      /// <summary>
      /// Length in bytes of the command at the given address, including arguments.
      /// </summary>
      private int CommandLength(int address, ref int runningStatus, out int effectiveCommand) {
         int command = model[address];
         int length = 1;
         int argStart = address + 1;
         if (command < 0x80) {
            effectiveCommand = runningStatus;
            length = 0;
            argStart = address;
         } else {
            effectiveCommand = command;
            if (command >= 0xBD) runningStatus = command;
         }
         int ArgCountWhileSmall(int max) {
            int count = 0;
            while (count < max && argStart + count < model.Count && model[argStart + count] < 0x80) count++;
            return count;
         }
         if (effectiveCommand >= 0xCF) {
            length += ArgCountWhileSmall(3);
         } else if (effectiveCommand == 0xCE) {
            length += ArgCountWhileSmall(1);
         } else if (effectiveCommand == 0xB2 || effectiveCommand == 0xB3) {
            length += 4;
         } else if (effectiveCommand == 0xB5) {
            length += 5; // the count, then the address. A count of 0 is an endless jump (ply_rept goes on like GOTO): the address is there as well
         } else if (effectiveCommand == 0xB9) {
            length += 3;
         } else if (effectiveCommand == 0xCD) {
            length += 2;
         } else if (effectiveCommand == 0xB1 || effectiveCommand == 0xB4 || effectiveCommand < 0xB1) {
            // FINE, PEND, waits: no arguments
         } else if (effectiveCommand >= 0xBA && effectiveCommand <= 0xC8 || effectiveCommand >= 0xB6 && effectiveCommand <= 0xB8) {
            length += 1;
         }
         if (command < 0x80 && length == 0) length = 1; // unknown running status: consume the byte
         return length;
      }

      private void PrintBlock(StringBuilder text, int start, int end, IReadOnlyDictionary<int, string> labels, string name) {
         int address = start;
         int runningStatus = 0;
         while (address < end) {
            if (labels.TryGetValue(address, out var label)) text.AppendLine($"{label}:");
            int command = model[address];
            int length = CommandLength(address, ref runningStatus, out int effective);
            var args = new List<string>();
            int argStart = command < 0x80 ? address : address + 1;
            int argCount = address + length - argStart;
            string commandName = null;
            if (command >= 0x80) {
               commandName = CommandName(command);
            }
            if (effective >= 0xCF) {
               if (argCount > 0) args.Add(MPlayDef.NoteName(model[argStart]));
               if (argCount > 1) args.Add($"v{model[argStart + 1]:D3}");
               if (argCount > 2) args.Add(model[argStart + 2] >= 1 && model[argStart + 2] <= 3 ? $"gtp{model[argStart + 2]}" : model[argStart + 2].ToString()); // mid2agb only has names for 1 to 3, other programs use any number up to 127
            } else if (effective == 0xCE) {
               if (argCount > 0) args.Add(MPlayDef.NoteName(model[argStart]));
            } else if (effective == 0xB2 || effective == 0xB3) {
               var target = model.ReadPointer(argStart);
               text.AppendLine($"\t.byte\t{commandName}");
               text.AppendLine($"\t .word\t{(labels.TryGetValue(target, out var targetLabel) ? targetLabel : $"0x{target + BaseModel.PointerOffset:X8}")}");
               address += length;
               continue;
            } else if (effective == 0xB5) {
               var target = model.ReadPointer(argStart + 1);
               text.AppendLine($"\t.byte\t{commandName} , {model[argStart]}");
               text.AppendLine($"\t .word\t{(labels.TryGetValue(target, out var targetLabel) ? targetLabel : $"0x{target + BaseModel.PointerOffset:X8}")}");
               address += length;
               continue;
            } else if (effective == 0xB9) {
               for (int i = 0; i < argCount; i++) args.Add(model[argStart + i].ToString());
            } else if (effective == 0xCD) {
               if (argCount > 0) args.Add(model[argStart] == 8 ? "xIECV" : model[argStart] == 9 ? "xIECL" : model[argStart].ToString());
               if (argCount > 1) args.Add(model[argStart + 1].ToString());
            } else if (effective == 0xBF || effective == 0xC0 || effective == 0xC8) {
               if (argCount > 0) { var v = model[argStart] - 0x40; args.Add(v >= 0 ? $"c_v+{v}" : $"c_v{v}"); }
            } else if (effective == 0xBE) {
               if (argCount > 0) args.Add($"{model[argStart]}*{name}_mvl/mxv");
            } else if (effective == 0xBB) {
               if (argCount > 0) args.Add($"{model[argStart] * 2}*{name}_tbs/2");
            } else if (effective == 0xBC) {
               if (argCount > 0) args.Add($"{name}_key+{(sbyte)model[argStart]}");
            } else if (effective == 0xC5) {
               if (argCount > 0) args.Add(model[argStart] switch { 0 => "mod_vib", 1 => "mod_tre", 2 => "mod_pan", _ => model[argStart].ToString() });
            } else {
               for (int i = 0; i < argCount; i++) args.Add(model[argStart + i].ToString());
            }
            var parts = new List<string>();
            if (commandName != null) parts.Add(commandName);
            parts.AddRange(args);
            var indent = command >= 0x80 && command < 0xB1 ? "\t.byte\t" : command >= 0x80 ? "\t.byte\t\t" : "\t.byte\t\t        ";
            text.AppendLine(indent + string.Join(" , ", parts));
            address += length;
         }
      }

      private static string CommandName(int command) {
         if (command < 0xB1) return MPlayDef.WaitName(command - 0x80) ?? $"0x{command:X2}";
         if (command >= 0xCF) return MPlayDef.NoteLengthName(command - 0xCF) ?? $"0x{command:X2}";
         foreach (var pair in MPlayDef.Symbols) {
            if (pair.Value == command && pair.Key != "PAM" && pair.Key.All(c => char.IsUpper(c) || char.IsDigit(c))) return pair.Key;
         }
         return $"0x{command:X2}";
      }
   }

   /// <summary>
   /// A small GNU-assembler compatible assembler for mid2agb .s song files (the format Sappy's "Assemble Song" reads).
   /// Supports labels, .equ/.set, .byte/.hword/.word, .align, .global, .include (MPlayDef.s is built in) and arithmetic expressions.
   /// </summary>
   public class SongAssembler {
      public class Result {
         public byte[] Bytes { get; init; }
         public int HeaderOffset { get; init; }
         public string Error { get; init; }
         public IReadOnlyList<string> UndefinedSymbols { get; init; } = Array.Empty<string>();
         public bool Success => Error == null;
         public string SongName { get; init; }
         /// <summary>The symbol in the header's voicegroup word when the file leaves it undefined (the symbol the Sound tab replaces by the chosen voicegroup), otherwise null.</summary>
         public string VoicegroupSymbol { get; init; }
      }

      private class Item {
         public int Line;
         public string Directive;     // .byte .hword .word .align .label
         public List<string> Args = new();
         public int Offset;
         public int Size;
      }

      private readonly Dictionary<string, string> equates = new();
      private readonly Dictionary<string, int> labels = new();
      private readonly Dictionary<string, int> externals = new();
      private readonly HashSet<string> undefined = new();
      private readonly List<Item> items = new();
      private readonly List<string> globals = new();
      private readonly HashSet<string> evaluating = new();
      private int baseAddress;

      public static readonly string[] IgnoredDirectives = { ".include", ".section", ".text", ".data", ".rodata", ".end", ".arm", ".thumb", ".syntax", ".type", ".size", ".file", ".ident", ".cpu", ".fpu", ".eabi_attribute", ".p2align" };

      /// <summary>
      /// Assemble the file as if it were placed at the given ROM address (used for absolute .word pointers).
      /// </summary>
      public static Result Assemble(string text, int baseAddress, IReadOnlyDictionary<string, int> externalSymbols = null) {
         var assembler = new SongAssembler();
         if (externalSymbols != null) foreach (var pair in externalSymbols) assembler.externals[pair.Key] = pair.Value;
         return assembler.Run(text, baseAddress);
      }

      private Result Run(string text, int baseAddress) {
         this.baseAddress = baseAddress;
         var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
         int offset = 0;
         for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++) {
            var line = StripComment(lines[lineIndex]);
            if (string.IsNullOrWhiteSpace(line)) continue;
            // labels (possibly several on a line, possibly followed by a directive)
            while (true) {
               var trimmed = line.TrimStart();
               var colon = trimmed.IndexOf(':');
               if (colon <= 0) break;
               var candidate = trimmed.Substring(0, colon).Trim();
               if (!IsSymbol(candidate)) break;
               if (labels.ContainsKey(candidate)) return Fail($"Line {lineIndex + 1}: label '{candidate}' is defined twice.");
               labels[candidate] = offset;
               line = trimmed.Substring(colon + 1);
            }
            line = line.Trim();
            if (line.Length == 0) continue;
            string directive, rest;
            var space = line.IndexOfAny(new[] { ' ', '\t' });
            if (space < 0) { directive = line; rest = string.Empty; } else { directive = line.Substring(0, space); rest = line.Substring(space + 1).Trim(); }
            var lower = directive.ToLowerInvariant();
            if (lower == ".equ" || lower == ".set" || lower == ".equiv") {
               var comma = rest.IndexOf(',');
               if (comma < 0) return Fail($"Line {lineIndex + 1}: expected '.equ name, value'.");
               var name = rest.Substring(0, comma).Trim();
               if (!IsSymbol(name)) return Fail($"Line {lineIndex + 1}: '{name}' is not a valid symbol name.");
               equates[name] = rest.Substring(comma + 1).Trim();
            } else if (!lower.StartsWith(".") && rest.StartsWith("=")) {
               if (!IsSymbol(directive)) return Fail($"Line {lineIndex + 1}: '{directive}' is not a valid symbol name.");
               equates[directive] = rest.Substring(1).Trim();
            } else if (lower == ".global" || lower == ".globl") {
               foreach (var name in SplitArgs(rest)) globals.Add(name.Trim());
            } else if (lower == ".byte" || lower == ".hword" || lower == ".2byte" || lower == ".short" || lower == ".word" || lower == ".4byte" || lower == ".int" || lower == ".long") {
               var args = SplitArgs(rest);
               if (args.Count == 0) return Fail($"Line {lineIndex + 1}: {directive} needs a value.");
               int size = lower == ".byte" ? 1 : (lower == ".hword" || lower == ".2byte" || lower == ".short") ? 2 : 4;
               var item = new Item { Line = lineIndex + 1, Directive = size == 1 ? ".byte" : size == 2 ? ".hword" : ".word", Args = args, Offset = offset, Size = size * args.Count };
               items.Add(item);
               offset += item.Size;
            } else if (lower == ".align" || lower == ".balign") {
               if (!TryParseNumber(rest.Trim(), out var amount)) return Fail($"Line {lineIndex + 1}: {directive} needs a number.");
               int alignment = lower == ".align" ? 1 << (int)amount : (int)amount;
               if (alignment <= 0 || alignment > 0x1000) return Fail($"Line {lineIndex + 1}: bad alignment.");
               int padding = (alignment - offset % alignment) % alignment;
               if (padding > 0) items.Add(new Item { Line = lineIndex + 1, Directive = ".align", Offset = offset, Size = padding });
               offset += padding;
            } else if (lower == ".space" || lower == ".skip" || lower == ".zero") {
               if (!TryParseNumber(SplitArgs(rest)[0].Trim(), out var amount)) return Fail($"Line {lineIndex + 1}: {directive} needs a number.");
               items.Add(new Item { Line = lineIndex + 1, Directive = ".align", Offset = offset, Size = (int)amount });
               offset += (int)amount;
            } else if (IgnoredDirectives.Contains(lower)) {
               // nothing to do
            } else if (lower.StartsWith(".")) {
               return Fail($"Line {lineIndex + 1}: unsupported directive '{directive}'.");
            } else {
               return Fail($"Line {lineIndex + 1}: unexpected text '{line}'.");
            }
         }

         // second pass: evaluate
         var bytes = new byte[offset];
         foreach (var item in items) {
            if (item.Directive == ".align") continue;
            int size = item.Size / item.Args.Count;
            for (int i = 0; i < item.Args.Count; i++) {
               if (!TryEvaluate(item.Args[i], out var value, out var error)) {
                  if (undefined.Count > 0) continue;
                  return Fail($"Line {item.Line}: {error}");
               }
               for (int b = 0; b < size; b++) bytes[item.Offset + i * size + b] = (byte)(value >> (8 * b));
            }
         }
         if (undefined.Count > 0) {
            return new Result { Error = $"Undefined symbol(s): {string.Join(", ", undefined.OrderBy(s => s))}", UndefinedSymbols = undefined.OrderBy(s => s).ToList(), VoicegroupSymbol = FindVoicegroupSymbol() };
         }

         // find the song header: the .global label (last one defined in the file)
         string songName = globals.LastOrDefault(labels.ContainsKey);
         if (songName == null) {
            // fallback: a label that's followed by 'trackCount, blockCount, priority, reverb, voicegroup pointer' and is at the end
            songName = labels.OrderByDescending(pair => pair.Value).Select(pair => pair.Key).FirstOrDefault();
         }
         if (songName == null) return Fail("Could not find the song header label (expected a .global symbol).");
         int header = labels[songName];
         if (header + 8 > bytes.Length) return Fail($"The song header '{songName}' is too short.");
         int trackCount = bytes[header];
         if (trackCount > 16 || header + 8 + 4 * trackCount > bytes.Length) return Fail($"The song header '{songName}' has an invalid track count ({trackCount}).");
         return new Result { Bytes = bytes, HeaderOffset = header, SongName = songName };
      }

      private Result Fail(string error) => new Result { Error = error };

      /// <summary>
      /// The undefined symbol that the voicegroup word of the song header (the word after the four bytes of counts, priority and reverb) is made of, following .equ aliases:
      /// "test_grp" in ".word test_grp" with ".equ test_grp, voicegroup000" gives "voicegroup000". Null if that word is a number or a defined symbol, or the header is not found.
      /// </summary>
      private string FindVoicegroupSymbol() {
         var songName = globals.LastOrDefault(labels.ContainsKey) ?? labels.OrderByDescending(pair => pair.Value).Select(pair => pair.Key).FirstOrDefault();
         if (songName == null) return null;
         var word = labels[songName] + 4;
         foreach (var item in items) {
            if (item.Directive != ".word" || word < item.Offset || word >= item.Offset + item.Size || (word - item.Offset) % 4 != 0) continue;
            var name = item.Args[(word - item.Offset) / 4].Trim();
            for (int hops = 0; hops < 16 && equates.TryGetValue(name, out var expression) && IsSymbol(expression.Trim()); hops++) name = expression.Trim();
            return undefined.Contains(name) ? name : null;
         }
         return null;
      }

      private static string StripComment(string line) {
         var sb = new StringBuilder();
         bool inString = false;
         for (int i = 0; i < line.Length; i++) {
            var c = line[i];
            if (c == '"') inString = !inString;
            if (!inString && (c == '@' || c == ';' || c == '#' && i == 0)) break;
            if (!inString && c == '/' && i + 1 < line.Length && line[i + 1] == '/') break;
            sb.Append(c);
         }
         return sb.ToString();
      }

      private static bool IsSymbol(string text) {
         if (string.IsNullOrEmpty(text)) return false;
         if (!(char.IsLetter(text[0]) || text[0] == '_' || text[0] == '.' || text[0] == '$')) return false;
         return text.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == '$');
      }

      private static List<string> SplitArgs(string text) {
         var result = new List<string>();
         int depth = 0;
         var current = new StringBuilder();
         foreach (var c in text) {
            if (c == '(') depth++;
            if (c == ')') depth--;
            if (c == ',' && depth == 0) { result.Add(current.ToString().Trim()); current.Clear(); continue; }
            current.Append(c);
         }
         if (current.ToString().Trim().Length > 0) result.Add(current.ToString().Trim());
         return result;
      }

      private static bool TryParseNumber(string text, out long value) {
         value = 0;
         text = text.Trim();
         if (text.Length == 0) return false;
         if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) return long.TryParse(text.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
         if (text.StartsWith("0b", StringComparison.OrdinalIgnoreCase)) {
            try { value = Convert.ToInt64(text.Substring(2), 2); return true; } catch { return false; }
         }
         if (text.Length > 1 && text[0] == '0' && text.All(char.IsDigit)) {
            try { value = Convert.ToInt64(text, 8); return true; } catch { return false; }
         }
         if (text.EndsWith("'") && text.StartsWith("'") && text.Length == 3) { value = text[1]; return true; }
         return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
      }

      #region expressions

      public bool TryEvaluate(string expression, out long value, out string error) {
         value = 0;
         error = null;
         try {
            var parser = new ExpressionParser(expression, ResolveSymbol);
            value = parser.Parse();
            return true;
         } catch (Exception e) {
            error = e.Message;
            return false;
         }
      }

      private long ResolveSymbol(string name) {
         if (labels.TryGetValue(name, out var offset)) return offset + baseAddress;
         if (equates.TryGetValue(name, out var expression)) {
            if (!evaluating.Add(name)) throw new InvalidOperationException($"Symbol '{name}' is defined in terms of itself.");
            try {
               var parser = new ExpressionParser(expression, ResolveSymbol);
               return parser.Parse();
            } finally {
               evaluating.Remove(name);
            }
         }
         if (externals.TryGetValue(name, out var external)) return external;
         if (MPlayDef.Symbols.TryGetValue(name, out var builtin)) return builtin;
         if (name == ".") return baseAddress;
         undefined.Add(name);
         throw new InvalidOperationException($"Undefined symbol '{name}'.");
      }

      private class ExpressionParser {
         private readonly string text;
         private readonly Func<string, long> resolve;
         private int position;

         public ExpressionParser(string text, Func<string, long> resolve) { this.text = text; this.resolve = resolve; }

         public long Parse() {
            var value = ParseOr();
            SkipSpace();
            if (position < text.Length) throw new InvalidOperationException($"Unexpected '{text.Substring(position)}' in expression '{text}'.");
            return value;
         }

         private void SkipSpace() { while (position < text.Length && char.IsWhiteSpace(text[position])) position++; }

         private bool Accept(string token) {
            SkipSpace();
            if (string.CompareOrdinal(text, position, token, 0, token.Length) == 0) { position += token.Length; return true; }
            return false;
         }

         private long ParseOr() {
            var left = ParseXor();
            while (true) {
               SkipSpace();
               if (position < text.Length && text[position] == '|' && !(position + 1 < text.Length && text[position + 1] == '|')) { position++; left |= ParseXor(); } else return left;
            }
         }
         private long ParseXor() {
            var left = ParseAnd();
            while (Accept("^")) left ^= ParseAnd();
            return left;
         }
         private long ParseAnd() {
            var left = ParseShift();
            while (true) {
               SkipSpace();
               if (position < text.Length && text[position] == '&' && !(position + 1 < text.Length && text[position + 1] == '&')) { position++; left &= ParseShift(); } else return left;
            }
         }
         private long ParseShift() {
            var left = ParseAdditive();
            while (true) {
               if (Accept("<<")) left <<= (int)ParseAdditive();
               else if (Accept(">>")) left >>= (int)ParseAdditive();
               else return left;
            }
         }
         private long ParseAdditive() {
            var left = ParseMultiplicative();
            while (true) {
               if (Accept("+")) left += ParseMultiplicative();
               else if (Accept("-")) left -= ParseMultiplicative();
               else return left;
            }
         }
         private long ParseMultiplicative() {
            var left = ParseUnary();
            while (true) {
               if (Accept("*")) left *= ParseUnary();
               else if (Accept("/")) { var right = ParseUnary(); if (right == 0) throw new InvalidOperationException("Division by zero."); left /= right; }
               else if (Accept("%")) { var right = ParseUnary(); if (right == 0) throw new InvalidOperationException("Division by zero."); left %= right; }
               else return left;
            }
         }
         private long ParseUnary() {
            if (Accept("-")) return -ParseUnary();
            if (Accept("+")) return ParseUnary();
            if (Accept("~")) return ~ParseUnary();
            if (Accept("!")) return ParseUnary() == 0 ? 1 : 0;
            if (Accept("(")) {
               var value = ParseOr();
               if (!Accept(")")) throw new InvalidOperationException($"Missing ')' in expression '{text}'.");
               return value;
            }
            SkipSpace();
            int start = position;
            if (position < text.Length && (char.IsLetterOrDigit(text[position]) || text[position] == '_' || text[position] == '.' || text[position] == '$' || text[position] == '\'')) {
               if (text[position] == '\'') {
                  if (position + 2 < text.Length && text[position + 2] == '\'') { position += 3; return text[start + 1]; }
               }
               while (position < text.Length && (char.IsLetterOrDigit(text[position]) || text[position] == '_' || text[position] == '.' || text[position] == '$')) position++;
               var token = text.Substring(start, position - start);
               if (char.IsDigit(token[0])) {
                  if (TryParseNumber(token, out var number)) return number;
                  throw new InvalidOperationException($"'{token}' is not a valid number.");
               }
               return resolve(token);
            }
            throw new InvalidOperationException(position >= text.Length ? $"Expression '{text}' ended unexpectedly." : $"Unexpected '{text[position]}' in expression '{text}'.");
         }
      }

      #endregion
   }
}
