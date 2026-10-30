using HavenSoft.HexManiac.Core;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace HavenSoft.HexManiac.WPF.Controls;

public class DelayWorkTimer : IDelayWorkTimer {
   private CancellationTokenSource? cancel;

   public bool HasScheduledWork => cancel != null;

   public DelayWorkResult DelayCall(TimeSpan delay, Action action) {
      var result = HasScheduledWork ? DelayWorkResult.WorkScheduledAndPreviousWorkCleared : DelayWorkResult.WorkScheduled;
      cancel?.Cancel();
      cancel = new CancellationTokenSource();
      Run(delay, action, cancel.Token);
      return result;
   }

   public void Reset() {
      cancel?.Cancel();
      cancel = null;
   }

   private async void Run(TimeSpan delay, Action action, CancellationToken token) {
      try {
         await Task.Delay(delay, token);
      } catch (TaskCanceledException) { }
      if (token.IsCancellationRequested) return;
      action();
      cancel = null;
   }
}
