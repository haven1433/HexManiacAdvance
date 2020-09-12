using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Dynamic;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class ImageEditorTests {
      private readonly IDataModel model;
      private readonly ChangeHistory<ModelDelta> history;
      private readonly ImageEditorViewModel editor;

      private Func<ModelDelta, ModelDelta> Revert { get; set; }

      private ModelDelta RevertHistoryChange(ModelDelta change) {
         return Revert?.Invoke(change) ?? change;
      }

      private short Rgb(int r, int g, int b) => (short)((r << 10) | (g << 5) | b);
      private short GetPixel(int x, int y) => editor.PixelData[editor.PixelIndex(new Point(x, y))];
      private (int r, int g, int b) Rgb(short color) => (color >> 10, (color >> 5) & 31, color & 31);

      public ImageEditorTests() {
         model = new PokemonModel(new byte[0x200], singletons: BaseViewModelTestClass.Singletons);
         history = new ChangeHistory<ModelDelta>(RevertHistoryChange);

         var palette = new PaletteRun(0x40, new PaletteFormat(4, 1));
         model.ObserveAnchorWritten(history.CurrentChange, "palette", palette);

         var sprite = new SpriteRun(0, new SpriteFormat(4, 1, 1, "palette"));
         model.ObserveAnchorWritten(history.CurrentChange, "sprite", sprite);

         editor = new ImageEditorViewModel(history, model, 0);
      }

      [Fact]
      public void Palette_Default_NoColorsSelected() {
         Assert.Empty(editor.Palette.Elements.Where(sc => sc.Selected));
      }

      [Fact]
      public void Palette_ChangeColor_PixelsUpdate() {
         editor.Palette.Elements[0].Color = Rgb(1, 1, 1);

         Assert.Equal((1, 1, 1), Rgb(GetPixel(0, 0)));
      }

      [Fact]
      public void NewColor_Draw_PixelsChange() {
         var palette = editor.Palette;
         palette.SelectionStart = 1;
         palette.Elements[1].Color = Rgb(31, 31, 31);

         editor.ToolDown(new Point(0, 0));
         editor.ToolUp(new Point(0, 0));

         Assert.Equal((31, 31, 31), Rgb(GetPixel(4, 4)));
         Assert.Equal(1, ((ISpriteRun)model.GetNextRun(0)).GetPixels(model, 0)[4, 4]);
         Assert.Equal(Rgb(31, 31, 31), ((IPaletteRun)model.GetNextRun(0x40)).GetPalette(model, 0)[1]);
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
         editor.SelectedTool = ImageEditorViewModel.Tools.Pan;

         editor.ToolDown(new Point(0, 0));
         editor.Hover(new Point(2, 0));
         editor.ToolUp(new Point(2, 0));

         Assert.Equal(2, editor.XOffset);
      }

      [Fact]
      public void Zoom_Pan2_Offset2() {
         editor.SelectedTool = ImageEditorViewModel.Tools.Pan;

         editor.ZoomIn(new Point(0, 0));
         editor.ToolDown(new Point(0, 0));
         editor.Hover(new Point(2, 0));
         editor.ToolUp(new Point(2, 0));

         Assert.Equal(2, editor.XOffset);
      }

      [Fact]
      public void Pan2_Zoom_Offset4() {
         editor.SelectedTool = ImageEditorViewModel.Tools.Pan;

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
         editor.SelectedTool = ImageEditorViewModel.Tools.Draw;

         editor.ToolDown(new Point(0, 0));
         editor.Hover(new Point(1, 0));
         editor.ToolUp(new Point(1, 0));

         Assert.Equal((31, 31, 31), Rgb(GetPixel(4, 4)));
         Assert.Equal((31, 31, 31), Rgb(GetPixel(5, 4)));
      }

      [Fact]
      public void Fill_Blank_FillAll() {
         editor.Palette.SelectionStart = 1;
         editor.Palette.Elements[1].Color = Rgb(31, 31, 31);
         editor.SelectedTool = ImageEditorViewModel.Tools.Fill;

         editor.ToolDown(new Point(0, 0));
         editor.ToolUp(new Point(0, 0));

         Assert.All(Enumerable.Range(0, 64),
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
   }
}
