using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using HavenSoft.HexManiac.Core.ViewModels.Visitors;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using static HavenSoft.HexManiac.Core.ICommandExtensions;

namespace HavenSoft.HexManiac.Core.ViewModels {
   public class GotoShortcutViewModel : ViewModelCore {
      // one shared event argument: an animated button changes its picture several times a second, and that must not allocate
      private static readonly PropertyChangedEventArgs ImageChangedArgs = new(nameof(Image));

      private readonly GotoControlViewModel viewModel;
      private readonly IViewPort viewPort;
      private readonly string anchor;

      private IPixelViewModel image;
      private Func<IPixelViewModel> imageFactory;       // makes the picture the first time somebody asks for it
      private Func<IPixelViewModel> fallbackFactory;    // makes the built-in picture, only if the button has no picture of its own (see SetFallbackImage)
      private Func<ShortcutAnimation> animationSource;  // makes the animation the first time the clock starts
      private ShortcutAnimation animation;
      private int frameIndex;

      public string DisplayText { get; }

      /// <summary>
      /// The picture on the button. A button made with a factory draws its picture the first time it is shown, not when the Goto dialog is prepared.
      /// An animated button replaces this picture with the next frame, and tells the view with PropertyChanged.
      /// </summary>
      public IPixelViewModel Image {
         get {
            if (image == null) {
               BuildAnimation(); // an animated button starts on its first frame, so the button looks the same whether or not the clock is running yet
               if (image == null && imageFactory != null) {
                  var factory = imageFactory;
                  imageFactory = null;
                  try { image = factory(); } catch (Exception) { image = null; } // pictures are decoration: never fail the dialog because of one
               }
               if (image == null && fallbackFactory != null) {
                  var fallback = fallbackFactory;
                  fallbackFactory = null;
                  try { image = fallback(); } catch (Exception) { image = null; }
               }
            }
            return image;
         }
      }

      /// <summary>True if the button has an animation that the clock plays (a single picture is not an animation).</summary>
      public bool IsAnimated => animation != null;

      private bool smallMode;
      public bool SmallMode {
         get => smallMode;
         set => Set(ref smallMode, value);
      }

      private bool visible = true;
      public bool Visible { get => visible; set => Set(ref visible, value); }

      public GotoShortcutViewModel(GotoControlViewModel parent, IViewPort viewPort, IPixelViewModel image, string anchor, string display) {
         viewModel = parent;
         this.viewPort = viewPort;
         this.image = image;
         this.anchor = anchor;
         DisplayText = display;
      }

      /// <summary>Same, but the picture is only drawn when the button is first shown (null from the factory is a button without a picture).</summary>
      public GotoShortcutViewModel(GotoControlViewModel parent, IViewPort viewPort, Func<IPixelViewModel> imageFactory, string anchor, string display)
         : this(parent, viewPort, (IPixelViewModel)null, anchor, display) {
         this.imageFactory = imageFactory;
      }

      /// <summary>
      /// The built-in picture for a button that has no picture of its own: the factory is only asked (once, when the button is first shown) if neither the picture the button was made with,
      /// nor <see cref="PreferImage"/>, nor an animation gave it one. A picture from the ROM therefore always wins, and a button that has one never pays for the fallback.
      /// Use it before the dialog shows the button.
      /// </summary>
      public void SetFallbackImage(Func<IPixelViewModel> factory) => fallbackFactory = factory;

      /// <summary>
      /// Use this picture instead of the one the button has, unless the factory has none (then the button keeps its old picture).
      /// The factory runs when the button is first shown. Use it before the dialog shows the button.
      /// </summary>
      public void PreferImage(Func<IPixelViewModel> factory) {
         if (factory == null) return;
         var fallbackImage = image;
         var fallbackFactory = imageFactory;
         image = null;
         imageFactory = () => {
            IPixelViewModel preferred = null;
            try { preferred = factory(); } catch (Exception) { preferred = null; } // a picture that can't be drawn is not a reason to lose the old one
            return preferred ?? fallbackImage ?? fallbackFactory?.Invoke();
         };
      }

