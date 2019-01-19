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

      public ToolTray(IModel model, Selection selection, ChangeHistory<DeltaModel> history) {
         tools = new IToolViewModel[] {
            new PCSTool(model, selection, history),
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
      private readonly Selection selection;
      private readonly ChangeHistory<DeltaModel> history;
      public string Name => "String";

      private int contentIndex;
      public int ContentIndex {
         get => contentIndex;
         set {
            if (TryUpdate(ref contentIndex, value)) UpdateSelectionFromTool();
         }
      }

      private int contentSelectionLength;
      public int ContentSelectionLength {
         get => contentSelectionLength;
         set {
            if (TryUpdate(ref contentSelectionLength, value)) UpdateSelectionFromTool();
         }
      }

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
            if (run.Start > value) return;
            if (TryUpdate(ref address, run.Start)) {
               if (run is PCSRun pcsRun) {
                  DataForCurrentRunChanged(pcsRun);
                  history.ChangeCompleted();
               }
            }
         }
      }

      public event EventHandler<IFormattedRun> ModelDataChanged;
      public event EventHandler<(int originalLocation, int newLocation)> ModelDataMoved;

      public PCSTool(IModel model, Selection selection, ChangeHistory<DeltaModel> history) => (this.model, this.selection, this.history) = (model, selection, history);

      public void DataForCurrentRunChanged(IFormattedRun run) {
         if (run is PCSRun) {
            var newContent = PCSString.Convert(model, run.Start, run.Length);
            newContent = newContent.Substring(1, newContent.Length - 2); // remove quotes

            TryUpdate(ref content, newContent, nameof(Content));
            return;
         }

         throw new NotImplementedException();
      }

      private void UpdateSelectionFromTool() {
         var run = model.GetNextRun(Address);
         if (run.Start != Address) return;
         var content = Content;
         if (content.Length < contentIndex + contentSelectionLength) return; // transient invalid state
         var selectionStart = this.contentIndex;
         var selectionLength = this.contentSelectionLength;
         selectionLength = Math.Max(PCSString.Convert(content.Substring(selectionStart, selectionLength)).Count - 1, 0);
         selectionStart = PCSString.Convert(content.Substring(0, selectionStart)).Count + run.Start;

         //while (content.Contains(Environment.NewLine)) {
         //   var index = content.IndexOf(Environment.NewLine);
         //   if (index < selectionStart) {
         //      selectionStart -= Environment.NewLine.Length;
         //   } else if (index < selectionStart + selectionLength) {
         //      selectionLength -= Environment.NewLine.Length;
         //   }
         //   content = content.Split(new[] { Environment.NewLine }, 2, StringSplitOptions.None).Aggregate(string.Concat);
         //}

         //selectionStart += run.Start;
         //selectionLength = Math.Max(0, selectionLength - 1); // if the length is 2, then SelectionEnd should be SelectionStart+1

         selection.SelectionStart = selection.Scroll.DataIndexToViewPoint(selectionStart);
         selection.SelectionEnd = selection.Scroll.DataIndexToViewPoint(selectionStart + selectionLength);
      }
   }

   public class FillerTool : IToolViewModel {
      public string Name { get; }
      public FillerTool(string name) { Name = name; }
      public event PropertyChangedEventHandler PropertyChanged;
   }
}
