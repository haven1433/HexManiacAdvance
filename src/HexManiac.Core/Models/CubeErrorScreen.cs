using System;
using System.Collections.Generic;
using System.Text;

namespace HavenSoft.HexManiac.Core.Models {
   /// <summary>
   /// CUBE (pokeemerald-expansion) checks its data while the game runs and shows a blue error screen when a check fails
   /// ("invalid ability num 4 for species 25", "INVALID ITEM", ...). The checks are meant for the people who build the game from source:
   /// a hacked ROM (a randomizer, data edited by hand) trips them for data the game can play with perfectly well.
   ///
   /// The screen is drawn by one function (src/assertf.c). It is reached through two small wrappers: one for the checks the game can go on after
   /// (level 0, nearly 1500 of them) and one for failures it can't survive (level 1: out of memory and the like, a handful). This finds the level-0 wrapper
   /// by its shape and makes it return at once, so the checks still run but nothing is shown and the game simply goes on. The level-1 wrapper stays.
   /// Nothing here knows an address: it is found again in every CUBE build. Apply CUBE and Update CUBE do this to every ROM they make.
   /// </summary>
   public static class CubeErrorScreen {
      private static readonly byte[] sourceFile = Encoding.ASCII.GetBytes("src/assertf.c:");
      private const int RomBase = 0x08000000;
      private const ushort PushR1R2R3 = 0xB40E, BxLr = 0x4770;

      private static int Half(byte[] data, int p) => data[p] | (data[p + 1] << 8);
      private static int Word(byte[] data, int p) => data[p] | (data[p + 1] << 8) | (data[p + 2] << 16) | (data[p + 3] << 24);

      /// <summary>The address of the wrapper that shows the screen for the checks the game can go on after, or -1 when this is not a ROM with that code.</summary>
      public static int Find(byte[] data) {
         if (data == null || data.Length < 0x1000) return -1;
         var drawer = FindDrawer(data);
         if (drawer < 0) return -1;
         var wrappers = new List<int>();
         for (int p = 0x200; p < data.Length - 4; p += 2) {
            // a call to the start of the drawing function: a 'push {..., lr}' a little before the instruction that reads its text
            if (!TryBranchLink(data, p, out var target) || target > drawer || drawer - target > 0x40 || (Half(data, target) & 0xFF00) != 0xB500) continue;
            // the wrapper: push {r1,r2,r3} ; push {lr} ; ... ; movs r0, #level ; ... ; bl drawer
            int level = -1, start = -1;
            for (int q = p - 2; q >= p - 0x20 && q >= 0; q -= 2) {
               var half = Half(data, q);
               if ((half & 0xFF00) == 0x2000 && level < 0) level = half & 0xFF; // movs r0, #imm
               if (half == PushR1R2R3 || half == BxLr) { start = q; break; } // or the wrapper as this leaves it
            }
            if (start >= 0 && level == 0) wrappers.Add(start);
         }
         return wrappers.Count == 1 ? wrappers[0] : -1;
      }

      /// <summary>The function that draws the screen is the one that loads the pointer to its own "src/assertf.c:" text: this is the address of that load.</summary>
      private static int FindDrawer(byte[] data) {
         var text = new ReadOnlySpan<byte>(data).IndexOf(new ReadOnlySpan<byte>(sourceFile));
         if (text < 0) return -1;
         // the literal that points at the text
         for (int lit = 0x200; lit < data.Length - 4; lit += 4) {
            if (Word(data, lit) != RomBase + text) continue;
            // the ldr rX, [pc, #n] that reads the literal
            for (int p = Math.Max(0x200, lit - 1024); p < lit; p += 2) {
               var half = Half(data, p);
               if ((half & 0xF800) == 0x4800 && ((p + 4) & ~3) + (half & 0xFF) * 4 == lit) return p;
            }
         }
         return -1;
      }

      private static bool TryBranchLink(byte[] data, int p, out int target) {
         target = -1;
         var h1 = Half(data, p); var h2 = Half(data, p + 2);
         if ((h1 & 0xF800) != 0xF000 || (h2 & 0xF800) != 0xF800) return false;
         var offset = ((h1 & 0x7FF) << 12) | ((h2 & 0x7FF) << 1);
         if ((offset & 0x400000) != 0) offset -= 0x800000;
         target = p + 4 + offset;
         return target >= 0x200 && target < data.Length - 2;
      }

      public static bool IsDisabled(byte[] data, int wrapper) => wrapper >= 0 && Half(data, wrapper) == BxLr;

      /// <summary>Makes the wrapper return at once. Returns false when the ROM has no such code or it is disabled already.</summary>
      public static bool Disable(IDataModel model, ModelDelta token) {
         var wrapper = Find(model.RawData);
         if (wrapper < 0 || IsDisabled(model.RawData, wrapper)) return false;
         model.WriteMultiByteValue(wrapper, 2, token, BxLr);
         return true;
      }

      /// <summary>The same on plain bytes (for a ROM that is being built).</summary>
      public static bool Disable(byte[] data) {
         var wrapper = Find(data);
         if (wrapper < 0 || IsDisabled(data, wrapper)) return false;
         data[wrapper] = unchecked((byte)BxLr); data[wrapper + 1] = unchecked((byte)(BxLr >> 8));
         return true;
      }
   }
}
