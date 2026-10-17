using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Map;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HexManiac.Core.Models.Runs.Sprites;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class TilesetAnimationTests : BaseViewModelTestClass {
      private const int TilesetStart = 0x20;

      public TilesetAnimationTests() : base(0x1000) {
         SetFullModel(0xFF);
         // a fake tileset header: isCompressed, isSecondary, 2 flags, tiles, palettes, blocks, attributes, animation (null)
         Model[TilesetStart] = 1;
         Model[TilesetStart + 1] = 0;
         Model.WritePointer(Token, TilesetStart + 4, 0x800);
         Model.WritePointer(Token, TilesetStart + 8, 0x900);
         Model.WritePointer(Token, TilesetStart + 12, 0xA00);
         Model.WritePointer(Token, TilesetStart + 16, 0xB00);
         Model.WriteValue(Token, TilesetStart + 20, 0);
         foreach (var (name, value) in new[] {
            ("buffer", 0x02037000), ("buffersize", 0x03001000), ("primary.counter", 0x03001002), ("primary.max", 0x03001004), ("primary.callback", 0x03001008),
            ("secondary.counter", 0x0300100C), ("secondary.max", 0x0300100E), ("secondary.callback", 0x03001010), ("primarytiles", 512),
         }) Model.SetUnmappedConstant(Token, TilesetAnimationConstants.Prefix + name, value);
         Model.FreeSpaceStart = 0x100;
      }

      private TilesetAnimations CreateAnimations() => new(Model, () => Token, ViewPort.Tools.CodeTool.Parser);

      [Fact]
      public void Constants_AreReadFromMetadata() {
         Assert.True(TilesetAnimationConstants.TryRead(Model, out var constants));
         Assert.Equal(0x02037000, constants.Buffer);
         Assert.Equal(512, constants.PrimaryTiles);
         Assert.True(TilesetAnimations.IsSupported(Model));
      }

      [Fact]
      public void EnsureTable_InstallsCodeAndPointsTilesetAtIt() {
         var animations = CreateAnimations();
         TilesetAnimationConstants.TryRead(Model, out var constants);

         var table = animations.EnsureTable(TilesetStart, false, constants, out var baseName);

         Assert.Equal(0, table.ElementCount);
         Assert.StartsWith(TilesetAnimations.AnchorPrefix, baseName);
         var callback = animations.ReadCallback(TilesetStart);
         Assert.Equal(1, callback & 1); // thumb bit
         Assert.Equal(baseName + ".init", Model.GetAnchorFromAddress(-1, callback));
         Assert.True(animations.TryGetTable(TilesetStart, out var again, out _));
         Assert.Equal(table.Start, again.Start);
         Assert.Equal(0xB5, Model[(callback & ~1) + 1]); // push {.., lr}
      }

      [Fact]
      public void AddEntry_CreatesFramesFromCurrentTiles() {
         var animations = CreateAnimations();
         TilesetAnimationConstants.TryRead(Model, out var constants);
         var table = animations.EnsureTable(TilesetStart, false, constants, out _);
         var tiles = Enumerable.Range(0, 32 * 16).Select(i => (byte)(i & 0x77)).ToArray();

         var entry = animations.AddEntry(table, 4, 2, 3, 4, false, constants, tiles);

         Assert.Equal(4, entry.FirstTile);
         Assert.Equal(2, entry.TileCount);
         Assert.Equal(3, entry.FrameCount);
         Assert.Equal(4, entry.Timer);
         for (int f = 0; f < 3; f++) {
            var frame = animations.ReadFrame(entry, f);
            Assert.Equal(tiles.Skip(4 * 32).Take(64), frame);
         }
         // the frames don't overlap each other or the frame table
         var frameAddresses = Enumerable.Range(0, 3).Select(f => Model.ReadPointer(entry.FramesAddress + 4 * f)).ToList();
         Assert.Equal(3, frameAddresses.Distinct().Count());
         Assert.All(frameAddresses, address => Assert.True(address >= entry.FramesAddress + 12 || address + 64 <= entry.FramesAddress));
      }

      [Fact]
      public void RemoveEntry_ShiftsLaterEntries() {
         var animations = CreateAnimations();
         TilesetAnimationConstants.TryRead(Model, out var constants);
         var table = animations.EnsureTable(TilesetStart, false, constants, out _);
         animations.AddEntry(table, 1, 1, 2, 1, false, constants, null);
         table = animations.TryGetTable(TilesetStart, out var t2, out _) ? t2 : table;
         animations.AddEntry(table, 7, 3, 2, 2, false, constants, null);
         animations.TryGetTable(TilesetStart, out table, out _);
         Assert.Equal(2, table.ElementCount);

         animations.RemoveEntry(table, 0);

         animations.TryGetTable(TilesetStart, out table, out _);
         var entries = animations.ReadEntries(table, false, constants);
         Assert.Single(entries);
         Assert.Equal(7, entries[0].FirstTile);
         Assert.Equal(3, entries[0].TileCount);
      }

      [Fact]
      public void SecondaryTileset_OffsetsTilesByPrimaryCount() {
         var animations = CreateAnimations();
         TilesetAnimationConstants.TryRead(Model, out var constants);
         Model[TilesetStart + 1] = 1;
         var table = animations.EnsureTable(TilesetStart, true, constants, out _);

         var entry = animations.AddEntry(table, 5, 1, 2, 0, true, constants, null);

         Assert.Equal(5, entry.FirstTile);
         Assert.Equal(512 + 5, Model.ReadMultiByteValue(entry.EntryAddress + 8, 4));
      }

      [Fact]
      public void AddDoor_GoesInFrontOfTheTerminator() {
         // a doors table with one real door and the all-zero terminator the game stops at
         var tableStart = 0x600;
         Model.WriteMultiByteValue(tableStart, 2, Token, 33);
         Model.WritePointer(Token, tableStart + 4, TilesetStart);
         Model[tableStart + 8] = 0; Model[tableStart + 9] = 1;
         Model.WritePointer(Token, tableStart + 12, 0xC00);
         Model.WritePointer(Token, tableStart + 16, 0xD00);
         for (int i = 20; i < 40; i++) Model[tableStart + i] = 0;
         ViewPort.Edit($"@{tableStart:X6} ^{DoorAnimations.TableName}[metatile: unused: tileset<> sound. size. unused: tiles<`ucs4x2x12`> palettes<>]2 ");
         var doors = new DoorAnimations(Model, () => Token);
         Assert.True(DoorAnimations.IsSupported(Model));
         Assert.Equal(1, doors.TerminatorIndex(doors.Table));

         var added = doors.AddDoor(TilesetStart, 34, 1, 1, 2);

         var table = doors.Table;
         Assert.Equal(3, table.ElementCount);
         Assert.Equal(1, added.Index);
         Assert.Equal(34, added.Metatile);
         Assert.Equal(2, doors.TerminatorIndex(table));
         Assert.Equal(Pointer.NULL, Model.ReadPointer(table.Start + table.ElementLength * 2 + 12));
         Assert.Equal(33, Model.ReadMultiByteValue(table.Start, 2));
         Assert.Equal(0xC00, Model.ReadPointer(table.Start + 12));
         Assert.IsType<SpriteRun>(Model.GetNextRun(added.TilesAddress));
         Assert.Equal(DoorAnimations.PaletteHint, ((ISpriteRun)Model.GetNextRun(added.TilesAddress)).SpriteFormat.PaletteHint);

         doors.RemoveDoor(1);

         table = doors.Table;
         Assert.Equal(2, table.ElementCount);
         Assert.Equal(1, doors.TerminatorIndex(table));
      }

      [Fact]
      public void BuiltInEntries_AreReadFromTheBuildTable() {
         // two rows of: tileset<> frames<> frameCount: firstTile: tileCount. timerShift. phase. padding. name""32
         const int table = 0x700, secondTileset = 0x40;
         for (int row = 0; row < 2; row++) {
            var start = table + row * 48;
            Model.WritePointer(Token, start, row == 0 ? TilesetStart : secondTileset);
            Model.WritePointer(Token, start + 4, 0x300 + row * 0x20);
            Model.WriteMultiByteValue(start + 8, 2, Token, 2);
            Model.WriteMultiByteValue(start + 10, 2, Token, row == 0 ? 432 : 512 + 96);
            Model[start + 12] = 2;
            Model[start + 13] = (byte)(row == 0 ? 3 : 4);
            Model[start + 14] = (byte)row;
            var name = row == 0 ? "Water" : "Steam";
            var text = Model.TextConverter.Convert(name, out var _);
            for (int i = 0; i < 32; i++) Model[start + 16 + i] = i < text.Count && text[i] != 0xFF ? text[i] : (byte)0xFF;
            Model.WritePointer(Token, 0x300 + row * 0x20, 0x400 + row * 0x80);
            Model.WritePointer(Token, 0x304 + row * 0x20, 0x440 + row * 0x80);
         }
         for (int i = 0; i < 64; i++) Model[0x440 + i] = (byte)(i + 1);
         ViewPort.Edit($"@{table:X6} ^{TilesetAnimations.BuiltInTableName}[tileset<> frames<> frameCount: firstTile: tileCount. timerShift. phase. padding. name\"\"32]2 ");
         var animations = CreateAnimations();
         TilesetAnimationConstants.TryRead(Model, out var constants);

         var primary = animations.ReadBuiltInEntries(TilesetStart, false, constants);
         var secondary = animations.ReadBuiltInEntries(secondTileset, true, constants);

         Assert.True(animations.HasBuiltInTable);
         var water = Assert.Single(primary);
         Assert.True(water.IsBuiltIn);
         Assert.Equal("Water", water.Name);
         Assert.Equal(432, water.FirstTile);
         Assert.Equal(2, water.TileCount);
         Assert.Equal(2, water.FrameCount);
         Assert.Equal(3, water.Timer);
         Assert.Equal(0, water.Phase);
         Assert.Equal(Enumerable.Range(0, 64).Select(i => (byte)(i + 1)), animations.ReadFrame(water, 1));
         var steam = Assert.Single(secondary);
         Assert.Equal("Steam", steam.Name);
         Assert.Equal(96, steam.FirstTile);      // secondary tilesets count from the end of the primary tiles
         Assert.Equal(1, steam.Phase);
      }

      #region Block numbers and door layout

      [Fact]
      public void ReadAllBlocks_NumbersTheSecondaryBlocksFromThePrimaryBlockCount() {
         // block 512 is the secondary blockset's block 0 (the doors list used to show block N+1 for every secondary door)
         const int secondaryStart = 0x60;
         Model[secondaryStart] = 0; Model[secondaryStart + 1] = 1;
         Model.WritePointer(Token, secondaryStart + 4, 0x800);
         Model.WritePointer(Token, secondaryStart + 8, 0x900);
         Model.WritePointer(Token, secondaryStart + 12, 0xC00);
         Model.WritePointer(Token, secondaryStart + 16, 0xD00);
         for (int i = 0; i < 4; i++) { Model[0xA00 + i * 16] = (byte)(0x10 + i); Model[0xC00 + i * 16] = (byte)(0xA0 + i); }
         // each blockset's attributes follow its blocks (16 blocks each here), which is how the length of the blocks is found
         foreach (var attributes in new[] { 0xB00, 0xD00 }) Model.ObserveRunWritten(Token, new SpriteRun(Model, attributes, new SpriteFormat(4, 1, 1, string.Empty)));
         var primary = new BlocksetModel(Model, TilesetStart);
         var secondary = new BlocksetModel(Model, secondaryStart);

         var all = BlockmapRun.ReadAllBlocks(primary, secondary);

         Assert.Equal(0x10, all[0][0]);
         Assert.Equal(0x12, all[2][0]);
         Assert.Equal(0xA0, all[primary.PrimaryBlocks][0]);
         Assert.Equal(0xA1, all[primary.PrimaryBlocks + 1][0]);
         Assert.Equal(0xA3, all[primary.PrimaryBlocks + 3][0]);
         Assert.Equal(primary.PrimaryBlocks + secondary.ReadBlocks(-1).Length, all.Length);
      }

      [Fact]
      public void DoorTileLayout_FollowsTheOrderTheGameDrawsTheTilesIn() {
         // one and two block tall doors are stored in rows, two tiles wide
         Assert.Equal((1, 0), DoorEntry.TilePositionFor(0, 1));
         Assert.Equal((0, 1), DoorEntry.TilePositionFor(1, 2));
         Assert.Equal((1, 3), DoorEntry.TilePositionFor(1, 7));
         // a 2x2 door is four blocks of four tiles: top left, bottom left, top right, bottom right
         Assert.Equal((0, 0), DoorEntry.TilePositionFor(2, 0));
         Assert.Equal((1, 1), DoorEntry.TilePositionFor(2, 3));
         Assert.Equal((0, 2), DoorEntry.TilePositionFor(2, 4));
         Assert.Equal((1, 3), DoorEntry.TilePositionFor(3, 7));
         Assert.Equal((2, 0), DoorEntry.TilePositionFor(3, 8));
         Assert.Equal((3, 1), DoorEntry.TilePositionFor(3, 11));
         Assert.Equal((2, 2), DoorEntry.TilePositionFor(2, 12));
         Assert.Equal((3, 3), DoorEntry.TilePositionFor(2, 15));
         // and the two directions agree
         for (int size = 0; size < 4; size++) {
            var count = DoorEntry.WidthTilesFor(size) * DoorEntry.HeightTilesFor(size);
            var seen = new System.Collections.Generic.HashSet<(int, int)>();
            for (int i = 0; i < count; i++) {
               var (x, y) = DoorEntry.TilePositionFor(size, i);
               Assert.True(x < DoorEntry.WidthTilesFor(size) && y < DoorEntry.HeightTilesFor(size));
               Assert.True(seen.Add((x, y)));
               Assert.Equal(i, DoorEntry.TileIndexFor(size, x, y));
            }
         }
      }

      [Fact]
      public void EnsureTilesFormat_ReshapesDoorTilesRegisteredWithTheWrongSize() {
         // a 2x2 door: 16 tiles per frame, 3 frames. It was registered 4 tiles wide (as if the tiles were stored row by row).
         var tableStart = 0x600;
         Model.WriteMultiByteValue(tableStart, 2, Token, 600);
         Model.WritePointer(Token, tableStart + 4, TilesetStart);
         Model[tableStart + 8] = 0; Model[tableStart + 9] = 2;
         Model.WritePointer(Token, tableStart + 12, 0x800);
         Model.WritePointer(Token, tableStart + 16, 0xF00);
         for (int i = 20; i < 40; i++) Model[tableStart + i] = 0;
         ViewPort.Edit($"@{tableStart:X6} ^{DoorAnimations.TableName}[metatile: unused: tileset<> sound. size. unused: tiles<`ucs4x4x12`> palettes<>]2 ");
         var doors = new DoorAnimations(Model, () => Token);
         var entry = doors.ReadEntries()[0];
         Assert.Equal(4, ((ISpriteRun)Model.GetNextRun(0x800)).SpriteFormat.TileWidth);

         doors.EnsureTilesFormat(entry);

         var format = ((ISpriteRun)Model.GetNextRun(0x800)).SpriteFormat;
         Assert.Equal(2, format.TileWidth);
         Assert.Equal(24, format.TileHeight);
         Assert.Equal(entry.TilesLength, format.ExpectedByteLength);
         Assert.Equal(DoorAnimations.PaletteHint, format.PaletteHint);
         Assert.Contains(tableStart + 12, Model.GetNextRun(0x800).PointerSources);
      }

      #endregion

      #region Init routines

      private void WriteThumb(int address, params int[] instructions) {
         for (int i = 0; i < instructions.Length; i++) Model.WriteMultiByteValue(address + i * 2, 2, Token, instructions[i]);
      }

      [Fact]
      public void FindCallbackInInit_StopsAtTheEndOfTheRoutine() {
         // init A installs no callback ("bx lr"); init B, right behind it, loads one from its own literal pool:
         //    A: 0x300  bx lr
         //    B: 0x304  push {lr} / ldr r0, [pc, #8] / pop {pc};  literal pool: 0x310 = 0x08000321
         WriteThumb(0x300, 0x4770, 0x0000);
         WriteThumb(0x304, 0xB500, 0x4802, 0xBD00);
         Model.WriteMultiByteValue(0x310, 4, Token, 0x08000321);

         Assert.Equal(Pointer.NULL, TilesetAnimations.FindCallbackInInit(Model, 0x301));
         Assert.Equal(0x321, TilesetAnimations.FindCallbackInInit(Model, 0x305));
      }

      [Fact]
      public void FindCallbackInInit_IgnoresWordsThatAreNotLoadedByTheRoutine() {
         // "push {lr} / pop {pc}" followed by some data that happens to look like a thumb pointer
         WriteThumb(0x300, 0xB500, 0xBD00);
         Model.WriteMultiByteValue(0x304, 4, Token, 0x08000321);
         Assert.Equal(Pointer.NULL, TilesetAnimations.FindCallbackInInit(Model, 0x301));
      }

      private int ReadChainedCallback(int callbackAddress) {
         // the generated callback starts: push {r4-r7, lr} / mov r6, r0 / ldr r7, =<the tileset's own callback or 0>
         var ldr = Model.ReadMultiByteValue(callbackAddress + 4, 2);
         Assert.Equal(0x4F00, ldr & 0xFF00);
         return Model.ReadMultiByteValue(((callbackAddress + 8) & ~3) + (ldr & 0xFF) * 4, 4);
      }

      [Fact]
      public void EnsureTable_DoesNotChainTheNeighbouringInitsCallback() {
         // the tileset's init (0x300) has no callback, but the init behind it (0x304) does: only a tileset that really has a callback may chain it
         WriteThumb(0x300, 0x4770, 0x0000);
         WriteThumb(0x304, 0xB500, 0x4802, 0xBD00);
         Model.WriteMultiByteValue(0x310, 4, Token, 0x08000321);
         Model.WritePointer(Token, TilesetStart + 20, 0x301);
         var animations = CreateAnimations();
         TilesetAnimationConstants.TryRead(Model, out var constants);

         animations.EnsureTable(TilesetStart, false, constants, out var baseName);

         var callback = Model.GetAddressFromAnchor(Token, -1, baseName + ".callback");
         Assert.Equal(0, ReadChainedCallback(callback & ~1));
      }

      [Fact]
      public void EnsureTable_ChainsTheTilesetsOwnCallback() {
         WriteThumb(0x304, 0xB500, 0x4802, 0xBD00);
         Model.WriteMultiByteValue(0x310, 4, Token, 0x08000321);
         Model.WritePointer(Token, TilesetStart + 20, 0x305);
         var animations = CreateAnimations();
         TilesetAnimationConstants.TryRead(Model, out var constants);

         animations.EnsureTable(TilesetStart, false, constants, out var baseName);

         var callback = Model.GetAddressFromAnchor(Token, -1, baseName + ".callback");
         Assert.Equal(0x08000320, ReadChainedCallback(callback & ~1));
      }

      #endregion

      #region Frame pictures (what the image editor draws on)

      private TilesetAnimationEntry AddPatternedAnimation(TilesetAnimations animations, int tileCount, int frameCount) {
         TilesetAnimationConstants.TryRead(Model, out var constants);
         var table = animations.EnsureTable(TilesetStart, false, constants, out _);
         var tiles = Enumerable.Range(0, 32 * 16).Select(i => (byte)(i * 7 + 3)).ToArray();
         return animations.AddEntry(table, 0, tileCount, frameCount, 4, false, constants, tiles);
      }

      private TilesetAnimationEntry CurrentFirstEntry(TilesetAnimations animations) {
         TilesetAnimationConstants.TryRead(Model, out var constants);
         return animations.TryGetTable(TilesetStart, out var table, out _) ? animations.ReadEntries(table, false, constants).FirstOrDefault() : null;
      }

      [Fact]
      public void FramePictures_ShowTheTilesInRowsOfTheWidthYouChoose() {
         var animations = CreateAnimations();
         var entry = AddPatternedAnimation(animations, 4, 3);
         var data = animations.ReadFrame(entry, 1);

         var square = animations.ReadFramePixels(entry, 1, 0); // the default is as square as possible
         var row = animations.ReadFramePixels(entry, 1, 4);

         Assert.Equal(16, square.GetLength(0));
         Assert.Equal(16, square.GetLength(1));
         Assert.Equal(32, row.GetLength(0));
         Assert.Equal(8, row.GetLength(1));
         Assert.Equal(data[32] & 0xF, square[8, 0]);  // tile 1 is next to tile 0...
         Assert.Equal(data[32] >> 4, square[9, 0]);
         Assert.Equal(data[64] & 0xF, square[0, 8]);  // ...and tile 2 is under it
         Assert.Equal(data[32] & 0xF, row[8, 0]);     // in a row, tile 1 is still next to tile 0
         Assert.Equal(data[96] & 0xF, row[24, 0]);    // and tile 3 comes after tile 2
         Assert.Equal((2, 2), TilesetAnimations.PictureShape(4, 0));
         Assert.Equal((6, 5), TilesetAnimations.PictureShape(30, 0));
         Assert.Equal((4, 3), TilesetAnimations.PictureShape(10, 4));
      }

      [Fact]
      public void FramePictures_WritingAPictureChangesOnlyThatFrameAndOnlyThePixelThatChanged() {
         var animations = CreateAnimations();
         var entry = AddPatternedAnimation(animations, 4, 3);
         var before = Enumerable.Range(0, 3).Select(f => animations.ReadFrame(entry, f)).ToArray();
         var picture = animations.ReadFramePixels(entry, 1, 0);
         picture[9, 10] = (picture[9, 10] + 5) & 0xF; // tile 3 (right, bottom), pixel (1, 2)

         animations.WriteFramePixels(Token, entry, 1, 0, picture);

         var after = Enumerable.Range(0, 3).Select(f => animations.ReadFrame(entry, f)).ToArray();
         Assert.Equal(before[0], after[0]);
         Assert.Equal(before[2], after[2]);
         var changed = Enumerable.Range(0, before[1].Length).Where(i => before[1][i] != after[1][i]).ToList();
         Assert.Single(changed);
         Assert.Equal(3 * 32 + 2 * 4 + 0, changed[0]);
         Assert.Equal(picture[9, 10], after[1][changed[0]] >> 4);
         Assert.Equal(picture[9, 10], animations.ReadFramePixels(entry, 1, 0)[9, 10]);
      }

      [Fact]
      public void FramePictures_AnyWidthStoresTheSameTiles() {
         var animations = CreateAnimations();
         var entry = AddPatternedAnimation(animations, 4, 3);
         var before = animations.ReadFrame(entry, 2);
         var row = animations.ReadFramePixels(entry, 2, 4);
         row[25, 3] = (row[25, 3] + 1) & 0xF; // tile 3, pixel (1, 3)

         animations.WriteFramePixels(Token, entry, 2, 4, row);

         var after = animations.ReadFrame(entry, 2);
         var changed = Enumerable.Range(0, before.Length).Where(i => before[i] != after[i]).ToList();
         Assert.Single(changed);
         Assert.Equal(3 * 32 + 3 * 4 + 0, changed[0]);
      }

      [Fact]
      public void FramePictures_ABlankTileAtTheEndOfAShortRowIsNotStored() {
         var animations = CreateAnimations();
         var entry = AddPatternedAnimation(animations, 10, 2); // 10 tiles in rows of 4: the last row has two tiles
         var before = animations.ReadFrame(entry, 0);
         var picture = animations.ReadFramePixels(entry, 0, 4);
         Assert.Equal(32, picture.GetLength(0));
         Assert.Equal(24, picture.GetLength(1));
         picture[20, 20] = 9; // inside the part of the last row that is no tile
         picture[3, 3] = (picture[3, 3] + 1) & 0xF;

         animations.WriteFramePixels(Token, entry, 0, 4, picture);

         var after = animations.ReadFrame(entry, 0);
         Assert.Equal(before.Length, after.Length);
         var changed = Enumerable.Range(0, before.Length).Where(i => before[i] != after[i]).ToList();
         Assert.Single(changed); // only the real pixel changed
      }

      [Fact]
      public void AnimationFrameSource_FollowsItsAnimation() {
         var animations = CreateAnimations();
         AddPatternedAnimation(animations, 4, 3);
         var entry = CurrentFirstEntry(animations);
         var source = new AnimationFrameSource(Model, animations, TilesetStart, entry, "Anim", () => CurrentFirstEntry(animations));

         Assert.True(source.IsValid);
         Assert.Equal(3, source.FrameCount);
         Assert.Equal("Anim", source.Title);
         Assert.True(source.CanChooseWidth);
         Assert.Equal(2, source.DefaultWidthTiles);
         Assert.Equal(4, source.MaxWidthTiles);
         Assert.Equal(entry.FramesAddress + 8, source.SpritePointer(2));

         // every frame is registered as tiles that know their palette: that is how the editor finds the colors
         Assert.True(source.Prepare());
         for (int f = 0; f < 3; f++) Assert.IsAssignableFrom<ISpriteRun>(Model.GetNextRun(animations.FrameAddress(entry, f)));

         // drawing through the source changes that frame
         var picture = source.ReadFrame(1, 0);
         picture[0, 0] = (picture[0, 0] + 1) & 0xF;
         source.WriteFrame(Token, 1, 0, picture);
         Assert.Equal(picture[0, 0], source.ReadFrame(1, 0)[0, 0]);

         // a frame table that points at one picture twice: editing one of them edits the other
         Model.WritePointer(Token, entry.FramesAddress + 8, Model.ReadPointer(entry.FramesAddress));
         Assert.Equal("the same picture as frame 1", source.FrameNote(2));
         Assert.Equal(string.Empty, source.FrameNote(0));
         Assert.Equal(string.Empty, source.FrameNote(1));
      }

      [Fact]
      public void AnimationFrameSource_StrayPointerIntoAFrame_FramesStillDontOverlapOtherRuns() {
         // some ROMs have a word in the middle of unrelated data that looks like a pointer into the middle of a frame: the model then has a one byte run there
         var animations = CreateAnimations();
         AddPatternedAnimation(animations, 4, 3);
         var entry = CurrentFirstEntry(animations);
         var frameStart = animations.FrameAddress(entry, 1);
         Model.ClearFormat(Token, frameStart, 32 * entry.TileCount); // like in a ROM that was never formatted: the frame is plain data that only the frame table points to
         Model.WritePointer(Token, entry.FramesAddress + 4, frameStart);
         var inside = frameStart + 40;
         var strayPointerAt = 0xE00;
         Model.WritePointer(Token, strayPointerAt, inside);
         Model.ObserveRunWritten(Token, NoInfoRun.FromPointer(Model, strayPointerAt)); // the model keeps a one byte run for every pointer destination
         Assert.Equal(inside, Model.GetNextRun(inside).Start); // the stray pointer made a run in the middle of the frame

         var source = new AnimationFrameSource(Model, animations, TilesetStart, entry, "Anim", () => CurrentFirstEntry(animations));
         Assert.True(source.Prepare());

         IFormattedRun previous = null;
         foreach (var run in Model.All<IFormattedRun>()) {
            if (previous != null) Assert.True(previous.Start + previous.Length <= run.Start, $"the run at {previous.Start:X} ({previous.Length} bytes) overlaps the run at {run.Start:X}");
            previous = run;
         }
         Assert.IsAssignableFrom<ISpriteRun>(Model.GetNextRun(frameStart));
         Assert.Equal(frameStart, Model.GetNextRun(frameStart).Start);
      }

      [Fact]
      public void AnimationFrameSource_IsNotValidOnceItsAnimationIsGoneOrIsAnother() {
         var animations = CreateAnimations();
         TilesetAnimationConstants.TryRead(Model, out var constants);
         AddPatternedAnimation(animations, 4, 3);
         var source = new AnimationFrameSource(Model, animations, TilesetStart, CurrentFirstEntry(animations), "Anim", () => CurrentFirstEntry(animations));
         Assert.True(source.IsValid);

         animations.TryGetTable(TilesetStart, out var table, out _);
         animations.RemoveEntry(table, 0);
         Assert.False(source.IsValid);
         Assert.Equal(0, source.FrameCount);

         // a different animation took its place in the table
         animations.TryGetTable(TilesetStart, out table, out _);
         animations.AddEntry(table, 0, 2, 3, 4, false, constants, new byte[32 * 16]);
         Assert.False(source.IsValid);
      }

      private DoorAnimations CreateDoorTable(int size, out DoorEntry entry) {
         // like EnsureTilesFormat_ReshapesDoorTilesRegisteredWithTheWrongSize: a door at block 600 with its frames at 0x800
         var tableStart = 0x600;
         Model.WriteMultiByteValue(tableStart, 2, Token, 600);
         Model.WritePointer(Token, tableStart + 4, TilesetStart);
         Model[tableStart + 8] = 0; Model[tableStart + 9] = (byte)size;
         Model.WritePointer(Token, tableStart + 12, 0x800);
         Model.WritePointer(Token, tableStart + 16, 0xF00);
         for (int i = 20; i < 40; i++) Model[tableStart + i] = 0;
         ViewPort.Edit($"@{tableStart:X6} ^{DoorAnimations.TableName}[metatile: unused: tileset<> sound. size. unused: tiles<`ucs4x4x12`> palettes<>]2 ");
         var doors = new DoorAnimations(Model, () => Token);
         entry = doors.ReadEntries()[0];
         doors.EnsureTilesFormat(entry);
         return doors;
      }

      [Fact]
      public void DoorFramePictures_AreLaidOutTheWayTheGameDrawsThem() {
         var doors = CreateDoorTable(2, out var entry); // 2x2: four blocks of 4 tiles per frame
         Model[entry.TilesAddress + (1 * 16 + 4) * 32] = 0x5A; // frame 1, tile 4: the first tile of the second block, bottom left

         var picture = doors.ReadFramePixels(entry, 1);

         Assert.Equal(32, picture.GetLength(0));
         Assert.Equal(32, picture.GetLength(1));
         Assert.Equal(0xA, picture[0, 16]);
         Assert.Equal(0x5, picture[1, 16]);
         Assert.Equal(0xF, picture[0, 0]); // the rest of the model is 0xFF
      }

      [Fact]
      public void DoorFramePictures_WritingChangesOnlyThatPixelOfThatFrame() {
         var doors = CreateDoorTable(2, out var entry);
         var before = Model.RawData.Skip(entry.TilesAddress).Take(entry.TilesLength).ToArray();
         var picture = doors.ReadFramePixels(entry, 1);
         picture[2, 17] = 3; // left column, third row of tiles: tile 4 of the frame; pixel (2, 1)

         doors.WriteFramePixels(Token, entry, 1, picture);

         var after = Model.RawData.Skip(entry.TilesAddress).Take(entry.TilesLength).ToArray();
         var changed = Enumerable.Range(0, before.Length).Where(i => before[i] != after[i]).ToList();
         Assert.Single(changed);
         Assert.Equal((1 * 16 + 4) * 32 + 1 * 4 + 1, changed[0]);
         Assert.Equal(0xF3, after[changed[0]]);
      }

      [Fact]
      public void DoorFrameSource_DescribesTheDoorAndNoticesWhenItChanged() {
         var doors = CreateDoorTable(2, out var entry);
         var source = new DoorFrameSource(doors, entry, "Door");

         Assert.True(source.IsValid);
         Assert.Equal(3, source.FrameCount);
         Assert.False(source.CanChooseWidth);
         Assert.Equal(4, source.DefaultWidthTiles);
         Assert.True(source.Prepare());
         Assert.Equal(entry.EntryAddress + 12, source.SpritePointer(2));
         Assert.Equal(32, source.ReadFrame(1, 0).GetLength(0));
         Assert.Equal(32, source.ReadFrame(1, 0).GetLength(1));

         Model.WritePointer(Token, entry.EntryAddress + 4, 0x40); // the door now belongs to another tileset
         Assert.False(source.IsValid);
      }

      [Fact]
      public void DoorFrameSource_StrayPointerIntoTheTiles_TilesStillDontOverlapOtherRuns() {
         var doors = CreateDoorTable(2, out var entry);
         Model.ClearFormat(Token, entry.TilesAddress, entry.TilesLength);
         Model.WritePointer(Token, entry.EntryAddress + 12, entry.TilesAddress);
         var inside = entry.TilesAddress + 100;
         Model.WritePointer(Token, 0xE00, inside);
         Model.ObserveRunWritten(Token, NoInfoRun.FromPointer(Model, 0xE00)); // the model keeps a one byte run for every pointer destination
         Assert.Equal(inside, Model.GetNextRun(inside).Start);

         Assert.True(new DoorFrameSource(doors, entry, "Door").Prepare());

         IFormattedRun previous = null;
         foreach (var run in Model.All<IFormattedRun>()) {
            if (previous != null) Assert.True(previous.Start + previous.Length <= run.Start, $"the run at {previous.Start:X} ({previous.Length} bytes) overlaps the run at {run.Start:X}");
            previous = run;
         }
         Assert.IsAssignableFrom<ISpriteRun>(Model.GetNextRun(entry.TilesAddress));
      }

      #endregion

      #region The first frame in the tileset (so the animated tiles can be picked in the map editor)

      /// <summary>Tiles whose bytes are all different from zero, so a blank tile can't be mistaken for one.</summary>
      private static byte[] Tiles(int count) => Enumerable.Range(0, count * 32).Select(i => (byte)((i / 32) * 16 + (i % 32 > 15 ? 3 : 0) + 1)).ToArray();

      private void WriteTileset(byte[] tiles, bool registered = true) {
         var compressed = LZRun.Compress(tiles, 0, tiles.Length);
         for (int i = 0; i < compressed.Count; i++) Model[0x800 + i] = compressed[i];
         if (registered) Model.ObserveRunWritten(Token, new LzTilesetRun(new TilesetFormat(4, null), Model, 0x800, SortedSpan.One(TilesetStart + 4)));
      }

      private TilesetAnimationEntry AddAnimation(TilesetAnimations animations, byte[] tilesetTiles, int firstTile, int tileCount, int frameCount = 2) {
         TilesetAnimationConstants.TryRead(Model, out var constants);
         animations.TryGetTable(TilesetStart, out var existing, out _);
         var table = existing ?? animations.EnsureTable(TilesetStart, false, constants, out _);
         return animations.AddEntry(table, firstTile, tileCount, frameCount, 4, false, constants, tilesetTiles);
      }

      private static byte[] Slice(byte[] data, int tile, int count) => data.Skip(tile * 32).Take(count * 32).ToArray();

      [Fact]
      public void FirstFrame_IsStoredAsTheTilesetsPictureOfTheTiles() {
         var animations = CreateAnimations();
         var tiles = Tiles(16);
         WriteTileset(tiles);
         var entry = AddAnimation(animations, tiles, 4, 2);
         Assert.Equal(FirstFrameState.InSync, animations.CompareFirstFrame(animations.ReadTilesetData(TilesetStart), entry));
         var picture = animations.ReadFramePixels(entry, 0, 0);
         picture[11, 3] = 7; // the second tile of the two
         animations.WriteFramePixels(Token, entry, 0, 0, picture);
         Assert.Equal(FirstFrameState.Different, animations.CompareFirstFrame(animations.ReadTilesetData(TilesetStart), entry));

         var result = animations.ShowFirstFrameInTileset(Token, TilesetStart, entry);

         Assert.False(result.Failed);
         Assert.Equal(1, result.ChangedTiles);
         Assert.Equal(0, result.SkippedTiles);
         var after = animations.ReadTilesetData(TilesetStart);
         Assert.Equal(16 * 32, after.Length);
         Assert.Equal(animations.ReadFrame(entry, 0), Slice(after, 4, 2));
         Assert.Equal(Slice(tiles, 0, 4), Slice(after, 0, 4));   // the tiles around the animation are as they were
         Assert.Equal(Slice(tiles, 6, 10), Slice(after, 6, 10));
         Assert.Equal(FirstFrameState.InSync, animations.CompareFirstFrame(after, entry));
         // the animation itself is untouched: the other frames still start as copies of the old tiles
         Assert.Equal(Slice(tiles, 4, 2), animations.ReadFrame(entry, 1));
      }

      [Fact]
      public void FirstFrame_AlreadyInTheTileset_ChangesNothing() {
         var animations = CreateAnimations();
         var tiles = Tiles(16);
         WriteTileset(tiles);
         var entry = AddAnimation(animations, tiles, 4, 2);
         var before = Model.RawData.ToArray();

         var result = animations.ShowFirstFrameInTileset(Token, TilesetStart, entry);

         Assert.Equal(0, result.ChangedTiles);
         Assert.False(result.Failed);
         Assert.Equal(before, Model.RawData);
      }

      [Fact]
      public void FirstFrame_WorksForTilesetsNothingRegisteredAsTiles() {
         var animations = CreateAnimations();
         var tiles = Tiles(16);
         WriteTileset(tiles, registered: false);
         var entry = AddAnimation(animations, tiles, 4, 2);
         animations.WriteFrame(entry, 0, Enumerable.Repeat((byte)0x5A, 64).ToArray());

         var result = animations.ShowFirstFrameInTileset(Token, TilesetStart, entry);

         Assert.Equal(2, result.ChangedTiles);
         Assert.Equal(Enumerable.Repeat((byte)0x5A, 64), Slice(animations.ReadTilesetData(TilesetStart), 4, 2));
      }

      [Fact]
      public void FirstFrame_BlankTilesetTilesAreRecognizedAndFilledIn() {
         var animations = CreateAnimations();
         var tiles = Tiles(16);
         for (int i = 8 * 32; i < 10 * 32; i++) tiles[i] = 0; // tiles 8 and 9 are free space in the tileset
         WriteTileset(tiles);
         var blank = AddAnimation(animations, tiles, 8, 2);
         var used = AddAnimation(animations, tiles, 0, 1);
         animations.TryGetTable(TilesetStart, out var table, out _);
         TilesetAnimationConstants.TryRead(Model, out var constants);
         var entries = animations.ReadEntries(table, false, constants);
         blank = entries[0];
         used = entries[1];
         animations.WriteFrame(blank, 0, Enumerable.Range(0, 64).Select(i => (byte)(i + 1)).ToArray());
         animations.WriteFrame(used, 0, Enumerable.Repeat((byte)0x77, 32).ToArray());
         var tileset = animations.ReadTilesetData(TilesetStart);
         Assert.Equal(FirstFrameState.TilesetBlank, animations.CompareFirstFrame(tileset, blank));
         Assert.Equal(FirstFrameState.Different, animations.CompareFirstFrame(tileset, used));

         var healed = animations.ShowFirstFramesInBlankTiles(Token, TilesetStart, false, constants);

         Assert.Equal(1, healed);
         var after = animations.ReadTilesetData(TilesetStart);
         Assert.Equal(FirstFrameState.InSync, animations.CompareFirstFrame(after, blank));
         Assert.Equal(Slice(tiles, 0, 1), Slice(after, 0, 1)); // a tile that has its own picture is never replaced on its own
         Assert.Equal(FirstFrameState.Different, animations.CompareFirstFrame(after, used));
         Assert.Equal(0, animations.ShowFirstFramesInBlankTiles(Token, TilesetStart, false, constants));

         // asked for explicitly, the tile does follow its animation
         Assert.Equal(1, animations.ShowFirstFrameInTileset(Token, TilesetStart, used).ChangedTiles);
         Assert.Equal(Enumerable.Repeat((byte)0x77, 32), Slice(animations.ReadTilesetData(TilesetStart), 0, 1));
      }

      [Fact]
      public void FirstFrame_TilesetGrowsToHoldTilesPastItsEnd() {
         var animations = CreateAnimations();
         var tiles = Tiles(16);
         WriteTileset(tiles);
         var entry = AddAnimation(animations, tiles, 14, 4);   // tiles 16 and 17 are not part of the tileset yet
         var frame = Enumerable.Range(0, 128).Select(i => (byte)(i % 13 + 1)).ToArray();
         animations.WriteFrame(entry, 0, frame);

         var result = animations.ShowFirstFrameInTileset(Token, TilesetStart, entry);

         Assert.Equal(4, result.ChangedTiles);
         Assert.Equal(0, result.SkippedTiles);
         var after = animations.ReadTilesetData(TilesetStart);
         Assert.Equal(18 * 32, after.Length);
         Assert.Equal(frame, Slice(after, 14, 4));
         Assert.Equal(Slice(tiles, 0, 14), Slice(after, 0, 14));
      }

      [Theory]
      [InlineData(512)]
      [InlineData(640)]
      public void FirstFrame_SecondaryTilesetsNumberTheirTilesAfterThePrimaryOnes(int primaryTiles) {
         Model.SetUnmappedConstant(Token, TilesetAnimationConstants.Prefix + "primarytiles", primaryTiles);
         Model[TilesetStart + 1] = 1; // the header says: secondary tileset
         var animations = CreateAnimations();
         var tiles = Tiles(16);
         WriteTileset(tiles);
         TilesetAnimationConstants.TryRead(Model, out var constants);
         var table = animations.EnsureTable(TilesetStart, true, constants, out _);
         var entry = animations.AddEntry(table, 4, 2, 2, 4, true, constants, tiles);
         Assert.Equal(primaryTiles + 4, Model.ReadMultiByteValue(entry.EntryAddress + 8, 4)); // what the game's table stores...
         animations.WriteFrame(entry, 0, Enumerable.Repeat((byte)0x5C, 64).ToArray());

         var result = animations.ShowFirstFrameInTileset(Token, TilesetStart, entry);

         Assert.False(result.Failed);
         Assert.Equal(2, result.ChangedTiles);
         Assert.Equal(0, result.SkippedTiles);
         var after = animations.ReadTilesetData(TilesetStart);
         Assert.Equal(16 * 32, after.Length);                                                  // ...is tile 4 of the secondary tileset's own graphics, which did not grow
         Assert.Equal(Enumerable.Repeat((byte)0x5C, 64), Slice(after, 4, 2));
         Assert.Equal(Slice(tiles, 0, 4), Slice(after, 0, 4));
         Assert.Equal(Slice(tiles, 6, 10), Slice(after, 6, 10));
         Assert.Equal(FirstFrameState.InSync, animations.CompareFirstFrame(after, entry));
      }

      [Fact]
      public void FirstFrame_StopsAtTheEndOfVideoMemory() {
         Model.SetUnmappedConstant(Token, TilesetAnimationConstants.Prefix + "primarytiles", 16);
         var animations = CreateAnimations();
         var tiles = Tiles(16);
         WriteTileset(tiles);
         var entry = AddAnimation(animations, tiles, 14, 4);
         animations.WriteFrame(entry, 0, Enumerable.Repeat((byte)0x33, 128).ToArray());

         var result = animations.ShowFirstFrameInTileset(Token, TilesetStart, entry);

         Assert.Equal(2, result.ChangedTiles);
         Assert.Equal(2, result.SkippedTiles); // the primary tileset only has room for 16 tiles here; the rest is the secondary tileset's
         var after = animations.ReadTilesetData(TilesetStart);
         Assert.Equal(16 * 32, after.Length);
         Assert.Equal(Enumerable.Repeat((byte)0x33, 64), Slice(after, 14, 2));
      }

      [Theory]
      [InlineData(true)]
      [InlineData(false)]
      public void FirstFrame_TilesGetMovedWhenTheyGetLonger_AndTheTilesetFollows(bool registered) {
         var animations = CreateAnimations();
         var tiles = new byte[16 * 32];                         // all blank: it compresses to almost nothing...
         WriteTileset(tiles, registered);
         var oldLength = LZRun.Compress(tiles, 0, tiles.Length).Count;
         for (int i = 0; i < 4; i++) Model[0x800 + oldLength + i] = 0x12;   // ...and something else starts right behind it
         var entry = AddAnimation(animations, tiles, 2, 2);
         var frame = Enumerable.Range(0, 64).Select(i => (byte)(i * 37 + 11)).ToArray(); // does not compress
         animations.WriteFrame(entry, 0, frame);

         var result = animations.ShowFirstFrameInTileset(Token, TilesetStart, entry);

         Assert.Equal(2, result.ChangedTiles);
         var moved = Model.ReadPointer(TilesetStart + 4);
         Assert.NotEqual(0x800, moved);                          // the header follows the tiles
         Assert.Equal(0xFF, Model[0x800]);                       // the old copy is cleared
         Assert.Equal(0x12, Model[0x800 + oldLength]);           // what was behind it is intact
         var after = animations.ReadTilesetData(TilesetStart);
         Assert.Equal(frame, Slice(after, 2, 2));
         Assert.Equal(Slice(tiles, 0, 2), Slice(after, 0, 2));
         var run = Assert.IsType<LzTilesetRun>(Model.GetNextRun(moved));
         Assert.Equal(moved, run.Start);
         Assert.Contains(TilesetStart + 4, run.PointerSources);
      }

      [Fact]
      public void FirstFrame_UndoTakesBackTheTilesetAndItsPointerTogether() {
         var animations = CreateAnimations();
         var tiles = new byte[16 * 32];
         WriteTileset(tiles);
         var oldLength = LZRun.Compress(tiles, 0, tiles.Length).Count;
         for (int i = 0; i < 4; i++) Model[0x800 + oldLength + i] = 0x12;
         var entry = AddAnimation(animations, tiles, 2, 2);
         animations.WriteFrame(entry, 0, Enumerable.Range(0, 64).Select(i => (byte)(i * 37 + 11)).ToArray());
         ViewPort.ChangeHistory.ChangeCompleted();
         var compressedBefore = Model.RawData.Skip(0x800).Take(oldLength).ToArray();

         animations.ShowFirstFrameInTileset(ViewPort.CurrentChange, TilesetStart, entry);
         ViewPort.ChangeHistory.ChangeCompleted();
         Assert.NotEqual(0x800, Model.ReadPointer(TilesetStart + 4));
         ViewPort.Undo.Execute();

         Assert.Equal(0x800, Model.ReadPointer(TilesetStart + 4));
         Assert.Equal(compressedBefore, Model.RawData.Skip(0x800).Take(oldLength).ToArray());
         Assert.Equal(tiles, animations.ReadTilesetData(TilesetStart));
      }

      [Fact]
      public void FirstFrame_TilesetsThatAreNotCompressedAreWrittenInPlace() {
         Model[TilesetStart] = 0;
         var animations = CreateAnimations();
         var tiles = Tiles(8);
         for (int i = 0; i < tiles.Length; i++) Model[0x800 + i] = tiles[i];
         var entry = AddAnimation(animations, ReadRaw(0x800, 8), 2, 2);
         animations.WriteFrame(entry, 0, Enumerable.Repeat((byte)0x44, 64).ToArray());

         var result = animations.ShowFirstFrameInTileset(Token, TilesetStart, entry);

         Assert.False(result.Failed);
         Assert.Equal(2, result.ChangedTiles);
         Assert.Equal(0x800, Model.ReadPointer(TilesetStart + 4));
         Assert.Equal(Enumerable.Repeat((byte)0x44, 64), Slice(animations.ReadTilesetData(TilesetStart), 2, 2));
         Assert.Equal(Slice(tiles, 0, 2), Slice(animations.ReadTilesetData(TilesetStart), 0, 2));
      }

      private byte[] ReadRaw(int address, int tileCount) => Model.RawData.Skip(address).Take(tileCount * 32).ToArray();

      [Fact]
      public void FirstFrame_BuiltInAnimationsAreNotTouched() {
         var animations = CreateAnimations();
         var tiles = Tiles(16);
         WriteTileset(tiles);
         var entry = new TilesetAnimationEntry { FirstTile = 4, TileCount = 2, FrameCount = 1, IsBuiltIn = true, FramesAddress = 0x300 };
         Model.WritePointer(Token, 0x300, 0x400);

         var result = animations.ShowFirstFrameInTileset(Token, TilesetStart, entry);

         Assert.True(result.Failed);
         Assert.Equal(tiles, animations.ReadTilesetData(TilesetStart));
      }

      [Fact]
      public void FirstFrame_RemovingTheAnimationLeavesTheTilesetsTiles() {
         var animations = CreateAnimations();
         var tiles = Tiles(16);
         WriteTileset(tiles);
         var entry = AddAnimation(animations, tiles, 4, 2);
         animations.WriteFrame(entry, 0, Enumerable.Repeat((byte)0x66, 64).ToArray());
         animations.ShowFirstFrameInTileset(Token, TilesetStart, entry);
         var before = animations.ReadTilesetData(TilesetStart);

         animations.TryGetTable(TilesetStart, out var table, out _);
         animations.RemoveEntry(table, 0);

         // the blocks that were built from these tiles keep looking right: the tiles stay as frame 1 was
         Assert.Equal(before, animations.ReadTilesetData(TilesetStart));
         Assert.Equal(Enumerable.Repeat((byte)0x66, 64), Slice(animations.ReadTilesetData(TilesetStart), 4, 2));
      }

      [Fact]
      public void RemoveEntry_OnlyClearsTheAnimationsOwnFramesAndFrameTable() {
         var animations = CreateAnimations();
         var tiles = Tiles(16);
         WriteTileset(tiles);
         var first = AddAnimation(animations, tiles, 4, 2);
         var second = AddAnimation(animations, tiles, 8, 2, 3);
         animations.TryGetTable(TilesetStart, out var table, out _);
         TilesetAnimationConstants.TryRead(Model, out var constants);
         second = animations.ReadEntries(table, false, constants)[1];
         for (int f = 0; f < 3; f++) animations.WriteFrame(second, f, Enumerable.Repeat((byte)(0x10 + f), 64).ToArray());
         var tilesetBefore = animations.ReadTilesetData(TilesetStart);
         var compressedBefore = Model.RawData.Skip(0x800).Take(0x100).ToArray();

         animations.RemoveEntry(table, 0);

         // the frame table of the removed animation is only 4 bytes per frame: nothing after it may be erased
         animations.TryGetTable(TilesetStart, out table, out _);
         var entries = animations.ReadEntries(table, false, constants);
         Assert.Single(entries);
         Assert.Equal(3, entries[0].FrameCount);
         for (int f = 0; f < 3; f++) Assert.Equal(Enumerable.Repeat((byte)(0x10 + f), 64), animations.ReadFrame(entries[0], f));
         Assert.Equal(tilesetBefore, animations.ReadTilesetData(TilesetStart));
         Assert.Equal(compressedBefore, Model.RawData.Skip(0x800).Take(0x100).ToArray());
         Assert.Equal(1, table.ElementCount);
      }

      private ImageEditorViewModel CreateAnimationEditor(TilesetAnimations animations, byte[] tiles, int firstTile, int tileCount, out AnimationFrameSource source) {
         var entry = AddAnimation(animations, tiles, firstTile, tileCount, 3);
         source = new AnimationFrameSource(Model, animations, TilesetStart, entry, "Anim", () => CurrentFirstEntry(animations));
         Assert.True(source.Prepare());
         ViewPort.ChangeHistory.ChangeCompleted();
         var editor = ViewPort.CreateImageEditor(Model.ReadPointer(source.SpritePointer(0)), 0, 0);
         editor.SpriteScale = 1;
         editor.SetFrameSource(source, 0);
         return editor;
      }

      private static void Draw(ImageEditorViewModel editor, int paletteIndex, int pixelX, int pixelY, bool lift = true) {
         editor.SelectedTool = ImageEditorTools.Draw;
         editor.Palette.SelectionStart = paletteIndex;
         var point = new Point(pixelX - editor.PixelWidth / 2, pixelY - editor.PixelHeight / 2);
         editor.ToolDown(point);
         if (lift) editor.ToolUp(point);
      }

      [Fact]
      public void ImageEditor_DrawingOnTheFirstFrameStoresItInTheTilesetWhenThePenIsLifted() {
         var animations = CreateAnimations();
         var tiles = Tiles(16);
         WriteTileset(tiles);
         var editor = CreateAnimationEditor(animations, tiles, 4, 1, out var source);

         Draw(editor, 5, 2, 3, lift: false);
         var entry = CurrentFirstEntry(animations);
         Assert.Equal(Slice(tiles, 4, 1), animations.ReadFrame(entry, 0));                        // the pen is still down: nothing is stored yet
         Assert.Equal(Slice(tiles, 4, 1), Slice(animations.ReadTilesetData(TilesetStart), 4, 1));
         editor.ToolUp(new Point(2 - editor.PixelWidth / 2, 3 - editor.PixelHeight / 2));
         Assert.Equal(5, animations.ReadFramePixels(entry, 0, 0)[2, 3]);                          // the stroke is in the frame...

         var tileset = animations.ReadTilesetData(TilesetStart);
         Assert.Equal(animations.ReadFrame(entry, 0), Slice(tileset, 4, 1));
         Assert.Equal(Slice(tiles, 0, 4), Slice(tileset, 0, 4));
         Assert.Equal(Slice(tiles, 5, 11), Slice(tileset, 5, 11));
         Assert.Equal(FirstFrameState.InSync, animations.CompareFirstFrame(tileset, entry));
      }

      [Fact]
      public void ImageEditor_OtherFramesDoNotTouchTheTileset() {
         var animations = CreateAnimations();
         var tiles = Tiles(16);
         WriteTileset(tiles);
         var editor = CreateAnimationEditor(animations, tiles, 4, 1, out var source);
         var before = Model.RawData.Skip(0x800).Take(0x100).ToArray();
         var pointerBefore = Model.ReadPointer(TilesetStart + 4);

         editor.Frame = 1;
         Draw(editor, 9, 1, 1);

         var entry = CurrentFirstEntry(animations);
         Assert.Equal(9, animations.ReadFramePixels(entry, 1, 0)[1, 1]);
         Assert.Equal(pointerBefore, Model.ReadPointer(TilesetStart + 4));
         Assert.Equal(before, Model.RawData.Skip(0x800).Take(0x100).ToArray());
         Assert.Equal(tiles, animations.ReadTilesetData(TilesetStart));
      }

      [Fact]
      public void ImageEditor_UndoTakesBackTheFrameAndTheTilesetTogether() {
         var animations = CreateAnimations();
         var tiles = Tiles(16);
         WriteTileset(tiles);
         var editor = CreateAnimationEditor(animations, tiles, 4, 1, out var source);

         Draw(editor, 5, 2, 3);
         var entry = CurrentFirstEntry(animations);
         Assert.NotEqual(Slice(tiles, 4, 1), Slice(animations.ReadTilesetData(TilesetStart), 4, 1));
         ViewPort.Undo.Execute();

         Assert.Equal(Slice(tiles, 4, 1), animations.ReadFrame(entry, 0));
         Assert.Equal(tiles, animations.ReadTilesetData(TilesetStart));
      }

      [Fact]
      public void ImageEditor_ThePictureIsStoredOnceNotForEveryPixel() {
         var animations = CreateAnimations();
         var tiles = Tiles(16);
         WriteTileset(tiles);
         var editor = CreateAnimationEditor(animations, tiles, 4, 1, out var source);
         var writes = 0;
         var counting = new CountingFrameSource(source, () => writes++);
         editor.SetFrameSource(counting, 0);

         editor.SelectedTool = ImageEditorTools.Draw;
         editor.Palette.SelectionStart = 6;
         var start = new Point(0 - 4, 0 - 4);
         editor.ToolDown(start);
         for (int x = 1; x < 8; x++) editor.Hover(new Point(x - 4, 0 - 4));
         editor.ToolUp(new Point(7 - 4, 0 - 4));

         Assert.Equal(1, writes);
      }

      private class CountingFrameSource : IImageFrameSource {
         private readonly IImageFrameSource inner;
         private readonly System.Action onCompleted;
         public CountingFrameSource(IImageFrameSource inner, System.Action onCompleted) => (this.inner, this.onCompleted) = (inner, onCompleted);
         public string Title => inner.Title;
         public bool IsValid => inner.IsValid;
         public int FrameCount => inner.FrameCount;
         public string FrameNote(int frame) => inner.FrameNote(frame);
         public int SpritePointer(int frame) => inner.SpritePointer(frame);
         public bool CanChooseWidth => inner.CanChooseWidth;
         public int DefaultWidthTiles => inner.DefaultWidthTiles;
         public int MaxWidthTiles => inner.MaxWidthTiles;
         public int[,] ReadFrame(int frame, int widthTiles) => inner.ReadFrame(frame, widthTiles);
         public void WriteFrame(ModelDelta token, int frame, int widthTiles, int[,] pixels) => inner.WriteFrame(token, frame, widthTiles, pixels);
         public void EditCompleted(ModelDelta token, int frame) { onCompleted(); inner.EditCompleted(token, frame); }
      }

      #endregion
   }
}
