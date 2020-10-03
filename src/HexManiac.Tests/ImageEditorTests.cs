using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels;
using System;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class ImageEditorTests {
      private readonly IDataModel model;
      private readonly ChangeHistory<ModelDelta> history;
      private readonly ImageEditorViewModel editor;

      private Func<ModelDelta, ModelDelta> Revert { get; set; }

      private ModelDelta RevertHistoryChange(ModelDelta change) {
         return Revert?.Invoke(change) ?? change.Revert(model);
      }

      private void DrawBox(int colorIndex, Point start, int width, int height) {
         editor.Palette.SelectionStart = colorIndex;

         editor.ToolDown(start);
         for (int x = 1; x < width; x++) editor.Hover(start = new Point(start.X + 1, start.Y));
         for (int y = 1; y < height; y++) editor.Hover(start = new Point(start.X, start.Y + 1));
         for (int x = 1; x < width; x++) editor.Hover(start = new Point(start.X - 1, start.Y));
         for (int y = 1; y < height; y++) editor.Hover(start = new Point(start.X, start.Y - 1));
         editor.ToolUp(start);
      }

      private void ToolMove(params Point[] motion) {
         editor.ToolDown(motion[0]);
         for (int i = 1; i < motion.Length; i++) editor.Hover(motion[i]);
         editor.ToolUp(motion[motion.Length - 1]);
      }

      private static short Rgb(int r, int g, int b) => (short)((r << 10) | (g << 5) | b);
      private short GetPixel(int x, int y) => editor.PixelData[editor.PixelIndex(new Point(x, y))];
      private static (int r, int g, int b) Rgb(short color) => (color >> 10, (color >> 5) & 31, color & 31);

      private static readonly short Black = Rgb(0, 0, 0);
      private static readonly short White = Rgb(31, 31, 31);
      private static readonly short Red = Rgb(31, 0, 0);
      private static readonly short Blue = Rgb(0, 0, 31);

      private static readonly int SpriteStart = 0x00;
      private static readonly int PaletteStart = 0x40;
      public ImageEditorTests() {
         model = new PokemonModel(new byte[0x200], singletons: BaseViewModelTestClass.Singletons);
         history = new ChangeHistory<ModelDelta>(RevertHistoryChange);

         model.WritePointer(history.CurrentChange, 0x80, SpriteStart);
         model.WritePointer(history.CurrentChange, 0x88, PaletteStart);

         var sprite = new SpriteRun(SpriteStart, new SpriteFormat(4, 1, 1, "palette"), new SortedSpan<int>(0x80));
         model.ObserveAnchorWritten(history.CurrentChange, "sprite", sprite);

         var palette = new PaletteRun(PaletteStart, new PaletteFormat(4, 1), new SortedSpan<int>(0x88));
         model.ObserveAnchorWritten(history.CurrentChange, "palette", palette);

         model[0x20] = 0x23; // random data after the sprite, so expanding it causes a repoint
         model[0x60] = 0x23; // random data after the palette, so expanding it causes a repoint

         editor = new ImageEditorViewModel(history, model, 0);
      }

      private void Insert64CompressedBytes(int start) {
         // header: 10 40 00 00
         // body: 0b00111000 00 00 1F0 1F0 1F0 00 00 00
         //       0x00       00 00 00 00 00
         model.WriteValue(history.CurrentChange, start, 0x4010);
         model[start + 4] = 0b00111000;
         model.WriteMultiByteValue(start + 7, 2, history.CurrentChange, 0x1F0);
         model.WriteMultiByteValue(start + 9, 2, history.CurrentChange, 0x1F0);
         model.WriteMultiByteValue(start + 11, 2, history.CurrentChange, 0x1F0);
      }

      private void Create2PageCompressedSprite() {
         Insert64CompressedBytes(SpriteStart);

         var sprite = new LzSpriteRun(new SpriteFormat(4, 1, 1, "palette"), model, SpriteStart, new SortedSpan<int>(0x80));
         model.ObserveAnchorWritten(history.CurrentChange, "sprite", sprite);

         editor.Refresh();
      }

      private void Create2PageCompressedPalette() {
         Insert64CompressedBytes(PaletteStart);

         var pal = new LzPaletteRun(new PaletteFormat(4, 2), model, PaletteStart, new SortedSpan<int>(0x88));
         model.ObserveAnchorWritten(history.CurrentChange, "palette", pal);

         editor.Refresh();
      }

      [Fact]
      public void Palette_Default_NoColorsSelected() {
         Assert.Empty(editor.Palette.Elements.Where(sc => sc.Selected));
      }

      [Fact]
      public void Palette_ChangeColor_PixelsUpdate() {
         var notifyPixelData = 0;
         editor.Bind(nameof(editor.PixelData), (sender, e) => notifyPixelData += 1);
         editor.Palette.Elements[0].Color = Rgb(1, 1, 1);

         Assert.Equal((1, 1, 1), Rgb(GetPixel(0, 0)));
         Assert.Equal(1, notifyPixelData);
      }

      [Fact]
      public void NewColor_Draw_PixelsChange() {
         var palette = editor.Palette;
         palette.SelectionStart = 1;
         palette.Elements[1].Color = Rgb(31, 31, 31);
         var notifyPixelData = 0;
         editor.Bind(nameof(editor.PixelData), (sender, e) => notifyPixelData += 1);

         ToolMove(new Point());

         Assert.Equal((31, 31, 31), Rgb(GetPixel(4, 4)));
         Assert.Equal(1, ((ISpriteRun)model.GetNextRun(SpriteStart)).GetPixels(model, 0)[4, 4]);
         Assert.Equal(Rgb(31, 31, 31), ((IPaletteRun)model.GetNextRun(PaletteStart)).GetPalette(model, 0)[1]);
         Assert.Equal(1, notifyPixelData);
      }

      [Fact]
      public void Zoom_UpperLeft_StaysPut() {
         editor.ZoomIn(new Point(-4, -4));

         Assert.Equal(2, editor.SpriteScale);
         Assert.Equal(4, editor.XOffset);
         Assert.Equal(4, editor.YOffset);
      }

      [Fact]
      public void Zoom_Zoom16_NoZoom() {
         for (int i = 0; i < 17; i++) editor.ZoomIn(new Point(0, 0));
         Assert.Equal(16, editor.SpriteScale);
      }

      [Fact]
      public void ZoomOut_NoZoom_NoZoom() {
         editor.ZoomOut(new Point(0, 0));
         Assert.Equal(1, editor.SpriteScale);
      }

      [Fact]
      public void ZoomIn_ZoomOut_DefaultZoom() {
         editor.ZoomIn(default);
         editor.ZoomOut(default);
         Assert.Equal(1, editor.SpriteScale);
      }

      [Fact]
      public void Center_Pan2_Offset2() {
         editor.SelectedTool = ImageEditorTools.Pan;

         ToolMove(default, new Point(2, 0));

         Assert.Equal(2, editor.XOffset);
      }

      [Fact]
      public void Zoom_Pan2_Offset2() {
         editor.SelectedTool = ImageEditorTools.Pan;

         editor.ZoomIn(new Point(0, 0));
         ToolMove(default, new Point(2, 0));

         Assert.Equal(2, editor.XOffset);
      }

      [Fact]
      public void Pan2_Zoom_Offset4() {
         editor.SelectedTool = ImageEditorTools.Pan;

         editor.ToolDown(new Point(0, 0));
         editor.Hover(new Point(2, 0));
         editor.ToolUp(new Point(2, 0));
         editor.ZoomIn(new Point(0, 0));

         Assert.Equal(4, editor.XOffset);
      }

      [InlineData(-4, -4)]
      [InlineData(3, 3)]
      [InlineData(-4, 3)]
      [InlineData(3, -4)]
      [Theory]
      public void Zoom_Unzoom_Symmetric(int x, int y) {
         editor.ZoomIn(new Point(x, y));
         editor.ZoomIn(new Point(x, y));
         editor.ZoomOut(new Point(x, y));
         editor.ZoomOut(new Point(x, y));

         Assert.Equal(0, editor.XOffset);
         Assert.Equal(0, editor.YOffset);
      }

      [Fact]
      public void Draw_Drag_Line() {
         editor.Palette.SelectionStart = 1;
         editor.Palette.Elements[1].Color = Rgb(31, 31, 31);
         editor.SelectedTool = ImageEditorTools.Draw;

         ToolMove(default, new Point(1, 0));

         Assert.Equal((31, 31, 31), Rgb(GetPixel(4, 4)));
         Assert.Equal((31, 31, 31), Rgb(GetPixel(5, 4)));
      }

      [Fact]
      public void Fill_Blank_FillAll() {
         editor.Palette.SelectionStart = 1;
         editor.Palette.Elements[1].Color = Rgb(31, 31, 31);
         editor.SelectedTool = ImageEditorTools.Fill;

         ToolMove(new Point());

         Assert.All(64.Range(),
            i => Assert.Equal((31, 31, 31), Rgb(GetPixel(i % 8, i / 8))));
      }

      [Fact]
      public void EyeDropper_SelectColor_ColorSelected() {
         editor.Palette.SelectionStart = 1;

         editor.EyeDropperDown(default);
         editor.EyeDropperUp(default);

         Assert.True(editor.Palette.Elements[0].Selected);
         Assert.False(editor.Palette.Elements[1].Selected);
      }

      [Fact]
      public void EyeDropper_OutOfRange_Noop() {
         editor.EyeDropperDown(new Point(-50, 0));
         editor.EyeDropperUp(new Point(-50, 0));
      }

      [Fact]
      public void BigPan_SmallImage_PanHitsLimit() {
         editor.ToolDown(new Point(0, 0));
         editor.Hover(new Point(50, 0));
         editor.ToolUp(new Point(50, 0));

         Assert.Equal(4, editor.XOffset);
      }

      [Fact]
      public void Editor_Close_InvokesClosed() {
         var count = 0;
         editor.Closed += (sender, e) => count += 1;

         editor.Close.Execute();

         Assert.Equal(1, count);
      }

      [Fact]
      public void Pan_SelectFill_FillSelected() {
         editor.SelectTool.Execute(ImageEditorTools.Fill);

         Assert.Equal(ImageEditorTools.Fill, editor.SelectedTool);
      }

      [Fact]
      public void NoZoom_Zoom_OffsetChangeNotify() {
         int xOffsetNotify = 0, yOffsetNotify = 0;
         editor.Bind(nameof(editor.XOffset), (sender, e) => xOffsetNotify += 1);
         editor.Bind(nameof(editor.YOffset), (sender, e) => yOffsetNotify += 1);

         editor.ZoomIn(new Point(-4, 3));

         Assert.Equal(1, xOffsetNotify);
         Assert.Equal(1, yOffsetNotify);
      }

      [Fact]
      public void Image_Repoint_ToolStillWorks() {
         var destination = model.ReadPointer(editor.SpritePointer);
         var spriteRun = (ISpriteRun)model.GetNextRun(destination);

        spriteRun = model.RelocateForExpansion(history.CurrentChange, spriteRun, spriteRun.Length + 1);

         // if this doesn't throw, we're happy
         editor.SelectedTool = ImageEditorTools.Draw;
         ToolMove(new Point());
      }

      [Fact]
      public void Palette_Repoint_AdressUpdate() {
         var destination = model.ReadPointer(editor.PalettePointer);
         var palRun = (IPaletteRun)model.GetNextRun(destination);

         palRun = model.RelocateForExpansion(history.CurrentChange, palRun, palRun.Length + 1);

         editor.Palette.Elements[0].Color = Rgb(31, 31, 31);
         Assert.Equal(Rgb(31, 31, 31), model.ReadMultiByteValue(palRun.Start, 2));
      }

      [Fact]
      public void Zoom_Draw_ColorChanges() {
         editor.ZoomIn(-4, -4);

         editor.Palette.SelectionStart = 1;
         ToolMove(new Point(-4, -4));

         Assert.Equal(1, model[0]);
      }

      [Fact]
      public void Draw_OutOfBounds_Noop() {
         editor.Palette.SelectionStart = 1;

         ToolMove(new Point(50, 50));

         Assert.All(0x20.Range(), i => Assert.Equal(0, model[0]));
      }

      [Fact]
      public void New_NothingSelected() {
         Assert.All(64.Range(), i => Assert.False(editor.ShowSelectionRect(i % 8, i / 8)));
      }

      [Fact]
      public void DrawTool_Hover_ShowSelectionRect() {
         editor.SelectedTool = ImageEditorTools.Draw;

         editor.Hover(0, 0);

         Assert.True(editor.ShowSelectionRect(4, 4));
      }

      [Fact]
      public void DrawToolAndZoom_Hover_ShowLargeSelectionRect() {
         editor.SelectedTool = ImageEditorTools.Draw;
         editor.ZoomIn(default);

         editor.Hover(0, 0);

         Assert.True(editor.ShowSelectionRect(4, 4));
         Assert.False(editor.ShowSelectionRect(4, 5));
         Assert.False(editor.ShowSelectionRect(4, 3));
         Assert.False(editor.ShowSelectionRect(5, 4));
         Assert.False(editor.ShowSelectionRect(3, 4));
      }

      [Fact]
      public void SelectTool_Drag_ShowSelectionRect() {
         editor.SelectedTool = ImageEditorTools.Select;

         ToolMove(default, new Point(2, 1));

         Assert.True(editor.ShowSelectionRect(4, 4));
         Assert.True(editor.ShowSelectionRect(5, 4));
         Assert.True(editor.ShowSelectionRect(6, 4));
         Assert.True(editor.ShowSelectionRect(4, 5));
         Assert.True(editor.ShowSelectionRect(5, 5));
         Assert.True(editor.ShowSelectionRect(6, 5));
      }

      [Fact]
      public void Select_Drag_MovePixels() {
         editor.Palette.Elements[1].Color = Rgb(31, 31, 31);
         DrawBox(1, new Point(-4, -4), 2, 2);
         editor.SelectedTool = ImageEditorTools.Select;
         ToolMove(new Point(-4, -4), new Point(-3, -3));

         ToolMove(new Point(-3, -3), new Point(-2, -3));

         Assert.Equal(Rgb(31, 31, 31), editor.PixelData[editor.PixelIndex(new Point(2, 1))]);
      }

      [Fact]
      public void SelectDrag_DragBack_OriginalPixelsBack() {
         editor.Palette.Elements[1].Color = Rgb(31, 31, 31);
         DrawBox(1, new Point(-4, -4), 2, 2);
         ToolMove(new Point(-2, -4)); // sentinel pixel
         editor.SelectedTool = ImageEditorTools.Select;
         ToolMove(new Point(-4, -4), new Point(-3, -3));

         // move it back and forth
         ToolMove(new Point(-4, -4), new Point(-3, -4));
         ToolMove(new Point(-3, -4), new Point(-4, -4));

         Assert.Equal(Rgb(31, 31, 31), editor.PixelData[editor.PixelIndex(new Point(2, 0))]);
      }

      [Fact]
      public void Selection_MidInteraction_SelectionRectIsCorrect() {
         editor.SelectedTool = ImageEditorTools.Select;

         editor.ToolDown(default);
         editor.Hover(new Point(-1, 0));

         Assert.False(editor.ShowSelectionRect(2, 4));
         Assert.True(editor.ShowSelectionRect(3, 4));
         Assert.True(editor.ShowSelectionRect(4, 4));
         Assert.False(editor.ShowSelectionRect(5, 4));
      }

      [Fact]
      public void Color1Selected_EyeDropColor0_Color0Selected() {
         editor.Palette.SelectionStart = 1;
         editor.SelectedTool = ImageEditorTools.EyeDropper;

         ToolMove(new Point(0, 0));

         Assert.Equal(0, editor.Palette.SelectionStart);
         Assert.Equal(0, editor.Palette.SelectionEnd);
      }

      [Fact]
      public void EyeDropper_Drag_DrawBlock() {
         editor.Palette.Elements[1].Color = Rgb(31, 31, 31);
         DrawBox(1, new Point(-4, -4), 2, 2);

         editor.EyeDropperDown(-4, -4);
         editor.Hover(-3, -3);
         editor.EyeDropperUp(-3, -3);
         ToolMove(new Point(0, 0));

         Assert.Equal(Rgb(31, 31, 31), editor.PixelData[editor.PixelIndex(4, 4)]);
         Assert.Equal(Rgb(31, 31, 31), editor.PixelData[editor.PixelIndex(4, 5)]);
         Assert.Equal(Rgb(31, 31, 31), editor.PixelData[editor.PixelIndex(5, 4)]);
         Assert.Equal(Rgb(31, 31, 31), editor.PixelData[editor.PixelIndex(5, 5)]);
      }

      [Fact]
      public void ModelChange_Refresh_PixelsChanged() {
         editor.Palette.Elements[15].Color = Rgb(31, 31, 31);
         editor.Palette.PushColorsToModel();
         model[0] = 0xFF;

         editor.Refresh();

         Assert.Equal(Rgb(31, 31, 31), editor.PixelData[0]);
         Assert.Equal(Rgb(31, 31, 31), editor.PixelData[1]);
      }

      [Fact]
      public void DrawTool_Hover_RaiseRefreshSelection() {
         editor.SelectedTool = ImageEditorTools.Draw;
         var refreshCount = 0;
         editor.RefreshSelection += (sender, e) => refreshCount += 1;

         editor.Hover(default);

         Assert.Equal(1, refreshCount);
      }

      [Fact]
      public void Zoom_OddPixel_RoundLeft() {
         editor.Palette.Elements[1].Color = Rgb(31, 31, 31);
         editor.Palette.SelectionStart = 1;
         editor.ZoomIn(default);
         editor.ZoomIn(default);

         ToolMove(new Point(-1, -1));

         Assert.Equal(Rgb(31, 31, 31), editor.PixelData[editor.PixelIndex(3, 3)]);
      }

      [Fact]
      public void DrawToolSelected_PanMethod_Pan() {
         editor.SelectedTool = ImageEditorTools.Draw;

         editor.PanDown(default);
         editor.Hover(2, 0);
         editor.PanUp(2, 0);

         Assert.Equal(2, editor.XOffset);
      }

      [Fact]
      public void FillTool_SelectColor_FilToolStillSelected() {
         editor.SelectedTool = ImageEditorTools.Fill;

         editor.Palette.SelectionStart = 1;

         Assert.Equal(ImageEditorTools.Fill, editor.SelectedTool);
      }

      [Fact]
      public void FillTool_Hover_Selection() {
         editor.SelectedTool = ImageEditorTools.Fill;

         editor.Hover(default);

         Assert.True(editor.ShowSelectionRect(4, 4));
      }

      [Fact]
      public void Change_Undo_Refresh() {
         editor.Palette.Elements[1].Color = Rgb(31, 31, 31);
         editor.Palette.SelectionStart = 1;
         ToolMove(new Point(0, 0));

         editor.Undo.Execute();

         Assert.Equal(0, editor.PixelData[editor.PixelIndex(4, 4)]);
      }

      [Fact]
      public void DifferentPixel_HoverColor_SelectPixel() {
         editor.Palette.Elements[1].Color = Rgb(31, 31, 31);
         editor.Palette.SelectionStart = 1;
         ToolMove(new Point(0, 0));

         editor.Palette.HoverIndex = 1;

         Assert.True(editor.ShowSelectionRect(4, 4));
      }

      [Fact]
      public void EyeDropper_Hover_ShowSelection() {
         editor.SelectedTool = ImageEditorTools.EyeDropper;

         editor.Hover(0, 0);

         Assert.True(editor.ShowSelectionRect(4, 4));
      }

      [Fact]
      public void EyeDropper_Drag_ShowSelection() {
         editor.SelectedTool = ImageEditorTools.EyeDropper;

         editor.ToolDown(0, 0);
         editor.Hover(1, 1);

         Assert.All(new[] {
            new Point(4, 4), new Point(4, 5), new Point(5, 4), new Point(5, 5),
         }, point => Assert.True(editor.ShowSelectionRect(point)));
      }

      [Fact]
      public void BlockPreview_Default_NotEnabled() {
         Assert.False(editor.BlockPreview.Enabled);
      }

      [Fact]
      public void EyeDropper_BlockSelected_BlockPreview() {
         editor.Palette.Elements[0].Color = Rgb(31, 31, 31);
         editor.SelectedTool = ImageEditorTools.EyeDropper;

         ToolMove(new Point(0, 0), new Point(1, 1));

         Assert.True(editor.BlockPreview.Enabled);
         Assert.Equal(2, editor.BlockPreview.PixelWidth);
         Assert.Equal(2, editor.BlockPreview.PixelHeight);
         Assert.Equal(4, editor.BlockPreview.PixelData.Length);
         Assert.All(4.Range(), i => Assert.Equal(Rgb(31, 31, 31), editor.BlockPreview.PixelData[i]));
         Assert.Equal(0, editor.CursorSize);
      }

      [Fact]
      public void BlockSelected_ColorSelected_BlockCleared() {
         editor.SelectedTool = ImageEditorTools.EyeDropper;
         ToolMove(new Point(0, 0), new Point(1, 1));

         editor.Palette.SelectionStart = 0;

         Assert.False(editor.BlockPreview.Enabled);
      }

      [Fact]
      public void BlockSelected_EyeDropperSinglePixel_BlockCleared() {
         editor.SelectedTool = ImageEditorTools.EyeDropper;
         ToolMove(new Point(0, 0), new Point(1, 1));

         ToolMove(new Point(-1, -1));

         Assert.False(editor.BlockPreview.Enabled);
      }

      [Fact]
      public void BlockSelected_Small_LargeScale() {
         editor.SelectedTool = ImageEditorTools.EyeDropper;

         ToolMove(new Point(0, 0), new Point(1, 1));

         Assert.Equal(32, editor.BlockPreview.SpriteScale);
      }

      [Fact]
      public void BlockSelected_Large_SmallScale() {
         editor.SelectedTool = ImageEditorTools.EyeDropper;

         ToolMove(new Point(-4, -4), new Point(3, 3));

         Assert.Equal(8, editor.BlockPreview.SpriteScale);
      }

      [Fact]
      public void EyeDropper_SelectRect_SelectionIsSquare() {
         editor.EyeDropperDown(0, 0);
         editor.Hover(3, 2);

         Assert.False(editor.ShowSelectionRect(6, 4));
      }

      [Fact]
      public void EyeDropper_SelectUpRight_RectangleAnchorsToBottomLeft() {
         editor.EyeDropperDown(0, 0);
         editor.Hover(1, -2);

         Assert.False(editor.ShowSelectionRect(4, 2));
      }

      [Fact]
      public void Draw_SetCursorSize_LargeCursor() {
         editor.SelectedTool = ImageEditorTools.Draw;

         editor.SetCursorSize.Execute("2");
         editor.Hover(default);

         Assert.All( new[] {
            new Point(4, 4),
            new Point(4, 5),
            new Point(5, 4),
            new Point(5, 5),
         }, p => Assert.True(editor.ShowSelectionRect(p)));
      }

      [Fact]
      public void EyeDropperBlock_SelectColor_DrawSinglePixel() {
         editor.EyeDropperDown(0, 0);
         editor.Hover(1, 1);
         editor.EyeDropperUp(1, 1);

         editor.Palette.SelectionStart = 1;
         editor.Hover(0, 0);

         Assert.All(new[] {
            new Point(4, 5),
            new Point(5, 4),
            new Point(5, 5),
         }, p => Assert.False(editor.ShowSelectionRect(p)));
      }

      [Fact]
      public void FillTool_HorizontalDrag_FillAreaWithHorizontalGradient() {
         editor.Palette.Elements[1].Color = White;
         editor.Palette.Elements[2].Color = Red;
         editor.Palette.Elements[3].Color = Blue;
         DrawBox(1, default, 4, 4);

         editor.Palette.SelectionStart = 2;
         editor.Palette.SelectionEnd = 3;
         editor.SelectedTool = ImageEditorTools.Fill;
         ToolMove(new Point(1, 1), new Point(2, 1));

         Assert.Equal(Red, editor.PixelData[editor.PixelIndex(5, 5)]);
         Assert.Equal(Blue, editor.PixelData[editor.PixelIndex(5, 6)]);
         Assert.Equal(Blue, editor.PixelData[editor.PixelIndex(6, 5)]);
         Assert.Equal(Blue, editor.PixelData[editor.PixelIndex(6, 6)]);
      }

      [Fact]
      public void FillTool_Drag_UpdateCursor() {
         editor.SelectedTool = ImageEditorTools.Fill;

         editor.ToolDown(default);
         editor.Hover(1, 1);

         Assert.True(editor.ShowSelectionRect(5, 5));
      }

      [Fact]
      public void TwoPageSprite_RequestSecondPage_EditsSecondPage() {
         Create2PageCompressedSprite();
         editor.SpritePage = 1;
         editor.Palette.Elements[1].Color = White;
         editor.Palette.SelectionStart = 1;

         ToolMove(new Point(-4, -4));

         var decompress = LZRun.Decompress(model, 0);
         Assert.Equal(1, decompress[0x20] & 0xF);
      }

      [Fact]
      public void TwoPagePalette_RequestSecondPalette_EditsSecondPage() {
         Create2PageCompressedPalette();
         editor.PalettePage = 1;

         editor.Palette.Elements[0].Color = White;

         var decompress = LZRun.Decompress(model, PaletteStart);
         Assert.Equal(White, decompress.ReadMultiByteValue(0x20, 2));
      }

      [Fact]
      public void TwoPageContent_CheckPageCount_ReturnsTwo() {
         Create2PageCompressedSprite();
         Create2PageCompressedPalette();

         Assert.Equal(2, editor.SpritePages);
         Assert.Equal(2, editor.PalettePages);
      }

      [Fact]
      public void TwoPageContent_SwitchPageCommand_SwitchPages() {
         Create2PageCompressedSprite();
         Create2PageCompressedPalette();

         editor.SpritePageOptions[1].Selected = true;
         editor.PalettePageOptions[1].Selected = true;

         Assert.Equal(1, editor.SpritePage);
         Assert.Equal(1, editor.PalettePage);
      }
   }
}
