using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class ImagePasteMapperTests {
      private static short Rgb(int r, int g, int b) => (short)((r << 10) | (g << 5) | b);
      private static readonly short Black = Rgb(0, 0, 0), Red = Rgb(31, 0, 0), Green = Rgb(0, 31, 0), Blue = Rgb(0, 0, 31), White = Rgb(31, 31, 31);

      private static ImagePasteResult Map(short[] colors, int width, ImageClip source, params short[] palette) => ImagePasteMapper.Map(colors, width, source, (x, y) => palette);

      [Fact]
      public void ExactColors_NoSource_FindsThePaletteIndexes() {
         var result = Map(new[] { Red, Blue, Green, White }, 2, null, Black, Red, Green, Blue, White);

         Assert.Equal(new[] { 1, 3, 2, 4 }, new[] { result.Indexes[0, 0], result.Indexes[1, 0], result.Indexes[0, 1], result.Indexes[1, 1] });
         Assert.Equal(0, result.RemappedColors);
         Assert.Equal(4, result.DirectPixels);
      }

      [Fact]
      public void ColorMissingFromThePalette_UsesClosestColor_AndCountsIt() {
         var almostRed = Rgb(28, 2, 1);

         var result = Map(new[] { almostRed, almostRed, Blue }, 3, null, Black, Green, Red, Blue);

         Assert.Equal(2, result.Indexes[0, 0]);
         Assert.Equal(2, result.Indexes[1, 0]);
         Assert.Equal(3, result.Indexes[2, 0]);
         Assert.Equal(1, result.RemappedColors);
         Assert.Equal(2, result.RemappedPixels);
         Assert.Contains("1 color was remapped", result.Describe());
      }

      [Fact]
      public void ClosestColor_NeverTransparentIndex() {
         var result = Map(new[] { Rgb(1, 1, 1) }, 1, null, Black, White, Rgb(20, 20, 20));

         Assert.NotEqual(0, result.Indexes[0, 0]);
         Assert.Equal(1, result.RemappedColors);
      }

      [Fact]
      public void SameColorAsTransparentColor_AndAlsoAsOtherIndex_PrefersTheOtherIndex() {
         var result = Map(new[] { Black }, 1, null, Black, Red, Black);

         Assert.Equal(2, result.Indexes[0, 0]);
      }

      [Fact]
      public void SourceIndexKnown_TransparentStaysTransparent_EvenIfTheColorIsDifferent() {
         var palette = new short[] { Rgb(1, 1, 1), Red };
         var source = new ImageClip(new[] { Rgb(1, 1, 1), Red }, 2, new[] { 0, 1 }, new IReadOnlyList<short>[] { palette, palette });

         var result = Map(new[] { Rgb(1, 1, 1), Red }, 2, source, Black, Green, Red);

         Assert.Equal(0, result.Indexes[0, 0]);
         Assert.Equal(2, result.Indexes[1, 0]); // red lives at 2 in the target
         Assert.Equal(1, result.TransparentPixels);
         Assert.Equal(0, result.RemappedColors);
         Assert.Contains("colors were matched", result.Describe());
      }

      [Fact]
      public void IdenticalPalettes_PasteIndexForIndex_EvenWithDuplicateColors() {
         var palette = new short[] { Black, Red, Red, Blue };
         var source = new ImageClip(new[] { Red, Red }, 2, new[] { 1, 2 }, new IReadOnlyList<short>[] { palette, palette });

         var result = Map(new[] { Red, Red }, 2, source, palette);

         Assert.Equal(1, result.Indexes[0, 0]);
         Assert.Equal(2, result.Indexes[1, 0]);
         Assert.Contains("palette matched", result.Describe());
      }

      [Fact]
      public void ClipMatches_ChecksColorsAndWidth() {
         var clip = new ImageClip(new[] { Red, Blue }, 2, new[] { 1, 2 }, null);

         Assert.True(clip.Matches(new[] { Red, Blue }, 2));
         Assert.False(clip.Matches(new[] { Red, Green }, 2));
         Assert.False(clip.Matches(new[] { Red, Blue }, 1));
         Assert.False(clip.Matches(null, 2));
      }
   }

   public class ImagePasteEditorTests {
      private static short Rgb(int r, int g, int b) => (short)((r << 10) | (g << 5) | b);
      private static readonly short Black = Rgb(0, 0, 0), Red = Rgb(31, 0, 0), Green = Rgb(0, 31, 0), Blue = Rgb(0, 0, 31), White = Rgb(31, 31, 31);

      /// <summary>One image editor tab on its own (tiny) rom: a 16x16 picture of 4 tiles with a 16-color palette.</summary>
      private class Tab {
         public const int SpriteStart = 0x000, PaletteStart = 0x100, SpritePointer = 0x300, PalettePointer = 0x308;
         public readonly IDataModel Model = new PokemonModel(new byte[0x400], singletons: BaseViewModelTestClass.Singletons);
         public readonly ChangeHistory<ModelDelta> History;
         public readonly ImageEditorViewModel Editor;
         public readonly List<string> Messages = new List<string>();
         public readonly List<string> Errors = new List<string>();

         public Tab(params short[] colors) {
            History = new ChangeHistory<ModelDelta>(change => change.Revert(Model));
            Model.WritePointer(History.CurrentChange, SpritePointer, SpriteStart);
            Model.WritePointer(History.CurrentChange, PalettePointer, PaletteStart);
            Model.ObserveAnchorWritten(History.CurrentChange, "sprite", new SpriteRun(Model, SpriteStart, new SpriteFormat(4, 2, 2, "palette"), new SortedSpan<int>(SpritePointer)));
            Model.ObserveAnchorWritten(History.CurrentChange, "palette", new PaletteRun(PaletteStart, new PaletteFormat(4, 1), new SortedSpan<int>(PalettePointer)));
            Editor = new ImageEditorViewModel(History, Model, SpriteStart) { SpriteScale = 1 };
            for (int i = 0; i < colors.Length; i++) Editor.Palette.Elements[i].Color = colors[i];
            Editor.OnMessage += (sender, message) => Messages.Add(message);
            Editor.OnError += (sender, message) => Errors.Add(message);
            History.ChangeCompleted();
         }

         public Point Screen(int x, int y) => Editor.FromSpriteSpace(new Point(x, y));
         public void Draw(int index, int x, int y) {
            Editor.SelectedTool = ImageEditorTools.Draw;
            Editor.Palette.SelectionStart = index;
            Editor.ToolDown(Screen(x, y));
            Editor.ToolUp(Screen(x, y));
         }
         public void Select(int x0, int y0, int x1, int y1) {
            Editor.SelectedTool = ImageEditorTools.Select;
            Editor.ToolDown(Screen(x0, y0));
            Editor.Hover(Screen(x1, y1));
            Editor.ToolUp(Screen(x1, y1));
         }
         public short Color(int x, int y) => Editor.PixelData[Editor.PixelIndex(x, y)];
         public int Raw(int x, int y) => Editor.ReadRawPixel(x, y);
         public string Dump() {
            var text = new System.Text.StringBuilder();
            for (int y = 0; y < 16; y++) { for (int x = 0; x < 16; x++) text.Append(Raw(x, y).ToString("X")); text.Append('|'); }
            return text.ToString();
         }
      }

      private static readonly short[] PaletteA = { Black, Red, Blue, White, Green };

      [Fact]
      public void SamePalette_TwoTabs_IndexesPasteDirectly() {
         var fs = new StubFileSystem();
         var source = new Tab(PaletteA);
         var target = new Tab(PaletteA);
         source.Draw(4, 0, 0); source.Draw(1, 1, 0); source.Draw(2, 0, 1); source.Draw(3, 1, 1);
         source.Select(0, 0, 7, 7);
         source.Editor.Copy.Execute(fs);

         target.Editor.Paste.Execute(fs);

         // 8x8 is whole tiles: it snaps to the tile grid (centered on 8,8 means tile (0,0) or (1,1); either way tile aligned)
         var (x, y) = FindPaste(target);
         Assert.Equal(0, x % 8); Assert.Equal(0, y % 8);
         Assert.Equal(4, target.Raw(x, y)); Assert.Equal(1, target.Raw(x + 1, y)); Assert.Equal(2, target.Raw(x, y + 1)); Assert.Equal(3, target.Raw(x + 1, y + 1));
         Assert.Contains("palette matched", target.Messages.Last());
         Assert.Empty(target.Errors);
      }

      private static (int x, int y) FindPaste(Tab tab) {
         for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) if (tab.Raw(x, y) == 4) return (x, y);
         return (-1, -1);
      }

      [Fact]
      public void DifferentIndexesSameColors_TwoTabs_ColorsAreKept() {
         var fs = new StubFileSystem();
         var source = new Tab(PaletteA);
         var target = new Tab(Black, Green, White, Blue, Red); // same colors, other order
         source.Draw(1, 0, 0); source.Draw(2, 1, 0); source.Draw(3, 0, 1); source.Draw(4, 1, 1);
         source.Select(0, 0, 7, 7);
         source.Editor.Copy.Execute(fs);

         target.Editor.Paste.Execute(fs);

         var (x, y) = FindPasteColor(target, Red);
         Assert.Equal(Red, target.Color(x, y)); Assert.Equal(Blue, target.Color(x + 1, y)); Assert.Equal(White, target.Color(x, y + 1)); Assert.Equal(Green, target.Color(x + 1, y + 1));
         Assert.Equal(4, target.Raw(x, y)); Assert.Equal(3, target.Raw(x + 1, y)); Assert.Equal(2, target.Raw(x, y + 1)); Assert.Equal(1, target.Raw(x + 1, y + 1));
         Assert.Contains("matched", target.Messages.Last());
         Assert.DoesNotContain("remapped", target.Messages.Last());
      }

      private static (int x, int y) FindPasteColor(Tab tab, short color) {
         for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) if (tab.Color(x, y) == color) return (x, y);
         return (-1, -1);
      }

      [Fact]
      public void DifferentPalettes_TwoTabs_NearestColorAndMessage() {
         var fs = new StubFileSystem();
         var source = new Tab(PaletteA);
         var target = new Tab(Black, Rgb(30, 1, 0), Rgb(1, 0, 30), Green); // no pure red, no pure blue, no white
         source.Draw(1, 0, 0); source.Draw(2, 1, 0); source.Draw(4, 0, 1);
         source.Select(0, 0, 7, 7);
         source.Editor.Copy.Execute(fs);

         target.Editor.Paste.Execute(fs);

         var (x, y) = FindPasteColor(target, Rgb(30, 1, 0));
         Assert.Equal(1, target.Raw(x, y));
         Assert.Equal(2, target.Raw(x + 1, y));
         Assert.Equal(3, target.Raw(x, y + 1));
         Assert.Contains("remapped", target.Messages.Last());
         Assert.Contains("Pasted 8x8 px", target.Messages.Last());
      }

      [Fact]
      public void TransparentPixels_PasteLeavesWhatIsUnderneath() {
         var fs = new StubFileSystem();
         var source = new Tab(Rgb(1, 1, 1), Red, Blue);
         var target = new Tab(Black, Red, Blue, Green);
         source.Draw(1, 2, 2); // all other pixels are transparent index 0
         source.Select(0, 0, 7, 7);
         source.Editor.Copy.Execute(fs);
         target.Draw(3, 0, 0); target.Draw(3, 8, 8); target.Draw(3, 9, 9); target.Draw(3, 10, 10);

         target.Editor.Paste.Execute(fs);

         var count = 0;
         for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) if (target.Raw(x, y) == 1) count++;
         Assert.Equal(1, count);
         Assert.Equal(3, target.Raw(0, 0)); // the untouched green is still there
         Assert.DoesNotContain("remapped", target.Messages.Last());
      }

      [Fact]
      public void Paste_CanUndo() {
         var fs = new StubFileSystem();
         var source = new Tab(PaletteA);
         var target = new Tab(PaletteA);
         source.Draw(1, 0, 0); source.Draw(2, 1, 1); source.Draw(3, 7, 7);
         source.Select(0, 0, 7, 7);
         source.Editor.Copy.Execute(fs);
         var before = (byte[])target.Model.RawData.Clone();

         target.Editor.Paste.Execute(fs);
         target.History.ChangeCompleted();
         Assert.NotEqual(before, target.Model.RawData);
         target.Editor.Undo.Execute();

         Assert.Equal(before, target.Model.RawData);
      }

      [Fact]
      public void SelectionInTarget_PasteStartsAtTopLeftOfSelection() {
         var fs = new StubFileSystem();
         var source = new Tab(PaletteA);
         var target = new Tab(PaletteA);
         source.Draw(1, 0, 0); source.Draw(2, 7, 0);
         source.Select(0, 0, 7, 7);
         source.Editor.Copy.Execute(fs);
         target.Select(8, 8, 9, 9);

         target.Editor.Paste.Execute(fs);

         Assert.Equal(1, target.Raw(8, 8));
         Assert.Equal(2, target.Raw(15, 8));
      }

      [Fact]
      public void SmallPasteInSameTab_WithSourceSelectionStillThere_PastesAtCursorNotOnTopOfItself() {
         var fs = new StubFileSystem();
         var tab = new Tab(PaletteA);
         tab.Draw(1, 2, 2); tab.Draw(2, 3, 2);
         tab.Select(2, 2, 3, 3);
         tab.Editor.Copy.Execute(fs);
         tab.Editor.Hover(tab.Screen(12, 12)); // the selection is still there, but the cursor is somewhere else

         tab.Editor.Paste.Execute(fs);

         int count = 0;
         for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) if (tab.Raw(x, y) == 1) count++;
         Assert.Equal(2, count); // original and the pasted copy
      }

      [Fact]
      public void ClipboardWithoutPixels_ReportsAnErrorInsteadOfCrashing() {
         var fs = new StubFileSystem();
         fs.CopyImage = (null, 4);
         var target = new Tab(PaletteA);

         target.Editor.Paste.Execute(fs);

         Assert.Single(target.Errors);
      }

      [Fact]
      public void PictureFromOtherProgram_NoRememberedCopy_StillPastesByColor() {
         var fs = new StubFileSystem();
         var target = new Tab(PaletteA);
         fs.CopyImage = (new short[] { Blue, Green, Red, White }, 2);

         target.Editor.Paste.Execute(fs);

         var (x, y) = FindPasteColor(target, Blue);
         Assert.True(x >= 0, target.Dump());
         Assert.Equal(2, target.Raw(x, y)); Assert.Equal(4, target.Raw(x + 1, y)); Assert.Equal(1, target.Raw(x, y + 1)); Assert.Equal(3, target.Raw(x + 1, y + 1));
      }

      [Fact]
      public void CopyWithoutSelection_SaysWhatToDo_AndLeavesTheClipboardAlone() {
         var fs = new StubFileSystem();
         fs.CopyImage = (new short[] { Red }, 1);
         var tab = new Tab(PaletteA);

         tab.Editor.Copy.Execute(fs);

         Assert.Contains("Select tool", tab.Messages.Last());
         Assert.Equal(new short[] { Red }, fs.CopyImage.value.Item1);
      }

      [Fact]
      public void TooLarge_SaysHowLarge() {
         var fs = new StubFileSystem();
         var target = new Tab(PaletteA);
         fs.CopyImage = (new short[32 * 32], 32);

         target.Editor.Paste.Execute(fs);

         Assert.Contains("32x32", target.Messages.Last());
      }
   }

   /// <summary>Tilesets show one 16-color page of a bigger palette: tiles copied from page 2 of one tileset must land on page 1 of another with the same colors.</summary>
   public class ImagePasteTilesetTests {
      private static short Rgb(int r, int g, int b) => (short)((r << 10) | (g << 5) | b);
      private static readonly short Red = Rgb(31, 0, 0), Green = Rgb(0, 31, 0), Blue = Rgb(0, 0, 31), White = Rgb(31, 31, 31);

      private class TilesetTab {
         public readonly IDataModel Model = new PokemonModel(new byte[0x4000], singletons: BaseViewModelTestClass.Singletons);
         public readonly ChangeHistory<ModelDelta> History;
         public readonly ImageEditorViewModel Editor;
         public readonly List<string> Messages = new List<string>();

         private void Insert(int start, int length) {
            var compressed = LZRun.Compress(new byte[length], 0, length);
            for (int i = 0; i < compressed.Count; i++) Model[start + i] = compressed[i];
         }

         public TilesetTab(int page) {
            History = new ChangeHistory<ModelDelta>(change => change.Revert(Model));
            Model.WritePointer(History.CurrentChange, 0x3F00, 0x100); // palette
            Model.WritePointer(History.CurrentChange, 0x3F04, 0x400); // tileset
            Insert(0x100, 32 * 3);
            Insert(0x400, 32 * 16);
            PokemonModel.ApplyAnchor(Model, History.CurrentChange, 0x100, "^pal`lzp4:3`");
            PokemonModel.ApplyAnchor(Model, History.CurrentChange, 0x400, "^tiles`lzt4|pal`");
            Editor = new ImageEditorViewModel(History, Model, 0x400) { SpriteScale = 1 };
            Editor.OnMessage += (sender, message) => Messages.Add(message);
            Editor.PalettePage = page;
            History.ChangeCompleted();
         }

         public Point Screen(int x, int y) => Editor.FromSpriteSpace(new Point(x, y));
         public void Draw(int index, int x, int y) {
            Editor.SelectedTool = ImageEditorTools.Draw;
            Editor.Palette.SelectionStart = index;
            Editor.ToolDown(Screen(x, y));
            Editor.ToolUp(Screen(x, y));
         }
         public void Select(int x0, int y0, int x1, int y1) {
            Editor.SelectedTool = ImageEditorTools.Select;
            Editor.ToolDown(Screen(x0, y0));
            Editor.Hover(Screen(x1, y1));
            Editor.ToolUp(Screen(x1, y1));
         }
      }

      [Fact]
      public void TileCopiedFromAnotherPaletteSlot_PastesWithTheSameColorsOnTheShownPage() {
         var fs = new StubFileSystem();
         var source = new TilesetTab(page: 2);
         var target = new TilesetTab(page: 1);
         foreach (var tab in new[] { source, target }) {
            tab.Editor.Palette.Elements[1].Color = Red;
            tab.Editor.Palette.Elements[2].Color = Green;
            tab.Editor.Palette.Elements[3].Color = Blue;
         }
         source.Draw(1, 0, 0); source.Draw(2, 1, 0); source.Draw(3, 0, 1);
         source.Select(0, 0, 7, 7);
         source.Editor.Copy.Execute(fs);

         target.Editor.Paste.Execute(fs);

         var found = Enumerable.Range(0, target.Editor.PixelWidth * target.Editor.PixelHeight).Where(i => target.Editor.PixelData[i] == Red).ToList();
         Assert.Single(found);
         var (x, y) = (found[0] % target.Editor.PixelWidth, found[0] / target.Editor.PixelWidth);
         Assert.Equal(1, target.Editor.ReadRawPixel(x, y));
         Assert.Equal(Green, target.Editor.PixelData[target.Editor.PixelIndex(x + 1, y)]);
         Assert.Equal(2, target.Editor.ReadRawPixel(x + 1, y));
         Assert.Equal(Blue, target.Editor.PixelData[target.Editor.PixelIndex(x, y + 1)]);
         Assert.Equal(3, target.Editor.ReadRawPixel(x, y + 1));
         Assert.Equal(0, x % 8); Assert.Equal(0, y % 8);
         Assert.DoesNotContain("remapped", target.Messages.Last());
      }
   }
}
