using HavenSoft.HexManiac.Core.Models.PokemonAnimations;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using System;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// The timeline turns the engine's game frames into a short list of pictures with durations; the renderer draws a sprite the way the GBA's object layer does.
   /// </summary>
   public class PokemonAnimationTimelineTests : BaseViewModelTestClass {
      private const int Canvas = 96, HSlide = 3;

      public PokemonAnimationTimelineTests() : base(0x400) { }

      private static FrameCommand F(int image, int duration) => FrameCommand.Frame(image, duration);

      /// <summary>HexManiac draws colours with red in the high bits, the GBA with red in the low bits.</summary>
      private static short Hma(int r, int g, int b) => (short)((r << 10) | (g << 5) | b);

      private static byte[] Picture(Func<int, int, int> index) {
         var result = new byte[64 * 64];
         for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) result[y * 64 + x] = (byte)index(x, y);
         return result;
      }

      private static readonly short Backdrop = 0x7C1F;

      private static ushort[] Palette(int firstR = 31) {
         var palette = new ushort[16];
         for (int i = 1; i < 16; i++) palette[i] = (ushort)((i * 2) | ((i + 3) << 5) | ((firstR - i) << 10)); // GBA order
         return palette;
      }

      private static PokemonAnimationSimulation Simulate(int animId, FrameCommand[] commands = null, int delay = 0) {
         return PokemonAnimationEngine.Simulate(new PokemonAnimationRequest { AnimId = animId, Delay = delay, FrameCommands = commands, Palette = Palette() });
      }

      private static short[] Render(AnimFrameState state, byte[][] images, short backdrop = 0x7C1F) {
         var palette = Enumerable.Range(0, 16).Select(i => Hma(i, 31 - i, i / 2)).ToArray();
         return PokemonAnimationRenderer.Render(state, images, palette, backdrop, false, Canvas);
      }

      private static AnimFrameState Still(int x2 = 0, int y2 = 0, int image = 0, int mode = 1, short a = 256, short b = 0, short c = 0, short d = 256, bool hFlip = false, bool vFlip = false, bool invisible = false) {
         var corner = mode == 3 ? -64 : -32;
         return new AnimFrameState(image, x2, y2, corner, corner, mode, a, b, c, d, invisible, hFlip, vFlip, 0);
      }

      private static int Painted(short[] canvas) => canvas.Count(pixel => pixel != Backdrop);

      #region timeline

      [Fact]
      public void Timeline_RoundsTheCumulativeTime_AndHoldsTheRestingPicture() {
         // [(1, 6 frames), (0, 6 frames), END] with a movement the game does not know: frames 0-5 show picture 1, the frames 6-13 show picture 0 (END is reached on frame 12,
         // and noticed on frame 13), so 14 game frames: 6 -> 100 ms, 14 -> 233 ms (233.33 rounded), the second picture gets the difference 133 ms.
         var simulation = Simulate(200, new[] { F(1, 6), F(0, 6), FrameCommand.End });
         var images = new[] { Picture((x, y) => 1), Picture((x, y) => 2) };

         var timeline = PokemonAnimationTimeline.Build(simulation, images, 200, Canvas, 700, true);

         Assert.Equal(2, timeline.Frames.Count);
         Assert.Equal(new[] { 100, 133 + 700 }, timeline.DurationsMs.ToArray());
         Assert.Equal(14, timeline.GameFrames);
         Assert.Equal(233 + 700, timeline.TotalMilliseconds);
      }

      [Fact]
      public void Timeline_IdleTimeIsPartOfTheLastPicture() {
         var simulation = Simulate(HSlide);
         var images = new[] { Picture((x, y) => 1) };
         var without = PokemonAnimationTimeline.Build(simulation, images, HSlide, Canvas, 0, true);
         var with = PokemonAnimationTimeline.Build(simulation, images, HSlide, Canvas, 3000, true);
         Assert.Equal(without.Frames.Count, with.Frames.Count);
         Assert.Equal(without.TotalMilliseconds + 3000, with.TotalMilliseconds);
         Assert.Equal(without.DurationsMs.Last() + 3000, with.DurationsMs.Last());
      }

      [Fact]
      public void Timeline_TotalTimeIsTheRoundedTimeOfAllGameFrames() {
         // the durations never drift: the sum is the rounded total, whatever the number of frames
         var simulation = Simulate(HSlide);
         var timeline = PokemonAnimationTimeline.Build(simulation, new[] { Picture((x, y) => 1) }, HSlide, Canvas, 0, true);
         Assert.Equal(simulation.Frames.Length, timeline.GameFrames);
         Assert.Equal((int)Math.Round(simulation.Frames.Length * 1000.0 / 60, MidpointRounding.AwayFromZero), timeline.TotalMilliseconds);
      }

      [Fact]
      public void Timeline_MergesFramesThatLookTheSame() {
         var simulation = Simulate(HSlide);
         var timeline = PokemonAnimationTimeline.Build(simulation, new[] { Picture((x, y) => 1) }, HSlide, Canvas, 0, true);
         Assert.True(timeline.Frames.Count < simulation.Frames.Length, "frames 0 and 1 (x2 = 0) and the stretch at x2 = 5 are the same picture");
         Assert.Equal(timeline.Frames.Count, timeline.DurationsMs.Count);
         Assert.All(timeline.DurationsMs, duration => Assert.True(duration > 0));
      }

      [Fact]
      public void Timeline_PicturesAreSquareCanvases() {
         var timeline = PokemonAnimationTimeline.Build(Simulate(HSlide), new[] { Picture((x, y) => 1) }, HSlide, Canvas, 0, true);
         Assert.Equal(Canvas, timeline.CanvasSize);
         Assert.All(timeline.Frames, frame => {
            Assert.Equal(Canvas, frame.PixelWidth);
            Assert.Equal(Canvas, frame.PixelHeight);
            Assert.Equal(Canvas * Canvas, frame.PixelData.Length);
         });
      }

      [Fact]
      public void Timeline_Opaque_HasNoTransparentColour_AndShowsColourZero() {
         var simulation = Simulate(HSlide);
         var timeline = PokemonAnimationTimeline.Build(simulation, new[] { Picture((x, y) => 0) }, HSlide, Canvas, 0, true);
         Assert.All(timeline.Frames, frame => Assert.Equal(-1, frame.Transparent));
         // an empty picture: everything is the palette's first colour (here black, the palette given to the simulation has 0 there)
         Assert.All(timeline.Frames[0].PixelData, pixel => Assert.Equal(0, pixel));
      }

      [Fact]
      public void Timeline_NotOpaque_MarksTheBackgroundWithAColourThePaletteDoesNotUse() {
         var timeline = PokemonAnimationTimeline.Build(Simulate(HSlide), new[] { Picture((x, y) => x < 32 ? 1 : 0) }, HSlide, Canvas, 0, false);
         var transparent = timeline.Frames[0].Transparent;
         Assert.NotEqual((short)-1, transparent);
         var palette = Palette().Select(color => PaletteSwap(color)).ToArray();
         Assert.DoesNotContain(transparent, palette);
         Assert.Equal(transparent, timeline.Frames[0].PixelData[0]);
      }

      private static short PaletteSwap(ushort gba) => (short)(((gba & 31) << 10) | (gba & 0x3E0) | ((gba >> 10) & 31));

      [Fact]
      public void Timeline_SingleStaticPicture_StillHasOneFrame() {
         var timeline = PokemonAnimationTimeline.Build(PokemonAnimationEngine.Simulate(new PokemonAnimationRequest { AnimId = 200, MaxGameFrames = 1 }), new[] { Picture((x, y) => 1) }, 200, Canvas, 500, true);
         Assert.Single(timeline.Frames);
         Assert.True(timeline.TotalMilliseconds >= 500);
      }

      [Fact]
      public void Timeline_UnknownMovement_IsNotFaithful_ButStillAnimatesThePictures() {
         var timeline = PokemonAnimationTimeline.Build(Simulate(200, new[] { F(1, 3), F(0, 3), FrameCommand.End }), new[] { Picture((x, y) => 1), Picture((x, y) => 2) }, 200, Canvas, 0, true);
         Assert.False(timeline.IsFaithful);
         Assert.Equal(2, timeline.Frames.Count);
      }

      [Fact]
      public void Timeline_KnownMovement_IsFaithful() {
         var timeline = PokemonAnimationTimeline.Build(Simulate(HSlide), new[] { Picture((x, y) => 1) }, HSlide, Canvas, 0, true);
         Assert.True(timeline.IsFaithful);
      }

      [Fact]
      public void Timeline_CanvasSize_IsLimited() {
         var small = PokemonAnimationTimeline.Build(Simulate(HSlide), new[] { Picture((x, y) => 1) }, HSlide, 8, 0, true);
         var large = PokemonAnimationTimeline.Build(Simulate(HSlide), new[] { Picture((x, y) => 1) }, HSlide, 4000, 0, true);
         Assert.Equal(64, small.CanvasSize);
         Assert.Equal(256, large.CanvasSize);
      }

      [Fact]
      public void Timeline_Memory_StaysSmall() {
         // a long animation with a glow: still only a few hundred pictures of 96x96 at 2 bytes a pixel
         var timeline = PokemonAnimationTimeline.Build(Simulate(143, new[] { F(1, 10), F(0, 10), FrameCommand.End }), new[] { Picture((x, y) => 1 + x % 15), Picture((x, y) => 1 + y % 15) }, 143, Canvas, 700, true);
         Assert.True(timeline.Frames.Count <= 400);
         Assert.True(timeline.ApproximateBytes <= 400L * Canvas * Canvas * 2);
      }

      [Fact]
      public void TryBuild_WithoutAPokemonTable_Fails() {
         Assert.False(PokemonAnimationTimeline.TryBuild(Model, 0, Canvas, 0, out var timeline));
         Assert.Null(timeline);
         Assert.False(PokemonAnimationTimeline.TryBuild(null, 0, Canvas, 0, out timeline));
         Assert.Null(timeline);
      }

      [Fact]
      public void TryBuild_NegativeOrHugeSpecies_Fails() {
         Assert.False(PokemonAnimationTimeline.TryBuild(Model, -5, Canvas, 0, out _));
         Assert.False(PokemonAnimationTimeline.TryBuild(Model, int.MaxValue, Canvas, 0, out _));
      }

      #endregion

      #region renderer

      [Fact]
      public void Render_PutsTheSpriteInTheMiddleOfTheCanvas() {
         // a 64x64 sprite on a 96x96 canvas starts at (16, 16)
         var canvas = Render(Still(), new[] { Picture((x, y) => 1) });
         Assert.Equal(64 * 64, Painted(canvas));
         Assert.NotEqual(Backdrop, canvas[16 * Canvas + 16]);
         Assert.NotEqual(Backdrop, canvas[79 * Canvas + 79]);
         Assert.Equal(Backdrop, canvas[15 * Canvas + 16]);
         Assert.Equal(Backdrop, canvas[16 * Canvas + 80]);
      }

      [Fact]
      public void Render_UsesThePaletteEntryOfEveryPixel() {
         var canvas = Render(Still(), new[] { Picture((x, y) => x / 4) });
         for (int x = 0; x < 64; x += 4) {
            // palette entry 0 is transparent: index 0 shows nothing
            var expected = x == 0 ? Backdrop : Hma(x / 4, 31 - x / 4, x / 8);
            Assert.Equal(expected, canvas[40 * Canvas + 16 + x]);
         }
      }

      [Fact]
      public void Render_AppliesTheOffsets() {
         var canvas = Render(Still(x2: 5, y2: -3), new[] { Picture((x, y) => 1) });
         Assert.NotEqual(Backdrop, canvas[13 * Canvas + 21]);
         Assert.Equal(Backdrop, canvas[13 * Canvas + 20]);
         Assert.Equal(Backdrop, canvas[12 * Canvas + 21]);
         Assert.Equal(64 * 64, Painted(canvas));
      }

      [Fact]
      public void Render_Magnifies_AroundTheMiddleOfTheDoubleSizeBox() {
         // a 8x8 block in the middle of the picture: with the matrix 128 (the picture is sampled at half speed) it covers 16x16 pixels
         var picture = Picture((x, y) => x >= 28 && x < 36 && y >= 28 && y < 36 ? 1 : 0);
         Assert.Equal(64, Painted(Render(Still(), new[] { picture })));
         Assert.Equal(256, Painted(Render(Still(mode: 3, a: 128, d: 128), new[] { picture })));
      }

      [Fact]
      public void Render_Shrinks() {
         var picture = Picture((x, y) => x >= 28 && x < 36 && y >= 28 && y < 36 ? 1 : 0);
         Assert.Equal(16, Painted(Render(Still(a: 512, d: 512), new[] { picture })));
      }

      [Fact]
      public void Render_QuarterTurn_MovesAMarkerFromTheRightToTheTop() {
         // GBA: texel = matrix * (screen - centre) + centre. With (a, b, c, d) = (0, -256, 256, 0) the texel (40, 32) is at screen offset (0, -8)
         var picture = Picture((x, y) => x == 40 && y == 32 ? 1 : 0);
         var upright = Render(Still(), new[] { picture });
         Assert.NotEqual(Backdrop, upright[(16 + 32) * Canvas + 16 + 40]);
         var turned = Render(Still(b: -256, c: 256, a: 0, d: 0), new[] { picture });
         Assert.Equal(1, Painted(turned));
         Assert.NotEqual(Backdrop, turned[(16 + 24) * Canvas + 16 + 32]);
      }

      [Fact]
      public void Render_HalfTurn_MirrorsBothWays() {
         var picture = Picture((x, y) => x == 40 && y == 32 ? 1 : 0);
         var turned = Render(Still(a: -256, d: -256), new[] { picture });
         // screen offset (dx, dy) samples texel (32 - dx, 32 - dy): the texel (40, 32) shows at offset (-8, 0), that is box x = 24
         Assert.Equal(1, Painted(turned));
         Assert.NotEqual(Backdrop, turned[(16 + 32) * Canvas + 16 + 24]);
      }

      [Fact]
      public void Render_FlippedPlainSprite_IsMirrored() {
         var picture = Picture((x, y) => x == 40 && y == 10 ? 1 : 0);
         Assert.NotEqual(Backdrop, Render(Still(mode: 0), new[] { picture })[(16 + 10) * Canvas + 16 + 40]);
         Assert.NotEqual(Backdrop, Render(Still(mode: 0, hFlip: true), new[] { picture })[(16 + 10) * Canvas + 16 + 23]);
         Assert.NotEqual(Backdrop, Render(Still(mode: 0, vFlip: true), new[] { picture })[(16 + 53) * Canvas + 16 + 40]);
      }

      [Fact]
      public void Render_Invisible_DrawsNothing() {
         Assert.Equal(0, Painted(Render(Still(invisible: true), new[] { Picture((x, y) => 1) })));
      }

      [Fact]
      public void Render_PictureNumberBeyondTheSprite_UsesTheLastPicture() {
         // a sprite with a single picture asked for its second picture (a list that wants two pictures in a one-picture sprite)
         var canvas = Render(Still(image: 1), new[] { Picture((x, y) => 1) });
         Assert.Equal(64 * 64, Painted(canvas));
      }

      [Fact]
      public void Render_NoPictures_DrawsNothing() {
         Assert.Equal(0, Painted(Render(Still(), new byte[0][])));
         Assert.Equal(0, Painted(Render(Still(), null)));
      }

      [Fact]
      public void Render_FarOutsideTheCanvas_DrawsNothing() {
         Assert.Equal(0, Painted(Render(Still(x2: 500), new[] { Picture((x, y) => 1) })));
         Assert.Equal(0, Painted(Render(Still(y2: -500), new[] { Picture((x, y) => 1) })));
      }

      [Fact]
      public void Render_PartlyOutsideTheCanvas_IsCropped() {
         var canvas = Render(Still(x2: 40), new[] { Picture((x, y) => 1) });
         // the sprite starts at x = 56 and the canvas ends at 96
         Assert.Equal(40 * 64, Painted(canvas));
      }

      #endregion

      #region reading the ROM

      [Fact]
      public void SplitFrames_CutsASheetIntoPictures() {
         Assert.Single(PokemonAnimationSource.SplitFrames(new int[64, 64]));
         Assert.Equal(2, PokemonAnimationSource.SplitFrames(new int[64, 128]).Length);
         Assert.Equal(2, PokemonAnimationSource.SplitFrames(new int[64, 192]).Length);
         Assert.Null(PokemonAnimationSource.SplitFrames(new int[32, 64]));
         Assert.Null(PokemonAnimationSource.SplitFrames(new int[64, 40]));
      }

      [Fact]
      public void SplitFrames_KeepsTheRowsOfEachPicture() {
         var sheet = new int[64, 128];
         sheet[5, 3] = 7;   // first picture
         sheet[6, 64 + 9] = 0x1F; // second picture, only the palette index (low 4 bits) is kept
         var pictures = PokemonAnimationSource.SplitFrames(sheet);
         Assert.Equal(7, pictures[0][3 * 64 + 5]);
         Assert.Equal(15, pictures[1][9 * 64 + 6]);
      }

      [Fact]
      public void ToGbaPalette_SwapsRedAndBlue() {
         var colors = Enumerable.Range(0, 16).Select(i => Hma(1, 2, 3)).ToArray();
         var gba = PokemonAnimationSource.ToGbaPalette(colors);
         Assert.Equal((ushort)(1 | (2 << 5) | (3 << 10)), gba[0]);
      }

      private void WriteWord(int address, uint value) {
         for (int i = 0; i < 4; i++) Data[address + i] = (byte)(value >> (8 * i));
      }

      [Fact]
      public void ReadFrameCommands_FollowsTheSecondPointerToTheList() {
         WriteWord(0x100, 0x08000200);
         WriteWord(0x104, 0x08000210);
         WriteWord(0x210, (uint)(5 << 16) | 1);
         WriteWord(0x214, (uint)(7 << 16) | 0);
         WriteWord(0x218, 0xFFFF);
         var commands = PokemonAnimationSource.ReadFrameCommands(Model, 0x100);
         Assert.Equal(3, commands.Length);
         Assert.Equal((FrameCommand.KindFrame, 1, 5), (commands[0].Kind, commands[0].Value, commands[0].Duration));
         Assert.Equal((FrameCommand.KindFrame, 0, 7), (commands[1].Kind, commands[1].Value, commands[1].Duration));
         Assert.Equal(FrameCommand.KindEnd, commands[2].Kind);
      }

      [Fact]
      public void ReadFrameCommands_ParsesFlipsLoopsAndJumps() {
         WriteWord(0x100, 0x08000200);
         WriteWord(0x104, 0x08000210);
         WriteWord(0x210, (uint)(3 << 16) | (1u << 22) | 2);
         WriteWord(0x214, 0x0002FFFD); // loop 2
         WriteWord(0x218, 0x0000FFFE); // jump to 0
         WriteWord(0x21C, 0xFFFF);
         var commands = PokemonAnimationSource.ReadFrameCommands(Model, 0x100);
         Assert.True(commands[0].HFlip);
         Assert.False(commands[0].VFlip);
         Assert.Equal(2, commands[0].Value);
         Assert.Equal(FrameCommand.KindLoop, commands[1].Kind);
         Assert.Equal(2, commands[1].Value);
         Assert.Equal(FrameCommand.KindJump, commands[2].Kind);
         Assert.Equal(0, commands[2].Value);
      }

      [Fact]
      public void ReadFrameCommands_ListThatNeverEnds_IsNotAList() {
         WriteWord(0x100, 0x08000200);
         WriteWord(0x104, 0x08000210);
         for (int i = 0; i < 70; i++) WriteWord(0x210 + i * 4 > 0x3F0 ? 0x3F0 : 0x210 + i * 4, 1);
         Assert.Null(PokemonAnimationSource.ReadFrameCommands(Model, 0x100));
      }

      [Fact]
      public void ReadFrameCommands_BadPointers_GiveNothing() {
         Assert.Null(PokemonAnimationSource.ReadFrameCommands(Model, -4));
         Assert.Null(PokemonAnimationSource.ReadFrameCommands(Model, 0x3FC));  // the two pointers do not fit
         WriteWord(0x100, 0x08000200);
         WriteWord(0x104, 0x00000000);                                         // not a pointer
         Assert.Null(PokemonAnimationSource.ReadFrameCommands(Model, 0x100));
         WriteWord(0x104, 0x08F00000);                                         // beyond the data
         Assert.Null(PokemonAnimationSource.ReadFrameCommands(Model, 0x100));
      }

      #endregion
   }
}
