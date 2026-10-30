using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// Importing a picture into an overworld sprite slot whose pictures and palette are shared with other slots
   /// (the spare slots of CUBE all start as copies of the Pikachu): the other slots must keep their pictures, and the user can ask for a new palette.
   /// </summary>
   public class OverworldNewPaletteTests : BaseViewModelTestClass {
      private const int
         Frame0 = 0x100, Frame1 = 0x140,
         PalA = 0x180, PalB = 0x1A0,
         PalTable = 0x1C0,
         List = 0x200,
         RowA = 0x240, RowB = 0x280, RowC = 0x2C0,
         SpritesOffset = 28, PaletteIdOffset = 2;
      private const string RowFormat = "[tileTag: paletteid:|h reflectionPaletteTag:|h length: width: height: info. tracks. unused: oam<> subsprites<> anims<> sprites<`osl|graphics.overworld.palettes:id=`> affine<>]1";

      private readonly List<string> dialogs = new List<string>();
      private readonly List<string[]> dialogOptions = new List<string[]>();

      /// <summary>Is the palette table closed by an end marker row that is part of its count (CUBE) or by eight zero bytes after the last palette (the vanilla games)?</summary>
      private readonly bool endCode;
      private int EndRows => endCode ? 0 : 1;

      public OverworldNewPaletteTests() : this(false) { }

      protected OverworldNewPaletteTests(bool endCode) : base(0x4000) {
         this.endCode = endCode;
         SetFullModel(0xFF);
         for (int i = 0; i < 32; i++) { Data[Frame0 + i] = (byte)(0x11 * (1 + i % 3)); Data[Frame1 + i] = (byte)(0x21 + i); }
         for (int i = 0; i < 16; i++) {
            Model.WriteMultiByteValue(PalA + i * 2, 2, Token, 0x0421 * (i + 1));
            Model.WriteMultiByteValue(PalB + i * 2, 2, Token, 0x0842 * (i + 1) % 0x7FFF);
         }
         // palette table: two palettes and the end marker the game looks for
         Model.WritePointer(Token, PalTable, PalA);
         Model.WriteMultiByteValue(PalTable + 4, 2, Token, 0x1100);
         Model.WriteMultiByteValue(PalTable + 6, 2, Token, 0);
         Model.WritePointer(Token, PalTable + 8, PalB);
         Model.WriteMultiByteValue(PalTable + 12, 2, Token, 0x1101);
         Model.WriteMultiByteValue(PalTable + 14, 2, Token, 0);
         Model.WriteMultiByteValue(PalTable + 16, 4, Token, 0);
         Model.WriteMultiByteValue(PalTable + 20, 2, Token, endCode ? 0 : 0x11FF);
         Model.WriteMultiByteValue(PalTable + 22, 2, Token, 0);
         if (endCode) ViewPort.Edit($"@{PalTable:X} ^{HardcodeTablesModel.OverworldPalettes}[pal<`ucp4`> id:|h unused:]!0000000000000000 ");
         else ViewPort.Edit($"@{PalTable:X} ^{HardcodeTablesModel.OverworldPalettes}[pal<`ucp4`> id:|h unused:]3 ");
         // the list of two 8x8 pictures
         Model.WritePointer(Token, List, Frame0);
         Model.WriteMultiByteValue(List + 4, 4, Token, 0x20);
         Model.WritePointer(Token, List + 8, Frame1);
         Model.WriteMultiByteValue(List + 12, 4, Token, 0x20);
         // the graphics-info rows
         foreach (var row in new[] { RowA, RowB, RowC }) {
            for (int i = 0; i < 36; i++) Data[row + i] = 0;
            Model.WriteMultiByteValue(row + PaletteIdOffset, 2, Token, 0x1100);
            Model.WriteMultiByteValue(row + 4, 2, Token, 0x11FF);
            Model.WriteMultiByteValue(row + 8, 2, Token, 8);
            Model.WriteMultiByteValue(row + 10, 2, Token, 8);
            Model.WritePointer(Token, row + SpritesOffset, List);
         }
         foreach (var row in new[] { RowA, RowB, RowC }) ViewPort.Edit($"@{row:X} ^row{row:X}{RowFormat} ");
         ViewPort.ChangeHistory.ChangeCompleted();
      }

      private int Pointer(int row) => Model.ReadPointer(row + SpritesOffset);
      private int PaletteId(int row) => Model.ReadMultiByteValue(row + PaletteIdOffset, 2);
      private ITableRun PaletteTable => Model.GetTable(HardcodeTablesModel.OverworldPalettes);

      private byte[] Snapshot() => Model.RawData.ToArray();

      private IEnumerable<byte> Bytes(int start, int length) => Model.RawData.Skip(start).Take(length).ToArray();

      /// <summary>Two 8x8 pictures side by side (16x8), each in colors of its own.</summary>
      private static (short[] image, int width) Sheet() {
         var image = new short[16 * 8];
         for (int y = 0; y < 8; y++) {
            for (int x = 0; x < 16; x++) {
               var frame = x / 8;
               var inside = (x % 8 + y) % 3 != 0;
               image[y * 16 + x] = (short)(inside ? (frame == 0 ? 0x1234 : 0x2468) : 0x7C1F);
            }
         }
         return (image, 16);
      }

      private StubFileSystem FileSystem(params int[] answers) {
         var (image, width) = Sheet();
         var next = 0;
         return new StubFileSystem {
            LoadImage = name => (image, width),
            TryLoadIndexImage = (ref string name, out int[,] indexed, out IReadOnlyList<short> palette) => { name = "sheet.png"; indexed = null; palette = null; return false; },
            ShowOptions = (title, prompt, details, options) => {
               dialogs.Add(prompt);
               dialogOptions.Add(options.Select(option => option.Option).ToArray());
               var answer = next < answers.Length ? answers[next] : -1;
               next++;
               return answer;
            },
         };
      }

      private void ImportInto(int row, StubFileSystem fileSystem) {
         ViewPort.Goto.Execute(row);
         var sprite = ViewPort.Tools.TableTool.Children.OfType<SpriteElementViewModel>().First();
         sprite.ImportPair.Execute(fileSystem);
      }

      [Fact]
      public void Find_PointerOfASharedSlot_DescribesWhatIsShared() {
         var slot = OverworldSlotImport.Find(Model, RowA + SpritesOffset);

         Assert.NotNull(slot);
         Assert.Equal(2, slot.OtherRowsOnList);
         Assert.Equal(2, slot.OtherRowsOnPalette);
         Assert.True(slot.SharesPictures);
         Assert.True(slot.CanCreatePalette);
         Assert.True(slot.NeedsChoice);
         Assert.Equal(0x1100, slot.PaletteId);
         Assert.Equal(0x11FF, slot.ReflectionTag);
         if (endCode) Assert.IsType<TableStreamRun>(PaletteTable);
         else Assert.IsType<ArrayRun>(PaletteTable);
      }

      [Fact]
      public void Find_PaletteIdThatIsNotInTheOverworldPaletteTable_CanNotGetANewPalette() {
         Model.WriteMultiByteValue(RowA + PaletteIdOffset, 2, Token, 0x1000); // (a field effect palette, say)

         var slot = OverworldSlotImport.Find(Model, RowA + SpritesOffset);

         Assert.NotNull(slot);
         Assert.False(slot.CanCreatePalette);
         Assert.True(slot.SharesPictures);
      }

      [Fact]
      public void Find_NotAGraphicsInfoPointer_ReturnsNull() {
         Assert.Null(OverworldSlotImport.Find(Model, 0));
         Assert.Null(OverworldSlotImport.Find(Model, RowA)); // not the 'sprites' pointer
         Assert.Null(OverworldSlotImport.Find(Model, -1));
         Assert.Null(OverworldSlotImport.Find(Model, 0x10000));
      }

      [Fact]
      public void FindFreeTag_SkipsEveryTagTheTableAndTheRowsUse() {
         var slot = OverworldSlotImport.Find(Model, RowA + SpritesOffset);
         Assert.Equal(OverworldSlotImport.FirstNewTag, OverworldSlotImport.FindFreeTag(slot));

         Model.WriteMultiByteValue(RowB + PaletteIdOffset, 2, Token, OverworldSlotImport.FirstNewTag); // another sprite uses the first tag already
         Model.WriteMultiByteValue(RowC + 4, 2, Token, OverworldSlotImport.FirstNewTag + 1); // and one of them as reflection palette

         Assert.Equal(OverworldSlotImport.FirstNewTag + 2, OverworldSlotImport.FindFreeTag(slot));
      }

      [Fact]
      public void NewPalette_SharedSlot_OthersKeepPicturesAndPalette() {
         var before = Snapshot();
         var fileSystem = FileSystem(1);

         ImportInto(RowA, fileSystem);

         Assert.Empty(Errors);
         Assert.Single(dialogs);
         Assert.Contains("New palette", dialogOptions[0]);
         // the other slots point at the old data, which is untouched
         Assert.Equal(List, Pointer(RowB));
         Assert.Equal(List, Pointer(RowC));
         Assert.Equal(before.Skip(Frame0).Take(0x40), Bytes(Frame0, 0x40));
         Assert.Equal(before.Skip(PalA).Take(0x40), Bytes(PalA, 0x40));
         Assert.Equal(before.Skip(List).Take(16), Bytes(List, 16));
         Assert.Equal(0x1100, PaletteId(RowB));
         Assert.Equal(0x1100, PaletteId(RowC));
         // this slot has its own pictures and a new palette
         Assert.NotEqual(List, Pointer(RowA));
         Assert.Equal(OverworldSlotImport.FirstNewTag, PaletteId(RowA));
         Assert.Equal(0x11FF, Model.ReadMultiByteValue(RowA + 4, 2));
         var run = Assert.IsType<OverworldSpriteListRun>(Model.GetNextRun(Pointer(RowA)));
         Assert.Equal(2, run.ElementCount);
         Assert.Single(run.PointerSources);
         Assert.NotEqual(Model.ReadPointer(run.Start), Model.ReadPointer(run.Start + 8));
      }

      [Fact]
      public void NewPalette_AddsExactlyOneRowInFrontOfTheEndMarker() {
         ImportInto(RowA, FileSystem(1));

         var table = PaletteTable;
         Assert.Equal(3 + EndRows, table.ElementCount);
         var tags = table.ElementCount.Range().Select(i => Model.ReadMultiByteValue(table.Start + i * table.ElementLength + 4, 2)).ToList();
         Assert.Equal(endCode ? new[] { 0x1100, 0x1101, OverworldSlotImport.FirstNewTag } : new[] { 0x1100, 0x1101, OverworldSlotImport.FirstNewTag, 0x11FF }, tags);
         // the end marker has no palette
         Assert.Equal(0, Model.ReadMultiByteValue(table.Start + table.ElementCount * table.ElementLength - (endCode ? 0 : table.ElementLength), 4));
         if (endCode) Assert.Equal(0, Model.ReadMultiByteValue(table.Start + table.ElementCount * table.ElementLength + 4, 4));
         var paletteAddress = Model.ReadPointer(table.Start + 2 * table.ElementLength);
         var palette = Assert.IsAssignableFrom<IPaletteRun>(Model.GetNextRun(paletteAddress));
         Assert.Equal(paletteAddress, palette.Start);
         Assert.NotEqual(PalA, paletteAddress);
         Assert.NotEqual(PalB, paletteAddress);
         // the table moved if it had to: whatever points at it follows, and the name still finds it
         Assert.Equal(table.Start, Model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, HardcodeTablesModel.OverworldPalettes));
      }

      [Fact]
      public void NewPalette_TheSlotShowsTheImportedPicturesInTheNewColors() {
         ImportInto(RowA, FileSystem(1));

         var (image, width) = Sheet();
         var tool = ViewPort.Tools.SpriteTool;
         Assert.Equal(Pointer(RowA), tool.SpriteAddress);
         Assert.Equal(Model.ReadPointer(PaletteTable.Start + 2 * PaletteTable.ElementLength), tool.PaletteAddress);
         for (int page = 0; page < 2; page++) {
            tool.SpritePage = page;
            for (int y = 0; y < 8; y++) {
               for (int x = 0; x < 8; x++) {
                  Assert.Equal(image[y * width + page * 8 + x] & 0x7FFF, tool.PixelData[y * 8 + x] & 0x7FFF);
               }
            }
         }
         var frame = Assert.IsAssignableFrom<ISpriteRun>(Model.GetNextRun(Model.ReadPointer(Pointer(RowA))));
         Assert.Equal($"{HardcodeTablesModel.OverworldPalettes}:id={OverworldSlotImport.FirstNewTag:X4}", frame.SpriteFormat.PaletteHint);
      }

      [Fact]
      public void NewPalette_Undo_RestoresEveryByte() {
         var before = Snapshot();

         ImportInto(RowA, FileSystem(1));
         Assert.NotEqual(before.Length, 0);
         Assert.False(before.SequenceEqual(Snapshot()));
         ViewPort.Undo.Execute();

         Assert.True(before.SequenceEqual(Snapshot()));
         Assert.Equal(2 + EndRows, PaletteTable.ElementCount);
         Assert.Equal(3, Model.GetNextRun(List).PointerSources.Count);
      }

      [Fact]
      public void NewPalette_TwiceInDifferentSlots_MakesTwoDifferentPalettes() {
         ImportInto(RowA, FileSystem(1));
         ImportInto(RowB, FileSystem(1)); // (RowC still shares the old pictures)

         Assert.Equal(OverworldSlotImport.FirstNewTag, PaletteId(RowA));
         Assert.Equal(OverworldSlotImport.FirstNewTag + 1, PaletteId(RowB));
         Assert.Equal(0x1100, PaletteId(RowC));
         Assert.NotEqual(Pointer(RowA), Pointer(RowB));
         Assert.Equal(List, Pointer(RowC));
         Assert.Equal(4 + EndRows, PaletteTable.ElementCount);
         var ids = PaletteTable.ElementCount.Range().Select(i => Model.ReadMultiByteValue(PaletteTable.Start + i * PaletteTable.ElementLength + 4, 2)).ToList();
         Assert.Equal(ids.Count, ids.Distinct().Count());
         if (!endCode) Assert.Equal(0x11FF, ids.Last());
         Assert.Empty(Errors);
      }

      [Fact]
      public void SharedPalette_SharedSlot_CopiesThePicturesButKeepsThePaletteTag() {
         var before = Snapshot();

         ImportInto(RowA, FileSystem(2, 1)); // keep the shared palette, then 'Greedy' for the shared palette

         Assert.Equal(2, dialogs.Count);
         Assert.NotEqual(List, Pointer(RowA));
         Assert.Equal(List, Pointer(RowB));
         Assert.Equal(0x1100, PaletteId(RowA));
         Assert.Equal(2 + EndRows, PaletteTable.ElementCount);
         Assert.Equal(before.Skip(Frame0).Take(0x40), Bytes(Frame0, 0x40)); // the pictures of the other slots are not touched
         Assert.Equal(before.Skip(List).Take(16), Bytes(List, 16));
      }

      [Fact]
      public void Cancel_ChangesNothing() {
         var before = Snapshot();

         ImportInto(RowA, FileSystem(-1));

         Assert.Single(dialogs);
         Assert.True(before.SequenceEqual(Snapshot()));
      }

      [Fact]
      public void SlotWithItsOwnPicturesAndPalette_ImportsWithoutAskingAndInPlace() {
         ImportInto(RowA, FileSystem(1)); // RowA is private now
         var list = Pointer(RowA);
         var tag = PaletteId(RowA);
         var rows = PaletteTable.ElementCount;
         dialogs.Clear();

         ImportInto(RowA, FileSystem());

         Assert.Empty(dialogs);
         Assert.Equal(list, Pointer(RowA));
         Assert.Equal(tag, PaletteId(RowA));
         Assert.Equal(rows, PaletteTable.ElementCount);
      }

      [Fact]
      public void SlotWithItsOwnPicturesButASharedPalette_OffersANewPaletteWithoutCopyingThePictures() {
         // RowB and RowC get a list (and pictures) of their own, so RowA is the only user of its pictures; all three still use palette 1100
         const int OtherList = 0x300, OtherFrame0 = 0x340, OtherFrame1 = 0x360;
         for (int i = 0; i < 32; i++) { Data[OtherFrame0 + i] = (byte)(0x31 + i); Data[OtherFrame1 + i] = (byte)(0x51 + i); }
         Model.WritePointer(Token, OtherList, OtherFrame0);
         Model.WriteMultiByteValue(OtherList + 4, 4, Token, 0x20);
         Model.WritePointer(Token, OtherList + 8, OtherFrame1);
         Model.WriteMultiByteValue(OtherList + 12, 4, Token, 0x20);
         ViewPort.Edit($"@{RowB + SpritesOffset:X} <{OtherList:X6}> ");
         ViewPort.Edit($"@{RowC + SpritesOffset:X} <{OtherList:X6}> ");
         Assert.Single(Model.GetNextRun(List).PointerSources);
         Assert.False(OverworldSlotImport.Find(Model, RowA + SpritesOffset).SharesPictures);
         dialogs.Clear();

         ImportInto(RowA, FileSystem(1));

         Assert.Single(dialogs);
         Assert.Contains("New palette", dialogOptions[0]);
         Assert.Equal(List, Pointer(RowA)); // nothing else shares the pictures: they stay where they are
         Assert.Equal(OverworldSlotImport.FirstNewTag, PaletteId(RowA));
         Assert.Equal(0x1100, PaletteId(RowB));
         Assert.Equal(3 + EndRows, PaletteTable.ElementCount);
      }
   }

   /// <summary>The same slots, but with the palette table the vanilla games have: it ends with eight zero bytes instead of a counted end-marker row.</summary>
   public class OverworldNewPaletteEndCodeTableTests : OverworldNewPaletteTests {
      public OverworldNewPaletteEndCodeTableTests() : base(true) { }
   }
}
