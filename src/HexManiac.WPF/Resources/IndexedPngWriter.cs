using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace HexManiac.WPF.Resources {
   public class IndexedPngWriter : Stream {
      public static void Save(string filename, int[,] pixels, IReadOnlyList<short> palette) {
         if (palette.Count > 256) {
            throw new PngArgumentException("An indexed PNG file cannot have more than 256 colors.");
         }
         for (var x = 0; x < pixels.GetLength(0); x++) {
            for (var y = 0; y < pixels.GetLength(1); y++) {
               var pixel = pixels[x, y];
               if (pixel < 0 || pixel >= palette.Count()) {
                  throw new PngArgumentException(String.Format($"Pixel at {x}, {y} ({pixel}) is out of range."));
               }
            }
         }
         using (var outputStream = File.Create(filename)) {
            var stream = new IndexedPngWriter(outputStream);
            var width = pixels.GetLength(0);
            var height = pixels.GetLength(1);
            var bitDepth = palette.Count <= 16 ? 4 : 8;
            stream.WritePngSignature();
            stream.WriteIhdr(width, height, bitDepth);
            stream.WritePlte(palette);
            stream.WriteIdat(pixels, bitDepth);
            stream.WriteIend();
         }
      }

      private readonly Stream inner;
      private readonly List<byte> written = new List<byte>();

      public override bool CanRead => false;

      public override bool CanSeek => false;

      public override bool CanWrite => inner.CanWrite;

      public override long Length => inner.Length;

      public override long Position {
         get => inner.Position;
         set => inner.Position = value;
      }

      private IndexedPngWriter(Stream inner) {
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

      private void WritePngSignature() {
         Write(Png.Signature, 0, Png.Signature.Length);
      }

      private void WriteChunkHeader(int chunkSize, string chunkType) {
         WriteInt(chunkSize);
         Debug.Assert(chunkType.Length == 4);
         written.Clear();
         Write(Encoding.ASCII.GetBytes(chunkType), 0, chunkType.Length);
      }

      private void WriteCrc() {
         var checksum = Force.Crc32.Crc32Algorithm.Compute(written.ToArray());
         WriteInt((int)checksum);
      }

      private void WriteIhdr(int width, int height, int bitDepth) {
         WriteChunkHeader(13, "IHDR");
         WriteInt(width);
         WriteInt(height);
         WriteByte((byte)bitDepth);
         WriteByte((byte)3); // Indexed-colour
         WriteByte((byte)0); // Compression method 0
         WriteByte((byte)0); // Filter method 0
         WriteByte((byte)0); // No interlace
         WriteCrc();
      }

      private void WritePlte(IReadOnlyList<short> palette) {
         var data = IndexedPng.ConvertFromPalette(palette);
         WriteChunkHeader(data.Length, "PLTE");
         Write(data, 0, data.Length);
         WriteCrc();
      }

      private void WriteIdat(int[,] pixels, int bitDepth) {
         var uncompressedData = IndexedPng.ConvertFromPixels(pixels, bitDepth);
         var compressedData = Png.ZLibCompress(uncompressedData);
         WriteChunkHeader(compressedData.Length, "IDAT");
         Write(compressedData, 0, compressedData.Length);
         WriteCrc();
      }

      private void WriteIend() {
         WriteChunkHeader(0, "IEND");
         WriteCrc();
      }
   }
}
