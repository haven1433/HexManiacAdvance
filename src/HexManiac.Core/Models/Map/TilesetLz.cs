using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using System;
using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.Models.Map {
   /// <summary>
   /// LZ77 compression (the format of the GBA BIOS: header 0x10 and a 3 byte length, then groups of 8 tokens behind a flag byte,
   /// a token is one plain byte or a copy of 3 to 18 bytes from up to 4096 bytes back) that makes the smallest data the format allows.
   /// The tile data of a tileset has to fit where the old compressed tiles are, or the tileset has to move: the smaller the data, the more often it can stay.
   /// The compression of <see cref="LZRun.Compress(IReadOnlyList{byte}, int, int)"/> only looks at even positions and even distances and makes data that is about 9% bigger than the data the game ships with.
   /// The data the game ships with uses every position and every distance except 1 (the picture memory is written 16 bits at a time, so the decompressor of the BIOS
   /// can't copy from the byte it has just written), and so does this: a copy never reaches back less than 2 bytes.
   /// </summary>
   public static class TilesetLz {
      public const int MinimumRun = 3, MaximumRun = 18, MinimumDistance = 2, MaximumDistance = 4096;

      /// <summary>The smallest LZ data that decompresses to <paramref name="data"/>, with the same header and flag layout the game's data has.</summary>
      public static byte[] Compress(byte[] data) {
         var length = data.Length;
         var bestLength = new byte[length];
         var bestDistance = new short[length];
         FindMatches(data, bestLength, bestDistance);

         // cost[i] = the fewest bits (a flag bit for every token, 8 for a plain byte, 16 for a copy) that the data from i on needs
         var cost = new int[length + 1];
         var choice = new byte[length]; // 1: plain byte, 3..18: copy of that many
         for (int i = length - 1; i >= 0; i--) {
            var best = cost[i + 1] + 9;
            var run = (byte)1;
            for (int l = MinimumRun; l <= bestLength[i]; l++) {
               if (cost[i + l] + 17 < best) (best, run) = (cost[i + l] + 17, (byte)l);
            }
            (cost[i], choice[i]) = (best, run);
         }

         var result = new List<byte>(length / 2 + 16) {
            0x10, // magic number
            (byte)length,
            (byte)(length >> 8),
            (byte)(length >> 16),
         };
         var flagAt = -1;
         for (int i = 0, token = 0; i < length; token++) {
            if (token % 8 == 0) {
               flagAt = result.Count;
               result.Add(0);
            }
            if (choice[i] == 1) {
               result.Add(data[i]);
               i++;
            } else {
               var run = choice[i];
               var distance = bestDistance[i] - 1;
               result[flagAt] |= (byte)(0x80 >> (token % 8));
               result.Add((byte)(((run - MinimumRun) << 4) | (distance >> 8)));
               result.Add((byte)distance);
               i += run;
            }
         }
         return result.ToArray();
      }

      /// <summary>For every position: the longest copy (up to 18 bytes) of earlier data that starts there, and how far back it is.</summary>
      private static void FindMatches(byte[] data, byte[] bestLength, short[] bestDistance) {
         const int hashSize = 1 << 16;
         var length = data.Length;
         var head = new int[hashSize];
         var previous = new int[length];
         Array.Fill(head, -1);
         for (int i = 0; i < length; i++) {
            if (i + MinimumRun <= length) {
               var hash = Hash(data, i);
               var limit = Math.Min(MaximumRun, length - i);
               var (longest, back) = (0, 0);
               for (int candidate = head[hash]; candidate >= 0 && i - candidate <= MaximumDistance; candidate = previous[candidate]) {
                  var distance = i - candidate;
                  if (distance < MinimumDistance) continue; // (1 back is not allowed)
                  var run = 0;
                  while (run < limit && data[candidate + run] == data[i + run]) run++;
                  if (run > longest) (longest, back) = (run, distance);
                  if (longest == limit) break;
               }
               if (longest >= MinimumRun) (bestLength[i], bestDistance[i]) = ((byte)longest, (short)back);
               previous[i] = head[hash];
               head[hash] = i;
            }
         }
      }

      private static int Hash(byte[] data, int i) => ((data[i] * 251 + data[i + 1]) * 251 + data[i + 2]) & 0xFFFF;
   }
}
