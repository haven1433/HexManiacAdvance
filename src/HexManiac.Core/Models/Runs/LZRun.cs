using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;

namespace HavenSoft.HexManiac.Core.Models.Runs {
   public class LZRun : BaseRun {
      private readonly IReadOnlyList<byte> data;

      public override int Length { get; }

      public override string FormatString => "`lz`";

      public LZRun(IReadOnlyList<byte> data, int start, IReadOnlyList<int> sources = null) : base(start, sources) {
         this.data = data;
         Length = IsCompressedLzData(data, start);
      }

      /// <returns>The length of the compressed data</returns>
      public static int IsCompressedLzData(IReadOnlyList<byte> data, int start) {
         var initialStart = start;
         int length = ReadHeader(data, ref start);
         if (length < 1) return -1;
         int index = 0;
         while (index < length && start < data.Count) {
            var bitField = data[start];
            start++;
            for (int i = 0; i < 8; i++) {
               if (index > length) return -1;
               if (index == length) return bitField == 0 ? start - initialStart : -1;
               var compressed = IsNextTokenCompressed(ref bitField);
               if (!compressed) {
                  index += 1;
                  start += 1;
               } else {
                  var (runLength, runOffset) = ReadCompressedToken(data, ref start);
                  if (index - runOffset < initialStart || runLength < 0) return -1;
                  index += runLength;
               }
            }
         }

         return index == length && start <= data.Count ? start - initialStart : -1;
      }

      public static bool TryDecompress(IReadOnlyList<byte> data, int start, out byte[] result) => (result = Decompress(data, start)) != null;

      public static byte[] Decompress(IReadOnlyList<byte> data, int start) {
         int length = ReadHeader(data, ref start);
         if (length < 1) return null;
         var index = 0;
         var result = new byte[length];
         while (index < length) {
            var bitField = data[start];
            start++;
            for (int i = 0; i < 8; i++) {
               if (index > length) return null;
               if (index == length) return bitField == 0 ? result : null;
               if (start >= data.Count) return null;
               var compressed = IsNextTokenCompressed(ref bitField);
               if (!compressed) {
                  result[index] = data[start];
                  index += 1;
                  start += 1;
               } else {
                  if (start + 2 > data.Count) return null;
                  var (runLength, runOffset) = ReadCompressedToken(data, ref start);
                  if (index + runLength > result.Length) return null;
                  if (index - runOffset < 0) return null;
                  for (int j = 0; j < runLength; j++) result[index + j] = result[index + j - runOffset];
                  index += runLength;
               }
            }
            if (bitField != 0) return null;
         }

         return index == length ? result : null;
      }

      public static (int runLength, int runOffset) ReadCompressedToken(IReadOnlyList<byte> data, ref int start) {
         if (data.Count < start + 2) return (-1, -1);
         var byte1 = data[start + 0];
         var byte2 = data[start + 1];
         start += 2;
         var runLength = (byte1 >> 4) + 3;
         var runOffset = (((byte1 & 0xF) << 8) | byte2) + 1;
         return (runLength, runOffset);
      }

      public static IReadOnlyList<byte> Compress(IReadOnlyList<byte> data, int start, int length) {
         // step 1: tokenize
         var tokens = new List<ILzDataToken>();
         int index = start;
         while (index < start + length) {
            var (runLength, runOffset) = FindLongestMatch(data, index, start, start + length);
            if (runLength < 3) {
               tokens.Add(new LzUncompressedToken(data[index]));
               index += 1;
            } else {
               tokens.Add(new LzCompressedToken((byte)runLength, (short)runOffset));
               index += runLength;
            }
         }

         // step 2: render the tokens into a stream
         var result = new List<byte>();
         result.Add(0x10); // magic number
         result.Add((byte)length);
         result.Add((byte)(length >> 8));
         result.Add((byte)(length >> 16));
         for (int i = 0; i < tokens.Count; i++) {
            if (i % 8 == 0) {
               byte bitfield = 0;
               for (int j = 0; j < 8 && i + j < tokens.Count; j++) bitfield |= tokens[i + j].CompressionBit(j);
               result.Add(bitfield);
            }
            result.AddRange(tokens[i].Render());
         }

         return result;
      }

