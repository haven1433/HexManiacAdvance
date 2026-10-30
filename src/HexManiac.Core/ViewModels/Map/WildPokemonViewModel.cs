using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace HavenSoft.HexManiac.Core.ViewModels.Map {
   public class WildPokemonViewModel : ViewModelCore {
      // The vanilla games have one table per terrain: grass, surf, tree (rock smash), fish.
      // The pokeemerald-expansion has one set per time of day: morningGrass, dayGrass, eveningGrass, nightGrass, ... and a 'hidden' terrain.
      private const string Grass = "grass", Surf = "surf", Tree = "tree", Fish = "fish", Hidden = "hidden";
      private static readonly string[] Terrains = { Grass, Surf, Tree, Fish, Hidden };
      private static readonly string[] TimeKeys = { "morning", "day", "evening", "night" };
      private static readonly string[] TimeNames = { "Morning", "Day", "Evening", "Night" };

      private readonly IEditableViewPort viewPort;
      private readonly IDataModel model;
      private readonly Func<ModelDelta> tokenFactory;
      private readonly MapTutorialsViewModel tutorials;
      private readonly int bank, map;

      public WildPokemonViewModel(IEditableViewPort viewPort, MapTutorialsViewModel tutorials, int group, int map) {
         (this.viewPort, this.tokenFactory) = (viewPort, () => viewPort.ChangeHistory.CurrentChange);
         (this.model, this.tutorials) = (viewPort.Model, tutorials);
         (this.bank, this.map) = (group, map);
      }

      #region Field names (vanilla vs time-of-day)

      private bool? hasTimesOfDay;
      /// <summary>True for the pokeemerald-expansion, where every terrain has a separate table for morning/day/evening/night.</summary>
      public bool HasTimesOfDay {
         get {
            if (hasTimesOfDay == null) {
               var table = model.GetTable(HardcodeTablesModel.WildTableName);
               hasTimesOfDay = table != null && table.ElementContent.Any(segment => segment.Name == TimeKeys[0] + "Grass");
            }
            return hasTimesOfDay.Value;
         }
      }

      public IReadOnlyList<string> TimeOfDayOptions => TimeNames;
      public bool IsMorning { get => selectedTimeOfDay == 0; set { if (value) SelectedTimeOfDay = 0; } }
      public bool IsDay { get => selectedTimeOfDay == 1; set { if (value) SelectedTimeOfDay = 1; } }
      public bool IsEvening { get => selectedTimeOfDay == 2; set { if (value) SelectedTimeOfDay = 2; } }
      public bool IsNight { get => selectedTimeOfDay == 3; set { if (value) SelectedTimeOfDay = 3; } }

      private int selectedTimeOfDay;
      public int SelectedTimeOfDay {
         get => selectedTimeOfDay;
         set {
            value = value.LimitToRange(0, TimeKeys.Length - 1);
            if (selectedTimeOfDay == value) return;
            selectedTimeOfDay = value;
            NotifyPropertiesChanged(nameof(SelectedTimeOfDay), nameof(IsMorning), nameof(IsDay), nameof(IsEvening), nameof(IsNight), nameof(GrassExists), nameof(SurfExists), nameof(TreeExists), nameof(FishingExists), nameof(HiddenExists));
         }
      }

      private string FieldName(string terrain, int timeOfDay) {
         if (!HasTimesOfDay) return terrain;
         return TimeKeys[timeOfDay] + char.ToUpperInvariant(terrain[0]) + terrain.Substring(1);
      }

      private bool TerrainSupported(string terrain) {
         var table = model.GetTable(HardcodeTablesModel.WildTableName);
         if (table == null) return false;
         var name = FieldName(terrain, 0);
         return table.ElementContent.Any(segment => segment.Name == name);
      }

      #endregion

      #region Top-Level Wild Data

      private int wildDataIndex = int.MinValue;
      public bool HasWildData {
         get {
            if (wildDataIndex == int.MinValue) wildDataIndex = FindWildData();
            return wildDataIndex != -1;
         }
      }
      private int FindWildData() {
         var wildData = model.GetTableModel(HardcodeTablesModel.WildTableName, default);
         if (wildData == null) return -1;
         for (int i = 0; i < wildData.Count; i++) {
            var bank = wildData[i].GetValue("bank");
            var map = wildData[i].GetValue("map");
            if (this.bank != bank || this.map != map) continue;
            return i;
         }
         return -1;
      }

      // used to decide whether the popup is showing or not
      // when the popup is first shown, create data for this map if there isn't any
      private bool showWildData;
      public bool ShowWildData {
         get => showWildData;
         set {
            Set(ref showWildData, value);
            if (!showWildData || HasWildData) return;
            var token = tokenFactory();

            // extend wild table
            var wildTable = model.GetTable(HardcodeTablesModel.WildTableName);
            if (wildTable == null) return;
            var originalStart = wildTable.Start;
            wildTable = model.RelocateForExpansion(token, wildTable, wildTable.Length + wildTable.ElementLength);
            wildTable = wildTable.Append(token, 1);
            model.ObserveRunWritten(token, wildTable);

            // create new entry: appending copies the previous entry, so clear everything that isn't the map id
            var element = new ModelArrayElement(model, wildTable.Start, wildTable.ElementCount - 1, tokenFactory, wildTable);
            element.SetValue("bank", bank);
            element.SetValue("map", map);
            foreach (var segment in wildTable.ElementContent) {
               if (segment.Type == ElementContentType.Pointer) {
                  element.SetAddress(segment.Name, Pointer.NULL);
               } else if (segment.Name != "bank" && segment.Name != "map") {
                  for (int i = 0; i < segment.Length; i++) token.ChangeData(model, element.Start + SegmentOffset(wildTable, segment) + i, 0);
               }
            }
            wildDataIndex = wildTable.ElementCount - 1;

            // bookkeeping
            if (wildTable.Start != originalStart) InformRepoint(new("Wild", wildTable.Start));
            wildText = null;
            NotifyPropertiesChanged(nameof(HasWildData), nameof(WildSummary));
            NotifyExistsChanged();
            viewPort.ChangeHistory.ChangeCompleted();
         }
      }

      private static int SegmentOffset(ITableRun table, ArrayRunElementSegment target) {
         int offset = 0;
         foreach (var segment in table.ElementContent) {
            if (ReferenceEquals(segment, target)) return offset;
            offset += segment.Length;
         }
         return offset;
      }

      private string wildText;
      public string WildSummary {
         get {
            if (wildText != null) return wildText;
            if (wildDataIndex == int.MinValue) wildDataIndex = FindWildData();
            var text = new StringBuilder();
            if (wildDataIndex < 0) return text.ToString();
            var wild = model.GetTableModel(HardcodeTablesModel.WildTableName)[wildDataIndex];
            foreach (var terrain in Terrains) {
               if (!TerrainSupported(terrain)) continue;
               if (!HasTimesOfDay) {
                  BuildWildTooltip(text, wild, FieldName(terrain, 0), terrain);
                  continue;
               }
               // times of day that share the same table are described together.
               // a time of day with no table of its own uses the morning table (the game's fallback).
               var groups = new List<(int address, List<int> times)>();
               var morning = wild.HasField(FieldName(terrain, 0)) ? wild.GetAddress(FieldName(terrain, 0)) : Pointer.NULL;
               for (int time = 0; time < TimeKeys.Length; time++) {
                  var field = FieldName(terrain, time);
                  if (!wild.HasField(field)) continue;
                  var address = wild.GetAddress(field);
                  if (address == Pointer.NULL) address = morning;
                  if (address == Pointer.NULL) continue;
                  var group = groups.FindIndex(g => g.address == address);
                  if (group == -1) groups.Add((address, new List<int> { time }));
                  else groups[group].times.Add(time);
               }
               foreach (var group in groups) {
                  var times = group.times.Count == TimeKeys.Length ? string.Empty : $" ({string.Join("/", group.times.Select(time => TimeNames[time].ToLower()))})";
                  BuildWildTooltip(text, wild, FieldName(terrain, group.times[0]), terrain + times);
               }
            }
            wildText = text.TrimEnd().ToString();
            if (string.IsNullOrWhiteSpace(wildText)) wildText = "No Wild Pokemon (yet!)";
            return wildText;
         }
      }
      private static bool BuildWildTooltip(StringBuilder text, ModelArrayElement wild, string field, string heading) {
         // list<[low. high. species:]n>
         var terrain = wild.GetSubTable(field);
         if (terrain == null || terrain.Count == 0) return false;
         var list = terrain[0].GetSubTable("list");
         if (list == null) return false;
         text.Append(heading);
         text.AppendLine(":");

         if (field.EndsWith("fish", StringComparison.OrdinalIgnoreCase)) {
            text.Append($"old rod: ");
            AppendHistogram(text, list.Take(2), "species");
            text.AppendLine();
            text.Append($"good rod: ");
            AppendHistogram(text, list.Skip(2).Take(3), "species");
            text.AppendLine();
            text.Append($"super rod: ");
            AppendHistogram(text, list.Skip(5), "species");
         } else {
            AppendHistogram(text, list, "species");
         }
         text.AppendLine();
         text.AppendLine();
         return true;
      }

      private static void AppendHistogram(StringBuilder text, IEnumerable<ModelArrayElement> elements, string fieldName) {
         var histogram = elements.Select(element => element.GetEnumValue(fieldName)).ToHistogram();
         text.AppendJoin(", ", histogram.Keys.Select(key => {
            if (histogram[key] == 1) return key;
            return $"{key} x{histogram[key]}";
         }));
      }

      private StubCommand gotoWildData;
      public ICommand GotoWildData => StubCommand(ref gotoWildData, () => {
         var wildTable = model.GetTable(HardcodeTablesModel.WildTableName);
         if (wildTable == null || !HasWildData) return;
         viewPort.Goto.Execute(wildTable.Start + wildTable.ElementLength * wildDataIndex);
         tutorials.Complete(Tutorial.ToolbarButton_GotoWildData);
      }, () => model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, HardcodeTablesModel.WildTableName) != Pointer.NULL);

      #endregion

      #region Overworld wild Pokemon (CUBE)

      /// <summary>
      /// CUBE (pokeemerald-expansion with Overworld Wild Encounters): one bit per map, set = no overworld wild Pokemon on that map
      /// (random grass/cave/surf battles instead). Byte [group * 32 + map / 8], bit (map % 8). All zero = overworld Pokemon everywhere.
      /// ROMs/TOMLs without this anchor don't show the checkbox.
      /// </summary>
      public const string OverworldTableName = "data.maps.wild.overworld";
      public const int OverworldGroups = 256, OverworldBytesPerGroup = 32;

      /// <summary>Address of the byte holding this map's bit, or -1 when the ROM has no (valid) overworld table.</summary>
      private int OverworldByteAddress {
         get {
            if (bank < 0 || bank >= OverworldGroups || map < 0 || map >= OverworldBytesPerGroup * 8) return -1;
            var address = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, OverworldTableName);
            if (address < 0 || address >= model.Count) return -1;
            // only trust a run of the expected size: a different anchor format must not give a misleading checkbox
            var run = model.GetNextRun(address);
            if (run.Start != address || run.Length < OverworldGroups * OverworldBytesPerGroup) return -1;
            if (address + OverworldGroups * OverworldBytesPerGroup > model.Count) return -1;
            return address + bank * OverworldBytesPerGroup + map / 8;
         }
      }

      public bool HasOverworldWildPokemonOption => OverworldByteAddress >= 0;

      /// <summary>Checked (the default) = wild Pokemon walk around in the overworld and only touching one starts a battle.</summary>
      public bool OverworldWildPokemon {
         get {
            var address = OverworldByteAddress;
            if (address < 0) return true;
            return (model[address] & (1 << (map % 8))) == 0;
         }
         set {
            var address = OverworldByteAddress;
            if (address < 0 || value == OverworldWildPokemon) return;
            var bit = 1 << (map % 8);
            var original = model[address];
            var updated = (byte)(value ? original & ~bit : original | bit);
            tokenFactory().ChangeData(model, address, updated);
            viewPort.ChangeHistory.ChangeCompleted();
            NotifyPropertyChanged();
         }
      }

      #endregion

      #region Buttons for the Tables

      private StubCommand gotoGrass, gotoTree, gotoFishing, gotoSurf, gotoHidden;

      public bool GrassExists => Exists(Grass);
      public ICommand GotoGrass => StubCommand(ref gotoGrass, () => GotoData(Grass));

      public bool SurfExists => Exists(Surf);
      public ICommand GotoSurf => StubCommand(ref gotoSurf, () => GotoData(Surf));

      public bool TreeExists => Exists(Tree);
      public ICommand GotoTree => StubCommand(ref gotoTree, () => GotoData(Tree));

      public bool FishingExists => Exists(Fish);
      public ICommand GotoFishing => StubCommand(ref gotoFishing, () => GotoData(Fish));

      /// <summary>Hidden (special) encounters only exist in the expansion.</summary>
      public bool HasHidden => HasTimesOfDay && TerrainSupported(Hidden);
      public bool HiddenExists => Exists(Hidden);
      public ICommand GotoHidden => StubCommand(ref gotoHidden, () => GotoData(Hidden));

      #endregion

      private void NotifyExistsChanged() => NotifyPropertiesChanged(nameof(GrassExists), nameof(SurfExists), nameof(TreeExists), nameof(FishingExists), nameof(HiddenExists));

      private bool Exists(string terrain) {
         if (wildDataIndex == int.MinValue) wildDataIndex = FindWildData();
         if (wildDataIndex == -1) return false;
         var wild = model.GetTableModel(HardcodeTablesModel.WildTableName)[wildDataIndex];
         var field = FieldName(terrain, selectedTimeOfDay);
         if (!wild.HasField(field)) return false;
         return wild.GetAddress(field) != Pointer.NULL;
      }

      private (int rate, byte minLevel, byte maxLevel, string species, int fallbackSpecies, int count) Defaults(string terrain) {
         bool frlg = model.IsFRLG();
         return terrain switch {
            Grass => (20, 2, 3, frlg ? "RATTATA" : "ZIGZAGOON", frlg ? 19 : HasTimesOfDay ? 263 : 288, 12),
            Surf => (4, 5, 35, "TENTACOOL", 72, 5),
            Tree => (20, 10, 15, "GEODUDE", 74, 5),
            Fish => (30, 5, 10, "MAGIKARP", 129, 10),
            _ => (10, 5, 10, frlg ? "RATTATA" : "ZIGZAGOON", frlg ? 19 : 263, 3),
         };
      }

      private int FindSpecies(string name, int fallback) {
         var options = model.GetOptions(HardcodeTablesModel.GetSpeciesNameTable(model));
         if (options == null) return fallback;
         for (int i = 0; i < options.Count; i++) {
            if (string.Equals(options[i]?.Trim('"'), name, StringComparison.OrdinalIgnoreCase)) return i;
         }
         return fallback;
      }

      private void GotoData(string terrain) {
         if (wildDataIndex == int.MinValue) wildDataIndex = FindWildData();
         if (wildDataIndex == -1) return;
         var wild = model.GetTableModel(HardcodeTablesModel.WildTableName, tokenFactory)[wildDataIndex];
         var field = FieldName(terrain, selectedTimeOfDay);
         if (!wild.HasField(field)) return;
         var address = wild.GetAddress(field);
         tutorials.Complete(Tutorial.ToolbarButton_GotoWildData);

         ShowWildData = false;

         if (address != Pointer.NULL) {
            viewPort.Goto.Execute(address);
            return;
         }

         var (rate, minLevel, maxLevel, speciesName, fallbackSpecies, count) = Defaults(terrain);
         var subtableStart = model.FindFreeSpace(model.FreeSpaceStart, 8 + 4 * count);
         if (subtableStart < 0) {
            viewPort.RaiseError("Could not find free space for the new wild data.");
            return;
         }
         var dataStart = subtableStart + 8;
         var token = tokenFactory();

         // a new table for one time of day starts as a copy of the same terrain from another time of day
         int source = FindOtherTimeOfDay(wild, terrain, count);
         if (source != Pointer.NULL) {
            var sourceList = model.ReadPointer(source + 4);
            model.WriteMultiByteValue(subtableStart + 0, 4, token, model.ReadMultiByteValue(source, 4));
            model.WritePointer(token, subtableStart + 4, dataStart);
            for (int i = 0; i < count * 4; i++) token.ChangeData(model, dataStart + i, model[sourceList + i]);
         } else {
            var species = FindSpecies(speciesName, fallbackSpecies);
            model.WriteMultiByteValue(subtableStart + 0, 4, token, rate);
            model.WritePointer(token, subtableStart + 4, dataStart);
            for (int i = 0; i < count; i++) {
               token.ChangeData(model, dataStart + i * 4 + 0, minLevel);
               token.ChangeData(model, dataStart + i * 4 + 1, maxLevel);
               model.WriteMultiByteValue(dataStart + i * 4 + 2, 2, token, species);
            }
         }
         wild.SetAddress(field, subtableStart);
         viewPort.Goto.Execute(subtableStart);
         var when = HasTimesOfDay ? $" ({TimeNames[selectedTimeOfDay].ToLower()})" : string.Empty;
         viewPort.RaiseMessage($"New {terrain}{when} data was added at {subtableStart:X6}.");
         wildText = null;
         NotifyPropertiesChanged(nameof(WildSummary));
         NotifyExistsChanged();
      }

      /// <summary>Finds the address of an existing, readable table for this terrain at another time of day.</summary>
      private int FindOtherTimeOfDay(ModelArrayElement wild, string terrain, int count) {
         if (!HasTimesOfDay) return Pointer.NULL;
         for (int time = 0; time < TimeKeys.Length; time++) {
            if (time == selectedTimeOfDay) continue;
            var field = FieldName(terrain, time);
            if (!wild.HasField(field)) continue;
            var address = wild.GetAddress(field);
            if (address < 0 || address + 8 > model.Count) continue;
            var list = model.ReadPointer(address + 4);
            if (list < 0 || list + count * 4 > model.Count) continue;
            return address;
         }
         return Pointer.NULL;
      }

      public void ClearCache() {
         wildDataIndex = int.MinValue;
         wildText = null;
         hasTimesOfDay = null;
         NotifyPropertiesChanged(nameof(HasWildData), nameof(WildSummary), nameof(HasTimesOfDay), nameof(HasHidden), nameof(HasOverworldWildPokemonOption), nameof(OverworldWildPokemon));
         NotifyExistsChanged();
      }

      private void InformRepoint(DataMovedEventArgs e) {
         viewPort.RaiseMessage($"{e.Type} data was moved to {e.Address:X6}.");
      }
   }

}
