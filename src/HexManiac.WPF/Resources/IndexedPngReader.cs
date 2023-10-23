using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace HexManiac.WPF.Resources {
   public class IndexedPngReader : Stream {
      public static (int[,] pixels, IReadOnlyList<short> palette) Load(string fileName) {
         using (var inputStream = File.Open(fileName, FileMode.Open, FileAccess.Read)) {
            var stream = new IndexedPngReader(inputStream);
            stream.ReadPngSignature();
            while (stream.ReadChunk()) {}
            if (stream.palette == null) {
               throw new PngArgumentException("No PLTE chunk read.");
            }
            if (stream.pixels == null) {
               throw new PngArgumentException("No IDAT chunk read.");
            }
            return (stream.pixels, stream.palette);
         }
      }

      private readonly Stream inner;
      private readonly List<byte> bytesRead = new List<byte>();
      private uint width;
      private uint height;
      private int bitDepth;
      private int[,] pixels;
      private IReadOnlyList<short> palette;

      public override bool CanRead => inner.CanRead;

      public override bool CanSeek => inner.CanSeek;

      public override bool CanWrite => inner.CanWrite;

      public override long Length => inner.Length;

      public override long Position {
         get => inner.Position;
         set => inner.Position = value;
      }

      private IndexedPngReader(Stream inner) {
         this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
      }

      public override void Flush() => inner.Flush();

      public override int Read(byte[] buffer, int offset, int count) {
         int value = inner.Read(buffer, offset, count);
         bytesRead.AddRange(buffer.Skip(offset).Take(count));
         return value;
      }

      public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);

      public override void SetLength(long value) => inner.SetLength(value);

      public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
      
      public uint ReadInt() {
         // 32-bit int, big endian
         return ((uint)ReadByte() << 24)
            | ((uint)ReadByte() << 16)
            | ((uint)ReadByte() << 8)
            | ((uint)ReadByte() << 0);
      }

      private byte[] ReadBytes(int count) {
         var buffer = new byte[count];
         Read(buffer, 0, count);
         return buffer;
      }

      private void ReadPngSignature() {
         if (!Enumerable.SequenceEqual(ReadBytes(Png.Signature.Length), Png.Signature)) {
            throw new PngArgumentException("Not a PNG file.");
         }
      }

      private (int chunkSize, string chunkType) ReadChunkHeader() {
         var chunkSize = (int)ReadInt();
         bytesRead.Clear();
         var chunkType = Encoding.ASCII.GetString(ReadBytes(4));
         return (chunkSize, chunkType);
      }

      private void ReadCrc(String chunkType) {
         var computed = Force.Crc32.Crc32Algorithm.Compute(bytesRead.ToArray());
         var crc32Checksum = ReadInt();
         if (computed != crc32Checksum) {
            throw new PngArgumentException(String.Format($"{chunkType} chunk is corrupted (CRC check failed)."));
         }
      }

      private bool ReadChunk() {
         var (chunkSize, chunkType) = ReadChunkHeader();
         switch (chunkType){
            case "IHDR":
               if (chunkSize != 13) {
                  throw new PngArgumentException("IHDR chunk has invalid size.");
               }
               ReadIhdr();
               ReadCrc(chunkType);
               break;
            case "PLTE":
               if (chunkSize % 3 != 0) {
                  throw new PngArgumentException("PLTE chunk size not divisible by 3.");
               }
               ReadPlte(chunkSize);
               ReadCrc(chunkType);
               break;
            case "IDAT":
               ReadIdat(chunkSize);
               ReadCrc(chunkType);
               break;
            case "IEND":
               if (chunkSize != 0) {
                  throw new PngArgumentException("IEND chunk has invalid size.");
               }
               ReadIend();
               ReadCrc(chunkType);
               break;
            default:
               // skip all other chunks
               var _ = ReadBytes(chunkSize + 4);
               break;
         }
         return chunkType != "IEND";
      }

      private void ReadIhdr() {
         if (this.width != 0) {
            throw new PngArgumentException("Multiple IHDR chunks present.");
         }
         var width = ReadInt();
         var height = ReadInt();
         var bitDepth = ReadByte();
         var colourType = ReadByte();
         var compressionMethod = ReadByte();
         var filterMethod = ReadByte();
         var interlaceMethod = ReadByte();
         if (width == 0) {
            throw new PngArgumentException("Image has width of 0.");
         }
         if (height == 0) {
            throw new PngArgumentException("Image has height of 0.");
         }
         if (colourType != 3) {
            throw new PngArgumentException("Image is not palette indexed.");
         }
         if (bitDepth != 1 && bitDepth != 2 && bitDepth != 4 && bitDepth != 8) {
            throw new PngArgumentException($"Bit depth ({bitDepth}) is invalid for a palette indexed image.");
         }
         if (compressionMethod != 0) {
            throw new PngArgumentException($"Compression method ({compressionMethod}) is invalid.");
         }
         if (filterMethod != 0) {
            throw new PngArgumentException($"Image has a non-zero filter method ({filterMethod})");
         }
         if (interlaceMethod != 0) {
            throw new PngArgumentException($"Image has a non-zero interlace method ({interlaceMethod})");
         }

         this.width = width;
         this.height = height;
         this.bitDepth = bitDepth;
      }

      private void ReadPlte(int chunkSize) {
         if (palette != null) {
            throw new PngArgumentException("Multiple PLTE chunks present.");
         }
         palette = IndexedPng.ConvertToPalette(ReadBytes(chunkSize));
      }

      private void ReadIdat(int chunkSize) {
         if (pixels != null) {
            throw new NotImplementedException("Multiple IDAT chunks present.");
         }
         var compressedData = ReadBytes(chunkSize);
         var decompressedData = Png.ZLibDecompress(compressedData);

         pixels = IndexedPng.ConvertToPixels(decompressedData, width, height, bitDepth);
      }

      private void ReadIend() {
         // nothing to read since IEND chunk is always empty
      }
   }
}
