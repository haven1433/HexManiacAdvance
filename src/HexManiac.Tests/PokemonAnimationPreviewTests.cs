using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.PokemonAnimations;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// The preview under frontAnimFrames: it must do no work while nobody can see it, build once per burst of changes, and play without allocating.
   /// The pictures are synthetic (the real ROM is covered by the integration run); the two seams for the signature and the timeline are overridden.
   /// </summary>
   public class PokemonAnimationPreviewTests : BaseViewModelTestClass {
      private const int Canvas = 96;
      /// <summary>A movement id the engine does not know: the sprite stays where it is, so the pictures only change with the frame list.</summary>
      private const int Unmoving = 200;
      private const int HorizontalSlide = 3;

      public PokemonAnimationPreviewTests() : base(0x400) { }

      private class FakePreview : PokemonAnimationPreviewViewModel {
         public string Signature = "a";
         public int Created;
         public IReadOnlyList<short> LastColors;
         public Func<PokemonAnimationTimeline> Factory = () => MakeTimeline(3);

         public FakePreview(IDataModel model, int species, IDelayWorkTimer timer, Func<IReadOnlyList<short>> palette = null, Func<long> token = null, Func<INotifyPropertyChanged> source = null)
            : base(model, species, timer, palette, token, source) { }

         protected override string ComputeSignature() => Signature;

         protected override PokemonAnimationTimeline CreateTimeline(IReadOnlyList<short> colors) {
            Created++;
            LastColors = colors;
            return Factory();
         }
      }

      private class PictureSource : INotifyPropertyChanged {
         public event PropertyChangedEventHandler PropertyChanged;
         public bool HasListeners => PropertyChanged != null;
         public void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
      }

      private static byte[] Picture(int index) {
         var result = new byte[64 * 64];
         for (int i = 0; i < result.Length; i++) result[i] = (byte)index;
         return result;
      }

      private static ushort[] Palette() {
         var palette = new ushort[16];
         for (int i = 1; i < 16; i++) palette[i] = (ushort)(i | (i << 5) | ((15 - i) << 10));
         return palette;
      }

      /// <summary>Shows one picture per command, without movement (so three pictures, or a single one).</summary>
      private static PokemonAnimationTimeline MakeTimeline(int pictures, int animId = Unmoving, int idle = 0) {
         var commands = new List<FrameCommand>();
         for (int i = 0; i < pictures; i++) commands.Add(FrameCommand.Frame(i, 6 * (i + 1)));
         commands.Add(FrameCommand.End);
         var simulation = PokemonAnimationEngine.Simulate(new PokemonAnimationRequest { AnimId = animId, FrameCommands = commands, Palette = Palette() });
         var images = Enumerable.Range(0, pictures).Select(i => Picture(i + 1)).ToArray();
         return PokemonAnimationTimeline.Build(simulation, images, animId, Canvas, idle, true);
      }

      private FakePreview Create(ManualWorkTimer timer, int species = 1, Func<IReadOnlyList<short>> palette = null, Func<long> token = null, Func<INotifyPropertyChanged> source = null) {
         return new FakePreview(Model, species, timer, palette, token, source);
      }

      private static FakePreview Show(FakePreview preview, ManualWorkTimer timer) {
         preview.IsDisplayed = true;
         timer.RunWork();
         return preview;
      }

      #region cost model

      [Fact]
      public void NothingIsBuilt_WhileNotDisplayed() {
         var timer = new ManualWorkTimer();
         var preview = Create(timer);

         preview.RequestUpdate();
         preview.Update();
         timer.RunWork();

         Assert.Equal(0, preview.Created);
         Assert.Equal(0, preview.BuildCount);
         Assert.False(timer.HasScheduledWork);
         Assert.False(preview.HasAnimation);
         Assert.False(preview.WantsTicks);
      }

      [Fact]
      public void NothingIsBuilt_WhileCollapsed() {
         var timer = new ManualWorkTimer();
         var preview = Create(timer);
         preview.Visible = false;
         preview.IsDisplayed = true;

         preview.RequestUpdate();
         preview.Update();
         timer.RunWork();

         Assert.Equal(0, preview.Created);
         Assert.False(timer.HasScheduledWork);
         Assert.False(preview.WantsTicks);
      }

      [Fact]
      public void Displaying_WithNothingShownYet_BuildsAtOnce_NotAfterTheDebounce() {
         var timer = new ManualWorkTimer();
         var preview = Create(timer);

         preview.IsDisplayed = true;

         // an empty preview must never be on screen for a debounce interval, so the first picture is not delayed
         Assert.False(timer.HasScheduledWork);
         Assert.Equal(1, preview.Created);
         Assert.Equal(1, preview.BuildCount);
         Assert.True(preview.HasAnimation);
         Assert.Equal(3, preview.Timeline.Frames.Count);
      }

      [Fact]
      public void Displaying_AnAlreadyBuiltPreview_OnlyChecksAfterTheDebounce() {
         var timer = new ManualWorkTimer();
         var preview = Show(Create(timer), timer);
         preview.IsDisplayed = false;

         preview.IsDisplayed = true;   // the pictures are very likely still right: they stay, and are checked like any other change

         Assert.True(preview.HasAnimation);
         Assert.True(timer.HasScheduledWork);
         timer.RunWork();
         Assert.Equal(1, preview.Created);
      }

      [Fact]
      public void ManyChanges_AreOneBuild() {
         var timer = new ManualWorkTimer();
         var preview = Show(Create(timer), timer);
         Assert.Equal(1, preview.Created);

         for (int i = 0; i < 20; i++) { preview.Signature = "s" + i; preview.RequestUpdate(); }
         Assert.Equal(1, preview.Created);
         timer.RunWork();
         timer.RunWork(); // nothing left to run

         Assert.Equal(2, preview.Created);
      }

      [Fact]
      public void UnchangedSignature_DoesNotRebuild_ChangedSignatureDoes() {
         var timer = new ManualWorkTimer();
         var preview = Show(Create(timer), timer);
         Assert.Equal(1, preview.Created);

         preview.RequestUpdate();
         timer.RunWork();
         Assert.Equal(1, preview.Created);

         preview.Signature = "b";
         preview.RequestUpdate();
         timer.RunWork();
         Assert.Equal(2, preview.Created);
      }

      [Fact]
      public void ChangedPicture_Rebuilds_WithoutAChangedSignature() {
         var timer = new ManualWorkTimer();
         long token = 1;
         var preview = Show(Create(timer, token: () => token), timer);
         Assert.Equal(1, preview.Created);

         preview.RequestUpdate();
         timer.RunWork();
         Assert.Equal(1, preview.Created);

         token = 2; // the sprite or its palette changed
         preview.RequestUpdate();
         timer.RunWork();
         Assert.Equal(2, preview.Created);
      }

      [Fact]
      public void TheSelectedPalette_IsPassedOn()  {
         var timer = new ManualWorkTimer();
         IReadOnlyList<short> colors = Enumerable.Range(0, 16).Select(i => (short)i).ToArray();
         var preview = Show(Create(timer, palette: () => colors), timer);
         Assert.Same(colors, preview.LastColors);
      }

      [Fact]
      public void AShortOrMissingPalette_FallsBackToTheSpeciesPalette() {
         var timer = new ManualWorkTimer();
         var preview = Show(Create(timer, palette: () => new short[] { 1, 2, 3 }), timer);
         Assert.Null(preview.LastColors);

         var other = Show(Create(timer, palette: () => null), timer);
         Assert.Null(other.LastColors);

         var broken = Show(Create(timer, palette: () => throw new InvalidOperationException()), timer);
         Assert.Null(broken.LastColors);
         Assert.True(broken.HasAnimation);
      }

      [Fact]
      public void ChoosingAnotherPalette_IsHeardFromTheSprite_OnlyWhileOnScreen() {
         var timer = new ManualWorkTimer();
         var sprite = new PictureSource();
         long token = 1;
         var preview = Create(timer, token: () => token, source: () => sprite);
         Assert.False(sprite.HasListeners);   // nobody looks: nobody listens

         Show(preview, timer);
         Assert.True(sprite.HasListeners);
         Assert.Equal(1, preview.Created);

         sprite.Raise("SomethingElse");
         Assert.False(timer.HasScheduledWork);

         token = 2;   // the other palette
         sprite.Raise(nameof(Core.ViewModels.Images.IPixelViewModel.PixelData));
         Assert.True(timer.HasScheduledWork);
         timer.RunWork();
         Assert.Equal(2, preview.Created);

         preview.Visible = false;
         Assert.False(sprite.HasListeners);
         preview.Visible = true;
         timer.RunWork();
         Assert.True(sprite.HasListeners);
         preview.IsDisplayed = false;
         Assert.False(sprite.HasListeners);
      }

      [Fact]
      public void Hiding_CancelsPendingWork_AndStopsTheTicks() {
         var timer = new ManualWorkTimer();
         var preview = Show(Create(timer), timer);
         Assert.True(preview.WantsTicks);

         preview.RequestUpdate();
         Assert.True(timer.HasScheduledWork);
         preview.Visible = false;
         Assert.False(timer.HasScheduledWork);
         Assert.False(preview.WantsTicks);

         preview.Visible = true;
         Assert.True(preview.WantsTicks);

         preview.IsDisplayed = false; // tab switched, window deactivated, view removed
         Assert.False(timer.HasScheduledWork);
         Assert.False(preview.WantsTicks);
      }

      [Fact]
      public void ChangesWhileHidden_AreCatchedUpWhenShownAgain() {
         var timer = new ManualWorkTimer();
         var preview = Show(Create(timer), timer);
         preview.IsDisplayed = false;

         preview.Signature = "b";
         preview.RequestUpdate();
         Assert.False(timer.HasScheduledWork);
         Assert.Equal(1, preview.Created);

         preview.IsDisplayed = true;
         timer.RunWork();
         Assert.Equal(2, preview.Created);
      }

      [Fact]
      public void ChangingThePokemon_KeepsTheOldAnimationOnScreen_UntilTheNewOneIsReady() {
         var timer = new ManualWorkTimer();
         var old = Show(Create(timer, species: 1), timer);
         var oldPixels = old.Surface.PixelData;
         var oldStatus = old.Status;
         var changes = new List<string>();
         old.PropertyChanged += (sender, e) => changes.Add(e.PropertyName);
         old.Surface.PropertyChanged += (sender, e) => changes.Add("Surface." + e.PropertyName);

         Assert.True(old.TryCopy(Create(timer, species: 2)));

         // what is on screen is still the old pokemon, complete: nothing collapsed, nothing blanked, nothing announced
         Assert.True(old.HasAnimation);
         Assert.True(old.WantsTicks);
         Assert.Same(oldPixels, old.Surface.PixelData);
         Assert.Equal(oldStatus, old.Status);
         Assert.Equal(1, old.Created);
         Assert.Empty(changes);
      }

      [Fact]
      public void ChangingThePokemon_ThenUpdateNow_ReplacesTheAnimationInOneStep() {
         var timer = new ManualWorkTimer();
         var old = Show(Create(timer, species: 1), timer);
         var oldPixels = old.Surface.PixelData;
         old.Factory = () => MakeTimeline(2);
         var changes = new List<string>();
         old.PropertyChanged += (sender, e) => changes.Add(e.PropertyName);
         old.Surface.PropertyChanged += (sender, e) => changes.Add("Surface." + e.PropertyName);
         Assert.True(old.TryCopy(Create(timer, species: 2)));

         old.UpdateNow();

         Assert.Equal(2, old.Created);
         Assert.True(old.HasAnimation);
         Assert.True(old.WantsTicks);
         Assert.Equal(2, old.Timeline.Frames.Count);
         Assert.NotSame(oldPixels, old.Surface.PixelData);
         Assert.False(timer.HasScheduledWork);   // the debounce of the copy was replaced by the immediate build
         Assert.DoesNotContain(nameof(PokemonAnimationPreviewViewModel.HasAnimation), changes);   // there never was a moment without an animation
         Assert.Equal(1, changes.Count(change => change == "Surface." + nameof(IPixelViewModel.PixelData)));
         Assert.True(changes.Count(change => change == nameof(PokemonAnimationPreviewViewModel.Status)) <= 1);   // at most one announcement, at the same moment as the picture
      }

      [Fact]
      public void ChangingThePokemon_WithoutUpdateNow_CatchesUpAfterTheDebounce() {
         var timer = new ManualWorkTimer();
         var old = Show(Create(timer, species: 1), timer);
         Assert.True(old.TryCopy(Create(timer, species: 2)));

         Assert.True(timer.HasScheduledWork);
         timer.RunWork();

         Assert.Equal(2, old.Created);
         Assert.True(old.HasAnimation);
      }

      [Fact]
      public void ChangingThePokemon_ToOneWithoutAnimation_SaysSo_AtTheSameMoment() {
         var timer = new ManualWorkTimer();
         var old = Show(Create(timer, species: 1), timer);
         old.Factory = () => null;
         Assert.True(old.TryCopy(Create(timer, species: 2)));
         Assert.True(old.HasAnimation);   // not before the new pokemon is known

         old.UpdateNow();

         Assert.False(old.HasAnimation);
         Assert.False(old.WantsTicks);
         Assert.NotEqual(string.Empty, old.Status);
      }

      [Fact]
      public void ChangingThePokemon_WhileNobodyLooks_ForgetsTheOldPictures_AndBuildsWhenShown() {
         var timer = new ManualWorkTimer();
         var old = Show(Create(timer, species: 1), timer);
         old.IsDisplayed = false;

         Assert.True(old.TryCopy(Create(timer, species: 2)));

         Assert.False(old.HasAnimation);   // the old pokemon's pictures must not be what shows up the next time
         Assert.False(timer.HasScheduledWork);
         Assert.Equal(1, old.Created);
         old.IsDisplayed = true;
         Assert.Equal(2, old.Created);   // nothing is shown first, so it is built at once
         Assert.True(old.HasAnimation);
      }

      [Fact]
      public void UpdateNow_CancelsThePendingDebounce_AndBuildsOnce() {
         var timer = new ManualWorkTimer();
         var preview = Show(Create(timer), timer);
         preview.Signature = "b";
         preview.RequestUpdate();
         Assert.True(timer.HasScheduledWork);

         preview.UpdateNow();

         Assert.False(timer.HasScheduledWork);
         Assert.Equal(2, preview.Created);
         timer.RunWork();
         Assert.Equal(2, preview.Created);
      }

      [Fact]
      public void UpdateNow_WhileNobodyLooks_BuildsNothing() {
         var timer = new ManualWorkTimer();
         var preview = Create(timer);

         preview.UpdateNow();

         Assert.Equal(0, preview.Created);
         Assert.False(preview.HasAnimation);
      }

      [Fact]
      public void CopyingTheCollapsedState_NeverBuildsBeforeTheTableToolSaysItIsDone() {
         var timer = new ManualWorkTimer();
         var preview = Show(Create(timer, species: 1), timer);
         var collapsed = Create(timer, species: 1);
         collapsed.Visible = false;

         Assert.True(preview.TryCopy(collapsed));
         Assert.False(preview.Visible);
         Assert.False(preview.WantsTicks);
         Assert.False(timer.HasScheduledWork);
         Assert.Equal(1, preview.Created);

         Assert.True(preview.TryCopy(Create(timer, species: 1)));   // expanded again by the copy: checked after the debounce, not on the spot
         Assert.True(preview.Visible);
         Assert.Equal(1, preview.Created);
         Assert.True(timer.HasScheduledWork);
      }

      [Fact]
      public void RefreshingTheSamePokemon_KeepsThePlayingAnimation() {
         var timer = new ManualWorkTimer();
         var preview = Show(Create(timer, species: 1), timer);
         preview.Advance(120);
         var frame = preview.CurrentFrameIndex;
         Assert.True(frame > 0);

         Assert.True(preview.TryCopy(Create(timer, species: 1))); // the table tool rebuilds its groups on every change
         Assert.True(preview.HasAnimation);
         Assert.Equal(frame, preview.CurrentFrameIndex);
         timer.RunWork();
         Assert.Equal(1, preview.Created);
      }

      [Fact]
      public void TryCopy_RejectsOtherElements() {
         var timer = new ManualWorkTimer();
         var preview = Create(timer);
         Assert.False(preview.TryCopy(null));
         Assert.False(preview.TryCopy(new FakePreview(new PokemonModel(new byte[0x10], singletons: Singletons), 1, timer)));
      }

      #endregion

      #region playing

      [Fact]
      public void Advance_ShowsThePictureOfTheTime_AndLoops() {
         var timer = new ManualWorkTimer();
         var preview = Show(Create(timer), timer);
         var durations = preview.Timeline.DurationsMs.ToArray();
         Assert.Equal(3, durations.Length);
         Assert.Equal(durations.Sum(), preview.TimelineMilliseconds);
         Assert.Equal(0, preview.CurrentFrameIndex);
         Assert.Same(preview.Timeline.Frames[0].PixelData, preview.Surface.PixelData);

         preview.Advance(durations[0] - 1);
         Assert.Equal(0, preview.CurrentFrameIndex);
         preview.Advance(1);
         Assert.Equal(1, preview.CurrentFrameIndex);
         Assert.Same(preview.Timeline.Frames[1].PixelData, preview.Surface.PixelData);
         preview.Advance(durations[1]);
         Assert.Equal(2, preview.CurrentFrameIndex);
         Assert.Same(preview.Timeline.Frames[2].PixelData, preview.Surface.PixelData);
         preview.Advance(durations[2] - 1);
         Assert.Equal(2, preview.CurrentFrameIndex);
         preview.Advance(1);   // the end of the timeline: back to the start
         Assert.Equal(0, preview.CurrentFrameIndex);
         Assert.Same(preview.Timeline.Frames[0].PixelData, preview.Surface.PixelData);
      }

      [Fact]
      public void Advance_OverSeveralLoops_LandsOnTheRightPicture() {
         var timer = new ManualWorkTimer();
         var preview = Show(Create(timer), timer);
         var durations = preview.Timeline.DurationsMs.ToArray();

         preview.Advance(1000);   // the host caps one tick at a second
         var expected = 1000 % durations.Sum();
         var index = 0;
         for (int start = 0; index + 1 < durations.Length && start + durations[index] <= expected; index++) start += durations[index];

         Assert.Equal(index, preview.CurrentFrameIndex);
      }

      [Fact]
      public void Advance_IgnoresNonsense() {
         var timer = new ManualWorkTimer();
         var preview = Show(Create(timer), timer);
         preview.Advance(0);
         preview.Advance(-5);
         Assert.Equal(0, preview.CurrentFrameIndex);

         var none = Create(timer);
         none.Advance(100); // not displayed, nothing built
         Assert.Equal(0, none.CurrentFrameIndex);
      }

      [Fact]
      public void Pause_FreezesThePicture_AndStopsTheTicks()  {
         var timer = new ManualWorkTimer();
         var preview = Show(Create(timer), timer);
         Assert.Equal("Pause", preview.PlayLabel);
         var changes = new List<string>();
         preview.PropertyChanged += (sender, e) => changes.Add(e.PropertyName);

         preview.TogglePlay.Execute(null);

         Assert.False(preview.IsPlaying);
         Assert.Equal("Play", preview.PlayLabel);
         Assert.False(preview.WantsTicks);
         Assert.Contains(nameof(PokemonAnimationPreviewViewModel.WantsTicks), changes);
         Assert.Contains(nameof(PokemonAnimationPreviewViewModel.PlayLabel), changes);
         preview.Advance(150);
         Assert.Equal(0, preview.CurrentFrameIndex);

         preview.TogglePlay.Execute(null);
         Assert.True(preview.WantsTicks);
         preview.Advance(150);
         Assert.True(preview.CurrentFrameIndex > 0);
      }

      [Fact]
      public void Replay_StartsOver_AndResumes() {
         var timer = new ManualWorkTimer();
         var preview = Show(Create(timer), timer);
         preview.Advance(200);
         Assert.True(preview.CurrentFrameIndex > 0);
         preview.IsPlaying = false;

         preview.Replay.Execute(null);

         Assert.True(preview.IsPlaying);
         Assert.True(preview.WantsTicks);
         Assert.Equal(0, preview.CurrentFrameIndex);
         Assert.Same(preview.Timeline.Frames[0].PixelData, preview.Surface.PixelData);
      }

      [Fact]
      public void Replay_WithoutAnAnimation_DoesNotThrow() {
         var preview = Create(new ManualWorkTimer());
         preview.Replay.Execute(null);
         Assert.True(preview.IsPlaying);
         Assert.False(preview.WantsTicks);
      }

      [Fact]
      public void AStillPicture_NeedsNoTimer() {
         var timer = new ManualWorkTimer();
         var preview = Create(timer);
         preview.Factory = () => MakeTimeline(1);
         Show(preview, timer);

         Assert.True(preview.HasAnimation);
         Assert.Equal(1, preview.Timeline.Frames.Count);
         Assert.False(preview.WantsTicks);
         preview.Advance(5000);
         Assert.Equal(0, preview.CurrentFrameIndex);
      }

      [Fact]
      public void WantsTicks_IsAnnouncedOnlyWhenItChanges() {
         var timer = new ManualWorkTimer();
         var preview = Create(timer);
         var count = 0;
         preview.PropertyChanged += (sender, e) => { if (e.PropertyName == nameof(PokemonAnimationPreviewViewModel.WantsTicks)) count++; };

         preview.IsDisplayed = true;
         timer.RunWork();
         Assert.Equal(1, count);   // the build made it true
         preview.Visible = true;
         preview.IsPlaying = true;
         preview.IsDisplayed = true;
         Assert.Equal(1, count);
         preview.Visible = false;
         Assert.Equal(2, count);
         preview.IsDisplayed = false;
         Assert.Equal(2, count);   // already false
      }

      #endregion

      #region surface

      [Fact]
      public void Surface_SwapsThePixelsWithoutCopying_AndReusesTheNotification() {
         var surface = new PokemonAnimationSurface();
         var first = new ReadonlyPixelViewModel(Canvas, Canvas, new short[Canvas * Canvas], -1);
         var second = new ReadonlyPixelViewModel(Canvas, Canvas, new short[Canvas * Canvas], -1);
         var args = new List<PropertyChangedEventArgs>();
         surface.PropertyChanged += (sender, e) => args.Add(e);

         surface.Show(first);
         surface.Show(first);   // nothing changed
         surface.Show(second);
         surface.Show(first);

         Assert.Same(first.PixelData, surface.PixelData);
         Assert.Equal(3, args.Count);
         Assert.All(args, e => Assert.Equal(nameof(PokemonAnimationSurface.PixelData), e.PropertyName));
         Assert.Same(args[0], args[1]);
         Assert.Same(args[1], args[2]);
         Assert.Equal(-1, surface.Transparent);
      }

      [Fact]
      public void Surface_AnnouncesASizeChange() {
         var surface = new PokemonAnimationSurface();
         var names = new List<string>();
         surface.PropertyChanged += (sender, e) => names.Add(e.PropertyName);

         surface.Show(new ReadonlyPixelViewModel(128, 128, null, -1));

         Assert.Equal(128, surface.PixelWidth);
         Assert.Equal(128, surface.PixelHeight);
         Assert.Contains(nameof(PokemonAnimationSurface.PixelWidth), names);
         Assert.Contains(nameof(PokemonAnimationSurface.PixelHeight), names);
         surface.Show(null);   // ignored
         Assert.Equal(128, surface.PixelWidth);
      }

      #endregion

      #region status

      [Fact]
      public void Status_SaysWhetherTheAnimationIsExact() {
         var timer = new ManualWorkTimer();
         var exact = Create(timer);
         exact.Factory = () => MakeTimeline(3, HorizontalSlide);
         Show(exact, timer);
         Assert.True(exact.Timeline.IsFaithful);
         Assert.StartsWith("As in battle", exact.Status);

         var approximate = Create(timer);
         approximate.Factory = () => MakeTimeline(3, Unmoving);   // a movement the engine does not know
         Show(approximate, timer);
         Assert.False(approximate.Timeline.IsFaithful);
         Assert.StartsWith("Approximation", approximate.Status);

         var nothing = Create(timer);
         nothing.Factory = () => null;
         Show(nothing, timer);
         Assert.False(nothing.HasAnimation);
         Assert.False(nothing.WantsTicks);
         Assert.StartsWith("No animation", nothing.Status);
      }

      [Fact]
      public void TheHeader_NamesTheSection() {
         Assert.Equal("In-game animation", Create(new ManualWorkTimer()).Header);
      }

      #endregion

      #region hook

      [Fact]
      public void OnlyTheFramesFieldOfThePokemonTable_GetsAPreview() {
         var other = new ArrayRunElementSegment("frontAnimId", ElementContentType.Integer, 1);
         var frames = new ArrayRunElementSegment(PokemonAnimationPreviewViewModel.FramesFieldName, ElementContentType.Integer, 4);
         ViewPort.Edit("^table[a:]4 ");
         var table = Model.GetTable("table");

         Assert.False(PokemonAnimationPreviewViewModel.IsFramesField(Model, table, other));
         Assert.False(PokemonAnimationPreviewViewModel.IsFramesField(Model, table, null));
         Assert.False(PokemonAnimationPreviewViewModel.IsFramesField(Model, table, frames));   // the right name, but not the pokemon table
      }

      #endregion
   }
}
