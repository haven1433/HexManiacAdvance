using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;


namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class ToolTray : ViewModelCore, IToolTrayViewModel {
      public readonly IList<IToolViewModel> tools;
      public readonly HashSet<Action> deferredWork = new HashSet<Action>();
      public readonly IDataModel model;
      public readonly Selection selection;
      public int selectedIndex;

      public int Count => tools.Count;
      public IToolViewModel this[int index] => tools[index];

      public PCSTool StringTool => (PCSTool)tools[1];

      public TableTool TableTool => (TableTool)tools[0];

      public CodeTool CodeTool => (CodeTool)tools[3];

      public SpriteTool SpriteTool => (SpriteTool)tools[2];

      public bool runningDeferredWork;
      public StubDisposable currentDeferralToken;
      public IDisposable DeferUpdates {
         get {
            Debug.Assert(currentDeferralToken == null);
            currentDeferralToken = new StubDisposable {
               Dispose = () => {
                  var workingCopy = deferredWork.ToList();
                  runningDeferredWork = true;
                  currentDeferralToken = null;
                  using (new StubDisposable { Dispose = () => runningDeferredWork = false }) {
                     foreach (var action in workingCopy) {
                        action();
                        deferredWork.Remove(action);
                     }
                  }
               }
            };
            return currentDeferralToken;
         }
      }

      public event EventHandler<string> OnError;
      public event EventHandler<string> OnMessage;
      public event EventHandler RequestMenuClose;

      public void Schedule(Action action) {
         if (currentDeferralToken != null) {
            Debug.Assert(!runningDeferredWork, "Scheduling deferred work while deferred work is running is not safe!");
            deferredWork.Add(action);
         } else {
            action();
         }
      }

      public IEnumerator<IToolViewModel> GetEnumerator() => tools.GetEnumerator();

      IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
   }
}
