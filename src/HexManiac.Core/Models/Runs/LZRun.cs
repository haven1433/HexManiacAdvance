using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;

namespace HavenSoft.HexManiac.Core.Models.Runs {
   public class LZRun : BaseRun {
      public override int Length { get; }

      public override string FormatString { get; }

      public LZRun(IReadOnlyList<byte> data, int start, IReadOnlyList<int> sources = null) : base(start, sources) {
      }

      // ported from another project
      public static byte[] UncompressLZ(byte[] memory, int offset) {
         // all LZ compressed data starts with 0x10
         if (memory[offset] != 0x10) return null;
         int length = (memory[offset + 3] << 16) | (memory[offset + 2] << 8) | (memory[offset + 1] << 0);
         var uncompressed = new List<byte>();
         offset += 4;

         while (true) {
            // always start with a bitfield
            // it encodes the next 8 steps
            // "1" means its a runlength dictionary compression
            //     and the dictionary is the most recent decompressed data
            // "0" means its decompressed
            // this makes a fully compressed data stream only 12.5% longer than it was when it started (at worst).
            var bitField = memory[offset++];
            var bits = new[] {
               (bitField & 0x80) != 0,
               (bitField & 0x40) != 0,
               (bitField & 0x20) != 0,
               (bitField & 0x10) != 0,
               (bitField & 0x08) != 0,
               (bitField & 0x04) != 0,
               (bitField & 0x02) != 0,
               (bitField & 0x01) != 0,
            };

            foreach (var bit in bits) {
               if (bit) {
                  // the next two bytes explain the dictionary position/length of the next set of bytes.
                  // aaaa bbbb . bbbb bbbb

                  // aaaa : the runlength of the dictionary encoding. Never less than 3, so the run
                  //        is encoded as 3 smaller (to allow for slightly larger runs).
                  //        possible final values: 3-18
                  // bbbb bbbb bbbb : how far from the end of the stream to start reading for the run.
                  //                  never 0, so the value is encoded as 1 smaller to allow for slightly
                  //                  longer backtracks.
                  //                  possible final values: 1-4096

                  if (offset >= memory.Length) return null;
                  var byte1 = memory[offset++];
                  var runLength = (byte1 >> 4) + 3;
                  var runOffset_upper = (byte1 & 0xF) << 8;
                  var runOffset_lower = memory[offset++];
                  var runOffset = (runOffset_lower | runOffset_upper) + 1;
                  if (runOffset > uncompressed.Count) return null;
                  foreach (var i in Enumerable.Range(0, runLength)) {
                     uncompressed.Add(uncompressed[uncompressed.Count - runOffset]);
                     if (uncompressed.Count == length) return uncompressed.ToArray();
                  }
               } else {
                  uncompressed.Add(memory[offset++]);
                  if (uncompressed.Count == length) return uncompressed.ToArray();
               }
            }
         }
      }

      public static bool IsCompressedLzData(byte[] data, int start) => false;

      public static byte[] Decompress(byte[] data, int start) => null;

      public override IDataFormat CreateDataFormat(IDataModel data, int index) {
         throw new NotImplementedException();
      }

      protected override BaseRun Clone(IReadOnlyList<int> newPointerSources) {
         throw new NotImplementedException();
      }
   }
}
