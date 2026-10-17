using System;
using System.IO;
using System.IO.Compression;

namespace HavenSoft.HexManiac.Core {
   public enum BpsError {
      None,
      /// <summary>The data does not start with the "BPS1" header, or is far too short to be a patch.</summary>
      NotBps,
      /// <summary>The patch file itself is damaged (its own CRC32 does not match, or an action is cut off / out of range).</summary>
      CorruptPatch,
      /// <summary>The input is not the file the patch was made for (different length).</summary>
      WrongSourceSize,
      /// <summary>The input is not the file the patch was made for (same length, different content).</summary>
      WrongSourceCrc,
      /// <summary>The patch ran, but the result is not what the patch author produced (should never happen with an intact patch).</summary>
      WrongTargetCrc,
   }

   public class BpsApplyResult {
      public BpsError Error { get; }
      public string Message { get; }
      public byte[] Target { get; }
      public bool Success => Error == BpsError.None;

      private BpsApplyResult(BpsError error, string message, byte[] target) => (Error, Message, Target) = (error, message, target);
      public static BpsApplyResult Ok(byte[] target) => new BpsApplyResult(BpsError.None, null, target);
      public static BpsApplyResult Fail(BpsError error, string message) => new BpsApplyResult(error, message, null);
   }

   /// <summary>The numbers stored in a BPS patch header/footer. Cheap to read, does not decode any action.</summary>
   public class BpsInfo {
      public long SourceSize { get; init; }
      public long TargetSize { get; init; }
      public uint SourceCrc32 { get; init; }
      public uint TargetCrc32 { get; init; }
      public uint PatchCrc32 { get; init; }
   }

   /// <summary>
   /// Decoder for byuu's BPS patch format ("BPS1").
   ///
   /// header:  "BPS1", varint sourceSize, varint targetSize, varint metadataSize, metadata bytes
   /// actions: varint ((length - 1) &lt;&lt; 2 | command)
   ///          0 SourceRead  copy 'length' bytes from the source at the current output offset
   ///          1 TargetRead  'length' literal bytes follow in the patch
   ///          2 SourceCopy  varint v: sourceRelativeOffset += (v &amp; 1 ? -1 : 1) * (v &gt;&gt; 1); copy from the source there (and advance)
   ///          3 TargetCopy  same, but copying from the already written part of the target (byte by byte, so ranges may overlap)
   /// footer:  three little-endian CRC32 values: source file, target file, and everything in the patch before the last 4 bytes.
   /// </summary>
   public static class BpsPatch {
      private const int FooterLength = 12;
      /// <summary>Safety valve so that a damaged header can not make us allocate gigabytes.</summary>
      public const long MaxTargetSize = 1L << 28;

      public static bool LooksLikeBps(byte[] patch) =>
         patch != null && patch.Length >= 4 + 3 + FooterLength && patch[0] == (byte)'B' && patch[1] == (byte)'P' && patch[2] == (byte)'S' && patch[3] == (byte)'1';

      /// <summary>Patches shipped inside the app are gzip-compressed; this returns the raw .bps bytes for either form.</summary>
      public static byte[] DecompressIfGzip(byte[] data) {
         if (data == null || data.Length < 2 || data[0] != 0x1F || data[1] != 0x8B) return data;
         using var input = new MemoryStream(data);
         using var gzip = new GZipStream(input, CompressionMode.Decompress);
         using var output = new MemoryStream();
         gzip.CopyTo(output);
         return output.ToArray();
      }

      /// <summary>Reads source/target sizes and checksums from the patch. Returns null if this is not a BPS patch.</summary>
      public static BpsInfo ReadInfo(byte[] patch) {
         if (!LooksLikeBps(patch)) return null;
         int footer = patch.Length - FooterLength;
         int position = 4;
         if (!TryReadNumber(patch, ref position, footer, out var sourceSize)) return null;
         if (!TryReadNumber(patch, ref position, footer, out var targetSize)) return null;
         return new BpsInfo {
            SourceSize = (long)sourceSize,
            TargetSize = (long)targetSize,
            SourceCrc32 = ReadUInt32(patch, footer),
            TargetCrc32 = ReadUInt32(patch, footer + 4),
            PatchCrc32 = ReadUInt32(patch, footer + 8),
         };
      }

