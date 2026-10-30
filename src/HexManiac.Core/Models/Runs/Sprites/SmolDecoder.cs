using System;
using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.Models.Runs.Sprites {
   /// <summary>
   /// Reads the 'smol' compression of the pokeemerald-expansion (graphics/*.smol, see tools/compresSmol and src/decompress.c there).
   /// HexManiac only reads it: edited graphics are written back as plain LZ77, which the expansion's DecompressDataWithHeader also accepts.
   /// </summary>
   public static class SmolDecoder {
      private const int TableSize = 64;
      public const int ImageSizeMultiplier = 4;

      private const int BaseOnly = 1, EncodeSyms = 2, EncodeDeltaSyms = 3, EncodeLo = 4, EncodeBoth = 5, EncodeBothDeltaSyms = 6;

      private static bool IsLoEncoded(int mode) => mode == EncodeLo || mode == EncodeBoth || mode == EncodeBothDeltaSyms;
      private static bool IsSymEncoded(int mode) => mode == EncodeSyms || mode == EncodeDeltaSyms || mode == EncodeBoth || mode == EncodeBothDeltaSyms;
      private static bool IsSymDelta(int mode) => mode == EncodeDeltaSyms || mode == EncodeBothDeltaSyms;

      /// <summary>Cheap test of the first byte: the mode is in the low nibble, 0 is plain LZ77, 7 and 8 are frame containers and tilemaps (not handled).</summary>
      public static bool LooksLikeSmol(IReadOnlyList<byte> data, int start) {
         if (start < 0 || start + 8 > data.Count) return false;
         var mode = data[start] & 0xF;
         return mode >= BaseOnly && mode <= EncodeBothDeltaSyms;
      }

      /// <summary>The size of the decompressed data according to the header, or -1 if this is not a smol header.</summary>
      public static int GetDecompressedLength(IReadOnlyList<byte> data, int start) {
         if (!LooksLikeSmol(data, start)) return -1;
         var word0 = ReadWord(data, start);
         return (int)((word0 >> 4) & 0x3FFF) * ImageSizeMultiplier;
      }

      /// <returns>The length of the compressed data (a multiple of 4), or -1 if it is not valid smol data.</returns>
      public static int GetCompressedLength(IReadOnlyList<byte> data, int start) => TryDecompress(data, start, out _, out var length) ? length : -1;

      public static byte[] Decompress(IReadOnlyList<byte> data, int start) => TryDecompress(data, start, out var result, out _) ? result : null;

      public static bool TryDecompress(IReadOnlyList<byte> data, int start, out byte[] result, out int compressedLength) {
         result = null;
         compressedLength = -1;
         try {
            return TryDecompressUnsafe(data, start, out result, out compressedLength);
         } catch (IndexOutOfRangeException) {
            result = null;
            return false;
         } catch (ArgumentOutOfRangeException) {
            result = null;
            return false;
         }
      }

      private static bool TryDecompressUnsafe(IReadOnlyList<byte> data, int start, out byte[] result, out int compressedLength) {
         result = null;
         compressedLength = -1;
         if (!LooksLikeSmol(data, start)) return false;
         var word0 = ReadWord(data, start);
         var word1 = ReadWord(data, start + 4);
         int mode = (int)(word0 & 0xF);
         int imageBytes = (int)((word0 >> 4) & 0x3FFF) * ImageSizeMultiplier;
         int symSize = (int)((word0 >> 18) & 0x3FFF);
         int initialState = (int)(word1 & 0x3F);
         int bitstreamSize = (int)((word1 >> 6) & 0x1FFF);
         int loSize = (int)((word1 >> 19) & 0x1FFF);
         if (imageBytes == 0 || symSize == 0 || loSize == 0) return false;

         bool loEncoded = IsLoEncoded(mode), symEncoded = IsSymEncoded(mode);
         var position = start + 8;
         int[] loFreqs = null, symFreqs = null;
         if (loEncoded) { loFreqs = UnpackFrequencies(data, position); position += 12; }
         if (symEncoded) { symFreqs = UnpackFrequencies(data, position); position += 12; }
         if (loFreqs != null && !FrequenciesAreValid(loFreqs)) return false;
         if (symFreqs != null && !FrequenciesAreValid(symFreqs)) return false;

         int bitCount = 0;
         uint[] bits = null;
         if (loEncoded || symEncoded) {
            bits = new uint[bitstreamSize];
            for (int i = 0; i < bitstreamSize; i++) bits[i] = ReadWord(data, position + i * 4);
            position += bitstreamSize * 4;
            bitCount = bitstreamSize * 32;
         }

         var loVec = new byte[loSize];
         var symVec = new ushort[symSize];
         int bitIndex = 0, state = initialState;
         if (loEncoded) {
            var table = BuildTable(loFreqs);
            var nibbles = new byte[loSize * 2];
            if (!DecodeNibbles(table, bits, bitCount, ref state, nibbles, ref bitIndex, last: !symEncoded)) return false;
            for (int i = 0; i < loSize; i++) loVec[i] = (byte)(nibbles[2 * i] + (nibbles[2 * i + 1] << 4));
         }
         if (symEncoded) {
            var table = BuildTable(symFreqs);
            var nibbles = new byte[symSize * 4];
            if (!DecodeNibbles(table, bits, bitCount, ref state, nibbles, ref bitIndex, last: true)) return false;
            if (IsSymDelta(mode)) {
               int previous = 0;
               for (int i = 0; i < nibbles.Length; i++) { previous = (nibbles[i] + previous) & 0xF; nibbles[i] = (byte)previous; }
            }
            for (int i = 0; i < symSize; i++) symVec[i] = (ushort)(nibbles[i * 4] | (nibbles[i * 4 + 1] << 4) | (nibbles[i * 4 + 2] << 8) | (nibbles[i * 4 + 3] << 12));
         } else {
            for (int i = 0; i < symSize; i++) symVec[i] = (ushort)(data[position + 2 * i] | (data[position + 2 * i + 1] << 8));
            position += symSize * 2;
         }
         if (!loEncoded) {
            for (int i = 0; i < loSize; i++) loVec[i] = data[position + i];
            position += loSize;
         }

         var output = new byte[imageBytes];
         if (!DecodeInstructions(loVec, symVec, output)) return false;
         compressedLength = (position - start + 3) & ~3;
         result = output;
         return true;
      }

      private static bool DecodeInstructions(byte[] loVec, ushort[] symVec, byte[] output) {
         int loIndex = 0, symIndex = 0, outIndex = 0; // outIndex counts bytes: the data is a stream of 16 bit values
         while (loIndex < loVec.Length) {
            int length = loVec[loIndex++] & 0x7F;
            if ((loVec[loIndex - 1] & 0x80) != 0) length += loVec[loIndex++] << 7;
            int offset = loVec[loIndex++] & 0x7F;
            if ((loVec[loIndex - 1] & 0x80) != 0) offset += loVec[loIndex++] << 7;
            if (length != 0) {
               if (symIndex >= symVec.Length || outIndex + 2 > output.Length) return false;
               WriteShort(output, ref outIndex, symVec[symIndex++]);
               if (offset == 0 || offset * 2 > outIndex) return false;
               for (int i = 0; i < length; i++) {
                  if (outIndex + 2 > output.Length) return false;
                  output[outIndex] = output[outIndex - offset * 2];
                  output[outIndex + 1] = output[outIndex + 1 - offset * 2];
                  outIndex += 2;
               }
            } else {
               for (int i = 0; i < offset; i++) {
                  if (symIndex >= symVec.Length || outIndex + 2 > output.Length) return false;
                  WriteShort(output, ref outIndex, symVec[symIndex++]);
               }
            }
         }
         return outIndex == output.Length;
      }

      private static void WriteShort(byte[] output, ref int index, ushort value) {
         output[index++] = (byte)value;
         output[index++] = (byte)(value >> 8);
      }

      private struct Column { public byte Symbol, K; public int Y; }

      private static bool FrequenciesAreValid(int[] frequencies) {
         var total = 0;
         foreach (var f in frequencies) total += f;
         return total == TableSize;
      }

      private static int[] UnpackFrequencies(IReadOnlyList<byte> data, int position) {
         var result = new int[16];
         int freq15 = 0;
         for (int i = 0; i < 3; i++) {
            var value = ReadWord(data, position + i * 4);
            for (int j = 0; j < 5; j++) result[i * 5 + j] = (int)((value >> (6 * j)) & 0x3F);
            freq15 += (int)((value >> 30) & 3) << (2 * i);
         }
         result[15] = freq15;
         return result;
      }

      private static Column[] BuildTable(int[] frequencies) {
         var table = new Column[TableSize];
         int column = 0;
         for (int symbol = 0; symbol < 16; symbol++) {
            for (int j = 0; j < frequencies[symbol]; j++) {
               var y = frequencies[symbol] + j;
               int k = 0;
               while ((y << k) < TableSize) k++;
               table[column++] = new Column { Symbol = (byte)symbol, Y = y, K = (byte)k };
            }
         }
         return table;
      }

      private static bool DecodeNibbles(Column[] table, uint[] bits, int bitCount, ref int state, byte[] nibbles, ref int bitIndex, bool last) {
         for (int i = 0; i < nibbles.Length; i++) {
            var column = table[state];
            nibbles[i] = column.Symbol;
            if (i + 1 == nibbles.Length && last) return true;
            int next = column.Y << column.K;
            for (int j = 0; j < column.K; j++) {
               if (bitIndex >= bitCount) return false;
               next += (int)((bits[bitIndex >> 5] >> (bitIndex & 31)) & 1) << j;
               bitIndex++;
            }
            state = next - TableSize;
            if (state < 0 || state >= TableSize) return false;
         }
         return true;
      }

      private static uint ReadWord(IReadOnlyList<byte> data, int index) => (uint)(data[index] | (data[index + 1] << 8) | (data[index + 2] << 16) | (data[index + 3] << 24));
   }
}