      /// <summary>
      /// Make the button animated: show 'frames' one after the other, each for the matching number of milliseconds, and start again after the last.
      /// The shared clock (<see cref="ShortcutAnimationClock"/>) plays it while the Goto dialog is shown. One frame, no frames, or times that don't match the frames leave the button without an animation.
      /// </summary>
      public void SetAnimation(IReadOnlyList<IPixelViewModel> frames, IReadOnlyList<int> durationsMilliseconds) => SetAnimation(ShortcutAnimation.Create(frames, durationsMilliseconds));

      public void SetAnimation(ShortcutAnimation newAnimation) {
         animationSource = null;
         animation = newAnimation != null && newAnimation.FrameCount > 1 ? newAnimation : null;
         if (newAnimation != null) ShowFrame(newAnimation, 0);
         viewModel?.RaiseShortcutAnimationsChanged();
      }

      /// <summary>
      /// The animation is built the first time the clock starts (the first time the dialog is shown), not when the dialog is prepared for the tab.
      /// A source that returns null leaves the button as it is.
      /// </summary>
      public void SetAnimationSource(Func<ShortcutAnimation> source) {
         animationSource = source;
         viewModel?.RaiseShortcutAnimationsChanged();
      }

      /// <summary>Builds the animation if it was only promised (see <see cref="SetAnimationSource"/>). Returns true if the button is animated.</summary>
      public bool PrepareAnimation() {
         var before = image;
         BuildAnimation();
         if (!ReferenceEquals(before, image)) NotifyPropertyChanged(ImageChangedArgs);
         return animation != null;
      }

      private void BuildAnimation() {
         var source = animationSource;
         if (source == null) return;
         animationSource = null;
         ShortcutAnimation built = null;
         try { built = source(); } catch (Exception) { built = null; } // animations are decoration too
         if (built == null) return;
         animation = built.FrameCount > 1 ? built : null;
         frameIndex = 0;
         image = built.Frames[0];
         imageFactory = null;
      }

      /// <summary>
      /// Show the frame that belongs 'elapsedMilliseconds' after the animation started, and say how long until the frame changes again (long.MaxValue if the button isn't animated).
      /// Only changes the picture (and tells the view) when the frame really is a different one.
      /// </summary>
      public void ShowFrameAt(long elapsedMilliseconds, out long millisecondsUntilNextFrame) {
         var current = animation;
         if (current == null) {
            millisecondsUntilNextFrame = long.MaxValue;
            return;
         }
         var index = current.FrameIndexAt(elapsedMilliseconds, out millisecondsUntilNextFrame);
         if (index != frameIndex || image != current.Frames[index]) ShowFrame(current, index);
      }

      private void ShowFrame(ShortcutAnimation source, int index) {
         frameIndex = index;
         image = source.Frames[index];
         imageFactory = null;
         NotifyPropertyChanged(ImageChangedArgs);
      }

      public void Goto() {
         viewModel.ControlVisible = false;
         viewModel.ShowAll = false;
         viewPort.Goto.Execute(anchor);
      }
   }

   public class GotoControlViewModel : ViewModelCore {
      private readonly IEditableViewPort viewPort;
      private IReadOnlyList<DocLabel> availableDocs;
      private bool withinTextChange = false, devMode = false;

      #region NotifyProperties

      private bool controlVisible;
      public bool ControlVisible {
         get => controlVisible;
         set {
            if (TryUpdate(ref controlVisible, value) && value) {
               CompletionIndex = -1;
               if (CubeScripts.IsAvailable(viewPort?.Model)) RefreshOptions(); // entries like Scripts > EXP Share depend on a byte of the ROM (it may have changed, or been undone)
            }
            if (value) MoveFocusToGoto?.Invoke(this, EventArgs.Empty);
         }
      }

      private bool loading = true;
      public bool Loading { get => loading; set => Set(ref loading, value); }

      private string text = string.Empty;
      public string Text {
         get => text;
         set {
            if (viewPort?.Model == null) return;
            if (TryUpdate(ref text, value)) {
               ShowAll = true;
               withinTextChange = true;
               using (new StubDisposable { Dispose = () => withinTextChange = false }) {
                  RefreshOptions();
               }
            }
         }
      }
      public void RefreshOptions() {
         UpdatePrefixSelectionsAfterTextChange();
      }

