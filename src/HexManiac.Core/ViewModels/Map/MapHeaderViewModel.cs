using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Map;
using HavenSoft.HexManiac.Core.ViewModels.Images;
using HexManiac.Core.Models.Runs.Sprites;
using System;
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

         // battle
         battleOptions.Add("Normal");
         battleOptions.Add("Gym");
         battleOptions.Add("Evil Team");
         battleOptions.Add("Unknown");
         battleOptions.Add("Elite 1");
         battleOptions.Add("Elite 2");
         battleOptions.Add("Elite 3");
         battleOptions.Add("Elite 4");
         battleOptions.Add("Big Red Pokeball");
      }

      public ObservableCollection<string> WeatherOptions => weatherOptions;

      public ObservableCollection<string> CaveOptions => caveOptions;

      public ObservableCollection<string> BattleOptions => battleOptions;

      public MapHeaderViewModel(ModelArrayElement element, Format format, Func<ModelDelta> tokens) {
         (map, this.format, tokenFactory) = (element, format, tokens);
         if (element == null) return;
         if (element.Model.TryGetList("songnames", out var songnames)) {
            for (int i = 0; i < songnames.Count; i++) {
               var name = songnames[i] ?? $"song_{i}";
               MusicOptions.Add(name);
            }
         }
         if (element.Model.TryGetList("maptypes", out var mapTypes)) {
            foreach (var name in mapTypes) MapTypeOptions.Add(name);
         }
         Refresh();
      }

      public void UpdateFromModel() {
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
      public int Music { get => GetValue(); set => SetValue(value); }
      public int LayoutID { get => GetValue(); set => SetValue(value); }
      public int RegionSectionID { get => GetValue(); set => SetValue(value); }
      public int Cave { get => GetValue(); set => SetValue(value); }
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

      public bool HasMusicOptions => MusicOptions.Count > 0;
      public ObservableCollection<string> MusicOptions { get; } = new();

      public bool HasMapTypeOptions => MapTypeOptions.Count > 0;
      public ObservableCollection<string> MapTypeOptions { get; } = new();

      private int GetValue([CallerMemberName]string name = null) {
         name = char.ToLower(name[0]) + name.Substring(1);
         if (!map.HasField(name)) return -1;
         return map.GetValue(name);
      }

      // when we call SetValue, get the latest token
      private void SetValue(int value, [CallerMemberName]string name = null) {
         if (value == GetValue(name)) return;
         map = new(map.Model, map.Table.Start, (map.Start - map.Table.Start) / map.Table.ElementCount, tokenFactory, map.Table);
         var originalName = name;
         name = char.ToLower(name[0]) + name.Substring(1);
         map.SetValue(name, value);
         NotifyPropertyChanged(originalName);
      }

      private bool GetBool([CallerMemberName]string name = null) {
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

      private void SetBool(bool value, [CallerMemberName]string name = null) {
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
      public IPixelViewModel Render => render?.Value;
      public string AddressText => Address.ToAddress();

      public BlocksetOption(IDataModel model, int address) {
         Model = model;
         Address = address;
         if (!model.SpartanMode) render = new Lazy<IPixelViewModel>(() => new BlocksetModel(Model, Address).RenderBlockset(.5));
      }
   }
}
