using HavenSoft.HexManiac.Core.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public interface IToolTrayViewModel : IReadOnlyList<IToolViewModel>, INotifyPropertyChanged {
      int SelectedIndex { get; set; }

      ICommand HideCommand { get; }
      ICommand StringToolCommand { get; }
      ICommand TableToolCommand { get; }
      ICommand Tool3Command { get; }

      PCSTool StringTool { get; }

      TableTool TableTool { get; }

      IDisposable DeferUpdates { get; }

      event EventHandler<string> OnError;

      void Schedule(Action action);
      void RefreshContent();
   }

   public class ToolTray : ViewModelCore, IToolTrayViewModel {
      private readonly IList<IToolViewModel> tools;
      private readonly StubCommand hideCommand;
      private readonly StubCommand stringToolCommand, tableToolCommand, tool3Command;
      private readonly HashSet<Action> deferredWork = new HashSet<Action>();

      private int selectedIndex;
      public int SelectedIndex {
         get => selectedIndex;
         set {
            if (TryUpdate(ref selectedIndex, value)) {
               hideCommand.CanExecuteChanged.Invoke(hideCommand, EventArgs.Empty);
            }
         }
      }

      public int Count => tools.Count;
      public IToolViewModel this[int index] => tools[index];

      public ICommand HideCommand => hideCommand;
      public ICommand StringToolCommand => stringToolCommand;
      public ICommand TableToolCommand => tableToolCommand;
      public ICommand Tool3Command => tool3Command;

      public PCSTool StringTool => (PCSTool)tools[0];

      public TableTool TableTool => (TableTool)tools[1];

      public IToolViewModel Tool3 => tools[2];

      private StubDisposable currentDeferralToken;
      public IDisposable DeferUpdates {
         get {
            Debug.Assert(currentDeferralToken == null);
            currentDeferralToken = new StubDisposable {
               Dispose = () => {
                  foreach (var action in deferredWork) action();
                  deferredWork.Clear();
                  currentDeferralToken = null;
               }
            };
            return currentDeferralToken;
         }
      }

      public event EventHandler<string> OnError;

      public ToolTray(IDataModel model, Selection selection, ChangeHistory<ModelDelta> history) {
         tools = new IToolViewModel[] {
            new PCSTool(model, selection, history, this),
            new TableTool(model, selection, history, this),
            new FillerTool("Tool3"),
         };

         stringToolCommand = new StubCommand {
            CanExecute = ICommandExtensions.CanAlwaysExecute,
            Execute = arg => SelectedIndex = selectedIndex == 0 ? -1 : 0,
         };

         tableToolCommand = new StubCommand {
            CanExecute = ICommandExtensions.CanAlwaysExecute,
            Execute = arg => SelectedIndex = selectedIndex == 1 ? -1 : 1,
         };

         tool3Command = new StubCommand {
            CanExecute = ICommandExtensions.CanAlwaysExecute,
            Execute = arg => SelectedIndex = selectedIndex == 2 ? -1 : 2,
         };

         hideCommand = new StubCommand {
            CanExecute = arg => SelectedIndex != -1,
            Execute = arg => SelectedIndex = -1,
         };

         SelectedIndex = -1;

         StringTool.OnError += (sender, e) => OnError?.Invoke(this, e);
         TableTool.OnError += (sender, e) => OnError?.Invoke(this, e);
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
