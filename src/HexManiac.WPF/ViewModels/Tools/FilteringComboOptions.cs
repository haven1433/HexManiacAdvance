using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace HavenSoft.HexManiac.Core.ViewModels.Tools {

   // type a letter, arrow down, shouldn't update the text in the textbox

   public class FilteringComboOptions : ViewModelCore {
      private enum InteractionType { None, Text, DropDown, Update }
      private InteractionType interactionType;

      public ObservableCollection<ComboOption> AllOptions { get; private set; }
      public ObservableCollection<ComboOption> FilteredOptions { get; private set; }

      public bool CanFilter => AllOptions?.All(option => option.DisplayAsText) ?? false;

      // direct-editing the text updates the filter / opens the dropdown
      private string displayText;
      public string DisplayText {
         get => displayText;
         set {
            if (interactionType != InteractionType.None) return;
            interactionType = InteractionType.Text;
            dropDownIsOpen = true;
            displayText = value;
            FilteredOptions = new(AllOptions.Where(option => option.Text.MatchesPartial(displayText)).OrderBy(option => option.Text.SkipCount(displayText)));
            selectedIndex = 0;
            NotifyPropertiesChanged(nameof(FilteredOptions), nameof(SelectedIndex), nameof(DisplayText), nameof(DropDownIsOpen), nameof(ModelValue));
            interactionType = InteractionType.None;
         }
      }

      // direct-editing the selected index clears the filter
      private int selectedIndex;
      public int SelectedIndex {
         get => selectedIndex;
         set {
            if (interactionType != InteractionType.None) return;
            if (selectedIndex < 0 || selectedIndex >= FilteredOptions.Count) return;
            interactionType = InteractionType.DropDown;
            selectedIndex = value;
            dropDownIsOpen = false;
            if (!ClearFilter()) {
               displayText = AllOptions[selectedIndex].Text;
               NotifyPropertiesChanged(nameof(SelectedIndex), nameof(DisplayText), nameof(DropDownIsOpen), nameof(ModelValue));
            }
            interactionType = InteractionType.None;
         }
      }

      public int ModelValue {
         get {
            if (FilteredOptions.Count <= selectedIndex) return 0;
            return FilteredOptions[selectedIndex].Index;
         }
      }

      // direct-closing the drop-down clears the filter
      private bool dropDownIsOpen;
      public bool DropDownIsOpen {
         get => dropDownIsOpen;
         set {
            if (interactionType != InteractionType.None) return;
            interactionType = InteractionType.DropDown;
            dropDownIsOpen = value;
            if (!value) {
               ClearFilter();
            } else {
               NotifyPropertyChanged(nameof(DropDownIsOpen));
            }
            interactionType = InteractionType.None;
         }
      }

      public void Update(IEnumerable<ComboOption> options, int selection) {
         if (interactionType != InteractionType.None) return;
         interactionType = InteractionType.Update;
         bool notifyOptions = false, notifyDropOpen = false;

         // warning: this might be a performance sink. It might be faster to replace elements, rather than replacing the full collection.
         if (AllOptions == null || !AllOptions.Select(option => option.Text).SequenceEqual(options.Select(option => option.Text))) {
            AllOptions = new(options);
            FilteredOptions = new(options);
            notifyOptions = true;
         } else if (selectedIndex == selection && !dropDownIsOpen) {
            // no need to notify, nothing changed
            interactionType = InteractionType.None;
            return;
         }

         selectedIndex = selection;
         if (!selectedIndex.InRange(0, AllOptions.Count)) selectedIndex = -1;
         notifyDropOpen = !dropDownIsOpen;
         dropDownIsOpen = false;
         displayText = selectedIndex >= 0 ? AllOptions[selectedIndex].Text : selection.ToString();

         if (notifyOptions) NotifyPropertiesChanged(nameof(CanFilter), nameof(AllOptions), nameof(FilteredOptions));
         NotifyPropertiesChanged(nameof(SelectedIndex), nameof(DisplayText), nameof(ModelValue));
         if (notifyDropOpen) NotifyPropertyChanged(nameof(DropDownIsOpen));
         interactionType = InteractionType.None;
      }

      #region Keyboard Command Methods

      public void SelectUp() => SelectMove(-1);

      public void SelectDown() => SelectMove(1);

      public void SelectConfirm() {
         interactionType = InteractionType.DropDown;

         dropDownIsOpen = false;
         ClearFilter();

         interactionType = InteractionType.None;
      }

      private void SelectMove(int direction) {
         if (interactionType != InteractionType.None) return;
         if (FilteredOptions.Count == 0) return;
         interactionType = InteractionType.DropDown;

         selectedIndex = (selectedIndex + direction).LimitToRange(0, FilteredOptions.Count - 1);
         NotifyPropertyChanged(nameof(SelectedIndex));
         if (!dropDownIsOpen) {
            displayText = FilteredOptions[selectedIndex].Text;
            NotifyPropertyChanged(nameof(DisplayText));
         }
         NotifyPropertyChanged(nameof(ModelValue));

         interactionType = InteractionType.None;
      }

      private bool ClearFilter() {
         if (FilteredOptions.SequenceEqual(AllOptions)) return false;
         var selectedOption = FilteredOptions.Count == 0 ? 0 : FilteredOptions[selectedIndex.LimitToRange(0, FilteredOptions.Count - 1)].Index;
         FilteredOptions = new(AllOptions);
         selectedIndex = AllOptions.IndexOf(AllOptions.Single(option => option.Index == selectedOption));
         displayText = AllOptions[selectedIndex].Text;
         NotifyPropertiesChanged(nameof(FilteredOptions), nameof(SelectedIndex), nameof(DisplayText), nameof(DropDownIsOpen), nameof(ModelValue));
         return true;
      }

      #endregion
   }
}
