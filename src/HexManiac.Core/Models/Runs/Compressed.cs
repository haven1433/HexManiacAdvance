using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Tools;

namespace HavenSoft.HexManiac.Core.Models.Runs.Compressed {
   public class LZRun : BaseRun, IStreamRun {
      public IDataModel Model { get; }

      public override int Length { get; }

      public override string FormatString => "`lz`";

      public LZRun(IDataModel data, int start, IReadOnlyList<int> sources = null) : base(start, sources) {
         this.Model = data;
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
                  if (index - runOffset < 0 || runLength < 0) return -1;
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

      public static IReadOnlyList<byte> CompressedToken(byte runLength, short runOffset) => new LzCompressedToken(runLength, runOffset).Render().ToList();

      private IDisposable previousScope;
      private IDataFormat[] cache;
      public override IDataFormat CreateDataFormat(IDataModel data, int index) {
         var scope = ModelCacheScope.GetCache(data);
         if (previousScope != scope) UpdateCache(scope, data);
         return cache[index - Start];
      }

      /// <summary>
      /// In response to a change to the raw data,
      /// shorten or lengthen the run.
      /// </summary>
      public LZRun FixupEnd(IDataModel model, ModelDelta token) {
         var newData = RecommendedFixup();
         var newRun = model.RelocateForExpansion(token, this, newData.Count);
         var newStart = newRun.Start;
         for (int i = 0; i < newData.Count; i++) token.ChangeData(model, newStart + i, newData[i]);
         for (int i = newData.Count; i < Length; i++) token.ChangeData(model, newStart + i, 0xFF);
         return (LZRun)Duplicate(newStart, newRun.PointerSources?.ToArray());
      }

      #region StreamRun

      public string SerializeRun() {
         var uncompressed = Decompress(Model, Start);
         var builder = new StringBuilder();
         for (int i = 0; i < uncompressed.Length; i++) {
            if (i % 16 != 0) {
               builder.Append(" ");
            } else if (i != 0) {
               builder.AppendLine();
            }
            builder.Append(uncompressed[i].ToHexString());
         }
         return builder.ToString();
      }

      public IStreamRun DeserializeRun(string content, ModelDelta token) {
         var uncompressed = new List<byte>();
         foreach (var textByte in content.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)) {
            if (byte.TryParse(textByte, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out byte value)) uncompressed.Add(value);
         }
         var compressed = Compress(uncompressed, 0, uncompressed.Count);
         IStreamRun run = this;
         if (compressed.Count > Length) {
            run = (IStreamRun)Model.RelocateForExpansion(token, this, compressed.Count);
         }
         for (int i = 0; i < compressed.Count; i++) token.ChangeData(Model, run.Start + i, compressed[i]);
         for (int i = compressed.Count; i < Length; i++) token.ChangeData(Model, run.Start + i, 0xFF);
         return (LZRun)Duplicate(run.Start, PointerSources?.ToArray());
      }

      public bool DependsOn(string anchorName) => false;

      #endregion

