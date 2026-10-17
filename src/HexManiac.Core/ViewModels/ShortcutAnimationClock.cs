using HavenSoft.HexManiac.Core.Models;
using System;
using System.ComponentModel;

namespace HavenSoft.HexManiac.Core.ViewModels {
   /// <summary>
   /// The one clock that plays the animated shortcut buttons of the Goto dialog (the Pokemon button, the Anim Tiles flower).
   /// The editor owns it and points it at the Goto dialog of the selected tab (<see cref="Attach"/>).
   ///
   /// It runs only while somebody can see the buttons move: the dialog is shown, the window is the active window, and the dialog has an animated button.
   /// When any of that stops being true it stops completely: no timer is waiting, nothing is redrawn, nothing is scheduled.
   /// It does not tick at a fixed rate either: it sleeps until the next frame of any button is due, so a button that holds a picture for 3 seconds costs one wake-up in 3 seconds.
   /// A wake-up changes the picture of the buttons whose frame changed and allocates nothing in this class.
   ///
   /// The clock keeps only the Goto dialog of the selected tab. Attaching another one (every tab change makes a new dialog) lets go of the old one,
   /// so the clock never keeps the dialogs (and the pictures) of closed tabs alive.
   /// </summary>
   public class ShortcutAnimationClock {
      /// <summary>Whatever a button asks for, the clock doesn't wake up more often than this (a timer can't do better than about 15ms anyway).</summary>
      public const int MinimumDelayMilliseconds = 10;

      private readonly IDelayWorkTimer timerA, timerB;
      private readonly Action wakeA, wakeB;
      private readonly Func<long> millisecondsNow;

      private GotoControlViewModel target;
      private IDelayWorkTimer executing; // the timer whose callback is running right now (it is never asked to wait again from inside its own callback)
      private bool windowActive = true, running, arming;
      private long startedAt;

      /// <param name="dispatcher">Gives the timers. The editor passes its own dispatcher.</param>
      /// <param name="millisecondsNow">The time source (milliseconds that only grow). The default is the system's. Tests pass a fake one.</param>
      public ShortcutAnimationClock(IWorkDispatcher dispatcher, Func<long> millisecondsNow = null) {
         dispatcher ??= InstantDispatch.Instance;
         this.millisecondsNow = millisecondsNow ?? (() => Environment.TickCount64);
         // two timers, taking turns: a timer must not be asked to wait again from inside its own callback (the WPF one forgets that new wait when the callback returns)
         timerA = dispatcher.CreateDelayTimer();
         timerB = dispatcher.CreateDelayTimer();
         wakeA = () => Wake(timerA);
         wakeB = () => Wake(timerB);
      }

      /// <summary>True while the buttons are being played (a wake-up is waiting, or, with a dispatcher that runs timers at once, would be).</summary>
      public bool IsRunning => running;

      /// <summary>The Goto dialog being played, or null.</summary>
      public GotoControlViewModel Target => target;

      /// <summary>False while the window is not the active window (another program is in front, or the window is minimized): the buttons stand still then. Starts true.</summary>
      public bool WindowActive {
         get => windowActive;
         set {
            if (windowActive == value) return;
            windowActive = value;
            Evaluate();
         }
      }

      /// <summary>Play the buttons of this Goto dialog (null: none). The previous dialog is let go of and stops moving.</summary>
      public void Attach(GotoControlViewModel dialog) {
         if (!ReferenceEquals(target, dialog)) {
            if (target != null) {
               target.PropertyChanged -= TargetPropertyChanged;
               target.ShortcutAnimationsChanged -= TargetAnimationsChanged;
            }
            Stop();
            target = dialog;
            if (target != null) {
               target.PropertyChanged += TargetPropertyChanged;
               target.ShortcutAnimationsChanged += TargetAnimationsChanged;
            }
         }
         Evaluate();
      }

      private void TargetPropertyChanged(object sender, PropertyChangedEventArgs e) {
         if (e.PropertyName == nameof(GotoControlViewModel.ControlVisible) || e.PropertyName == nameof(GotoControlViewModel.Shortcuts)) Evaluate();
      }

      private void TargetAnimationsChanged(object sender, EventArgs e) => Evaluate();

      /// <summary>Start or stop, depending on whether anybody can see an animated button right now.</summary>
      private void Evaluate() {
         try {
            if (target == null || !windowActive || !target.ControlVisible) {
               Stop();
               return;
            }
            // only now, with the dialog in front of the user, are the animations built (the first time)
            var anyAnimated = false;
            var shortcuts = target.Shortcuts;
            for (int i = 0; i < shortcuts.Count; i++) anyAnimated |= shortcuts[i].PrepareAnimation();
            if (!anyAnimated) {
               Stop();
               return;
            }
            if (!running) {
               running = true;
               startedAt = millisecondsNow(); // every time the dialog opens, the animations start from their first picture
            }
            Tick();
         } catch (Exception) {
            Stop(); // an animation is decoration: whatever went wrong, leave the buttons still instead of failing the dialog
         }
      }

      private void Stop() {
         running = false;
         timerA.Reset();
         timerB.Reset();
      }

      private void Wake(IDelayWorkTimer timer) {
         if (arming) return; // the dispatcher runs timers at once (tests): there is no real wait, so don't run this recursively. Tick() can be called by hand then.
         executing = timer;
         try {
            Tick();
         } finally {
            executing = null;
         }
      }

      /// <summary>
      /// Show the frame of every animated button that belongs to now, then wait until the earliest next change. The timer calls this; tests can call it too.
      /// </summary>
      public void Tick() {
         if (!running || target == null) return;
         try {
            var elapsed = millisecondsNow() - startedAt;
            var next = long.MaxValue;
            var shortcuts = target.Shortcuts;
            for (int i = 0; i < shortcuts.Count; i++) {
               var shortcut = shortcuts[i];
               if (!shortcut.IsAnimated) continue;
               shortcut.ShowFrameAt(elapsed, out var until);
               if (until < next) next = until;
            }
            if (next == long.MaxValue) {
               Stop();
               return;
            }
            Wait(next);
         } catch (Exception) {
            Stop();
         }
      }

      private void Wait(long milliseconds) {
         var delay = (int)Math.Min(int.MaxValue, Math.Max(MinimumDelayMilliseconds, milliseconds));
         var timer = ReferenceEquals(executing, timerA) ? timerB : timerA;
         (ReferenceEquals(timer, timerA) ? timerB : timerA).Reset();
         arming = true;
         try {
            timer.DelayCall(TimeSpan.FromMilliseconds(delay), ReferenceEquals(timer, timerA) ? wakeA : wakeB);
         } finally {
            arming = false;
         }
      }
   }
}
