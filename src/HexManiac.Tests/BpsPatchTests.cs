using HavenSoft.HexManiac.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class BpsPatchTests {
      /// <summary>
      /// Writes BPS patches straight from the format description (byuu's BPS1), so the decoder under test is checked against an independent encoder.
      /// </summary>
      public class BpsBuilder {
         private readonly byte[] source, target;
         private readonly List<byte> actions = new List<byte>();

         public BpsBuilder(byte[] source, byte[] target) {
            this.source = source;
            this.target = target;
         }

         public static byte[] Number(long value) {
            var result = new List<byte>();
            while (true) {
               var x = (byte)(value & 0x7F);
               value >>= 7;
               if (value == 0) {
                  result.Add((byte)(0x80 | x));
                  return result.ToArray();
               }
               result.Add(x);
               value -= 1;
            }
         }

         private static long Signed(long delta) => (Math.Abs(delta) << 1) | (delta < 0 ? 1L : 0L);
         private void Action(int command, int length) => actions.AddRange(Number(((long)(length - 1) << 2) | (long)command));

         public BpsBuilder SourceRead(int length) { Action(0, length); return this; }
         public BpsBuilder TargetRead(params byte[] literal) { Action(1, literal.Length); actions.AddRange(literal); return this; }
         public BpsBuilder SourceCopy(int length, int offsetDelta) { Action(2, length); actions.AddRange(Number(Signed(offsetDelta))); return this; }
         public BpsBuilder TargetCopy(int length, int offsetDelta) { Action(3, length); actions.AddRange(Number(Signed(offsetDelta))); return this; }
         public BpsBuilder Raw(params byte[] bytes) { actions.AddRange(bytes); return this; }

         public byte[] Build(uint? sourceCrc = null, uint? targetCrc = null) {
            var patch = new List<byte>();
            patch.AddRange(new[] { (byte)'B', (byte)'P', (byte)'S', (byte)'1' });
            patch.AddRange(Number(source.Length));
            patch.AddRange(Number(target.Length));
            patch.AddRange(Number(0)); // no metadata
            patch.AddRange(actions);
            patch.AddRange(BitConverter.GetBytes(sourceCrc ?? Crc(source)));
            patch.AddRange(BitConverter.GetBytes(targetCrc ?? Crc(target)));
            patch.AddRange(BitConverter.GetBytes(Crc(patch.ToArray())));
            return patch.ToArray();
         }

         public static uint Crc(byte[] data) => (uint)Patcher.CalcCRC32(data);
      }

      private static byte[] Bytes(params byte[] data) => data;

      private static byte[] Range(int start, int count) {
         var result = new byte[count];
         for (int i = 0; i < count; i++) result[i] = (byte)(start + i);
         return result;
      }

      private static byte[] Join(params byte[][] parts) {
         var result = new List<byte>();
         foreach (var part in parts) result.AddRange(part);
         return result.ToArray();
      }

      [Fact]
      public void SourceRead_CopiesBytesFromTheSameOffsetOfTheSource() {
         var source = Bytes(1, 2, 3, 4, 5, 6);
         var target = Bytes(1, 2, 3, 4, 9, 9);
         var patch = new BpsBuilder(source, target).SourceRead(4).TargetRead(9, 9).Build();

         var result = BpsPatch.Apply(patch, source);

         Assert.True(result.Success, result.Message);
         Assert.Equal(target, result.Target);
      }

      [Fact]
      public void TargetRead_WritesTheLiteralBytesFromThePatch() {
         var source = new byte[4];
         var target = Bytes(0xDE, 0xAD, 0xBE, 0xEF, 0x00, 0xFF);
         var patch = new BpsBuilder(source, target).TargetRead(0xDE, 0xAD, 0xBE, 0xEF).TargetRead(0x00, 0xFF).Build();

         var result = BpsPatch.Apply(patch, source);

         Assert.True(result.Success, result.Message);
         Assert.Equal(target, result.Target);
      }

      [Fact]
      public void SourceCopy_FollowsSignedRelativeOffsets() {
         var source = Range(0x10, 12);
         // take source[6..9), then source[2..5): the first jump is +6, the second goes back by 7 (the offset is already at 9 after the first copy)
         var target = Join(new[] { source[6], source[7], source[8] }, new[] { source[2], source[3], source[4] });
         var patch = new BpsBuilder(source, target).SourceCopy(3, 6).SourceCopy(3, -7).Build();

         var result = BpsPatch.Apply(patch, source);

         Assert.True(result.Success, result.Message);
         Assert.Equal(target, result.Target);
      }

      [Fact]
      public void TargetCopy_RepeatsEarlierOutput() {
         var source = new byte[2];
         var target = Bytes(1, 2, 3, 4, 2, 3);
         var patch = new BpsBuilder(source, target).TargetRead(1, 2, 3, 4).TargetCopy(2, 1).Build();

         var result = BpsPatch.Apply(patch, source);

         Assert.True(result.Success, result.Message);
         Assert.Equal(target, result.Target);
      }

      [Fact]
      public void TargetCopy_OverlappingItsOwnOutput_RepeatsThePattern() {
         var source = new byte[2];
         var target = Bytes(7, 8, 7, 8, 7, 8, 7, 8);
         var patch = new BpsBuilder(source, target).TargetRead(7, 8).TargetCopy(6, 0).Build();

         var result = BpsPatch.Apply(patch, source);

         Assert.True(result.Success, result.Message);
         Assert.Equal(target, result.Target);
      }

      [Fact]
      public void TargetCopy_OfASingleByteBehindTheOutput_MakesARun() {
         var source = new byte[1];
         var target = new byte[20];
         for (int i = 0; i < target.Length; i++) target[i] = 0x55;
         var patch = new BpsBuilder(source, target).TargetRead(0x55).TargetCopy(19, 0).Build();

         var result = BpsPatch.Apply(patch, source);

         Assert.True(result.Success, result.Message);
         Assert.Equal(target, result.Target);
      }

      [Fact]
      public void AllFourActions_InOnePatch() {
         var source = Range(0, 0x40);
         // 8 bytes read in place, 3 new bytes, 8 bytes copied from source[0x20..], 8 bytes copied from the target start, a final new byte
         var target = Join(Range(0, 8), Bytes(0xA1, 0xA2, 0xA3), Range(0x20, 8), Range(0, 8), Bytes(0xFF));
         var patch = new BpsBuilder(source, target)
            .SourceRead(8)
            .TargetRead(0xA1, 0xA2, 0xA3)
            .SourceCopy(8, 0x20)
            .TargetCopy(8, 0)
            .TargetRead(0xFF)
            .Build();

         var result = BpsPatch.Apply(patch, source);

         Assert.True(result.Success, result.Message);
         Assert.Equal(target, result.Target);
         Assert.Equal(Range(0, 0x40), source); // the input is not modified
      }

      [Fact]
      public void LargeNumbers_UseMoreThanOneByte() {
         // 300 literal bytes make the action number (299 << 2 | 1) need two bytes; the copy offset of 70000 needs three
         var source = new byte[100000];
         for (int i = 0; i < source.Length; i++) source[i] = (byte)(i * 31 + i / 251);
         var literal = new byte[300];
         for (int i = 0; i < literal.Length; i++) literal[i] = (byte)(i ^ 0x5A);
         var target = Join(literal, new byte[] { source[70000], source[70001], source[70002] });
         var patch = new BpsBuilder(source, target).TargetRead(literal).SourceCopy(3, 70000).Build();

         var result = BpsPatch.Apply(patch, source);

         Assert.True(result.Success, result.Message);
         Assert.Equal(target, result.Target);
      }

      [Fact]
      public void Number_RoundTripsWithTheReaderInPatcher() {
         foreach (var value in new long[] { 0, 1, 127, 128, 129, 16383, 16384, 70000, 0x1FFFFFF, 0x2000000 }) {
            var bytes = BpsBuilder.Number(value);
            int index = 0;
            Assert.Equal((int)value, Patcher.ReadVariableWidthInteger(bytes, ref index));
            Assert.Equal(bytes.Length, index);
         }
      }

      [Fact]
      public void NotAPatch_Rejected() {
         var result = BpsPatch.Apply(new byte[64], new byte[4]);

         Assert.False(result.Success);
         Assert.Equal(BpsError.NotBps, result.Error);
      }

      [Fact]
      public void UpsPatch_IsNotABpsPatch() {
         var patch = Patcher.BuildUpsPatch(new byte[0x20], new byte[0x20]);

         var result = BpsPatch.Apply(patch, new byte[0x20]);

         Assert.Equal(BpsError.NotBps, result.Error);
      }

      [Fact]
      public void DamagedPatch_FailsItsOwnChecksum() {
         var source = Range(0, 16);
         var target = Range(0, 8);
         var patch = new BpsBuilder(source, target).SourceRead(8).Build();
         patch[patch.Length - 13] ^= 0x40; // flip a bit in the last action byte: the stored patch checksum no longer fits

         var result = BpsPatch.Apply(patch, source);

         Assert.Equal(BpsError.CorruptPatch, result.Error);
      }

      [Fact]
      public void WrongInputLength_Rejected() {
         var source = Range(0, 16);
         var patch = new BpsBuilder(source, Range(0, 8)).SourceRead(8).Build();

         var result = BpsPatch.Apply(patch, Range(0, 17));

         Assert.Equal(BpsError.WrongSourceSize, result.Error);
         Assert.Contains("17", result.Message);
         Assert.Contains("16", result.Message);
      }

      [Fact]
      public void WrongInputContent_Rejected() {
         var source = Range(0, 16);
         var patch = new BpsBuilder(source, Range(0, 8)).SourceRead(8).Build();
         var other = Range(0, 16);
         other[15] = 0xEE;

         var result = BpsPatch.Apply(patch, other);

         Assert.Equal(BpsError.WrongSourceCrc, result.Error);
         Assert.Null(result.Target);
      }

      [Fact]
      public void WrongTargetChecksum_Rejected() {
         var source = Range(0, 16);
         var target = Range(0, 8);
         var patch = new BpsBuilder(source, target).SourceRead(8).Build(targetCrc: BpsBuilder.Crc(target) ^ 1);

         var result = BpsPatch.Apply(patch, source);

         Assert.Equal(BpsError.WrongTargetCrc, result.Error);
         Assert.Null(result.Target);
      }

      [Fact]
      public void ActionCutOff_Rejected() {
         var source = Range(0, 16);
         var target = Range(0, 10);
         // TargetRead of 10 bytes, but only 2 bytes follow in the patch
         var patch = new BpsBuilder(source, target).Raw(BpsBuilder.Number((10 - 1) << 2 | 1)).Raw(1, 2).Build();

         var result = BpsPatch.Apply(patch, source);

         Assert.Equal(BpsError.CorruptPatch, result.Error);
      }

      [Fact]
      public void SourceCopyOutsideTheSource_Rejected() {
         var source = Range(0, 16);
         var patch = new BpsBuilder(source, Range(0, 4)).SourceCopy(4, 100).Build();

         var result = BpsPatch.Apply(patch, source);

         Assert.Equal(BpsError.CorruptPatch, result.Error);
      }

      [Fact]
      public void SourceCopyBeforeTheStartOfTheSource_Rejected() {
         var source = Range(0, 16);
         var patch = new BpsBuilder(source, Range(0, 4)).SourceCopy(4, -3).Build();

         var result = BpsPatch.Apply(patch, source);

         Assert.Equal(BpsError.CorruptPatch, result.Error);
      }

      [Fact]
      public void TargetCopyOfDataThatIsNotWrittenYet_Rejected() {
         var source = Range(0, 16);
         var patch = new BpsBuilder(source, Range(0, 4)).TargetCopy(4, 0).Build();

         var result = BpsPatch.Apply(patch, source);

         Assert.Equal(BpsError.CorruptPatch, result.Error);
      }

      [Fact]
      public void ActionWritingPastTheEnd_Rejected() {
         var source = Range(0, 16);
         var patch = new BpsBuilder(source, Range(0, 4)).SourceRead(8).Build();

         var result = BpsPatch.Apply(patch, source);

         Assert.Equal(BpsError.CorruptPatch, result.Error);
      }

      [Fact]
      public void ActionsThatDoNotFillTheTarget_Rejected() {
         var source = Range(0, 16);
         var patch = new BpsBuilder(source, Range(0, 8)).SourceRead(4).Build();

         var result = BpsPatch.Apply(patch, source);

         Assert.Equal(BpsError.CorruptPatch, result.Error);
      }

      [Fact]
      public void ReadInfo_ReportsSizesAndChecksums() {
         var source = Range(0, 16);
         var target = Range(0, 12);
         var patch = new BpsBuilder(source, target).SourceRead(12).Build();

         var info = BpsPatch.ReadInfo(patch);

         Assert.Equal(16, info.SourceSize);
         Assert.Equal(12, info.TargetSize);
         Assert.Equal(BpsBuilder.Crc(source), info.SourceCrc32);
         Assert.Equal(BpsBuilder.Crc(target), info.TargetCrc32);
         Assert.Null(BpsPatch.ReadInfo(new byte[100]));
      }

      [Fact]
      public void DecompressIfGzip_UnpacksGzipAndLeavesOtherDataAlone() {
         var source = Range(0, 16);
         var patch = new BpsBuilder(source, Range(0, 8)).SourceRead(8).Build();
         byte[] gz;
         using (var output = new MemoryStream()) {
            using (var gzip = new GZipStream(output, CompressionMode.Compress, leaveOpen: true)) gzip.Write(patch, 0, patch.Length);
            gz = output.ToArray();
         }

         Assert.Equal(patch, BpsPatch.DecompressIfGzip(gz));
         Assert.Equal(patch, BpsPatch.DecompressIfGzip(patch));
         Assert.True(BpsPatch.Apply(BpsPatch.DecompressIfGzip(gz), source).Success);
      }
   }
}
