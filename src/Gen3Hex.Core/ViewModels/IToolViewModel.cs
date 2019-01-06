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
      private readonly StubCommand stringToolCommand;

      private int selectedIndex;
      public int SelectedIndex {
         get => selectedIndex;
         set => TryUpdate(ref selectedIndex, value);
      }

      public int Count => tools.Count;
      public IToolViewModel this[int index] => tools[index];
      public PCSTool StringTool => (PCSTool)tools[0];
      public ICommand StringToolCommand => stringToolCommand;

      public ToolTray(IModel model, ChangeHistory<DeltaModel> history) {
         tools = new[] {
            new PCSTool(model, history),
         };

         stringToolCommand = new StubCommand {
            CanExecute = ICommandExtensions.CanAlwaysExecute,
            Execute = arg => SelectedIndex = selectedIndex == 0 ? -1 : 0,
         };

         foreach (var tool in tools) tool.PropertyChanged += (sender, e) => SelectedIndex = tools.IndexOf((IToolViewModel)sender);
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
                  for (int i = bytes.Count; i < run.Length; i++) model[run.Start + i] = 0xFF;
               }

               for (int i = 0; i < bytes.Count; i++) model[newRun.Start + i] = bytes[i];
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
            if (TryUpdate(ref address, value)) {
               var run = model.GetNextRun(address) as PCSRun;
               if (run == null) return;
               TryUpdate(ref content, PCSString.Convert(model, run.Start, run.Length), nameof(Content));
            }
         }
      }

      public event EventHandler<IFormattedRun> ModelDataChanged;
      public event EventHandler<(int originalLocation, int newLocation)> ModelDataMoved;

      public PCSTool(IModel model, ChangeHistory<DeltaModel> history) => (this.model, this.history) = (model, history);
   }
}
