using System;

namespace HavenSoft.HexManiac.Core.Models.Runs.Sprites {
   /// <summary>
   /// GBA LZ77 (type 0x10) compressor with an optimal parse, the same algorithm as CUBE's build tool (tools/gbagfx/lz.c):
   /// every position gets its longest match (3 to 18 bytes, distance 2 to 0x1000, the nearest distance wins a tie),
   /// then a backward pass picks the cheapest mix of literals (9 bits) and matches (17 bits) for the whole stream.
   /// The result is an ordinary LZ77 stream (LZ77UnCompWram/Vram safe, distance >= 2) that decompresses to exactly the input.
   /// It is never larger than what the greedy compressors produce for the same data, and usually a few percent smaller.
   /// The output only depends on the input bytes.
   /// </summary>
   public static class LzOptimalCompressor {
      public const int MinimumVramDistance = 2;

      /// <param name="padToFour">Pad the stream with zeros to a multiple of 4 bytes, like the build tool does.</param>
      public static byte[] Compress(byte[] src, bool padToFour = false, int minDistance = MinimumVramDistance) {
         if (src == null || src.Length == 0 || src.Length > 0xFFFFFF) throw new ArgumentException("LZ77 can compress 1 to 16 MB of data.", nameof(src));
         if (minDistance < 1) minDistance = 1;
         int n = src.Length;
         var matchLength = new int[n + 1];
         var matchDistance = new int[n + 1];
         var chainHead = new int[0x10000];
         var chainPrev = new int[n + 1];
         Array.Fill(chainHead, -1);

         // 1. longest match at every position (hash chains over the next 3 bytes, newest = nearest first)
         for (int pos = 0; pos < n; pos++) {
            chainPrev[pos] = -1;
            if (pos + 3 > n) continue;
            int hash = ((src[pos] << 8) ^ (src[pos + 1] << 4) ^ (src[pos + 2] * 0x9E5)) & 0xFFFF;
            for (int candidate = chainHead[hash]; candidate >= 0 && pos - candidate <= 0x1000; candidate = chainPrev[candidate]) {
               int distance = pos - candidate;
               if (distance < minDistance) continue;
               int length = 0;
               while (length < 18 && pos + length < n && src[candidate + length] == src[pos + length]) length++;
               if (length > matchLength[pos]) {
                  matchLength[pos] = length;
                  matchDistance[pos] = distance;
                  if (length == 18) break;
               }
            }
            chainPrev[pos] = chainHead[hash];
            chainHead[hash] = pos;
         }

         // 2. cheapest encoding (in bits) of the rest of the data from every position; a longer match wins a tie
         var cost = new int[n + 1];
         var choice = new int[n + 1];
         for (int pos = n - 1; pos >= 0; pos--) {
            cost[pos] = cost[pos + 1] + 9;
            choice[pos] = 1;
            for (int length = 3; length <= matchLength[pos]; length++) {
               int c = cost[pos + length] + 17;
               if (c <= cost[pos]) {
                  cost[pos] = c;
                  choice[pos] = length;
               }
            }
         }

         // 3. write the stream
         var dest = new byte[4 + n + (n + 7) / 8 + 4];
         dest[0] = 0x10;
         dest[1] = (byte)n;
         dest[2] = (byte)(n >> 8);
         dest[3] = (byte)(n >> 16);
         int srcPos = 0, destPos = 4;
         while (srcPos < n) {
            int flagIndex = destPos++;
            dest[flagIndex] = 0;
            for (int i = 0; i < 8 && srcPos < n; i++) {
               int length = choice[srcPos];
               if (length >= 3) {
                  int distance = matchDistance[srcPos] - 1;
                  dest[flagIndex] |= (byte)(0x80 >> i);
                  dest[destPos++] = (byte)(((length - 3) << 4) | (distance >> 8));
                  dest[destPos++] = (byte)distance;
                  srcPos += length;
               } else {
                  dest[destPos++] = src[srcPos++];
               }
            }
         }
         if (padToFour) while (destPos % 4 != 0) dest[destPos++] = 0;

         var result = new byte[destPos];
         Array.Copy(dest, result, destPos);
         return result;
      }
   }
}