      /// <summary>
      /// Without worrying about available space, rectify the end of the data to make it the appropriate length.
      /// </summary>
      private IReadOnlyList<byte> RecommendedFixup() {
         var newCompressedData = new List<byte>(new[] { Model[Start + 0], Model[Start + 1], Model[Start + 2], Model[Start + 3] });
         var uncompressedLength = newCompressedData.ReadMultiByteValue(1, 3);
         int currentLength = 0;
         int readIndex = Start + 4;
         while (currentLength < uncompressedLength) {
            int lastBitFieldIndex = readIndex;
            byte bitField = 0;
            if (readIndex < Start + Length) {
               bitField = Model[lastBitFieldIndex];
               readIndex += 1;
            } else {
               lastBitFieldIndex = newCompressedData.Count;
            }
            byte lastBitFieldValue = bitField;
            newCompressedData.Add(lastBitFieldValue);
            for (int i = 0; i < 8; i++) {
               if (currentLength == uncompressedLength) {
                  lastBitFieldValue &= (byte)(0xFF << 8 - i);
                  newCompressedData[lastBitFieldIndex] = lastBitFieldValue;
                  break;
               }
               if (readIndex == Start + Length) {
                  // ran out of data to read.
                  var runLength = (byte)Math.Min(uncompressedLength - currentLength, 18);
                  if (runLength < 3) {
                     // too short for a compressed token, add an uncompressed token instead.
                     lastBitFieldValue &= (byte)(0xFF << 8 - i);
                     newCompressedData.Add(0);
                     newCompressedData[lastBitFieldIndex] = lastBitFieldValue;
                     runLength = 1;
                  } else {
                     // Try to fill the rest with a compressed token.
                     lastBitFieldValue |= (byte)(1 << 7 - i);
                     newCompressedData[lastBitFieldIndex] = lastBitFieldValue;
                     newCompressedData.AddRange(new LzCompressedToken(runLength, 1).Render());
                  }
                  currentLength += runLength;
                  continue;
               }
               var isCompressed = IsNextTokenCompressed(ref bitField);
               if (!isCompressed) {
                  newCompressedData.Add(Model[Start + readIndex]);
                  readIndex += 1;
                  currentLength += 1;
               } else {
                  (int runLength, int runOffset) = ReadCompressedToken(Model, ref readIndex);
                  if (currentLength + runLength > uncompressedLength) {
                     TruncateCompressedToken(newCompressedData, uncompressedLength, ref currentLength, lastBitFieldIndex, ref lastBitFieldValue, i, ref runOffset);
                     break;
                  } else {
                     newCompressedData.AddRange(new LzCompressedToken((byte)runLength, (short)runOffset).Render());
                     currentLength += runLength;
                  }
               }
            }
         }

         return newCompressedData;
      }

      private static void TruncateCompressedToken(List<byte> newData, int uncompressedLength, ref int currentLength, int lastBitField, ref byte lastBitFieldValue, int groupIndex, ref int runOffset) {
         var recommendedCompressedLength = uncompressedLength - currentLength;
         if (recommendedCompressedLength == 1) {
            lastBitFieldValue &= (byte)(0xFF << 8 - groupIndex);
            newData[lastBitField] = lastBitFieldValue;
            if (runOffset > newData.Count) runOffset = newData.Count;
            newData.Add(newData[newData.Count - runOffset]);
            currentLength += 1;
         } else if (recommendedCompressedLength == 2) {
            lastBitFieldValue &= (byte)(0xFF << 8 - groupIndex);
            newData[lastBitField] = lastBitFieldValue;
            if (runOffset > newData.Count) runOffset = newData.Count;
            newData.Add(newData[newData.Count - runOffset]);
            if (groupIndex == 7) {
               var lastValue = newData[newData.Count - runOffset];
               newData.Add(0x00); // group header, no compressed data
               newData.Add(lastValue);
            } else {
               newData.Add(newData[newData.Count - runOffset]);
            }
            currentLength += 2;
         } else if (recommendedCompressedLength > 2) {
            // we can still use a compressed token, just make it shorter
            lastBitFieldValue &= (byte)(0xFF << 7 - groupIndex);
            newData[lastBitField] = lastBitFieldValue;
            newData.AddRange(new LzCompressedToken((byte)recommendedCompressedLength, (short)runOffset).Render());
            currentLength += recommendedCompressedLength;
         }
      }

      protected override BaseRun Clone(IReadOnlyList<int> newPointerSources) => new LZRun(Model, Start, newPointerSources);

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
            while (start + runLength < end && runLength < 18 && data[start - runOffset + runLength] == data[start + runLength]) runLength++;
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
                     cache[cacheIndex] = LzUncompressed.Instance;
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

      private interface ILzDataToken {
         byte CompressionBit(int offset);
         IEnumerable<byte> Render();
      }

