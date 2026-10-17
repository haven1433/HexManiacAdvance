using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.PokemonAnimations;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   /// <summary>
   /// The picture of the preview. The player only swaps which pictures' pixels it points at, so a tick costs no allocation:
   /// <see cref="Show"/> changes <see cref="PixelData"/> and raises one cached notification.
   /// </summary>
   public class PokemonAnimationSurface : ViewModelCore, IPixelViewModel {
      private static readonly PropertyChangedEventArgs PixelDataChanged = new PropertyChangedEventArgs(nameof(PixelData));

      public short Transparent => -1; // opaque pictures are copied straight into the bitmap, see PixelImage.UpdateSource
      public int PixelWidth { get; private set; } = PokemonAnimationTimeline.DefaultCanvasSize;
      public int PixelHeight { get; private set; } = PokemonAnimationTimeline.DefaultCanvasSize;
      public short[] PixelData { get; private set; } = new short[PokemonAnimationTimeline.DefaultCanvasSize * PokemonAnimationTimeline.DefaultCanvasSize];
      public double SpriteScale => 1;

      private IPixelViewModel current;
      public IPixelViewModel Current => current;

      public void Show(IPixelViewModel frame) {
         if (ReferenceEquals(frame, current)) return;
         current = frame;
         if (frame == null) return;
         var sizeChanged = frame.PixelWidth != PixelWidth || frame.PixelHeight != PixelHeight;
         PixelWidth = frame.PixelWidth;
         PixelHeight = frame.PixelHeight;
         PixelData = frame.PixelData;
         if (sizeChanged) {
            NotifyPropertyChanged(nameof(PixelWidth));
            NotifyPropertyChanged(nameof(PixelHeight));
         }
         NotifyPropertyChanged(PixelDataChanged);
      }
   }

   /// <summary>
   /// Shown in the table tool under the frontAnimFrames field of a pokemon: the front sprite as the game animates it in battle
   /// (movement, the second picture and the glow colours), looped with a short pause.
   ///
   /// Cost model: nothing is built until the preview is on screen (<see cref="IsDisplayed"/>, set by the view) and visible
   /// (<see cref="Visible"/>, the section is not collapsed). Changes are debounced into a single build of one cached
   /// <see cref="PokemonAnimationTimeline"/> (a few milliseconds, at most a few hundred 96x96 pictures). The view runs one timer, only
   /// while <see cref="WantsTicks"/>, and calls <see cref="Advance"/>; advancing allocates nothing.
   /// </summary>
   public class PokemonAnimationPreviewViewModel : ViewModelCore, IArrayElementViewModel {
      public const string FramesFieldName = "frontAnimFrames";
      public const string Title = "In-game animation";
      public const int CanvasSize = PokemonAnimationTimeline.DefaultCanvasSize;
      /// <summary>How long the resting pose is held before the animation loops.</summary>
      public const int IdleMilliseconds = 700;
      public const int DebounceMilliseconds = 150;

      private static readonly PropertyChangedEventArgs WantsTicksChanged = new PropertyChangedEventArgs(nameof(WantsTicks));

      private readonly IDelayWorkTimer debounce;
      private IDataModel model;
      private int speciesIndex;
      private Func<IReadOnlyList<short>> paletteProvider;
      private Func<long> pictureToken;
      private Func<INotifyPropertyChanged> pictureSource;
      private INotifyPropertyChanged watched;

      private PokemonAnimationTimeline timeline;
      private int[] starts = Array.Empty<int>();
      private int totalMilliseconds;
      private int elapsed, frameIndex;
      private string builtSignature;
      private long builtToken;
      private bool dirty = true;
      private bool lastWantsTicks;

      /// <summary>The picture to draw (an opaque 96x96 image whose pixels change as the animation plays).</summary>
      public PokemonAnimationSurface Surface { get; } = new PokemonAnimationSurface();

      public string Header => Title;

      /// <summary>How many timelines this preview has built. For tests and diagnostics.</summary>
      public int BuildCount { get; private set; }

      public int CurrentFrameIndex => frameIndex;

      public int TimelineMilliseconds => totalMilliseconds;

      public PokemonAnimationTimeline Timeline => timeline;

      public bool HasAnimation => timeline != null;
      private bool lastHasAnimation;

      private string status = string.Empty;
      public string Status { get => status; private set => Set(ref status, value); }

      private bool isPlaying = true;
      public bool IsPlaying {
         get => isPlaying;
         set => Set(ref isPlaying, value, old => { NotifyPropertyChanged(nameof(PlayLabel)); UpdateWantsTicks(); });
      }

      public string PlayLabel => isPlaying ? "Pause" : "Play";

      private bool isDisplayed;
      /// <summary>Set by the view: true while the preview is actually on screen (loaded, shown, in the active window).</summary>
      public bool IsDisplayed {
         get => isDisplayed;
         set => Set(ref isDisplayed, value, old => { if (isDisplayed) RequestShowing(); else { debounce.Reset(); Unwatch(); } UpdateWantsTicks(); });
      }

      private bool visible = true;
      public bool Visible {
         get => visible;
         set => Set(ref visible, value, old => { if (visible) { if (copying) RequestUpdate(); else RequestShowing(); } else { debounce.Reset(); Unwatch(); } UpdateWantsTicks(); });
      }

      /// <summary>True while the table tool copies a freshly made preview into this one: nothing is built before it has finished (see <see cref="UpdateNow"/>).</summary>
      private bool copying;

      /// <summary>True while the view should deliver ticks: something is on screen and playing, and there is more than one picture to show.</summary>
      public bool WantsTicks => isPlaying && isDisplayed && visible && timeline != null && starts.Length > 1 && totalMilliseconds > 0;

      private StubCommand togglePlay, replay;
      public ICommand TogglePlay => StubCommand(ref togglePlay, () => IsPlaying = !IsPlaying);
      public ICommand Replay => StubCommand(ref replay, ExecuteReplay);

      #region IArrayElementViewModel

      private string theme; public string Theme { get => theme; set => Set(ref theme, value); }
      public bool IsInError => false;
      public string ErrorText => string.Empty;
      public int ZIndex => 0;
      event EventHandler IArrayElementViewModel.DataChanged { add { } remove { } }
      event EventHandler IArrayElementViewModel.DataSelected { add { } remove { } }

      public bool TryCopy(IArrayElementViewModel other) {
         if (other is not PokemonAnimationPreviewViewModel that || !ReferenceEquals(that.model, model)) return false;
         if (that.speciesIndex != speciesIndex) {
            dirty = true;
            speciesIndex = that.speciesIndex;
            // While the preview is on screen, the old pokemon's pictures stay exactly where they are (nothing collapses, nothing blanks) until the table tool
            // has finished the sprites next to it and calls UpdateNow: the new animation then replaces the old one in the same step as the new sprites.
            // If nobody can see the preview, there is nothing to keep steady, and the old pictures must not be what shows up the next time it is displayed.
            if (!isDisplayed || !visible) DropTimeline();
         }
         paletteProvider = that.paletteProvider;
         pictureToken = that.pictureToken;
         pictureSource = that.pictureSource;
         copying = true;
         try { Visible = that.visible; } finally { copying = false; }
         RequestUpdate();
         return true;
      }

      #endregion

      #region Creation

      /// <param name="model">The ROM.</param>
      /// <param name="speciesIndex">The row of the pokemon table.</param>
      /// <param name="debounce">Delays the build until the changes stop coming.</param>
      /// <param name="paletteProvider">The colours (HexManiac order) to draw with, for example the palette selected next to the sprite. Null or returning null means the species' own palette.</param>
      /// <param name="pictureToken">Changes whenever the sprite's pixels or colours change, so a build is only repeated if it can look different.</param>
      /// <param name="pictureSource">The sprite next to the preview. Choosing another palette there changes no data, so the preview listens to the sprite (only while it is on screen).</param>
      public PokemonAnimationPreviewViewModel(IDataModel model, int speciesIndex, IDelayWorkTimer debounce, Func<IReadOnlyList<short>> paletteProvider = null, Func<long> pictureToken = null, Func<INotifyPropertyChanged> pictureSource = null) {
         this.model = model;
         this.speciesIndex = speciesIndex;
         this.debounce = debounce ?? new ImmediateWorkTimer();
         this.paletteProvider = paletteProvider;
         this.pictureToken = pictureToken;
         this.pictureSource = pictureSource;
      }

      /// <summary>
      /// True for the field that gets the preview under it: frontAnimFrames of the pokemon table when it holds the animation (pokeemerald-expansion, CUBE),
      /// otherwise the picture field of the front sprite table of the unedited games (the sprite and its animation are next to each other there).
      /// </summary>
      public static bool IsFramesField(IDataModel model, ITableRun table, ArrayRunElementSegment item) {
         if (item == null || table == null) return false;
         if (string.Equals(item.Name, FramesFieldName, StringComparison.Ordinal)) {
            return model.GetAnchorFromAddress(-1, table.Start) == HardcodeTablesModel.PokemonStatsTable;
         }
         // the cheap tests first: this is asked for every field the table tool shows
         // (a table has many pointer fields, the model's anchor lookup is only worth it for the one that can be the picture)
         if (item is not ArrayRunPointerSegment) return false;
         if (item.Name != "sprite" && !ReferenceEquals(item, table.ElementContent.FirstOrDefault(segment => segment is ArrayRunPointerSegment))) return false;
         if (model.GetAnchorFromAddress(-1, table.Start) != HardcodeTablesModel.FrontSpritesTable) return false;
         return PokemonAnimationLayout.Find(model) is SeparateTablesLayout layout && layout.IsPictureTable(table) && layout.PictureFieldName == item.Name;
      }

      /// <param name="fieldAddress">The address of the field the preview was asked for (see <see cref="IsFramesField"/>).</param>
      public static PokemonAnimationPreviewViewModel Create(ViewPort viewPort, ITableRun table, int elementIndex, int fieldAddress = -1) {
         var species = elementIndex;
         int picture;
         if (PokemonAnimationLayout.Find(viewPort.Model) is SeparateTablesLayout separate) {
            // the front sprite table: the preview belongs to the species of this row, and the sprite next to it is the one this field points at
            species = PokemonAnimationLayout.SpeciesOfRow(table, elementIndex);
            picture = fieldAddress >= 0 ? fieldAddress : separate.FrontPictureAddress(viewPort.Model, species);
         } else {
            picture = FieldAddress(table, elementIndex, "frontPic");
         }
         // the sprite next to this preview knows which palette is selected; it is looked up when a build starts (the group is complete by then)
         SpriteElementViewModel Sibling() => viewPort.Tools.TableTool.Children.OfType<SpriteElementViewModel>().FirstOrDefault(sprite => sprite.Start == picture);
         return new PokemonAnimationPreviewViewModel(
            viewPort.Model, species, viewPort.Singletons.WorkDispatcher.CreateDelayTimer(),
            () => Sibling()?.GetCurrentColors(),
            () => Token(Sibling()),
            Sibling);
      }

      private static int FieldAddress(ITableRun table, int elementIndex, string name) {
         return table.Start + table.ElementLength * elementIndex + table.ElementContent.TakeWhile(segment => segment.Name != name).Sum(segment => segment.Length);
      }

      private static long Token(SpriteElementViewModel sprite) {
         if (sprite?.PixelData == null) return 0;
         unchecked {
            long hash = 1469598103934665603L ^ sprite.CurrentPalette;
            foreach (var pixel in sprite.PixelData) hash = (hash ^ (ushort)pixel) * 1099511628211L;
            return hash;
         }
      }

      #endregion

      #region Building

      /// <summary>Asks for the pictures to be built, after the changes stopped coming. Does nothing while nobody can see them.</summary>
      public void RequestUpdate() {
         if (!isDisplayed || !visible) { dirty = true; return; }
         debounce.DelayCall(TimeSpan.FromMilliseconds(DebounceMilliseconds), Update);
      }

      /// <summary>
      /// The preview just came on screen. If what it holds is stale or missing, that must not be seen even for a debounce interval, so it is built right now;
      /// if it is just being checked (the pictures are very likely still right), the check waits for the debounce like every other change.
      /// </summary>
      private void RequestShowing() {
         if (!isDisplayed || !visible) { dirty = true; return; }
         if (dirty || timeline == null) UpdateNow(); else RequestUpdate();
      }

      /// <summary>
      /// Brings the pictures up to date right now, without waiting for the debounce. The table tool calls this once the sprites next to the preview are done
      /// when it has switched to another pokemon: the animation of the new pokemon then appears in the very same step as its sprites.
      /// </summary>
      public void UpdateNow() {
         debounce.Reset();
         Update();
      }

      /// <summary>Rebuilds now (if something relevant changed). The debounced work ends up here.</summary>
      public void Update() {
         if (!isDisplayed || !visible) { dirty = true; return; }
         Watch();
         var signature = ComputeSignature();
         var token = pictureToken?.Invoke() ?? 0;
         if (!dirty && signature == builtSignature && token == builtToken && timeline != null) return;
         dirty = false;
         builtSignature = signature;
         builtToken = token;
         Build();
      }

      private void Watch() {
         var source = pictureSource?.Invoke();
         if (ReferenceEquals(source, watched)) return;
         Unwatch();
         watched = source;
         if (watched != null) watched.PropertyChanged += PictureChanged;
      }

      private void Unwatch() {
         if (watched != null) watched.PropertyChanged -= PictureChanged;
         watched = null;
      }

      private void PictureChanged(object sender, PropertyChangedEventArgs e) {
         if (e.PropertyName == nameof(IPixelViewModel.PixelData)) RequestUpdate();
      }

      /// <summary>Everything that can change the animation and is not a picture: the movement, its delay and the list of pictures.</summary>
      protected virtual string ComputeSignature() {
         try {
            var layout = PokemonAnimationLayout.Find(model);
            if (layout == null || !layout.TryReadInputs(model, speciesIndex, out var inputs)) return "none";
            var text = new System.Text.StringBuilder();
            text.Append(speciesIndex).Append('|').Append(inputs.AnimId).Append('|').Append(inputs.Delay).Append('|').Append(inputs.FramesAddress);
            var commands = PokemonAnimationSource.ReadFrameCommands(model, inputs.FramesAddress);
            if (commands == null) {
               text.Append("|?");
            } else {
               foreach (var command in commands) text.Append('|').Append(command.Kind).Append(',').Append(command.Value).Append(',').Append(command.Duration).Append(command.HFlip ? 'h' : '-').Append(command.VFlip ? 'v' : '-');
            }
            return text.ToString();
         } catch (Exception ex) when (ex is NotImplementedException || ex is InvalidOperationException || ex is ArgumentException || ex is IndexOutOfRangeException) {
            return "error";
         }
      }

      /// <summary>Renders the animation of the species (a few milliseconds). Null if there is nothing to show.</summary>
      protected virtual PokemonAnimationTimeline CreateTimeline(IReadOnlyList<short> colors) {
         PokemonAnimationTimeline.TryBuild(model, speciesIndex, CanvasSize, IdleMilliseconds, true, colors, out var built);
         return built;
      }

      private void Build() {
         IReadOnlyList<short> colors = null;
         try {
            colors = paletteProvider?.Invoke();
            if (colors != null && colors.Count < 16) colors = null;
         } catch (Exception ex) when (ex is NotImplementedException || ex is InvalidOperationException || ex is ArgumentException || ex is IndexOutOfRangeException) {
            colors = null;
         }
         PokemonAnimationTimeline built;
         try {
            built = CreateTimeline(colors);
         } catch (Exception) {
            built = null; // decoration: a table entry the engine cannot read just shows no preview
         }
         BuildCount++;
         SetTimeline(built);
      }

      private void SetTimeline(PokemonAnimationTimeline built) {
         timeline = built;
         elapsed = 0;
         frameIndex = 0;
         if (built == null) {
            starts = Array.Empty<int>();
            totalMilliseconds = 0;
            Status = "No animation to show for this Pokémon.";
         } else {
            starts = new int[built.Frames.Count];
            var time = 0;
            for (int i = 0; i < starts.Length; i++) {
               starts[i] = time;
               time += built.DurationsMs[i];
            }
            totalMilliseconds = time;
            Surface.Show(built.Frames[0]);
            Status = built.IsFaithful
               ? $"As in battle: {(built.TotalMilliseconds - IdleMilliseconds) / 1000.0:0.0} seconds."
               : "Approximation: this animation does something the preview cannot follow exactly.";
         }
         NotifyHasAnimation();
         UpdateWantsTicks();
      }

      /// <summary>Forgets the pictures of the last pokemon (only done while nobody sees them).</summary>
      private void DropTimeline() {
         if (timeline == null) return;
         timeline = null;
         starts = Array.Empty<int>();
         totalMilliseconds = 0;
         elapsed = 0;
         frameIndex = 0;
         Status = string.Empty;
         NotifyHasAnimation();
         UpdateWantsTicks();
      }

      private void NotifyHasAnimation() {
         if (lastHasAnimation == HasAnimation) return;
         lastHasAnimation = HasAnimation;
         NotifyPropertyChanged(nameof(HasAnimation));
      }

      #endregion

      #region Playing

      /// <summary>
      /// Moves the clock forward and shows the picture of the new time (looping). The view calls this from its one timer.
      /// Allocation free: it only changes which picture the surface points at.
      /// </summary>
      public void Advance(int milliseconds) {
         if (!isPlaying || timeline == null || starts.Length < 2 || totalMilliseconds <= 0 || milliseconds <= 0) return;
         elapsed += milliseconds;
         if (elapsed >= totalMilliseconds) {
            elapsed %= totalMilliseconds;
            frameIndex = 0;
         }
         while (frameIndex + 1 < starts.Length && starts[frameIndex + 1] <= elapsed) frameIndex++;
         Surface.Show(timeline.Frames[frameIndex]);
      }

      private void ExecuteReplay() {
         elapsed = 0;
         frameIndex = 0;
         if (timeline != null) Surface.Show(timeline.Frames[0]);
         IsPlaying = true;
         UpdateWantsTicks();
      }

      private void UpdateWantsTicks() {
         var wants = WantsTicks;
         if (wants == lastWantsTicks) return;
         lastWantsTicks = wants;
         NotifyPropertyChanged(WantsTicksChanged);
      }

      #endregion
   }
}
