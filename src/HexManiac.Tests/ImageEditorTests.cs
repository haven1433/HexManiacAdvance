using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
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
      }
   }
}
