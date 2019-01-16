using HavenSoft.Gen3Hex.Core.Models;
using HavenSoft.Gen3Hex.Core.ViewModels.DataFormats;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;

namespace HavenSoft.Gen3Hex.Core.ViewModels {
   public interface IToolTrayViewModel : IReadOnlyList<IToolViewModel>, INotifyPropertyChanged {
      int SelectedIndex { get; set; }

      PCSTool StringTool { get; }
   }

   public class ToolTray : ViewModelCore, IToolTrayViewModel {
      private readonly IList<IToolViewModel> tools;
      private readonly StubCommand hideCommand;
      private readonly StubCommand stringToolCommand, tool2Command, tool3Command;

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
      public ICommand Tool2Command => tool2Command;
      public ICommand Tool3Command => tool3Command;

      public PCSTool StringTool => (PCSTool)tools[0];

      public IToolViewModel Tool2 => tools[1];

      public IToolViewModel Tool3 => tools[2];

      public ToolTray(IModel model, ChangeHistory<DeltaModel> history) {
         tools = new IToolViewModel[] {
            new PCSTool(model, history),
            new FillerTool("Tool2"),
            new FillerTool("Tool3"),
         };

         stringToolCommand = new StubCommand {
            CanExecute = ICommandExtensions.CanAlwaysExecute,
            Execute = arg => SelectedIndex = selectedIndex == 0 ? -1 : 0,
         };

         tool2Command = new StubCommand {
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
      }

      public IEnumerator<IToolViewModel> GetEnumerator() => tools.GetEnumerator();

      IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
   }

   public interface IToolViewModel : INotifyPropertyChanged {
      string Name { get; }
   }

   public class PCSTool : ViewModelCore, IToolViewModel {
      private readonly IModel model;
      private readonly ChangeHistory<DeltaModel> history;
      public string Name => "String";

      private string content;
      public string Content {
         get => content;
         set {
            if (TryUpdate(ref content, value)) {
               var run = model.GetNextRun(address) as PCSRun;
               if (run == null) return;
               var bytes = PCSString.Convert(content);
               var newRun = model.RelocateForExpansion(history.CurrentChange, run, bytes.Count);
               if (run.Start != newRun.Start) ModelDataMoved?.Invoke(this, (run.Start, newRun.Start));

               // clear out excess bytes that are no longer in use
               if (run.Start == newRun.Start) {
                  for (int i = bytes.Count; i < run.Length; i++) history.CurrentChange.ChangeData(model, run.Start + i, 0xFF);
               }

               for (int i = 0; i < bytes.Count; i++) history.CurrentChange.ChangeData(model, newRun.Start + i, bytes[i]);
               run = new PCSRun(newRun.Start, bytes.Count, newRun.PointerSources);
               model.ObserveRunWritten(history.CurrentChange, run);
               ModelDataChanged?.Invoke(this, run);
               TryUpdate(ref address, newRun.Start, nameof(Address));
            }
         }
      }

      private int address = Pointer.NULL;
      public int Address {
         get => address;
         set {
            var run = model.GetNextRun(value);
            if (run.Start > value || run.Start + run.Length <= value) return;
            if (TryUpdate(ref address, run.Start)) {
               if (run is PCSRun) {
                  TryUpdate(ref content, PCSString.Convert(model, run.Start, run.Length), nameof(Content));
                  history.ChangeCompleted();
               }
            }
         }
      }

      public event EventHandler<IFormattedRun> ModelDataChanged;
      public event EventHandler<(int originalLocation, int newLocation)> ModelDataMoved;

      public PCSTool(IModel model, ChangeHistory<DeltaModel> history) => (this.model, this.history) = (model, history);
   }

   public class FillerTool : IToolViewModel {
      public string Name { get; }
      public FillerTool(string name) { Name = name; }
      public event PropertyChangedEventHandler PropertyChanged;
   }
}
