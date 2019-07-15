using HavenSoft.HexManiac.Core.Models.Runs;
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
               using (ModelCacheScope.CreateScope(viewPort.Model)) {
                  var options = viewPort.Model?.GetAutoCompleteAnchorNameOptions(text) ?? new string[0];
                  AutoCompleteOptions = AutoCompleteSelectionItem.Generate(options, completionIndex);
                  ShowAutoCompleteOptions = AutoCompleteOptions.Count > 0;
               }
            }
         }
      }

      private int completionIndex = -1;
      public int CompletionIndex {
         get => completionIndex;
         set {
            if (TryUpdate(ref completionIndex, value.LimitToRange(-1, autoCompleteOptions.Count - 1))) {
               AutoCompleteOptions = AutoCompleteSelectionItem.Generate(AutoCompleteOptions.Select(option => option.CompletionText), completionIndex);
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
               using (ModelCacheScope.CreateScope(viewPort.Model)) {
                  var text = Text;
                  var index = completionIndex.LimitToRange(-1, AutoCompleteOptions.Count - 1);
                  if (index != -1) text = AutoCompleteOptions[index].CompletionText;
                  if (arg is string) text = (string)arg;
                  viewPort?.Goto?.Execute(text);
                  ControlVisible = false;
                  ShowAutoCompleteOptions = false;
               }
            },
         };
         ShowGoto = new StubCommand {
            CanExecute = arg => viewPort?.Goto != null && arg is bool,
            Execute = arg => ControlVisible = (bool)arg,
         };
      }
   }
}
