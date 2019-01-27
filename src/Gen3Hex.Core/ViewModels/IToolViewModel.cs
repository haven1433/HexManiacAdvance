using HavenSoft.Gen3Hex.Core.Models;
using HavenSoft.Gen3Hex.Core.ViewModels.DataFormats;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace HavenSoft.Gen3Hex.Core.ViewModels {
   public interface IToolTrayViewModel : IReadOnlyList<IToolViewModel>, IActionRunner, INotifyPropertyChanged {
      int SelectedIndex { get; set; }

      PCSTool StringTool { get; }

      IDisposable DeferUpdates { get; }
   }

   public interface IActionRunner {
      void Schedule(Action action);
   }

   public class ToolTray : ViewModelCore, IToolTrayViewModel {
      private readonly IList<IToolViewModel> tools;
      private readonly StubCommand hideCommand;
      private readonly StubCommand stringToolCommand, tool2Command, tool3Command;
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
      public ICommand Tool2Command => tool2Command;
      public ICommand Tool3Command => tool3Command;

      public PCSTool StringTool => (PCSTool)tools[0];

      public IToolViewModel Tool2 => tools[1];

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

      public ToolTray(IModel model, Selection selection, ChangeHistory<DeltaModel> history) {
         tools = new IToolViewModel[] {
            new PCSTool(model, selection, history, this),
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

      public void Schedule(Action action) {
         if (currentDeferralToken != null) {
            deferredWork.Add(action);
         } else {
            action();
         }
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
      private readonly IActionRunner runner;
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
               var run = model.GetNextRun(address);
               if (run is PCSRun pcsRun) UpdateRun(pcsRun);
               if (run is ArrayRun arrayRun) UpdateRun(arrayRun);
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
               if (run is PCSRun || run is ArrayRun) {
                  runner.Schedule(DataForCurrentRunChanged);
                  Enabled = true;
               } else {
                  Enabled = false;
               }
            }
         }
      }

      private bool enabled;
      public bool Enabled {
         get => enabled;
         private set => TryUpdate(ref enabled, value);
      }

      public event EventHandler<IFormattedRun> ModelDataChanged;
      public event EventHandler<(int originalLocation, int newLocation)> ModelDataMoved;

      public PCSTool(IModel model, Selection selection, ChangeHistory<DeltaModel> history, IActionRunner runner) {
         this.model = model;
         this.selection = selection;
         this.history = history;
         this.runner = runner;
      }

      public void DataForCurrentRunChanged() {
         var run = model.GetNextRun(address);
         if (run is PCSRun) {
            var newContent = PCSString.Convert(model, run.Start, run.Length);
            newContent = newContent.Substring(1, newContent.Length - 2); // remove quotes

            TryUpdate(ref content, newContent, nameof(Content));
            return;
         } else if (run is ArrayRun array) {
            var lines = new string[array.ElementCount];
            var offsets = array.ConvertByteOffsetToArrayOffset(address);
            var segment = array.ElementContent[offsets.SegmentIndex];
            for (int i = 0; i < lines.Length; i++) {
               var newContent = PCSString.Convert(model, offsets.SegmentStart + i * array.ElementLength, segment.Length).Trim();
               newContent = newContent.Substring(1, newContent.Length - 2); // remove quotes
               lines[i] = newContent;
            }
            if (lines.Length > 0) {
               var builder = new StringBuilder();
               for(int i = 0; i < lines.Length; i++) {
                  builder.Append(lines[i]);
                  if (i != lines.Length - 1) builder.Append(Environment.NewLine);
               }
               TryUpdate(ref content, builder.ToString(), nameof(Content));
            }
            return;
         }

         throw new NotImplementedException();
      }

      private void UpdateSelectionFromTool() {
         var run = model.GetNextRun(Address);
         if (!(run is ArrayRun) && !(run is PCSRun)) return;

         // for simple strings, the address must be at the start of the run
         if (run is PCSRun && run.Start != Address) return;

         // for arrays, the address must be at the start of a string segment within the first element of the array
         if (run is ArrayRun arrayRun) {
            var offset = arrayRun.ConvertByteOffsetToArrayOffset(Address);
            if (arrayRun.ElementContent[offset.SegmentIndex].Type != ElementContentType.PCS) return;  // must be a string
            if (offset.ElementIndex != 0) return;                                                     // must be first element
            if (offset.SegmentOffset != 0) return;                                                    // must be start of the segment
         }

         var content = Content;
         if (content.Length < contentIndex + contentSelectionLength) return; // transient invalid state
         var selectionStart = contentIndex;
         var selectionLength = contentSelectionLength;

         if (run is PCSRun) {
            selectionLength = Math.Max(PCSString.Convert(content.Substring(selectionStart, selectionLength)).Count - 2, 0); // remove 1 byte since 0xFF was added on and 1 byte since the selection should visually match
            selectionStart = PCSString.Convert(content.Substring(0, selectionStart)).Count - 1 + run.Start; // remove 1 byte since the 0xFF was added on
         } else if (run is ArrayRun array) {
            var offset = array.ConvertByteOffsetToArrayOffset(Address);
            var leadingLines = content.Substring(0, contentIndex).Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            selectionStart = offset.SegmentStart + (leadingLines.Length - 1) * array.ElementLength + leadingLines[leadingLines.Length - 1].Length;

            selectionLength = Math.Max(0, selectionLength - 1); // decrease by one since a selection of 0 and selection of 1 have no difference
            var afterLines = content.Substring(0, contentIndex + selectionLength).Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            var selectionEnd = offset.SegmentStart + (afterLines.Length - 1) * array.ElementLength + afterLines[afterLines.Length - 1].Length;
            selectionLength = selectionEnd - selectionStart;
         }

         selection.SelectionStart = selection.Scroll.DataIndexToViewPoint(selectionStart);
         selection.SelectionEnd = selection.Scroll.DataIndexToViewPoint(selectionStart + selectionLength);
      }

      private void UpdateRun(ArrayRun arrayRun) {
         var lines = content.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
         var arrayByteLength = lines.Length * arrayRun.ElementLength;
         var newRun = (ArrayRun)model.RelocateForExpansion(history.CurrentChange, arrayRun, arrayByteLength);

         TryUpdate(ref address, address + newRun.Start - arrayRun.Start, nameof(Address));

         var offsets = newRun.ConvertByteOffsetToArrayOffset(address);
         if (arrayRun.Start != newRun.Start) ModelDataMoved?.Invoke(this, (arrayRun.Start, newRun.Start));
         var segmentLength = newRun.ElementContent[offsets.SegmentIndex].Length;
         for (int i = 0; i < lines.Length; i++) {
            var bytes = PCSString.Convert(lines[i]);
            if (bytes.Count > segmentLength) bytes[segmentLength - 1] = 0xFF; // truncate and always end with an endstring character
            for (int j = 0; j < segmentLength; j++) {
               if (j < bytes.Count) {
                  history.CurrentChange.ChangeData(model, offsets.SegmentStart + i * newRun.ElementLength + j, bytes[j]);
               } else {
                  history.CurrentChange.ChangeData(model, offsets.SegmentStart + i * newRun.ElementLength + j, 0x00);
               }
            }
         }

         if (newRun.ElementCount != lines.Length) {
            newRun = newRun.Append(lines.Length - newRun.ElementCount);
            model.ObserveRunWritten(history.CurrentChange, newRun);
            history.CurrentChange.AddRun(newRun);
         }

         ModelDataChanged?.Invoke(this, newRun);
      }

      private void UpdateRun(PCSRun run) {
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
         history.CurrentChange.AddRun(run);
         ModelDataChanged?.Invoke(this, run);
         TryUpdate(ref address, run.Start, nameof(Address));
      }
   }

   public class FillerTool : IToolViewModel {
      public string Name { get; }
      public FillerTool(string name) { Name = name; }
      public event PropertyChangedEventHandler PropertyChanged;
   }
}
