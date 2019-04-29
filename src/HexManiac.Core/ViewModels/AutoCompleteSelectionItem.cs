using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace HavenSoft.HexManiac.Core.ViewModels {
   public class AutoCompleteSelectionItem : IEquatable<AutoCompleteSelectionItem>, INotifyPropertyChanged {
      public string CompletionText { get; }
      public bool IsSelected { get; }

#pragma warning disable 0067 // it's ok if events are never used after implementing an interface
      public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore 0067

      public AutoCompleteSelectionItem(string text, bool selection) => (CompletionText, IsSelected) = (text, selection);

      public static IReadOnlyList<AutoCompleteSelectionItem> Generate(IEnumerable<string> options, int selectionIndex) {
         var list = new List<AutoCompleteSelectionItem>();

         int i = 0;
         foreach (var option in options) {
            list.Add(new AutoCompleteSelectionItem(option, i == selectionIndex));
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
