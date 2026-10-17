using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.PokemonAnimations;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// Walking from one pokemon to the next in the table tool must look like a single change: the Sprites section shows the old pokemon, and the next thing it shows is the new one.
   /// The ROM here is small and synthetic: three pokemon with a (compressed) front sprite sheet, a palette and an animation each.
   /// The dispatcher never fires a timer by itself, the way a real timer leaves a gap between two screen renders: if something waits for a timer, it is waiting visibly.
   /// </summary>
   public class PokemonSpriteSwitchTests {
      private const int ElementLength = 14, Species = 3;
      private const int Frames = 0x40, Commands = 0x80, Sprites = 0x400, SpriteSpacing = 0x800, Palettes = 0x2000;

      /// <summary>Timers only run when the test says that time has passed.</summary>
      private class ManualDispatch : IWorkDispatcher {
         public readonly List<ManualWorkTimer> Timers = new();
         public Task WaitForRenderingAsync() => Task.CompletedTask;
         public void BlockOnUIWork(Action action) => action();
         public Task DispatchWork(Action action) { action?.Invoke(); return Task.CompletedTask; }
         public Task RunBackgroundWork(Action action) => DispatchWork(action);
         public IDelayWorkTimer CreateDelayTimer() { var timer = new ManualWorkTimer(); Timers.Add(timer); return timer; }
         public bool HasScheduledWork => Timers.Any(timer => timer.HasScheduledWork);
         /// <summary>The time the timers were waiting for has passed.</summary>
         public void Pump() { for (int i = 0; i < 10 && HasScheduledWork; i++) foreach (var timer in Timers.Where(t => t.HasScheduledWork).ToList()) timer.RunWork(); }
      }

      /// <summary>Everything a view would hear from the table tool, in order. Collections are followed as they are replaced.</summary>
      private class Recorder {
         public readonly List<string> Events = new();
         public readonly List<(string Key, string Value)> Values = new();
         public int BitmapWrites;
         private readonly HashSet<object> attached = new();
         private readonly Dictionary<object, string> names = new();
         private readonly TableTool tool;

         public Recorder(TableTool tool) { this.tool = tool; Attach(); }

         private string Name(object o) {
            if (!names.TryGetValue(o, out var name)) {
               var label = o is SplitterArrayElementViewModel splitter ? splitter.SectionName : o.GetType().GetProperty("Name")?.GetValue(o) as string;
               names[o] = name = o.GetType().Name + "#" + names.Count + (label == null ? string.Empty : "(" + label + ")");
            }
            return name;
         }

         private void Attach() {
            Hook(tool.Groups);
            foreach (var group in tool.Groups) {
               Hook(group.Members);
               foreach (var member in group.Members) {
                  Hook(member);
                  if (member is SpriteElementViewModel sprite) Hook(sprite.PaletteSelection);
                  if (member is PokemonAnimationPreviewViewModel preview) Hook(preview.Surface);
                  if (member is PaletteElementViewModel palette) { Hook(palette.Colors); Hook(palette.Colors.Elements); foreach (var color in palette.Colors.Elements) Hook(color); }
               }
            }
         }

         private void Hook(object o) {
            if (o == null || !attached.Add(o)) return;
            if (o is INotifyPropertyChanged properties) {
               properties.PropertyChanged += (sender, e) => {
                  var property = sender.GetType().GetProperty(e.PropertyName);
                  var value = property != null && property.GetIndexParameters().Length == 0 ? property.GetValue(sender) : null;
                  var text = value is Array array ? "array" + array.Length : value?.ToString() ?? "null";
                  Events.Add($"property {Name(sender)}.{e.PropertyName}");
                  Values.Add((Name(sender) + "." + e.PropertyName, text));
                  if (sender is IPixelViewModel && e.PropertyName is nameof(IPixelViewModel.PixelWidth) or nameof(IPixelViewModel.PixelHeight) or nameof(IPixelViewModel.PixelData)) BitmapWrites++;
               };
            }
            if (o is INotifyCollectionChanged collection) {
               collection.CollectionChanged += (sender, e) => {
                  Events.Add($"collection {e.Action}");
                  Attach();
               };
            }
         }

         public IEnumerable<string> CollectionEvents => Events.Where(e => e.StartsWith("collection"));

         /// <summary>Properties that were announced twice: the view does the work twice, or draws something it then draws over.</summary>
         public IEnumerable<string> Repeated => Values.GroupBy(v => v.Key).Where(g => g.Count() > 1).Select(g => g.Key + ": " + string.Join(" -> ", g.Select(v => v.Value)));
      }

      private readonly ManualDispatch dispatch = new();
      private readonly byte[] data = new byte[0x3000];
      private readonly Singletons singletons;
      private readonly ViewPort viewPort;
      private readonly PokemonModel model;

      public PokemonSpriteSwitchTests() : this(-1) { }

      /// <param name="speciesWithoutPalette">The pokemon whose palette pointer is empty (-1 for none): its table tool has no palette, so it is not made of the same kinds of views.</param>
      private PokemonSpriteSwitchTests(int speciesWithoutPalette) {
         singletons = new Singletons(dispatch);
         WriteRom(data);
         if (speciesWithoutPalette >= 0) WriteWord(data, speciesWithoutPalette * ElementLength + 10, 0);
         model = new PokemonModel(data, null, singletons);
         viewPort = new ViewPort("file.gba", model, InstantDispatch.Instance, singletons) { AllowMultipleElementsPerLine = true, Width = 0x10, Height = 0x10 };
         viewPort.Edit($"@00 ^{HardcodeTablesModel.PokemonStatsTable}[frontAnimDelay. frontAnimId. frontAnimFrames<> frontPic<`lzs4x8x16`> palette<`ucp4`>]{Species} ");
      }

      #region the ROM

      private static void WritePointer(byte[] rom, int at, int target) {
         rom[at] = (byte)target; rom[at + 1] = (byte)(target >> 8); rom[at + 2] = (byte)(target >> 16); rom[at + 3] = 0x08;
      }

      private static void WriteWord(byte[] rom, int at, uint word) {
         for (int i = 0; i < 4; i++) rom[at + i] = (byte)(word >> (8 * i));
      }

      private static uint Frame(int image, int duration) => (uint)image | (uint)(duration << 16);

      private static void WriteRom(byte[] rom) {
         for (int species = 0; species < Species; species++) {
            var element = species * ElementLength;
            rom[element + 0] = 0;                        // frontAnimDelay
            rom[element + 1] = 200;                      // frontAnimId: a movement the engine does not know, so the pictures only change with the frame list
            WritePointer(rom, element + 2, Frames + species * 8);
            WritePointer(rom, element + 6, Sprites + species * SpriteSpacing);
            WritePointer(rom, element + 10, Palettes + species * 0x20);

            // a pair of pointers to command lists; every pokemon shows its second picture for a different time, so every animation has another length
            WritePointer(rom, Frames + species * 8 + 0, Commands + species * 0x20);
            WritePointer(rom, Frames + species * 8 + 4, Commands + species * 0x20);
            var duration = 6 + 4 * species;
            WriteWord(rom, Commands + species * 0x20 + 0, Frame(1, duration));
            WriteWord(rom, Commands + species * 0x20 + 4, Frame(0, duration));
            WriteWord(rom, Commands + species * 0x20 + 8, 0xFFFF);

            // 64x128 pixels (two pictures) in tiles that are different for every pokemon
            var sheet = new byte[8 * 16 * 32];
            for (int tile = 0; tile < 8 * 16; tile++) {
               for (int i = 0; i < 32; i++) sheet[tile * 32 + i] = (byte)(((tile + 5 * species + 1) % 15 + 1) * 0x11);
            }
            var compressed = LZRun.Compress(sheet);
            Assert.True(compressed.Count < SpriteSpacing);
            for (int i = 0; i < compressed.Count; i++) rom[Sprites + species * SpriteSpacing + i] = compressed[i];

            for (int color = 0; color < 16; color++) {
               var value = (color * (species + 2) + 1) & 0x1F;
               var gba = value | ((31 - value) << 5) | ((color * 2) << 10);
               rom[Palettes + species * 0x20 + color * 2] = (byte)gba;
               rom[Palettes + species * 0x20 + color * 2 + 1] = (byte)(gba >> 8);
            }
         }
      }

      #endregion

      private TableTool Tool => viewPort.Tools.TableTool;

      private void Select(int species) => viewPort.Goto.Execute(species * ElementLength);

      private static long Hash(short[] pixels) {
         if (pixels == null) return 0;
         unchecked { long hash = 1469598103934665603L; foreach (var pixel in pixels) hash = (hash ^ (ushort)pixel) * 1099511628211L; return hash; }
      }

      /// <summary>What the screen would show for the Sprites section: one line per thing, so that two renders with the same lines look the same.</summary>
      private static List<string> Render(TableTool tool) {
         var lines = new List<string>();
         foreach (var group in tool.Groups) {
            lines.Add($"group '{group.GroupName}' members={group.Members.Count}");
            foreach (var member in group.Members) {
               var line = new StringBuilder("   " + member.GetType().Name + " visible=" + member.Visible);
               switch (member) {
                  case SpriteElementViewModel sprite:
                     line.Append($" {sprite.PixelWidth}x{sprite.PixelHeight} scale={sprite.SpriteScale} pages={sprite.Pages} palette={sprite.CurrentPalette}/{sprite.PaletteSelection.Count} pixels={Hash(sprite.PixelData):X}");
                     break;
                  case PaletteElementViewModel palette:
                     line.Append($" colors={string.Join(",", palette.Colors.Elements.Select(c => c.Color.ToString("X4")))}");
                     break;
                  case PokemonAnimationPreviewViewModel preview:
                     line.Append($" animation={preview.HasAnimation} status='{preview.Status}' {preview.Surface.PixelWidth}x{preview.Surface.PixelHeight} pixels={Hash(preview.Surface.PixelData):X}");
                     break;
               }
               lines.Add(line.ToString());
            }
         }
         return lines;
      }

      private PokemonAnimationPreviewViewModel Preview => Tool.Children.OfType<PokemonAnimationPreviewViewModel>().Single();
      private List<SpriteElementViewModel> SpriteViews => Tool.Children.OfType<SpriteElementViewModel>().ToList();

      /// <summary>The preview is on screen once the view is loaded; the table tool shows its content for the pokemon under the cursor.</summary>
      private void ShowOnScreen(int species) {
         Select(species);
         dispatch.Pump();
         Preview.IsDisplayed = true;
         dispatch.Pump();
      }

      [Fact]
      public void TheTableTool_ShowsTheSpritesAndTheAnimationOfThePokemon() {
         ShowOnScreen(0);

         Assert.Single(SpriteViews);
         Assert.All(SpriteViews, sprite => Assert.Equal(64, sprite.PixelWidth));
         Assert.True(Preview.HasAnimation);
         Assert.Single(Tool.Children.OfType<PaletteElementViewModel>());
      }

      [Fact]
      public void SwitchingPokemon_IsOneStep_NothingWaitsForATimer() {
         ShowOnScreen(0);
         var before = Render(Tool);

         Select(1);
         var immediately = Render(Tool);
         var waitingForATimer = dispatch.HasScheduledWork;
         dispatch.Pump();
         var later = Render(Tool);

         Assert.NotEqual(before, immediately);   // it did change
         Assert.Equal(later, immediately);       // and what the first render shows is already the final state
         Assert.False(waitingForATimer);         // so nothing is going to change it a moment later
      }

      [Fact]
      public void SwitchingPokemon_LooksLikeOpeningThatPokemonFresh() {
         ShowOnScreen(0);
         Select(2);
         dispatch.Pump();
         var switched = Render(Tool);

         var other = new PokemonSpriteSwitchTests();
         other.ShowOnScreen(2);

         Assert.Equal(Render(other.Tool), switched);
      }

      [Fact]
      public void SwitchingPokemon_NeverShowsTheAnimationAsMissing() {
         ShowOnScreen(0);
         var recorder = new Recorder(Tool);
         var animationStates = new List<bool>();
         Preview.PropertyChanged += (sender, e) => animationStates.Add(Preview.HasAnimation);
         var picture = Hash(Preview.Surface.PixelData);

         Select(1);

         Assert.DoesNotContain(recorder.Events, e => e.EndsWith("." + nameof(PokemonAnimationPreviewViewModel.HasAnimation)));
         Assert.DoesNotContain(false, animationStates);
         Assert.True(Preview.HasAnimation);
         Assert.NotEqual(picture, Hash(Preview.Surface.PixelData));   // and the new pokemon's first picture is already there
      }

      [Fact]
      public void SwitchingPokemon_ReusesTheViewsOfTheSprites_InsteadOfReplacingThem() {
         ShowOnScreen(0);
         var group = Tool.Groups[0];
         var before = group.Members.ToList();
         var recorder = new Recorder(Tool);

         Select(1);

         Assert.Equal(before, group.Members.ToList());   // the same view models in the same order: no container is thrown away and built again
         Assert.Empty(recorder.CollectionEvents.Where(e => e.Contains("Reset")));
         Assert.Empty(recorder.CollectionEvents.Where(e => e.Contains("Remove") || e.Contains("Add")));
      }

      [Fact]
      public void SwitchingPokemon_ChangesThePictureOnceAndTheColorsOnce() {
         ShowOnScreen(0);
         var sprite = SpriteViews.Single();
         var recorder = new Recorder(Tool);
         var pixels = Hash(sprite.PixelData);
         var palette = Tool.Children.OfType<PaletteElementViewModel>().Single();
         var colors = palette.Colors.Elements.Select(c => c.Color).ToList();

         Select(1);

         Assert.NotEqual(pixels, Hash(sprite.PixelData));
         Assert.NotEqual(colors, palette.Colors.Elements.Select(c => c.Color).ToList());
         // a bitmap is rewritten for every announcement of its width, its height or its pixels: the sprite and the animation ask for one rewrite each
         Assert.Equal(2, recorder.BitmapWrites);
         Assert.Empty(recorder.Repeated.Where(line => !line.Contains("Color") && !line.Contains("Content")));
      }

      [Fact]
      public void SwitchingPokemon_ThePaletteIsUpdatedInPlace() {
         ShowOnScreen(0);
         var palette = Tool.Children.OfType<PaletteElementViewModel>().Single();
         var elements = palette.Colors.Elements.ToList();
         var recorder = new Recorder(Tool);

         Select(1);

         Assert.Equal(elements, palette.Colors.Elements.ToList());   // the same sixteen swatches, with other colors
         Assert.Empty(recorder.CollectionEvents);
      }

      [Fact]
      public void SwitchingPokemon_NeverGoesThroughAnEmptyOrHalfBuiltPicture() {
         ShowOnScreen(0);
         var sprite = SpriteViews.Single();
         var seen = new List<string>();
         sprite.PropertyChanged += (sender, e) => seen.Add($"{e.PropertyName}: {sprite.PixelWidth}x{sprite.PixelHeight} pixels={sprite.PixelData?.Length ?? -1}");
         Preview.Surface.PropertyChanged += (sender, e) => seen.Add($"surface {e.PropertyName}: {Preview.Surface.PixelWidth}x{Preview.Surface.PixelHeight} pixels={Preview.Surface.PixelData?.Length ?? -1}");

         Select(1);

         Assert.NotEmpty(seen);
         Assert.All(seen, line => Assert.DoesNotContain("0x", line));   // no zero width
         Assert.All(seen, line => Assert.DoesNotContain("x0 ", line));  // no zero height
         Assert.All(seen, line => Assert.DoesNotContain("pixels=0", line));
         Assert.All(seen, line => Assert.DoesNotContain("pixels=-1", line));
      }

      [Fact]
      public void SwitchingBackAndForth_EndsExactlyWhereItStarted() {
         ShowOnScreen(0);
         var start = Render(Tool);

         Select(1);
         Select(2);
         Select(1);
         Select(0);
         dispatch.Pump();

         Assert.Equal(start, Render(Tool));
      }

      [Fact]
      public void ThePreviewKeepsPlaying_ThroughASwitch() {
         ShowOnScreen(0);
         Select(1);
         Assert.True(Preview.WantsTicks);

         Preview.Advance(40);

         Assert.Equal(Preview.Timeline.Frames[Preview.CurrentFrameIndex].PixelData, Preview.Surface.PixelData);
      }

      [Fact]
      public void SwitchingToAPokemonWithoutAPalette_ReplacesOnlyTheViewThatIsNotTheSameKind() {
         var other = new PokemonSpriteSwitchTests(2);
         other.ShowOnScreen(0);
         var fields = other.Tool.Groups[0].Members.ToList();
         var group = other.Tool.Groups[1];   // the sprite and the palette
         var sprite = group.Members.OfType<SpriteElementViewModel>().Single();
         var memberActions = new List<NotifyCollectionChangedAction>();
         var groupActions = new List<NotifyCollectionChangedAction>();
         group.Members.CollectionChanged += (sender, e) => memberActions.Add(e.Action);
         other.Tool.Groups.CollectionChanged += (sender, e) => groupActions.Add(e.Action);

         other.Select(2);
         other.dispatch.Pump();

         // the palette view cannot show "no palette": one view is replaced. The sprite and the fields keep their views, and no group is replaced.
         Assert.Equal(new[] { NotifyCollectionChangedAction.Replace }, memberActions);
         Assert.Empty(groupActions);
         Assert.Same(group, other.Tool.Groups[1]);
         Assert.Same(sprite, group.Members[0]);
         Assert.IsType<TextStreamElementViewModel>(group.Members[1]);
         Assert.Equal(fields, other.Tool.Groups[0].Members.ToList());

         memberActions.Clear();
         other.Select(0);
         other.dispatch.Pump();

         Assert.Equal(new[] { NotifyCollectionChangedAction.Replace }, memberActions);
         Assert.Same(sprite, group.Members[0]);
         Assert.IsType<PaletteElementViewModel>(group.Members[1]);
         Assert.Equal(fields, other.Tool.Groups[0].Members.ToList());
      }

      [Fact]
      public void SwitchingToAPokemonWithoutAPalette_AndBack_LooksLikeOpeningThemFresh() {
         var withoutPalette = new PokemonSpriteSwitchTests(2);
         withoutPalette.ShowOnScreen(0);
         var first = Render(withoutPalette.Tool);

         withoutPalette.Select(2);
         withoutPalette.dispatch.Pump();
         var switched = Render(withoutPalette.Tool);
         withoutPalette.Select(0);
         withoutPalette.dispatch.Pump();
         var back = Render(withoutPalette.Tool);

         var fresh = new PokemonSpriteSwitchTests(2);
         fresh.ShowOnScreen(2);
         Assert.Equal(Render(fresh.Tool), switched);
         Assert.NotEqual(first, switched);
         Assert.Equal(first, back);
      }

      [Fact]
      public void WhileNobodyLooks_SwitchingBuildsNothing_AndShowingBuildsTheCurrentPokemon() {
         Select(0);
         dispatch.Pump();
         var builds = Preview.BuildCount;

         Select(1);
         Select(2);
         dispatch.Pump();

         Assert.Equal(builds, Preview.BuildCount);
         Assert.False(Preview.HasAnimation);   // the old pictures are not kept for the next time it is shown

         Preview.IsDisplayed = true;

         Assert.Equal(builds + 1, Preview.BuildCount);
         Assert.True(Preview.HasAnimation);
         Assert.False(dispatch.HasScheduledWork);
      }
   }
}
