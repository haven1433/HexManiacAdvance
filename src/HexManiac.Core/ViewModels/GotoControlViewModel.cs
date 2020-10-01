using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using static HavenSoft.HexManiac.Core.ICommandExtensions;

namespace HavenSoft.HexManiac.Core.ViewModels {
   public class GotoControlViewModel : ViewModelCore {
      private readonly IViewPort viewPort;
      private bool withinTextChange = false;

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
            if (viewPort?.Model == null) return;
            if (TryUpdate(ref text, value)) {
               withinTextChange = true;
               using (new StubDisposable { Dispose = () => withinTextChange = false }) {
                  using (ModelCacheScope.CreateScope(viewPort.Model)) {
                     var options = viewPort.Model.GetExtendedAutocompleteOptions(text);
                     AutoCompleteOptions = AutoCompleteSelectionItem.Generate(options, completionIndex);
                     ShowAutoCompleteOptions = AutoCompleteOptions.Count > 0;
                     UpdatePrefixSelectionsAfterTextChange();
                  }
               }
            }
         }
      }

      private int completionIndex = -1;
      public int CompletionIndex {
         get => completionIndex;
         set {
            if (value < -1) value = autoCompleteOptions.Count - 1;
            if (value >= autoCompleteOptions.Count) value = -1;
            if (TryUpdate(ref completionIndex, value)) {
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
         private set => TryUpdateSequence<IReadOnlyList<AutoCompleteSelectionItem>, AutoCompleteSelectionItem>(ref autoCompleteOptions, value);
      }

      #endregion

      #region Commands

      public ICommand MoveAutoCompleteSelectionUp { get; }
      public ICommand MoveAutoCompleteSelectionDown { get; }
      public ICommand Goto { get; }
      public ICommand ShowGoto { get; }                        // arg -> true to show, false to hide

      #endregion

      public event EventHandler MoveFocusToGoto;

      public ObservableCollection<GotoLabelSection> PrefixSelections { get; }

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
         PrefixSelections = new ObservableCollection<GotoLabelSection>();
         UpdatePrefixSelectionsAfterTextChange();
      }

      private void UpdatePrefixSelectionsAfterTextChange() {
         var previousSelections = GotoLabelSection.GetSectionSelections(PrefixSelections).ToArray();
         PrefixSelections.Clear();
         if (viewPort == null || viewPort.Model == null) return;
         var section = GotoLabelSection.Build(viewPort.Model, Text, PrefixSelections);
         PrefixSelections.Add(AddListeners(section));
         for (int i = 0; i < previousSelections.Length; i++) {
            if (PrefixSelections.Count <= i) break;
            var matchingToken = PrefixSelections[i].Tokens.FirstOrDefault(token => token.Content == previousSelections[i]);
            if (matchingToken == null) break;
            matchingToken.IsSelected = true;
         }
      }

      private void UpdatePrefixSelectionsAfterSelectionMade() {
         var currentSelection = string.Join(".", GotoLabelSection.GetSectionSelections(PrefixSelections));
         var address = Pointer.NULL;
         var matchedWords = viewPort.Model.GetMatchedWords(currentSelection);
         if (matchedWords.Count > 0) {
            viewPort?.Goto?.Execute(currentSelection);
            ControlVisible = false;
            ShowAutoCompleteOptions = false;
            DeselectLastRow();
            return;
         }

         using (ModelCacheScope.CreateScope(viewPort.Model)) {
            address = viewPort.Model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, currentSelection);
         }
         if (address != Pointer.NULL && !withinTextChange) {
            viewPort?.Goto?.Execute(address);
            ControlVisible = false;
            ShowAutoCompleteOptions = false;
            DeselectLastRow();
         } else if (address != Pointer.NULL) {
            // we could go to the address, but this is a text change.
            // The user may try to click the button.
            // Deselect it.
            DeselectLastRow();
         } else {
            var newSection = GotoLabelSection.Build(viewPort.Model, Text, PrefixSelections);
            PrefixSelections.Add(AddListeners(newSection));
         }
      }

      private void DeselectLastRow() {
         foreach (var token in PrefixSelections.Last().Tokens) token.IsSelected = false;
      }

      private GotoLabelSection AddListeners(GotoLabelSection section) {
         section.ClearLowerRows += (sender, e) => {
            var index = PrefixSelections.IndexOf(sender);
            while (index >= 0 && PrefixSelections.Count > index + 1) PrefixSelections.RemoveAt(PrefixSelections.Count - 1);
         };
         section.GenerateLowerRow += (sender, e) => {
            UpdatePrefixSelectionsAfterSelectionMade();
         };
         return section;
      }
   }

   public static class IDataModelExtensions {
      /// <summary>
      /// Returns a list of autocomplete options, based on the current Text.
      /// If there are very few options and there are no / characters, this also looks for elements with those names.
      /// This allows the user to get results when searching for "charizard" or "brock"
      /// </summary>
      public static IReadOnlyList<string> GetExtendedAutocompleteOptions(this IDataModel model, string text) {
         var options = new List<string>(model?.GetAutoCompleteAnchorNameOptions(text, int.MaxValue) ?? new string[0]);
         options.AddRange(model?.GetAutoCompleteByteNameOptions(text) ?? new string[0]);
         if (!text.Contains("/") && options.Count < 30) {
            options.AddRange(model?.GetAutoCompleteAnchorNameOptions("/" + text) ?? new string[0]);
         }
         text = text.ToLower();
         var bestMatches = options.Where(option => option.ToLower().Contains(text));
         options = bestMatches.Concat(options).Distinct().ToList();
         return options;
      }
   }

   public class GotoLabelSection : ViewModelCore {
      private int width, height;
      public int Width { get => width; set => Set(ref width, value); }
      public int Height { get => height; set => Set(ref height, value); }
      public ObservableCollection<GotoToken> Tokens { get; }

      public event EventHandler GenerateLowerRow;
      public event EventHandler ClearLowerRows;

      public GotoLabelSection(IEnumerable<string> allOptions, IEnumerable<string> previousSectionSelections) {
         var prefix = string.Join(".", previousSectionSelections);
         if (prefix.Length > 0) prefix += ".";
         var thisLevel = new HashSet<string>();
         foreach (var option in allOptions) {
            if (option.StartsWith(prefix)) {
               thisLevel.Add(option.Substring(prefix.Length).Split(".")[0]);
            }
         }
         Tokens = GotoToken.Generate(thisLevel);
         Initialize();
      }

      public GotoLabelSection(string prefix, IList<GotoToken> tokens) {
         Tokens = new ObservableCollection<GotoToken>();
         foreach (var token in tokens) Tokens.Add(new GotoToken { Content = prefix + "." + token.Content, IsSelected = token.IsSelected });
         if (tokens.Count == 0) {
            Tokens.Add(new GotoToken { Content = prefix });
         }
         Initialize();
      }

      private void Initialize() {
         height = (int)Math.Floor(Math.Sqrt(Tokens.Count));
         width = (int)Math.Ceiling(Tokens.Count / (double)height);
         foreach (var token in Tokens) {
            token.Bind(nameof(GotoToken.IsSelected), (obj, e) => {
               if (!obj.IsSelected) {
                  ClearLowerRows?.Invoke(this, EventArgs.Empty);
               } else {
                  foreach (var t in Tokens) t.IsSelected = t == token;
                  GenerateLowerRow?.Invoke(this, EventArgs.Empty);
               }
            });
         }
      }

      public static IEnumerable<string> GetSectionSelections(IEnumerable<GotoLabelSection> sections) {
         foreach (var section in sections) {
            var selectedToken = section.Tokens.FirstOrDefault(token => token.IsSelected);
            if (selectedToken == null) yield break;
            yield return selectedToken.Content;
         }
      }

      public static GotoLabelSection Build(IDataModel model, string filter, IEnumerable<GotoLabelSection> previousSections) {
         using (ModelCacheScope.CreateScope(model)) {
            var allOptions = model.GetExtendedAutocompleteOptions(filter);
            var selections = GetSectionSelections(previousSections).ToList();

            var newSection = new GotoLabelSection(allOptions ?? new string[0], selections);
            if (newSection.Tokens.Count == 1) {
               newSection.Tokens[0].IsSelected = true;
               var child = Build(model, filter, previousSections.Concat(new[] { newSection })); // recursion ftw
               newSection = new GotoLabelSection(newSection.Tokens[0].Content, child.Tokens);
            }

            return newSection;
         }
      }
   }

   public class GotoToken : ViewModelCore {
      private bool isSelected;
      public bool IsSelected { get => isSelected; set => Set(ref isSelected, value); }

      private string content;
      public string Content { get => content; set => Set(ref content, value); }

      public static ObservableCollection<GotoToken> Generate(IEnumerable<string> content) {
         var collection = new ObservableCollection<GotoToken>();
         foreach (var c in content) collection.Add(new GotoToken { Content = c });
         return collection;
      }
   }
}
