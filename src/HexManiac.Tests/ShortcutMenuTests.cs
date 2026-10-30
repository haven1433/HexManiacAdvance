using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   // The main menu (the Goto dialog's big buttons): at most 5 buttons per row, rows centred, the pictures on the buttons, and the animation clock that plays them.

   #region The rows

   public class ShortcutRowsTests {
      private static double[] Buttons(int count, double width = 120) => Enumerable.Repeat(width, count).ToArray();

      [Fact]
      public void FiveIsTheDefaultMaximum() => Assert.Equal(5, ShortcutRows.DefaultMaxPerRow);

      [Fact]
      public void NineButtons_AreFiveThenFour() {
         Assert.Equal(new[] { 5, 4 }, ShortcutRows.Break(Buttons(9), 5, 5000));
      }

      [Theory]
      [InlineData(0, new int[0])]
      [InlineData(1, new[] { 1 })]
      [InlineData(4, new[] { 4 })]
      [InlineData(5, new[] { 5 })]
      [InlineData(6, new[] { 5, 1 })]
      [InlineData(10, new[] { 5, 5 })]
      [InlineData(11, new[] { 5, 5, 1 })]
      public void WithRoomToSpare_RowsHoldFive(int count, int[] expected) {
         Assert.Equal(expected, ShortcutRows.Break(Buttons(count), 5, 5000));
      }

      [Fact]
      public void UnlimitedWidth_StillStopsAtFive() {
         Assert.Equal(new[] { 5, 4 }, ShortcutRows.Break(Buttons(9), 5, double.PositiveInfinity));
         Assert.Equal(new[] { 5, 4 }, ShortcutRows.Break(Buttons(9), 5, double.NaN));
      }

      [Fact]
      public void NarrowWindow_PutsFewerButtonsInARow() {
         // 120 wide buttons: 400 pixels hold three of them
         Assert.Equal(new[] { 3, 3, 3 }, ShortcutRows.Break(Buttons(9), 5, 400));
         Assert.Equal(new[] { 4, 4, 1 }, ShortcutRows.Break(Buttons(9), 5, 500));
         Assert.Equal(new[] { 5, 4 }, ShortcutRows.Break(Buttons(9), 5, 600)); // exactly five fit
      }

      [Fact]
      public void WindowNarrowerThanOneButton_EachButtonGetsItsOwnRow_AndItTerminates() {
         Assert.Equal(Enumerable.Repeat(1, 9), ShortcutRows.Break(Buttons(9), 5, 50));
         Assert.Equal(Enumerable.Repeat(1, 3), ShortcutRows.Break(Buttons(3), 5, 0));
      }

      [Fact]
      public void SmallButtons_UseTheSameRule() {
         // SmallMode buttons are 50 wide with 5 margin on each side = 60: still never more than five, and a narrow window gives fewer
         Assert.Equal(new[] { 5, 4 }, ShortcutRows.Break(Buttons(9, 60), 5, 5000));
         Assert.Equal(new[] { 4, 4, 1 }, ShortcutRows.Break(Buttons(9, 60), 5, 250));
      }

      [Fact]
      public void ButtonsOfDifferentWidths_AreMeasuredByTheirRealWidth() {
         var widths = new double[] { 200, 200, 200, 60, 60, 60 };
         Assert.Equal(new[] { 3, 3 }, ShortcutRows.Break(widths, 5, 650)); // 600 for the first three, the fourth would make 660
         Assert.Equal(new[] { 5, 1 }, ShortcutRows.Break(new double[] { 60, 60, 60, 60, 60, 200 }, 5, 650));
      }

      [Fact]
      public void FractionsThatAddUpToTheWidth_StillFit() {
         var widths = Enumerable.Repeat(0.1 + 0.2, 5).ToArray(); // 0.30000000000000004 each
         Assert.Equal(new[] { 5 }, ShortcutRows.Break(widths, 5, 1.5));
      }

      [Theory]
      [InlineData(0)]
      [InlineData(-3)]
      public void MaximumBelowOne_CountsAsOne(int max) {
         Assert.Equal(new[] { 1, 1, 1 }, ShortcutRows.Break(Buttons(3), max, 5000));
      }

      [Fact]
      public void ThreePerRow_IsAlsoPossible() {
         Assert.Equal(new[] { 3, 3, 3 }, ShortcutRows.Break(Buttons(9), 3, 5000));
      }

      [Fact]
      public void EveryRowFitsAndNoButtonIsLost() {
         var random = new Random(1234);
         for (int run = 0; run < 400; run++) {
            var count = random.Next(0, 20);
            var widths = Enumerable.Range(0, count).Select(i => (double)random.Next(40, 200)).ToArray();
            var available = random.Next(30, 1200);
            var max = random.Next(1, 7);
            var rows = ShortcutRows.Break(widths, max, available);

            Assert.Equal(count, rows.Sum());
            var start = 0;
            foreach (var row in rows) {
               Assert.InRange(row, 1, max);
               if (row > 1) Assert.True(ShortcutRows.RowWidth(widths, start, row) <= available + 0.001, $"row of {row} is {ShortcutRows.RowWidth(widths, start, row)} wide, only {available} available");
               start += row;
            }
            // greedy: the first button of the next row really didn't fit on the row before
            start = 0;
            for (int r = 0; r + 1 < rows.Count; r++) {
               start += rows[r];
               Assert.True(rows[r] == max || ShortcutRows.RowWidth(widths, start - rows[r], rows[r]) + widths[start] > available + 0.001);
            }
         }
      }

      [Fact]
      public void BreakInto_ReusesTheListAndForgetsTheOldRows() {
         var rows = new List<int> { 99, 98 };
         ShortcutRows.BreakInto(rows, Buttons(6), 5, 5000);
         Assert.Equal(new[] { 5, 1 }, rows);
         ShortcutRows.BreakInto(rows, null, 5, 5000);
         Assert.Empty(rows);
      }

      [Fact]
      public void RowWidth_AddsTheButtonsOfTheRow() {
         var widths = new double[] { 10, 20, 30, 40 };
         Assert.Equal(50, ShortcutRows.RowWidth(widths, 1, 2));
         Assert.Equal(70, ShortcutRows.RowWidth(widths, 2, 5)); // the end of the list stops it
         Assert.Equal(0, ShortcutRows.RowWidth(widths, 4, 2));
      }

      [Fact]
      public void ButtonWithoutAWidth_CountsAsZeroWide_RowStillHasAnOffset() {
         var widths = new double[] { 100, double.NaN, 100 };
         Assert.Equal(200, ShortcutRows.RowWidth(widths, 0, 3));
         Assert.Equal(new[] { 3 }, ShortcutRows.Break(widths, 5, 250));
         Assert.Equal(300, ShortcutRows.CentreOffset(800, ShortcutRows.RowWidth(widths, 0, 3)));
      }

      [Fact]
      public void ARowIsCentred_AndOneThatIsTooWideStartsAtTheLeftEdge() {
         Assert.Equal(100, ShortcutRows.CentreOffset(800, 600));
         Assert.Equal(160, ShortcutRows.CentreOffset(800, 480)); // the short last row of 4 under a row of 5
         Assert.Equal(0, ShortcutRows.CentreOffset(500, 600));
         Assert.Equal(0, ShortcutRows.CentreOffset(double.PositiveInfinity, 600));
         Assert.Equal(0, ShortcutRows.CentreOffset(double.NaN, 600));
      }

      [Fact]
      public void ShortRowIsCentredUnderTheFullRow() {
         var widths = Buttons(9);
         var rows = ShortcutRows.Break(widths, 5, 5000);
         var full = ShortcutRows.RowWidth(widths, 0, rows[0]); // 600: this is the panel's width
         var shortRowOffset = ShortcutRows.CentreOffset(full, ShortcutRows.RowWidth(widths, rows[0], rows[1]));
         Assert.Equal(60, shortRowOffset);
         Assert.Equal(0, ShortcutRows.CentreOffset(full, full));
      }
   }

   #endregion

   #region The animation

   public class ShortcutAnimationTests {
      private static IPixelViewModel Frame() => new ReadonlyPixelViewModel(1, 1, new short[1], -1);

      private static IPixelViewModel[] Frames(int count) => Enumerable.Range(0, count).Select(i => Frame()).ToArray();

      [Fact]
      public void CreateRejectsWhatCannotBeAnimated() {
         Assert.Null(ShortcutAnimation.Create(null, new[] { 100 }));
         Assert.Null(ShortcutAnimation.Create(Frames(2), null));
         Assert.Null(ShortcutAnimation.Create(Frames(0), new int[0]));
         Assert.Null(ShortcutAnimation.Create(Frames(2), new[] { 100 }));          // a time is missing
         Assert.Null(ShortcutAnimation.Create(Frames(1), new[] { 100, 100 }));     // a picture is missing
         Assert.Null(ShortcutAnimation.Create(Frames(2), new[] { 100, 0 }));       // a picture that is not shown at all
         Assert.Null(ShortcutAnimation.Create(Frames(2), new[] { 100, -5 }));
         Assert.Null(ShortcutAnimation.Create(new IPixelViewModel[] { Frame(), null }, new[] { 100, 100 }));
      }

      [Fact]
      public void CreateCopiesTheLists() {
         var frames = Frames(2);
         var times = new[] { 100, 200 };
         var animation = ShortcutAnimation.Create(frames, times);

         frames[0] = null;
         times[0] = 5;

         Assert.NotNull(animation.Frames[0]);
         Assert.Equal(new[] { 100, 200 }, animation.DurationsMilliseconds);
         Assert.Equal(300, animation.CycleMilliseconds);
         Assert.Equal(2, animation.FrameCount);
      }

      [Fact]
      public void FrameIndexAt_ThreeFramesWithAThreeSecondPause() {
         // two quick pictures, then the last one held for 3 seconds: the pause between plays of the Pokemon animation
         var animation = ShortcutAnimation.Create(Frames(3), new[] { 100, 100, 3000 });
         long until;

         Assert.Equal(0, animation.FrameIndexAt(0, out until)); Assert.Equal(100, until);
         Assert.Equal(0, animation.FrameIndexAt(99, out until)); Assert.Equal(1, until);
         Assert.Equal(1, animation.FrameIndexAt(100, out until)); Assert.Equal(100, until);
         Assert.Equal(1, animation.FrameIndexAt(199, out until)); Assert.Equal(1, until);
         Assert.Equal(2, animation.FrameIndexAt(200, out until)); Assert.Equal(3000, until);
         Assert.Equal(2, animation.FrameIndexAt(3199, out until)); Assert.Equal(1, until);
         Assert.Equal(0, animation.FrameIndexAt(3200, out until)); Assert.Equal(100, until); // the next round
         Assert.Equal(2, animation.FrameIndexAt(3200 * 7 + 250, out until)); Assert.Equal(2950, until);
         Assert.Equal(0, animation.FrameIndexAt(-50, out until)); Assert.Equal(100, until);
         Assert.Equal(2, animation.FrameIndexAt(long.MaxValue - 1, out until)); Assert.InRange(until, 1, 3000);
      }

      [Fact]
      public void FrameIndexAt_SingleFrame() {
         var animation = ShortcutAnimation.Create(Frames(1), new[] { 500 });
         Assert.Equal(0, animation.FrameIndexAt(12345, out var until));
         Assert.InRange(until, 1, 500);
      }
   }

   #endregion

   #region The button

   public class GotoShortcutViewModelPictureTests {
      private static IPixelViewModel Frame() => new ReadonlyPixelViewModel(1, 1, new short[1], -1);

      private static GotoShortcutViewModel Create(IPixelViewModel image) => new GotoShortcutViewModel(null, null, image, "anchor", "text");

      private static GotoShortcutViewModel Create(Func<IPixelViewModel> factory) => new GotoShortcutViewModel(null, null, factory, "anchor", "text");

      private static int CountImageNotifications(GotoShortcutViewModel shortcut, Action action) {
         var count = 0;
         shortcut.PropertyChanged += (sender, e) => { if (e.PropertyName == nameof(GotoShortcutViewModel.Image)) count++; };
         action();
         return count;
      }

      [Fact]
      public void ButtonWithAPicture_ShowsIt() {
         var picture = Frame();
         var shortcut = Create(picture);

         Assert.Same(picture, shortcut.Image);
         Assert.False(shortcut.IsAnimated);
      }

      [Fact]
      public void FactoryPicture_IsDrawnWhenFirstShown_AndOnlyOnce() {
         var calls = 0;
         var picture = Frame();
         var shortcut = Create(() => { calls++; return picture; });

         Assert.Equal(0, calls); // preparing the dialog doesn't draw anything

         Assert.Same(picture, shortcut.Image);
         Assert.Same(picture, shortcut.Image);
         Assert.Equal(1, calls);
      }

      [Fact]
      public void FactoryThatFails_GivesAButtonWithoutPicture_NotAnException() {
         var shortcut = Create(() => throw new InvalidOperationException("broken ROM"));

         Assert.Null(shortcut.Image);
         Assert.Null(shortcut.Image);
      }

      [Fact]
      public void PreferImage_ReplacesThePictureWhenTheFactoryHasOne() {
         var old = Frame();
         var better = Frame();
         var shortcut = Create(old);

         shortcut.PreferImage(() => better);

         Assert.Same(better, shortcut.Image);
      }

      [Fact]
      public void PreferImage_KeepsTheOldPictureWhenTheFactoryHasNone() {
         var old = Frame();
         var shortcut = Create(old);

         shortcut.PreferImage(() => null);
         Assert.Same(old, shortcut.Image);

         var viaFactory = Create(() => old);
         viaFactory.PreferImage(() => throw new Exception("no such sprite"));
         Assert.Same(old, viaFactory.Image);
      }

      [Fact]
      public void PreferImage_IsLazy() {
         var calls = 0;
         var shortcut = Create(Frame());

         shortcut.PreferImage(() => { calls++; return Frame(); });

         Assert.Equal(0, calls);
         Assert.NotNull(shortcut.Image);
         Assert.Equal(1, calls);
      }

      [Fact]
      public void SetAnimation_ShowsTheFirstFrame_AndTellsTheView() {
         var frames = new[] { Frame(), Frame(), Frame() };
         var shortcut = Create(Frame());

         var notifications = CountImageNotifications(shortcut, () => shortcut.SetAnimation(frames, new[] { 100, 100, 3000 }));

         Assert.True(shortcut.IsAnimated);
         Assert.Same(frames[0], shortcut.Image);
         Assert.Equal(1, notifications);
      }

      [Fact]
      public void ShowFrameAt_ChangesThePicture_OnlyWhenTheFrameChanges() {
         var frames = new[] { Frame(), Frame(), Frame() };
         var shortcut = Create(Frame());
         shortcut.SetAnimation(frames, new[] { 100, 100, 3000 });
         long until = 0;

         var notifications = CountImageNotifications(shortcut, () => {
            shortcut.ShowFrameAt(0, out until);   // still the first frame
            shortcut.ShowFrameAt(50, out until);
            shortcut.ShowFrameAt(99, out until);
         });
         Assert.Equal(0, notifications);
         Assert.Same(frames[0], shortcut.Image);

         notifications = CountImageNotifications(shortcut, () => shortcut.ShowFrameAt(100, out until));
         Assert.Equal(1, notifications);
         Assert.Same(frames[1], shortcut.Image);
         Assert.Equal(100, until);

         shortcut.ShowFrameAt(250, out until);
         Assert.Same(frames[2], shortcut.Image);
         Assert.Equal(2950, until);

         shortcut.ShowFrameAt(3200, out until);
         Assert.Same(frames[0], shortcut.Image); // and around again
      }

      [Fact]
      public void SetAnimation_WithOneFrame_IsJustAPicture() {
         var only = Frame();
         var shortcut = Create(Frame());

         shortcut.SetAnimation(new[] { only }, new[] { 100 });

         Assert.False(shortcut.IsAnimated);
         Assert.Same(only, shortcut.Image);
         shortcut.ShowFrameAt(5000, out var until);
         Assert.Equal(long.MaxValue, until);
      }

      [Theory]
      [InlineData(0)]
      [InlineData(1)]
      public void SetAnimation_WithBadLists_LeavesTheButtonAsItWas(int kind) {
         var old = Frame();
         var shortcut = Create(old);

         if (kind == 0) shortcut.SetAnimation(new IPixelViewModel[0], new int[0]);
         else shortcut.SetAnimation(new[] { Frame(), Frame() }, new[] { 100 });

         Assert.False(shortcut.IsAnimated);
         Assert.Same(old, shortcut.Image);
      }

      [Fact]
      public void SetAnimation_NullIsAllowed() {
         var shortcut = Create(Frame());
         shortcut.SetAnimation((ShortcutAnimation)null);
         shortcut.SetAnimation(null, null);
         Assert.False(shortcut.IsAnimated);
      }

      [Fact]
      public void AnimationSource_IsOnlyBuiltWhenNeeded_AndOnlyOnce() {
         var calls = 0;
         var frames = new[] { Frame(), Frame() };
         var shortcut = Create(Frame());
         shortcut.SetAnimationSource(() => { calls++; return ShortcutAnimation.Create(frames, new[] { 100, 100 }); });

         Assert.Equal(0, calls);
         Assert.False(shortcut.IsAnimated); // promised, not built

         Assert.True(shortcut.PrepareAnimation());
         Assert.True(shortcut.PrepareAnimation());

         Assert.Equal(1, calls);
         Assert.True(shortcut.IsAnimated);
         Assert.Same(frames[0], shortcut.Image);
      }

      [Fact]
      public void AnimationSource_IsBuiltWhenThePictureIsFirstShown_SoTheButtonNeverShowsADifferentStill() {
         var frames = new[] { Frame(), Frame() };
         var shortcut = Create(() => Frame());
         shortcut.SetAnimationSource(() => ShortcutAnimation.Create(frames, new[] { 100, 100 }));

         Assert.Same(frames[0], shortcut.Image);
         Assert.True(shortcut.IsAnimated);
      }

      [Fact]
      public void AnimationSourceWithoutAnimation_KeepsTheStillPicture() {
         var still = Frame();
         var shortcut = Create(() => still);
         shortcut.SetAnimationSource(() => null);

         Assert.False(shortcut.PrepareAnimation());
         Assert.Same(still, shortcut.Image);
         Assert.False(shortcut.IsAnimated);
      }

      [Fact]
      public void AnimationSourceThatFails_IsNotAnException() {
         var still = Frame();
         var shortcut = Create(() => still);
         shortcut.SetAnimationSource(() => throw new InvalidOperationException("no pokemon animation in this ROM"));

         Assert.False(shortcut.PrepareAnimation());
         Assert.Same(still, shortcut.Image);
      }

      [Fact]
      public void ExplicitAnimation_BeatsAPromisedOne() {
         var promised = 0;
         var frames = new[] { Frame(), Frame() };
         var shortcut = Create(Frame());
         shortcut.SetAnimationSource(() => { promised++; return null; });

         shortcut.SetAnimation(frames, new[] { 100, 100 });
         shortcut.PrepareAnimation();

         Assert.Equal(0, promised);
         Assert.Same(frames[0], shortcut.Image);
      }
   }

   #endregion

   #region The clock

   public class ShortcutAnimationClockTests {
      private class FakeTimer : IDelayWorkTimer {
         private Action work;
         public TimeSpan LastDelay;
         public int DelayCalls;

         public bool HasScheduledWork => work != null;

         public DelayWorkResult DelayCall(TimeSpan delay, Action action) {
            var result = work == null ? DelayWorkResult.WorkScheduled : DelayWorkResult.WorkScheduledAndPreviousWorkCleared;
            (LastDelay, work) = (delay, action);
            DelayCalls++;
            return result;
         }

         public void Reset() => work = null;

         /// <summary>Does what the WPF timer does: runs the work, then forgets its work (so work that waits again on the same timer from inside its own callback would be lost).</summary>
         public void Fire() {
            var action = work;
            if (action == null) return;
            action();
            work = null;
         }
      }

      private class FakeDispatcher : IWorkDispatcher {
         public List<FakeTimer> Timers { get; } = new();
         public Task WaitForRenderingAsync() => Task.CompletedTask;
         public void BlockOnUIWork(Action action) => action();
         public Task DispatchWork(Action action) { action(); return Task.CompletedTask; }
         public Task RunBackgroundWork(Action action) => DispatchWork(action);
         public IDelayWorkTimer CreateDelayTimer() { var timer = new FakeTimer(); Timers.Add(timer); return timer; }

         public int WaitingCount => Timers.Count(timer => timer.HasScheduledWork);
         public FakeTimer Waiting => Timers.SingleOrDefault(timer => timer.HasScheduledWork);
         public int TotalWaits => Timers.Sum(timer => timer.DelayCalls);
      }

      private static IPixelViewModel Frame() => new ReadonlyPixelViewModel(1, 1, new short[1], -1);

      /// <summary>A Goto dialog with one animated button (3 frames: two quick ones, then a 3 second pause), a fake clock and a dispatcher whose timers wait until the test fires them.</summary>
      private class Fixture {
         public long Now = 1_000_000;
         public FakeDispatcher Dispatcher { get; } = new();
         public ShortcutAnimationClock Clock { get; }
         public GotoControlViewModel Goto { get; }
         public GotoShortcutViewModel Animated { get; }
         public GotoShortcutViewModel Still { get; }
         public IPixelViewModel[] Frames { get; } = { Frame(), Frame(), Frame() };
         public int ImageChanges;

         public Fixture(params int[] durations) {
            if (durations.Length == 0) durations = new[] { 100, 100, 3000 };
            Clock = new ShortcutAnimationClock(Dispatcher, () => Now);
            Goto = new GotoControlViewModel(null, null, null, false);
            Animated = new GotoShortcutViewModel(null, null, Frame(), "a", "Anim Tiles");
            Animated.SetAnimation(Frames, durations);
            Animated.PropertyChanged += (sender, e) => { if (e.PropertyName == nameof(GotoShortcutViewModel.Image)) ImageChanges++; };
            Still = new GotoShortcutViewModel(null, null, Frame(), "b", "Maps");
            Goto.Shortcuts = new ObservableCollection<GotoShortcutViewModel> { Still, Animated };
            Clock.Attach(Goto);
         }

         public void Show() => Goto.ControlVisible = true;
         public void Hide() => Goto.ControlVisible = false;

         /// <summary>Time passes, and the timer that was waiting (if any) fires.</summary>
         public void Elapse(long milliseconds) {
            Now += milliseconds;
            Dispatcher.Waiting?.Fire();
         }
      }

      [Fact]
      public void ClockDoesNothing_WhileTheDialogIsHidden() {
         var f = new Fixture();

         Assert.False(f.Clock.IsRunning);
         Assert.Equal(0, f.Dispatcher.TotalWaits);
         Assert.Equal(0, f.Dispatcher.WaitingCount);
         Assert.Equal(0, f.ImageChanges);
      }

      [Fact]
      public void ShowingTheDialog_StartsTheClockOnTheFirstFrame() {
         var f = new Fixture();

         f.Show();

         Assert.True(f.Clock.IsRunning);
         Assert.Same(f.Frames[0], f.Animated.Image);
         Assert.Equal(1, f.Dispatcher.WaitingCount);
         Assert.Equal(TimeSpan.FromMilliseconds(100), f.Dispatcher.Waiting.LastDelay);
      }

      [Fact]
      public void FakeThreeFrameAnimation_PlaysFrameOrderAndTheThreeSecondIdle() {
         var f = new Fixture(100, 100, 3000);
         f.Show();
         Assert.Same(f.Frames[0], f.Animated.Image);
         Assert.Equal(TimeSpan.FromMilliseconds(100), f.Dispatcher.Waiting.LastDelay);

         f.Elapse(100);
         Assert.Same(f.Frames[1], f.Animated.Image);
         Assert.Equal(TimeSpan.FromMilliseconds(100), f.Dispatcher.Waiting.LastDelay);

         f.Elapse(100);
         Assert.Same(f.Frames[2], f.Animated.Image);
         Assert.Equal(TimeSpan.FromMilliseconds(3000), f.Dispatcher.Waiting.LastDelay); // the idle: one wait of 3 seconds, not 300 ticks of 10ms

         f.Elapse(3000);
         Assert.Same(f.Frames[0], f.Animated.Image); // plays again
         Assert.Equal(TimeSpan.FromMilliseconds(100), f.Dispatcher.Waiting.LastDelay);

         f.Elapse(100);
         Assert.Same(f.Frames[1], f.Animated.Image);
      }

      [Fact]
      public void TheIdle_CostsOneWakeUp()
      {
         var f = new Fixture(100, 100, 3000);
         f.Show();
         var waitsBefore = f.Dispatcher.TotalWaits;

         f.Elapse(100);
         f.Elapse(100);
         f.Elapse(3000); // a full round took 3 wake-ups

         Assert.Equal(waitsBefore + 3, f.Dispatcher.TotalWaits);
         Assert.Equal(1, f.Dispatcher.WaitingCount); // never more than one wait at a time
      }

      [Fact]
      public void OnlyChangedFrames_AreAnnounced() {
         var f = new Fixture(100, 100, 3000);
         f.Show();
         var after = f.ImageChanges;

         f.Elapse(100);
         f.Elapse(100);
         f.Elapse(3000);

         Assert.Equal(after + 3, f.ImageChanges);
      }

      [Fact]
      public void WakingUpEarly_DoesNotSkipAFrame_AndWaitsTheRest() {
         var f = new Fixture(100, 100, 3000);
         f.Show();

         f.Elapse(90); // the timer fired a little early

         Assert.Same(f.Frames[0], f.Animated.Image);
         Assert.Equal(TimeSpan.FromMilliseconds(MinimumOr(10)), f.Dispatcher.Waiting.LastDelay);
      }

      private static double MinimumOr(double milliseconds) => Math.Max(ShortcutAnimationClock.MinimumDelayMilliseconds, milliseconds);

      [Fact]
      public void WakingUpLate_ShowsTheFrameThatBelongsToNow()
      {
         var f = new Fixture(100, 100, 3000);
         f.Show();

         f.Elapse(3200 * 5 + 150); // five whole rounds and 150ms later (the computer was busy)

         Assert.Same(f.Frames[1], f.Animated.Image);
         Assert.Equal(TimeSpan.FromMilliseconds(50), f.Dispatcher.Waiting.LastDelay);
      }

      [Fact]
      public void VeryShortFrames_DoNotMakeTheClockSpin() {
         var f = new Fixture(1, 1, 1);
         f.Show();

         Assert.Equal(TimeSpan.FromMilliseconds(ShortcutAnimationClock.MinimumDelayMilliseconds), f.Dispatcher.Waiting.LastDelay);
      }

      [Fact]
      public void HidingTheDialog_StopsTheClockCompletely() {
         var f = new Fixture();
         f.Show();
         f.Elapse(100);
         var waits = f.Dispatcher.TotalWaits;

         f.Hide();

         Assert.False(f.Clock.IsRunning);
         Assert.Equal(0, f.Dispatcher.WaitingCount);
         f.Elapse(10_000);
         Assert.Equal(waits, f.Dispatcher.TotalWaits); // nothing woke up, nothing was scheduled
         var changes = f.ImageChanges;
         f.Elapse(10_000);
         Assert.Equal(changes, f.ImageChanges);
      }

      [Fact]
      public void ShowingTheDialogAgain_StartsOverFromTheFirstFrame() {
         var f = new Fixture();
         f.Show();
         f.Elapse(100);
         f.Elapse(100);
         Assert.Same(f.Frames[2], f.Animated.Image);
         f.Hide();
         f.Now += 777;

         f.Show();

         Assert.Same(f.Frames[0], f.Animated.Image);
         Assert.Equal(TimeSpan.FromMilliseconds(100), f.Dispatcher.Waiting.LastDelay);
      }

      [Fact]
      public void DeactivatingTheWindow_StopsTheClock_AndActivatingItStartsAgain() {
         var f = new Fixture();
         f.Show();
         f.Elapse(100);

         f.Clock.WindowActive = false;

         Assert.False(f.Clock.IsRunning);
         Assert.Equal(0, f.Dispatcher.WaitingCount);

         f.Clock.WindowActive = true;

         Assert.True(f.Clock.IsRunning);
         Assert.Equal(1, f.Dispatcher.WaitingCount);
         Assert.Same(f.Frames[0], f.Animated.Image);
      }

      [Fact]
      public void DialogShownWhileTheWindowIsInactive_DoesNotRun() {
         var f = new Fixture();
         f.Clock.WindowActive = false;

         f.Show();

         Assert.False(f.Clock.IsRunning);
         Assert.Equal(0, f.Dispatcher.TotalWaits);
      }

      [Fact]
      public void ChangingTab_LetsGoOfTheOldDialog_AndStopsIt() {
         var f = new Fixture();
         f.Show();
         Assert.True(f.Clock.IsRunning);
         var other = new GotoControlViewModel(null, null, null, false); // every tab change makes a new dialog (hidden at first)

         f.Clock.Attach(other);

         Assert.Same(other, f.Clock.Target);
         Assert.False(f.Clock.IsRunning);
         Assert.Equal(0, f.Dispatcher.WaitingCount);

         f.Hide();
         f.Show(); // the old dialog is no longer watched
         Assert.False(f.Clock.IsRunning);
         Assert.Equal(0, f.Dispatcher.WaitingCount);
      }

      [Fact]
      public void ChangingTab_DoesNotKeepTheOldDialogAlive() {
         var clock = new ShortcutAnimationClock(new FakeDispatcher(), () => 0);
         [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
         WeakReference Attach() {
            var oldDialog = new GotoControlViewModel(null, null, null, false);
            var shortcut = new GotoShortcutViewModel(null, null, Frame(), "a", "Anim Tiles");
            shortcut.SetAnimation(new[] { Frame(), Frame() }, new[] { 100, 100 });
            oldDialog.Shortcuts = new ObservableCollection<GotoShortcutViewModel> { shortcut };
            oldDialog.ControlVisible = true;
            clock.Attach(oldDialog);
            return new WeakReference(oldDialog);
         }
         var weak = Attach();

         clock.Attach(new GotoControlViewModel(null, null, null, false));
         GC.Collect();
         GC.WaitForPendingFinalizers();
         GC.Collect();

         Assert.False(weak.IsAlive);
      }

      [Fact]
      public void AttachingNull_Stops() {
         var f = new Fixture();
         f.Show();

         f.Clock.Attach(null);

         Assert.False(f.Clock.IsRunning);
         Assert.Null(f.Clock.Target);
         Assert.Equal(0, f.Dispatcher.WaitingCount);
      }

      [Fact]
      public void DialogWithoutAnimatedButtons_NeverStartsTheClock() {
         var dispatcher = new FakeDispatcher();
         var clock = new ShortcutAnimationClock(dispatcher, () => 0);
         var dialog = new GotoControlViewModel(null, null, null, false);
         dialog.Shortcuts = new ObservableCollection<GotoShortcutViewModel> { new GotoShortcutViewModel(null, null, Frame(), "a", "Maps") };
         clock.Attach(dialog);

         dialog.ControlVisible = true;

         Assert.False(clock.IsRunning);
         Assert.Equal(0, dispatcher.TotalWaits);
      }

      [Fact]
      public void AnimationsAreOnlyBuilt_WhenTheDialogIsShownAndTheWindowIsActive() {
         var builds = 0;
         var dispatcher = new FakeDispatcher();
         var clock = new ShortcutAnimationClock(dispatcher, () => 0);
         var dialog = new GotoControlViewModel(null, null, null, false);
         var shortcut = new GotoShortcutViewModel(dialog, null, Frame(), "a", "Anim Tiles");
         shortcut.SetAnimationSource(() => { builds++; return ShortcutAnimation.Create(new[] { Frame(), Frame() }, new[] { 100, 100 }); });
         dialog.Shortcuts = new ObservableCollection<GotoShortcutViewModel> { shortcut };

         clock.Attach(dialog);
         clock.WindowActive = false;
         dialog.ControlVisible = true;
         Assert.Equal(0, builds);

         clock.WindowActive = true;

         Assert.Equal(1, builds);
         Assert.True(clock.IsRunning);
      }

      [Fact]
      public void PromisedAnimationThatTurnsOutToBeNone_LeavesTheClockOff() {
         var dispatcher = new FakeDispatcher();
         var clock = new ShortcutAnimationClock(dispatcher, () => 0);
         var dialog = new GotoControlViewModel(null, null, null, false);
         var shortcut = new GotoShortcutViewModel(dialog, null, Frame(), "pokemon", "Pokemon");
         shortcut.SetAnimationSource(() => null); // the hook for the Pokemon animation, before it is wired
         dialog.Shortcuts = new ObservableCollection<GotoShortcutViewModel> { shortcut };
         clock.Attach(dialog);

         dialog.ControlVisible = true;

         Assert.False(clock.IsRunning);
         Assert.Equal(0, dispatcher.TotalWaits);
      }

      [Fact]
      public void ReplacingTheButtons_WhileTheDialogIsOpen_PlaysTheNewOnes() {
         var f = new Fixture();
         f.Show();
         var second = new GotoShortcutViewModel(f.Goto, null, Frame(), "c", "Pokemon");
         var frames = new[] { Frame(), Frame() };
         second.SetAnimation(frames, new[] { 50, 50 });

         f.Goto.Shortcuts = new ObservableCollection<GotoShortcutViewModel> { second }; // RefreshGotoShortcuts does this

         Assert.True(f.Clock.IsRunning);
         Assert.Same(frames[0], second.Image);
         Assert.Equal(TimeSpan.FromMilliseconds(50), f.Dispatcher.Waiting.LastDelay);
         f.Elapse(50);
         Assert.Same(frames[1], second.Image);
      }

      [Fact]
      public void GivingAButtonAnAnimation_WhileTheDialogIsOpen_StartsTheClock() {
         var f = new Fixture();
         var dialog = new GotoControlViewModel(null, null, null, false);
         var shortcut = new GotoShortcutViewModel(dialog, null, Frame(), "a", "Anim Tiles");
         dialog.Shortcuts = new ObservableCollection<GotoShortcutViewModel> { shortcut };
         f.Clock.Attach(dialog);
         dialog.ControlVisible = true;
         Assert.False(f.Clock.IsRunning);

         shortcut.SetAnimation(new[] { Frame(), Frame() }, new[] { 100, 100 });

         Assert.True(f.Clock.IsRunning);
      }

      [Fact]
      public void TwoAnimatedButtons_WaitForTheEarliestChange() {
         var f = new Fixture(100, 100, 3000);
         var quick = new GotoShortcutViewModel(f.Goto, null, Frame(), "q", "Pokemon");
         quick.SetAnimation(new[] { Frame(), Frame() }, new[] { 40, 40 });
         f.Goto.Shortcuts = new ObservableCollection<GotoShortcutViewModel> { f.Animated, quick };

         f.Show();

         Assert.Equal(TimeSpan.FromMilliseconds(40), f.Dispatcher.Waiting.LastDelay);
         f.Elapse(40);
         Assert.Equal(TimeSpan.FromMilliseconds(40), f.Dispatcher.Waiting.LastDelay);
         f.Elapse(40);
         Assert.Same(f.Frames[0], f.Animated.Image); // the slow one waits for its own time
         Assert.Equal(TimeSpan.FromMilliseconds(20), f.Dispatcher.Waiting.LastDelay);
         f.Elapse(20);
         Assert.Same(f.Frames[1], f.Animated.Image);
      }

      [Fact]
      public void ReArmingFromInsideTheCallback_WorksWithATimerThatForgetsItsWorkAfterTheCallback() {
         // FakeTimer.Fire() forgets the work of the timer that fired, like the WPF timer: the clock takes turns between two timers
         var f = new Fixture(100, 100, 3000);
         f.Show();

         for (int i = 0; i < 10; i++) {
            f.Elapse(f.Dispatcher.Waiting.LastDelay.TotalMilliseconds < 1 ? 1 : (long)f.Dispatcher.Waiting.LastDelay.TotalMilliseconds);
            Assert.Equal(1, f.Dispatcher.WaitingCount);
         }
         Assert.True(f.Clock.IsRunning);
      }

      [Fact]
      public void ADispatcherThatRunsTimersAtOnce_DoesNotRecurseForever() {
         // InstantDispatch (used by most tests) runs the timer's work inside DelayCall
         var clock = new ShortcutAnimationClock(InstantDispatch.Instance, () => 0);
         var dialog = new GotoControlViewModel(null, null, null, false);
         var frames = new[] { Frame(), Frame() };
         var shortcut = new GotoShortcutViewModel(dialog, null, Frame(), "a", "Anim Tiles");
         shortcut.SetAnimation(frames, new[] { 100, 100 });
         dialog.Shortcuts = new ObservableCollection<GotoShortcutViewModel> { shortcut };
         clock.Attach(dialog);

         dialog.ControlVisible = true;

         Assert.True(clock.IsRunning);
         Assert.Same(frames[0], shortcut.Image);
      }

      [Fact]
      public void AnAnimationThatFails_StopsTheClock_AndNeverThrows() {
         var f = new Fixture();
         var broken = new GotoShortcutViewModel(f.Goto, null, Frame(), "x", "Broken");
         broken.SetAnimationSource(() => throw new InvalidOperationException("bad frames"));
         f.Goto.Shortcuts = new ObservableCollection<GotoShortcutViewModel> { broken, f.Animated };

         f.Show();

         Assert.True(f.Clock.IsRunning); // the good button plays, the broken one is just a picture
         Assert.False(broken.IsAnimated);
      }

      [Fact]
      public void DefaultTimeSource_RunsWithoutAFakeClock() {
         var dispatcher = new FakeDispatcher();
         var clock = new ShortcutAnimationClock(dispatcher);
         var dialog = new GotoControlViewModel(null, null, null, false);
         var shortcut = new GotoShortcutViewModel(dialog, null, Frame(), "a", "Anim Tiles");
         shortcut.SetAnimation(new[] { Frame(), Frame() }, new[] { 100, 100 });
         dialog.Shortcuts = new ObservableCollection<GotoShortcutViewModel> { shortcut };
         clock.Attach(dialog);

         dialog.ControlVisible = true;

         Assert.True(clock.IsRunning);
         Assert.Equal(1, dispatcher.WaitingCount);
      }

      [Fact]
      public void NullDispatcher_FallsBackToTheInstantOne() {
         var clock = new ShortcutAnimationClock(null);
         clock.Attach(new GotoControlViewModel(null, null, null, false));
         Assert.False(clock.IsRunning);
      }
   }

   #endregion

   #region The editor owns the clock

   public class ShortcutClockEditorTests : BaseViewModelTestClass {
      [Fact]
      public void SwappingTabs_PointsTheClockAtTheSelectedTabsDialog() {
         var editor = New.EditorViewModel();
         var first = new ViewPort("a.gba", new PokemonModel(new byte[0x200], null, Singletons), InstantDispatch.Instance, Singletons);
         var second = new ViewPort("b.gba", new PokemonModel(new byte[0x200], null, Singletons), InstantDispatch.Instance, Singletons);
         editor.Add(first);
         editor.Add(second);

         editor.SelectedIndex = 0;
         var firstDialog = editor.GotoViewModel;
         Assert.Same(firstDialog, editor.ShortcutClock.Target);

         editor.SelectedIndex = 1;

         Assert.NotSame(firstDialog, editor.GotoViewModel);
         Assert.Same(editor.GotoViewModel, editor.ShortcutClock.Target);
      }

      [Fact]
      public void TheClockStartsWithTheWindowActive() {
         Assert.True(New.EditorViewModel().ShortcutClock.WindowActive);
      }
   }

   #endregion

   #region The pictures

   public class ShortcutIconsTests : BaseViewModelTestClass {
      private static IPixelViewModel Frame() => new ReadonlyPixelViewModel(1, 1, new short[1], -1);

      [Theory]
      [InlineData("Pok\u00e9 Ball", "POKEBALL")]
      [InlineData("\"Pok\u00e9 Ball\"", "POKEBALL")]
      [InlineData("POK\u00e9 BALL", "POKEBALL")]
      [InlineData("Poke Ball", "POKEBALL")]
      [InlineData("  poke-ball ", "POKEBALL")]
      [InlineData("Great Ball", "GREATBALL")]
      [InlineData("", "")]
      [InlineData(null, "")]
      public void NamesAreComparedByTheirLetters(string name, string expected) {
         Assert.Equal(expected, ShortcutIcons.NormalizeName(name));
      }

      [Fact]
      public void PokeBallIsNotAnyOtherBall() {
         Assert.NotEqual(ShortcutIcons.NormalizeName("Pok\u00e9 Ball"), ShortcutIcons.NormalizeName("Master Ball"));
         Assert.NotEqual(ShortcutIcons.NormalizeName("Pok\u00e9 Ball"), ShortcutIcons.NormalizeName("Safari Ball"));
      }

      [Fact]
      public void ModelWithoutTheTables_HasNoIcons_AndNothingThrows() {
         Assert.Null(ShortcutIcons.PokeBall(Model));
         Assert.Null(ShortcutIcons.BrendanStanding(Model));
         Assert.Null(ShortcutIcons.CharacterFront(Model));
         Assert.Null(ShortcutIcons.FlowerAnimation(Model));
         Assert.Equal(-1, ShortcutIcons.FindSpecies(Model, "Cradily"));
         Assert.Null(ShortcutIcons.SpeciesFront(Model, "data.pokemon.stats/1/frontPic/", 3));
      }

      [Fact]
      public void FindSpecies_FindsTheNameWhateverTheCapitalLetters() {
         CreateTextTable(HardcodeTablesModel.PokemonNameTable, 0x100, "LILEEP", "CRADILY", "ANORITH");

         Assert.Equal(1, ShortcutIcons.FindSpecies(Model, "Cradily"));
         Assert.Equal(1, ShortcutIcons.FindSpecies(Model, "CRADILY"));
         Assert.Equal(2, ShortcutIcons.FindSpecies(Model, "anorith"));
         Assert.Equal(-1, ShortcutIcons.FindSpecies(Model, "Exploud"));
      }

      [Fact]
      public void CradilyIsWhatThePokemonButtonShows() {
         Assert.Equal("Cradily", ShortcutIcons.PokemonButtonSpecies);
         Assert.Equal(3000, ShortcutIcons.PokemonButtonIdleMilliseconds);
      }

      [Fact]
      public void PokemonAnimationHook_ReturnsAnAnimationOrNull_ButNeverThrows() {
         var animation = ShortcutIcons.CreatePokemonButtonAnimation(Model, 1);
         if (animation != null) Assert.True(animation.FrameCount >= 1);
      }

      [Fact]
      public void ButtonsThatAreNotPokemonOrItems_AreLeftAlone() {
         var picture = Frame();
         foreach (var destination in new[] { "data.pokemon.moves.stats", "data.trainers.stats", "maps.bank0.littleroot town.0-9 ", "data.items.stats.extra" }) {
            var shortcut = new GotoShortcutViewModel(null, null, picture, destination, destination);

            ShortcutIcons.CustomizeMenuButton(Model, shortcut, destination, "data.items.stats/298/icon/");

            Assert.Same(picture, shortcut.Image);
            Assert.False(shortcut.IsAnimated);
         }
      }

      [Fact]
      public void ItemsButtonKeepsItsPicture_WhenTheRomHasNoPokeBall() {
         var picture = Frame();
         var shortcut = new GotoShortcutViewModel(null, null, picture, HardcodeTablesModel.ItemsTableName, "Items");

         ShortcutIcons.CustomizeMenuButton(Model, shortcut, HardcodeTablesModel.ItemsTableName, "data.items.stats/13/icon/");

         Assert.Same(picture, shortcut.Image);
      }

      [Fact]
      public void PokemonButtonKeepsItsPicture_WhenTheRomHasNoCradily() {
         var picture = Frame();
         var shortcut = new GotoShortcutViewModel(null, null, picture, "data.pokemon.stats/1", "Pokemon");
         CreateTextTable(HardcodeTablesModel.PokemonNameTable, 0x100, "LILEEP", "ANORITH");

         ShortcutIcons.CustomizeMenuButton(Model, shortcut, "data.pokemon.stats/1", "data.pokemon.stats/295/frontPic/");

         Assert.Same(picture, shortcut.Image);
         Assert.False(shortcut.IsAnimated);
      }

      [Fact]
      public void PokemonButtonWithCradily_ButNoSprites_StillKeepsItsPicture() {
         var picture = Frame();
         var shortcut = new GotoShortcutViewModel(null, null, picture, "data.pokemon.stats/1", "Pokemon");
         CreateTextTable(HardcodeTablesModel.PokemonNameTable, 0x100, "LILEEP", "CRADILY");

         ShortcutIcons.CustomizeMenuButton(Model, shortcut, "data.pokemon.stats/1", "data.pokemon.stats/295/frontPic/");

         Assert.Same(picture, shortcut.Image); // the species' picture can't be found, so the old one stays
      }

      [Fact]
      public void PokemonButtonWithAnImageAnchorThatIsNotASpeciesPicture_IsLeftAlone() {
         var picture = Frame();
         var shortcut = new GotoShortcutViewModel(null, null, picture, "data.pokemon.stats/1", "Pokemon");
         CreateTextTable(HardcodeTablesModel.PokemonNameTable, 0x100, "LILEEP", "CRADILY");

         ShortcutIcons.CustomizeMenuButton(Model, shortcut, "data.pokemon.stats/1", "something.else/3/pic");

         Assert.Same(picture, shortcut.Image);
      }

      [Fact]
      public void FirstFrame_KeepsTheTopPoseOfATwoPoseFrontPicture() {
         var pixels = new short[64 * 128];
         for (int i = 0; i < pixels.Length; i++) pixels[i] = (short)(i < 64 * 64 ? 1 : 2);

         var top = ShortcutIcons.FirstFrame(new ReadonlyPixelViewModel(64, 128, pixels, 0x7C1F));

         Assert.Equal(64, top.PixelWidth);
         Assert.Equal(64, top.PixelHeight);
         Assert.All(top.PixelData, pixel => Assert.Equal(1, pixel));
         Assert.Equal(0x7C1F, top.Transparent);
      }

      [Fact]
      public void FirstFrame_LeavesOtherShapesAlone() {
         var square = new ReadonlyPixelViewModel(64, 64, new short[64 * 64], -1);
         var wide = new ReadonlyPixelViewModel(64, 32, new short[64 * 32], -1);
         Assert.Same(square, ShortcutIcons.FirstFrame(square));
         Assert.Same(wide, ShortcutIcons.FirstFrame(wide));
         Assert.Null(ShortcutIcons.FirstFrame(null));
      }

      #region The dark skin and the blue clothes of the Character Customization picture

      private static CharacterColorRow Row(int index, string name) => new(index, name, new ushort[] { 1, 2, 3 });

      [Fact]
      public void FindRow_PrefersTheNamesInOrder_AndNeverRowZero() {
         var rows = new[] { Row(0, "Dark Brown"), Row(1, "Brown"), Row(2, "Ebony"), Row(3, "Dark Brown") };

         Assert.Equal(3, ShortcutIcons.FindRow(rows, "Dark Brown", "Dark", "Ebony", "Brown").Index); // row 0 is the original look, whatever it is called
         Assert.Equal(2, ShortcutIcons.FindRow(rows, "ebony", "Brown").Index);
         Assert.Equal(1, ShortcutIcons.FindRow(rows, "Dark", "BROWN").Index);
         Assert.Null(ShortcutIcons.FindRow(rows, "Blue"));
         Assert.Null(ShortcutIcons.FindRow(null, "Blue"));
      }

      [Fact]
      public void FindRow_IgnoresPaddingAroundTheName() {
         Assert.Equal(1, ShortcutIcons.FindRow(new[] { Row(0, "Original"), Row(1, " Blue ") }, "Blue").Index);
      }

      #endregion

      #region The flower's metatile

      private static byte[] Block(params int[] entries) {
         var block = new byte[16];
         for (int i = 0; i < entries.Length && i < 8; i++) {
            block[i * 2] = (byte)(entries[i] & 0xFF);
            block[i * 2 + 1] = (byte)(entries[i] >> 8);
         }
         return block;
      }

      [Fact]
      public void FindBlockUsingTiles_FindsTheMetatileWhoseTopLayerIsTheAnimatedTiles() {
         var blocks = new[] {
            Block(1, 1, 1, 1, 0, 0, 0, 0),
            Block(2, 3, 3, 2, 508, 509, 510, 511),
            Block(2, 3, 3, 2, 508, 509, 510, 511),
         };

         Assert.Equal(1, ShortcutIcons.FindBlockUsingTiles(blocks, 508));
      }

      [Fact]
      public void FindBlockUsingTiles_IgnoresThePaletteAndFlipBitsOfTheEntries() {
         var withPalette2 = 0x2000;
         var blocks = new[] { Block(2, 3, 3, 2, withPalette2 | 508, withPalette2 | 509, withPalette2 | 510, withPalette2 | 511) };

         Assert.Equal(0, ShortcutIcons.FindBlockUsingTiles(blocks, 508));
      }

      [Fact]
      public void FindBlockUsingTiles_NeedsTheTilesInTheirOrder() {
         var blocks = new[] { Block(2, 3, 3, 2, 509, 508, 510, 511), Block(2, 3, 3, 2, 508, 509, 510, 512) };

         Assert.Equal(-1, ShortcutIcons.FindBlockUsingTiles(blocks, 508));
      }

      [Fact]
      public void FindBlockUsingTiles_AlsoLooksAtTheBottomLayer() {
         var blocks = new[] { Block(508, 509, 510, 511, 0, 0, 0, 0) };

         Assert.Equal(0, ShortcutIcons.FindBlockUsingTiles(blocks, 508));
      }

      [Fact]
      public void FindBlockUsingTiles_CopesWithShortAndMissingBlocks() {
         var blocks = new byte[][] { null, new byte[4], Block(0, 0, 0, 0, 508, 509, 510, 511) };

         Assert.Equal(2, ShortcutIcons.FindBlockUsingTiles(blocks, 508));
         Assert.Equal(-1, ShortcutIcons.FindBlockUsingTiles(new byte[0][], 508));
      }

      #endregion
   }

   #endregion
}
