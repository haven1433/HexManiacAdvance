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

      private short Rgb(int r, int g, int b) => (short)((r << 10) | (g << 5) | b);
      private short GetPixel(int x, int y) => editor.PixelData[editor.PixelIndex(new Point(x, y))];
      private (int r, int g, int b) Rgb(short color) => (color >> 10, (color >> 5) & 31, color & 31);

      public ImageEditorTests() {
         model = new PokemonModel(new byte[0x200], singletons: BaseViewModelTestClass.Singletons);
         history = new ChangeHistory<ModelDelta>(RevertHistoryChange);

         model.WritePointer(history.CurrentChange, 0x80, 0);
         model.WritePointer(history.CurrentChange, 0x88, 0x40);

         var sprite = new SpriteRun(0, new SpriteFormat(4, 1, 1, "palette"), new SortedSpan<int>(0x80));
         model.ObserveAnchorWritten(history.CurrentChange, "sprite", sprite);

         var palette = new PaletteRun(0x40, new PaletteFormat(4, 1), new SortedSpan<int>(0x88));
         model.ObserveAnchorWritten(history.CurrentChange, "palette", palette);

         editor = new ImageEditorViewModel(history, model, 0);
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
         Assert.Equal(1, ((ISpriteRun)model.GetNextRun(0)).GetPixels(model, 0)[4, 4]);
         Assert.Equal(Rgb(31, 31, 31), ((IPaletteRun)model.GetNextRun(0x40)).GetPalette(model, 0)[1]);
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
         var source = editor.SpritePointer;
         var destination = model.ReadPointer(editor.SpritePointer);
         var spriteRun = (ISpriteRun)model.GetNextRun(destination);

         model.RelocateForExpansion(history.CurrentChange, spriteRun, spriteRun.Length + 1);

         // if this doesn't throw, we're happy
         editor.SelectedTool = ImageEditorTools.Draw;
         ToolMove(new Point());
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

         Assert.True(editor.ShowSelectionRect(8, 8));
         Assert.True(editor.ShowSelectionRect(8, 9));
         Assert.True(editor.ShowSelectionRect(9, 8));
         Assert.True(editor.ShowSelectionRect(9, 9));
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
   }
}
