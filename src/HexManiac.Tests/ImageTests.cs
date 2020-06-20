using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System.Collections.Generic;
using System.Linq;
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
         var model = new PokemonModel(data);
         var run = new LZRun(model, 0);
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
         Assert.Equal(0x50, Model[6]);
         Assert.Equal(0x00, Model[7]);

         // check that compressed segment 2 got shorter
         Assert.Equal(0x10, Model[8]);
         Assert.Equal(0x00, Model[9]);

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

         ViewPort.Edit("@20 @00 lz "); // too short! We need 1 more byte at the end!

         Assert.Equal(new Point(1, 0), ViewPort.SelectionStart);
         Assert.Equal(new Point(3, 0), ViewPort.SelectionEnd);
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

      /// <summary>
      /// Note that group-header changes near the end of the run might not be fully obeyed.
      /// Can this cause problems when pasting over an existing run with a run that has the same decompressed length?
      /// </summary>
      [Fact]
      public void CanEditLzGroupHeaderBitfield() {
         SetFullModel(0xFF);
         CreateLzRun(0,
            0x10, 6, 0, 0, // header (uncompressed length = 6)
            0b01000000,     // group
            0x30,           // uncompressed 30
            0x20, 0x00);    // compressed   5:1

         ViewPort.Edit("@04 00 "); // override bitfield to be 'none compressed'
         // there were 3 bytes after the group header, so an additional 3 bytes are needed to make the uncompressed data length 6

         Assert.Equal(4 + 1 + 3 + 2, Model.GetNextRun(0).Length); // header, group header, 3 uncompressed bytes, auto-fill with a compressed segment
         Assert.Equal(0x10, Model[4]);

         ViewPort.Edit("@04 08 "); // note that the last compressed segment now straddles into unknown data.

         Assert.Equal(4 + 1 + 4 + 1 + 1, Model.GetNextRun(0).Length); // header, group header, 4 uncompressed bytes, then 2 uncompressed bytes because there's not enough data left to be compressed
         Assert.Equal(0, Model[4]);
      }

      [Fact]
      public void CanEditLzDataViaStreamTool() {
         SetFullModel(0xFF);
         CreateLzRun(0,
            0x10, 6, 0, 0, // header (uncompressed length = 6)
            0b01000000,     // group
            0x30,           // uncompressed 30
            0x20, 0x00);    // compressed   5:1

         ViewPort.Edit("@04 ");
         Assert.Equal("30 30 30 30 30 30", ViewPort.Tools.StringTool.Content);

         ViewPort.Tools.StringTool.Content = "30 20 30 30 30 30"; // should result in 30 20 30 (3:1)

         var run = Model.GetNextRun(0);
         Assert.Equal(0b00010000, Model[4]);
         Assert.Equal(0x30, Model[5]);
         Assert.Equal(0x20, Model[6]);
         Assert.Equal(0x30, Model[7]);
         Assert.Equal(0x00, Model[8]);
         Assert.Equal(0x00, Model[9]);
         Assert.Equal(10, run.Length);
         Assert.IsAssignableFrom<IStreamRun>(run);
      }

      [Fact]
      public void CanSelectWholeCompressedRun() {
         SetFullModel(0xFF);
         CreateLzRun(0,
            0x10, 6, 0, 0, // header (uncompressed length = 6)
            0b01000000,     // group
            0x30,           // uncompressed 30
            0x20, 0x00);    // compressed   5:1

         ViewPort.ExpandSelection(0, 0);

         Assert.True(ViewPort.IsSelected(new Point(7, 0)));
      }

      [Fact]
      public void ImageToolExists() {
         Model.ObserveRunWritten(ViewPort.CurrentChange, new SpriteRun(0x10, new SpriteFormat(4, 2, 2, null)));
         Model.ObserveRunWritten(ViewPort.CurrentChange, new PaletteRun(0x100, new PaletteFormat(4, 1)));
         ViewPort.SelectionStart = new Point(2, 2);
         var tools = ViewPort.Tools;

         Assert.Equal(tools.IndexOf(tools.SpriteTool), tools.SelectedIndex);
         Assert.Equal(0x10, tools.SpriteTool.SpriteAddress);
         Assert.Equal(Pointer.NULL, tools.SpriteTool.PaletteAddress);
         Assert.False(tools.SpriteTool.PreviousSpritePage.CanExecute(null));
         Assert.False(tools.SpriteTool.NextSpritePage.CanExecute(null));
         Assert.False(tools.SpriteTool.PreviousPalettePage.CanExecute(null));
         Assert.False(tools.SpriteTool.NextPalettePage.CanExecute(null));
      }

      [Fact]
      public void CanAddUncompressedImageRunsFromViewPort() {
         ViewPort.Edit("^sprite`ucs4x2x2`");
         var run = Model.GetNextRun(0);
         Assert.Equal(32 * 2 * 2, run.Length);
         Assert.IsAssignableFrom<ISpriteRun>(run);

         ViewPort.Edit("@100 ^pal`ucp4`");
         run = Model.GetNextRun(0x100);
         Assert.Equal(32, run.Length);
         Assert.IsAssignableFrom<IPaletteRun>(run);
      }

      [Fact]
      public void CanCopyCompressedSprite() {
         ViewPort.Edit("10 20 @00 ^run`lzs4x1x1`");
         var fileSystem = new StubFileSystem();
         ViewPort.ExpandSelection(0, 0);
         ViewPort.Copy.Execute(fileSystem);
         Assert.StartsWith("^run`lzs4x1x1` lz 32 00 ", fileSystem.CopyText);
      }

      [Fact]
      public void CanCopyCompressedPalette() {
         ViewPort.Edit("10 20 @00 ^run`lzp4`");
         var fileSystem = new StubFileSystem();
         ViewPort.ExpandSelection(0, 0);
         ViewPort.Copy.Execute(fileSystem);
         Assert.StartsWith("^run`lzp4` lz 32 00 ", fileSystem.CopyText);
      }

      [Fact]
      public void CanCopyUncompressedSprite() {
         ViewPort.Edit("^run`ucs4x1x1`");
         var fileSystem = new StubFileSystem();
         ViewPort.ExpandSelection(0, 0);
         ViewPort.Copy.Execute(fileSystem);
         Assert.StartsWith("^run`ucs4x1x1` 00 ", fileSystem.CopyText);
      }

      [Fact]
      public void CanCopyUncompressedPalette() {
         ViewPort.Edit("^run`ucp4`");
         var fileSystem = new StubFileSystem();
         ViewPort.ExpandSelection(0, 0);
         ViewPort.Copy.Execute(fileSystem);
         Assert.StartsWith("^run`ucp4` 0:0:0 ", fileSystem.CopyText);
      }

      [Fact]
      public void CanAddLzTilesetAndTilemapFromViewPort() {
         Model.ExpandData(ViewPort.CurrentChange, 0x400);
         ViewPort.Refresh();

         // Arrange tileset data
         var tileByteLength = 8 * 8 / 2;
         var tileCount = 11;
         var lzData = LZRun.Compress(new byte[tileByteLength * tileCount], 0, tileByteLength * tileCount);
         for (int i = 0; i < lzData.Count; i++) Model[0x20 + i] = lzData[i];

         // Act: add tileset run
         ViewPort.Edit("<20> @20 ^tileset`lzt4`"); // 4 bits per pixel

         // Assert: tilesets show up as ISpriteRuns for the image tool
         var run = Model.GetNextRun(0x20) as ISpriteRun;

         // Assert: tilesets have 1 page
         Assert.Equal(1, run.Pages);

         // Assert: tilesets figure out dimensions dynamically.
         // They attempt to show in a square if possible,
         //   then add extra needed full columns,
         //   then add extra single tiles in a bottom row.
         Assert.Equal(4, run.SpriteFormat.TileWidth);
         Assert.Equal(3, run.SpriteFormat.TileHeight);


         // Arrange tilemap data
         lzData = LZRun.Compress(new byte[2 * 16], 0, 2 * 16);
         for (int i = 0; i < lzData.Count; i++) Model[0x200 + i] = lzData[i];

         // Act: add tilemap run
         ViewPort.Edit("@10 <200> @0200 ^tilemap`lzm4x4x4|tileset`"); // 4 bits per pixel, 4 tiles wide, 4 tiles tall.

         // Assert: tilemaps show up as ISpriteRun for the image tool
         run = Model.GetNextRun(0x200) as ISpriteRun;

         // Assert: tilemaps have 1 page
         Assert.Equal(1, run.Pages);

         // Assert: tilemaps have the specified dimensions.
         Assert.Equal(4, run.SpriteFormat.TileWidth);
         Assert.Equal(4, run.SpriteFormat.TileHeight);
      }

      [Fact]
      public void DoNotRenameAnchorWhenCreatingAPaletteAutomatically() {
         ViewPort.Edit("^bob @20 ^tom @bob ");
         var items = ViewPort.GetContextMenuItems(new Point(0, 0));
         var item = items.Single(element => element.Text == "View as 16-color palette");
         item.Command.Execute();

         var anchor = Model.GetAnchorFromAddress(-1, 0);
         Assert.Equal("bob", anchor);
      }

      [Fact]
      public void LoosePaletteCanFindSpriteThatUsesIt() {
         ViewPort.Edit("@00 ^pal`ucp4`");
         ViewPort.Edit("@40 ^sprite`ucs4x1x1|pal`");
         ViewPort.Edit("@80 ^tiles`uct4x1|pal`");

         var run = (IPaletteRun)Model.GetNextRun(0x00);
         IReadOnlyList<ISpriteRun> sprites = run.FindDependentSprites(Model);

         sprites = sprites.OrderBy(sprite => sprite.Start).ToList();
         Assert.Equal(0x40, sprites[0].Start);
         Assert.Equal(0x80, sprites[1].Start);
      }

      [Fact]
      public void TablePaletteCanFindSpritesThatUseIt() {
         CreateTextTable("names", 0, "Adam", "Bob");
         ViewPort.Edit("@10 <0020> <0040> @10 ^sprites[pointer<`ucs4x1x1`>]names "); // note that the sprite doesn't reference the palette by name.
         ViewPort.Edit("@60 <0070> <0090> @60 ^palettes[pointer<`ucp4`>]names ");    // the palette should pick it up by index

         var run = (IPaletteRun)Model.GetNextRun(0x70);
         var sprite1 = run.FindDependentSprites(Model).Single();
         Assert.Equal(0x20, sprite1.Start);

         run = (IPaletteRun)Model.GetNextRun(0x90);
         var sprite2 = run.FindDependentSprites(Model).Single();
         Assert.Equal(0x40, sprite2.Start);
      }

      [Fact]
      public void IndexPaletteCanFindSpritesThatUseIt() {
         CreateTextTable("names", 0, "Adam", "Bob");
         ViewPort.Edit("@10 <0020> <0040> @10 ^palettes[pointer<`ucp4`>]2 ");
         ViewPort.Edit("@60 ^palindex[id:palettes]names 1 0 ");
         ViewPort.Edit("@70 <0080> <00A0> @70 ^sprites[pointer<`ucs4x1x1|palindex`>]names ");

         var run = (IPaletteRun)Model.GetNextRun(0x20); // palette 0, used by Bob
         var sprite = run.FindDependentSprites(Model).Single();
         Assert.Equal(0xA0, sprite.Start);
      }

      [Fact]
      public void CanFindPalettesForSpriteInTable() {
         CreateTextTable("names", 0, "Adam", "Bob");
         ViewPort.Edit("@10 <0020> <0040> @10 ^sprites[pointer<`ucs4x1x1`>]names "); // note that the sprite doesn't reference the palette by name.
         ViewPort.Edit("@60 <0070> <0090> @60 ^palettes[pointer<`ucp4`>]names ");    // the palette should pick it up by index

         var run = (ISpriteRun)Model.GetNextRun(0x20);
         var palette = run.FindRelatedPalettes(Model).Single();

         Assert.Equal(0x70, palette.Start);
      }

      [Fact]
      public void CanFindPalettesForSpriteOutsideTable() {
         ViewPort.Edit("@00 ^pal`ucp4`");
         ViewPort.Edit("@40 ^sprite`ucs4x1x1|pal`");

         var run = (ISpriteRun)Model.GetNextRun(0x40);
         var palette = run.FindRelatedPalettes(Model).Single();

         Assert.Equal(0x0, palette.Start);
      }

      [Fact]
      public void CanReplaceTilemapWithSprite() {
         ViewPort.Edit("@00 10 20 00 00 @00 ^data`lzm4x4x4`");
         ViewPort.Refresh();

         ViewPort.Tools.SpriteTool.SpriteIsTilemap = false;

         Assert.IsType<LzSpriteRun>(Model.GetNextRun(0));
      }

      [Fact]
      public void UncompressedImagesFormatIncludesPaletteHint() {
         ViewPort.Edit("^data`ucs4x4x4|pal`");
         Assert.Equal("`ucs4x4x4|pal`", Model.GetNextRun(0).FormatString);
      }

      [Fact]
      public void EditPaletteUpdatesMatchingSprites() {
         ViewPort.Edit("01 @00 ^sprite`ucs4x1x1|pal`");
         ViewPort.Edit("@20 ^pal`ucp4`");
         ViewPort.Refresh();

         ViewPort.Tools.SpriteTool.Colors.HandleMove(0, 1);
         ViewPort.Tools.SpriteTool.Colors.CompleteCurrentInteraction();

         Assert.Equal(0x10, Model[0]); // 01 changes to 10
         Assert.Equal(0x11, Model[1]); // 00 changes to 11
      }

      [Fact]
      public void CanCreateGradientForSelectedColors() {
         ViewPort.Edit("^palette`ucp4` 0:0:0 0:0:0 30:30:30 ");

         ViewPort.Tools.SpriteTool.Colors.SelectionStart = 0;
         ViewPort.Tools.SpriteTool.Colors.SelectionEnd = 2;
         ViewPort.Tools.SpriteTool.Colors.CreateGradient.Execute();

         var color = (UncompressedPaletteColor)ViewPort[2, 0].Format;
         Assert.Equal("15:15:15", color.ToString());
      }

      [Fact]
      public void AutoPaletteFailsIfHighBitsSet() {
         ViewPort.Edit("FF FF @20 ^somedata @00 ^pal ");

         var makePalette = ViewPort.GetContextMenuItems(new Point(0, 0)).Single(item => item.Text == "View as 16-color palette").Command;
         makePalette.Execute();

         Assert.NotEmpty(Errors);
      }

      [Fact]
      public void CanFindPaletteInTableUsingField() {
         // make two palettes in a table with keys 0012 and 0055
         ViewPort.Edit("^pals[id:|h pointer<`ucp4`>]4 0012 @{ @} 0055 @{ 3:3:3 @} ");

         // make a sprite that uses the 2nd palette
         ViewPort.Edit("@20 ^sprite`ucs4x1x1|pals:id=0055` 11 ");

         var spriteRun = (ISpriteRun)Model.GetNextRun(0x20);
         var paletteRun = spriteRun.FindRelatedPalettes(Model).Single();

         Assert.Equal("3:3:3", paletteRun.CreateDataFormat(Model, paletteRun.Start).ToString());
         Assert.NotEqual(0, ViewPort.Tools.SpriteTool.PixelData[0]); // sprite is rendered with correct palette
      }

      [Fact]
      public void CanCreateOverworldSpriteList() {
         // setup data elements
         ViewPort.Edit("@00 00 00 11 00 00 00 40 00 08 00 08 00 00 00 00 00 <null> <null> <null> <060> <null>");     // parent (start, id, backup_id, length, width, height)
         ViewPort.Edit("@30 <040> 11 00 00 00 @30 ^overworld.palettes[p<`ucp4`> id:|h unused:]1 ");   // palette table. Palette goes from 40 to 60.
         ViewPort.Edit("@60 <080> 20 00 00 00 <0A0> 20 00 00 00 ");                    // sprite list. Sprites go 80-A0 and A0-C0 (2 sprites)

         // add format to parent
         // starterbytes:|h paletteid:|h secondid:|h length: width: height: slot.|h overwrite. unused: distribution<> sizedraw<> animation<> sprites<> ramstore<>
         ViewPort.Edit("@0000 ^parent[starterbytes: paletteid:|h secondid: length: width: height: other:: a<> b<> c<> sprites<`osl`> e<>]1 "); // sprites<`osl`> eventually

         // verify that the correct number of sprites were found
         var table = Model.GetNextRun(0x60) as ITableRun;
         Assert.Equal(2, table.ElementCount);

         // verify that the sprite runs were added as expected.
         var sprite1 = Model.GetNextRun(0x80) as ISpriteRun;
         Assert.Equal(1, sprite1.SpriteFormat.TileWidth);
         Assert.Equal(1, sprite1.SpriteFormat.TileHeight);
         Assert.Equal("overworld.palettes:id=0011", sprite1.SpriteFormat.PaletteHint);

         Assert.IsType<SpriteRun>(Model.GetNextRun(0xA0));
      }

      private void CreateLzRun(int start, params byte[] data) {
         for (int i = 0; i < data.Length; i++) Model[start + i] = data[i];
         var run = new LZRun(Model, start);
         Model.ObserveRunWritten(ViewPort.CurrentChange, run);
      }
   }
}