      private int completionIndex = -1;
      public int CompletionIndex {
         get => completionIndex;
         set {
            if (value < -1) value = autoCompleteOptions.Count - 1;
            if (value >= autoCompleteOptions.Count) value = -1;
            Set(ref completionIndex, value);
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

      private bool showAll;
      public bool ShowAll { get => showAll; set => Set(ref showAll, value, oldValue => {
         if (showAll) MoveFocusToGoto?.Invoke(this, EventArgs.Empty);
         UpdateShortcutSize();
      }); }

      private void UpdateShortcutSize() {
         foreach (var shortcut in shortcuts) shortcut.SmallMode = false;
      }

      private bool allowToggleShowAll = true;
      public bool AllowToggleShowAll { get => allowToggleShowAll; set => Set(ref allowToggleShowAll, value); }

      #endregion

      #region Commands

      public ICommand MoveAutoCompleteSelectionUp { get; }
      public ICommand MoveAutoCompleteSelectionDown { get; }
      public ICommand Goto { get; }
      public ICommand ShowGoto { get; }                        // arg -> true to show, false to hide

      #endregion

      public event EventHandler MoveFocusToGoto;

      /// <summary>Raised when one of the shortcut buttons gets (or is promised) an animation, so the clock that plays them can start.</summary>
      public event EventHandler ShortcutAnimationsChanged;
      internal void RaiseShortcutAnimationsChanged() => ShortcutAnimationsChanged?.Invoke(this, EventArgs.Empty);

      private ObservableCollection<GotoShortcutViewModel> shortcuts = new();
      public ObservableCollection<GotoShortcutViewModel> Shortcuts {
         get => shortcuts;
         set {
            shortcuts = value;
            NotifyPropertyChanged();
            UpdateShortcutSize();
         }
      }

      public ObservableCollection<GotoLabelSection> PrefixSelections { get; }

      public GotoControlViewModel(ITabContent tabContent, IWorkDispatcher dispatcher, IReadOnlyList<DocLabel> docs, bool devMode) {
         availableDocs = docs;
         viewPort = (tabContent as IEditableViewPort);
         this.devMode = devMode;
         if (tabContent is MapEditorViewModel mevm) viewPort = mevm.ViewPort;

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
               viewPort.Model.InitializationWorkload.ContinueWith(task => dispatcher.DispatchWork(() => {
                  var text = Text;
                  var index = completionIndex.LimitToRange(-1, AutoCompleteOptions.Count - 1);
                  if (index != -1) text = AutoCompleteOptions[index].CompletionText;
                  if (arg is string) text = (string)arg;
                  if (viewPort is ViewPort editableViewport && text.StartsWith("@") && text.Contains(" ") && (text.Contains("^") || text.Contains("!"))) {
                     // user just put a paste-script into the goto field.
                     // this starts with a meta-input and then contains an anchor definition or another meta command.
                     // Since this is clearly not meant to be a goto, just do it as an Edit instead.
                     editableViewport.Edit(text + " ");
                  } else {
                     viewPort?.Goto?.Execute(text);
                  }
                  ControlVisible = false;
                  ShowAutoCompleteOptions = false;
               }), TaskContinuationOptions.ExecuteSynchronously);
            },
         };
         ShowGoto = new StubCommand {
            CanExecute = arg => viewPort?.Goto != null && (arg is bool || arg is null),
            Execute = arg => {
               AllowToggleShowAll = false;
               ControlVisible = (bool)(arg ?? !ControlVisible);
            },
         };
         PrefixSelections = new ObservableCollection<GotoLabelSection>();
         UpdatePrefixSelectionsAfterTextChange();
      }

      public void UpdateDocs(IList<DocLabel> docs) {
         availableDocs = availableDocs.Concat(docs).ToList();
      }

