using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {
   public class IndexComboBoxViewModel : ViewModelCore {
      private List<string> allOptions = new List<string>();
      private int selectedIndex, selectedVisibleIndex;
      private string text;

      public IReadOnlyList<string> Options { get; private set; }
      public string Text {
         get => text;
         set {
            if (resetting) return;
            Set(ref text, value, TextChanged);
         }
      }

      public int SelectedIndex {
         get => selectedVisibleIndex;
         set {
            if (value == -1) return;
            if (selectedVisibleIndex != value && !resetting) {
               selectedIndex = allOptions.IndexOf(Options[value]);
               ResetFilter();
               UpdateSelection?.Invoke(this, EventArgs.Empty);
            }
         }
      }

      public event EventHandler UpdateSelection;

      public IDataModel Model { get; }
      public IndexComboBoxViewModel(IDataModel model) => Model = model;

      public void SetupFromModel(int address) {
         if (!(Model.GetNextRun(address) is ITableRun tableRun)) return;
         var offset = tableRun.ConvertByteOffsetToArrayOffset(address);
         allOptions = tableRun.ElementNames?.ToList();
         if (allOptions == null) allOptions = tableRun.ElementCount.Range().Select(i => i.ToString()).ToList();
         while (allOptions.Count < tableRun.ElementCount) {
            allOptions.Add(allOptions.Count.ToString());
         }
         selectedIndex = offset.ElementIndex;
         ResetFilter();
      }

      public void CompleteFilterInteraction() {
         if (Options.Count == 0) {
            selectedIndex = 0;
         } else {
            selectedIndex = allOptions.IndexOf(Options[0]);
         }

         ResetFilter();
         UpdateSelection?.Invoke(this, EventArgs.Empty);
      }

      private void TextChanged(string old) {
         Options = allOptions.Where(option => option.MatchesPartial(text)).ToList();
         selectedVisibleIndex = Options.IndexOf(allOptions[selectedIndex]);
         NotifyProperties(nameof(Options), nameof(SelectedIndex));
      }

      private bool resetting;
      private void ResetFilter() {
         resetting = true;
         Options = allOptions;
         selectedVisibleIndex = selectedIndex;
         text = allOptions[selectedIndex];
         NotifyProperties(nameof(Options), nameof(SelectedIndex), nameof(Text));
         resetting = false;
      }

      private void NotifyProperties(params string[] props) {
         foreach (var prop in props) NotifyPropertyChanged(prop);
      }
   }
}
