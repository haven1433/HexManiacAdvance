using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace HavenSoft.HexManiac.Core.Models.Runs.Sprites {
   public class LZRun : BaseRun, IStreamRun, IAppendToBuilderRun {
      public static readonly int
         GeneralDecompressionError = -1,
         DecompressedTooLong = -2;

      public IDataModel Model { get; }

      private int length;
      public override int Length => length;
      protected void InvalidateLength() => length = -1;

      public int DecompressedLength { get; }

      public override string FormatString => "`lz`";

      public bool AllowLengthErrors { get; }

      public bool HasLengthErrors { get; }

      public LZRun(IDataModel data, int start, bool allowLengthErrors = false, SortedSpan<int> sources = null) : base(start, sources) {
         this.Model = data;
         this.AllowLengthErrors = allowLengthErrors;
         length = IsCompressedLzData(data, start, allowLengthErrors, out bool hasLengthErrors);
         HasLengthErrors = hasLengthErrors;
         if (data.Count > start + 4) DecompressedLength = data.ReadMultiByteValue(start + 1, 3);
      }

      /// <returns>The length of the compressed data</returns>
      public static int IsCompressedLzData(IReadOnlyList<byte> data, int start, bool allowLengthErrors = false) => IsCompressedLzData(data, start, allowLengthErrors, out var _);

      /// <returns>
      /// The length of the compressed data.
      /// If the decompressed data is longer than expected and allowLengthErrors is false, returns -2.
      /// Any other decompression error returns -1.
      /// </returns>
      public static int IsCompressedLzData(IReadOnlyList<byte> data, int start, bool allowLengthErrors, out bool hasLengthErrors) {
         hasLengthErrors = false;
         var initialStart = start;
         int length = ReadHeader(data, ref start);
         if (length < 1) return GeneralDecompressionError;
         int index = 0; // the index into the uncompressed data
         while (index < length && start < data.Count) {
            var bitField = data[start];
            start++;
            for (int i = 0; i < 8; i++) {
               if (index > length) break;
               if (index == length) return bitField == 0 ? start - initialStart : GeneralDecompressionError;
               var compressed = IsNextTokenCompressed(ref bitField);
               if (!compressed) {
                  index += 1;
                  start += 1;
               } else {
                  var (runLength, runOffset) = ReadCompressedToken(data, ref start);
                  if (index - runOffset < 0 || runLength < 0) return GeneralDecompressionError;
                  index += runLength;
               }
            }
         }

         hasLengthErrors = index != length || start > data.Count;
         if (allowLengthErrors) return start - initialStart;
         if (!hasLengthErrors) return start - initialStart;
         if (index > length) return DecompressedTooLong;
         return GeneralDecompressionError;
      }

      public static bool TryDecompress(IReadOnlyList<byte> data, int start, bool allowLengthErrors, out byte[] result) => (result = Decompress(data, start, allowLengthErrors)) != null;

      public static byte[] Decompress(IReadOnlyList<byte> data, int start, bool allowLengthErrors = false) {
         int length = ReadHeader(data, ref start);
         if (length < 1) return null;
         var index = 0;
         var result = new byte[length];
         while (index < length) {
            var bitField = data[start];
            start++;
            for (int i = 0; i < 8; i++) {
               if (index > length) break;
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
                  if (index - runOffset < 0) return null;
                  for (int j = 0; j < runLength; j++) {
                     if (index + j < result.Length) result[index + j] = result[index + j - runOffset];
                  }
                  index += runLength;
               }
            }
            if (bitField != 0) return null;
         }

         if (allowLengthErrors) return result;
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

      public static IReadOnlyList<byte> Compress(IReadOnlyList<byte> data) => Compress(data, 0, data.Count);

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
         var result = new List<byte> {
            0x10, // magic number
            (byte)length,
            (byte)(length >> 8),
            (byte)(length >> 16)
         };
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
      public LZRun FixupEnd(IDataModel model, ModelDelta token, int fixupStart) {
         var newData = RecommendedFixup(fixupStart);
         if (newData == null) return null;
         var newRun = model.RelocateForExpansion(token, this, newData.Count);
         var newStart = newRun.Start;
         for (int i = 0; i < newData.Count; i++) token.ChangeData(model, newStart + i, newData[i]);
         for (int i = newData.Count; i < Length; i++) token.ChangeData(model, newStart + i, 0xFF);
         return (LZRun)Duplicate(newStart, newRun.PointerSources);
      }

      #region StreamRun

      public string SerializeRun() {
         var uncompressed = Decompress(Model, Start, AllowLengthErrors);
         var builder = new StringBuilder();
         for (int i = 0; i < (uncompressed?.Length ?? 0); i++) {
            if (i % 16 != 0) {
               builder.Append(" ");
            } else if (i != 0) {
               builder.AppendLine();
            }
            builder.Append(uncompressed[i].ToHexString());
         }
         return builder.ToString();
      }

      public IStreamRun DeserializeRun(string content, ModelDelta token, out IReadOnlyList<int> changedOffsets) {
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
         changedOffsets = new List<int>(); // don't track changes for compression streams
         return (LZRun)Duplicate(run.Start, PointerSources);
      }

      public IReadOnlyList<AutocompleteItem> GetAutoCompleteOptions(string line, int caretLineIndex, int caretCharacterIndex) {
         var result = new List<AutocompleteItem>();
         return result;
      }

      public IReadOnlyList<IPixelViewModel> Visualizations => new List<IPixelViewModel>();
      public bool DependsOn(string anchorName) => false;

      #endregion

      /// <summary>
      /// Without worrying about available space, rectify the end of the data to make it the appropriate length.
      /// </summary>
      /// <param name="fixupStart">
      /// The first index of the compressed data that is allowed to change.
      /// </param>
      /// <returns>
      /// null, only if the data tail cannot safely be modified to make the length correct.
      /// </returns>
      private IReadOnlyList<byte> RecommendedFixup(int fixupStart) {
         var newCompressedData = new List<byte>(new[] { Model[Start + 0], Model[Start + 1], Model[Start + 2], Model[Start + 3] });
         var uncompressedLength = newCompressedData.ReadMultiByteValue(1, 3);
         var currentLength = 0;     // tracks the uncompressed length of newCompressedData so far
         var readIndex = Start + 4; // tracks the current location we're reading from Model
         byte header = 0;

         while (currentLength < uncompressedLength) {
            if (newCompressedData.Count < fixupStart) {
               header = Model[readIndex];
               readIndex += 1;
            } else {
               var remainingLength = uncompressedLength - currentLength;
               var desiredCompressedTokens = (int)Math.Ceiling(remainingLength / 18.0);
               if (remainingLength < 3) desiredCompressedTokens = 0;
               if (desiredCompressedTokens > 8) desiredCompressedTokens = 8;
               header = (byte)(0xFF << (8 - desiredCompressedTokens));
               if (readIndex == Start + 4) header &= 0x7F; // if this is the first header, the highest bit must be 0.
            }
            newCompressedData.Add(header);
            for (int i = 0; i < 8; i++) {
               if (currentLength >= uncompressedLength) break;
               if (IsNextTokenCompressed(ref header)) {
                  if (newCompressedData.Count < fixupStart) {
                     var (runLength, runOffset) = ReadCompressedToken(Model, ref readIndex);
                     newCompressedData.AddRange(new LzCompressedToken((byte)runLength, (short)runOffset).Render());
                     currentLength += runLength;
                  } else {
                     // get to the end as fast as possible (prefer 18) but no faster than able (make sure we can fulfill our requirements)
                     var minLeftover = MinimumLengthNeededToFullfillHeader(header);
                     int desiredLength = uncompressedLength - currentLength - minLeftover;
                     if (desiredLength > 18) desiredLength = 18;
                     if (desiredLength < 3) return null; // no fixup can match the current header
                     newCompressedData.AddRange(new LzCompressedToken((byte)desiredLength, 1).Render());
                     currentLength += desiredLength;
                     readIndex += 2;
                  }
               } else {
                  if (newCompressedData.Count < fixupStart) {
                     newCompressedData.Add(Model[readIndex]);
                  } else {
                     // we can render anything, we're at the 'end'
                     newCompressedData.Add(0);
                  }
                  readIndex += 1;
                  currentLength += 1;
               }
            }
         }

         if (header == 0 && currentLength == uncompressedLength) return newCompressedData;
         return null; // no data space left, but high-bits are left. No fixup can fix this.
      }

      protected override BaseRun Clone(SortedSpan<int> newPointerSources) => new LZRun(Model, Start, AllowLengthErrors, newPointerSources);

      private static int ReadHeader(IReadOnlyList<byte> data, ref int start) {
         var originalStart = start;
         start += 4;
         if (data.Count <= start || originalStart < 0) return -1;
         if (originalStart < 0 || start > data.Count) return -1;
         if (data[originalStart] != 0x10) return -1;
         int length = data.ReadMultiByteValue(originalStart + 1, 3);
         return length;
      }

      private static (int runLength, int runOffset) FindLongestMatch(IReadOnlyList<byte> data, int start, int earliest, int end) {
         //return (2, -1); // temporary
         // do not allow compression tokens to start on an odd byte
         // see https://www.akkit.org/info/gbatek.htm : SWI 11h (GBA/NDS7/NDS9) - LZ77UnCompWram
         if (start % 2 == 1) return (2, -1);

         int bestLength = 2, bestOffset = -1;
         for (int runOffset = 2; runOffset <= 0x1000 && start - runOffset >= earliest; runOffset += 2) {
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

      // returns the minimum number of remaining bytes needed to fullfill a header
      private static int MinimumLengthNeededToFullfillHeader(byte bitField) {
         if (bitField == 0) return 0;
         // at least one bit is high
         var minimum = 0;
         while (bitField != 0) {
            if ((bitField & 0x80) != 0) minimum += 3;
            else minimum += 1;
            bitField <<= 1;
         }
         return minimum;
      }

      private void UpdateCache(IDisposable scope, IReadOnlyList<byte> data) {
         previousScope = scope;
         cache = new IDataFormat[Length];
         int start = Start;
         var cacheIndex = 4;

         var decompressedLength = ReadHeader(data, ref start);
         cache[0] = new LzMagicIdentifier(Start);
         cache[1] = new Integer(Start + 1, 0, decompressedLength, 3);
         cache[2] = new Integer(Start + 1, 1, decompressedLength, 3);
         cache[3] = new Integer(Start + 1, 2, decompressedLength, 3);

         while (cacheIndex < cache.Length) {
            cache[cacheIndex] = new LzGroupHeader(start);
            var bitfield = data[start];
            cacheIndex++;
            start++;
            for (int i = 0; i < 8 && cacheIndex < cache.Length; i++) {
               if (IsNextTokenCompressed(ref bitfield)) {
                  if (cacheIndex + 2 > cache.Length) {
                     cache[cacheIndex] = new LzUncompressed(start);
                     cacheIndex++;
                  } else {
                     var (runLength, runOffset) = ReadCompressedToken(data, ref start);
                     cache[cacheIndex + 0] = new LzCompressed(start - 2, 0, runLength, runOffset);
                     cache[cacheIndex + 1] = new LzCompressed(start - 2, 1, runLength, runOffset);
                     cacheIndex += 2;
                  }
               } else {
                  cache[cacheIndex] = new LzUncompressed(start);
                  cacheIndex++;
                  start++;
               }
            }
         }
      }

      public void AppendTo(IDataModel model, StringBuilder builder, int start, int length, bool deep) {
         while (length > 0) {
            if (start >= Start + Length) break;
            var format = CreateDataFormat(model, start);
            while (format is IDataFormatDecorator decorator) format = decorator.OriginalFormat;
            if (format is LzMagicIdentifier) {
               builder.Append("lz ");
               start += 1;
               length -= 1;
            } else if (format is Integer integer) {
               var uncompressedLength = model.ReadMultiByteValue(integer.Source, 3);
               builder.Append($"{uncompressedLength} ");
               start += 3 - integer.Position;
               length -= 3 - integer.Position;
            } else if (format is LzGroupHeader) {
               builder.Append(model[start].ToHexString() + " ");
               start += 1;
               length -= 1;
            } else if (format is LzUncompressed) {
               builder.Append(model[start].ToHexString() + " ");
               start += 1;
               length -= 1;
            } else if (format is LzCompressed compressed) {
               var tempStart = start - compressed.Position;
               var (runLength, runOffset) = ReadCompressedToken(model, ref tempStart);
               builder.Append($"{runLength}:{runOffset} ");
               length -= tempStart - start;
               start = tempStart;
            } else {
               throw new NotImplementedException();
            }
         }
      }

      public void Clear(IDataModel model, ModelDelta changeToken, int start, int length) {
         for (int i = 0; i < length; i++) changeToken.ChangeData(model, start + i, 0);
         FixupEnd(model, changeToken, start + length);
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

   public abstract class PagedLZRun : LZRun {
      public abstract int Pages { get; }
      protected abstract int UncompressedPageLength { get; }

      public PagedLZRun(IDataModel data, int start, bool allowLengthErrors = false, SortedSpan<int> sources = null) : base(data, start, allowLengthErrors, sources) { }

      public PagedLZRun AppendPage(ModelDelta token) {
         var data = Decompress(Model, Start);
         var lastPage = Pages - 1;
         var pageLength = UncompressedPageLength;
         var newData = new byte[data.Length + pageLength];
         Array.Copy(data, newData, data.Length);
         Array.Copy(data, lastPage * pageLength, newData, data.Length, pageLength);
         var newModelData = Compress(newData, 0, newData.Length);

         var newRun = Model.RelocateForExpansion(token, this, newModelData.Count);
         for (int i = 0; i < newModelData.Count; i++) token.ChangeData(Model, newRun.Start + i, newModelData[i]);
         newRun = (PagedLZRun)newRun.Duplicate(newRun.Start, newRun.PointerSources);
         Model.ObserveRunWritten(token, newRun);
         return newRun;
      }

      public PagedLZRun DeletePage(int page, ModelDelta token) {
         var data = Decompress(Model, Start);
         var pageLength = UncompressedPageLength;
         var newData = new byte[data.Length - pageLength];
         Array.Copy(data, newData, page * pageLength);
         Array.Copy(data, (page + 1) * pageLength, newData, page * pageLength, (Pages - page - 1) * pageLength);
         var newModelData = Compress(newData, 0, newData.Length);

         for (int i = 0; i < newModelData.Count; i++) token.ChangeData(Model, Start + i, newModelData[i]);
         for (int i = newModelData.Count; i < Length; i++) token.ChangeData(Model, Start + i, 0xFF);
         var newRun = (PagedLZRun)Duplicate(Start, PointerSources);
         Model.ObserveRunWritten(token, newRun);
         return newRun;
      }
   }
}