      private class LzUncompressedToken : ILzDataToken {
         public byte Value { get; }
         public LzUncompressedToken(byte value) => Value = value;
         public byte CompressionBit(int offset) => 0;
         public IEnumerable<byte> Render() { yield return Value; }
      }

      private class LzCompressedToken : ILzDataToken {
         public byte Length { get; }
         public short Offset { get; }
         public LzCompressedToken(byte length, short offset) => (Length, Offset) = (length, offset);
         public byte CompressionBit(int offset) => (byte)(1 << 7 - offset);
         public IEnumerable<byte> Render() {
            var offset = (Offset - 1).LimitToRange(0, 0xFFF);
            var length = (Length - 3).LimitToRange(0, 0xF);
            yield return (byte)((length << 4) | (offset >> 8));
            yield return (byte)offset;
         }
      }
   }

   public class SpriteRun : LZRun {
      public SpriteFormat SpriteFormat { get; }

      public override string FormatString { get; }

      public SpriteRun(SpriteFormat spriteFormat, IDataModel data, int start, IReadOnlyList<int> sources)
         : base(data, start, sources) {
         SpriteFormat = spriteFormat;
         FormatString = $"`lzs{spriteFormat.BitsPerPixel}x{spriteFormat.TileWidth}x{spriteFormat.TileHeight}`";
      }

      public static bool TryParseSpriteFormat(string pointerFormat, out SpriteFormat spriteFormat) {
         spriteFormat = default;
         if (!pointerFormat.StartsWith("`lzs") || !pointerFormat.EndsWith("`")) return false;
         var formatContent = pointerFormat.Substring(4);
         formatContent = formatContent.Substring(0, formatContent.Length - 1);
         var dimensionsAsText = formatContent.Split('x');
         if (dimensionsAsText.Length != 3) return false;
         if (!int.TryParse(dimensionsAsText[0], out var bitsPerPixel)) return false;
         if (!int.TryParse(dimensionsAsText[1], out var width)) return false;
         if (!int.TryParse(dimensionsAsText[2], out var height)) return false;
         spriteFormat = new SpriteFormat(bitsPerPixel, width, height);
         return true;
      }

      protected override BaseRun Clone(IReadOnlyList<int> newPointerSources) => new SpriteRun(SpriteFormat, Model, Start, newPointerSources);
   }

   public struct SpriteFormat {
      public int BitsPerPixel { get; }
      public int TileWidth { get; }
      public int TileHeight { get; }
      public int ExpectedByteLength { get; }
      public SpriteFormat(int bitsPerPixel, int width, int height) {
         (BitsPerPixel, TileWidth, TileHeight) = (bitsPerPixel, width, height);
         ExpectedByteLength = 8 * BitsPerPixel * TileWidth * TileHeight;
      }
   }

   public class PaletteRun : LZRun {
      public PaletteFormat PaletteFormat { get; }

      public override string FormatString { get; }

      public PaletteRun(PaletteFormat paletteFormat, IDataModel data, int start, IReadOnlyList<int> sources)
         : base(data,start,sources){
         PaletteFormat = paletteFormat;
         FormatString = $"`lzp{paletteFormat.Bits}`";
      }

      public static bool TryParsePaletteFormat(string pointerFormat, out PaletteFormat paletteFormat) {
         paletteFormat = default;
         if (!pointerFormat.StartsWith("`lzp") || !pointerFormat.EndsWith("`")) return false;
         var formatContent = pointerFormat.Substring(4);
         formatContent = formatContent.Substring(0, formatContent.Length - 1);
         if (!int.TryParse(formatContent, out var bits)) return false;
         paletteFormat = new PaletteFormat(bits);
         return true;
      }

      protected override BaseRun Clone(IReadOnlyList<int> newPointerSources) => new PaletteRun(PaletteFormat, Model, Start, newPointerSources);
   }

   public struct PaletteFormat {
      public int Bits { get; }
      public PaletteFormat(int bits) => Bits = bits;
   }
}
