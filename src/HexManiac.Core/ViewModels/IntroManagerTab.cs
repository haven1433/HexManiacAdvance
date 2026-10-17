using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels {
   /// <summary>One choice of a list: a Pokémon, a song, a picture. 'Available' is false for the ones the game's code can't store (the list shows them greyed out).</summary>
   public class IntroOption : ViewModelCore {
      public int Value { get; }
      public string Text { get; }
      public bool Available { get; }
      /// <summary>Why the choice can't be used (a tooltip), or empty.</summary>
      public string Reason { get; }
      /// <summary>What was typed in the search box is matched with this: the name and the number.</summary>
      internal string SearchText { get; }

      public IntroOption(int value, string text, bool available = true, string reason = "") {
         (Value, Text, Available, Reason) = (value, text, available, reason ?? string.Empty);
         SearchText = (text + " " + value).ToLowerInvariant();
      }

      public override string ToString() => Text;
   }

   /// <summary>
   /// A long list with a search box: the box shows only the options that contain what was typed (in their name or number). The chosen option always stays in the list, so the drop down never loses its selection while typing.
   /// Choosing by the person raises <see cref="UserSelected"/>; choosing by the program (<see cref="Select"/>) doesn't.
   /// </summary>
   public class IntroOptionList : ViewModelCore {
      private readonly List<IntroOption> all = new();

      public IReadOnlyList<IntroOption> All => all;

      /// <summary>The options that match the search, in order.</summary>
      public ObservableCollection<IntroOption> Shown { get; } = new();

      public event EventHandler<IntroOption> UserSelected;

      private string search = string.Empty;
      public string Search {
         get => search;
         set { if (TryUpdate(ref search, value ?? string.Empty)) Filter(); }
      }

      private IntroOption selected;
      /// <summary>The chosen option. The view sets it when a person picks one; it never takes back to nothing (a drop down that is rebuilt reports nothing).</summary>
      public IntroOption Selected {
         get => selected;
         set {
            if (value == null || ReferenceEquals(value, selected)) return;
            selected = value;
            NotifyPropertyChanged();
            UserSelected?.Invoke(this, value);
         }
      }

      public int Count => all.Count;

      public void SetOptions(IEnumerable<IntroOption> options) {
         all.Clear();
         all.AddRange(options.OrderBy(option => option.Value));
         Filter();
      }

      public IntroOption Find(int value) => all.FirstOrDefault(option => option.Value == value);

      /// <summary>Chooses the option of a value without telling anyone. A value that is not in the list gets an option of its own ("Number 374") so that what the game uses is always shown.</summary>
      public IntroOption Select(int value, Func<int, string> unnamed = null) {
         var option = Find(value);
         if (option == null && value >= 0) {
            option = new IntroOption(value, unnamed?.Invoke(value) ?? $"Number {value}");
            all.Add(option);
            all.Sort((a, b) => a.Value.CompareTo(b.Value));
         }
         selected = option;
         Filter();
         NotifyPropertyChanged(nameof(Selected));
         return option;
      }

      /// <summary>Keeps the options that contain every word of the search (and the chosen one).</summary>
      public void Filter() {
         var words = search.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
         var wanted = all.Where(option => ReferenceEquals(option, selected) || words.All(word => option.SearchText.Contains(word))).ToList();
         Synchronize(Shown, wanted);
         NotifyPropertyChanged(nameof(MatchCount));
         NotifyPropertyChanged(nameof(MatchText));
      }

      public int MatchCount => Shown.Count;

      public string MatchText => search.Length == 0 ? $"{all.Count} choices" : $"{Math.Max(0, Shown.Count - (selected != null && !Matches(selected) ? 1 : 0))} of {all.Count} match";

      private bool Matches(IntroOption option) {
         var words = search.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
         return words.All(word => option.SearchText.Contains(word));
      }

      /// <summary>Makes the collection equal to the wanted list with as few changes as possible (an item that stays is never removed, so a view that shows it keeps it selected).</summary>
      public static void Synchronize<T>(ObservableCollection<T> target, IReadOnlyList<T> wanted) where T : class {
         var keep = new HashSet<T>(wanted);
         for (int i = target.Count - 1; i >= 0; i--) if (!keep.Contains(target[i])) target.RemoveAt(i);
         for (int i = 0; i < wanted.Count; i++) {
            if (i < target.Count && ReferenceEquals(target[i], wanted[i])) continue;
            var existing = target.IndexOf(wanted[i]);
            if (existing >= 0) target.Move(existing, i);
            else target.Insert(i, wanted[i]);
         }
      }
   }

   /// <summary>One setting of the intro, as the tab shows it: the Pokémon of the speech, a song... with its list, its number box, a picture for Pokémon, and what to do when it can't be changed.</summary>
   public class IntroSettingEditor : ViewModelCore {
      private readonly IntroManagerTab tab;

      public IntroSetting Setting { get; }
      public string Title => Setting.Title;
      public string Description => Setting.Description;
      public bool IsSpecies => Setting.Kind == IntroValueKind.Species;
      public IntroOptionList Options { get; } = new();

      /// <summary>The number from when the tab was opened: what 'Back to the original' writes.</summary>
      public int OriginalValue { get; private set; } = -1;

      public PixelPreview Preview { get; } = new PixelPreview(null, 2);
      public bool HasPreview => IsSpecies && Preview.HasImage;

      private int currentValue = -1;
      /// <summary>The number the ROM has now (-1 if the places don't hold a number).</summary>
      public int Value => currentValue;

      private string status = string.Empty;
      public string Status { get => status; private set { if (TryUpdate(ref status, value)) NotifyPropertyChanged(nameof(HasStatus)); } }
      public bool HasStatus => status.Length > 0;

      private bool statusIsError;
      public bool StatusIsError { get => statusIsError; private set => Set(ref statusIsError, value); }

      /// <summary>"Highest number: 255" and where the number is stored, for the small print under the box.</summary>
      public string Details => $"The game's code can store 0 to {Setting.MaxValue} here. Stored at {string.Join(", ", Setting.Addresses.Select(address => address.ToString("X6")))}.";

      public string NumberLabel => IsSpecies ? "Pokémon number" : "Song number";

      private string numberText = string.Empty;
      /// <summary>The number as text for the number box. Setting it (the box lost focus, or Enter) changes the ROM if the number is allowed.</summary>
      public string NumberText {
         get => numberText;
         set {
            value = (value ?? string.Empty).Trim();
            if (value == numberText) return;
            if (!int.TryParse(value, out var parsed) || parsed < 0) {
               tab.ReportError($"'{value}' is not a number: type a whole number from 0 to {Setting.MaxValue}.");
               NotifyPropertyChanged();
               return;
            }
            Apply(parsed);
         }
      }

      public ICommand Reset { get; }
      public ICommand ShowInMainTab { get; }

      public IntroSettingEditor(IntroManagerTab tab, IntroSetting setting, IReadOnlyList<string> names) {
         this.tab = tab;
         Setting = setting;
         var options = new List<IntroOption>();
         if (names != null) {
            for (int i = IsSpecies ? 1 : 0; i < names.Count; i++) {
               var name = CleanName(names[i]);
               if (name.Length == 0) continue;
               var available = setting.CanHold(i, out var reason);
               options.Add(new IntroOption(i, $"{name} ({i})", available, available ? string.Empty : reason));
            }
         }
         Options.SetOptions(options);
         Options.UserSelected += (sender, option) => Apply(option.Value);
         Reset = new StubCommand { CanExecute = arg => OriginalValue >= 0 && currentValue != OriginalValue, Execute = arg => Apply(OriginalValue) };
         ShowInMainTab = new StubCommand { CanExecute = arg => true, Execute = arg => tab.ShowInMainTab(Setting.Addresses.First()) };
      }

      /// <summary>A name as the lists store it ("NIDORAN\sf"), written the way people read it ("NIDORAN♀").</summary>
      public static string CleanName(string name) {
         return (name ?? string.Empty).Trim('"').Replace("\\sf", "♀").Replace("\\sm", "♂").Trim();
      }

      public void Capture(byte[] data) {
         OriginalValue = Setting.Read(data, out _);
         Reload(data);
      }

      /// <summary>Reads the number from the ROM again and shows it.</summary>
      public void Reload(byte[] data) {
         currentValue = Setting.Read(data, out var consistent);
         var option = currentValue >= 0 ? Options.Select(currentValue, number => $"{(IsSpecies ? "Pokémon" : "Song")} {number}") : null;
         numberText = currentValue >= 0 ? currentValue.ToString() : string.Empty;
         NotifyPropertyChanged(nameof(NumberText));
         NotifyPropertyChanged(nameof(Value));
         if (!consistent) {
            Status = $"The game's code holds different numbers here ({string.Join(", ", Setting.ReadAll(data).Distinct())}). Choose one to make them all the same.";
            StatusIsError = true;
         } else if (Setting.IsShared) {
            Status = "Other code of the game uses this same number too, so changing it also changes things outside the intro.";
            StatusIsError = true;
         } else if (option != null && !option.Available) {
            Status = option.Reason;
            StatusIsError = true;
         } else {
            Status = currentValue != OriginalValue && OriginalValue >= 0 ? $"Changed. The original was {Describe(OriginalValue)}." : string.Empty;
            StatusIsError = false;
         }
         (Reset as StubCommand)?.RaiseCanExecuteChanged();
         if (IsSpecies) {
            Preview.Replace(currentValue >= 0 ? tab.DrawPokemon(currentValue) : null);
            NotifyPropertyChanged(nameof(HasPreview));
         }
      }

      private string Describe(int number) => Options.Find(number)?.Text ?? number.ToString();

      private void Apply(int number) {
         if (!Setting.CanHold(number, out var reason)) {
            tab.ReportError(reason);
            Reload(tab.Model.RawData);
            return;
         }
         tab.WriteSetting(this, number);
      }
   }

   /// <summary>The professor's picture: what it is now, a list of pictures that could replace it (any Pokémon's front picture or any trainer's), a preview of the choice, and the buttons that apply it or go back.</summary>
   public class IntroPictureEditor : ViewModelCore {
      private readonly IntroManagerTab tab;
      private int[] originalTargets;
      private IntroImage candidate;

      public IntroPicture Picture { get; }
      public string Title => $"{Picture.Title}'s picture";

      public string Description => Picture.Width == 64 && Picture.Height == 64
         ? "The picture of the professor in the speech. Choose any Pokémon's or trainer's picture to show instead. The new picture is copied into free space of the ROM and the game is pointed at it."
         : "The picture of the professor in the speech. Choose any Pokémon's or trainer's picture to show instead; it is placed at the bottom of the professor's picture. The new picture is copied into free space of the ROM and the game is pointed at it.";

      public PixelPreview Current { get; } = new PixelPreview(null, 3);
      public PixelPreview Candidate { get; } = new PixelPreview(null, 3);
      public bool HasCandidate => Candidate.HasImage;

      public IntroOptionList Pokemon { get; } = new();
      public IntroOptionList Trainers { get; } = new();
      private readonly Dictionary<int, IntroPictureChoice> pokemonChoices = new(), trainerChoices = new();

      public bool HasPokemon => pokemonChoices.Count > 0;
      public bool HasTrainers => trainerChoices.Count > 0;

      private bool usePokemon = true;
      /// <summary>Choose from the Pokémon pictures (true) or from the trainer pictures.</summary>
      public bool UsePokemon {
         get => usePokemon;
         set { if (!TryUpdate(ref usePokemon, value)) return; NotifyPropertyChanged(nameof(UseTrainer)); NotifyPropertyChanged(nameof(Active)); ShowCandidate(); }
      }
      public bool UseTrainer {
         get => !usePokemon;
         set => UsePokemon = !value;
      }

      /// <summary>The list that is shown now.</summary>
      public IntroOptionList Active => usePokemon ? Pokemon : Trainers;

      private string status = string.Empty;
      public string Status { get => status; private set { if (TryUpdate(ref status, value)) NotifyPropertyChanged(nameof(HasStatus)); } }
      public bool HasStatus => status.Length > 0;
      private bool statusIsError;
      public bool StatusIsError { get => statusIsError; private set => Set(ref statusIsError, value); }

      public string Details => $"Stored by the pointers at {string.Join(" and ", Picture.Addresses.Select(address => address.ToString("X6")))}.";

      public ICommand Apply { get; }
      public ICommand Restore { get; }
      public ICommand ShowInMainTab { get; }

      public IntroPictureEditor(IntroManagerTab tab, IntroPicture picture, IDataModel model) {
         this.tab = tab;
         Picture = picture;
         var pokemon = IntroPictureSources.Pokemon(model);
         var trainers = IntroPictureSources.Trainers(model);
         foreach (var choice in pokemon) pokemonChoices[choice.Index] = choice;
         foreach (var choice in trainers) trainerChoices[choice.Index] = choice;
         Pokemon.SetOptions(pokemon.Select(choice => new IntroOption(choice.Index, $"{choice.Name} ({choice.Index})")));
         Trainers.SetOptions(trainers.Select(choice => new IntroOption(choice.Index, $"{choice.Name} ({choice.Index})")));
         Pokemon.UserSelected += (sender, option) => ShowCandidate();
         Trainers.UserSelected += (sender, option) => ShowCandidate();
         Apply = new StubCommand { CanExecute = arg => candidate != null, Execute = arg => tab.ApplyPicture(this, candidate) };
         Restore = new StubCommand { CanExecute = arg => originalTargets != null && !CurrentTargets().SequenceEqual(originalTargets), Execute = arg => tab.RestorePicture(this, originalTargets) };
         ShowInMainTab = new StubCommand { CanExecute = arg => true, Execute = arg => tab.ShowInMainTab(Picture.Addresses.First()) };
      }

      private int[] CurrentTargets() => Picture.CurrentTargets(tab.Model.RawData);

      public IntroImage Original { get; private set; }

      public void Capture(byte[] data) {
         originalTargets = Picture.CurrentTargets(data);
         Reload(data);
      }

      public void Reload(byte[] data) {
         var image = Picture.Read(data);
         Current.Replace(image == null ? null : CharacterSprites.Draw(image.Pixels(), image.DisplayPalette()));
         if (image == null) { Status = "The picture in the ROM could not be read: the game's data here is not what the Intro Manager expects."; StatusIsError = true; }
         else if (originalTargets != null && !CurrentTargets().SequenceEqual(originalTargets)) { Status = "Changed. 'Back to the original' points the game at the first picture again."; StatusIsError = false; }
         else { Status = string.Empty; StatusIsError = false; }
         (Restore as StubCommand)?.RaiseCanExecuteChanged();
         (Apply as StubCommand)?.RaiseCanExecuteChanged();
      }

      /// <summary>Loads the picture that is chosen in the shown list and draws it the way it will look in the game.</summary>
      public void ShowCandidate() {
         var option = Active.Selected;
         candidate = null;
         if (option != null && (usePokemon ? pokemonChoices : trainerChoices).TryGetValue(option.Value, out var choice)) candidate = choice.Load();
         if (option != null && candidate == null) {
            Status = $"{option.Text} has no picture that can be used (it could not be read).";
            StatusIsError = true;
         } else if (candidate != null && !IntroPicture.CanUse(candidate, out var reason)) {
            Status = reason;
            StatusIsError = true;
            candidate = null;
         } else if (HasStatus && StatusIsError) {
            Status = string.Empty;
            StatusIsError = false;
         }
         var shown = candidate == null ? null : Picture.Compose(candidate);
         Candidate.Replace(shown == null ? null : CharacterSprites.Draw(shown.Pixels(), shown.DisplayPalette()));
         NotifyPropertyChanged(nameof(HasCandidate));
         (Apply as StubCommand)?.RaiseCanExecuteChanged();
      }
   }

   /// <summary>
   /// The 'New game spawn' card: the map, X and Y where the player appears after the speech, and whether the truck shakes. It edits gCubeNewGameSpawn of CUBE
   /// (<see cref="CubeNewGameSpawnTable"/>); a ROM without that table gets a disabled card with a message.
   /// </summary>
   public class IntroSpawnEditor : ViewModelCore {
      private readonly IntroManagerTab tab;

      public string Title => "New game spawn";
      public string Description => "Where the player appears after the Birch speech. The default is the moving truck.";
      public const string NeedsCubeMessage = "This needs the latest CUBE: this ROM has no new game spawn table, so there is nothing to change here.";

      public IntroOptionList Maps { get; } = new();

      private bool isAvailable;
      public bool IsAvailable { get => isAvailable; private set { if (TryUpdate(ref isAvailable, value)) NotifyPropertyChanged(nameof(IsUnavailable)); } }
      public bool IsUnavailable => !isAvailable;
      public string UnavailableMessage => NeedsCubeMessage;

      private CubeNewGameSpawn current = CubeNewGameSpawn.Original;
      public CubeNewGameSpawn Current => current;

      private string xText = "-1", yText = "-1";
      public string XText { get => xText; set => SetCoordinate(ref xText, value, "X", nameof(XText), (spawn, number) => spawn with { X = number }); }
      public string YText { get => yText; set => SetCoordinate(ref yText, value, "Y", nameof(YText), (spawn, number) => spawn with { Y = number }); }

      public bool ShakeTruck => current.ShakeTruck;
      public string ShakeLabel => current.ShakeTruck ? "The truck shakes" : "The truck does not shake";
      public string ToggleShakeText => current.ShakeTruck ? "Remove truck shaking" : "Restore truck shaking";

      private string status = string.Empty;
      public string Status { get => status; private set { if (TryUpdate(ref status, value)) NotifyPropertyChanged(nameof(HasStatus)); } }
      public bool HasStatus => status.Length > 0;
      private bool statusIsError;
      public bool StatusIsError { get => statusIsError; private set => Set(ref statusIsError, value); }

      public string Details => CubeNewGameSpawnTable.Find(tab.Model) is var address && address != -1
         ? $"X and Y are tile coordinates (-32768 to 32767); -1 and -1 is the default of the truck. Stored at {address:X6}."
         : string.Empty;

      public ICommand Reset { get; }
      public ICommand ToggleShake { get; }
      public ICommand ShowInMainTab { get; }

      public IntroSpawnEditor(IntroManagerTab tab) {
         this.tab = tab;
         Maps.SetOptions(BuildMapOptions(tab.Model));
         Maps.UserSelected += (sender, option) => Change(spawn => spawn with { MapGroup = option.Value / 256, MapNum = option.Value % 256 });
         Reset = new StubCommand { CanExecute = arg => isAvailable && current != CubeNewGameSpawn.Original, Execute = arg => Change(spawn => CubeNewGameSpawn.Original) };
         ToggleShake = new StubCommand { CanExecute = arg => isAvailable, Execute = arg => Change(spawn => spawn with { ShakeTruck = !spawn.ShakeTruck }) };
         ShowInMainTab = new StubCommand { CanExecute = arg => isAvailable, Execute = arg => tab.ShowInMainTab(CubeNewGameSpawnTable.Find(tab.Model)) };
         Reload();
      }

      /// <summary>The value of a map in the list: group * 256 + map number.</summary>
      public static int MapValue(int group, int map) => group * 256 + map;

      /// <summary>Every map of the ROM as "Littleroot Town (0.9)". An empty list (a ROM whose map tables can't be read) is fine: the chosen map is then shown as "Map 25.40".</summary>
      public static List<IntroOption> BuildMapOptions(IDataModel model) {
         var options = new List<IntroOption>();
         try {
            var lookup = new HavenSoft.HexManiac.Core.ViewModels.Map.MapNameLookup(model);
            var banks = HavenSoft.HexManiac.Core.Models.Map.AllMapsModel.Create(model);
            for (int group = 0; group < banks.Count && group < 256; group++) {
               var bank = banks[group];
               if (bank == null) continue;
               for (int map = 0; map < bank.Count && map < 256; map++) {
                  var full = lookup.Text(group, map);
                  var suffix = $".{group}-{map}";
                  var name = full.EndsWith(suffix) ? full.Substring(0, full.Length - suffix.Length) : string.Empty;
                  if (name.Length == 0 || full == $"{group}-{map}") name = "Map";
                  options.Add(new IntroOption(MapValue(group, map), $"{name} ({group}.{map})"));
               }
            }
         } catch (Exception) {
            // no readable map tables: the list stays as far as it got
         }
         return options;
      }

      /// <summary>Reads the table again and shows it.</summary>
      public void Reload() {
         var spawn = CubeNewGameSpawnTable.Read(tab.Model);
         IsAvailable = spawn != null;
         current = spawn ?? CubeNewGameSpawn.Original;
         Maps.Select(MapValue(current.MapGroup, current.MapNum), number => $"Map {number / 256}.{number % 256}");
         xText = current.X.ToString();
         yText = current.Y.ToString();
         NotifyPropertyChanged(nameof(XText));
         NotifyPropertyChanged(nameof(YText));
         NotifyPropertyChanged(nameof(ShakeTruck));
         NotifyPropertyChanged(nameof(ShakeLabel));
         NotifyPropertyChanged(nameof(ToggleShakeText));
         NotifyPropertyChanged(nameof(Details));
         if (!isAvailable) { Status = string.Empty; StatusIsError = false; }
         else if (!current.IsTruck && (current.X < 0 || current.Y < 0)) {
            Status = "This map starts the player at a negative position, which is outside the map. Type the X and Y of a tile to start on.";
            StatusIsError = true;
         } else if (current != CubeNewGameSpawn.Original) {
            Status = current.IsTruck ? "Changed. 'Back to the original' puts the truck back." : "Changed. The new game starts on this map without the truck scene.";
            StatusIsError = false;
         } else { Status = string.Empty; StatusIsError = false; }
         (Reset as StubCommand)?.RaiseCanExecuteChanged();
         (ToggleShake as StubCommand)?.RaiseCanExecuteChanged();
         (ShowInMainTab as StubCommand)?.RaiseCanExecuteChanged();
      }

      private void SetCoordinate(ref string field, string value, string name, string property, Func<CubeNewGameSpawn, int, CubeNewGameSpawn> apply) {
         value = (value ?? string.Empty).Trim();
         if (value == field) return;
         if (!isAvailable) { NotifyPropertyChanged(property); return; }
         if (!int.TryParse(value, out var number) || number < short.MinValue || number > short.MaxValue) {
            tab.ReportError($"'{value}' is not a valid {name}: type a whole number from {short.MinValue} to {short.MaxValue}.");
            NotifyPropertyChanged(property);
            return;
         }
         Change(spawn => apply(spawn, number));
      }

      private void Change(Func<CubeNewGameSpawn, CubeNewGameSpawn> change) {
         if (!isAvailable) return;
         tab.WriteSpawn(this, change(current));
      }
   }

   /// <summary>
   /// The Intro Manager: what happens at the start of a new game. Choose the Pokémon of the professor's speech from a list, the music of the title screen and of the speech from a list of songs,
   /// and the picture of the professor. Works on Emerald, FireRed and the games made from them. Every change is a real change of the ROM and a normal undo step.
   /// </summary>
   public class IntroManagerTab : ViewModelCore, ITabContent, IRaiseMessageTab {
      private readonly ViewPort viewPort;
      private readonly IDataModel model;
      private readonly StubCommand close = new();

      #region ITabContent

      public string Name => "Intro Manager";
      public bool SpartanMode { get; set; }
      public IDataModel Model => model;
      public bool IsMetadataOnlyChange => false;
      public ICommand Save { get; }
      public ICommand SaveAs { get; }
      public ICommand ExportBackup { get; } = new StubCommand();
      public ICommand Undo { get; }
      public ICommand Redo { get; }
      public ICommand Copy { get; } = new StubCommand();
      public ICommand DeepCopy { get; } = new StubCommand();
      public ICommand Diff => null;
      public ICommand DiffLeft => null;
      public ICommand DiffRight => null;
      public ICommand Clear { get; } = new StubCommand();
      public ICommand SelectAll { get; } = new StubCommand();
      public ICommand Goto { get; } = new StubCommand();
      public ICommand ResetAlignment { get; } = new StubCommand();
      public ICommand Back { get; } = new StubCommand();
      public ICommand Forward { get; } = new StubCommand();
      public ICommand Close => close;
      public bool CanDuplicate => false;
      public void Duplicate() { }

      public event EventHandler<string> OnError;
      public event EventHandler<string> OnMessage;
      public event EventHandler Closed;
      public event EventHandler<TabChangeRequestedEventArgs> RequestTabChange;
      event EventHandler ITabContent.ClearMessage { add { } remove { } }
      event EventHandler<Action> ITabContent.RequestDelayedWork { add { } remove { } }
      event EventHandler ITabContent.RequestMenuClose { add { } remove { } }
      event EventHandler<Direction> ITabContent.RequestDiff { add { } remove { } }
      event EventHandler<CanDiffEventArgs> ITabContent.RequestCanDiff { add { } remove { } }
      event EventHandler<CanPatchEventArgs> ITabContent.RequestCanCreatePatch { add { } remove { } }
      event EventHandler<CanPatchEventArgs> ITabContent.RequestCreatePatch { add { } remove { } }
      event EventHandler ITabContent.RequestRefreshGotoShortcuts { add { } remove { } }

      public bool CanIpsPatchRight => false;
      public bool CanUpsPatchRight => false;
      public void IpsPatchRight() { }
      public void UpsPatchRight() { }
      void ITabContent.Refresh() => Reload();
      public bool TryImport(LoadedFile file, IFileSystem fileSystem) => false;

      public void RaiseMessage(string message) => OnMessage?.Invoke(this, message);

      #endregion

      /// <summary>True for Emerald, FireRed, LeafGreen and games made from them (by the game code in the header). Whether the intro's code can really be found is shown by the tab itself.</summary>
      public static bool IsSupported(IDataModel model) => IntroManager.IsSupported(model);

      public const string UnsupportedMessage = IntroManager.UnsupportedMessage;

      public IntroManager Manager { get; private set; }

      public string Introduction => Manager.Game == IntroGame.FireRed
         ? "Change what happens when a new game starts: the Pokémon and the picture of Professor Oak's speech, the music, and the cry on the title screen. Choose from the lists; the game's code is changed for you."
         : "Change what happens when a new game starts: the Pokémon and the picture of Professor Birch's speech and the music. Choose from the lists; the game's code is changed for you.";

      public IntroSettingEditor Species { get; private set; }
      public IntroSettingEditor ProfessorMusic { get; private set; }
      public IntroSettingEditor TitleMusic { get; private set; }
      public IntroSettingEditor TitleCry { get; private set; }
      public IntroPictureEditor Picture { get; private set; }
      public IntroSpawnEditor Spawn { get; private set; }
      /// <summary>Everything the tab shows as a card, in the order of the cards: the Pokémon, the professor's picture, the music, the title cry, the spawn. The view lays them out side by side.</summary>
      public IReadOnlyList<object> Cards { get; private set; } = Array.Empty<object>();
      public IReadOnlyList<IntroSettingEditor> MusicEditors { get; private set; } = Array.Empty<IntroSettingEditor>();
      public IReadOnlyList<IntroSettingEditor> Editors { get; private set; } = Array.Empty<IntroSettingEditor>();

      public bool HasSpecies => Species != null;
      public bool HasMusic => MusicEditors.Count > 0;
      public bool HasTitleCry => TitleCry != null;
      public bool HasPicture => Picture != null;
      /// <summary>The spawn card is shown for Emerald-based games (it is disabled with a message when the ROM has no spawn table).</summary>
      public bool HasSpawn => Spawn != null;
      public bool Found => Manager.Found;
      public bool NotFound => !Manager.Found;
      public string NotFoundMessage => IntroManager.UnsupportedMessage;

      /// <summary>What could not be found (one sentence each), or empty.</summary>
      public IReadOnlyList<string> Problems => Manager.Problems;
      public bool HasProblems => Manager.Problems.Count > 0;

      public string ProfessorName => Manager.ProfessorName;

      /// <summary>What can't be changed, shown at the bottom.</summary>
      public string Limits => Manager.Game == IntroGame.FireRed
         ? "Limits: the game's code stores the Pokémon number of Oak's speech in one byte, so only Pokémon 1 to 255 can be chosen (the later ones are greyed out). Songs above 510 that are written as two instructions can't be chosen either."
         : "Limits: where the game's code builds a number from two instructions, it can store 0 to 510 (or a byte times a power of two). Pokémon and songs above that are greyed out; this matters only for hacks with many more Pokémon or songs than the original game.";

      private readonly Dictionary<int, IntroPictureChoice> pokemonPictures = new();

      public IntroManagerTab(ViewPort viewPort) {
         this.viewPort = viewPort;
         model = viewPort.Model;
         Save = viewPort.Save;
         SaveAs = viewPort.SaveAs;
         Undo = ReloadAfter(viewPort.Undo);
         Redo = ReloadAfter(viewPort.Redo);
         close.CanExecute = arg => true;
         close.Execute = arg => Closed?.Invoke(this, EventArgs.Empty);
         Manager = IntroManager.Find(model);
         Build();
      }

      private ICommand ReloadAfter(ICommand command) {
         var wrapper = new StubCommand {
            CanExecute = arg => command.CanExecute(arg),
            Execute = arg => { command.Execute(arg); Reload(); },
         };
         command.CanExecuteChanged += (sender, e) => wrapper.RaiseCanExecuteChanged();
         return wrapper;
      }

      private StubCommand refresh;
      public ICommand Refresh => StubCommand(ref refresh, Reload, () => true);

      private void Build() {
         var data = model.RawData;
         var speciesNames = SpeciesNames();
         var songNames = SongNames();
         IntroSettingEditor Make(IntroSetting setting, IReadOnlyList<string> names) {
            if (setting == null) return null;
            var editor = new IntroSettingEditor(this, setting, names);
            editor.Capture(data);
            return editor;
         }
         Species = Make(Manager.Species, speciesNames);
         ProfessorMusic = Make(Manager.ProfessorMusic, songNames);
         TitleMusic = Make(Manager.TitleMusic, songNames);
         TitleCry = Make(Manager.TitleCry, speciesNames);
         MusicEditors = new[] { ProfessorMusic, TitleMusic }.Where(editor => editor != null).ToList();
         Editors = new[] { Species, ProfessorMusic, TitleMusic, TitleCry }.Where(editor => editor != null).ToList();
         if (Manager.Picture != null) {
            Picture = new IntroPictureEditor(this, Manager.Picture, model);
            Picture.Capture(data);
         }
         if (Manager.Found && Manager.Game != IntroGame.FireRed) Spawn = new IntroSpawnEditor(this);
         Cards = new object[] { Species, Picture, ProfessorMusic, TitleMusic, TitleCry, Spawn }.Where(card => card != null).ToList();
         foreach (var choice in IntroPictureSources.Pokemon(model)) pokemonPictures[choice.Index] = choice;
         Species?.Reload(data);
         TitleCry?.Reload(data);
      }

      private IReadOnlyList<string> SpeciesNames() {
         try { return model.GetOptions(HardcodeTablesModel.GetSpeciesNameTable(model)); } catch (Exception) { return null; }
      }

      private IReadOnlyList<string> SongNames() {
         try { return model.TryGetList("songnames", out var list) ? list : null; } catch (Exception) { return null; }
      }

      /// <summary>Reads everything from the ROM again (after undo, redo or a change somewhere else).</summary>
      public void Reload() {
         var data = model.RawData;
         foreach (var editor in Editors) editor.Reload(data);
         Picture?.Reload(data);
         Spawn?.Reload();
      }

      /// <summary>The front picture of a Pokémon for the previews, or null.</summary>
      internal IPixelViewModel DrawPokemon(int species) {
         if (!pokemonPictures.TryGetValue(species, out var choice)) return null;
         var image = choice.Load();
         return image == null ? null : CharacterSprites.Draw(image.Pixels(), image.DisplayPalette());
      }

      #region Editing

      internal void ReportError(string message) => OnError?.Invoke(this, message);

      private void CompleteEdit() => viewPort.ChangeHistory.ChangeCompleted();

      internal void WriteSetting(IntroSettingEditor editor, int value) {
         CompleteEdit();
         try {
            editor.Setting.Write(model, viewPort.CurrentChange, value);
         } catch (Exception e) {
            CompleteEdit();
            ReportError("Could not change the ROM: " + e.Message);
            Reload();
            return;
         }
         CompleteEdit();
         viewPort.Refresh();
         Reload();
         var now = editor.Options.Find(editor.Value);
         if (now != null) OnMessage?.Invoke(this, $"{editor.Title}: {now.Text}.");
      }

      internal void WriteSpawn(IntroSpawnEditor editor, CubeNewGameSpawn spawn) {
         CompleteEdit();
         try {
            if (!CubeNewGameSpawnTable.Write(model, viewPort.CurrentChange, spawn)) { ReportError(IntroSpawnEditor.NeedsCubeMessage); Reload(); return; }
         } catch (Exception e) {
            CompleteEdit();
            ReportError("Could not change the ROM: " + e.Message);
            Reload();
            return;
         }
         CompleteEdit();
         viewPort.Refresh();
         Reload();
         OnMessage?.Invoke(this, $"New game spawn: {editor.Maps.Selected?.Text ?? "changed"} at {spawn.X}, {spawn.Y}.");
      }

      internal void ApplyPicture(IntroPictureEditor editor, IntroImage source) {
         CompleteEdit();
         bool ok;
         string reason;
         try {
            ok = editor.Picture.Apply(model, viewPort.CurrentChange, source, out reason);
         } catch (Exception e) {
            CompleteEdit();
            ReportError("Could not change the picture: " + e.Message);
            Reload();
            return;
         }
         CompleteEdit();
         if (!ok) { ReportError(reason); return; }
         viewPort.Refresh();
         Reload();
         OnMessage?.Invoke(this, $"{editor.Picture.Title}'s picture was replaced.");
      }

      internal void RestorePicture(IntroPictureEditor editor, int[] targets) {
         CompleteEdit();
         try {
            editor.Picture.Restore(model, viewPort.CurrentChange, targets);
         } catch (Exception e) {
            CompleteEdit();
            ReportError("Could not change the picture: " + e.Message);
            Reload();
            return;
         }
         CompleteEdit();
         viewPort.Refresh();
         Reload();
      }

      internal void ShowInMainTab(int address) {
         if (address < 0) return;
         viewPort.Goto.Execute(address);
         RequestTabChange?.Invoke(this, new TabChangeRequestedEventArgs(viewPort));
      }

      #endregion
   }
}
