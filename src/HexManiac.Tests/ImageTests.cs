using HavenSoft.HexManiac.Core.Models.Runs;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class ImageTests : BaseViewModelTestClass {
      [Fact]
      public void ValidHeaderIsRecognized() {
         var data = new byte[] { 0x10, 8, 0, 0, 0b000000, 0, 0, 0, 0, 0, 0, 0, 0 };
         Assert.NotEqual(-1, LZRun.IsCompressedLzData(data, 0));
      }

      [Theory]
      [InlineData(new byte[] { 0x00, 8, 0, 0, 0b000000, 0, 0, 0, 0, 0, 0, 0, 0 })] // first byte must be 0x10
      [InlineData(new byte[] { 0x10, 8, 1, 0, 0b000000, 0, 0, 0, 0, 0, 0, 0, 0 })] // must contain enough bytes to read the whole format
      [InlineData(new byte[] { 0x10, 6, 0, 0, 0b000001, 0, 0, 0, 0, 0, 0, 0, 0 })] // any unused bits at the end of the 'compression' byte must be 0
      [InlineData(new byte[] { 0x10, 7, 0, 0, 0b100000, 0, 0, 0, 0, 0, 0, 0, 0 })] // first bit of the first 'compression' byte must be 0, since compression looks back
      [InlineData(new byte[] { 0x10, 7, 0, 0, 0b010000, 0, 0, 4, 0, 0, 0, 0, 0 })] // compressed bytes cannot start further back than the start of the stream
      public void InvalidHeaderIsInvalid(byte[] data) {
         Assert.Equal(-1, LZRun.IsCompressedLzData(data, 0));
      }

      [Theory]
      [InlineData(
         new byte[] { 0x10, 8, 0, 0, 0b000000, 0, 0, 0, 0, 0, 0, 0, 0 },
         new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 }
      )]
      public void Decompress(byte[] compressed, byte[] uncompressed) {
         var result = LZRun.Decompress(compressed, 0);
         Assert.Equal(uncompressed, result);
      }

      [Fact]
      public void RunHasExpectedLength() {
         var data = new byte[] { 0x10, 8, 0, 0, 0b000000, 0, 0, 0, 0, 0, 0, 0, 0 };
         var run = new LZRun(data, 0);
         Assert.Equal(13, run.Length);
      }

      // TODO validate the dataformats that come back from the LZRun: length 1 for header, 3 for length, 1 for bitfield, 1 for raw, and 2 for compressed
      // TODO if an LZRun compressed segment is edited such that the segment becomes longer, the overall LZRun becomes shorter. Fix up the end as needed, clearing excess bytes.
      // TODO if an LZRun compressed segment is edited such that the segment becomes shorter, the overall LZRun becomes longer. Append to the end as needed, adding extra '00' bytes
      // TODO verify that an LZRun 1-byte header cannot be edited
      // TODO if an LZRun decompressed length is edited, fix up end as needed
      // TODO if an LZRun bitfield segment is edited, reinterpret everything after and fixup end as needed
      // TODO if an LZRun decompressed segment is edited, we're fine
      // TODO if an LZRun has a length requirement that isn't met (example, the image is known to be 32x32 in size), error if the length is changed
   }
}
