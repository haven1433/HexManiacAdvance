using System;
using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.Models {
   /// <summary>
   /// A very small Thumb reader, just enough for the Intro Manager to find the numbers that the game's intro code uses
   /// (the Pokémon of the speech, the songs...) without knowing where the code is: it follows calls and looks at how a register is filled before a call.
   /// Everything here works on the raw bytes of the ROM and never throws for addresses outside of it.
   /// </summary>
   public static class IntroThumb {
      public const int RomBase = 0x08000000;

      public static int Half(byte[] data, int address) {
         if (address < 0 || address + 1 >= data.Length) return 0;
         return data[address] | (data[address + 1] << 8);
      }

      public static int Word(byte[] data, int address) {
         if (address < 0 || address + 3 >= data.Length) return 0;
         return data[address] | (data[address + 1] << 8) | (data[address + 2] << 16) | (data[address + 3] << 24);
      }

      /// <summary>The destination of the 32-bit BL instruction at an address, or false if there is no BL there.</summary>
      public static bool TryBranchLink(byte[] data, int address, out int target) {
         target = -1;
         if (address < 0 || address + 3 >= data.Length) return false;
         var high = Half(data, address);
         if ((high & 0xF800) != 0xF000) return false;
         var low = Half(data, address + 2);
         if ((low & 0xF800) != 0xF800) return false;
         var offset = ((high & 0x7FF) << 12) | ((low & 0x7FF) << 1);
         if ((offset & 0x400000) != 0) offset -= 0x800000;
         target = address + 4 + offset;
         return true;
      }

      /// <summary>The address of the literal that the 'ldr rX, [pc, #imm]' at an address loads, or -1 if the instruction is something else.</summary>
      public static int LiteralAddress(byte[] data, int address) {
         var half = Half(data, address);
         if ((half & 0xF800) != 0x4800) return -1;
         return ((address + 4) & ~3) + (half & 0xFF) * 4;
      }

      /// <summary>The 'ldr rX, [pc, #imm]' instructions (up to 1 KB before the literal) that load the literal at an address.</summary>
      public static List<int> FindLiteralLoads(byte[] data, int literalAddress) {
         var result = new List<int>();
         for (int address = Math.Max(0, literalAddress - 1024); address < literalAddress; address += 2) {
            if (LiteralAddress(data, address) == literalAddress) result.Add(address);
         }
         return result;
      }

      /// <summary>
      /// The first address of the function that contains an address: the closest 'push {..., lr}' before it.
      /// A compiler that has to save the high registers pushes twice (push {r4-r7, lr}, mov..., push {r7, lr}): then the first push counts.
      /// Returns -1 if there is none within 4 KB.
      /// </summary>
      public static int FunctionStart(byte[] data, int address) {
         address &= ~1;
         for (int p = address; p >= 0x200 && p > address - 0x1000; p -= 2) {
            if ((Half(data, p) & 0xFF00) != 0xB500) continue;
            while (true) {
               var earlier = -1;
               foreach (var back in new[] { 2, 4, 6, 8 }) {
                  if ((Half(data, p - back) & 0xFF00) == 0xB500) { earlier = p - back; break; }
               }
               if (earlier < 0) return p;
               p = earlier;
            }
         }
         return -1;
      }

      /// <summary>
      /// What an instruction does: the registers it reads and writes (bit masks, bit 0 is r0) and whether it can change the flow of the program (branches, calls, returns).
      /// Returns false for an instruction that isn't understood.
      /// </summary>
      public static bool Describe(int half, out int reads, out int writes, out bool flow) {
         reads = writes = 0;
         flow = false;
         int t = half >> 11;
         int rd = half & 7, rs = (half >> 3) & 7, rn = (half >> 6) & 7, rd8 = (half >> 8) & 7;
         int list = half & 0xFF;
         switch (t) {
            case 0: case 1: case 2: // lsls, lsrs, asrs by an immediate
               reads = 1 << rs; writes = 1 << rd; return true;
            case 3: // add/sub
               if ((half & 0x0400) != 0) reads = 1 << rs; else reads = (1 << rs) | (1 << rn);
               writes = 1 << rd;
               return true;
            case 4: writes = 1 << rd8; return true;          // mov rd, #imm
            case 5: reads = 1 << rd8; return true;           // cmp rd, #imm
            case 6: case 7: reads = writes = 1 << rd8; return true; // add/sub rd, #imm
            case 8:
               if ((half & 0xFC00) == 0x4000) { // data processing
                  var op = (half >> 6) & 15;
                  if (op == 8 || op == 10 || op == 11) { reads = (1 << rd) | (1 << rs); return true; } // tst, cmp, cmn
                  if (op == 9 || op == 15) { reads = 1 << rs; writes = 1 << rd; return true; }          // neg, mvn
                  reads = (1 << rd) | (1 << rs); writes = 1 << rd; return true;
               }
               if ((half & 0xFC00) == 0x4400) { // high register operations
                  var op = (half >> 8) & 3;
                  var rdh = (half & 7) | ((half >> 4) & 8);
                  var rsh = (half >> 3) & 15;
                  if (op == 3) { reads = 1 << rsh; flow = true; return true; }                 // bx, blx
                  if (op == 1) { reads = (1 << rdh) | (1 << rsh); return true; }                // cmp
                  if (op == 2) { reads = 1 << rsh; writes = 1 << rdh; if (rdh == 15) flow = true; return true; } // mov (to pc: a jump)
                  reads = (1 << rdh) | (1 << rsh); writes = 1 << rdh;                           // add
                  if (rdh == 15) flow = true;
                  return true;
               }
               return false;
            case 9: writes = 1 << rd8; return true;          // ldr rd, [pc, #imm]
            case 10: // str, strh, strb with a register offset; ldrsb
               if (((half >> 9) & 7) == 3) { reads = (1 << rs) | (1 << rn); writes = 1 << rd; } else reads = (1 << rd) | (1 << rs) | (1 << rn);
               return true;
            case 11: reads = (1 << rs) | (1 << rn); writes = 1 << rd; return true; // ldr, ldrh, ldrb, ldrsh with a register offset
            case 12: case 14: case 16: reads = (1 << rd) | (1 << rs); return true;  // str, strb, strh with an immediate
            case 13: case 15: case 17: reads = 1 << rs; writes = 1 << rd; return true; // ldr, ldrb, ldrh with an immediate
            case 18: reads = 1 << rd8; return true;          // str rd, [sp, #imm]
            case 19: writes = 1 << rd8; return true;         // ldr rd, [sp, #imm]
            case 20: case 21: writes = 1 << rd8; return true; // add rd, pc/sp, #imm
            case 22: case 23:
               if ((half & 0xFF00) == 0xB000) return true;   // add sp, #imm
               if ((half & 0xFE00) == 0xB400) { reads = list; return true; } // push
               if ((half & 0xFE00) == 0xBC00) { writes = list; flow = (half & 0x100) != 0; return true; } // pop
               return false;
            case 24: reads = (1 << rd8) | list; writes = 1 << rd8; return true; // stmia
            case 25: reads = 1 << rd8; writes = (1 << rd8) | list; return true; // ldmia
            case 26: case 27: flow = true; return true;      // conditional branch (and svc)
            case 28: case 29: flow = true; return true;      // branch
            case 30: case 31: reads = writes = 0xF; flow = true; return true; // halves of a BL: calls clobber r0-r3
            default: return false;
         }
      }

      /// <summary>
      /// What register 'register' holds when the instruction at 'use' runs, if it is a plain constant: 'movs r, #imm' (optionally followed, anywhere before the use, by 'lsls r, r, #k' or 'adds r, #imm'),
      /// or 'ldr r, [pc, #imm]'. Looks back up to 'maxInstructions' instructions and gives up (null) at the first call, branch or anything it does not understand.
      /// </summary>
      public static IntroConstant FindConstant(byte[] data, int use, int register, int maxInstructions = 48) {
         if (register < 0 || register > 7) return null;
         var secondAddress = -1;
         var secondKind = IntroImmediate.SecondNone;
         int p = use - 2;
         for (int n = 0; n < maxInstructions && p >= 0; n++, p -= 2) {
            var half = Half(data, p);
            if (!Describe(half, out var reads, out var writes, out var flow)) return null;
            if (flow) return null;
            if ((writes & (1 << register)) != 0) {
               var type = half >> 11;
               if (type == 4 && ((half >> 8) & 7) == register) return new IntroImmediate(p, register, secondAddress, secondKind);
               if (type == 9 && ((half >> 8) & 7) == register) {
                  if (secondKind != IntroImmediate.SecondNone) return null;
                  var literal = LiteralAddress(data, p);
                  return literal < 0 ? null : new IntroLiteral(literal, p);
               }
               if (secondKind != IntroImmediate.SecondNone) return null;
               if (type == 0 && ((half >> 3) & 7) == register && (half & 7) == register && ((half >> 6) & 31) > 0) {
                  secondKind = IntroImmediate.SecondShift; secondAddress = p; continue;     // lsls r, r, #k
               }
               if (type == 6 && ((half >> 8) & 7) == register) {
                  secondKind = IntroImmediate.SecondAdd; secondAddress = p; continue;       // adds r, #imm
               }
               return null;
            }
            if (secondKind != IntroImmediate.SecondNone && (reads & (1 << register)) != 0) return null; // the half-built number is used: not safe to rewrite
         }
         return null;
      }
   }
}
