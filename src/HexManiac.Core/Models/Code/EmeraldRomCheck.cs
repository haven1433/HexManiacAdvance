using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace HavenSoft.HexManiac.Core {
   /// <summary>Describes the one ROM image a patch was made for: how long it is, what its game code is, and its SHA-1.</summary>
   public class VanillaRomSpec {
      public string Name { get; init; }
      public int Length { get; init; }
      public string GameCode { get; init; }
      /// <summary>Lowercase hex, 40 characters.</summary>
      public string Sha1 { get; init; }
      /// <summary>
      /// CRC32 of the whole file, or 0 if it is not known. Together with the length and the game code this recognises the ROM in a few
      /// milliseconds (the SHA-1 takes a tenth of a second), which matters for a menu item that is asked about every tab each time the tab changes.
      /// </summary>
      public uint Crc32 { get; init; }

      /// <summary>Pokemon Emerald, USA/Europe, v1.0. The only English Emerald release that exists.</summary>
      public static VanillaRomSpec EmeraldUsV10 { get; } = new VanillaRomSpec {
         Name = "Pokemon Emerald (USA, Europe)",
         Length = 0x1000000,
         GameCode = "BPEE",
         Sha1 = "f3ae088181bf583e55daf962a92bb46f4f1d07b7",
         Crc32 = 0x1F1C08FB,
      };
   }

   public enum RomCheckKind {
      /// <summary>Exactly the expected ROM.</summary>
      Vanilla,
      /// <summary>Not a GBA ROM at all (too small to hold a header).</summary>
      NotARom,
      /// <summary>A different game, or a different language/region of the same game.</summary>
      WrongGame,
      /// <summary>Right game, but the file does not have the length of the original ROM (expanded, trimmed or with a copier header).</summary>
      WrongSize,
      /// <summary>Right game and length, but the content is not the original (hacked, edited, or a different dump).</summary>
      Modified,
      /// <summary>The ROM is exactly the result of the patch: it already is CUBE.</summary>
      AlreadyPatched,
   }

   public class RomCheckResult {
      public RomCheckKind Kind { get; }
      public string Message { get; }
      public bool IsVanilla => Kind == RomCheckKind.Vanilla;
      public RomCheckResult(RomCheckKind kind, string message) => (Kind, Message) = (kind, message);
   }

   public static class EmeraldRomCheck {
      private const int GameCodeStart = 0xAC, MinimumLength = 0xC0;
      private const int CopierHeaderLength = 0x200;

      public static string ComputeSha1(byte[] data) {
         using var sha = SHA1.Create();
         var hash = sha.ComputeHash(data);
         var text = new StringBuilder(hash.Length * 2);
         foreach (var b in hash) text.Append(b.ToString("x2"));
         return text.ToString();
      }

      public static string ReadGameCode(byte[] rom) {
         if (rom == null || rom.Length < MinimumLength) return string.Empty;
         var code = new char[4];
         for (int i = 0; i < 4; i++) {
            var b = rom[GameCodeStart + i];
            code[i] = b >= 0x20 && b < 0x7F ? (char)b : '?';
         }
         return new string(code);
      }

      /// <param name="rom">The file contents to inspect.</param>
      /// <param name="spec">The ROM that is acceptable as input.</param>
      /// <param name="patched">If known (from the patch footer), the length and CRC32 of the ROM the patch produces. Used to say "this already is CUBE".</param>
      public static RomCheckResult Check(byte[] rom, VanillaRomSpec spec, BpsInfo patched = null) {
         if (rom == null || rom.Length < MinimumLength) {
            return new RomCheckResult(RomCheckKind.NotARom, "This file is too small to be a GBA ROM.");
         }

         var code = ReadGameCode(rom);
         if (code != spec.GameCode) {
            return new RomCheckResult(RomCheckKind.WrongGame, DescribeWrongGame(code, spec));
         }

         if (patched != null && rom.Length == patched.TargetSize && (uint)Patcher.CalcCRC32(rom) == patched.TargetCrc32) {
            return new RomCheckResult(RomCheckKind.AlreadyPatched,
               "This ROM already is CUBE (it is identical to the ROM this patch builds), so there is nothing to apply.");
         }

         if (rom.Length != spec.Length) {
            string why;
            if (patched != null && rom.Length == patched.TargetSize) {
               why = $"It is {Megabytes(rom.Length)} like CUBE, so it is probably already CUBE, or a ROM built on top of it.";
            } else if (rom.Length > spec.Length && rom.Length != spec.Length + CopierHeaderLength) {
               why = $"It is bigger than the original ({Megabytes(spec.Length)}), so it has been expanded - maybe it already is CUBE or another hack.";
            } else if (rom.Length == spec.Length + CopierHeaderLength) {
               why = "It is 512 bytes longer than the original ROM, which looks like a copier header. Remove the header first.";
            } else {
               why = $"It is {rom.Length:N0} bytes long, the original is {spec.Length:N0} bytes.";
            }
            return new RomCheckResult(RomCheckKind.WrongSize,
               $"This is a {code} ROM, but not an unmodified {spec.Name}." + Environment.NewLine + why + Environment.NewLine +
               "CUBE can only be applied to an original, unmodified ROM.");
         }

         var sha1 = ComputeSha1(rom);
         if (!string.Equals(sha1, spec.Sha1, StringComparison.OrdinalIgnoreCase)) {
            return new RomCheckResult(RomCheckKind.Modified,
               $"This ROM has the right game code and length, but it is not the original {spec.Name}: it has been modified, or it is a different dump." + Environment.NewLine +
               $"Its SHA-1 is {sha1}, the original's is {spec.Sha1}." + Environment.NewLine +
               "If you edited it in HexManiac, use a fresh copy of the original ROM.");
         }

         return new RomCheckResult(RomCheckKind.Vanilla, null);
      }

      private static string DescribeWrongGame(string code, VanillaRomSpec spec) {
         string what;
         switch (code) {
            case "BPRE": what = "Pokemon FireRed (USA)"; break;
            case "BPGE": what = "Pokemon LeafGreen (USA)"; break;
            case "AXVE": what = "Pokemon Ruby (USA)"; break;
            case "AXPE": what = "Pokemon Sapphire (USA)"; break;
            default: what = null; break;
         }
         if (what != null) return $"This is {what} (game code {code}), not {spec.Name} (game code {spec.GameCode}).";
         if (code.Length == 4 && code.StartsWith(spec.GameCode.Substring(0, 3), StringComparison.Ordinal)) {
            return $"This looks like a different language or region of Pokemon Emerald (game code {code}). CUBE needs {spec.Name} (game code {spec.GameCode}).";
         }
         if (string.IsNullOrEmpty(code)) return $"This is not a GBA ROM. CUBE needs {spec.Name} (game code {spec.GameCode}).";
         return $"This is not {spec.Name}: its game code is {code}, not {spec.GameCode}.";
      }

      private static string Megabytes(long length) =>
         length >= (1 << 20) ? $"{(length / (double)(1 << 20)).ToString("0.#", CultureInfo.InvariantCulture)} MB" : $"{length:N0} bytes";
   }
}
