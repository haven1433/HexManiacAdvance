using HavenSoft.HexManiac.Core.Models.Runs;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace HavenSoft.HexManiac.Core.ViewModels {
   public class AutoCompleteSelectionItem : IEquatable<AutoCompleteSelectionItem>, INotifyPropertyChanged {
      public string DisplayText { get; }
      public string CompletionText { get; }
      public bool IsSelected { get; }
      public bool IsFormatComplete { get; set; } = true;

      event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged { add { } remove { } }

      public AutoCompleteSelectionItem(string text, bool selection) => (CompletionText, DisplayText, IsSelected) = (text, text, selection);
      public AutoCompleteSelectionItem(string display, string completion, bool selection) => (DisplayText, CompletionText, IsSelected) = (display, completion, selection);

      public static int SelectedIndex(IEnumerable<AutoCompleteSelectionItem> options) {
         int index = 0;
         foreach (var option in options) {
            if (option.IsSelected) return index;
            index += 1;
         }
         return -1;
      }

      public static IEnumerable<AutoCompleteSelectionItem> Generate(IEnumerable<string> options, int selectionIndex) {
         int i = 0;
         foreach (var option in options ?? new string[0]) {
            yield return new AutoCompleteSelectionItem(option, i == selectionIndex);
            i++;
         }
      }

      public static IEnumerable<AutoCompleteSelectionItem> Generate(IEnumerable<AutocompleteItem> options, int selectionIndex) {
         int i = 0;
         foreach (var option in options) {
            yield return new AutoCompleteSelectionItem(option.Text, option.LineText, i == selectionIndex);
            i++;
         }
      }

      public bool Equals(AutoCompleteSelectionItem other) {
         if (other == null) return false;
         return IsSelected == other.IsSelected && CompletionText == other.CompletionText;
      }
   }
}
