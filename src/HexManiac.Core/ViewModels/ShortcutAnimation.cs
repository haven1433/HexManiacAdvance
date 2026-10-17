using HavenSoft.HexManiac.Core.ViewModels.Images;
using System;
using System.Collections.Generic;

namespace HavenSoft.HexManiac.Core.ViewModels {
   /// <summary>
   /// The pictures of an animated shortcut button of the Goto dialog and how long each one stays, repeated forever.
   /// A picture can be shown for a long time to make a pause: the Pokemon button shows the battle animation and then holds its last picture for 3 seconds.
   /// Nothing here allocates after the animation is made, so the clock that plays it can run at any speed without making garbage.
   /// </summary>
   public sealed class ShortcutAnimation {
      private readonly IPixelViewModel[] frames;
      private readonly int[] durations;

      public IReadOnlyList<IPixelViewModel> Frames => frames;
      public IReadOnlyList<int> DurationsMilliseconds => durations;
      public int FrameCount => frames.Length;

      /// <summary>How long one round (all the pictures) takes.</summary>
      public long CycleMilliseconds { get; }

      private ShortcutAnimation(IPixelViewModel[] frames, int[] durations, long cycle) {
         this.frames = frames;
         this.durations = durations;
         CycleMilliseconds = cycle;
      }

      /// <summary>
      /// Pictures with the time each is shown. Returns null if the lists can't make an animation:
      /// there are no pictures, a picture is missing, there is not exactly one time for every picture, or a time is not above 0.
      /// (The lists are copied: changing them afterwards changes nothing.)
      /// </summary>
      public static ShortcutAnimation Create(IReadOnlyList<IPixelViewModel> frames, IReadOnlyList<int> durationsMilliseconds) {
         if (frames == null || durationsMilliseconds == null || frames.Count == 0 || frames.Count != durationsMilliseconds.Count) return null;
         var pictures = new IPixelViewModel[frames.Count];
         var times = new int[frames.Count];
         long cycle = 0;
         for (int i = 0; i < pictures.Length; i++) {
            if (frames[i] == null || durationsMilliseconds[i] <= 0) return null;
            pictures[i] = frames[i];
            times[i] = durationsMilliseconds[i];
            cycle += times[i];
         }
         return new ShortcutAnimation(pictures, times, cycle);
      }

      /// <summary>
      /// Which picture is shown 'elapsedMilliseconds' after the animation started, and how many milliseconds are left until the next picture replaces it.
      /// Time past the end of the round starts the next round, so a clock that was late (the computer was busy or asleep) shows the right picture instead of replaying what it missed.
      /// </summary>
      public int FrameIndexAt(long elapsedMilliseconds, out long millisecondsUntilNextFrame) {
         var position = elapsedMilliseconds <= 0 ? 0 : elapsedMilliseconds % CycleMilliseconds;
         long end = 0;
         for (int i = 0; i < durations.Length; i++) {
            end += durations[i];
            if (position < end) {
               millisecondsUntilNextFrame = end - position;
               return i;
            }
         }
         millisecondsUntilNextFrame = durations[0]; // unreachable: the position is always inside the round
         return 0;
      }
   }
}
