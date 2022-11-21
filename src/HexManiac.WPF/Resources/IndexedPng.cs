using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

// Edited from code provided by wet blanket#6152 from discord
// based on https://github.com/EliotJones/BigGustave/tree/master/src/BigGustave
// massive thanks!

namespace HexManiac.WPF.Resources {
   public class IndexedPng : Stream {
      public static void Save(string filename, int[,] pixels, IReadOnlyList<short> palette) {
         using (var outputStream = File.Create(filename)) {
            var stream = new IndexedPng(outputStream);
            var width = pixels.GetLength(0);
            var height = pixels.GetLength(1);
            var bitDepth = palette.Count <= 16 ? 4 : 8;
            stream.WritePngHeader();
            stream.WriteIhdr(width, height, bitDepth);
            stream.WritePlte(ConvertPalette(palette));
            stream.WriteIdat(pixels, bitDepth);
            stream.WriteIend();
         }
      }

      private static readonly byte[] PngHeader = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

      private readonly Stream inner;
      private readonly List<byte> written = new List<byte>();

      public override bool CanRead => inner.CanRead;

      public override bool CanSeek => inner.CanSeek;

      public override bool CanWrite => inner.CanWrite;

      public override long Length => inner.Length;

      public override long Position {
         get => inner.Position;
         set => inner.Position = value;
      }

      private IndexedPng(Stream inner) {
         this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
      }

      public override void Flush() => inner.Flush();

      public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

      public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);

      public override void SetLength(long value) => inner.SetLength(value);

      public override void Write(byte[] buffer, int offset, int count) {
         written.AddRange(buffer.Skip(offset).Take(count));
         inner.Write(buffer, offset, count);
      }

      private void WriteInt(int value) {
         // 32-bit int, big endian
         WriteByte((byte)(value >> 24));
         WriteByte((byte)(value >> 16));
         WriteByte((byte)(value >> 8));
         WriteByte((byte)(value >> 0));
      }

      private void WriteChunkHeader(string header) {
         Debug.Assert(header.Length == 4);
         written.Clear();
         Write(Encoding.ASCII.GetBytes(header), 0, header.Length);
      }

      private void WriteCrc() {
         var checksum = Force.Crc32.Crc32Algorithm.Compute(written.ToArray());
         WriteInt((int)checksum);
      }

      private void WritePngHeader() {
         Write(PngHeader, 0, PngHeader.Length);
      }

      private void WriteIhdr(int width, int height, int bitDepth) {
         WriteInt(13);
         WriteChunkHeader("IHDR");
         WriteInt(width);
         WriteInt(height);
         WriteByte((byte)bitDepth);
         WriteByte((byte)3); // Indexed-colour
         WriteByte((byte)0); // Compression method 0
         WriteByte((byte)0); // Filter method 0
         WriteByte((byte)0); // No interlace
         WriteCrc();
      }

      private void WritePlte(byte[] palette) {
         WriteInt(palette.Length);
         WriteChunkHeader("PLTE");
         Write(palette, 0, palette.Length);
         WriteCrc();
      }

      private void WriteIdat(int[,] pixels, int bitDepth) {
         var uncompressedData = PrepareData(pixels, bitDepth);
         var compressedData = Compress(uncompressedData);

         WriteInt(compressedData.Length);
         WriteChunkHeader("IDAT");
         Write(compressedData, 0, compressedData.Length);
         WriteCrc();
      }

      private void WriteIend() {
         WriteInt(0);
         WriteChunkHeader("IEND");
         WriteCrc();
      }

      private static byte[] ConvertPalette(IReadOnlyList<short> palette) {
         var result = new byte[3 * palette.Count];
         var i = 0;
         foreach (var color in palette) {
            result[i++] = (byte)(((color >> 10) & 31) * 255 / 31);
            result[i++] = (byte)(((color >> 5) & 31) * 255 / 31);
            result[i++] = (byte)(((color >> 0) & 31) * 255 / 31);
         }
         return result;
      }

      private static byte[] PrepareData(int[,] pixels, int bitDepth) {
         var width = pixels.GetLength(0);
         var height = pixels.GetLength(1);
         byte[] result;
         int i = 0;

         if (bitDepth == 4) {
            Debug.Assert(width % 2 == 0, "Width must be even.");
            result = new byte[(width / 2 + 1) * height];
            for (var y = 0; y < height; y++) {
               result[i++] = 0;
               for (var x = 0; x < width; x += 2) {
                  var pixel1 = pixels[x, y] % 16;
                  var pixel2 = pixels[x + 1, y] % 16;
                  result[i++] = (byte)((pixel1 << 4) | pixel2);
               }
            }
         } else if (bitDepth == 8) {
            result = new byte[(width + 1) * height];
            for (var y = 0; y < height; y++) {
               result[i++] = 0;
               for (var x = 0; x < width; x++) {
                  result[i++] = (byte)pixels[x, y];
               }
            }
         } else {
            throw new ArgumentException();
         }

         return result;
      }

      private static byte[] Compress(byte[] data) {
         using (var compressStream = new MemoryStream())
         using (var compressor = new DeflateStream(compressStream, CompressionLevel.Optimal, true)) {
            compressor.Write(data, 0, data.Length);
            compressor.Close();

            compressStream.Seek(0, SeekOrigin.Begin);

            var result = new byte[1 + 1 + compressStream.Length + 4];
            var i = 0;

            // https://www.ietf.org/rfc/rfc1950.txt
            // CMF
            result[i++] = (7 << 4) | 8;

            // FLG
            result[i++] = 1;

            // compressed data
            int streamValue;
            while ((streamValue = compressStream.ReadByte()) != -1) {
               result[i++] = (byte)streamValue;
            }

            // ADLER32
            var checksum = Adler32(data);
            result[i++] = (byte)(checksum >> 24);
            result[i++] = (byte)(checksum >> 16);
            result[i++] = (byte)(checksum >> 8);
            result[i++] = (byte)(checksum >> 0);

            return result;
         }
      }

      private static int Adler32(IEnumerable<byte> data) {
         var s1 = 1;
         var s2 = 0;

         foreach (var b in data) {
            s1 = (s1 + b) % 65521;
            s2 = (s1 + s2) % 65521;
         }

         return (s2 << 16) | s1;
      }
   }
}
