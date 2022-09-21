using HavenSoft.HexManiac.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.HexManiac.Core {
   public static class Patcher {
      #region IPS Patching

      public static byte[] BuildIpsPatch(byte[] source, byte[] destination) {
         var patch = new List<byte>();
         if (source.Length < destination.Length) {
            source = source.Concat(new byte[destination.Length - source.Length]).ToArray();
         }

         // header
         patch.AddRange("PATCH".Select(c => (byte)c));

         // body
         for (int i = 0; i < destination.Length; i++) {
            if (source[i] == destination[i]) continue;
            int j = i + 1;
            while (j < destination.Length && source[j] != destination[j] && j - i < 0xFFFF) j++;
            patch.AddRange(BuildChunk(destination, i, j - i));
            if (source[j] != destination[j]) j -= 1; // need to re-check this byte, we exited due to the length limit
            i = j;
         }

         // footer
         patch.AddRange("EOF".Select(c => (byte)c));

         return patch.ToArray();
      }

      public static byte[] BuildChunk(byte[] data, int start, int length) {
         var chunk = new List<byte>();
         bool compressed = length > 4 && length.Range().All(i => data[start + i] == data[start]);

         // offset
         chunk.Add((byte)(start >> 16));
         chunk.Add((byte)(start >> 8));
         chunk.Add((byte)start);

         // compressed content
         if (compressed) {
            chunk.AddRange(new byte[] { 0, 0, (byte)(length >> 8), (byte)length, data[start] });
            return chunk.ToArray();
         }

         // normal content
         chunk.AddRange(new byte[] { (byte)(length >> 8), (byte)length });
         chunk.AddRange(length.Range().Select(i => data[start + i]));

         return chunk.ToArray();
      }

      /// <returns>The first offset that was edited</returns>
      public static int ApplyIPSPatch(IDataModel model, byte[] patch, ModelDelta token) {
         // 5 byte header (PATCH) and 3 byte footer (EOF)
         // hunk type 1: offset (3 bytes), length (2 bytes), payload (length bytes). Write the payload at offset.
         // RLE hunk:    offset (3 bytes), 00 00, length (2 bytes), target (1 byte). Write the target, length times, at offset

         var start = 5;
         var firstOffset = -1;

         while (patch.Length - start >= 6) {
            var offset = (patch[start] << 16) + (patch[start + 1] << 8) + patch[start + 2];
            if (firstOffset < 0) firstOffset = offset;
            start += 3;
            var length = (patch[start] << 8) + patch[start + 1];
            start += 2;
            if (length > 0) {
               // normal
               model.ExpandData(token, offset + length - 1);
               while (length > 0) {
                  token.ChangeData(model, offset, patch[start]);
                  offset += 1;
                  start += 1;
                  length -= 1;
               }
            } else {
               length = (patch[start] << 8) + patch[start + 1];
               start += 2;
               model.ExpandData(token, offset + length - 1);
               // rle
               while (length > 0) {
                  token.ChangeData(model, offset, patch[start]);
                  offset += 1;
                  length -= 1;
               }
               start += 1;
            }
         }

         return firstOffset;
      }

      #endregion

      #region UPS Patching

      public enum UpsPatchDirection { Fail, SourceToDestination, DestinationToSource }

      public static byte[] BuildUpsPatch(byte[] source, byte[] destination) {
         var patch = new List<byte>();

         // header
         patch.AddRange("UPS1".Select(c => (byte)c));
         patch.AddRange(WriteVariableWidthInteger(source.Length));
         patch.AddRange(WriteVariableWidthInteger(destination.Length));

         // body
         int offset = 0;
         bool writing = false;
         for (int i = 0; i < destination.Length; i++) {
            var sourceVal = source.Length > i ? source[i] : (byte)0;
            if (sourceVal == destination[i]) {
               if (writing) {
                  writing = false;
                  offset = i + 1;
                  patch.Add(0);
               }
               continue;
            }
            if (!writing) {
               patch.AddRange(WriteVariableWidthInteger(i - offset));
               writing = true;
            }
            patch.Add((byte)(sourceVal ^ destination[i]));
         }
         if (writing) patch.Add(0);

         // footer
         patch.AddRange(BitConverter.GetBytes(CalcCRC32(source)));
         patch.AddRange(BitConverter.GetBytes(CalcCRC32(destination)));
         patch.AddRange(BitConverter.GetBytes(CalcCRC32(patch.ToArray())));

         return patch.ToArray();
      }

      // return -1 if the header is wrong (UPS1)
      // return -2 if the source file CRC doesn't match
      // return -3 if the patch file CRC doesn't match
      // return -4 if the source file size doesn't match
      // return -5 if the result file CRC doesn't match
      // return -6 if the UPS content didn't finish the last chunk with exactly 12 bytes left
      // return -7 if trying to write past the end of the destination file
      // returns a positive integer, the address of the first change, if everything worked correctly
      public static int ApplyUPSPatch(IDataModel model, byte[] patch, Func<ModelDelta> tokenFactory, bool ignoreChecksums, out UpsPatchDirection direction) {
         // 4 byte header: "UPS1"
         // variable width source-size
         // variable width destination-size
         // 12 byte footer: 3 CRC32 checksums. Source file, destination file, patch file (CRC of everything except the last 4 bytes)
         direction = UpsPatchDirection.Fail;

         // check header
         var headerMatches = patch.Take(4).Select(b => (char)b).SequenceEqual("UPS1");
         if (!headerMatches) return -1;

         // check source CRC
         var currentCRC = CalcCRC32(model.RawData);
         var patchSourceFileCRC = patch.ReadMultiByteValue(patch.Length - 12, 4);
         var patchDestinationFileCRC = patch.ReadMultiByteValue(patch.Length - 8, 4);
         if (currentCRC == patchSourceFileCRC) direction = UpsPatchDirection.SourceToDestination;
         if (currentCRC == patchDestinationFileCRC) direction = UpsPatchDirection.DestinationToSource;
         if (direction == UpsPatchDirection.Fail && !ignoreChecksums) return -2;
         if (direction == UpsPatchDirection.Fail) direction = UpsPatchDirection.SourceToDestination;

         // check patch CRC
         var patchWithoutCRC = new byte[patch.Length - 4];
         Array.Copy(patch, patchWithoutCRC, patchWithoutCRC.Length);
         var patchCRC = CalcCRC32(patchWithoutCRC);
         if (patchCRC != patch.ReadMultiByteValue(patch.Length - 4, 4)) return -3;

         // resize (bigger)
         int readIndex = 4, firstEdit = int.MaxValue;
         int sourceSize = ReadVariableWidthInteger(patch, ref readIndex);
         int destinationSize = ReadVariableWidthInteger(patch, ref readIndex);
         int writeLength = destinationSize;
         if (direction == UpsPatchDirection.DestinationToSource) (sourceSize, destinationSize) = (destinationSize, sourceSize);
         if (sourceSize != model.Count && !ignoreChecksums) return -4;
         var token = tokenFactory.Invoke();
         model.ExpandData(token, destinationSize - 1);
         token.ChangeData(model, sourceSize, new byte[Math.Max(0, destinationSize - sourceSize)]);
         token.ChangeData(model, destinationSize, new byte[Math.Max(0, sourceSize - destinationSize)]);

         // run algorithm
         firstEdit = RunUPSPatchAlgorithm(model, patch, token, writeLength, destinationSize, ref readIndex);
         if (firstEdit < 0) return firstEdit;

         // resize (smaller)
         model.ContractData(token, destinationSize - 1);

         // check result CRC
         if (!ignoreChecksums) {
            var finalCRC = CalcCRC32(model.RawData);
            if (direction == UpsPatchDirection.SourceToDestination && finalCRC != patchDestinationFileCRC) return -5;
            if (direction == UpsPatchDirection.DestinationToSource && finalCRC != patchSourceFileCRC) return -5;
         }

         // check that the chunk ended cleanly
         if (direction == UpsPatchDirection.SourceToDestination && readIndex != patch.Length - 12) return -6;

         return firstEdit;
      }

      public static int CalcCRC32(byte[] array) => (int)Force.Crc32.Crc32Algorithm.Compute(array);

      public static int ReadVariableWidthInteger(byte[] data, ref int index) {
         int result = 0, shift = 0;
         while (true) {
            result += (data[index] & 0x7F) << shift;
            if (shift != 0) result += 1 << shift;
            shift += 7;
            index += 1;
            if ((data[index - 1] & 0x80) != 0) {
               return result;
            }
         }
      }

      public static IEnumerable<byte> WriteVariableWidthInteger(int value) {
         do {
            var payload = value & 0x7F;
            value >>= 7;
            if (value == 0) payload |= 0x80;
            value -= 1;
            yield return (byte)payload;
         } while (value >= 0);
      }

      private static int RunUPSPatchAlgorithm(IDataModel model, byte[] patch, ModelDelta token, int writeLength, int destinationSize, ref int readIndex) {
         int writeIndex = 0;
         int firstEdit = int.MaxValue;
         while (readIndex < patch.Length - 12 && writeIndex < destinationSize) {
            var skipSize = ReadVariableWidthInteger(patch, ref readIndex);
            writeIndex += skipSize;
            if (writeIndex > writeLength) return -7;
            if (firstEdit == int.MaxValue) firstEdit = skipSize;

            while (patch[readIndex] != 0 && writeIndex < destinationSize) {
               token.ChangeData(model, writeIndex, (byte)(patch[readIndex] ^ model[writeIndex]));
               readIndex += 1;
               writeIndex += 1;
               if (writeIndex > writeLength) return -7;
            }
            readIndex += 1;
            writeIndex += 1;
         }

         return firstEdit;
      }

      #endregion
   }
}
