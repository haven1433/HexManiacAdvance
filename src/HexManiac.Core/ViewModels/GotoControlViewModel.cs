using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using static HavenSoft.HexManiac.Core.ICommandExtensions;

namespace HavenSoft.HexManiac.Core.ViewModels {
   public class GotoControlViewModel : ViewModelCore {
      private readonly IViewPort viewPort;

      #region NotifyProperties

      private bool controlVisible;
      public bool ControlVisible {
         get => controlVisible;
         set {
            if (TryUpdate(ref controlVisible, value) && value) CompletionIndex = -1;
            if (value) MoveFocusToGoto?.Invoke(this, EventArgs.Empty);
         }
      }

      private string text = string.Empty;
      public string Text {
         get => text;
         set {
            if (viewPort == null) return;
            if (TryUpdate(ref text, value)) {
               var options = viewPort.Model.GetAutoCompleteAnchorNameOptions(text);
               AutoCompleteOptions = CreateAutoCompleteOptions(options, options.Count);
               ShowAutoCompleteOptions = AutoCompleteOptions.Count > 0;
            }
         }
      }

      private int completionIndex = -1;
      public int CompletionIndex {
         get => completionIndex;
         set {
            if (TryUpdate(ref completionIndex, value.LimitToRange(-1, autoCompleteOptions.Count - 1))) {
               AutoCompleteOptions = CreateAutoCompleteOptions(AutoCompleteOptions.Select(option => option.CompletionText), AutoCompleteOptions.Count);
            }
         }
      }

      private bool showAutoCompleteOptions;
      public bool ShowAutoCompleteOptions {
         get => showAutoCompleteOptions;
         set => TryUpdate(ref showAutoCompleteOptions, value);
      }

      private IReadOnlyList<AutoCompleteSelectionItem> autoCompleteOptions = new AutoCompleteSelectionItem[0];
      public IReadOnlyList<AutoCompleteSelectionItem> AutoCompleteOptions {
         get => autoCompleteOptions;
         private set {
            if (autoCompleteOptions == value) return;
            var oldValue = autoCompleteOptions;
            autoCompleteOptions = value;
            NotifyPropertyChanged(oldValue, nameof(AutoCompleteOptions));
         }
      }

      #endregion

      #region Commands

      public ICommand MoveAutoCompleteSelectionUp { get; }
      public ICommand MoveAutoCompleteSelectionDown { get; }
      public ICommand Goto { get; }
      public ICommand ShowGoto { get; }                        // arg -> true to show, false to hide

      #endregion

      public event EventHandler MoveFocusToGoto;

      public GotoControlViewModel(ITabContent tabContent) {
         viewPort = (tabContent as IViewPort);
         MoveAutoCompleteSelectionUp = new StubCommand {
            CanExecute = CanAlwaysExecute,
            Execute = arg => CompletionIndex--,
         };
         MoveAutoCompleteSelectionDown = new StubCommand {
            CanExecute = CanAlwaysExecute,
            Execute = arg => CompletionIndex++,
         };
         Goto = new StubCommand {
            CanExecute = arg => viewPort?.Goto != null,
            Execute = arg => {
               var text = Text;
               if (CompletionIndex != -1) text = AutoCompleteOptions[CompletionIndex].CompletionText;
               if (arg is string) text = (string)arg;
               viewPort?.Goto?.Execute(text);
               ControlVisible = false;
               ShowAutoCompleteOptions = false;
            },
         };
         ShowGoto = new StubCommand {
            CanExecute = arg => viewPort?.Goto != null && arg is bool,
            Execute = arg => ControlVisible = (bool)arg,
         };
      }

      private IReadOnlyList<AutoCompleteSelectionItem> CreateAutoCompleteOptions(IEnumerable<string> options, int length) {
         if (completionIndex >= length) {
            completionIndex = length - 1;
            NotifyPropertyChanged(nameof(CompletionIndex));
         }
         var list = new List<AutoCompleteSelectionItem>(length);

         int i = 0;
         foreach (var option in options) {
            list.Add(new AutoCompleteSelectionItem(option, i == completionIndex));
            i++;
         }

         return list;
      }
   }

   public class AutoCompleteSelectionItem {
      public string CompletionText { get; }
      public bool IsSelected { get; }
      public AutoCompleteSelectionItem(string text, bool selection) => (CompletionText, IsSelected) = (text, selection);
   }
}
