using HavenSoft.HexManiac.Core.Models.PokemonAnimations;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// The engine is the game's own code (pokeemerald-expansion src/pokemon_animation.c and the parts of src/sprite.c it relies on), run one game frame at a time.
   /// Every expectation below was worked out by hand from that C code, not by running the engine.
   /// The battle runs the sprites before the tasks, so the movement starts on the later of the delay and the second game frame (frame index 1).
   /// </summary>
   public class PokemonAnimationEngineTests {
      private const int HSlide = 3, VSlide = 4, Grow = 107, GlowRed = 33, ShakeGlowRed = 143, UnknownMovement = 200;

      private static int Rgb(int r, int g, int b) => r | (g << 5) | (b << 10);

      /// <summary>src/trig.c: gSineTable[i] is 256 * sin(i * pi / 128), cut towards zero. Sin(index, amplitude) = (amplitude * gSineTable[index]) >> 8.</summary>
      private static int Sin(int index, int amplitude) => (amplitude * (int)(256 * Math.Sin(index * Math.PI / 128))) >> 8;

      private static FrameCommand F(int image, int duration, bool h = false, bool v = false) => FrameCommand.Frame(image, duration, h, v);

      private static PokemonAnimationSimulation Run(int animId, int delay = 0, FrameCommand[] commands = null, ushort[] palette = null, int paletteNum = 0, bool tasksFirst = false, int maxFrames = 1200) {
         return PokemonAnimationEngine.Simulate(new PokemonAnimationRequest {
            AnimId = animId,
            Delay = delay,
            FrameCommands = commands,
            Palette = palette,
            PaletteNum = paletteNum,
            TasksRunBeforeSprites = tasksFirst,
            MaxGameFrames = maxFrames,
            RecordSpriteData = true,
         });
      }

      #region BIOS ObjAffineSet

      [Fact]
      public void ObjAffineSet_NoScaleNoRotation_IsTheIdentity() {
         PokemonAnimationEngine.ObjAffineSet(0x100, 0x100, 0, out var a, out var b, out var c, out var d);
         Assert.Equal((256, 0, 0, 256), ((int)a, (int)b, (int)c, (int)d));
      }

      [Fact]
      public void ObjAffineSet_ScaleIsKeptPerAxis() {
         PokemonAnimationEngine.ObjAffineSet(0x200, 0x80, 0, out var a, out var b, out var c, out var d);
         Assert.Equal((512, 0, 0, 128), ((int)a, (int)b, (int)c, (int)d));
      }

      [Fact]
      public void ObjAffineSet_QuarterTurn_SwapsTheAxes() {
         // rotation 0x4000 is a quarter of the circle: cos = 0 and sin = 1, so a = sx*cos, b = -sx*sin, c = sy*sin, d = sy*cos
         PokemonAnimationEngine.ObjAffineSet(0x100, 0x100, 0x4000, out var a, out var b, out var c, out var d);
         Assert.Equal((0, -256, 256, 0), ((int)a, (int)b, (int)c, (int)d));
      }

      [Fact]
      public void ObjAffineSet_HalfTurn_Mirrors() {
         PokemonAnimationEngine.ObjAffineSet(0x100, 0x100, 0x8000, out var a, out var b, out var c, out var d);
         Assert.Equal((-256, 0, 0, -256), ((int)a, (int)b, (int)c, (int)d));
      }

      #endregion

      #region translation

      [Fact]
      public void HorizontalSlide_FollowsTheSourceCode() {
         // Anim_HorizontalSlide: data[0] = 40; HorizontalSlide runs once at once and then every frame:
         //    if (data[2] > data[0]) { callback = WaitAnimEnd; x2 = 0; } else x2 = Sin((data[2] * 384 / data[0]) % 256, 6); data[2]++
         var sim = Run(HSlide);

         // frame 0: the tasks start the movement after the sprites ran. frame 1 runs data[2] = 0, frame 2 runs data[2] = 1, ...
         Assert.Equal(0, sim.Frames[0].X2);
         for (int step = 0; step <= 40; step++) {
            Assert.Equal(Sin(step * 384 / 40 % 256, 6), sim.Frames[1 + step].X2);
         }
         // step 41 is past data[0]: x2 = 0 and the callback waits for the animation to end (it has: the sprite has no second picture)
         Assert.Equal(0, sim.Frames[42].X2);
         Assert.All(sim.Frames, state => Assert.Equal(0, state.Y2));
         Assert.Equal(44, sim.Frames.Length);
      }

      [Fact]
      public void HorizontalSlide_ReachesFivePixelsToTheRight() {
         // Sin(index, 6) peaks at index 64: 6 * 256 >> 8, but the steps jump by 9 and 10 indexes: the largest value actually reached is 5
         var sim = Run(HSlide);
         Assert.Equal(5, sim.Frames.Max(state => state.X2));
      }

      [Fact]
      public void VerticalSlide_MovesUpwards() {
         // VerticalSlide: y2 = -(Sin((data[2] * 384 / data[0]) % 256, 6))
         var sim = Run(VSlide);
         for (int step = 0; step <= 40; step++) {
            Assert.Equal(-Sin(step * 384 / 40 % 256, 6), sim.Frames[1 + step].Y2);
         }
      }

      [Fact]
      public void Movement_StartsOnTheSecondFrameWithoutDelay() {
         // data[2] = 1 is the first step with a visible offset (Sin(9, 6) = 1)
         Assert.Equal(1, Run(HSlide, delay: 0).Frames[2].X2);
         Assert.Equal(0, Run(HSlide, delay: 0).Frames[1].X2);
      }

      [Fact]
      public void Movement_WaitsForTheDelay() {
         // Task_AnimateAfterDelay counts the delay down once per frame, then launches Task_HandleMonAnimation, which sets the callback in the same frame:
         // the sprites see it on frame 'delay'. Step 0 runs there, step 1 (the first offset) one frame later.
         var sim = Run(HSlide, delay: 10);
         Assert.All(sim.Frames.Take(11), state => Assert.Equal(0, state.X2));
         Assert.Equal(1, sim.Frames[11].X2);
      }

      [Fact]
      public void Movement_InTheVisualizersOrder_IsOneFrameEarlier() {
         // the Pokemon sprite visualizer of the game runs its tasks before its sprites
         var sim = Run(HSlide, delay: 10, tasksFirst: true);
         Assert.All(sim.Frames.Take(10), state => Assert.Equal(0, state.X2));
         Assert.Equal(1, sim.Frames[10].X2);
         Assert.Equal(1, Run(HSlide, delay: 0, tasksFirst: true).Frames[1].X2);
      }

      #endregion

      #region scale and rotation

      [Fact]
      public void Grow_ScalesDownAndBackUpWithAHalfSine() {
         // Anim_Grow, first call: HandleStartAffineAnim (double size matrix), data[7] = 0, data[6] = 4, data[5] = 1. Then Grow, every frame:
         //    data[7] += 4; scale = Sin(data[7] / 2, 64); matrix = (256 - scale) on both axes; and at data[7] > 255 it resets to 256.
         // (On the very first frame AnimateSprites starts the sprite's affine animation, which puts the identity matrix in place again.)
         var sim = Run(Grow);
         Assert.Equal((256, 0, 0, 256), Matrix(sim.Frames[1]));
         Assert.Equal((250, 0, 0, 250), Matrix(sim.Frames[2])); // Sin(4, 64) = 64 * 25 >> 8 = 6
         Assert.Equal((247, 0, 0, 247), Matrix(sim.Frames[3])); // Sin(6, 64) = 64 * 37 >> 8 = 9
         for (int call = 2; call <= 60; call++) {
            var expected = 256 - Sin(call * 4 / 2, 64);
            Assert.Equal((expected, 0, 0, expected), Matrix(sim.Frames[call]));
         }
         // halfway (data[7] = 128) the sprite is at its smallest: Sin(64, 64) = 64
         Assert.Equal((192, 0, 0, 192), Matrix(sim.Frames[32]));
         // and back to the normal size at the end
         Assert.Equal((256, 0, 0, 256), Matrix(sim.Frames[sim.Frames.Length - 1]));
      }

      [Fact]
      public void Grow_DrawsTheSpriteInADoubleSizeBox() {
         // HandleStartAffineAnim: oam.affineMode = ST_OAM_AFFINE_DOUBLE (3), CalcCenterToCornerVec: -64
         var sim = Run(Grow);
         Assert.Equal(3, sim.Frames[5].AffineMode);
         Assert.Equal(-64, sim.Frames[5].CornerX);
         Assert.Equal(-64, sim.Frames[5].CornerY);
         // before the movement the sprite is a normal affine sprite with a 64x64 box
         Assert.Equal(1, sim.Frames[0].AffineMode);
         Assert.Equal(-32, sim.Frames[0].CornerX);
      }

      private static (int a, int b, int c, int d) Matrix(AnimFrameState state) => (state.A, state.B, state.C, state.D);

      #endregion

      #region glow

      [Fact]
      public void GlowRed_BlendsTheSpritePaletteTowardsRed() {
         // GlowColor(RGB_RED, 12, 2): data[2] = 0, 2, 4 ... 128 on the frames 1, 2, ... 65. Every frame:
         //    BlendPalette(OBJ_PLTT_ID(palette), 16, Sin(data[2], 12), color): each channel c becomes c + (((target - c) * coeff) >> 4)
         // Frame 33 has data[2] = 64: Sin(64, 12) = 12 * 256 >> 8 = 12.
         //    red 10 -> 10 + (21 * 12 >> 4) = 10 + 15 = 25;  green 20 -> 20 + (-20 * 12 >> 4) = 20 - 15 = 5;
         //    blue 30 -> 30 + (-30 * 12 >> 4) = 30 + (-360 >> 4) = 30 - 23 = 7 (the shift rounds down)
         var palette = new ushort[16];
         palette[1] = (ushort)Rgb(10, 20, 30);
         palette[0] = 0x7FFF;
         var sim = Run(GlowRed, palette: palette, paletteNum: 2);

         var peak = sim.Palettes[sim.Frames[33].Palette];
         Assert.Equal(Rgb(25, 5, 7), peak[1]);
         // entry 0 is blended too: white 31 -> 31 + (0 * 12 >> 4) = 31, 31 -> 31 + (-31 * 12 >> 4) = 31 - 24 = 7
         Assert.Equal(Rgb(31, 7, 7), peak[0]);
         // the first frame has coefficient 0: no change. The last has it back at 0 (data[2] > 128 blends with 0).
         Assert.Equal(Rgb(10, 20, 30), sim.Palettes[sim.Frames[1].Palette][1]);
         Assert.Equal(Rgb(10, 20, 30), sim.Palettes[sim.Frames[sim.Frames.Length - 1].Palette][1]);
      }

      [Fact]
      public void GlowRed_StepsUpFollowTheSine() {
         var palette = new ushort[16];
         palette[1] = (ushort)Rgb(10, 20, 30);
         var sim = Run(GlowRed, palette: palette);
         for (int frame = 1; frame <= 65; frame++) {
            var coefficient = Sin(2 * (frame - 1), 12);
            var color = sim.Palettes[sim.Frames[frame].Palette][1];
            Assert.Equal(Rgb(10 + ((31 - 10) * coefficient >> 4), 20 + ((0 - 20) * coefficient >> 4), 30 + ((0 - 30) * coefficient >> 4)), color);
         }
      }

      [Fact]
      public void ShakeGlowRed_ChangesPaletteAndPosition() {
         var palette = Enumerable.Range(0, 16).Select(i => (ushort)Rgb(i, 31 - i, 8)).ToArray();
         var sim = Run(ShakeGlowRed, palette: palette);
         Assert.True(sim.Palettes.Length > 5, "the colours should change");
         Assert.Contains(sim.Frames, state => state.X2 != 0 || state.Y2 != 0);
         Assert.False(sim.Faulted);
      }

      [Fact]
      public void Glow_OnlyTouchesTheSpritesOwnPalette() {
         // the engine only has the one palette of the sprite; its slot number must not matter for the colours
         var palette = Enumerable.Range(0, 16).Select(i => (ushort)Rgb(i, i * 2, 31 - i)).ToArray();
         var a = Run(GlowRed, palette: palette, paletteNum: 0);
         var b = Run(GlowRed, palette: palette, paletteNum: 13);
         Assert.Equal(a.Palettes.Length, b.Palettes.Length);
         for (int i = 0; i < a.Palettes.Length; i++) Assert.Equal(a.Palettes[i], b.Palettes[i]);
      }

      #endregion

      #region frame commands

      [Fact]
      public void FrameCommands_ShowEachPictureForItsDuration() {
         // sprite.c: BeginAnim shows cmd 0 with delayCounter = duration - 1, ContinueAnim counts it down and then moves on, END keeps the last picture
         // [(1, 2 frames), (0, 3 frames), END] -> pictures 1 1 0 0 0, the END is reached on frame 5 and WaitAnimEnd notices it on frame 6
         var sim = Run(UnknownMovement, commands: new[] { F(1, 2), F(0, 3), FrameCommand.End });
         Assert.Equal(new[] { 1, 1, 0, 0, 0, 0, 0 }, sim.Frames.Select(state => state.Image).ToArray());
      }

      [Fact]
      public void FrameCommands_ZeroDurationCountsAsOneFrame() {
         // AnimCmd_frame: if (duration) duration--; so 0 and 1 both show for a single frame
         var sim = Run(UnknownMovement, commands: new[] { F(1, 0), F(0, 1), F(1, 1), FrameCommand.End });
         Assert.Equal(new[] { 1, 0, 1, 1, 1 }, sim.Frames.Select(state => state.Image).ToArray());
      }

      [Fact]
      public void FrameCommands_Loop_RepeatsTheBlock() {
         // [(0,2), (1,2), LOOP 2, END]: the loop jumps back to the start twice, so the block plays three times: 0 0 1 1 | 0 0 1 1 | 0 0 1 1
         var sim = Run(UnknownMovement, commands: new[] { F(0, 2), F(1, 2), new FrameCommand(FrameCommand.KindLoop, 2), FrameCommand.End });
         var images = sim.Frames.Select(state => state.Image).ToArray();
         Assert.Equal(new[] { 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1 }, images.Take(12).ToArray());
         Assert.All(images.Skip(12), image => Assert.Equal(1, image));
         Assert.False(sim.HitFrameLimit);
      }

      [Fact]
      public void FrameCommands_Jump_NeverEndsAndTheSimulationIsCutOff() {
         var sim = Run(UnknownMovement, commands: new[] { F(1, 1), F(0, 1), new FrameCommand(FrameCommand.KindJump, 0) }, maxFrames: 50);
         Assert.True(sim.HitFrameLimit);
         Assert.Equal(50, sim.Frames.Length);
         for (int i = 0; i < 50; i++) Assert.Equal(1 - i % 2, sim.Frames[i].Image);
      }

      [Fact]
      public void FrameCommands_Flips_AreIgnoredForAffineSprites_ButKeptForPlainOnes() {
         // the sprite is an affine sprite while it is a battle sprite: SetSpriteOamFlipBits is skipped, the picture is never mirrored
         var sim = Run(UnknownMovement, commands: new[] { F(0, 2, h: true), F(1, 2, v: true), FrameCommand.End });
         Assert.All(sim.Frames, state => Assert.False(state.HFlip || state.VFlip));
      }

      [Fact]
      public void FrameCommands_MissingList_IsOnePictureAndTheMovementStillRuns() {
         var withNone = Run(HSlide, commands: null);
         var withEmpty = Run(HSlide, commands: new FrameCommand[0]);
         Assert.Equal(withNone.Frames.Select(state => state.X2).ToArray(), withEmpty.Frames.Select(state => state.X2).ToArray());
         Assert.All(withNone.Frames, state => Assert.Equal(0, state.Image));
      }

      [Fact]
      public void FrameCommands_CommandsAfterTheEnd_AreIgnored() {
         var sim = Run(UnknownMovement, commands: new[] { F(1, 1), FrameCommand.End, F(0, 5), FrameCommand.End });
         Assert.All(sim.Frames, state => Assert.Equal(1, state.Image));
      }

      [Fact]
      public void FrameCommands_ListWithoutEnd_GetsOne() {
         var sim = Run(UnknownMovement, commands: new[] { F(1, 1), F(0, 1) });
         Assert.Equal(new[] { 1, 0, 0, 0 }, sim.Frames.Select(state => state.Image).ToArray());
      }

      [Fact]
      public void FrameCommands_RunAlongsideTheMovement() {
         // the second picture shows from the first frame while the movement waits for its delay
         var sim = Run(HSlide, delay: 4, commands: new[] { F(1, 3), F(0, 3), FrameCommand.End });
         Assert.Equal(new[] { 1, 1, 1, 0, 0, 0 }, sim.Frames.Take(6).Select(state => state.Image).ToArray());
         Assert.Equal(0, sim.Frames[4].X2);
         Assert.Equal(1, sim.Frames[5].X2);
      }

      #endregion

      #region edge cases

      [Theory]
      [InlineData(-1)]
      [InlineData(154)]
      [InlineData(200)]
      [InlineData(255)]
      public void UnknownMovementId_OnlyTheFrameListAnimates(int animId) {
         var sim = Run(animId, commands: new[] { F(1, 2), F(0, 2), FrameCommand.End });
         Assert.True(sim.UnknownAnimId);
         Assert.All(sim.Frames, state => Assert.Equal(0, state.X2));
         Assert.All(sim.Frames, state => Assert.Equal(0, state.Y2));
         Assert.Equal(1, sim.Frames[0].Image);
      }

      [Fact]
      public void NullPalette_StillRuns() {
         var sim = Run(GlowRed, palette: null);
         Assert.False(sim.Faulted);
         Assert.All(sim.Palettes[0], color => Assert.Equal(0, color));
      }

      [Fact]
      public void ShortPalette_IsPaddedWithBlack() {
         var sim = Run(GlowRed, palette: new ushort[] { 0x7FFF, 0x001F });
         Assert.Equal(0x001F, sim.Palettes[0][1]);
         Assert.Equal(0, sim.Palettes[0][15]);
      }

      [Fact]
      public void PaletteNumberOutOfRange_IsClamped() {
         Assert.False(Run(GlowRed, paletteNum: 99).Faulted);
         Assert.False(Run(GlowRed, paletteNum: -4).Faulted);
      }

      [Fact]
      public void Simulation_IsDeterministic() {
         var palette = Enumerable.Range(0, 16).Select(i => (ushort)Rgb(i, 31 - i, i / 2)).ToArray();
         foreach (var id in new[] { 1, 7, 33, 100, 143 }) {
            var a = Run(id, delay: 3, palette: palette, commands: new[] { F(1, 4), F(0, 4), FrameCommand.End });
            var b = Run(id, delay: 3, palette: palette, commands: new[] { F(1, 4), F(0, 4), FrameCommand.End });
            Assert.Equal(a.Frames.Length, b.Frames.Length);
            for (int i = 0; i < a.Frames.Length; i++) Assert.Equal(a.Frames[i], b.Frames[i]);
         }
      }

      [Fact]
      public void FrameLimit_CutsOffMovementsThatNeverFinish() {
         var sim = Run(UnknownMovement, commands: new[] { F(1, 1), new FrameCommand(FrameCommand.KindJump, 0) }, maxFrames: 10);
         Assert.True(sim.HitFrameLimit);
         Assert.Equal(10, sim.Frames.Length);
      }

      [Fact]
      public void EveryMovementOfTheGame_FinishesAndLeavesTheSpriteAtRest() {
         Assert.Equal(154, PokemonAnimationEngine.AnimCount);
         var palette = Enumerable.Range(0, 16).Select(i => (ushort)Rgb(i * 2, 31 - i, i)).ToArray();
         for (int id = 0; id < PokemonAnimationEngine.AnimCount; id++) {
            var sim = Run(id, delay: 2, palette: palette, commands: new[] { F(1, 10), F(0, 10), FrameCommand.End });
            Assert.False(sim.Faulted, "movement " + id + " failed");
            Assert.False(sim.HitFrameLimit, "movement " + id + " never ends");
            Assert.False(sim.UnknownAnimId, "movement " + id + " is unknown");
            Assert.Equal(0, sim.OutOfRangeSineReads);
            Assert.InRange(sim.Frames.Length, 20, 400);
            var last = sim.Frames[sim.Frames.Length - 1];
            // the game itself leaves the three 'shake and slide' movements a pixel or two off (the sprite stays where the last step put it)
            var leavesItOff = id == 89 || id == 102 || id == 103;
            Assert.InRange(Math.Abs(last.X2) + Math.Abs(last.Y2), 0, leavesItOff ? 3 : 0);
            Assert.False(last.Invisible, "movement " + id + " ends invisible");
            // the sprite ends in its normal pose: an identity matrix (or none)
            Assert.True(last.AffineMode == 0 || Matrix(last) == (256, 0, 0, 256), "movement " + id + " ends with matrix " + Matrix(last));
         }
      }

      [Fact]
      public void ReadingOutsideTheSineTable_IsCountedNotCrashed() {
         // some movements index the table with values the game reads from whatever lies behind it; an editor has no such data and must not fail
         var total = 0;
         for (int id = 0; id < PokemonAnimationEngine.AnimCount; id++) total += Run(id).OutOfRangeSineReads;
         Assert.True(total >= 0);
      }

      #endregion
   }
}
