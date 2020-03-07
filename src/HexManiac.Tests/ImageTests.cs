using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class ImageTests : BaseViewModelTestClass {
      [Fact]
      public void ValidHeaderIsRecognized() {
         var data = new byte[] { 0x10, 8, 0, 0, 0b000000, 0, 0, 0, 0, 0, 0, 0, 0 };
         Assert.NotEqual(-1, LZRun.IsCompressedLzData(data, 0));
      }

      [Theory]
      [InlineData(new byte[] { 0x00, 8, 0, 0, 0b00000000, 0, 0, 0, 0, 0, 0, 0, 0 })] // first byte must be 0x10
      [InlineData(new byte[] { 0x10, 8, 1, 0, 0b00000000, 0, 0, 0, 0, 0, 0, 0, 0 })] // must contain enough bytes to read the whole format
      [InlineData(new byte[] { 0x10, 6, 0, 0, 0b00000001, 0, 0, 0, 0, 0, 0, 0, 0 })] // any unused bits at the end of the 'compression' byte must be 0
      [InlineData(new byte[] { 0x10, 7, 0, 0, 0b10000000, 0, 0, 0, 0, 0, 0, 0, 0 })] // first bit of the first 'compression' byte must be 0, since compression looks back
      [InlineData(new byte[] { 0x10, 7, 0, 0, 0b01000000, 0, 0, 4, 0, 0, 0, 0, 0 })] // compressed bytes cannot start further back than the start of the stream
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

      [Fact]
      public void RunHasExpectedDataFormats() {
         var data = new byte[] {
            0x10, 4, 0, 0, // header
            0b01000000,    // group
            0x30,          // uncompressed 30
            0x00, 0x00,    // compressed   3:1
         }; // biases should make the compressed token length 3, offset 1 (3:1)
         for (int i = 0; i < data.Length; i++) Model[i] = data[i];
         var run = new LZRun(Model, 0);

         using (ModelCacheScope.CreateScope(Model)) {
            Assert.IsType<LzMagicIdentifier>(run.CreateDataFormat(Model, 0));
            Assert.IsType<Integer>(run.CreateDataFormat(Model, 1));
            Assert.IsType<Integer>(run.CreateDataFormat(Model, 2));
            Assert.IsType<Integer>(run.CreateDataFormat(Model, 3));
            Assert.IsType<LzGroupHeader>(run.CreateDataFormat(Model, 4));
            Assert.IsType<LzUncompressed>(run.CreateDataFormat(Model, 5));
            Assert.IsType<LzCompressed>(run.CreateDataFormat(Model, 6));
            Assert.IsType<LzCompressed>(run.CreateDataFormat(Model, 7));
         }
      }

      [Fact]
      public void ExtendCompressedLzTokenShortensLzRun() {
         SetFullModel(0xFF);
         CreateLzRun(0,
            0x10, 13, 0, 0, // header
            0b01110000,    // group
            0x30,          // uncompressed 30
            0x00, 0x00,    // compressed   3:1    (we're going to make this one longer)
            0x20, 0x00,    // compressed   5:1    (this one will end up shorter)
            0x00, 0x00,    // compressed   3:1    (this one should go away, which means the group header needs to be changed)
            0x30           // uncompressed 30     (this one should go away)
            );

         // make the actual edit
         ViewPort.Edit("@07 8:1 ");

         // check that compressed segment 1 got longer
         Assert.Equal(0x50, Model[ 6]);
         Assert.Equal(0x00, Model[ 7]);

         // check that compressed segment 2 got shorter
         Assert.Equal(0x10, Model[ 8]);
         Assert.Equal(0x00, Model[ 9]);

         // check that compressed segment 3 is gone
         Assert.Equal(10, Model.GetNextRun(0).Length);
         Assert.Equal(0b01100000, Model[4]);
         Assert.Equal(0xFF, Model[10]);
         Assert.Equal(0xFF, Model[11]);

         // check that the final uncompressed segment is gone
         Assert.Equal(0xFF, Model[12]);
      }

      [Fact]
      public void ExtendLastCompressedTokenBeyondLengthErrors() {
         SetFullModel(0xFF);
         CreateLzRun(0,
            0x10, 4, 0, 0, // header (uncompressed length = 4)
            0b01000000,     // group
            0x30,           // uncompressed 30
            0x00, 0x00);    // compressed   3:1

         ViewPort.Edit("@06 4:1 "); // too long! You said you only want 3 more bytes, but this is 4 bytes!

         Assert.Single(Errors);
      }

      [Fact]
      public void ContractCompressedLzTokenLengthensAndRepoints() {
         SetFullModel(0xFF);
         CreateLzRun(0,
            0x10, 6, 0, 0, // header (uncompressed length = 6)
            0b01000000,     // group
            0x30,           // uncompressed 30
            0x20, 0x00);    // compressed   5:1
         Model[8] = 0xBA;   // random byte that we don't want to overwrite

         ViewPort.Edit("@00 ^bob @06 4:1 "); // too short! We need 1 more byte at the end!

         var address = Model.GetAddressFromAnchor(ViewPort.CurrentChange, -1, "bob");
         var run = (LZRun)Model.GetNextRun(address);

         Assert.Equal(9, run.Length);
         Assert.Single(Messages); // message about the repoint
      }

      [Fact]
      public void CanTypeLZOverHeader() {
         SetFullModel(0xFF);
         CreateLzRun(0,
            0x10, 6, 0, 0, // header (uncompressed length = 6)
            0b01000000,     // group
            0x30,           // uncompressed 30
            0x20, 0x00);    // compressed   5:1

         ViewPort.Edit("@00 lz "); // too short! We need 1 more byte at the end!

         Assert.Equal(new Point(1, 0), ViewPort.SelectionStart);
         Assert.Equal(new Point(3,0), ViewPort.SelectionEnd);
      }

      [Fact]
      public void CanEditDecompressedLengthInCompressedData() {
         SetFullModel(0xFF);
         CreateLzRun(0,
            0x10, 6, 0, 0, // header (uncompressed length = 6)
            0b01000000,     // group
            0x30,           // uncompressed 30
            0x20, 0x00);    // compressed   5:1

         ViewPort.Edit("@01 8 "); // overall data should be longer now

         Assert.Equal(10, Model.GetNextRun(0).Length);
      }

      // TODO if an LZRun bitfield segment is edited, reinterpret everything after and fixup end as needed
      // TODO if an LZRun has a length requirement that isn't met (example, the image is known to be 32x32 in size), error if the length is changed
      // TODO LZRun is IStreamRun and the stream is the decompressed data
      // TODO changing decompressed LZ Stream via Stream tool updates the run
      // TODO double-clicking selects the entire compressed run

      private void CreateLzRun(int start, params byte[] data) {
         for (int i = 0; i < data.Length; i++) Model[start + i] = data[i];
         var run = new LZRun(Model, start);
         Model.ObserveRunWritten(ViewPort.CurrentChange, run);
      }
   }
}