      /// <summary>
      /// Applies a patch. The source array is never modified.
      /// The checks run in this order: header, patch checksum, source length, source checksum, actions, target checksum.
      /// </summary>
      public static BpsApplyResult Apply(byte[] patch, byte[] source) {
         if (patch == null) throw new ArgumentNullException(nameof(patch));
         if (source == null) throw new ArgumentNullException(nameof(source));
         if (!LooksLikeBps(patch)) return BpsApplyResult.Fail(BpsError.NotBps, "This is not a BPS patch (it does not start with 'BPS1').");

         int footer = patch.Length - FooterLength;
         uint expectedPatchCrc = ReadUInt32(patch, footer + 8);
         if (Crc32(patch, footer + 8) != expectedPatchCrc) {
            return BpsApplyResult.Fail(BpsError.CorruptPatch, "The patch file is damaged (its checksum does not match).");
         }

         int position = 4;
         if (!TryReadNumber(patch, ref position, footer, out var sourceSize) ||
             !TryReadNumber(patch, ref position, footer, out var targetSize) ||
             !TryReadNumber(patch, ref position, footer, out var metadataSize)) {
            return BpsApplyResult.Fail(BpsError.CorruptPatch, "The patch header is damaged.");
         }
         if (metadataSize > (ulong)(footer - position)) return BpsApplyResult.Fail(BpsError.CorruptPatch, "The patch header is damaged (metadata is cut off).");
         position += (int)metadataSize;
         if (targetSize > (ulong)MaxTargetSize) return BpsApplyResult.Fail(BpsError.CorruptPatch, "The patch header is damaged (implausible result size).");

         uint expectedSourceCrc = ReadUInt32(patch, footer);
         uint expectedTargetCrc = ReadUInt32(patch, footer + 4);
         if ((ulong)source.Length != sourceSize) {
            return BpsApplyResult.Fail(BpsError.WrongSourceSize, $"The input file has {source.Length:N0} bytes, but this patch is for a file with {sourceSize:N0} bytes.");
         }
         if (Crc32(source, source.Length) != expectedSourceCrc) {
            return BpsApplyResult.Fail(BpsError.WrongSourceCrc, "The input file is not the file this patch was made for (its checksum does not match).");
         }

         var target = new byte[(int)targetSize];
         int output = 0, sourceRelative = 0, targetRelative = 0;
         while (position < footer) {
            if (!TryReadNumber(patch, ref position, footer, out var data)) return Corrupt("an action is cut off");
            int command = (int)(data & 3);
            ulong lengthValue = (data >> 2) + 1;
            if (lengthValue > (ulong)(target.Length - output)) return Corrupt("an action writes past the end of the result");
            int length = (int)lengthValue;

            switch (command) {
               case 0: // SourceRead
                  if (output + length > source.Length) return Corrupt("a SourceRead action reads past the end of the input");
                  Buffer.BlockCopy(source, output, target, output, length);
                  break;
               case 1: // TargetRead
                  if (length > footer - position) return Corrupt("a TargetRead action is cut off");
                  Buffer.BlockCopy(patch, position, target, output, length);
                  position += length;
                  break;
               case 2: // SourceCopy
                  if (!TryReadNumber(patch, ref position, footer, out var sourceDelta)) return Corrupt("an action is cut off");
                  sourceRelative = AddOffset(sourceRelative, sourceDelta);
                  if (sourceRelative < 0 || sourceRelative > source.Length - length) return Corrupt("a SourceCopy action reads outside the input");
                  Buffer.BlockCopy(source, sourceRelative, target, output, length);
                  sourceRelative += length;
                  break;
               default: // TargetCopy
                  if (!TryReadNumber(patch, ref position, footer, out var targetDelta)) return Corrupt("an action is cut off");
                  targetRelative = AddOffset(targetRelative, targetDelta);
                  if (targetRelative < 0 || targetRelative >= output) return Corrupt("a TargetCopy action reads data that was not written yet");
                  if (output - targetRelative >= length) {
                     Buffer.BlockCopy(target, targetRelative, target, output, length);
                  } else {
                     // ranges overlap: the copy repeats what it just wrote (this is how BPS stores runs), so it must go byte by byte
                     for (int i = 0; i < length; i++) target[output + i] = target[targetRelative + i];
                  }
                  targetRelative += length;
                  break;
            }
            output += length;
         }

         if (output != target.Length) return Corrupt("the actions do not fill the result");
         if (Crc32(target, target.Length) != expectedTargetCrc) {
            return BpsApplyResult.Fail(BpsError.WrongTargetCrc, "The patched result failed its checksum.");
         }
         return BpsApplyResult.Ok(target);
      }

      private static BpsApplyResult Corrupt(string detail) => BpsApplyResult.Fail(BpsError.CorruptPatch, $"The patch file is damaged ({detail}).");

      /// <summary>Same number encoding as UPS: 7 bits per byte, the last byte has bit 7 set, every continuation adds one.</summary>
      private static bool TryReadNumber(byte[] data, ref int position, int end, out ulong result) {
         result = 0;
         ulong shift = 1;
         while (true) {
            if (position >= end) return false;
            byte x = data[position++];
            result += (ulong)(x & 0x7F) * shift;
            if ((x & 0x80) != 0) return true;
            shift <<= 7;
            if (shift > (1UL << 56)) return false; // far beyond any file we could handle
            result += shift;
         }
      }

      private static int AddOffset(int current, ulong encodedDelta) {
         long delta = (long)(encodedDelta >> 1);
         if ((encodedDelta & 1) != 0) delta = -delta;
         long result = current + delta;
         if (result < int.MinValue || result > int.MaxValue) return -1;
         return (int)result;
      }

      private static uint ReadUInt32(byte[] data, int index) =>
         (uint)(data[index] | (data[index + 1] << 8) | (data[index + 2] << 16) | (data[index + 3] << 24));

      /// <summary>CRC32 of the first 'length' bytes.</summary>
      private static uint Crc32(byte[] data, int length) {
         if (length != data.Length) {
            var copy = new byte[length];
            Buffer.BlockCopy(data, 0, copy, 0, length);
            data = copy;
         }
         return (uint)Patcher.CalcCRC32(data);
      }
   }
}
