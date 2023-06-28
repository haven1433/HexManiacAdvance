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

            // create new entry
            var element = new ModelArrayElement(model, wildTable.Start, wildTable.ElementCount - 1, tokenFactory, wildTable);
            element.SetValue("bank", bank);
            element.SetValue("map", map);
            element.SetAddress("grass", Pointer.NULL);
            element.SetAddress("surf", Pointer.NULL);
            element.SetAddress("tree", Pointer.NULL);
            element.SetAddress("fish", Pointer.NULL);
            wildDataIndex = wildTable.ElementCount - 1;

            // bookkeeping
            if (wildTable.Start != originalStart) InformRepoint(new("Wild", wildTable.Start));
            NotifyPropertyChanged(nameof(HasWildData));
            viewPort.ChangeHistory.ChangeCompleted();
         }
      }

      private string wildText;
      public string WildSummary {
         get {
            if (wildText != null) return wildText;
            var wild = model.GetTableModel(HardcodeTablesModel.WildTableName);
            // grass<[rate:: list<>]1> surf<[rate:: list<>]1> tree<[rate:: list<>]1> fish<[rate:: list<>]1>
            var text = new StringBuilder();
            if (wildDataIndex < 0) return text.ToString();
            if (BuildWildTooltip(text, wild[wildDataIndex], "grass")) text.AppendLine();
            if (BuildWildTooltip(text, wild[wildDataIndex], "surf")) text.AppendLine();
            if (BuildWildTooltip(text, wild[wildDataIndex], "tree")) text.AppendLine();
            BuildWildTooltip(text, wild[wildDataIndex], "fish");
            wildText = text.ToString();
            wildText = text.TrimEnd().ToString();
            if (string.IsNullOrWhiteSpace(wildText)) wildText = "No Wild Pokemon (yet!)";
            return wildText;
         }
      }
      private static bool BuildWildTooltip(StringBuilder text, ModelArrayElement wild, string type) {
         // list<[low. high. species:]n>
         var terrain = wild.GetSubTable(type);
         if (terrain == null) return false;
         var list = terrain[0].GetSubTable("list");
         if (list == null) return false;
         text.Append(type);
         text.AppendLine(":");

         if (type == "fish") {
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
         if (!HasWildData) {
            
         }
         viewPort.Goto.Execute(wildTable.Start + wildTable.ElementLength * wildDataIndex);
         tutorials.Complete(Tutorial.ToolbarButton_GotoWildData);
      }, () => model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, HardcodeTablesModel.WildTableName) != Pointer.NULL);

      #endregion

      #region 4 Buttons for 4 Tables

      private StubCommand gotoGrass, gotoTree, gotoFishing, gotoSurf;

      public bool GrassExists => Exists("grass");
      public ICommand GotoGrass => StubCommand(ref gotoGrass, () => GotoData("grass", 20, 2, 3, model.IsFRLG() ? 19 : 288, 12)); // rattata, zigzagoon

      public bool SurfExists => Exists("surf");
      public ICommand GotoSurf => StubCommand(ref gotoSurf, () => GotoData("surf", 4, 5, 35, 72, 5)); // tentacool

      public bool TreeExists => Exists("tree");
      public ICommand GotoTree => StubCommand(ref gotoTree, () => GotoData("tree", 20, 10, 15, 74, 5)); // geodude

      public bool FishingExists => Exists("fish");
      public ICommand GotoFishing => StubCommand(ref gotoFishing, () => GotoData("fish", 30, 5, 10, 129, 10)); // magikarp

      #endregion

      private bool Exists(string subtable) {
         if (wildDataIndex == int.MinValue) FindWildData();
         if (wildDataIndex == -1) return false;
         var wild = model.GetTableModel(HardcodeTablesModel.WildTableName)[wildDataIndex];
         return wild.GetAddress(subtable) != Pointer.NULL;
      }

      private void GotoData(string subtable, int rate, byte minLevel, byte maxLevel, int species, int count) {
         if (wildDataIndex == int.MinValue) FindWildData();
         if (wildDataIndex == -1) return;
         var wild = model.GetTableModel(HardcodeTablesModel.WildTableName, tokenFactory)[wildDataIndex];
         var address = wild.GetAddress(subtable);
         tutorials.Complete(Tutorial.ToolbarButton_GotoWildData);

         ShowWildData = false;

         if (address != Pointer.NULL) {
            viewPort.Goto.Execute(address);
            return;
         }

         var subtableStart = model.FindFreeSpace(model.FreeSpaceStart, 8 + 4 * count);
         var dataStart = subtableStart + 8;
         var token = tokenFactory();

         model.WriteMultiByteValue(subtableStart + 0, 4, token, rate);
         model.WritePointer(token, subtableStart + 4, dataStart);
         for (int i = 0; i < count; i++) {
            token.ChangeData(model, dataStart + i * 4 + 0, minLevel);
            token.ChangeData(model, dataStart + i * 4 + 1, maxLevel);
            model.WriteMultiByteValue(dataStart + i * 4 + 2, 2, token, species);
         }
         wild.SetAddress(subtable, subtableStart);
         viewPort.Goto.Execute(subtableStart);
         viewPort.RaiseMessage($"New {subtable} data was added at {subtableStart:X6}.");
         wildText = null;
         NotifyPropertyChanged(nameof(WildSummary));
      }

      public void ClearCache() {
         wildDataIndex = int.MinValue;
         wildText = null;
         NotifyPropertiesChanged(nameof(HasWildData), nameof(WildSummary));
      }

      private void InformRepoint(DataMovedEventArgs e) {
         viewPort.RaiseMessage($"{e.Type} data was moved to {e.Address:X6}.");
      }
   }

}
