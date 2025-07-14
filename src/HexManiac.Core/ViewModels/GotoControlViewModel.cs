using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using HavenSoft.HexManiac.Core.ViewModels.Visitors;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using static HavenSoft.HexManiac.Core.ICommandExtensions;

namespace HavenSoft.HexManiac.Core.ViewModels {
   public class GotoShortcutViewModel : ViewModelCore {
      public readonly GotoControlViewModel viewModel;
      public readonly IViewPort viewPort;
      public readonly string anchor;

      public string DisplayText { get; }

      public IPixelViewModel Image { get; }

      public bool smallMode;
      public bool SmallMode {
         get => smallMode;
         set => Set(ref smallMode, value);
      }

      public bool visible = true;
      public bool Visible { get => visible; set => Set(ref visible, value); }

      public GotoShortcutViewModel(GotoControlViewModel parent, IViewPort viewPort, IPixelViewModel image, string anchor, string display) {
         viewModel = parent;
         this.viewPort = viewPort;
         Image = image;
         this.anchor = anchor;
         DisplayText = display;
      }
   }

   public class GotoControlViewModel : ViewModelCore {
      public readonly IEditableViewPort viewPort;
      public IReadOnlyList<DocLabel> availableDocs;
      public bool withinTextChange = false, devMode = false;

      #region NotifyProperties

      public bool controlVisible;
      public bool ControlVisible {
         get => controlVisible;
         set {
            if (TryUpdate(ref controlVisible, value) && value) CompletionIndex = -1;
            if (value) MoveFocusToGoto?.Invoke(this, EventArgs.Empty);
         }
      }

      public bool loading = true;
      public bool Loading { get => loading; set => Set(ref loading, value); }

      public string text = string.Empty;
      public int completionIndex = -1;
      public int CompletionIndex {
         get => completionIndex;
         set {
            if (value < -1) value = autoCompleteOptions.Count - 1;
            if (value >= autoCompleteOptions.Count) value = -1;
            Set(ref completionIndex, value);
         }
      }

      public bool showAutoCompleteOptions;
      public bool ShowAutoCompleteOptions {
         get => showAutoCompleteOptions;
         set => TryUpdate(ref showAutoCompleteOptions, value);
      }

      public IReadOnlyList<AutoCompleteSelectionItem> autoCompleteOptions = new AutoCompleteSelectionItem[0];
      public IReadOnlyList<AutoCompleteSelectionItem> AutoCompleteOptions {
         get => autoCompleteOptions;
         set => TryUpdateSequence<IReadOnlyList<AutoCompleteSelectionItem>, AutoCompleteSelectionItem>(ref autoCompleteOptions, value);
      }

      public bool showAll;
      public bool ShowAll { get => showAll; set => Set(ref showAll, value, oldValue => {
         if (showAll) MoveFocusToGoto?.Invoke(this, EventArgs.Empty);
         UpdateShortcutSize();
      }); }

      public void UpdateShortcutSize() {
         foreach (var shortcut in shortcuts) shortcut.SmallMode = false;
      }

      public bool allowToggleShowAll = true;
      public bool AllowToggleShowAll { get => allowToggleShowAll; set => Set(ref allowToggleShowAll, value); }

      #endregion

      public event EventHandler MoveFocusToGoto;

      public ObservableCollection<GotoShortcutViewModel> shortcuts = new();
      public ObservableCollection<GotoShortcutViewModel> Shortcuts {
         get => shortcuts;
         set {
            shortcuts = value;
            NotifyPropertyChanged();
            UpdateShortcutSize();
         }
      }

      public ObservableCollection<GotoLabelSection> PrefixSelections { get; }

      public void UpdateDocs(IList<DocLabel> docs) {
         availableDocs = availableDocs.Concat(docs).ToList();
      }

      public void OpenLink(string link) => NativeProcess.Start(link);

      public void DeselectLastRow() {
         ShowAll = true;
         foreach (var token in PrefixSelections.Last().Tokens) token.IsSelected = false;
      }
   }

