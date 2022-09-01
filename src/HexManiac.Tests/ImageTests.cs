using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Factory;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using HavenSoft.HexManiac.Core.ViewModels.Visitors;
using System;
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
      public void EditUncompressedCellMovesCursorByOne() {
         var compressed = LZRun.Compress(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, 0, 16).ToArray();
         Array.Copy(compressed, Model.RawData, compressed.Length);
         Model.ObserveRunWritten(new ModelDelta(), new LZRun(Model, 0));
         ViewPort.Refresh();

         ViewPort.SelectionStart = new Point(5, 0);
         ViewPort.Edit("00 ");

         Assert.Equal(new Point(6, 0), ViewPort.SelectionStart);
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
         ViewPort.Edit("@07 8:1 "); // after this run we expect 2 more compressed runs but have only 4 more bytes
         // edit should fail

         Assert.Single(Errors);

         // make the actual edit
         ViewPort.Edit("@07 6:1 "); // after this run we expect 2 more compressed runs, so they should both be length 3

         // check that compressed segment is 6:1
         Assert.Equal(0x30, Model[6]);
         Assert.Equal(0x00, Model[7]);

         // check that compressed segment is 3:1
         Assert.Equal(0x00, Model[8]);
         Assert.Equal(0x00, Model[9]);

         // check that compressed segment is 3:1
         Assert.Equal(0x00, Model[10]);
         Assert.Equal(0x00, Model[11]);

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

         Assert.Equal(8, LZRun.Decompress(Model, 0).Length);
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

         Assert.Equal(4 + 1 + 3 + 3, Model.GetNextRun(0).Length); // header, group header, 3 uncompressed bytes, auto-fill with 3 more uncompressed bytes to match the existing group header
         Assert.Equal(0x00, Model[4]); // the 'none compressed' write was respected

         ViewPort.Edit("@04 10 "); // address 8-9 are now read as a compressed pair (0000=3:1) which gets us exactly to our expected length.

         Assert.Equal(4 + 1 + 3 + 2, Model.GetNextRun(0).Length); // header, group header, 3 uncompressed bytes, then 2 compressed bytes
         Assert.Equal(0x10, Model[4]);
      }

      [Fact]
      public void CanEditLzDataViaStreamTool() {
         SetFullModel(0xFF);
         CreateLzRun(0,
            0x10, 8, 0, 0, // header (uncompressed length = 8)
            0b01000000,     // group
            0x30,           // uncompressed 30
            0x40, 0x00);    // compressed   7:1

         ViewPort.Edit("@04 ");
         Assert.Equal("30 30 30 30 30 30 30 30", ViewPort.Tools.StringTool.Content);

         ViewPort.Tools.StringTool.Content = "30 20 30 30 30 30 30 30"; // should result in 30 20 30 30 (4:2)

         var run = Model.GetNextRun(0);
         Assert.Equal(0b00001000, Model[4]);
         Assert.Equal(0x30, Model[5]);
         Assert.Equal(0x20, Model[6]);
         Assert.Equal(0x30, Model[7]);
         Assert.Equal(0x30, Model[8]);
         Assert.Equal(0x10, Model[9]);
         Assert.Equal(0x01, Model[10]);
         Assert.Equal(11, run.Length);
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
         Model.ObserveRunWritten(ViewPort.CurrentChange, new SpriteRun(Model, 0x10, new SpriteFormat(4, 2, 2, null)));
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
         Assert.Contains("^run`lzs4x1x1` lz 32 00 ", fileSystem.CopyText);
      }

      [Fact]
      public void CanCopyCompressedPalette() {
         ViewPort.Edit("10 20 @00 ^run`lzp4`");
         var fileSystem = new StubFileSystem();
         ViewPort.ExpandSelection(0, 0);
         ViewPort.Copy.Execute(fileSystem);
         Assert.Contains("^run`lzp4` lz 32 00 ", fileSystem.CopyText);
      }

      [Fact]
      public void CanCopyUncompressedSprite() {
         ViewPort.Edit("^run`ucs4x1x1`");
         var fileSystem = new StubFileSystem();
         ViewPort.ExpandSelection(0, 0);
         ViewPort.Copy.Execute(fileSystem);
         var emptyBlock = " ".Join(32.Range().Select(i => "00"));
         Assert.StartsWith($"^run`ucs4x1x1` {emptyBlock}", fileSystem.CopyText);
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
         var item = items.Single(element => element.Text == "Display As...");
         item = ((ContextItemGroup)item).Single(element => element.Text == "Color Palette");
         item.Command.Execute();

         var anchor = Model.GetAnchorFromAddress(-1, 0);
         Assert.Equal("bob", anchor);
      }

      [Fact]
      public void LoosePaletteCanFindSpriteThatUsesIt() {
         // setup an lz run header at 0x80
         Model[0x80] = 0x10;
         Model[0x81] = 32;

         ViewPort.Edit("@00 ^pal`ucp4`");
         ViewPort.Edit("@40 ^sprite`ucs4x1x1|pal`");
         ViewPort.Edit("@80 ^tiles`lzt4|pal`");

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
         ViewPort.Edit("@80 <pal> @20 ^pal`ucp4`");
         ViewPort.Refresh();

         ViewPort.Tools.SpriteTool.Colors.HandleMove(0, 1);
         ViewPort.Tools.SpriteTool.Colors.CompleteCurrentInteraction();

         Assert.Equal(0x10, Model[0]); // 01 changes to 10
         Assert.Equal(0x11, Model[1]); // 00 changes to 11
      }

      [Fact]
      public void CanCreateGradientForSelectedColors() {
         ViewPort.Edit("@80 <palette> @00 ^palette`ucp4` 0:0:0 0:0:0 30:30:30 ");

         ViewPort.Tools.SpriteTool.Colors.SelectionStart = 0;
         ViewPort.Tools.SpriteTool.Colors.SelectionEnd = 2;
         ViewPort.Tools.SpriteTool.Colors.CreateGradient.Execute();

         var color = (UncompressedPaletteColor)ViewPort[2, 0].Format;
         Assert.Equal("15:15:15", color.ToString());
      }

      [Fact]
      public void AutoPaletteFailsIfHighBitsSet() {
         ViewPort.Edit("FF FF @20 ^somedata @00 ^pal ");

         var items = ViewPort.GetContextMenuItems(new Point(0, 0));
         var item = items.Single(element => element.Text == "Display As...");
         item = ((ContextItemGroup)item).Single(element => element.Text == "Color Palette");
         var makePalette = item.Command;
         makePalette.Execute();

         Assert.NotEmpty(Errors);
      }

      [Fact]
      public void AutoPaletteNameHasNamespace() {
         ViewPort.Edit("@20 ^somedata @30 <000> @00 ");

         var items = ViewPort.GetContextMenuItems(new Point(0, 0));
         var item = items.Single(element => element.Text == "Display As...");
         item = ((ContextItemGroup)item).Single(element => element.Text == "Color Palette");
         var makePalette = item.Command;
         makePalette.Execute();

         var anchor = Model.GetAnchorFromAddress(-1, 0);
         Assert.Equal($"{HardcodeTablesModel.DefaultPaletteNamespace}.000000", anchor);
      }

      [Fact]
      public void AutoPaletteNameFromImageToolHasNamespace() {
         ViewPort.Edit("@20 ^somedata @30 <000> @00 ");

         ViewPort.Tools.SpriteTool.PaletteAddress = 0;
         ViewPort.Tools.SpriteTool.GotoPaletteAddress.Execute();

         var anchor = Model.GetAnchorFromAddress(-1, 0);
         Assert.Equal($"{HardcodeTablesModel.DefaultPaletteNamespace}.000000", anchor);
      }

      [Fact]
      public void AutoSpriteNameFromImageToolHasNamespace() {
         ViewPort.Edit("@20 ^somedata @30 <000> @00 ");

         ViewPort.Tools.SpriteTool.SpriteAddress = 0;
         ViewPort.Tools.SpriteTool.GotoSpriteAddress.Execute();

         var anchor = Model.GetAnchorFromAddress(-1, 0);
         Assert.Equal($"{HardcodeTablesModel.DefaultSpriteNamespace}.000000", anchor);
      }

      [Fact]
      public void CanFindPaletteInTableUsingField() {
         // make two palettes in a table with keys 0012 and 0055
         ViewPort.Edit("^pals[id:|h pointer<`ucp4`>]4 0012 @{ @} 0055 @{ 0:0:0 3:3:3 @} "); // set the 2nd color of the 2nd palette

         // make a sprite that uses the 2nd palette
         ViewPort.Edit("@20 ^sprite`ucs4x1x1|pals:id=0055` 11 "); // set this sprite to use the 2nd color for its first pixel

         var spriteRun = (ISpriteRun)Model.GetNextRun(0x20);
         var paletteRun = spriteRun.FindRelatedPalettes(Model).Single();

         Assert.Equal("3:3:3", paletteRun.CreateDataFormat(Model, paletteRun.Start + 2).ToString());
         Assert.NotEqual(0, ViewPort.Tools.SpriteTool.PixelData[0]); // sprite is rendered with correct palette
      }

      [Fact]
      public void CanCreateOverworldSpriteList() {
         // setup data elements
         ViewPort.Edit("@00 00 00 11 00 00 00 40 00 08 00 08 00 00 00 00 00 <null> <null> <null> <060> <null>");     // parent (start, id, backup_id, length, width, height)
         ViewPort.Edit($"@30 <040> 11 00 00 00 @30 ^{HardcodeTablesModel.OverworldPalettes}[p<`ucp4`> id:|h unused:]1 ");   // palette table. Palette goes from 40 to 60.
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
         Assert.Equal($"{HardcodeTablesModel.OverworldPalettes}:id=0011", sprite1.SpriteFormat.PaletteHint);

         Assert.IsType<SpriteRun>(Model.GetNextRun(0xA0));
      }

      [Fact]
      public void CanAddPageToCompressedSpritesAndPalettes() {
         // Arrange: place some sprites and palettes, linked to a name table
         // 000-010  : pokenames table
         // 010-020  : sprite/palette tables (each is a single pointer)
         // 100-130  : front sprites
         // 130-160  : back sprites
         // 160-190  : normal palettes
         // 190-1B0  : shiny palettes
         ViewPort.Edit($"FF @00 ^{HardcodeTablesModel.PokemonNameTable}[name\"\"15]1 Castform\"");
         ViewPort.Edit($"@100 10 20 @130 10 20 @160 10 20 @190 10 20 @10 ");
         ViewPort.Edit($"^front.sprites[sprite<`lzs4x1x1`>]{HardcodeTablesModel.PokemonNameTable} <100>");
         ViewPort.Edit($"^back.sprites[sprite<`lzs4x1x1`>]{HardcodeTablesModel.PokemonNameTable} <130>");
         ViewPort.Edit($"^normal.palette[pal<`lzp4`>]{HardcodeTablesModel.PokemonNameTable} <160>");
         ViewPort.Edit($"^shiny.palette[pal<`lzp4`>]{HardcodeTablesModel.PokemonNameTable} <190>");

         // Act: expand each by a single page
         ViewPort.Goto.Execute(HardcodeTablesModel.PokemonNameTable);
         var sevm = (SpriteElementViewModel)ViewPort.Tools.TableTool.Children.First(child => child is SpriteElementViewModel);
         sevm.AddPage.Execute();

         // Assert: each has 2 pages now
         foreach (var child in ViewPort.Tools.TableTool.Children) {
            if (child is IPagedViewModel pvm) {
               Assert.Equal(2, pvm.Pages);
               Assert.Equal(1, pvm.CurrentPage);
            }
         }
      }

      [Fact]
      public void CanDeletePageFromCompressedSpritesAndPalettes() {
         // Arrange: place some sprites and palettes, linked to a name table
         // 000-010  : pokenames table
         // 010-020  : sprite/palette tables (each is a single pointer)
         // 040-0A0  : front sprites
         // 0A0-100  : back sprites
         // 100-160  : normal palettes
         // 160-1C0  : shiny palettes
         ViewPort.Edit($"FF @00 ^{HardcodeTablesModel.PokemonNameTable}[name\"\"15]1 Castform\"");
         ViewPort.Edit($"@040 10 40 @0A0 10 40 @100 10 40 @160 10 40 @10 ");
         ViewPort.Edit($"^front.sprites[sprite<`lzs4x1x1`>]{HardcodeTablesModel.PokemonNameTable} <040>");
         ViewPort.Edit($"^back.sprites[sprite<`lzs4x1x1`>]{HardcodeTablesModel.PokemonNameTable} <0A0>");
         ViewPort.Edit($"^normal.palette[pal<`lzp4`>]{HardcodeTablesModel.PokemonNameTable} <100>");
         ViewPort.Edit($"^shiny.palette[pal<`lzp4`>]{HardcodeTablesModel.PokemonNameTable} <160>");

         // Act: contract each by a single page
         ViewPort.Goto.Execute(HardcodeTablesModel.PokemonNameTable);
         var sevm = (SpriteElementViewModel)ViewPort.Tools.TableTool.Children.First(child => child is SpriteElementViewModel);
         sevm.DeletePage.Execute();

         // Assert: each has 1 page now
         foreach (var child in ViewPort.Tools.TableTool.Children) {
            if (child is IPagedViewModel pvm) {
               Assert.Equal(1, pvm.Pages);
            }
         }
      }

      [Fact]
      public void ManuallyTypingPaletteChannelsWorks() {
         ViewPort.Edit("^pal`ucp4` 1:2:3 ");

         var format = ViewPort[1, 0].Format;
         var content = format.ToString();
         Assert.Equal("1:2:3", content);
      }

      [Fact]
      public void CanCreateSpriteFromRightClick() {
         ViewPort.Edit("^anchor 10 20 00 00");

         ViewPort.SelectionStart = new Point(1, 0);
         var group = (ContextItemGroup)ViewPort.GetContextMenuItems(new Point(1, 0)).Single(item => item.Text == "Display As...");
         var contextItem = group.Single(item => item.Text == "Sprite");
         contextItem.Command.Execute();

         Assert.Equal(ViewPort.Tools.SpriteTool, ViewPort.Tools.SelectedTool);
         Assert.IsType<LzSpriteRun>(Model.GetNextRun(0));
      }

      [Fact]
      public void CanCreatePaletteFromRightClick() {
         ViewPort.Edit("^anchor 10 20 00 00");

         ViewPort.SelectionStart = new Point(1, 0);
         var group = (ContextItemGroup)ViewPort.GetContextMenuItems(new Point(1, 0)).Single(item => item.Text == "Display As...");
         var contextItem = group.Single(item => item.Text == "Color Palette");
         contextItem.Command.Execute();

         Assert.Equal(ViewPort.Tools.SpriteTool, ViewPort.Tools.SelectedTool);
         Assert.IsType<LzPaletteRun>(Model.GetNextRun(0));
      }

      // TODO expand ! commands with more. Example: !00(32) writes 32 bytes of 00.
      // TODO add autocomplete options for ! commands
      [Fact]
      public void Freespace_InsertNewEmptyCompressedData_DataChanges() {
         SetFullModel(0xFF);
         ViewPort.Refresh();

         ViewPort.Edit("@!lz(32) "); // make sure that the next bytes represent 32 lz compressed bytes.

         var decompressed = LZRun.Decompress(Data, 0);
         Assert.Equal(32, decompressed.Length);
      }

      [Fact]
      public void CompressedData_InsertNewEmptyCompressedData_DataDoesNotChange() {
         var rnd = new Random(1234);
         SetFullModel(0xFF);
         var decompressed = new byte[32];
         rnd.NextBytes(decompressed);
         var compressed = LZRun.Compress(decompressed, 0, decompressed.Length);
         for (int i = 0; i < compressed.Count; i++) Data[i] = compressed[i];
         Model.ResetChanges();
         ViewPort.Refresh();

         ViewPort.Edit("@!lz(32) ");

         Assert.Equal(new Point(0, 0), ViewPort.SelectionStart);
         Assert.All(compressed.Count.Range(), i => Assert.False(Model.HasChanged(i)));
         Assert.Empty(Errors);
      }

      [Fact]
      public void NonCompressedData_InsertNewEmptyCompressedData_Error() {
         ViewPort.Edit("@!lz(32) ");

         Assert.Single(Errors);
      }

      [Fact]
      public void Freespace_GotoCommandFollowedByExecuteCommand_BothCommandsPerformed() {
         SetFullModel(0xFF);
         ViewPort.Refresh();

         ViewPort.Edit("@100!lz(32) ");

         var decompressed = LZRun.Decompress(Data, 0x100);
         Assert.Equal(32, decompressed.Length);
      }

      [Fact]
      public void CompressedPalette_Copy_CopyMetacommandToCreateCompressedRun() {
         SetFullModel(0xFF);
         var fs = new StubFileSystem();
         ViewPort.Edit("@!lz(32) ^pal`lzp4`");

         ViewPort.SelectionEnd = ViewPort.ConvertAddressToViewPoint(ViewPort.Model.GetNextRun(0).Length - 1);
         ViewPort.Copy.Execute(fs);

         Assert.StartsWith("@!lz(32) ^pal`lzp4` lz ", fs.CopyText);
      }

      [Fact]
      public void MultiPageSprite_InImageTool_CanExportMany() {
         WriteCompressedData(0, 0x20);      // palette
         WriteCompressedData(0x40, 0x40);   // sprite

         ViewPort.Edit("@00 ^pal`lzp4` @40 ^sprite`lzs4x1x1|pal`");
         ViewPort.Tools.SelectedIndex = ViewPort.Tools.IndexOf(ViewPort.Tools.SpriteTool);

         Assert.True(ViewPort.Tools.SpriteTool.CanExportMany);
      }

      [Fact]
      public void MultiPageSprite_ExportHorizontal_ReturnsHorizontal() {
         WriteCompressedData(0, 0x20);      // palette
         WriteCompressedData(0x40, 0x40);   // sprite
         ViewPort.Edit("@00 ^pal`lzp4` @40 ^sprite`lzs4x1x1|pal`");
         ViewPort.Tools.SelectedIndex = ViewPort.Tools.IndexOf(ViewPort.Tools.SpriteTool);

         short[] imageData = null;
         int width = 0;
         var fs = new StubFileSystem {
            ShowOptions = (title, text, details, options) => 0,
            SaveImage = (img, wdth, n) => (imageData, width) = (img, wdth),
         };

         ViewPort.Tools.SpriteTool.ExportMany.Execute(fs);

         Assert.Equal(8 * 2, width);
         Assert.Equal(8 * 8 * 2, imageData.Length);
      }

      [Fact]
      public void MultiPageSprite_ExportVertical_ReturnsVertical() {
         WriteCompressedData(0, 0x20);      // palette
         WriteCompressedData(0x40, 0x40);   // sprite
         ViewPort.Edit("@00 ^pal`lzp4` @40 ^sprite`lzs4x1x1|pal`");
         ViewPort.Tools.SelectedIndex = ViewPort.Tools.IndexOf(ViewPort.Tools.SpriteTool);

         short[] imageData = null;
         int width = 0;
         var fs = new StubFileSystem {
            ShowOptions = (title, text, details, options) => 1,
            SaveImage = (img, wdth, n) => (imageData, width) = (img, wdth),
         };

         ViewPort.Tools.SpriteTool.ExportMany.Execute(fs);

         Assert.Equal(8, width);
         Assert.Equal(8 * 8 * 2, imageData.Length);
      }

      [Fact]
      public void OneByteTilemap_Write_Works() {
         WriteCompressedData(0, 32);
         WriteCompressedData(0x20, 32);
         WriteCompressedData(0x40, 1);
         ViewPort.Edit("@00 ^pal`lzp4` @20 ^tileset`lzt4|pal` @40 ^tilemap`lzm4x1x1|tileset`");

         var tilemap = (ITilemapRun)Model.GetNextRun(Model.GetAddressFromAnchor(ViewPort.CurrentChange, -1, "tilemap"));
         var pixels = tilemap.GetPixels(Model, 0);
         pixels[2, 2] = 1;
         tilemap.SetPixels(Model, ViewPort.CurrentChange, 0, pixels);

         var tileset = (LzTilesetRun)Model.GetNextRun(Model.GetAddressFromAnchor(ViewPort.CurrentChange, -1, "tileset"));
         pixels = tileset.GetPixels(Model, 0);
         pixels = LzTilemapRun.Tilize(pixels, 4)[1, 0].pixels; // extract only tile 1, because tile 0 is the 'always blank' tile
         Assert.Equal(1, pixels[2, 2]);
      }

      [Fact]
      public void TwoUncompressedPalettes_Copy_32ColorsCopied() {
         var fs = new StubFileSystem();
         ViewPort.Edit("@00 ^a`ucp4` @20 ^b`ucp4` @00 ");
         ViewPort.SelectionStart = ViewPort.ConvertAddressToViewPoint(0);
         ViewPort.SelectionEnd = ViewPort.ConvertAddressToViewPoint(0x40 - 1);

         ViewPort.Copy.Execute(fs);

         var colorCount = fs.CopyText.value.Split("0:0:0").Length - 1;
         Assert.Equal(32, colorCount);
      }

      [Fact]
      public void UncompressedPalette_DeleteColor_BecomesBlack() {
         SetFullModel(0b01010101);
         Model.ObserveRunWritten(new NoDataChangeDeltaModel(), new PaletteRun(0, new PaletteFormat(4, 1)));
         ViewPort.Refresh();

         ViewPort.SelectionStart = ViewPort.ConvertAddressToViewPoint(2);
         ViewPort.SelectionEnd = ViewPort.ConvertAddressToViewPoint(3);
         ViewPort.Clear.Execute();

         Assert.Equal(0, Model.ReadMultiByteValue(2, 2));
         Assert.IsType<PaletteRun>(Model.GetNextRun(0));
      }

      [Fact]
      public void PointerToCompressedData_EditAnchorFromPointer_AnchorEdited() {
         Token.ChangeData(Model, 0x100, LZRun.Compress(new byte[0x20 * 4]));
         AddPointer(0, 0x100);
         ViewPort.Goto.Execute(0x100);
         ViewPort.Shortcuts.DisplayAsSprite.Execute();

         ViewPort.Goto.Execute(0);
         ViewPort.AnchorText = "^graphics.test`lzs4x2x2`";

         ViewPort.Goto.Execute(0x100);
         Assert.Equal("^graphics.test`lzs4x2x2`", ViewPort.AnchorText);
         Assert.Empty(Errors);
      }

      [Fact]
      public void TwoImages_Copy_ClipBoardContainsSpriteData() {
         var filesystem = new StubFileSystem();
         ViewPort.Edit("@00 ^sprite1`ucs4x1x1` @20 ^sprite2`ucs4x1x1`");
         var emptyBlock = " ".Join(32.Range().Select(i => "00"));

         ViewPort.SelectionStart = ViewPort.ConvertAddressToViewPoint(0x00);
         ViewPort.SelectionEnd = ViewPort.ConvertAddressToViewPoint(0x3F);
         ViewPort.Copy.Execute(filesystem);

         var copiedData = filesystem.CopyText.value;
         var expectedData = $"^sprite1`ucs4x1x1` {emptyBlock} ^sprite2`ucs4x1x1` {emptyBlock}";
         Assert.Equal(expectedData, copiedData);
      }

      [Fact]
      public void TilemapFormat_CreateNewTilemap_DataIsLargeEnoughForManyTiles() {
         var (tileWidth, tileHeight, bytesPerTile) = (32, 24, 2);
         var format = new TilemapFormat(4, tileWidth, tileHeight, string.Empty);
         var strategy = new LzTilemapRunContentStrategy(format);

         var newRun = (LzTilemapRun)strategy.WriteNewRun(Model, Token, default, 0, default, default);

         Assert.Equal(bytesPerTile, newRun.BytesPerTile);
         Assert.Equal(tileWidth * tileHeight * bytesPerTile, newRun.DecompressedLength);
      }

      const string Ruby_240C3C = "10 C0 07 00 33 00 00 F0 01 90 01 FF FF F0 01 90 01 00 88 88 88 88 28 27 22 22 80 10 03 78 77 77 77 C8 AA AA 48 9A 00 03 AA D8 10 03 DD DD DD 98 10 1F 22 22 30 01 00 1E 77 99 99 28 99 7C 10 03 9A 00 03 DD CC CC 7F 7C D0 1F 10 01 10 13 20 08 00 53 F0 1F 70 1F 00 22 22 72 77 66 66 66 66 7F E6 00 67 F0 03 30 03 10 1F 00 06 F0 01 40 01 E2 F0 1F F0 1F 00 A7 6C DD CD 00 03 DD AF 10 03 CD 00 03 DD 60 03 F0 9F 90 9F 50 BF 21 72 82 10 03 77 77 87 87 10 03 C0 00 12 20 07 32 33 83 87 11 B1 00 BB BB 11 AB AA AA B1 4A 10 44 44 B1 01 33 AB 44 44 44 C2 50 03 10 0F BB BB BB 2B 00 06 BA BF 00 0E B4 50 07 50 03 10 0F F1 B1 F0 01 F0 01 C2 70 01 11 B3 E8 BB BB 9B 00 03 AB 8B 20 03 EE EE DE 00 03 2E 20 07 00 0F 81 F1 B3 CC 7C 2E DE D2 72 01 0B 41 7C 01 EA 7C E8 E2 E2 E2 11 E7 D4 21 EF 80 03 28 00 B8 28 00 FF E2 D2 ED 70 2B 00 16 10 03 7D 10 03 00 1E 7B 00 1E 79 72 10 5F D2 33 90 53 F0 7F CC 7C 90 53 A1 91 EB 76 00 2B E6 EE EE EE 10 07 7D D6 00 07 10 0B 91 CB 10 4B 00 12 EE 40 07 77 7E 10 03 F0 1F 30 1F E7 00 07 10 03 91 EB AB 00 16 67 00 12 6E 40 07 6D 40 0B 21 EF FA 51 FF D0 0B 51 EF 11 EB B0 0B 77 10 0B 88 FF D1 EF D0 0F D1 EF F1 FF F0 01 F0 01 F0 01 50 01 E6 61 F7 00 03 F3 BF DD DD 91 B3 F1 8B CC 7F 7C 11 E3 10 03 10 3B 51 F3 90 13 11 E3 51 E7 FF 51 F3 90 13 F2 33 B0 3F F2 33 B0 3F 51 E7 50 03 97 51 FB 76 EE 21 5E 88 01 E2 01 E7 10 03 F7 31 EF 51 E3 20 1C 00 01 E7 01 E4 20 03 41 EF F0 D0 1F 51 E7 50 03 51 FB 88 78 EE 67 8E 00 1E 78 11 11 70 01 11 F7 D0 0F 33 1A 33 33 83 50 03 00 0E 82 00 16 81 FF 50 03 10 0F F1 FF F1 FF F1 FF F1 FF F0 01 F0 01 FF F0 01 01 FF F3 BF F3 BF F3 BF 61 8B 00 D7 60 01 FE F0 FF F0 0F D0 0F F0 FF 00 07 05 0B 15 13 22 1F B2 BB BB F0 FF 70 0B 15 1B F0 7F E1 7F F0 F0 1F F0 1F F0 1F 70 1F 12 33 33 23 73 11 00 06 F0 BF F0 0F 22 38 00 2C 90 03 FC D1 EF F1 FF F0 FF F1 FF F0 FF F1 FF";

      [Fact]
      public void TilesetWithLengthError_AddTilesetFormatAllowingLengthErrors_CanCreateMetadata() {
         ViewPort.Edit(Ruby_240C3C);

         ViewPort.Edit("@00 ^tileset`lzt4!` ");

         Assert.Empty(Errors);
         Assert.IsType<LzTilesetRun>(Model.GetNextRun(0));
      }

      [Fact]
      public void TilesetWithLengethError_ErrorMessage_MakesSense() {
         ViewPort.Edit(Ruby_240C3C);

         ViewPort.Edit("@00 ^tileset`lzt4` "); // this should produce an error

         Assert.Single(Errors);
         Assert.Equal("Decompressed more bytes than expected. Add a ! to override this.", Errors[0]);
      }

      private void WriteCompressedData(int start, int length) {
         var compressedData = LZRun.Compress(new byte[length], 0, length);
         for (int i = 0; i < compressedData.Count; i++) Model[start + i] = compressedData[i];
      }

      private void CreateLzRun(int start, params byte[] data) {
         for (int i = 0; i < data.Length; i++) Model[start + i] = data[i];
         var run = new LZRun(Model, start);
         Model.ObserveRunWritten(ViewPort.CurrentChange, run);
      }
   }
}
