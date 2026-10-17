using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Map;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using HexManiac.Core.Models.Runs.Sprites;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace HavenSoft.HexManiac.Core.ViewModels.Map {
   public class MapHeaderViewModel : ViewModelCore, INotifyPropertyChanged {
      private ModelArrayElement map;
      private readonly Func<ModelDelta> tokenFactory;
      private readonly Format format;
      // music: layoutID: regionSectionID. cave. weather. mapType. allowBiking. flags.|t|allowEscaping.|allowRunning.|showMapName::: floorNum. battleType.

      private static readonly ObservableCollection<string> weatherOptions = new();
      private static readonly ObservableCollection<string> caveOptions = new();
      private static readonly ObservableCollection<string> caveFlagOptions = new();
      private static readonly ObservableCollection<string> battleOptions = new();
      static MapHeaderViewModel() {
         // weather
         weatherOptions.Add("Indoor");
         weatherOptions.Add("Sunny Clouds");
         weatherOptions.Add("Outdoor");
         weatherOptions.Add("Rain");
         weatherOptions.Add("Snow");
         weatherOptions.Add("Thunderstorm");
         weatherOptions.Add("Fog - Horizontal");
         weatherOptions.Add("Ash");
         weatherOptions.Add("Sandstorm");
         weatherOptions.Add("Fog - Diagonal");
         weatherOptions.Add("Underwater");
         weatherOptions.Add("Shade");
         weatherOptions.Add("Drought");
         weatherOptions.Add("Downpour");
         weatherOptions.Add("Underwater - Bubbles");
         weatherOptions.Add("Alternating Storm");
         weatherOptions.Add("16 - Unused");
         weatherOptions.Add("17 - Unused");
         weatherOptions.Add("18 - Unused");
         weatherOptions.Add("19 - Unused");
         weatherOptions.Add("Cycle - Route 119");
         weatherOptions.Add("Cycle - Route 123");

         // cave
         caveOptions.Add("Normal");
         caveOptions.Add("Flash Usable");
         caveOptions.Add("Flash Not Usable");

         // pokeemerald-expansion keeps 'cave' as one bit of the header's flags instead of a byte of its own: it can only be on or off
         caveFlagOptions.Add("Normal");
         caveFlagOptions.Add("Flash Usable");

         // battle
         battleOptions.Add("Normal");
         battleOptions.Add("Gym");
         battleOptions.Add("Indoor 1 (Magma)");
         battleOptions.Add("Indoor 2 (Aqua)");
         battleOptions.Add("Elite 1");
         battleOptions.Add("Elite 2");
         battleOptions.Add("Elite 3");
         battleOptions.Add("Elite 4");
         battleOptions.Add("Big Red Poké ball");
      }

      public ObservableCollection<string> WeatherOptions => weatherOptions;

      /// <summary>The choices for the cave field: three for the vanilla byte, two for the single bit that the expansion uses.</summary>
      public ObservableCollection<string> CaveOptions => CaveIsFlagBit ? caveFlagOptions : caveOptions;

      public ObservableCollection<string> BattleOptions => battleOptions;

      public MapHeaderViewModel(ModelArrayElement element, Format format, Func<ModelDelta> tokens) {
         (map, this.format, tokenFactory) = (element, format, tokens);
         if (element == null) return;
         if (element.Model.TryGetList("songnames", out var songnames)) {
            songChoices = SongChoices.Get(songnames);
            // the music dropdown can be typed into to find a song quickly
            MusicFilter.Bind(nameof(FilteringComboOptions.ModelValue), (filter, args) => {
               if (!updatingMusicFilter) Music = filter.ModelValue;
            });
            SyncMusicFilter();
         }
         if (element.Model.TryGetList("maptypes", out var mapTypes)) {
            foreach (var name in mapTypes) MapTypeOptions.Add(name);
         }
         Refresh();
      }

      #region Music

      /// <summary>
      /// The songs that the music dropdown offers. A ROM's song list can be sparse: pokeemerald-expansion names song 0, 350-ish, 32767 and 65535,
      /// so the list is 65536 entries long and almost all of them are empty. The dropdown only offers the songs that have a name
      /// (a few hundred, not 65536: building and filling a 65536-item drop-down is what froze the map editor).
      /// All headers of one ROM share one list, it is only built again when the ROM's list changes.
      /// </summary>
      private sealed class SongChoices {
         private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ValidationList, SongChoices> cache = new();

         private readonly int sourceCount;
         public List<ComboOption> Options { get; } = new();
         /// <summary>song number to position in Options</summary>
         public Dictionary<int, int> Position { get; } = new();

         private SongChoices(IReadOnlyList<string> songnames) {
            sourceCount = songnames.Count;
            for (int i = 0; i < songnames.Count; i++) {
               if (string.IsNullOrEmpty(songnames[i])) continue;
               Position[i] = Options.Count;
               Options.Add(new ComboOption(songnames[i], i));
            }
         }

         public static SongChoices Get(ValidationList songnames) {
            lock (cache) {
               if (cache.TryGetValue(songnames, out var existing) && existing.sourceCount == songnames.Count) return existing;
               var created = new SongChoices(songnames);
               cache.Remove(songnames);
               cache.Add(songnames, created);
               return created;
            }
         }
      }

      private readonly SongChoices songChoices;
      private List<ComboOption> unnamedSongOptions; // the shared list plus the one song this map plays that has no name, if any
      private int unnamedSong = -1, unnamedSongPosition;

      /// <summary>The music dropdown: type part of a song's name to narrow the list, or pick from it.</summary>
      public FilteringComboOptions MusicFilter { get; } = new();
      private bool updatingMusicFilter;

      public bool HasMusicOptions => songChoices != null && songChoices.Options.Count > 0;

      private void SyncMusicFilter() {
         if (songChoices == null || map == null) return;
         var song = Music;
         if (!songChoices.Position.TryGetValue(song, out var position)) position = -1;
         var options = (IReadOnlyList<ComboOption>)songChoices.Options;
         if (position < 0 && song >= 0) {
            // a song without a name still has to show up (as 'song_N') or the box would be blank
            if (unnamedSongOptions == null || unnamedSong != song) {
               unnamedSongOptions = new List<ComboOption>(songChoices.Options);
               unnamedSongPosition = unnamedSongOptions.FindIndex(option => option.Index > song);
               if (unnamedSongPosition < 0) unnamedSongPosition = unnamedSongOptions.Count;
               unnamedSongOptions.Insert(unnamedSongPosition, new ComboOption($"song_{song}", song));
               unnamedSong = song;
            }
            options = unnamedSongOptions;
            position = unnamedSongPosition;
         }
         updatingMusicFilter = true;
         try {
            MusicFilter.Update(options, position);
         } finally {
            updatingMusicFilter = false;
         }
      }

      #endregion

      public void UpdateFromModel() {
         SyncMusicFilter();
         if (primaryIndex == -1 || secondaryIndex == -1) return; // only way this can ever be set is later in this same method (recursion guard)
         if (map == null) return;
         var layoutTable = map.GetSubTable(Format.Layout);
         if (layoutTable == null) return;
         var layout = layoutTable[0];
         var primaryAddress = layout.GetAddress(Format.PrimaryBlockset);
         var secondaryAddress = layout.GetAddress(Format.SecondaryBlockset);

         // if this is a no-op, skip
         var newPrimary = format.BlocksetCache.Primary.IndexOf(format.BlocksetCache.Primary.FirstOrDefault(blockset => blockset.Address == primaryAddress));
         var newSecondary = format.BlocksetCache.Secondary.IndexOf(format.BlocksetCache.Secondary.FirstOrDefault(blockset => blockset.Address == secondaryAddress));
         if (
            PrimaryOptions.SequenceEqual(format.BlocksetCache.Primary) &&
            SecondaryOptions.SequenceEqual(format.BlocksetCache.Secondary) &&
            primaryIndex == newPrimary &&
            secondaryIndex == newSecondary) {
            // already in a good state
            // don't actually notify, since there's no changes
            return;
         }

         PrimaryOptions.Clear();
         SecondaryOptions.Clear();
         foreach (var item in format.BlocksetCache.Primary) PrimaryOptions.Add(item);
         foreach (var item in format.BlocksetCache.Secondary) SecondaryOptions.Add(item);

         // force refresh for primary/secondary index
         (primaryIndex, secondaryIndex) = (-1, -1);
         NotifyPropertiesChanged(nameof(PrimaryIndex), nameof(SecondaryIndex));
         (primaryIndex, secondaryIndex) = (newPrimary, newSecondary);
         NotifyPropertiesChanged(nameof(PrimaryIndex), nameof(SecondaryIndex));
      }

      public void Refresh() {
         format.Refresh();
         UpdateFromModel();
      }

      public ObservableCollection<BlocksetOption> PrimaryOptions { get; } = new();
      public ObservableCollection<BlocksetOption> SecondaryOptions { get; } = new();
      private int primaryIndex, secondaryIndex;
      public int PrimaryIndex {
         get => primaryIndex;
         set {
            if (primaryIndex == value || value == -1) return;
            primaryIndex = value;
            UpdateBlocksets();
            NotifyPropertyChanged();
         }
      }
      public int SecondaryIndex {
         get => secondaryIndex;
         set {
            if (secondaryIndex == value || value == -1) return;
            secondaryIndex = value;
            UpdateBlocksets();
            NotifyPropertyChanged();
         }
      }
      private void UpdateBlocksets() {
         var map = new MapModel(this.map);
         if (map.Layout.Element == null) return;
         if (!primaryIndex.InRange(0, PrimaryOptions.Count)) return;
         if (!secondaryIndex.InRange(0, SecondaryOptions.Count)) return;
         map.Layout.Element.SetAddress(Format.PrimaryBlockset, PrimaryOptions[primaryIndex].Address);
         map.Layout.Element.SetAddress(Format.SecondaryBlockset, SecondaryOptions[secondaryIndex].Address);
      }

      // flags.|t|allowBiking.|allowEscaping.|allowRunning.|showMapName.
      public int Music {
         get => GetValue();
         set {
            SetValue(value);
            SyncMusicFilter(); // the dropdown shows what the ROM says, whoever changed it
         }
      }
      public int LayoutID { get => GetValue(); set => SetValue(value); }
      public int RegionSectionID { get => GetValue(); set => SetValue(value); }
      /// <summary>
      /// 0 = normal, 1 = flash usable (vanilla also has 2 = flash not usable).
      /// Vanilla headers have a byte called 'cave'. pokeemerald-expansion headers have no such byte: 'cave' is one bit of the 'flags' tuple,
      /// which used to leave the dropdown blank (the field was simply not found).
      /// </summary>
      public int Cave {
         get => CaveIsFlagBit ? GetFlags().GetValue("cave") : GetValue();
         set {
            if (value < 0) return; // a dropdown reports -1 when its selection is cleared: that is not a cave setting
            if (CaveIsFlagBit) {
               SetCaveFlag(value);
            } else if (map.HasField("cave")) {
               SetValue(value);
            }
         }
      }

      /// <summary>True when this ROM's header keeps 'cave' as a bit inside its 'flags' tuple instead of as a field of its own.</summary>
      private bool CaveIsFlagBit => map != null && !map.HasField("cave") && GetFlags()?.HasField("cave") == true;

      /// <summary>The header's 'flags' tuple, or null if the header has no field of that name or the field is not a tuple.</summary>
      private ModelTupleElement GetFlags() {
         if (map == null || !map.HasField("flags")) return null;
         var segment = map.Table.ElementContent.FirstOrDefault(s => s.Name == "flags");
         if (!(segment is ArrayRunTupleSegment) && !(segment is ArrayRunBitArraySegment)) return null;
         return map.GetTuple("flags");
      }

      private void SetCaveFlag(int value) {
         if (!value.InRange(0, 2)) return; // it's a single bit
         if (GetFlags().GetValue("cave") == value) return;
         RefreshToken();
         GetFlags().SetValue("cave", value);
         NotifyPropertyChanged(nameof(Cave));
      }
      public int Weather { get => GetValue(); set => SetValue(value); }
      public int MapType { get => GetValue(); set => SetValue(value); }
      public bool AllowBiking { get => GetBool(); set => SetBool(value); }
      public bool AllowEscaping { get => GetBool(); set => SetBool(value); }
      public bool AllowRunning { get => GetBool(); set => SetBool(value); }
      public bool ShowMapName { get => GetBool(); set => SetBool(value); }
      public int FloorNum { get => GetValue(); set => SetValue(value); }
      public int BattleType { get => GetValue(); set => SetValue(value); }

      public bool ShowFloorNumField => map.HasField("floorNum");                // FR/LG only
      public bool ShowAllowBikingField => map.HasField("allowBiking") || (map.HasField("flags") && map.GetTuple("flags").HasField("allowBiking"));       // not for R/S

      public bool HasMapTypeOptions => MapTypeOptions.Count > 0;
      public ObservableCollection<string> MapTypeOptions { get; } = new();

      private int GetValue([CallerMemberName] string name = null) {
         name = char.ToLower(name[0]) + name.Substring(1);
         if (!map.HasField(name)) return -1;
         return map.GetValue(name);
      }

      // get the latest token for the next change
      private void RefreshToken() {
         map = new(map.Model, map.Table.Start, map.ArrayIndex, tokenFactory, map.Table);
      }

      // when we call SetValue, get the latest token
      private void SetValue(int value, [CallerMemberName] string name = null) {
         if (value == GetValue(name)) return;
         RefreshToken();
         var originalName = name;
         name = char.ToLower(name[0]) + name.Substring(1);
         map.SetValue(name, value);
         NotifyPropertyChanged(originalName);
      }

      private bool GetBool([CallerMemberName] string name = null) {
         name = char.ToLower(name[0]) + name.Substring(1);
         if (map.HasField(name)) {
            return map.GetValue(name) != 0;
         } else if (map.HasField("flags")) {
            var tuple = map.GetTuple("flags");
            if (!tuple.HasField(name)) return false;
            return tuple.GetValue(name) != 0;
         }

         return false;
      }

      private void SetBool(bool value, [CallerMemberName] string name = null) {
         var originalName = name;
         name = char.ToLower(name[0]) + name.Substring(1);
         if (map.HasField(name)) {
            map.SetValue(name, value ? 1 : 0);
            NotifyPropertyChanged(originalName);
         } else if (map.HasField("flags")) {
            var tuple = map.GetTuple("flags");
            if (tuple.HasField(name)) {
               tuple.SetValue(name, value ? 1 : 0);
               NotifyPropertyChanged(originalName);
            }
         }
      }
   }

   public class BlocksetOption : ViewModelCore { // changing this to a record would interfere with combobox selection changes.
      private readonly Lazy<IPixelViewModel> render;
      public IDataModel Model { get; }
      public int Address { get; }
      public string NameHint { get; }
      public IPixelViewModel Render => render?.Value;
      public string AddressText => Address.ToAddress() + NameHint;

      public BlocksetOption(IDataModel model, int address) {
         Model = model;
         Address = address;

         var sources = model.GetNextRun(address)?.PointerSources ?? Enumerable.Empty<int>();
         var layoutRuns = sources.Select(source => model.GetNextRun(source)).Distinct();
         var layoutSources = layoutRuns.SelectMany(run => run?.PointerSources ?? Enumerable.Empty<int>());
         var mapRuns = layoutSources.Select(source => model.GetNextRun(source)).Where(run => model.GetAnchorFromAddress(-1, run.Start) != "data.maps.layouts" && run is ITableRun);
         var elements = mapRuns.Select(run => new ModelTable(model, (ITableRun)run)[0]).Where(element => element.HasField("regionSectionID"));
         NameHint = elements.Select(element => element.GetEnumValue("regionSectionID")).Distinct().OrderBy(s => s).FirstOrDefault() ?? string.Empty;
         if (NameHint != string.Empty) NameHint = $" (ex. {NameHint})";

         if (!model.SpartanMode) render = new Lazy<IPixelViewModel>(() => new BlocksetModel(Model, Address).RenderBlockset(.5));
      }
   }
}
