using HavenSoft.HexManiac.Core.ViewModels.Map;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>The map editor eases from one zoom level to the next. These tests use a clock they move by hand, so none of them waits.</summary>
   public class ZoomAnimatorTests {
      private TimeSpan now = TimeSpan.FromSeconds(100);

      private ZoomAnimator Create(double seconds = 0.25) => new ZoomAnimator(() => now, seconds);

      private void Wait(double seconds) => now += TimeSpan.FromSeconds(seconds);

      [Fact]
      public void DefaultDuration_IsAboutAThirdOfASecondOrLess() {
         Assert.InRange(ZoomAnimator.DefaultDurationSeconds, 0.05, 1.0 / 3);
      }

      [Fact]
      public void NewAnimator_IsIdle() {
         var animator = Create();
         Assert.False(animator.IsAnimating);
         Assert.Equal(animator.Target, animator.Update());
      }

      [Fact]
      public void Start_BeginsAtTheOldScale() {
         var animator = Create();
         animator.Start(1, 2);
         Assert.True(animator.IsAnimating);
         Assert.Equal(1, animator.Current);
         Assert.Equal(2, animator.Target);
         Assert.Equal(1, animator.Update());
         Assert.True(animator.IsAnimating);
      }

      [Fact]
      public void Update_AfterTheDuration_IsTheExactTargetAndStops() {
         var animator = Create(0.25);
         animator.Start(1, 3);
         Wait(0.25);
         Assert.Equal(3, animator.Update());
         Assert.False(animator.IsAnimating);
         Assert.Equal(3, animator.Current);
      }

      [Fact]
      public void Update_AFrameThatComesLate_IsTheExactTarget() {
         var animator = Create(0.25);
         animator.Start(0.5, 0.25);
         Wait(30); // the app was busy (or the window was dragged)
         Assert.Equal(0.25, animator.Update());
         Assert.False(animator.IsAnimating);
      }

      [Fact]
      public void Update_ComesBackWithoutAnimating_StaysAtTheTarget() {
         var animator = Create();
         animator.Start(1, 2);
         Wait(1);
         animator.Update();
         Wait(1);
         Assert.Equal(2, animator.Update());
         Assert.False(animator.IsAnimating);
      }

      [Fact]
      public void ZoomingIn_ScaleGrowsFrameByFrameAndNeverOvershoots() {
         var animator = Create(0.25);
         animator.Start(1, 2);
         var scales = new List<double>();
         for (int i = 0; i < 20 && animator.IsAnimating; i++) {
            Wait(0.016); // 60 frames per second
            scales.Add(animator.Update());
         }
         Assert.False(animator.IsAnimating);
         Assert.Equal(scales.OrderBy(scale => scale), scales);
         Assert.All(scales, scale => Assert.InRange(scale, 1, 2));
         Assert.Equal(2, scales.Last());
         Assert.True(scales.Count >= 10, "a quarter of a second should be several frames");
         Assert.True(scales.Count <= 17, "and it should not take longer than the duration");
      }

      [Fact]
      public void ZoomingOut_ScaleShrinksFrameByFrameAndNeverUndershoots() {
         var animator = Create(0.25);
         animator.Start(4, 3);
         var scales = new List<double>();
         for (int i = 0; i < 20 && animator.IsAnimating; i++) {
            Wait(0.016);
            scales.Add(animator.Update());
         }
         Assert.Equal(scales.OrderByDescending(scale => scale), scales);
         Assert.All(scales, scale => Assert.InRange(scale, 3, 4));
         Assert.Equal(3, scales.Last());
      }

      [Fact]
      public void EaseOut_StartsFastAndEndsSlow() {
         Assert.Equal(0, ZoomAnimator.EaseOut(0));
         Assert.Equal(1, ZoomAnimator.EaseOut(1));
         Assert.Equal(0, ZoomAnimator.EaseOut(-1));
         Assert.Equal(1, ZoomAnimator.EaseOut(2));
         Assert.True(ZoomAnimator.EaseOut(0.5) > 0.5, "ease-out is ahead of a straight line at the halfway point");
         Assert.True(ZoomAnimator.EaseOut(0.1) > 0.1);

         var first = ZoomAnimator.EaseOut(0.1) - ZoomAnimator.EaseOut(0);
         var last = ZoomAnimator.EaseOut(1) - ZoomAnimator.EaseOut(0.9);
         Assert.True(first > last * 3, "the first tenth covers much more of the zoom than the last tenth");
      }

      [Fact]
      public void EaseOut_NeverGoesBackwards() {
         var previous = 0.0;
         for (int i = 1; i <= 100; i++) {
            var value = ZoomAnimator.EaseOut(i / 100.0);
            Assert.True(value >= previous);
            previous = value;
         }
      }

      [Fact]
      public void Retarget_StartsFromTheScaleOnScreen_SoThePictureDoesNotJump() {
         var animator = Create(0.25);
         animator.Start(1, 2);
         Wait(0.1);
         var onScreen = animator.Current;
         Assert.InRange(onScreen, 1.2, 1.99);

         animator.Retarget(3);

         Assert.Equal(onScreen, animator.Current);
         Assert.Equal(3, animator.Target);
         Assert.True(animator.IsAnimating);
         Wait(0.05);
         Assert.True(animator.Update() > onScreen);
         Wait(0.25);
         Assert.Equal(3, animator.Update());
         Assert.False(animator.IsAnimating);
      }

      [Fact]
      public void Retarget_BackTheOtherWay_TurnsAround() {
         var animator = Create(0.25);
         animator.Start(1, 2);
         Wait(0.1);
         var onScreen = animator.Current;
         animator.Retarget(1);
         Wait(0.05);
         Assert.True(animator.Update() < onScreen);
         Wait(1);
         Assert.Equal(1, animator.Update());
      }

      [Fact]
      public void Retarget_WhenIdle_StartsFromTheScaleItStoppedAt() {
         var animator = Create();
         animator.Snap(5);
         animator.Retarget(6);
         Assert.True(animator.IsAnimating);
         Assert.Equal(5, animator.Current);
         Assert.Equal(6, animator.Target);
      }

      [Fact]
      public void Retarget_ManyTimesInARow_EndsOnTheLastTarget() {
         var animator = Create(0.25);
         animator.Start(1, 2);
         // a fast scroll of the wheel: another click every 40 milliseconds
         for (double target = 3; target <= 6; target++) {
            Wait(0.04);
            animator.Update();
            animator.Retarget(target);
         }
         Assert.Equal(6, animator.Target);
         Wait(0.5);
         Assert.Equal(6, animator.Update());
         Assert.False(animator.IsAnimating);
      }

      [Fact]
      public void ZeroDuration_TurnsTheAnimationOff() {
         var animator = Create(0);
         animator.Start(1, 2);
         Assert.False(animator.IsAnimating);
         Assert.Equal(2, animator.Current);
         Assert.Equal(2, animator.Update());
      }

      [Fact]
      public void NegativeDuration_IsTreatedAsOff() {
         var animator = Create(-1);
         animator.Start(1, 2);
         Assert.False(animator.IsAnimating);
         Assert.Equal(2, animator.Update());
      }

      [Fact]
      public void Start_ToTheSameScale_DoesNotAnimate() {
         var animator = Create();
         animator.Start(2, 2);
         Assert.False(animator.IsAnimating);
         Assert.Equal(2, animator.Update());
      }

      [Fact]
      public void Snap_StopsAnAnimationRightWhereYouTellIt() {
         var animator = Create();
         animator.Start(1, 4);
         Wait(0.05);
         animator.Snap(2);
         Assert.False(animator.IsAnimating);
         Assert.Equal(2, animator.Current);
         Assert.Equal(2, animator.Target);
         Wait(1);
         Assert.Equal(2, animator.Update());
      }

      [Fact]
      public void TheDefaultClock_IsARealClock() {
         var animator = new ZoomAnimator();
         animator.Start(1, 2);
         Assert.True(animator.IsAnimating);
         var scale = animator.Update();
         Assert.InRange(scale, 1, 2);
      }

      #region Frame pacing (there is no frame rate limit: every refresh of the display draws a frame)

      [Fact]
      public void ThereIsNoMinimumTimeBetweenFrames() {
         // the old limiter (frames closer than 12 ms were skipped) held 120, 144 and 240 Hz displays to about 80 frames a second
         var members = typeof(ZoomAnimator).GetMembers().Select(member => member.Name).ToList();
         Assert.DoesNotContain("MinimumFrameInterval", members);
         Assert.DoesNotContain("FrameInterval", members);
      }

      [Fact]
      public void IsFrameDue_WhileTheZoomRuns_IsAlwaysTrue() {
         var animator = Create();
         animator.Start(1, 2);
         Assert.True(animator.IsFrameDue); // the first frame doesn't wait for time to pass
         animator.Update();
         Assert.True(animator.IsFrameDue); // and a frame doesn't wait for the last one to get old
         Wait(0.0001);
         Assert.True(animator.IsFrameDue);
         animator.Update();
         Wait(0.002);
         Assert.True(animator.IsFrameDue);
      }

      [Fact]
      public void IsFrameDue_WhenTheTimeIsUp_TheExactTargetIsDue() {
         var animator = Create(0.25);
         animator.Start(1, 2);
         Wait(0.24);
         animator.Update();
         Wait(0.011);
         Assert.True(animator.IsFrameDue);
         Assert.Equal(2, animator.Update());
         Assert.False(animator.IsAnimating);
      }

      [Fact]
      public void IsFrameDue_WhenNothingIsAnimating_IsFalse() {
         var animator = Create();
         Assert.False(animator.IsFrameDue);
         animator.Start(1, 2);
         Wait(1);
         animator.Update();
         Assert.False(animator.IsFrameDue);
      }

      // Plays one zoom on a display that refreshes at the given rate (or at the given gaps between refreshes) and returns the scale of every frame that was drawn,
      // and how many times the display refreshed.
      private List<(double time, double scale)> Play(ZoomAnimator animator, params double[] refreshGapsInSeconds) => Play(animator, out _, refreshGapsInSeconds);

      private List<(double time, double scale)> Play(ZoomAnimator animator, out int refreshes, params double[] refreshGapsInSeconds) {
         var frames = new List<(double time, double scale)>();
         var start = now; // the clock is exact to 100 nanoseconds, so the time of a frame is read from it, not added up
         refreshes = 0;
         for (int i = 0; animator.IsAnimating && i < 10000; i++) {
            var gap = refreshGapsInSeconds[i % refreshGapsInSeconds.Length];
            Wait(gap);
            refreshes++;
            var time = (now - start).TotalSeconds;
            Assert.False(animator.IsFallingBehind, "frames that come on time are never too slow");
            if (!animator.IsFrameDue) continue;
            frames.Add((time, animator.Update()));
         }
         return frames;
      }

      [Theory]
      [InlineData(30, 7, 9)]
      [InlineData(60, 15, 17)]
      [InlineData(75, 18, 20)]
      [InlineData(120, 30, 32)]
      [InlineData(144, 36, 38)]
      [InlineData(165, 41, 43)]
      [InlineData(240, 60, 62)]
      [InlineData(360, 90, 92)]
      public void Display_OfAnyRefreshRate_DrawsAFrameOnEveryRefresh_AndEndsOnTheExactTarget(int hertz, int least, int most) {
         var animator = Create(0.25);
         animator.Start(1, 2);
         var frames = Play(animator, out var refreshes, 1.0 / hertz);
         Assert.Equal(refreshes, frames.Count); // not a single refresh is skipped
         Assert.InRange(frames.Count, least, most);
         Assert.Equal(2, frames.Last().scale);
         Assert.Equal(frames.Select(frame => frame.scale).OrderBy(scale => scale), frames.Select(frame => frame.scale));
         Assert.False(animator.IsAnimating);
      }

      [Fact]
      public void FasterDisplays_GetSmallerSteps_AndTheZoomStillTakesTheSameTime() {
         double LargestStep(int hertz, out double endTime) {
            var animator = Create(0.25);
            animator.Start(1, 2);
            var frames = Play(animator, 1.0 / hertz);
            endTime = frames.Last().time;
            var steps = frames.Zip(frames.Skip(1), (a, b) => b.scale - a.scale).ToList();
            return steps.Max();
         }

         var step60 = LargestStep(60, out var end60);
         var step144 = LargestStep(144, out var end144);
         var step240 = LargestStep(240, out var end240);
         Assert.True(step144 < step60 * 0.6, $"{step144} should be well below {step60}");
         Assert.True(step240 < step144 * 0.8, $"{step240} should be below {step144}");
         // the zoom ends within one refresh of the same moment on every display
         Assert.InRange(end60, 0.25, 0.25 + 1.0 / 60 + 0.001);
         Assert.InRange(end144, 0.25, 0.25 + 1.0 / 144 + 0.001);
         Assert.InRange(end240, 0.25, 0.25 + 1.0 / 240 + 0.001);
      }

      [Fact]
      public void Display_At60Hz_ThatWobblesByAFewMilliseconds_NeverSkipsAFrame() {
         // gaps between refreshes of 13.5 to 19.5 ms (the average is 16.7)
         var gaps = new[] { 0.0135, 0.0195, 0.0160, 0.0185, 0.0140, 0.0175, 0.0150, 0.0195, 0.0140, 0.0165 };
         var animator = Create(0.25);
         animator.Start(1, 2);
         var frames = Play(animator, out var refreshes, gaps);
         Assert.Equal(refreshes, frames.Count);
         Assert.InRange(frames.Count, 14, 18);
      }

      [Fact]
      public void Display_ThatDeliversTwoFramesAlmostAtOnce_DrawsBoth_AndTheZoomDoesNotSpeedUp() {
         // WPF can raise its Rendering event twice in quick succession: the second call must not make the zoom jump ahead
         var animator = Create(0.25);
         animator.Start(1, 2);
         var frames = Play(animator, 0.0167, 0.0002);
         Assert.Equal(2, frames.Last().scale);
         Assert.Equal(frames.Select(frame => frame.scale).OrderBy(scale => scale), frames.Select(frame => frame.scale));
         foreach (var (time, scale) in frames.Take(frames.Count - 1)) {
            Assert.Equal(1 + ZoomAnimator.EaseOut(time / 0.25), scale, 6);
         }
      }

      [Theory]
      [InlineData(60)]
      [InlineData(40)]
      [InlineData(20)]
      [InlineData(15)]
      [InlineData(144)]
      [InlineData(240)]
      public void TheZoom_IsAnimatedByTime_SoItEndsAtTheSameMomentWhateverTheFrameRate(int framesPerSecond) {
         var animator = Create(0.25);
         animator.Start(1, 2);
         var frames = Play(animator, 1.0 / framesPerSecond);
         // the last frame is the first one at or after the end of the zoom, and no frame shows a scale that is not where the zoom is at that moment
         Assert.InRange(frames.Last().time, 0.25, 0.25 + 1.0 / framesPerSecond + 0.001);
         foreach (var (time, scale) in frames.Take(frames.Count - 1)) {
            Assert.Equal(1 + ZoomAnimator.EaseOut(time / 0.25), scale, 6);
         }
         Assert.Equal(2, frames.Last().scale);
      }

      [Fact]
      public void ALateFrame_DoesNotMakeTheZoomLastLonger_TheNextFrameShowsWhereTheZoomIsByThen() {
         var animator = Create(0.25);
         animator.Start(1, 2);
         Wait(0.0167);
         animator.Update();
         Wait(0.0167);
         animator.Update();
         Wait(0.120); // the app was busy for 120 ms: a garbage collection, a map loading
         Assert.True(animator.IsFrameDue);
         var late = animator.Update();
         Assert.Equal(1 + ZoomAnimator.EaseOut((0.0167 + 0.0167 + 0.120) / 0.25), late, 6);
         Assert.True(animator.IsAnimating);
         Wait(0.0167);
         var next = animator.Update();
         Assert.Equal(1 + ZoomAnimator.EaseOut((0.0167 + 0.0167 + 0.120 + 0.0167) / 0.25), next, 6);
         Assert.True(next > late);
         Wait(0.080); // 0.25 seconds after the start, no matter how many frames were drawn
         Assert.Equal(2, animator.Update());
         Assert.False(animator.IsAnimating);
      }

      [Fact]
      public void Retarget_AtTheMomentOfAFrame_ContinuesWithTheNextFrame() {
         var animator = Create(0.25);
         animator.Start(1, 2);
         Wait(1.0 / 60);
         animator.Update();
         Wait(0.004);
         var onScreen = animator.Current;
         animator.Retarget(3);
         Assert.True(animator.IsFrameDue);
         Assert.Equal(onScreen, animator.Update()); // nothing has passed since the zoom was redirected: the picture stays where it is
         Wait(1.0 / 144);
         Assert.True(animator.IsFrameDue);
         Assert.True(animator.Update() > onScreen);
      }

      [Fact]
      public void IsFallingBehind_FramesOnTime_NoButOneThatTakesTooLong_Yes() {
         var animator = Create(0.25);
         animator.Start(1, 2);
         Wait(0.033);
         animator.Update();
         Assert.False(animator.IsFallingBehind);
         Wait(0.050);
         Assert.False(animator.IsFallingBehind);
         Wait(0.050); // 100 ms since the last frame
         Assert.True(animator.IsFallingBehind);
      }

      [Fact]
      public void IsFallingBehind_Idle_IsFalse_AndANewZoomStartsFresh() {
         var animator = Create();
         Wait(10);
         Assert.False(animator.IsFallingBehind);

         animator.Start(1, 2);
         Wait(0.5);
         Assert.True(animator.IsFallingBehind);
         animator.Retarget(3);
         Assert.False(animator.IsFallingBehind);
      }

      #endregion

      #region NextScale

      [Theory]
      [InlineData(1.0, 2.0)]
      [InlineData(2.0, 3.0)]
      [InlineData(9.0, 10.0)]
      [InlineData(0.5, 1.0)]
      [InlineData(0.25, 0.5)]
      [InlineData(0.0625, 0.125)]
      public void NextScale_ZoomingIn(double scale, double expected) {
         Assert.Equal(expected, BlockMapViewModel.NextScale(scale, true));
      }

      [Theory]
      [InlineData(10.0, 9.0)]
      [InlineData(2.0, 1.0)]
      [InlineData(1.0, 0.5)]
      [InlineData(0.5, 0.25)]
      [InlineData(0.125, 0.0625)]
      public void NextScale_ZoomingOut(double scale, double expected) {
         Assert.Equal(expected, BlockMapViewModel.NextScale(scale, false));
      }

      [Fact]
      public void NextScale_StopsAtTheLimits() {
         Assert.Equal(10, BlockMapViewModel.NextScale(10, true));
         Assert.Equal(0.0625, BlockMapViewModel.NextScale(0.0625, false));
      }

      [Fact]
      public void NextScale_InThenOut_ComesBack() {
         for (double scale = 0.125; scale <= 9; scale = BlockMapViewModel.NextScale(scale, true)) {
            Assert.Equal(scale, BlockMapViewModel.NextScale(BlockMapViewModel.NextScale(scale, true), false));
         }
      }

      #endregion
   }
}