   public static class IDataModelExtensions {
      /// <summary>
      /// Returns a list of autocomplete options, based on the current Text.
      /// If there are very few options and there are no / characters, this also looks for elements with those names.
      /// This allows the user to get results when searching for "charizard" or "brock"
      /// </summary>
      public static IReadOnlyList<string> GetExtendedAutocompleteOptions(this IDataModel model, string text) {
         text = text.Replace("é", "e");
         var sanitizedText = text.Replace(" ", string.Empty);
         var textWithExtension = sanitizedText;
         if (sanitizedText.EndsWith("~1")) sanitizedText = sanitizedText.Substring(0, text.Length - 2);
         var options = new List<string>(model?.GetAutoCompleteAnchorNameOptions(sanitizedText, int.MaxValue) ?? new string[0]);
         options.AddRange(model?.GetAutoCompleteByteNameOptions(sanitizedText) ?? new string[0]);
         text = text.ToLower();
         for (int i = 1; i < text.Length; i++) {
            var isEndOfSection = i == text.Length - 1 || !char.IsLetter(text[i + 1]);
            var isY = "yY".Contains(text[i]);
            if (isY && isEndOfSection) {
               var startOfText = text.Substring(0, i - 1);
               var endOfText = text.Substring(i + 1);
               var pluralResults = GetExtendedAutocompleteOptions(model, $"{startOfText}ies{endOfText}")
                  .Where(result => !options.Contains(result))
                  .Distinct();
               options.AddRange(pluralResults);
            }
         }
         if (!sanitizedText.Contains("/") && sanitizedText.Length >= 3) {
            options.AddRange((model?.GetAutoCompleteAnchorNameOptions("/" + sanitizedText) ?? new string[0]).Where(option => option.ToLower().Replace(" ", string.Empty).MatchesPartial(sanitizedText.ToLower())));
         }
         var bestMatches = options.Where(option => option.ToLower().Contains(text));
         options = bestMatches.Concat(options).Distinct().ToList();
         if (textWithExtension.EndsWith("~1")) {
            // user only wants the first token that matches, for example, TERRY but not TERRY~2
            options = options.Where(option => !option.Contains("~") || option.Contains("~1")).ToList();
         }
         return options;
      }

      public static bool EditorReleased => mapEditorRelease.Value;

      public static readonly Lazy<bool> mapEditorRelease;
      static IDataModelExtensions() {
         TimeZoneInfo centralZone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
         mapEditorRelease = new(() => {
            var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, centralZone);
            return now >= new DateTime(2022, 12, 25, 12, 0, 0);
         });
      }

      public static List<MapInfo> GetMatchingMaps(this IDataModel model, string text) {
         if (!mapEditorRelease.Value) return new();

         var allMaps = model.CurrentCacheScope?.GetAllMaps();
         var results = allMaps?.Where(map => map.Name.MatchesPartial(text) && map.Name.SkipCount(text) <= text.Length).ToList() ?? new();
         var lower = text.ToLower();
         foreach (var result in results) {
            if (result.Name.ToLower() == lower) return new() { result };
         }
         return results;
      }

