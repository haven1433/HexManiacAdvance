using HavenSoft.Gen3Hex.Core.Models;
using HavenSoft.Gen3Hex.Core.ViewModels.DataFormats;
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

      public ToolTray(IModel model) {
         tools = new[] {
            new PCSTool(model),
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
      public string Name => "String";

      private string content;
      public string Content {
         get => content;
         set {
            if (TryUpdate(ref content, value)) {
               var run = model.GetNextRun(address) as PCSRun;
               if (run == null) return;
               var bytes = PCSString.Convert(content);
               var newRun = model.RelocateForExpansion(run, bytes.Count);
               for (int i = 0; i < bytes.Count; i++) model[newRun.Start + i] = bytes[i];
               model.ObserveRunWritten(new PCSRun(newRun.Start, bytes.Count, newRun.PointerSources));
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

      public PCSTool(IModel model) => this.model = model;
   }
}
