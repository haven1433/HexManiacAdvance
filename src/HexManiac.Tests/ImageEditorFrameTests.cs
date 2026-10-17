using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// The image editor can show a group of pictures (the frames of an animation) in one tab:
   /// the dots on the left select the frame, and the palette has a row of its own.
   /// </summary>
   public class ImageEditorFrameTests {
      private const int PaletteStart = 0x000, FrameStart = 0x300, FrameTable = 0x400, PalettePointer = 0x410;
      private const int FrameCount = 3;

      private readonly IDataModel model = new PokemonModel(new byte[0x800], singletons: BaseViewModelTestClass.Singletons);
      private readonly ChangeHistory<ModelDelta> history;
      private readonly TestFrameSource source;
      private readonly ImageEditorViewModel editor;

      /// <summary>Three 8x8 pictures stored in the model (so that undo can take their changes back), reached through a table of pointers.</summary>
      private class TestFrameSource : IImageFrameSource {
         private readonly IDataModel model;
         public bool Valid { get; set; } = true;

         public TestFrameSource(IDataModel model) => this.model = model;

         public string Title => "Test frames";
         public bool IsValid => Valid;
         public int FrameCount => ImageEditorFrameTests.FrameCount;
         public string FrameNote(int frame) => frame == 2 ? "the same picture as frame 1" : string.Empty;
         public int SpritePointer(int frame) => FrameTable + 4 * frame;
         public bool CanChooseWidth => false;
         public int DefaultWidthTiles => 1;
         public int MaxWidthTiles => 1;

         public int[,] ReadFrame(int frame, int widthTiles) {
            var address = model.ReadPointer(SpritePointer(frame));
            var pixels = new int[8, 8];
            for (int y = 0; y < 8; y++) {
               for (int x = 0; x < 8; x++) {
                  var b = model[address + y * 4 + x / 2];
                  pixels[x, y] = x % 2 == 0 ? b & 0xF : b >> 4;
               }
            }
            return pixels;
         }

         public void WriteFrame(ModelDelta token, int frame, int widthTiles, int[,] pixels) {
            var address = model.ReadPointer(SpritePointer(frame));
            for (int y = 0; y < 8; y++) {
               for (int x = 0; x < 8; x += 2) {
                  var value = (byte)((pixels[x, y] & 0xF) | ((pixels[x + 1, y] & 0xF) << 4));
                  if (model[address + y * 4 + x / 2] != value) token.ChangeData(model, address + y * 4 + x / 2, value);
               }
            }
         }
      }

      public ImageEditorFrameTests() {
         history = new ChangeHistory<ModelDelta>(change => change.Revert(model));
         var token = history.CurrentChange;
         model.WritePointer(token, PalettePointer, PaletteStart);
         model.ObserveAnchorWritten(token, "palette", new PaletteRun(PaletteStart, new PaletteFormat(4, 16), SortedSpan.One(PalettePointer)));
         for (int f = 0; f < FrameCount; f++) {
            model.WritePointer(token, FrameTable + 4 * f, FrameStart + 32 * f);
            model.ObserveRunWritten(token, new TilesetRun(new TilesetFormat(4, 1, -1, "palette"), model, FrameStart + 32 * f, SortedSpan.One(FrameTable + 4 * f)));
         }
         history.ChangeCompleted(); // setting up is not something the editor's undo should take back

         editor = new ImageEditorViewModel(history, model, FrameStart) { SpriteScale = 1 };
         source = new TestFrameSource(model);
         editor.SetFrameSource(source);
      }

      private static short Rgb(int r, int g, int b) => (short)((r << 10) | (g << 5) | b);

      private void Draw(int paletteIndex, int pixelX, int pixelY) {
         editor.SelectedTool = ImageEditorTools.Draw;
         editor.Palette.SelectionStart = paletteIndex;
         var point = new Point(pixelX - 4, pixelY - 4); // the middle of the 8x8 picture is (0, 0)
         editor.ToolDown(point);
         editor.ToolUp(point);
      }

      [Fact]
      public void PlainEditor_HasNoFrames() {
         var plain = new ImageEditorViewModel(history, model, FrameStart);

         Assert.False(plain.HasFrames);
         Assert.Empty(plain.FrameOptions);
         Assert.False(plain.ShowPaletteCaption);
         Assert.Equal(string.Empty, plain.FrameCaption);
         Assert.Equal("Image Editor", plain.Name);
      }

      [Fact]
      public void FrameSource_OneDotPerFrame_FirstFrameSelected() {
         Assert.True(editor.HasFrames);
         Assert.Equal("Test frames", editor.Name);
         Assert.Equal(3, editor.FrameOptions.Count);
         Assert.Equal(new[] { "1", "2", "3" }, editor.FrameOptions.Select(option => option.Name));
         Assert.Equal(new[] { true, false, false }, editor.FrameOptions.Select(option => option.Selected));
         Assert.Equal("Frame 1 of 3", editor.FrameCaption);
         Assert.Equal("Frame 2", editor.FrameOptions[1].Description);
         Assert.Equal("Frame 3 (the same picture as frame 1)", editor.FrameOptions[2].Description);
         Assert.False(editor.CanEditTilesetWidth);
         Assert.Equal(8, editor.PixelWidth);
         Assert.Equal(8, editor.PixelHeight);
      }

      [Fact]
      public void PaletteHasDotsOfItsOwn() {
         Assert.Equal(16, editor.PalettePages);
         Assert.True(editor.ShowPaletteCaption);
         Assert.Equal("Palette 0", editor.PaletteCaption);

         editor.PalettePageOptions[5].Selected = true;

         Assert.Equal(5, editor.PalettePage);
         Assert.Equal("Palette 5", editor.PaletteCaption);
         Assert.Equal(0, editor.Frame);
         Assert.Equal(3, editor.FrameOptions.Count);
      }

      [Fact]
      public void SelectingADot_ShowsThatFramesPicture() {
         model[FrameStart + 32 * 1] = 0x0A; // frame 1: pixel (0, 0) is color 10

         editor.FrameOptions[1].Selected = true;

         Assert.Equal(1, editor.Frame);
         Assert.Equal(10, editor.ReadRawPixel(0, 0));
         Assert.Equal("Frame 2 of 3", editor.FrameCaption);
         Assert.True(editor.FrameOptions[1].Selected);
         Assert.False(editor.FrameOptions[0].Selected);

         editor.FrameOptions[0].Selected = true;

         Assert.Equal(0, editor.Frame);
         Assert.Equal(0, editor.ReadRawPixel(0, 0));
      }

      [Fact]
      public void Frame_StaysWithinTheFrames_AndStepFrameWrapsAround() {
         editor.Frame = 7;
         Assert.Equal(2, editor.Frame);

         editor.StepFrame(1);
         Assert.Equal(0, editor.Frame);

         editor.StepFrame(-1);
         Assert.Equal(2, editor.Frame);
      }

      [Fact]
      public void Drawing_ChangesOnlyTheSelectedFrame() {
         editor.Frame = 1;

         Draw(5, 3, 4);

         Assert.Equal(5, editor.ReadRawPixel(3, 4));
         Assert.Equal(5, source.ReadFrame(1, 1)[3, 4]);
         Assert.Equal(0, source.ReadFrame(0, 1)[3, 4]);
         Assert.Equal(0, source.ReadFrame(2, 1)[3, 4]);
      }

      [Fact]
      public void ChoosingAPalette_DoesNotChangeTheFrame() {
         editor.Frame = 2;

         editor.PalettePage = 3;

         Assert.Equal(2, editor.Frame);
         Assert.Equal(3, editor.PalettePage);
         Assert.True(editor.FrameOptions[2].Selected);
      }

      [Fact]
      public void Undo_ShowsTheFrameThatWasChanged() {
         editor.Frame = 2;
         Draw(6, 1, 1);
         editor.Frame = 0;
         Draw(7, 2, 2);

         editor.Undo.Execute();

         Assert.Equal(0, editor.Frame);
         Assert.Equal(0, source.ReadFrame(0, 1)[2, 2]);
         Assert.Equal(6, source.ReadFrame(2, 1)[1, 1]);

         editor.Undo.Execute();

         Assert.Equal(2, editor.Frame); // the stroke taken back was on another frame: the editor shows it
         Assert.Equal(0, source.ReadFrame(2, 1)[1, 1]);

         editor.Redo.Execute();

         Assert.Equal(2, editor.Frame);
         Assert.Equal(6, editor.ReadRawPixel(1, 1));
      }

      [Fact]
      public void Paste_GoesIntoTheSelectedFrame() {
         var black = Rgb(0, 0, 0);
         var red = Rgb(31, 0, 0);
         var blue = Rgb(0, 0, 31);
         var white = Rgb(31, 31, 31);
         var fileSystem = new StubFileSystem();
         fileSystem.CopyImage = (new short[] { black, red, blue, white }, 2);
         editor.Palette.Elements[1].Color = red;
         editor.Palette.Elements[2].Color = blue;
         editor.Palette.Elements[3].Color = white;
         editor.Frame = 1;

         editor.Paste.Execute(fileSystem);

         // pasted content is centered
         var frame = source.ReadFrame(1, 1);
         Assert.Equal(1, frame[4, 3]);
         Assert.Equal(2, frame[3, 4]);
         Assert.Equal(3, frame[4, 4]);
         Assert.Equal(0, source.ReadFrame(0, 1)[4, 3]);
         Assert.Equal(0, source.ReadFrame(2, 1)[4, 3]);
      }

      [Fact]
      public void SourceThatIsNoLongerValid_ClosesTheEditor() {
         var closed = 0;
         editor.Closed += (sender, e) => closed++;

         source.Valid = false;
         editor.Refresh();

         Assert.Equal(1, closed);
      }
   }
}