      public static IList<DocLabel> GetMatchingDocumentation(this IReadOnlyList<DocLabel> availableDocs, string text) {
         return availableDocs.Where(doc => doc.Label.MatchesPartial(text)).ToList();
      }
   }

   public class GotoLabelSection : ViewModelCore {
      public const int MaxCategories = 49;
      public int width, height;
      public int Width { get => width; set => Set(ref width, value); }
      public int Height { get => height; set => Set(ref height, value); }
      public ObservableCollection<GotoToken> Tokens { get; }

      public event EventHandler GenerateLowerRow;
      public event EventHandler ClearLowerRows;

      public GotoLabelSection(IEnumerable<string> allOptions, IEnumerable<string> previousSectionSelections) {
         var prefix = string.Join(".", previousSectionSelections);
         if (prefix.Length > 0) prefix += ".";
         var thisLevel = new HashSet<string>();

         // display order is alphabetically sorted non-numeric entries, followed by insert-order numeric entries
         var noNumbers = new List<string>();
         var hasNumbers = new List<string>();
         foreach (var option in allOptions) {
            if (option.Any(char.IsNumber)) hasNumbers.Add(option);
            else noNumbers.Add(option);
         }
         foreach (var option in noNumbers.OrderBy(text => text).Concat(hasNumbers)) {
            if (option.StartsWith(prefix)) {
               thisLevel.Add(DotSplit(option.Substring(prefix.Length)));
            }
         }

         Tokens = GotoToken.Generate(thisLevel);
         var count = Tokens.Count;
         if (count > MaxCategories) {
            Tokens.Clear();
            Tokens.Add(new GotoToken { Content = $"({count} options)", IsSelectable = false });
         }
         Initialize();
      }

      public string DotSplit(string content) {
         var parts = content.Split(".");
         var firstPart = parts[0];
         for (int i = 1; i < parts.Length; i++) {
            if (parts[i].Length == 0 || parts[i][0] == ' ' || firstPart.Count('"') % 2 != 0) {
               // "Mr. Mime" and similar names with ". " should not be split.
               firstPart += "." + parts[i];
            } else if (firstPart.Count('(') > firstPart.Count(')')) {
               // () pairs should not be split
               firstPart += "." + parts[i];
            } else if (firstPart.Length == 1) {
               // signle letters should not be split
               firstPart += "." + parts[i];
            } else {
               break;
            }
         }
         return firstPart;
      }

      public GotoLabelSection(string prefix, IList<GotoToken> tokens) {
         tokens = tokens.OrderBy(t => t.Content).ToList();
         Tokens = new ObservableCollection<GotoToken>();
         foreach (var token in tokens) Tokens.Add(new GotoToken { Content = prefix + "." + token.Content, IsSelected = token.IsSelected, IsSelectable = token.IsSelectable });
         if (tokens.Count == 0) {
            Tokens.Add(new GotoToken { Content = prefix });
            if (prefix.EndsWith(" options)")) Tokens[0].IsSelectable = false;
         }
         Initialize();
      }

      public void Initialize() {
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

      public static GotoLabelSection Build(IDataModel model, string filter, IReadOnlyList<DocLabel> docs, IEnumerable<GotoLabelSection> previousSections, bool includeMatchingMaps) {
         using (ModelCacheScope.CreateScope(model)) {
            List<string> allOptions = model.GetExtendedAutocompleteOptions(filter)?.ToList() ?? new();
            allOptions.AddRange(model.GetMatchingMaps(filter).Select(map => map.Name));
            allOptions.AddRange(docs.GetMatchingDocumentation(filter).Select(doc => doc.Label));
            var selections = GetSectionSelections(previousSections).ToList();

            var newSection = new GotoLabelSection(allOptions, selections);
            if (newSection.Tokens.Count == 1) {
               newSection.Tokens[0].IsSelected = true;
               var child = Build(model, filter, docs, previousSections.Concat(new[] { newSection }), includeMatchingMaps); // recursion ftw
               // only do the concatenation if the children are not unreadable long
               if (child.Tokens.All(token => token.Content.Length < 20)) {
                  newSection = new GotoLabelSection(newSection.Tokens[0].Content, child.Tokens);
               } else {
                  newSection.Tokens[0].IsSelected = false;
               }
            }

            return newSection;
         }
      }
   }

   public class GotoToken : ViewModelCore {
      public bool isSelected;
      public bool IsSelected { get => isSelected; set => Set(ref isSelected, value); }

      public bool isSelectable = true;
      public bool IsSelectable { get => isSelectable; set => Set(ref isSelectable, value); }

      public string content;
      public string Content { get => content; set => Set(ref content, value); }

      public bool isGoto; // true if clicking this button causes a goto, false if it opens another section.
      public bool IsGoto { get => isGoto; set => Set(ref isGoto, value); }

      public ObservableCollection<object> hoverTip;
      public ObservableCollection<object> HoverTip {
         get => hoverTip;
         set { hoverTip = value; NotifyPropertyChanged(); }
      }

      public static ObservableCollection<GotoToken> Generate(IEnumerable<string> content) {
         var collection = new ObservableCollection<GotoToken>();
         foreach (var c in content) collection.Add(new GotoToken { Content = c });
         return collection;
      }
   }
}
