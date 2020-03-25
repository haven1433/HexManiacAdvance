using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class ToolTray : ViewModelCore, IToolTrayViewModel {
      private readonly IList<IToolViewModel> tools;
      private readonly StubCommand hideCommand;
      private readonly StubCommand stringToolCommand, tableToolCommand, codeToolCommand, spriteToolCommand;
      private readonly HashSet<Action> deferredWork = new HashSet<Action>();
      private readonly IDataModel model;

      private int selectedIndex;
      public int SelectedIndex {
         get => selectedIndex;
         set {
            if (TryUpdate(ref selectedIndex, value)) {
               hideCommand.CanExecuteChanged.Invoke(hideCommand, EventArgs.Empty);
               RequestMenuClose?.Invoke(this, EventArgs.Empty);
            }
         }
      }

      public IToolViewModel SelectedTool => selectedIndex == -1 ? null : tools[selectedIndex];

      public int Count => tools.Count;
      public IToolViewModel this[int index] => tools[index];

      public ICommand HideCommand => hideCommand;
      public ICommand StringToolCommand => stringToolCommand;
      public ICommand TableToolCommand => tableToolCommand;
      public ICommand CodeToolCommand => codeToolCommand;
      public ICommand SpriteToolCommand => spriteToolCommand;

      public PCSTool StringTool => (PCSTool)tools[1];

      public TableTool TableTool => (TableTool)tools[0];

      public CodeTool CodeTool => (CodeTool)tools[3];

      public SpriteTool SpriteTool => (SpriteTool)tools[2];

      private StubDisposable currentDeferralToken;
      public IDisposable DeferUpdates {
         get {
            Debug.Assert(currentDeferralToken == null);
            currentDeferralToken = new StubDisposable {
               Dispose = () => {
                  using (ModelCacheScope.CreateScope(model)) {
                     foreach (var action in deferredWork) action();
                     deferredWork.Clear();
                     currentDeferralToken = null;
                  }
               }
            };
            return currentDeferralToken;
         }
      }

      public event EventHandler<string> OnError;
      public event EventHandler<string> OnMessage;
      public event EventHandler RequestMenuClose;

      public ToolTray(Singletons singletons, IDataModel model, Selection selection, ChangeHistory<ModelDelta> history, ViewPort viewPort) {
         this.model = model;
         tools = new IToolViewModel[] {
            new TableTool(model, selection, history, viewPort, this),
            new PCSTool(model, selection, history, this),
            new SpriteTool(viewPort),
            new CodeTool(singletons, model, selection, history),
         };

         StubCommand commandFor(int i) => new StubCommand {
            CanExecute = ICommandExtensions.CanAlwaysExecute,
            Execute = arg => SelectedIndex = selectedIndex == i ? -1 : i,
         };

         tableToolCommand = commandFor(0);
         stringToolCommand = commandFor(1);
         spriteToolCommand = commandFor(2);
         codeToolCommand = commandFor(3);
         
         hideCommand = new StubCommand {
            CanExecute = arg => SelectedIndex != -1,
            Execute = arg => SelectedIndex = -1,
         };

         SelectedIndex = 0; // table tool is open by default

         StringTool.OnError += (sender, e) => OnError?.Invoke(this, e);
         TableTool.OnError += (sender, e) => OnError?.Invoke(this, e);
         TableTool.OnMessage += (sender, e) => OnMessage?.Invoke(this, e);
         SpriteTool.OnMessage += (sender, e) => OnMessage?.Invoke(this, e);
         TableTool.RequestMenuClose += (sender, e) => RequestMenuClose?.Invoke(this, e);
      }

      public void Schedule(Action action) {
         if (currentDeferralToken != null) {
            deferredWork.Add(action);
         } else {
            action();
         }
      }

      public void RefreshContent() {
         TableTool.DataForCurrentRunChanged();
      }

      public IEnumerator<IToolViewModel> GetEnumerator() => tools.GetEnumerator();

      IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
   }
}
