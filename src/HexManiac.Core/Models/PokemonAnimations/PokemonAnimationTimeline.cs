using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.Models.Runs.Sprites;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.HexManiac.Core.Models.PokemonAnimations {
   /// <summary>Everything the animation of one species needs, read from the ROM.</summary>
   public class PokemonAnimationSource {
      /// <summary>The pictures of the front sprite, 64x64 palette indexes each (4096 values, row by row).</summary>
      public byte[][] Images { get; set; }

      /// <summary>The sprite palette in the GBA's colour order (bit 0-4 red).</summary>
      public ushort[] Palette { get; set; }

      public int AnimId { get; set; }
      public int Delay { get; set; }

      /// <summary>The frontAnimFrames command list, or null if the pointer does not lead to a readable list.</summary>
      public FrameCommand[] Frames { get; set; }

      /// <summary>
      /// Reads the front animation of one species. Returns null if the ROM has no table that describes it or the sprite is not readable.
      /// Where the pieces are depends on the ROM, see <see cref="PokemonAnimationLayout"/>.
      /// </summary>
      /// <param name="speciesIndex">The row of the pokemon table (the species number).</param>
      public static PokemonAnimationSource Read(IDataModel model, int speciesIndex, IReadOnlyList<short> hmaPaletteOverride = null) {
         return PokemonAnimationLayout.Find(model)?.Read(model, speciesIndex, hmaPaletteOverride);
      }

      /// <summary>HexManiac draws colours with red and blue swapped compared to the GBA.</summary>
      public static ushort[] ToGbaPalette(IReadOnlyList<short> hmaColors) {
         var result = new ushort[16];
         for (int i = 0; i < 16; i++) result[i] = unchecked((ushort)PaletteRun.FlipColorChannels(hmaColors[i]));
         return result;
      }

      /// <summary>Cuts a front sprite sheet (64 wide, a multiple of 64 tall) into its 64x64 pictures.</summary>
      public static byte[][] SplitFrames(int[,] pixels) {
         int width = pixels.GetLength(0), height = pixels.GetLength(1);
         if (width != 64 || height < 64 || height % 64 != 0) return null;
         var images = new byte[Math.Min(height / 64, 2)][];
         for (int f = 0; f < images.Length; f++) {
            var image = new byte[64 * 64];
            for (int y = 0; y < 64; y++) {
               for (int x = 0; x < 64; x++) image[y * 64 + x] = (byte)(pixels[x, f * 64 + y] & 0xF);
            }
            images[f] = image;
         }
         return images;
      }

      /// <summary>
      /// The frontAnimFrames field points at two pointers: the list for the sprite's first picture and the list that animates (both lists of AnimCmd).
      /// Reads the second list up to its end command. Returns null if the pointers are not valid.
      /// </summary>
      public static FrameCommand[] ReadFrameCommands(IDataModel model, int framesAddress) {
         if (framesAddress < 0 || framesAddress + 8 > model.Count) return null;
         int list = model.ReadPointer(framesAddress + 4);
         if (list < 0 || list + 4 > model.Count) return null;
         var commands = new List<FrameCommand>();
         for (int i = 0; i < 64 && list + i * 4 + 4 <= model.Count; i++) {
            var command = FrameCommand.Parse((uint)model.ReadMultiByteValue(list + i * 4, 4));
            commands.Add(command);
            if (command.Kind == FrameCommand.KindEnd) return commands.ToArray();
         }
         return null; // never ends: not a command list
      }
   }

   /// <summary>
   /// How the front animation of a species looks in the game, rendered once as a list of pictures with the time each one is shown.
   /// The last picture is the resting pose and is held for the idle time, so a player can simply loop the list.
   /// </summary>
   public class PokemonAnimationTimeline {
      public const int DefaultCanvasSize = 96;

      public IReadOnlyList<IPixelViewModel> Frames { get; }

      /// <summary>How long each of <see cref="Frames"/> is shown, in milliseconds. The last entry includes the idle time.</summary>
      public IReadOnlyList<int> DurationsMs { get; }

      public int CanvasSize { get; }

      /// <summary>The number of game frames (1/60 s) the animation takes, not counting the idle time.</summary>
      public int GameFrames { get; }

      /// <summary>The movement id that was simulated (frontAnimId).</summary>
      public int AnimId { get; }

      /// <summary>False if the game would read outside of its sine table, or the movement id is unknown: the preview then guesses.</summary>
      public bool IsFaithful { get; }

      /// <summary>An estimate of the memory the pictures need.</summary>
      public long ApproximateBytes { get; }

      public int TotalMilliseconds { get; }

      private PokemonAnimationTimeline(List<IPixelViewModel> frames, List<int> durations, int canvasSize, int gameFrames, int animId, bool faithful, long bytes) {
         Frames = frames;
         DurationsMs = durations;
         CanvasSize = canvasSize;
         GameFrames = gameFrames;
         AnimId = animId;
         IsFaithful = faithful;
         ApproximateBytes = bytes;
         TotalMilliseconds = durations.Sum();
      }

      /// <summary>
      /// Renders the front animation of a species with its normal palette, with the sprite's transparent colour left transparent
      /// (so the pictures can be drawn over anything), followed by a still picture held for idleMilliseconds.
      /// </summary>
      public static bool TryBuild(IDataModel model, int speciesIndex, int canvasSize, int idleMilliseconds, out PokemonAnimationTimeline timeline) {
         return TryBuild(model, speciesIndex, canvasSize, idleMilliseconds, false, null, out timeline);
      }

      /// <param name="opaque">True: transparent pixels show the palette's first colour (like the sprite editor does). False: they use <see cref="IPixelViewModel.Transparent"/>.</param>
      /// <param name="hmaPaletteOverride">Draw with these colours instead of the species' normal palette (the shiny palette, for example). In HexManiac's colour order.</param>
      public static bool TryBuild(IDataModel model, int speciesIndex, int canvasSize, int idleMilliseconds, bool opaque, IReadOnlyList<short> hmaPaletteOverride, out PokemonAnimationTimeline timeline) {
         timeline = null;
         if (model == null) return false;
         PokemonAnimationSource source;
         try {
            source = PokemonAnimationSource.Read(model, speciesIndex, hmaPaletteOverride);
         } catch (Exception ex) when (ex is NotImplementedException || ex is InvalidOperationException || ex is ArgumentException || ex is IndexOutOfRangeException) {
            return false; // a table that is not a pokemon table
         }
         if (source == null) return false;
         timeline = Build(source, canvasSize, idleMilliseconds, opaque);
         return timeline != null;
      }

      public static PokemonAnimationTimeline Build(PokemonAnimationSource source, int canvasSize, int idleMilliseconds, bool opaque) {
         var request = new PokemonAnimationRequest {
            AnimId = source.AnimId,
            Delay = source.Delay,
            FrameCommands = source.Frames,
            Palette = source.Palette,
         };
         var simulation = PokemonAnimationEngine.Simulate(request);
         return Build(simulation, source.Images, source.AnimId, canvasSize, idleMilliseconds, opaque);
      }

      public static PokemonAnimationTimeline Build(PokemonAnimationSimulation simulation, byte[][] images, int animId, int canvasSize, int idleMilliseconds, bool opaque) {
         canvasSize = Math.Max(64, Math.Min(256, canvasSize));
         var palettes = simulation.Palettes.Select(ToHmaPalette).ToArray();
         var backdrop = opaque ? palettes[0][0] : ChooseTransparentColor(palettes);
         var cache = new Dictionary<AnimFrameState, IPixelViewModel>();
         var frames = new List<IPixelViewModel>();
         var durations = new List<int>();
         AnimFrameState lastState = default;
         IPixelViewModel lastFrame = null;
         int runLength = 0, startFrame = 0;
         long bytes = 0;

         void Flush(int endFrame) {
            // the time of a run is the difference of the rounded cumulative times: no drift for animations of hundreds of frames
            durations.Add(ToMilliseconds(endFrame) - ToMilliseconds(startFrame));
         }

         for (int i = 0; i < simulation.Frames.Length; i++) {
            var state = simulation.Frames[i];
            if (i > 0 && state.Equals(lastState)) { runLength++; continue; }
            if (i > 0) Flush(i);
            startFrame = i;
            runLength = 1;
            lastState = state;
            if (!cache.TryGetValue(state, out lastFrame)) {
               var pixels = PokemonAnimationRenderer.Render(state, images, palettes[state.Palette], backdrop, opaque, canvasSize);
               lastFrame = new ReadonlyPixelViewModel(canvasSize, canvasSize, pixels, opaque ? (short)-1 : backdrop);
               cache[state] = lastFrame;
               bytes += pixels.Length * 2L;
            }
            frames.Add(lastFrame);
         }
         if (simulation.Frames.Length > 0) Flush(simulation.Frames.Length);

         if (frames.Count == 0) {
            // nothing to show: a single still picture
            var still = PokemonAnimationRenderer.Render(new AnimFrameState(0, 0, 0, -32, -32, 1, 256, 0, 0, 256, false, false, false, 0), images, palettes[0], backdrop, opaque, canvasSize);
            frames.Add(new ReadonlyPixelViewModel(canvasSize, canvasSize, still, opaque ? (short)-1 : backdrop));
            durations.Add(0);
         }
         durations[durations.Count - 1] += Math.Max(0, idleMilliseconds);

         return new PokemonAnimationTimeline(frames, durations, canvasSize, simulation.Frames.Length, animId,
            simulation.OutOfRangeSineReads == 0 && !simulation.UnknownAnimId && !simulation.Faulted && !simulation.HitFrameLimit, bytes);
      }

      private static int ToMilliseconds(int gameFrames) => (int)Math.Round(gameFrames * 1000.0 / PokemonAnimationEngine.GameFramesPerSecond, MidpointRounding.AwayFromZero);

      private static short[] ToHmaPalette(ushort[] gba) {
         var result = new short[16];
         for (int i = 0; i < 16; i++) result[i] = PaletteRun.FlipColorChannels(unchecked((short)gba[i]));
         return result;
      }

      /// <summary>A colour no palette of the animation contains, so it can mark transparent pixels.</summary>
      private static short ChooseTransparentColor(short[][] palettes) {
         var used = new HashSet<short>();
         foreach (var palette in palettes) foreach (var color in palette) used.Add(color);
         for (int candidate = 0x7C1F; candidate > 0; candidate--) {
            if (!used.Contains((short)candidate)) return (short)candidate;
         }
         return 0x7C1F;
      }
   }

   /// <summary>Draws one game frame of a sprite the way the GBA's object layer does.</summary>
   public static class PokemonAnimationRenderer {
      /// <summary>
      /// Draws the sprite into a square canvas, with the sprite's resting position in the middle.
      /// Pixels that are not covered by the sprite (or are the palette's transparent entry) get the backdrop colour.
      /// </summary>
      /// <param name="images">The pictures of the sprite: 64x64 palette indexes each.</param>
      /// <param name="palette">16 colours in HexManiac's colour order, as they are on this game frame.</param>
      /// <param name="backdrop">The colour of pixels without sprite.</param>
      /// <param name="opaque">True if the backdrop is the unblended colour 0 of the palette, false if it stands for transparency.</param>
      public static short[] Render(AnimFrameState state, byte[][] images, short[] palette, short backdrop, bool opaque, int canvasSize) {
         var canvas = new short[canvasSize * canvasSize];
         if (backdrop != 0) {
            for (int i = 0; i < canvas.Length; i++) canvas[i] = backdrop;
         }
         if (state.Invisible || images == null || images.Length == 0) return canvas;

         var image = images[Math.Max(0, Math.Min(images.Length - 1, state.Image))];
         bool doubleSize = (state.AffineMode & 2) != 0;
         int box = doubleSize ? 128 : 64;
         int center = canvasSize / 2;
         // the sprite's position is its center: the box's corner is that plus the (negative) corner vector
         int left = center + state.X2 + (doubleSize ? -64 : -32);
         int top = center + state.Y2 + (doubleSize ? -64 : -32);

         int x0 = Math.Max(0, -left), x1 = Math.Min(box, canvasSize - left);
         int y0 = Math.Max(0, -top), y1 = Math.Min(box, canvasSize - top);
         if (x0 >= x1 || y0 >= y1) return canvas;

         if (state.AffineMode == 0 || (!doubleSize && state.A == 256 && state.B == 0 && state.C == 0 && state.D == 256)) {
            // no matrix, or the identity: copy the picture
            for (int y = y0; y < y1; y++) {
               int sy = state.VFlip && state.AffineMode == 0 ? 63 - y : y;
               if (sy < 0 || sy >= 64) continue;
               for (int x = x0; x < x1; x++) {
                  int sx = state.HFlip && state.AffineMode == 0 ? 63 - x : x;
                  if (sx < 0 || sx >= 64) continue;
                  var index = image[sy * 64 + sx];
                  if (index != 0) canvas[(top + y) * canvasSize + left + x] = palette[index];
               }
            }
            return canvas;
         }

         // the GBA maps every screen pixel of the box back into the picture: texel = matrix * (pixel - box center) + picture center, in 8.8 fixed point
         int a = state.A, b = state.B, c = state.C, d = state.D;
         int half = box / 2;
         for (int y = y0; y < y1; y++) {
            int dy = y - half;
            int row = (top + y) * canvasSize + left;
            for (int x = x0; x < x1; x++) {
               int dx = x - half;
               int tx = (a * dx + b * dy + (32 << 8)) >> 8;
               int ty = (c * dx + d * dy + (32 << 8)) >> 8;
               if ((uint)tx >= 64 || (uint)ty >= 64) continue;
               var index = image[ty * 64 + tx];
               if (index != 0) canvas[row + x] = palette[index];
            }
         }
         return canvas;
      }
   }
}
