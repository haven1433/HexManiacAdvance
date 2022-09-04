using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class BaseImageEditorTests {
      protected readonly IDataModel model;
      protected readonly ChangeHistory<ModelDelta> history;
      protected readonly ImageEditorViewModel editor;

      private Func<ModelDelta, ModelDelta> Revert { get; set; }

      private ModelDelta RevertHistoryChange(ModelDelta change) {
         return Revert?.Invoke(change) ?? change.Revert(model);
      }

      #region Test Helper Methods

      protected void DrawBox(int colorIndex, Point start, int width, int height) {
         editor.Palette.SelectionStart = colorIndex;

         editor.ToolDown(start);
         for (int x = 1; x < width; x++) editor.Hover(start = new Point(start.X + 1, start.Y));
         for (int y = 1; y < height; y++) editor.Hover(start = new Point(start.X, start.Y + 1));
         for (int x = 1; x < width; x++) editor.Hover(start = new Point(start.X - 1, start.Y));
         for (int y = 1; y < height; y++) editor.Hover(start = new Point(start.X, start.Y - 1));
         editor.ToolUp(start);
      }

      protected void DrawPixel(int index, short color, params Point[] points) {
         editor.Palette.Elements[index].Color = color;
         editor.Palette.SelectionStart = index;
         ToolMove(points);
      }

      protected void ToolMove(params Point[] motion) {
         editor.ToolDown(motion[0]);
         for (int i = 1; i < motion.Length; i++) editor.Hover(motion[i]);
         editor.ToolUp(motion[motion.Length - 1]);
      }

      public static short Rgb(int r, int g, int b) => (short)((r << 10) | (g << 5) | b);
      protected short GetPixel(int x, int y) => editor.PixelData[editor.PixelIndex(new Point(x, y))];
      protected static(int r, int g, int b) Rgb(short color) => (color >> 10, (color >> 5) & 31, color & 31);

      #endregion

      protected static readonly short Black = Rgb(0, 0, 0);
      protected static readonly short White = Rgb(31, 31, 31);
      protected static readonly short Red = Rgb(31, 0, 0);
      protected static readonly short Blue = Rgb(0, 0, 31);

      protected static readonly int SpriteStart = 0x00, SpritePointerStart = 0x80;
      protected static readonly int PaletteStart = 0x40, PalettePointerStart = 0x88;

      public BaseImageEditorTests() {
         model = new PokemonModel(new byte[0x200], singletons: BaseViewModelTestClass.Singletons);
         history = new ChangeHistory<ModelDelta>(RevertHistoryChange);

         model.WritePointer(history.CurrentChange, SpritePointerStart, SpriteStart);
         model.WritePointer(history.CurrentChange, PalettePointerStart, PaletteStart);

         var sprite = new SpriteRun(model, SpriteStart, new SpriteFormat(4, 1, 1, "palette"), new SortedSpan<int>(SpritePointerStart));
         model.ObserveAnchorWritten(history.CurrentChange, "sprite", sprite);

         var palette = new PaletteRun(PaletteStart, new PaletteFormat(4, 1), new SortedSpan<int>(PalettePointerStart));
         model.ObserveAnchorWritten(history.CurrentChange, "palette", palette);

         model[0x20] = 0x23; // random data after the sprite, so expanding it causes a repoint
         model[0x60] = 0x23; // random data after the palette, so expanding it causes a repoint

         editor = new ImageEditorViewModel(history, model, SpriteStart);
         editor.SpriteScale = 1;
      }
   }

   public class ImageEditorTests : BaseImageEditorTests {
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

      private void InsertCompressedData(int start, int length) {
         var compressedData = LZRun.Compress(new byte[length], 0, length);
         for (int i = 0; i < compressedData.Count; i++) model[start + i] = compressedData[i];
      }

      private void Create2PageCompressedSprite() {
         Insert64CompressedBytes(SpriteStart);

         var sprite = new LzSpriteRun(new SpriteFormat(4, 1, 1, "palette"), model, SpriteStart, new SortedSpan<int>(SpritePointerStart));
         model.ObserveAnchorWritten(history.CurrentChange, "sprite", sprite);

         editor.Refresh();
      }

      private void Create2PageCompressedPalette(int initialBlankPages = 0) {
         Insert64CompressedBytes(PaletteStart);

         var pal = new LzPaletteRun(new PaletteFormat(4, 2, initialBlankPages), model, PaletteStart, new SortedSpan<int>(PalettePointerStart));
         model.ObserveAnchorWritten(history.CurrentChange, "palette", pal);

         editor.Refresh();
      }

      private void Create256ColorCompressedSprite() {
         Insert64CompressedBytes(SpriteStart);

         var sprite = new LzSpriteRun(new SpriteFormat(8, 1, 1, "palette"), model, SpriteStart, new SortedSpan<int>(SpritePointerStart));
         model.ObserveAnchorWritten(history.CurrentChange, "sprite", sprite);

         editor.Refresh();
      }

      private void WriteArray(int address, string name, string format) {
         ArrayRun.TryParse(model, format, address, SortedSpan<int>.None, out var table);
         model.ObserveAnchorWritten(history.CurrentChange, name, table);
      }

      [Fact]
      public void Palette_Default_Color0Selected() {
         Assert.Single(editor.Palette.Elements.Where(sc => sc.Selected));
         Assert.True(editor.Palette.Elements[0].Selected);
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
      public void Zoom_Zoom24_NoZoom() {
         for (int i = 0; i < 25; i++) editor.ZoomIn(new Point(0, 0));
         Assert.Equal(24, editor.SpriteScale);
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
      public void EyeDropper_SelectSquare_SelectionIsSquare() {
         editor.EyeDropperDown(0, 0);
         editor.Hover(2, 2);

         Assert.True(editor.ShowSelectionRect(6, 4));
      }

      [Fact]
      public void EyeDropper_SelectRect_SelectionIsRect() {
         editor.EyeDropperDown(0, 0);
         editor.Hover(3, 2);

         Assert.True(editor.ShowSelectionRect(7, 4));
      }

      [Fact]
      public void EyeDropper_SelectUpRight_RectangleAnchorsToBottomLeft() {
         editor.EyeDropperDown(0, 0);
         editor.Hover(1, -2);

         Assert.True(editor.ShowSelectionRect(4, 2));
      }

      [Fact]
      public void Draw_SetCursorSize_LargeCursor() {
         editor.SelectedTool = ImageEditorTools.Draw;

         editor.SetCursorSize.Execute("2");
         editor.Hover(default);

         Assert.All(new[] {
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

      [Fact]
      public void Sprite256Color_Draw2ndPagePalette_ExpectedBytesChange() {
         Create256ColorCompressedSprite();
         Create2PageCompressedPalette(2);

         editor.PalettePage = 1;
         editor.Palette.SelectionStart = 1;  // page 2+1, index 1 -> color 0x31
         editor.SelectedTool = ImageEditorTools.Draw;
         ToolMove(new Point(-4, -4));

         var data = LZRun.Decompress(model, 0);
         Assert.Equal(0x31, data[0]);
      }

      [Fact]
      public void Empty_PasteExistingColors_PixelsPaste() {
         var fileSystem = new StubFileSystem();
         fileSystem.CopyImage = (new short[] { Black, Red, Blue, White }, 2);
         editor.Palette.Elements[1].Color = Red;
         editor.Palette.Elements[2].Color = Blue;
         editor.Palette.Elements[3].Color = White;

         editor.Paste.Execute(fileSystem);

         // pasted content should be centered
         Assert.Equal(Black, GetPixel(3, 3));
         Assert.Equal(Red, GetPixel(4, 3));
         Assert.Equal(Blue, GetPixel(3, 4));
         Assert.Equal(White, GetPixel(4, 4));

         // selection tool is active
         Assert.Equal(ImageEditorTools.Select, editor.SelectedTool);
         Assert.True(editor.ShowSelectionRect(3, 3));
         Assert.True(editor.ShowSelectionRect(4, 3));
         Assert.True(editor.ShowSelectionRect(3, 4));
         Assert.True(editor.ShowSelectionRect(4, 4));
      }

      [Fact]
      public void Data_Copy_FileSystemImageContainsCopy() {
         var fileSystem = new StubFileSystem();
         DrawPixel(1, Red, new Point(0, -1));
         DrawPixel(2, Blue, new Point(-1, 0));
         DrawPixel(3, White, new Point(0, 0));

         editor.SelectedTool = ImageEditorTools.Select;
         ToolMove(new Point(-1, -1), new Point(0, 0));
         editor.Copy.Execute(fileSystem);

         var (image, width) = fileSystem.CopyImage.value;
         Assert.Equal(2, width);
         Assert.Equal(new[] { Black, Red, Blue, White }, image);
      }

      [Fact]
      public void Selection_FlipVertical_DataFlips() {
         DrawPixel(1, Red, new Point(0, -1));
         DrawPixel(2, Blue, new Point(-1, 0));
         DrawPixel(3, White, new Point(0, 0));

         editor.SelectedTool = ImageEditorTools.Select;
         ToolMove(new Point(-1, -1), new Point(0, 0));
         editor.FlipVertical.Execute();

         Assert.Equal(Blue, GetPixel(3, 3));
         Assert.Equal(White, GetPixel(4, 3));
         Assert.Equal(Black, GetPixel(3, 4));
         Assert.Equal(Red, GetPixel(4, 4));
      }

      [Fact]
      public void Selection_FlipHorizontal_DataFlips() {
         DrawPixel(1, Red, new Point(0, -1));
         DrawPixel(2, Blue, new Point(-1, 0));
         DrawPixel(3, White, new Point(0, 0));

         editor.SelectedTool = ImageEditorTools.Select;
         ToolMove(new Point(-1, -1), new Point(0, 0));
         editor.FlipHorizontal.Execute();

         Assert.Equal(Red, GetPixel(3, 3));
         Assert.Equal(Black, GetPixel(4, 3));
         Assert.Equal(White, GetPixel(3, 4));
         Assert.Equal(Blue, GetPixel(4, 4));
      }

      [Fact]
      public void EyeDropper_4PixelCursorHover_4PixelSelection() {
         editor.SelectedTool = ImageEditorTools.EyeDropper;

         editor.CursorSize = 4;
         editor.Hover(0, 0);

         Assert.Equal(ImageEditorTools.EyeDropper, editor.SelectedTool);
         Assert.True(editor.ShowSelectionRect(7, 7));
      }

      [Fact]
      public void ColorSelection_SelectNew_DisjointSelection() {
         editor.Palette.SelectionStart = 0;
         editor.Palette.SelectionEnd = 2;

         editor.Palette.ToggleSelection(5);

         Assert.True(editor.Palette.Elements[2].Selected);
         Assert.False(editor.Palette.Elements[3].Selected);
         Assert.True(editor.Palette.Elements[5].Selected);
      }

      [Fact]
      public void ColorSelection_DeselectColor_DisjointSelection() {
         editor.Palette.SelectionStart = 0;
         editor.Palette.SelectionEnd = 2;

         editor.Palette.ToggleSelection(1);

         Assert.True(editor.Palette.Elements[0].Selected);
         Assert.False(editor.Palette.Elements[1].Selected);
         Assert.True(editor.Palette.Elements[2].Selected);
      }

      [Fact]
      public void FillToolSelected_GrabColor_ColorGrabbed() {
         editor.SetCursorSize.Execute("2");
         editor.Palette.SelectionStart = 3;
         editor.SelectedTool = ImageEditorTools.Fill;

         editor.EyeDropperDown(0, 0);
         editor.EyeDropperUp(0, 0);

         Assert.True(editor.Palette.Elements[0].Selected);
         Assert.All(Enumerable.Range(1, 15), i => Assert.False(editor.Palette.Elements[i].Selected));
         Assert.False(editor.BlockPreview.Enabled);
      }

      [Fact]
      public void MultiplePalettesAvailable_SelectSecondOption_SwitchPalettes() {
         const int PalettePointer2Start = 0x90, Palette2Start = 0x100;
         model.ClearFormat(new NoDataChangeDeltaModel(), 0, model.Count);
         model.WritePointer(history.CurrentChange, PalettePointer2Start, Palette2Start);

         InsertCompressedData(SpriteStart, 0x20);
         InsertCompressedData(PaletteStart, 0x20);
         InsertCompressedData(Palette2Start, 0x20);
         WriteArray(SpritePointerStart, "sprites", "[sprite<`lzs4x1x1`>]1");
         WriteArray(PalettePointerStart, "palettes1", "[pal<`lzp4`>]sprites");
         WriteArray(PalettePointer2Start, "palettes2", "[pal<`lzp4`>]sprites");
         var editor = new ImageEditorViewModel(history, model, SpriteStart);

         editor.SelectedEditOption = 1;

         Assert.True(editor.HasMultipleEditOptions);
         Assert.Equal(PalettePointer2Start, editor.PalettePointer);
         Assert.Equal(2, editor.EditOptions.Count);
      }

      [Fact]
      public void TwoPageSpriteAndOnePageSprite_SelectSecondPageThenSwitchSprites_SelectPageZero() {
         const int SpritePointer2Start = 0x90, Sprite2Start = 0x100;
         model.ClearFormat(new NoDataChangeDeltaModel(), 0, model.Count);
         model.WritePointer(history.CurrentChange, SpritePointer2Start, Sprite2Start);

         InsertCompressedData(SpriteStart, 0x40); // 2 pages
         InsertCompressedData(Sprite2Start, 0x20); // 1 page
         WriteArray(SpritePointerStart, "sprites", "[sprite<`lzs4x1x1`>]1");
         WriteArray(PalettePointerStart, "palettes", "[pal<`ucp4`>]sprites");
         WriteArray(SpritePointer2Start, "sprites2", "[sprite<`lzs4x1x1`>]sprites");
         var editor = new ImageEditorViewModel(history, model, SpriteStart);

         editor.SpritePage = 1;
         editor.SelectedEditOption = 1;

         Assert.Equal(0, editor.SpritePage);
      }

      [Fact]
      public void LargeSelection_DrawOutOfBounds_LimitDrawPixels() {
         editor.EyeDropperDown(0, 0);
         editor.Hover(2, 2);
         editor.EyeDropperUp(2, 2);
         editor.SelectedTool = ImageEditorTools.Draw;

         ToolMove(new Point(3, 3));

         // nothing to assert: if it didn't crash, we're good.
      }

      [Fact]
      public void Sprite_CanEditTilePalettes_False() {
         Assert.False(editor.CanEditTilePalettes);
      }

      [Fact]
      public void EyeDropper_SelectFromLeftOfImage_DoNothing() {
         editor.SelectedTool = ImageEditorTools.EyeDropper;

         editor.ToolDown(-6, 0);
         editor.Hover(-1, 3);
         editor.ToolUp(-1, 3);

         Assert.False(editor.BlockPreview.Enabled);
      }

      [Fact]
      public void EyeDropper_SelectFromBelowImage_DoNothing() {
         editor.SelectedTool = ImageEditorTools.EyeDropper;

         editor.ToolDown(0, 6);
         editor.Hover(2, 3);
         editor.ToolUp(2, 3);

         Assert.False(editor.BlockPreview.Enabled);
      }

      [Fact]
      public void TallEyeDropper_Draw_DrawTallRect() {
         // draw a vertical line
         editor.Palette.Elements[1].Color = White;
         editor.Palette.SelectionStart = 1;
         ToolMove(new Point(0, -1), new Point(0, 0), new Point(0, 1));

         // grab the vertical line and click to draw it somewhere else
         editor.SelectedTool = ImageEditorTools.EyeDropper;
         ToolMove(new Point(0, -1), new Point(0, 0), new Point(0, 1));
         editor.SelectedTool = ImageEditorTools.Draw;
         ToolMove(new Point(-2, -1));

         Assert.Equal(White, editor.PixelData[editor.PixelIndex(2, 3)]);
         Assert.Equal(White, editor.PixelData[editor.PixelIndex(2, 4)]);
         Assert.Equal(White, editor.PixelData[editor.PixelIndex(2, 5)]);
      }

      [Fact]
      public void Selection_ControlPlusDrag_OriginalPixelsRemainTheSame() {
         editor.Palette.Elements[1].Color = White;
         editor.Palette.SelectionStart = 1;
         editor.CursorSize = 2;
         ToolMove(new Point(0, 0));
         editor.SelectedTool = ImageEditorTools.Select;
         ToolMove(new Point(0, 0), new Point(1, 1));

         editor.ToolDown(new Point(0, 0), altBehavior: true);
         editor.Hover(new Point(-4, -4));
         editor.ToolUp(-4, -4);

         Assert.Equal(White, editor.PixelData[editor.PixelIndex(0, 0)]);
         Assert.Equal(White, editor.PixelData[editor.PixelIndex(4, 4)]);
      }

      [Theory]
      [InlineData(-4, -4, 0, 0)]
      [InlineData(3, -4, 6, 0)]
      [InlineData(-4, 3, 0, 6)]
      [InlineData(3, 3, 6, 6)]
      public void Copy_PasteInCorner_NewPixelsInCorner(int hoverX, int hoverY, int pixelX, int pixelY) {
         var fileSystem = new StubFileSystem();
         editor.Palette.Elements[1].Color = White;
         fileSystem.CopyImage = (new short[] { White, White, White, White }, 2);

         editor.Hover(hoverX, hoverY);
         editor.Paste.Execute(fileSystem);

         Assert.Equal(White, editor.PixelData[editor.PixelIndex(pixelX + 0, pixelY + 0)]);
         Assert.Equal(White, editor.PixelData[editor.PixelIndex(pixelX + 1, pixelY + 0)]);
         Assert.Equal(White, editor.PixelData[editor.PixelIndex(pixelX + 0, pixelY + 1)]);
         Assert.Equal(White, editor.PixelData[editor.PixelIndex(pixelX + 1, pixelY + 1)]);
      }

      [Fact]
      public void EyeDropperSize4_ClickDrag_Select4x4Tiles() {
         editor.SelectedTool = ImageEditorTools.EyeDropper;
         editor.CursorSize = 4;

         editor.ToolDown(-2, -2);
         editor.Hover(2, -2);
         editor.ToolUp(2, -2);

         Assert.Equal(8, editor.BlockPreview.PixelWidth);
         Assert.Equal(4, editor.BlockPreview.PixelHeight);
         Assert.Equal(editor.BlockPreview.PixelData, editor.PixelData.Take(editor.BlockPreview.PixelData.Length).ToArray());
      }

      [Fact]
      public void EyeDropper_TallSelection_BlockPreviewSpriteScaleIsReasonable() {
         editor.EyeDropperDown(-4, -4);
         editor.Hover(-4, 3);
         editor.EyeDropperUp(-4, 3);
         Assert.InRange(editor.BlockPreview.SpriteScale, 4, 8);
      }

      [Fact]
      public void Selection_Delete_FillWithBlack() {
         editor.Palette.SelectionStart = 1;
         editor.Palette.Elements[1].Color = White;
         ToolMove(new Point(0, 0));

         editor.SelectedTool = ImageEditorTools.Select;
         ToolMove(new Point(0, 0), new Point(1, 1));

         editor.DeleteSelection();

         Assert.Equal(Black, editor.PixelData[editor.PixelIndex(4, 4)]);
      }
   }

   public class ImageEditorSingleSpriteMultiplePalettesTests : BaseImageEditorTests {
      public ImageEditorSingleSpriteMultiplePalettesTests() {
         var newPaletteData = LZRun.Compress(new byte[0x40], 0, 0x40).ToArray();
         Array.Copy(newPaletteData, 0, model.RawData, PaletteStart, newPaletteData.Length);
         model.ObserveRunWritten(new ModelDelta(), new LzPaletteRun(new PaletteFormat(4, 2), model, PaletteStart));
         editor.Refresh();
      }

      [Fact]
      public void SingleSpriteMultiplePalette_BucketToolWithSecondPalette_FillWorks() {
         editor.PalettePage = 1;
         editor.Palette.SelectionStart = 1;
         editor.Palette.Elements[1].Color = White;

         editor.SelectedTool = ImageEditorTools.Fill;
         ToolMove(new Point(0, 0));

         Assert.Equal(White, editor.PixelData[editor.PixelIndex(0, 0)]);
         Assert.Equal(White, editor.PixelData[editor.PixelIndex(0, 7)]);
         Assert.Equal(White, editor.PixelData[editor.PixelIndex(7, 0)]);
         Assert.Equal(White, editor.PixelData[editor.PixelIndex(7, 7)]);
         Assert.Equal(1, editor.ReadRawPixel(4, 4));
      }

      [Fact]
      public void SingleSpriteMultiplePalette_Draw8x8WithPalette1_DoNotSwitchPalette() {
         editor.PalettePage = 1;
         editor.SelectedTool = ImageEditorTools.Draw;

         editor.CursorSize = 8;
         ToolMove(new Point(0, 0));

         Assert.Equal(1, editor.PalettePage);
      }

      [Fact]
      public void SingleSpriteMutltiplePalettes_EyeDropToolOnSecondPalette_SecondPaletteStillSelected() {
         editor.PalettePage = 1;
         editor.Palette.SelectionStart = 1;

         editor.EyeDropperDown(0, 0);
         editor.EyeDropperUp(0, 0);

         Assert.Equal(0, editor.Palette.SelectionStart);
         Assert.Equal(1, editor.PalettePage);
      }

      [Fact]
      public void SingleSpriteMultiplePalettes_DrawPalette2_ColorsAreInRangeForPalette1() {
         editor.PalettePage = 1;

         editor.Palette.SelectionStart = 1;
         editor.Palette.Elements[1].Color = White;
         ToolMove(new Point(0, 0));

         Assert.Equal(1, editor.ReadRawPixel(4, 4));
      }

      [Fact]
      public void SingleSpriteMultiplePalettes_SelectDragOnSecondPalette_SpriteStillShowsSecondPalette() {
         editor.PalettePage = 1;
         editor.Palette.SelectionStart = 1;
         editor.Palette.Elements[1].Color = White;
         ToolMove(new Point(0, 0));

         editor.SelectedTool = ImageEditorTools.Select;
         ToolMove(new Point(0, 0), new Point(1, 1));
         ToolMove(new Point(0, 0), new Point(-1, 0));

         Assert.Equal(White, editor.PixelData[editor.PixelIndex(3, 4)]);
      }
   }

   public class ImageEditorTilemapTests {
      private readonly IDataModel model = new PokemonModel(new byte[0x200], singletons: BaseViewModelTestClass.Singletons);
      private readonly ChangeHistory<ModelDelta> history;
      private readonly ImageEditorViewModel editor;
      private ModelDelta RevertHistoryChange(ModelDelta change) => change.Revert(model);

      public const int TilemapStart = 0x00, TilesetStart = 0x40, PaletteStart = 0x80;

      private static short Rgb(int r, int g, int b) => (short)((r << 10) | (g << 5) | b);
      private short GetPixel(int x, int y) => editor.PixelData[editor.PixelIndex(new Point(x, y))];
      private void InsertCompressedData(int start, params byte[] data) {
         var compressedData = LZRun.Compress(data, 0, data.Length);
         for (int i = 0; i < compressedData.Count; i++) model[start + i] = compressedData[i];
      }

      public ImageEditorTilemapTests() {
         history = new ChangeHistory<ModelDelta>(RevertHistoryChange);

         model.WritePointer(history.CurrentChange, 0x160, TilemapStart);
         model.WritePointer(history.CurrentChange, 0x164, TilesetStart);
         model.WritePointer(history.CurrentChange, 0x168, PaletteStart);

         InsertCompressedData(TilemapStart, new byte[] {
            0x00, 0x20, // use 2 tiles with the 1st palette
            0x01, 0x20,
            0x02, 0x30, // use 2 tiles with the 2nd palette
            0x03, 0x30
         }); // 2000 is page 2, tile 0
         InsertCompressedData(TilesetStart, new byte[0x20 * 4]);      // 4 tile
         InsertCompressedData(PaletteStart, new byte[0x20 * 2]);      // 2 pages

         model.ObserveAnchorWritten(history.CurrentChange, "tilemap", new LzTilemapRun(new TilemapFormat(4, 2, 2, "tileset"), model, TilemapStart));
         model.ObserveAnchorWritten(history.CurrentChange, "tileset", new LzTilesetRun(new TilesetFormat(4, "palette"), model, TilesetStart));
         model.ObserveAnchorWritten(history.CurrentChange, "palette", new LzPaletteRun(new PaletteFormat(4, 2, 2), model, PaletteStart)); // pages are 2 and 3

         editor = new ImageEditorViewModel(history, model, TilemapStart);
         editor.SpriteScale = 1;
      }

      [Fact]
      public void Tilemap_Edit_SeePalettePagePerTile() {
         Assert.True(editor.CanEditTilePalettes);
         Assert.Equal(2, editor.TilePalettes[0]);
         Assert.Equal(2, editor.TilePalettes[1]);
         Assert.Equal(3, editor.TilePalettes[2]);
         Assert.Equal(3, editor.TilePalettes[3]);

         Assert.Equal(0, editor.PalettePage);
         Assert.Equal(new[] { -2, 0, 1 }, editor.PalettePageOptions.Select(option => option.Index).ToArray());
      }

      [Fact]
      public void Tilemap_EditTilePalettes_SeeTilePalettesChange() {
         editor.TilePalettes[0] = 3;

         var mapData = LZRun.Decompress(model, TilemapStart);
         var (pal, _, _, _) = LzTilemapRun.ReadTileData(mapData, 0, 2);
         Assert.Equal(3, pal);
      }

      [Fact]
      public void Tilemap_UseTilePaletteTool_TilePaletteChanged() {
         editor.SelectedTool = ImageEditorTools.TilePalette;
         editor.PalettePage = 1;

         editor.ToolDown(-4, -4);
         editor.ToolUp(-4, -4);

         var mapData = LZRun.Decompress(model, TilemapStart);
         var (pal, _, _, _) = LzTilemapRun.ReadTileData(mapData, 0, 2);
         Assert.Equal(3, pal);
      }

      [Fact]
      public void Tilemap_RightClickFromTilePaletteTool_SelectedTilePaletteIndexUpdates() {
         editor.SelectedTool = ImageEditorTools.TilePalette;

         editor.EyeDropperDown(4, 4);
         editor.EyeDropperUp(4, 4);

         Assert.Equal(1, editor.PalettePage);
      }

      [Fact]
      public void TilePaletteTool_Hover_ShowEntireTileAsSelected() {
         editor.SelectedTool = ImageEditorTools.TilePalette;

         editor.Hover(4, 4);

         Assert.True(editor.ShowSelectionRect(8, 8));
         Assert.True(editor.ShowSelectionRect(15, 15));
      }

      [Fact]
      public void WhitePalette_ChangePalette_PixelsChange() {
         editor.PalettePageOptions.Last().Selected = true;
         editor.Palette.Elements[0].Color = Rgb(31, 31, 31);
         editor.SelectedTool = ImageEditorTools.TilePalette;

         editor.ToolDown(4, -4);
         editor.ToolUp(4, -4);

         Assert.Equal(Rgb(0, 0, 0), GetPixel(2, 2));
         Assert.Equal(Rgb(31, 31, 31), GetPixel(12, 3));
      }

      [Fact]
      public void ChangePalette_RequiresRepoint_NoThrow() {
         // make a 4x2 image, since compression won't happen with a 2x2 image
         InsertCompressedData(TilemapStart, new byte[] {
            0x00, 0x20,
            0x00, 0x20,
            0x00, 0x20,
            0x00, 0x20,
            0x00, 0x20,
            0x00, 0x20,
            0x00, 0x20,
            0x00, 0x20,
         }); // 2000 is page 2, tile 0. Repeate for 8 tiles, 4x2
         model.ObserveAnchorWritten(history.CurrentChange, "tilemap", new LzTilemapRun(new TilemapFormat(4, 4, 2, "tileset"), model, TilemapStart));
         var editor = new ImageEditorViewModel(history, model, TilemapStart);
         var tilemap = (LzTilemapRun)model.GetNextRun(TilemapStart);
         model[tilemap.Start + tilemap.Length] = 32; // there's data after the run, so if it grows it needs to repoint

         // change a tile in a way that causes a repoint
         editor.SelectedTool = ImageEditorTools.TilePalette;
         editor.PalettePage = 1;
         editor.ToolDown(0, 0);
         editor.ToolUp(0, 0);
      }

      [Fact]
      public void DrawTool_DrawOnTileWithDifferentPalette_NoEffect() {
         editor.PalettePage = 1;
         editor.Palette.Elements[1].Color = Rgb(31, 0, 0);
         editor.Palette.SelectionStart = 1;
         editor.SelectedTool = ImageEditorTools.Draw;

         editor.ToolDown(-8, -8);
         editor.ToolUp(-8, -8);

         Assert.Equal(Rgb(0, 0, 0), editor.PixelData[editor.PixelIndex(0, 0)]);
      }

      [Fact]
      public void DrawTool_DrawOnTileWithSamePalette_Effect() {
         editor.PalettePage = 0;
         editor.Palette.Elements[1].Color = Rgb(31, 0, 0);
         editor.Palette.SelectionStart = 1;
         editor.SelectedTool = ImageEditorTools.Draw;

         editor.ToolDown(-8, -8);
         editor.ToolUp(-8, -8);

         Assert.Equal(Rgb(31, 0, 0), editor.PixelData[editor.PixelIndex(0, 0)]);
      }

      [Fact]
      public void DrawTool_HoverOnTileWithDifferentPalette_NoHoverSelection() {
         editor.PalettePage = 1;
         editor.SelectedTool = ImageEditorTools.Draw;

         editor.Hover(-8, -8);

         Assert.False(editor.ShowSelectionRect(0, 0));
      }

      [Fact]
      public void EyeDropperTool_PickPalette1Tile_SelectPalette1() {
         editor.EyeDropperDown(4, 4);
         editor.EyeDropperUp(4, 4);

         Assert.Equal(1, editor.PalettePage);
      }

      [Fact]
      public void EyeDropperPalette1Tile_DrawPalette0Tile_NoDraw() {
         editor.PalettePage = 1;
         editor.Palette.Elements[0].Color = Rgb(31, 0, 0); // set the bottom two tiles to red
         editor.EyeDropperDown(4, 4);
         editor.EyeDropperUp(4, 4);

         editor.ToolDown(-8, -8);
         editor.ToolUp(-8, -8);

         Assert.Equal(Rgb(0, 0, 0), editor.PixelData[editor.PixelIndex(0, 0)]);
      }

      [Fact]
      public void FillTool_FillPalette1_TilesUsingPalette0Unaffected() {
         editor.PalettePage = 1;
         editor.Palette.Elements[1].Color = Rgb(31, 0, 0); // set the bottom two tiles to red
         editor.Palette.SelectionStart = 1;
         editor.SelectedTool = ImageEditorTools.Fill;

         editor.ToolDown(4, 4);
         editor.ToolUp(4, 4);

         Assert.All(128.Range(), i => Assert.Equal(Rgb(0, 0, 0), editor.PixelData[i]));
         Assert.All(128.Range(), i => Assert.Equal(Rgb(31, 0, 0), editor.PixelData[i + 128]));
      }

      [Fact]
      public void PaletteHover_HoverOnPage1_OnlyPage1PixelsHighlight() {
         editor.PalettePage = 1;

         editor.Palette.HoverIndex = -1;
         editor.Palette.HoverIndex = 0;

         Assert.False(editor.ShowSelectionRect(0, 0));
         Assert.True(editor.ShowSelectionRect(12, 12));
      }

      [Fact]
      public void EyeDropperTool_ClickAndDrag_OnlyInitialPixelMattersForSelection() {
         editor.SelectedTool = ImageEditorTools.EyeDropper;
         editor.CursorSize = 2;

         editor.ToolDown(4, 4);
         editor.Hover(6, 6);
         editor.ToolUp(6, 6);

         Assert.Equal(2, editor.BlockPreview.PixelWidth);
         Assert.Equal(2, editor.BlockPreview.PixelHeight);
      }

      [Fact]
      public void EyeDropperTool_SelectEntireTile_CanDrawOverAnotherPalette() {
         editor.SelectedTool = ImageEditorTools.Draw;
         editor.CursorSize = 8;

         editor.EyeDropperDown(4, 4);
         editor.EyeDropperUp(4, 4);

         editor.ToolDown(-8, -8);
         editor.ToolUp(-8, -8);

         var mapData = LZRun.Decompress(model, TilemapStart);
         var (pal, _, _, _) = LzTilemapRun.ReadTileData(mapData, 0, 2);
         Assert.Equal(3, pal);
      }

      [Fact]
      public void EyeDropperTool_SelectEntireTile_HoverDifferentPaletteShowsSelection() {
         editor.SelectedTool = ImageEditorTools.Draw;
         editor.CursorSize = 8;

         editor.EyeDropperDown(4, 4);
         editor.EyeDropperUp(4, 4);

         editor.Hover(-6, -6);

         Assert.True(editor.ShowSelectionRect(3, 3));
      }

      [Fact]
      public void Selection_DragIntoTileWithDifferentPalette_ColorsFromNewPaletteAreUsed() {
         // draw red dots at (-4,-4) and (-3,-3)
         editor.SelectedTool = ImageEditorTools.Draw;
         editor.Palette.Elements[1].Color = Rgb(31, 0, 0);
         editor.Palette.SelectionStart = 1;
         editor.ToolDown(-4, -4);
         editor.ToolUp(-4, -4);
         editor.ToolDown(-3, -3);
         editor.ToolUp(-3, -3);

         // set color 1 of palette page 1 to green
         editor.PalettePage = 1;
         editor.Palette.Elements[1].Color = Rgb(0, 31, 0);

         // select the 2x2 square from (-4,-4) to (-3,-3)
         editor.SelectedTool = ImageEditorTools.Select;
         editor.ToolDown(-4, -4);
         editor.Hover(-3, -3);
         editor.ToolUp(-3, -3);

         // drag to the bottom left tile
         editor.ToolDown(-4, -4);
         editor.Hover(-4, 4);
         editor.ToolUp(-4, 4);

         // check that the pixels are green after being dragged
         Assert.Equal(Rgb(0, 31, 0), editor.PixelData[editor.PixelIndex(4, 12)]);
         Assert.Equal(Rgb(0, 31, 0), editor.PixelData[editor.PixelIndex(5, 13)]);
         Assert.Equal(Rgb(0, 0, 0), editor.PixelData[editor.PixelIndex(4, 13)]);
         Assert.Equal(Rgb(0, 0, 0), editor.PixelData[editor.PixelIndex(5, 12)]);
      }

      [Fact]
      public void Selection_FlipBetweenTilesWithDifferentPalettes_ColorsFromNewPaletteAreUsed() {
         // draw red dots at (-4,-4) and (-3,-3)
         editor.SelectedTool = ImageEditorTools.Draw;
         editor.Palette.Elements[1].Color = Rgb(31, 0, 0);
         editor.Palette.SelectionStart = 1;
         editor.ToolDown(-4, -4);
         editor.ToolUp(-4, -4);
         editor.ToolDown(-3, -3);
         editor.ToolUp(-3, -3);

         // set color 1 of palette page 1 to green
         editor.PalettePage = 1;
         editor.Palette.Elements[1].Color = Rgb(0, 31, 0);

         // select a tall rectangle that spans two tiles
         editor.SelectedTool = ImageEditorTools.Select;
         editor.ToolDown(-4, -4);
         editor.Hover(-3, 4);
         editor.ToolUp(-3, 4);

         editor.FlipVertical.Execute();

         // check that the pixels are green after being flipped
         Assert.Equal(Rgb(0, 31, 0), editor.PixelData[editor.PixelIndex(4, 12)]);
         Assert.Equal(Rgb(0, 31, 0), editor.PixelData[editor.PixelIndex(5, 11)]);
         Assert.Equal(Rgb(0, 0, 0), editor.PixelData[editor.PixelIndex(4, 11)]);
         Assert.Equal(Rgb(0, 0, 0), editor.PixelData[editor.PixelIndex(5, 12)]);
      }

      [Fact]
      public void FillTool_FillTileWithDifferentPalette_NothingHappens() {
         editor.PalettePage = 1;
         editor.Palette.Elements[0].Color = Rgb(31, 31, 31);
         editor.PalettePage = 0;
         editor.SelectedTool = ImageEditorTools.Fill;

         editor.ToolDown(5, 5);
         editor.ToolUp(5, 5);

         Assert.Equal(Rgb(31, 31, 31), editor.PixelData[editor.PixelIndex(13, 13)]);
      }

      [Fact]
      public void Tilemap_PaintSameTileWithTilePaletteTool_NoTilesChanged() {
         editor.SelectedTool = ImageEditorTools.TilePalette;
         editor.TilePaletteMode = TilePaletteMode.Fill;
         editor.PalettePage = 0;

         editor.ToolDown(-4, -4);
         editor.ToolUp(-4, -4);

         var mapData = LZRun.Decompress(model, TilemapStart);
         var tilePalette = 4.Range().Select(i => LzTilemapRun.ReadTileData(mapData, i, 2).paletteIndex).ToList();
         Assert.Equal(new[] { 2, 2, 3, 3 }, tilePalette);
      }

      [Fact]
      public void Tilemap_PaintDifferentTileWithTilePaletteTool_TilesChanged() {
         editor.SelectedTool = ImageEditorTools.TilePalette;
         editor.TilePaletteMode = TilePaletteMode.Fill;
         editor.PalettePage = 1;

         editor.ToolDown(-4, -4);
         editor.ToolUp(-4, -4);

         var mapData = LZRun.Decompress(model, TilemapStart);
         var tilePalette = 4.Range().Select(i => LzTilemapRun.ReadTileData(mapData, i, 2).paletteIndex).ToList();
         Assert.Equal(new[] { 3, 3, 3, 3 }, tilePalette);
      }
   }

   public class ImageEditor8BitTilemapTests : BaseViewModelTestClass {

      private const int PaletteStart = 0x000, TilesetStart = 0x080, TilemapStart = 0x100;
      private readonly short
         Black = UncompressedPaletteColor.Pack(0, 0, 0),
         White = UncompressedPaletteColor.Pack(31, 31, 31),
         Red = UncompressedPaletteColor.Pack(31, 0, 0);

      private readonly ImageEditorViewModel editor;

      public ImageEditor8BitTilemapTests() {
         SetFullModel(0xFF);
         LZRun.Compress(new byte[0x40]).WriteInto(Model.RawData, PaletteStart);
         LZRun.Compress(new byte[0x40]).WriteInto(Model.RawData, TilesetStart);
         LZRun.Compress(new byte[0x08]).WriteInto(Model.RawData, TilemapStart);

         Model.WritePointer(ViewPort.CurrentChange, 0x180, PaletteStart);
         Model.WritePointer(ViewPort.CurrentChange, 0x184, TilesetStart);
         Model.WritePointer(ViewPort.CurrentChange, 0x188, TilemapStart);

         Model.ObserveAnchorWritten(ViewPort.CurrentChange, "palette", new LzPaletteRun(new PaletteFormat(4, 2, 2), Model, PaletteStart)); // pages are 2 and 3
         Model.ObserveAnchorWritten(ViewPort.CurrentChange, "tileset", new LzTilesetRun(new TilesetFormat(8, "palette"), Model, TilesetStart));
         Model.ObserveAnchorWritten(ViewPort.CurrentChange, "tilemap", new LzTilemapRun(new TilemapFormat(8, 2, 2, "tileset"), Model, TilemapStart));

         editor = new ImageEditorViewModel(ViewPort.ChangeHistory, Model, TilemapStart) { SpriteScale = 1 };
      }

      [Fact]
      public void Tileset_8BPP_CannotEditTilePalettes() {
         Assert.False(editor.CanEditTilePalettes);
      }

      [Fact]
      public void Bucket_Fill_Filled() {
         editor.Palette.SelectionStart = 1;
         editor.Palette.Elements[1].Color = White;
         editor.SelectedTool = ImageEditorTools.Fill;

         editor.ToolDown(0, 0);
         editor.ToolUp(0, 0);

         Assert.Equal(White, editor.PixelData[editor.PixelIndex(0, 0)]);
      }

      [Fact]
      public void ImageWithMoreThan16ColorsInATile_Import_ColorsAreImportedCorrectly() {
         var tool = ViewPort.Tools.SpriteTool;
         ViewPort.Tools.SelectedIndex = ViewPort.Tools.IndexOf(tool);
         tool.SpriteAddress = TilemapStart;
         tool.PaletteAddress = PaletteStart;
         var imageToImport = new short[16 * 16];
         for (int i = 0; i < 32; i++) imageToImport[i / 8 * 16 + i % 8] = UncompressedPaletteColor.Pack(i, i, i);

         tool.ImportPair.Execute(new StubFileSystem { LoadImage = arg => (imageToImport, 16) });

         var tilesetData = ((ITilesetRun)Model.GetNextRun(TilesetStart)).GetPixels(Model, 0);
         var palette = (IPaletteRun)Model.GetNextRun(PaletteStart);
         var paletteData = palette.AllColors(Model);
         var pixels = SpriteTool.Render(tilesetData, paletteData, palette.PaletteFormat.InitialBlankPages, 0);
         Assert.All(32.Range(), i => {
            var pixel = pixels[8 + i + (i / 8) * 8];
            Assert.Equal(i, UncompressedPaletteColor.ToRGB(pixel).r);
         });
      }
   }

   public class ImageEditorOneBitImageTests : BaseViewModelTestClass {
      private readonly IDataModel model = new PokemonModel(new byte[0x200], singletons: Singletons);
      private readonly ChangeHistory<ModelDelta> history;
      private readonly ImageEditorViewModel editor;
      private ModelDelta RevertHistoryChange(ModelDelta change) => change.Revert(model);

      private const int ImageStart = 0;

      public ImageEditorOneBitImageTests() {
         history = new ChangeHistory<ModelDelta>(RevertHistoryChange);

         model.WritePointer(history.CurrentChange, 0x160, ImageStart);

         model.ObserveAnchorWritten(history.CurrentChange, "image", new SpriteRun(model, 0, new SpriteFormat(1, 1, 1, string.Empty)));

         editor = new ImageEditorViewModel(history, model, ImageStart);
      }

      [Fact]
      public void InitialState_PaletteHas2Colors() {
         Assert.Equal(2, editor.Palette.Elements.Count);
         Assert.False(editor.Palette.CanEditColors);
      }
   }

   public class ImageEditorOneByteTilemapTests {
      private const int PaletteStart = 0x00, TilesetStart = 0x20, TilemapStart = 0x100;

      private readonly IDataModel model = new PokemonModel(new byte[0x200], singletons: BaseViewModelTestClass.Singletons);
      private readonly ChangeHistory<ModelDelta> history;
      private readonly ImageEditorViewModel editor;

      private void InsertCompressedData(int start, int length) {
         var compressedData = LZRun.Compress(new byte[length], 0, length);
         for (int i = 0; i < compressedData.Count; i++) model[start + i] = compressedData[i];
      }

      public ImageEditorOneByteTilemapTests() {
         history = new ChangeHistory<ModelDelta>(change => change.Revert(model));
         model.WritePointer(history.CurrentChange, 0x160, PaletteStart);
         model.WritePointer(history.CurrentChange, 0x164, TilesetStart);
         model.WritePointer(history.CurrentChange, 0x168, TilemapStart);
         InsertCompressedData(PaletteStart, 32);
         InsertCompressedData(TilesetStart, 64);
         InsertCompressedData(TilemapStart, 2);

         PokemonModel.ApplyAnchor(model, history.CurrentChange, PaletteStart, "^pal`lzp4:1`");
         PokemonModel.ApplyAnchor(model, history.CurrentChange, TilesetStart, "^tiles`lzt8|pal`");
         PokemonModel.ApplyAnchor(model, history.CurrentChange, TilemapStart, "^map`lzm8x2x1|tiles`");
         editor = new ImageEditorViewModel(history, model, TilemapStart) { SpriteScale = 1 };
      }

      [Fact]
      public void InitialState_Valid() {
         Assert.Equal(2, editor.TileWidth);
         Assert.Equal(1, editor.TileHeight);
         Assert.Equal(2, editor.TilePalettes.Count);
      }

      [Fact]
      public void SelectColor1_Hover_CanDraw() {
         editor.Palette.SelectionStart = 1;
         editor.Hover(-4, 0);
         Assert.True(editor.ShowSelectionRect(4, 4));
      }

      [Fact]
      public void SelectColor1_Draw_PixelChanges() {
         editor.Palette.SelectionStart = 1;

         editor.ToolDown(-4, 0);
         editor.ToolUp(-4, 0);

         var tilemap = (ITilemapRun)model.GetNextRun(TilemapStart);
         var data = tilemap.GetTilemapData();
         Assert.Equal(2, data.Length);
         Assert.NotEqual(data[0], data[1]);
      }
   }

   public class ImageEditorTilesetTests : BaseViewModelTestClass {
      [Fact]
      public void NoPalette_LoadInEditor_EditorLoadsSelectedPalette() {
         SetFullModel(0xFF);
         var parent = new EditorViewModel(new StubFileSystem(), Singletons.WorkDispatcher) { ViewPort };
         ViewPort.Edit("@00 <020> <100> ");
         ViewPort.Edit("@20!lz(1024) ^tileset`lzt4` ");
         ViewPort.Edit("@100!lz(32)  ^pal`lzp4` ");

         ViewPort.Tools.SpriteTool.OpenInImageTab.Execute();

         var editor = (ImageEditorViewModel)parent.SelectedTab;
         Assert.Equal(4, editor.PalettePointer);
      }
   }

   public class ImageEditorRealDataTests {
      private byte[] pal = "00 00 00 00 1F 02 1F 21 70 7A 42 7C 32 4A FF 7F 00 03 16 36 1B 70 F3 7F 9C 73 8E 14 08 26 19 2A".ToByteArray();
      private byte[] tileset = "10 00 08 00 33 00 00 F0 01 90 01 77 77 F0 01 D0 01 09 37 33 33 73 F0 03 33 73 50 1F 09 F7 FF FF 7F F0 03 FF 7F 50 1F 09 27 22 22 72 F0 03 22 72 50 1F 09 57 55 55 75 F0 03 55 75 50 1F 09 67 66 66 76 F0 03 66 76 10 1B 30 88 88 F0 01 90 01 77 97 79 77 9F 50 03 99 99 30 01 90 13 90 E7 60 1F 90 14 FF 90 2B F0 03 F0 03 F0 01 A0 61 30 03 F0 01 F0 1D E7 F0 01 F0 8F B0 03 99 99 30 03 F0 2F 50 03 FF 10 FB F0 61 70 2B 40 1F F0 CF 40 9F 40 5F F0 7B FF B1 5F F0 7F B0 9F F0 3B B0 9F F0 7F B1 9F C0 1F 09 A7 AA AA 7A F0 03 AA 7A 10 1B 33 BB BB F0 01 90 01 CC CC F0 01 90 01 3E DD DD F0 01 90 01 90 2B 50 9F A0 40 9C 67 C9 F0 03 80 03 EE EE F0 01 90 01 10 C3 09 47 44 44 74 F0 03 44 74 10 1B FF F3 D1 F0 01 F0 01 F0 01 F0 01 F0 01 F0 01 F0 01 FF F0 01 F0 01 F0 01 F0 01 F0 01 F0 01 F0 01 F0 01 FF F0 01 F0 01 F0 01 F0 01 F0 01 F0 01 F0 01 F0 01 FE F0 01 F0 01 F0 01 F0 01 F0 01 F0 01 50 01 30 13 33 33 03 F0 03 33 03 30 1D 00 06 01 30 30 03 00 03 03 33 00 12 FC 00 07 00 11 10 06 00 03 70 41 10 1A 30 00 69 30 00 07 10 05 03 20 03 00 33 50 1D 7F 03 30 4A 10 31 10 08 10 50 50 13 30 7F 30 40 ED 40 39 20 0F 00 11 03 70 3F 00 A1 00 00 5F FF 00 03 00 07 30 23 20 44 60 1F 40 03 20 A0 30 0B E1 F0 01 F0 01 50 01 50 55 55 05 F0 03 2D 55 05 80 1F 50 00 05 40 03 05 30 03 3F 55 00 00 07 70 21 30 06 40 03 00 39 00 1E D5 30 13 20 17 50 40 01 05 10 0A 05 10 41 F3 A0 3D 00 30 10 03 20 38 00 05 10 59 30 01 C3 50 20 30 8A 05 05 55 05 40 24 00 1A FF 70 9E 80 26 00 5A 00 25 10 4E 00 C1 40 20 20 90 E0 10 CF D0 A0 20 1D".ToByteArray();
      private byte[][] tilemaps = new[] {
         "10 C8 00 00 3A 07 30 F0 01 F0 01 30 01 17 00 01 01 2B 30 02 00 03 05 C0 13 0E 20 03 50 13 58 01 20 01 0A 20 03 B0 13 10 30 09 35 30 13 20 09 10 13 03 20 09 06 20 03 F8 B0 27 30 01 F0 81 F0 01 50 01".ToByteArray(),
         "10 C8 00 00 3B 07 30 F0 01 F0 01 30 01 01 80 01 50 13 56 06 00 0B 04 00 03 02 80 13 10 1F 0E C0 40 03 50 0F 0C 30 09 30 13 30 54 16 00 03 0D 60 3F 01 20 3D 0A 30 47 05 C0 3B 01 30 10 20 25 F0 81 F0 01 80 50 01".ToByteArray(),
         "10 C8 00 00 3B 07 30 F0 01 F0 01 70 01 06 40 01 50 0F 00 02 30 01 30 11 30 09 30 14 16 30 12 20 0F 01 40 01 0B 30 5A 03 00 03 0B 20 0B 10 17 05 00 07 06 F6 20 0D 70 3B 10 0D 10 3B 09 40 3B 50 13 0B F8 20 09 30 05 F0 01 F0 01 10 01".ToByteArray(),
      };

      public ViewPort ViewPort { get; }
      public IDataModel Model => ViewPort.Model;
      public ImageEditorViewModel Editor { get; private set; }
      public ImageEditorRealDataTests() {
         var data = new byte[0x1000];
         for (int i = 0; i < data.Length; i++) data[i] = 0xFF;

         pal.WriteInto(data, 0);
         tileset.WriteInto(data, 0x100);
         tilemaps[0].WriteInto(data, 0x800);
         tilemaps[1].WriteInto(data, 0x900);
         tilemaps[2].WriteInto(data, 0xA00);

         var singletons = BaseViewModelTestClass.Singletons;
         var model = new PokemonModel(data, null, singletons);
         ViewPort = new ViewPort("Name", model, singletons.WorkDispatcher, singletons);
         ViewPort.Edit("@00 ^pal`ucp4:3` @30 <000> @100 ^tileset`lzt4|pal` @B00 <800> <900> <A00> @B00 ^tilemaps[tilemap<`lzm4x10x10|tileset`>]3 @800 ");
         ViewPort.Save.Execute(new StubFileSystem { Save = file => true });
         ViewPort = new ViewPort("Name", model, singletons.WorkDispatcher, singletons); // make a new ViewPort with a new history tracker

         ViewPort.RequestTabChange += (sender, tab) => Editor = (ImageEditorViewModel)tab;
         ViewPort.OpenImageEditorTab(0x800, 0, 0);
      }

      Point point(int x, int y) => Editor.FromSpriteSpace(new Point(x << 3, y << 3));

      [Fact]
      public void TilemapTableWithSharedTileset_Edit8x8_TilesetUnchanged() {
         var tilesetData = (LzTilesetRun)Model.GetNextAnchor("tileset");
         var before = tilesetData.GetData();

         // select an 8x8 region at tile (3,3), draw that tile at (4,3)
         Editor.SelectedTool = ImageEditorTools.Draw;
         Editor.CursorSize = 8;
         Editor.EyeDropperDown(point(3, 3));
         Editor.EyeDropperUp(point(3, 3));
         Editor.ToolDown(point(3, 4));
         Editor.ToolUp(point(3, 4));

         //      check that the tileset is not edited
         tilesetData = (LzTilesetRun)Model.GetNextAnchor("tileset");
         var after = tilesetData.GetData();
         Assert.Equal(before, after);
         Assert.Equal(tilemaps[1], Model.BytesFrom(Model.GetNextRun(0x900)));
         Assert.Equal(tilemaps[2], Model.BytesFrom(Model.GetNextRun(0xA00)));
      }

      [Fact]
      public void TilemapTableWithSharedTileset_EditOneTilemap_OtherTilemapsUnchanged() {
         var oldPixels = new[] { Model.ReadSprite(0x900), Model.ReadSprite(0xA00) };
         Editor.SelectedTool = ImageEditorTools.Draw;
         Editor.Palette.SelectionStart = 1;

         Editor.ToolDown(point(4, 4));
         Editor.ToolUp(point(4, 4));

         var newPixels = new[] { Model.ReadSprite(0x900), Model.ReadSprite(0xA00) };
         Assert.Equal(oldPixels, newPixels);
      }

      [Fact]
      public void Palette_Edit_CanUndo() {
         var watcher = new EventWatcher(handler => Editor.Undo.CanExecuteChanged += handler);
         Assert.False(Editor.Undo.CanExecute(default));

         Editor.Palette.Elements[1].Color = BaseImageEditorTests.Rgb(31, 31, 31);

         Assert.Equal(1, watcher.Count);
      }
   }
}