      private void UpdatePrefixSelectionsAfterTextChange() {
         var previousSelections = GotoLabelSection.GetSectionSelections(PrefixSelections).ToArray();
         PrefixSelections.Clear();
         if (viewPort == null || viewPort.Model == null) return;
         var section = GotoLabelSection.Build(viewPort.Model, Text, availableDocs, PrefixSelections, viewPort is ViewPort vp && vp.HasValidMapper);
         PrefixSelections.Add(AddListeners(section));
         for (int i = 0; i < previousSelections.Length; i++) {
            if (PrefixSelections.Count <= i) break;
            var matchingToken = PrefixSelections[i].Tokens.FirstOrDefault(token => token.Content == previousSelections[i]);
            if (matchingToken == null) break;
            matchingToken.IsSelected = true;
         }
         UpdateTooltips();
         foreach (var sc in shortcuts) sc.Visible = PrefixSelections.Count == 1;
      }

      private void UpdatePrefixSelectionsAfterSelectionMade() {
         var currentSelection = string.Join(".", GotoLabelSection.GetSectionSelections(PrefixSelections));
         var address = Pointer.NULL;
         var matchedWords = viewPort.Model.GetMatchedWords(currentSelection);
         ShowAll = true;
         if (matchedWords.Count > 0) {
            viewPort?.Goto?.Execute(currentSelection);
            ControlVisible = false;
            ShowAutoCompleteOptions = false;
            DeselectLastRow();
            return;
         }

         var matchedMaps = viewPort.Model.GetMatchingMaps(currentSelection);
         if (matchedMaps.Count == 1 || matchedMaps.Any(map => map.Name == currentSelection)) {
            // if we can add a new section, then don't go to a map
            var newSection = GotoLabelSection.Build(viewPort.Model, Text, availableDocs, PrefixSelections, viewPort is ViewPort vp1 && vp1.HasValidMapper);
            if (newSection.Tokens.Count == 0) {
               viewPort?.Goto?.Execute(currentSelection);
               ControlVisible = false;
               ShowAutoCompleteOptions = false;
               DeselectLastRow();
               return;
            }
         }

         var matchingDoc = availableDocs.FirstOrDefault(doc => doc.Label == currentSelection);
         if (matchingDoc != null && viewPort is ViewPort vp) {
            var newSection = GotoLabelSection.Build(viewPort.Model, Text, availableDocs, PrefixSelections, vp.HasValidMapper);
            if (newSection.Tokens.Count == 0) {
               OpenLink(matchingDoc.Url);
               ControlVisible = false;
               ShowAutoCompleteOptions = false;
               DeselectLastRow();
               return;
            }
         }

         using (ModelCacheScope.CreateScope(viewPort.Model)) {
            address = viewPort.Model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, currentSelection);
            if (address == Pointer.NULL) address = GotoAliases.FindAddress(viewPort.Model, currentSelection); // an original entry that this ROM keeps somewhere else
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
            var newSection = GotoLabelSection.Build(viewPort.Model, Text, availableDocs, PrefixSelections, viewPort is ViewPort vp2 && vp2.HasValidMapper);
            PrefixSelections.Add(AddListeners(newSection));
            foreach (var sc in shortcuts) sc.Visible = PrefixSelections.Count == 1;
         }
         UpdateTooltips();
      }

      private void OpenLink(string link) => NativeProcess.Start(link);

      private void UpdateTooltips() {
         using (ModelCacheScope.CreateScope(viewPort.Model)) {
            foreach (var prefix in PrefixSelections) {
               var currentSelection = string.Join(".", GotoLabelSection.GetSectionSelections(PrefixSelections.Until(section => section == prefix)));
               foreach (var token in prefix.Tokens) {
                  var fullName = token.Content;
                  if (!string.IsNullOrEmpty(currentSelection)) fullName = currentSelection + "." + token.Content;
                  token.UpdateHoverTip(viewPort, availableDocs, fullName);
               }
            }
         }
      }

      private void DeselectLastRow() {
         ShowAll = true;
         foreach (var token in PrefixSelections.Last().Tokens) token.IsSelected = false;
      }