      private IDisposable previousScope;
      private IDataFormat[] cache;
      public override IDataFormat CreateDataFormat(IDataModel data, int index) {
         var scope = ModelCacheScope.GetCache(data);
         if (previousScope != scope) UpdateCache(scope, data);
         return cache[index - Start];
      }

      protected override BaseRun Clone(IReadOnlyList<int> newPointerSources) => new LZRun(data, Start, newPointerSources);

      private static int ReadHeader(IReadOnlyList<byte> data, ref int start) {
         if (start + 4 > data.Count) return -1;
         if (data[start] != 0x10) return -1;
         int length = data.ReadMultiByteValue(start + 1, 3);
         start += 4;
         return length;
      }

      private static (int runLength, int runOffset) FindLongestMatch(IReadOnlyList<byte> data, int start, int earliest, int end) {
         int bestLength = 2, bestOffset = -1;
         for (int runOffset = 1; runOffset <= 0x1000 && start - runOffset >= earliest; runOffset++) {
            int runLength = 0;
            while (data[start - runOffset + runLength] == data[start + runLength] && start + runLength < end && runLength < 18) runLength++;
            if (runLength > bestLength) (bestLength, bestOffset) = (runLength, runOffset);
         }
         return (bestLength, bestOffset);
      }

      private static bool IsNextTokenCompressed(ref byte bitField) {
         var compressed = (bitField & 0x80) != 0;
         bitField <<= 1;
         return compressed;
      }

      private void UpdateCache(IDisposable scope, IReadOnlyList<byte> data) {
         previousScope = scope;
         cache = new IDataFormat[Length];
         int start = Start;
         var cacheIndex = 4;

         var decompressedLength = ReadHeader(data, ref start);
         cache[0] = LzMagicIdentifier.Instance;
         cache[1] = new Integer(Start + 1, 0, decompressedLength, 3);
         cache[2] = new Integer(Start + 1, 1, decompressedLength, 3);
         cache[3] = new Integer(Start + 1, 2, decompressedLength, 3);

         while (cacheIndex < cache.Length) {
            cache[cacheIndex] = LzGroupHeader.Instance;
            var bitfield = data[start];
            cacheIndex++;
            start++;
            for (int i = 0; i < 8 && cacheIndex < cache.Length; i++) {
               if (IsNextTokenCompressed(ref bitfield)) {
                  if (cacheIndex + 2 > cache.Length) {
                     cache[cacheIndex] = None.Instance;
                     cacheIndex++;
                  } else {
                     var (runLength, runOffset) = ReadCompressedToken(data, ref start);
                     cache[cacheIndex + 0] = new LzCompressed(start - 2, 0, runLength, runOffset);
                     cache[cacheIndex + 1] = new LzCompressed(start - 2, 1, runLength, runOffset);
                     cacheIndex += 2;
                  }
               } else {
                  cache[cacheIndex] = LzUncompressed.Instance;
                  cacheIndex++;
                  start++;
               }
            }
         }
      }
   }

   internal interface ILzDataToken {
      byte CompressionBit(int offset);
      IEnumerable<byte> Render();
   }

   internal class LzUncompressedToken : ILzDataToken {
      public byte Value { get; }
      public LzUncompressedToken(byte value) => Value = value;
      public byte CompressionBit(int offset) => 0;
      public IEnumerable<byte> Render() { yield return Value; }
   }

   internal class LzCompressedToken : ILzDataToken {
      public byte Length { get; }
      public short Offset { get; }
      public LzCompressedToken(byte length, short offset) => (Length, Offset) = (length, offset);
      public byte CompressionBit(int offset) => (byte)(1 << 7 - offset);
      public IEnumerable<byte> Render() {
         var offset = Offset - 1;
         var length = Length - 3;
         yield return (byte)((length << 4) | (offset >> 8));
         yield return (byte)offset;
      }
   }
}
