using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Visitors;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;


namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class PCSTool : ViewModelCore, IToolViewModel {
      public readonly IDataModel model;
      public readonly ChangeHistory<ModelDelta> history;
      public readonly IToolTrayViewModel runner;

      // if we're in the middle of updating ourselves, we may notify changes to other controls.
      // while we do, ignore any updates coming from those controls, since we may be in an inconsistent state.
      public bool ignoreExternalUpdates = false;

      public string Name => "String";

      public int contentIndex;

      public int contentSelectionLength;

      private string searchText = string.Empty;
      public string SearchText {
         get => searchText;
         set => TryUpdate(ref searchText, value);
      }

      public bool showMessage = true;
      private string message;
      public string Message {
         get => message;
         set => TryUpdate(ref message, value);
      }

      public string content;

      public int address = Pointer.NULL;

      public bool enabled;

      public ObservableCollection<ContextItem> GotoOptions { get; } = new();

      public event EventHandler<string> OnError;
      public event EventHandler<IFormattedRun> ModelDataChanged;
      public event EventHandler<(int originalLocation, int newLocation)> ModelDataMoved;

      // properties that exist solely so the UI can remember things when the tab switches
      public double VerticalOffset { get; set; }

      public IReadOnlyList<AutocompleteItem> GetAutocomplete(string line, int lineIndex, int characterIndex) {
         var run = model.GetNextRun(address);
         if (!(run is IStreamRun streamRun)) return new AutocompleteItem[0];
         return streamRun.GetAutoCompleteOptions(line, lineIndex, characterIndex);
      }

      public bool AddressIsTextInTableRun(IFormattedRun run, int address) {
         if (run is ITableRun tableRun) {
            var offsets = tableRun.ConvertByteOffsetToArrayOffset(address);
            return tableRun.ElementContent[offsets.SegmentIndex].Type == ElementContentType.PCS;
         }
         return false;
      }

      public string RemoveQuotes(string newContent) {
         if (newContent.Length == 0) return newContent;
         newContent = newContent.Substring(1);
         if (newContent.EndsWith("\"")) newContent = newContent.Substring(0, newContent.Length - 1);
         return newContent;
      }

      /// <summary>
      /// If a selection update is requested due to a change in the Content, ignore it.
      /// Otherwise, the selection update could send us back to the begining of the run.
      /// </summary>
      public bool ignoreSelectionUpdates;
   }
}
