using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace HavenSoft.HexManiac.Core.ViewModels.Map {
   public class WildPokemonViewModel : ViewModelCore {
      public readonly IEditableViewPort viewPort;
      public readonly IDataModel model;
      public readonly Func<ModelDelta> tokenFactory;
      public readonly MapTutorialsViewModel tutorials;
      public readonly int bank, map;

      #region Top-Level Wild Data

      public int wildDataIndex = int.MinValue;
      public bool HasWildData {
         get {
            if (wildDataIndex == int.MinValue) wildDataIndex = FindWildData();
            return wildDataIndex != -1;
         }
      }
      public int FindWildData() {
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
      public bool showWildData;

      public string wildText;

      #endregion

      #region 4 Buttons for 4 Tables

      public bool GrassExists => Exists("grass");
      public bool SurfExists => Exists("surf");
      public bool TreeExists => Exists("tree");
      public bool FishingExists => Exists("fish");

      #endregion

      public bool Exists(string subtable) {
         if (wildDataIndex == int.MinValue) FindWildData();
         if (wildDataIndex == -1) return false;
         var wild = model.GetTableModel(HardcodeTablesModel.WildTableName)[wildDataIndex];
         return wild.GetAddress(subtable) != Pointer.NULL;
      }
   }
}
