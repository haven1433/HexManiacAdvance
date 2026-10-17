using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using HexManiac.Core.Models.Runs.Sprites;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// The GBA only looks at 15 bits of a color, but some ROMs keep a flag in bit 15 (a color stored as EE FA is 0xFAEE: bit 15 set).
   /// The palette editor holds 15-bit colors, so it used to write every color back with bit 15 cleared. These tests pin down that
   /// every way of writing a palette color keeps the bit that is stored, and that palettes without the bit are written exactly as before.
   /// </summary>
   public static class Bit15 {
      /// <summary>A color the way the ROM stores it: red in the low bits.</summary>
      public static int Gba(int red, int green, int blue) => red | (green << 5) | (blue << 10);

      /// <summary>A color the way the editors hold it: red in the high bits.</summary>
      public static short Rgb(int red, int green, int blue) => (short)((red << 10) | (green << 5) | blue);

      /// <summary>Sixteen stored colors: slot 0 is EE FA, the other even slots have bit 15 set, the odd slots don't. Slot i is the grey i:i:i.</summary>
      public static int[] Flagged() => Enumerable.Range(0, 16).Select(i => i == 0 ? 0xFAEE : (Gba(i, i, i) | (i % 2 == 0 ? 0x8000 : 0))).ToArray();

      /// <summary>The same sixteen greys with no bit 15 anywhere.</summary>
      public static int[] Plain() => Enumerable.Range(0, 16).Select(i => Gba(i, i, i)).ToArray();

      public static void Store(byte[] data, int address, IReadOnlyList<int> colors) {
         for (int i = 0; i < colors.Count; i++) {
            data[address + i * 2] = (byte)colors[i];
            data[address + i * 2 + 1] = (byte)(colors[i] >> 8);
         }
      }

      public static int[] Read(IDataModel model, int address, int count) => Enumerable.Range(0, count).Select(i => model.ReadMultiByteValue(address + i * 2, 2)).ToArray();

      /// <summary>What the stored colors should be after the colors at the given slots were replaced: the new 15 bits, and the old bit 15.</summary>
      public static int[] WithReplaced(int[] before, params (int slot, int gba)[] changes) {
         var after = (int[])before.Clone();
         foreach (var (slot, gba) in changes) after[slot] = (gba & 0x7FFF) | (before[slot] & 0x8000);
         return after;
      }
   }

   public class PaletteBit15Tests : BaseViewModelTestClass {
      private const int Palette = 0x00, PalettePointer = 0x80, Table = 0x100, Blockset = 0x20, TilesetPalettes = 0x400;

      public PaletteBit15Tests() : base(0x800) {
         Bit15.Store(Data, Palette, Bit15.Flagged());
         ViewPort.Edit($"@{PalettePointer:X2} <{Palette:X2}> @{Palette:X2} ^pal`ucp4`");
      }

      #region Helpers on the palette run

      [Fact]
      public void KeepUnusedColorBit_TakesTheNewColorAndTheOldBit15() {
         Assert.Equal(unchecked((short)0xFAEE), PaletteRun.KeepUnusedColorBit(0xFAEE, 0x7AEE));
         Assert.Equal(unchecked((short)0xFAEE), PaletteRun.KeepUnusedColorBit(0xFFFF, 0x7AEE));
         Assert.Equal(0x7AEE, PaletteRun.KeepUnusedColorBit(0x7AEE, 0x7AEE));
         Assert.Equal(0x7AEE, PaletteRun.KeepUnusedColorBit(0x0000, 0x7AEE));
         Assert.Equal(0x0421, PaletteRun.KeepUnusedColorBit(0x7FFF, 0x0421));
         Assert.Equal(unchecked((short)0x8421), PaletteRun.KeepUnusedColorBit(0x8000, 0x0421));
      }

      [Fact]
      public void KeepUnusedColorBit_BitsAbove15OfTheNewColorAreIgnored() {
         Assert.Equal(0x0421, PaletteRun.KeepUnusedColorBit(0x0000, 0x10421));
         Assert.Equal(0x0421, PaletteRun.KeepUnusedColorBit(0x0000, -0x7BDF)); // 0xFFFF8421
      }

      [Fact]
      public void StaticSetPalette_KeepsTheBit15ThatIsInTheBuffer() {
         var buffer = new byte[32];
         Bit15.Store(buffer, 0, Bit15.Flagged());

         PaletteRun.SetPalette(buffer, 0, new short[] { Bit15.Rgb(1, 2, 3), Bit15.Rgb(4, 5, 6), Bit15.Rgb(7, 8, 9) });

         var stored = Enumerable.Range(0, 3).Select(i => buffer[i * 2] | (buffer[i * 2 + 1] << 8)).ToArray();
         Assert.Equal(0x8000 | Bit15.Gba(1, 2, 3), stored[0]);
         Assert.Equal(Bit15.Gba(4, 5, 6), stored[1]); // slot 1 had no flag
         Assert.Equal(0x8000 | Bit15.Gba(7, 8, 9), stored[2]);
         Assert.Equal(0x8000 | Bit15.Gba(4, 4, 4), buffer[8] | (buffer[9] << 8)); // slot 4 was not written, and is untouched
      }

      [Fact]
      public void StaticSetPalette_ZeroedBuffer_LeavesBit15Clear() {
         var buffer = new byte[32];

         PaletteRun.SetPalette(buffer, 0, Enumerable.Range(0, 16).Select(i => Bit15.Rgb(i, 31 - i, 15)).ToArray());

         for (int i = 0; i < 16; i++) Assert.Equal(Bit15.Gba(i, 31 - i, 15), buffer[i * 2] | (buffer[i * 2 + 1] << 8));
      }

      [Fact]
      public void StaticSetPalette_ColorsWithTheEditorsBit15Set_DoNotLeakIntoTheStoredBit() {
         var buffer = new byte[4];

         PaletteRun.SetPalette(buffer, 0, new short[] { unchecked((short)0xFFFF), 0 });

         Assert.Equal(0x7FFF, buffer[0] | (buffer[1] << 8));
         Assert.Equal(0, buffer[2] | (buffer[3] << 8));
      }

      #endregion

      #region Palette editor on a plain palette

      [Fact]
      public void PaletteEditor_ChangingOneColor_KeepsBit15OfEveryColor() {
         var before = Bit15.Flagged();
         var colors = ViewPort.Tools.SpriteTool.Colors;

         colors.SelectionStart = 4; // bit 15 is set in this slot
         FileSystem.CopyText = "5:6:7";
         colors.Paste.Execute(FileSystem);

         Assert.Equal(Bit15.WithReplaced(before, (4, Bit15.Gba(5, 6, 7))), Bit15.Read(Model, Palette, 16));
         Assert.Equal(0x8000 | Bit15.Gba(5, 6, 7), Model.ReadMultiByteValue(8, 2));
      }

      [Fact]
      public void PaletteEditor_ChangingAColorWithoutBit15_DoesNotSetIt() {
         var before = Bit15.Flagged();
         var colors = ViewPort.Tools.SpriteTool.Colors;

         colors.SelectionStart = 5; // bit 15 is clear in this slot
         FileSystem.CopyText = "5:6:7";
         colors.Paste.Execute(FileSystem);

         Assert.Equal(Bit15.WithReplaced(before, (5, Bit15.Gba(5, 6, 7))), Bit15.Read(Model, Palette, 16));
         Assert.Equal(Bit15.Gba(5, 6, 7), Model.ReadMultiByteValue(10, 2));
      }

      [Fact]
      public void PaletteEditor_ChangingTheColorThatIsEEFA_KeepsEEFA() {
         var colors = ViewPort.Tools.SpriteTool.Colors;
         Assert.Equal(0xEE, Model[0]);
         Assert.Equal(0xFA, Model[1]);

         // only the RGB of the colors next to it change
         colors.SelectionStart = 1;
         FileSystem.CopyText = "9:9:9";
         colors.Paste.Execute(FileSystem);
         colors.SelectionStart = 2;
         FileSystem.CopyText = "0:0:0";
         colors.Paste.Execute(FileSystem);

         Assert.Equal(0xEE, Model[0]);
         Assert.Equal(0xFA, Model[1]);
      }

      [Fact]
      public void PaletteEditor_ChangingEEFAItself_ChangesOnlyItsRgb() {
         var colors = ViewPort.Tools.SpriteTool.Colors;

         colors.SelectionStart = 0;
         FileSystem.CopyText = "1:2:3"; // r:g:b
         colors.Paste.Execute(FileSystem);

         Assert.Equal(0x8000 | Bit15.Gba(1, 2, 3), Model.ReadMultiByteValue(0, 2));
      }

      [Fact]
      public void PaletteEditor_Gradient_KeepsBit15() {
         var before = Bit15.Flagged();
         var colors = ViewPort.Tools.SpriteTool.Colors;

         colors.SelectionStart = 6;
         FileSystem.CopyText = "31:0:0";
         colors.Paste.Execute(FileSystem);
         colors.SelectionStart = 2;
         colors.SelectionEnd = 6;
         colors.CreateGradient.Execute();

         var after = Bit15.Read(Model, Palette, 16);
         for (int i = 0; i < 16; i++) Assert.Equal(before[i] & 0x8000, after[i] & 0x8000);
         Assert.NotEqual(before[4] & 0x7FFF, after[4] & 0x7FFF); // the gradient did change colors
      }

      [Fact]
      public void PaletteEditor_Reorder_KeepsBit15WhereItIs() {
         var before = Bit15.Flagged();
         var colors = ViewPort.Tools.SpriteTool.Colors;

         colors.SelectionStart = 1;
         colors.HandleMove(1, 2);
         colors.CompleteCurrentInteraction();

         var after = Bit15.Read(Model, Palette, 16);
         for (int i = 0; i < 16; i++) Assert.Equal(before[i] & 0x8000, after[i] & 0x8000); // the flag belongs to the slot
         Assert.Equal(before[0], after[0]);
      }

      [Fact]
      public void PaletteEditor_UndoRestoresTheStoredColors() {
         var before = Bit15.Flagged();
         var colors = ViewPort.Tools.SpriteTool.Colors;
         colors.SelectionStart = 4;
         FileSystem.CopyText = "5:6:7";
         colors.Paste.Execute(FileSystem);
         ViewPort.ChangeHistory.ChangeCompleted();

         ViewPort.Undo.Execute(null);

         Assert.Equal(before, Bit15.Read(Model, Palette, 16));
      }

      #endregion

      #region Palette run

      [Fact]
      public void SetPalette_WithTheColorsThatWereRead_ChangesNoBytes() {
         var run = (PaletteRun)Model.GetNextRun(Palette);
         var token = new ModelDelta();

         run.SetPalette(Model, token, 0, run.GetPalette(Model, 0));

         Assert.False(token.HasAnyChange);
         Assert.Equal(Bit15.Flagged(), Bit15.Read(Model, Palette, 16));
         Assert.Equal(0xEE, Model[0]);
         Assert.Equal(0xFA, Model[1]);
      }

      [Fact]
      public void SetPalette_ChangingOnlyRgb_KeepsEveryOtherByte() {
         var run = (PaletteRun)Model.GetNextRun(Palette);
         var colors = run.GetPalette(Model, 0).ToArray();
         colors[0] = Bit15.Rgb(3, 3, 3);
         colors[7] = Bit15.Rgb(30, 20, 10);
         colors[8] = Bit15.Rgb(10, 20, 30);

         run.SetPalette(Model, new ModelDelta(), 0, colors);

         var expected = Bit15.WithReplaced(Bit15.Flagged(), (0, Bit15.Gba(3, 3, 3)), (7, Bit15.Gba(30, 20, 10)), (8, Bit15.Gba(10, 20, 30)));
         Assert.Equal(expected, Bit15.Read(Model, Palette, 16));
         Assert.Equal(0x8000 | Bit15.Gba(3, 3, 3), Model.ReadMultiByteValue(0, 2));
         Assert.Equal(Bit15.Gba(30, 20, 10), Model.ReadMultiByteValue(14, 2)); // 7 had no flag
         Assert.Equal(0x8000 | Bit15.Gba(10, 20, 30), Model.ReadMultiByteValue(16, 2)); // 8 did
      }

      [Fact]
      public void SetPalette_WithNoBit15Stored_WritesExactlyWhatItAlwaysDid() {
         var plain = new byte[0x800];
         var model = new PokemonModel(plain, singletons: Singletons);
         Bit15.Store(plain, 0x40, Bit15.Plain());
         var run = new PaletteRun(0x40, new PaletteFormat(4, 1));
         var colors = Enumerable.Range(0, 16).Select(i => Bit15.Rgb(i * 2, 31 - i, (i * 7) % 32)).ToArray();

         run.SetPalette(model, new ModelDelta(), 0, colors);

         // the old code: every color flipped to the stored order, high bit clear
         for (int i = 0; i < 16; i++) Assert.Equal(Bit15.Gba(i * 2, 31 - i, (i * 7) % 32), model.ReadMultiByteValue(0x40 + i * 2, 2));
      }

      [Fact]
      public void SetPalette_TwoPages_EachPageKeepsItsOwnBit15() {
         var pages = new[] { Bit15.Flagged(), Bit15.Flagged().Select(c => c ^ 0x8000).ToArray() };
         Bit15.Store(Data, 0x200, pages[0]);
         Bit15.Store(Data, 0x220, pages[1]);
         var run = new PaletteRun(0x200, new PaletteFormat(4, 2));
         Model.ObserveRunWritten(Token, run);

         run.SetPalette(Model, Token, 1, Enumerable.Range(0, 16).Select(i => Bit15.Rgb(1, 1, i)).ToArray());

         Assert.Equal(pages[0], Bit15.Read(Model, 0x200, 16));
         var second = Bit15.Read(Model, 0x220, 16);
         for (int i = 0; i < 16; i++) Assert.Equal(pages[1][i] & 0x8000, second[i] & 0x8000);
         for (int i = 0; i < 16; i++) Assert.Equal(Bit15.Gba(1, 1, i), second[i] & 0x7FFF);
      }

      [Fact]
      public void WriteColor_KeepsBit15AndReportsChanges() {
         var token = new ModelDelta();

         var changed = PaletteRun.WriteColor(Model, token, 0, Bit15.Rgb(1, 2, 3));
         var changedAgain = PaletteRun.WriteColor(Model, token, 0, Bit15.Rgb(1, 2, 3));

         Assert.True(changed);
         Assert.False(changedAgain);
         Assert.Equal(0x8000 | Bit15.Gba(1, 2, 3), Model.ReadMultiByteValue(0, 2));
      }

      #endregion

      #region Compressed palettes

      [Fact]
      public void CompressedPalette_ChangingOneColor_KeepsBit15OfEveryColor() {
         const int start = 0x40, pointer = 0x84;
         var before = Bit15.Flagged();
         var raw = new byte[32];
         Bit15.Store(raw, 0, before);
         var compressed = LZRun.Compress(raw, 0, raw.Length);
         for (int i = 0; i < compressed.Count; i++) Data[start + i] = compressed[i];
         Model.WritePointer(Token, pointer, start);
         var run = new LzPaletteRun(new PaletteFormat(4, 1), Model, start, new SortedSpan<int>(pointer));
         Model.ObserveAnchorWritten(Token, "compressed", run);
         var colors = run.GetPalette(Model, 0).ToArray();
         colors[0] = Bit15.Rgb(2, 2, 2);
         colors[3] = Bit15.Rgb(31, 0, 31);
         colors[4] = Bit15.Rgb(0, 31, 0);

         var newRun = run.SetPalette(Model, Token, 0, colors);

         var after = LZRun.Decompress(Model, newRun.Start);
         var stored = Enumerable.Range(0, 16).Select(i => after[i * 2] | (after[i * 2 + 1] << 8)).ToArray();
         Assert.Equal(Bit15.WithReplaced(before, (0, Bit15.Gba(2, 2, 2)), (3, Bit15.Gba(31, 0, 31)), (4, Bit15.Gba(0, 31, 0))), stored);
      }

      [Fact]
      public void CompressedPalette_WithoutBit15_StoresPlainColors() {
         const int start = 0x40, pointer = 0x84;
         var raw = new byte[32];
         Bit15.Store(raw, 0, Bit15.Plain());
         var compressed = LZRun.Compress(raw, 0, raw.Length);
         for (int i = 0; i < compressed.Count; i++) Data[start + i] = compressed[i];
         Model.WritePointer(Token, pointer, start);
         var run = new LzPaletteRun(new PaletteFormat(4, 1), Model, start, new SortedSpan<int>(pointer));
         Model.ObserveAnchorWritten(Token, "compressed", run);

         var newRun = run.SetPalette(Model, Token, 0, Enumerable.Range(0, 16).Select(i => Bit15.Rgb(31 - i, i, 5)).ToArray());

         var after = LZRun.Decompress(Model, newRun.Start);
         for (int i = 0; i < 16; i++) Assert.Equal(Bit15.Gba(31 - i, i, 5), after[i * 2] | (after[i * 2 + 1] << 8));
      }

      #endregion

      #region Typing a color in the hex view

      [Fact]
      public void TypedRgb_KeepsBit15() {
         ViewPort.Edit("@00 1:2:3 4:5:6 ");

         Assert.Equal(0x8000 | Bit15.Gba(1, 2, 3), Model.ReadMultiByteValue(0, 2)); // slot 0 was EE FA
         Assert.Equal(Bit15.Gba(4, 5, 6), Model.ReadMultiByteValue(2, 2)); // slot 1 had no flag
      }

      [Fact]
      public void TypedHexDigits_WriteExactlyWhatWasTyped_SoTheBitCanStillBeChosen() {
         ViewPort.Edit("@00 7AEE @02 FAEE ");

         Assert.Equal(0x7AEE, Model.ReadMultiByteValue(0, 2)); // typed without the flag: the flag goes away, the user asked for it
         Assert.Equal(0xFAEE, Model.ReadMultiByteValue(2, 2)); // typed with the flag: it is set
      }

      [Fact]
      public void TypedBytes_WriteExactlyWhatWasTyped() {
         ViewPort.Edit("@02 EE 7A ");

         Assert.Equal(0x7AEE, Model.ReadMultiByteValue(2, 2));
      }

      #endregion

      #region Colors in a table

      private void CreateColorTable(params int[] stored) {
         ViewPort.Edit($"@{Table:X} ^colors[main:|c other:|c]{stored.Length / 2} ");
         for (int i = 0; i < stored.Length; i++) Model.WriteMultiByteValue(Table + i * 2, 2, Token, stored[i]);
      }

      [Fact]
      public void TableColorField_TypedColor_KeepsBit15() {
         CreateColorTable(0xFAEE, 0x0421, 0x8000, 0x7FFF);
         var field = new ColorFieldArrayElementViewModel(ViewPort, "main", Table);
         var plainField = new ColorFieldArrayElementViewModel(ViewPort, "other", Table + 2);

         field.Content = "31:0:4";
         plainField.Content = "1:2:3";

         Assert.Equal(0x8000 | Bit15.Gba(31, 0, 4), Model.ReadMultiByteValue(Table, 2));
         Assert.Equal(Bit15.Gba(1, 2, 3), Model.ReadMultiByteValue(Table + 2, 2));
      }

      [Fact]
      public void TableColorField_PickedColor_KeepsBit15() {
         CreateColorTable(0xFAEE, 0x0421);
         var field = new ColorFieldArrayElementViewModel(ViewPort, "main", Table);

         field.Color = Bit15.Rgb(2, 20, 30);

         Assert.Equal(0x8000 | Bit15.Gba(2, 20, 30), Model.ReadMultiByteValue(Table, 2));
      }

      [Fact]
      public void TableColorField_JustShowingIt_ChangesNothing() {
         CreateColorTable(0xFAEE, 0x0421);
         var before = Bit15.Read(Model, Table, 2);

         var field = new ColorFieldArrayElementViewModel(ViewPort, "main", Table);
         ViewPort.Goto.Execute(Table.ToString("X"));
         ViewPort.Tools.TableTool.DataForCurrentRunChanged();

         Assert.Equal("14:23:30", field.Content); // the flag is not part of what the field shows
         Assert.Equal(before, Bit15.Read(Model, Table, 2));
      }

      #endregion

      #region Tileset palettes

      [Fact]
      public void BlocksetPalettes_WritingThemBack_KeepsBit15() {
         Model.WritePointer(Token, Blockset + 8, TilesetPalettes);
         var stored = new List<int>();
         for (int i = 0; i < 256; i++) stored.Add(((i * 0x0101) & 0x7FFF) | (i % 3 == 0 ? 0x8000 : 0));
         Bit15.Store(Data, TilesetPalettes, stored);
         var blockset = new BlocksetModel(Model, Blockset);
         var palettes = blockset.ReadPalettes();
         palettes[2][5] = Bit15.Rgb(1, 2, 3);
         palettes[15][15] = Bit15.Rgb(31, 30, 29);

         blockset.WritePalettes(palettes, Token);

         var after = Bit15.Read(Model, TilesetPalettes, 256);
         for (int i = 0; i < 256; i++) Assert.Equal(stored[i] & 0x8000, after[i] & 0x8000);
         for (int i = 0; i < 256; i++) {
            if (i == 2 * 16 + 5) Assert.Equal(Bit15.Gba(1, 2, 3), after[i] & 0x7FFF);
            else if (i == 15 * 16 + 15) Assert.Equal(Bit15.Gba(31, 30, 29), after[i] & 0x7FFF);
            else Assert.Equal(stored[i], after[i]);
         }
      }

      #endregion
   }

   public class PaletteBit15ImageEditorTests : BaseImageEditorTests {
      private void StoreFlaggedPalette() {
         Bit15.Store(model.RawData, PaletteStart, Bit15.Flagged());
         editor.Refresh();
      }

      [Fact]
      public void ImageEditor_ChangingAColor_KeepsBit15OfEveryColor() {
         StoreFlaggedPalette();

         editor.Palette.Elements[3].Color = Rgb(5, 6, 7); // slot 3 had no flag
         editor.Palette.Elements[4].Color = Rgb(7, 6, 5); // slot 4 had it

         var expected = Bit15.WithReplaced(Bit15.Flagged(), (3, Bit15.Gba(5, 6, 7)), (4, Bit15.Gba(7, 6, 5)));
         Assert.Equal(expected, Bit15.Read(model, PaletteStart, 16));
         Assert.Equal(0xEE, model[PaletteStart]);
         Assert.Equal(0xFA, model[PaletteStart + 1]);
      }

      [Fact]
      public void ImageEditor_DrawingWithFlaggedColors_DoesNotTouchTheFlags() {
         StoreFlaggedPalette();

         DrawBox(1, new Point(0, 0), 3, 3);

         var after = Bit15.Read(model, PaletteStart, 16);
         for (int i = 0; i < 16; i++) Assert.Equal(Bit15.Flagged()[i], after[i]);
      }

      [Fact]
      public void ImageEditor_PaletteWithoutFlags_StaysWithoutFlags() {
         Bit15.Store(model.RawData, PaletteStart, Bit15.Plain());
         editor.Refresh();

         editor.Palette.Elements[2].Color = White;
         editor.Palette.Elements[9].Color = Red;

         var after = Bit15.Read(model, PaletteStart, 16);
         for (int i = 0; i < 16; i++) Assert.Equal(0, after[i] & 0x8000);
         Assert.Equal(0x7FFF, after[2]);
         Assert.Equal(Bit15.Gba(31, 0, 0), after[9]);
      }
   }
}
