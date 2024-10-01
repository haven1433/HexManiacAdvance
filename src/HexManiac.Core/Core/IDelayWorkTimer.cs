using System;

namespace HavenSoft.HexManiac.Core;

public enum DelayWorkResult { WorkScheduled, WorkScheduledAndPreviousWorkCleared }

public interface IDelayWorkTimer {
   /// <summary>
   /// Returns true if 'DelayCall' has been called, but the work has not yet been executed.
   /// </summary>
   bool HasScheduledWork { get; }

   /// <summary>
   /// Will call the action after the delay.
   /// Returns 'WorkScheduledAndPreviousWorkCleared' if this delay work timer was already tracking work.
   /// </summary>
   DelayWorkResult DelayCall(TimeSpan delay, Action action);

   /// <summary>
   /// Clears any currently scheduled work.
   /// </summary>
   void Reset();
}
