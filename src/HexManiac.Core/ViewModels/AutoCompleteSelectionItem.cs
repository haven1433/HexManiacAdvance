using HavenSoft.HexManiac.Core.Models.Runs;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace HavenSoft.HexManiac.Core.ViewModels {
   public class AutoCompleteSelectionItem : IEquatable<AutoCompleteSelectionItem>, INotifyPropertyChanged {
      public string DisplayText { get; }
      public string CompletionText { get; }
      public bool IsSelected { get; }

      event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged { add { } remove { } }

      public AutoCompleteSelectionItem(string text, bool selection) => (CompletionText, DisplayText, IsSelected) = (text, text, selection);
      public AutoCompleteSelectionItem(string display, string completion, bool selection) => (CompletionText, DisplayText, IsSelected) = (display, completion, selection);

      public static int SelectedIndex(IReadOnlyList<AutoCompleteSelectionItem> options) {
         for (int i = 0; i < options.Count; i++) {
            if (options[i].IsSelected) return i;
         }
         return -1;
      }

      public static IReadOnlyList<AutoCompleteSelectionItem> Generate(IEnumerable<string> options, int selectionIndex) {
         var list = new List<AutoCompleteSelectionItem>();

         int i = 0;
         foreach (var option in options ?? new string[0]) {
            list.Add(new AutoCompleteSelectionItem(option, i == selectionIndex));
            i++;
         }

         return list;
      }

      public static IReadOnlyList<AutoCompleteSelectionItem> Generate(IEnumerable<AutocompleteItem> options, int selectionIndex) {
         var list = new List<AutoCompleteSelectionItem>();

         int i = 0;
         foreach (var option in options) {
            list.Add(new AutoCompleteSelectionItem(option.Text, option.LineText, i == selectionIndex));
            i++;
         }

         return list;
      }

      public bool Equals(AutoCompleteSelectionItem other) {
         if (other == null) return false;
         return IsSelected == other.IsSelected && CompletionText == other.CompletionText;
      }
   }
}