      private GotoLabelSection AddListeners(GotoLabelSection section) {
         section.ClearLowerRows += (sender, e) => {
            var index = PrefixSelections.IndexOf(sender);
            while (index >= 0 && PrefixSelections.Count > index + 1) PrefixSelections.RemoveAt(PrefixSelections.Count - 1);
            foreach (var sc in shortcuts) sc.Visible = PrefixSelections.Count == 1;
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

      private static readonly Lazy<bool> mapEditorRelease;
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
      private const int MaxCategories = 49;
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

      private string DotSplit(string content) {
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

      public static GotoLabelSection Build(IDataModel model, string filter, IReadOnlyList<DocLabel> docs, IEnumerable<GotoLabelSection> previousSections, bool includeMatchingMaps) {
         using (ModelCacheScope.CreateScope(model)) {
            List<string> allOptions = model.GetExtendedAutocompleteOptions(filter)?.ToList() ?? new();
            allOptions.AddRange(model.GetMatchingMaps(filter).Select(map => map.Name));
            allOptions.AddRange(docs.GetMatchingDocumentation(filter).Select(doc => doc.Label));
            allOptions.AddRange(GotoAliases.Matching(model, filter).Except(allOptions)); // the original entries, for a ROM (CUBE) that merged their tables into others
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
      private bool isSelected;
      public bool IsSelected { get => isSelected; set => Set(ref isSelected, value); }

      private bool isSelectable = true;
      public bool IsSelectable { get => isSelectable; set => Set(ref isSelectable, value); }

      private string content;
      public string Content { get => content; set => Set(ref content, value); }

      private bool isGoto; // true if clicking this button causes a goto, false if it opens another section.
      public bool IsGoto { get => isGoto; set => Set(ref isGoto, value); }

      private ObservableCollection<object> hoverTip;
      public ObservableCollection<object> HoverTip {
         get => hoverTip;
         set { hoverTip = value; NotifyPropertyChanged(); }
      }

      public static ObservableCollection<GotoToken> Generate(IEnumerable<string> content) {
         var collection = new ObservableCollection<GotoToken>();
         foreach (var c in content) collection.Add(new GotoToken { Content = c });
         return collection;
      }

      public void UpdateHoverTip(IEditableViewPort viewPort, IReadOnlyList<DocLabel> docs, string fullName) {
         var model = viewPort.Model;
         var matchingMaps = new List<MapInfo>();
         if (fullName.StartsWith("maps.bank") && fullName.Contains("-")) matchingMaps = model.GetMatchingMaps(fullName);
         var address = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, fullName);
         if (address == Pointer.NULL) address = GotoAliases.FindAddress(model, fullName);
         var matchingDoc = docs.FirstOrDefault(doc => doc.Label == fullName);
         if (address != Pointer.NULL) {
            IsGoto = true;
            var run = model.GetNextRun(address);
            if (run != null && address == run.Start) {
               var hoverContent = ToolTipContentVisitor.BuildContentForRun(model, -1, address, run);
               if (hoverContent != null) {
                  HoverTip = new ObservableCollection<object> { hoverContent };
                  return;
               }
            }
         } else if (matchingMaps.Count == 1 && viewPort.MapEditor != null) {
            IsGoto = true;
            var info = matchingMaps[0];
            var hoverContent = viewPort.MapEditor.GetMapPreview(info.Group, info.Map, null);
            if (hoverContent != null) {
               var scale = 1.0;
               if (hoverContent.PixelWidth >= 240 * 4 || hoverContent.PixelHeight >= 160 * 4) {
                  scale = .25;
               } else if (hoverContent.PixelWidth >= 240 * 2 || hoverContent.PixelHeight >= 160 * 2) {
                  scale = .5;
               }
               if (scale < 1) {
                  hoverContent = new ReadonlyPixelViewModel(hoverContent.PixelWidth, hoverContent.PixelHeight, hoverContent.PixelData, hoverContent.Transparent) {
                     SpriteScale = scale
                  };
               }
               HoverTip = new ObservableCollection<object> { hoverContent };
               return;
            }
         } else if (matchingDoc != null) {
            IsGoto = true;
            HoverTip = new ObservableCollection<object> { "Web Link" };
         } else if (model.GetMatchedWords(fullName).Count > 0) {
            IsGoto = true;
         } else {
            IsGoto = false;
         }

         HoverTip = null;
      }
   }
}
