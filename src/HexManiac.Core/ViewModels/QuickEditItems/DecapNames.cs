using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HavenSoft.HexManiac.Core.ViewModels.QuickEditItems {
   // TODO 3A72A0, 3A72A2 -> addresses in FireRed for making things plural -> BerrIES, ItemS
   public class DecapNames : IQuickEditItem {
      public string Name => "Decapitalize Names";

      public string Description => "Decapitalize names of Pokemon, Species, Moves, Abilities, Items, Trainers, Types, and Natures";

      public string WikiLink => "https://github.com/haven1433/HexManiacAdvance/wiki/Decapitalization-Explained";

      public event EventHandler CanRunChanged;

      public bool CanRun(IViewPort viewPort) => viewPort is ViewPort;

      public async Task<ErrorInfo> Run(IViewPort viewPortInterface) {
         if (viewPortInterface is not ViewPort viewPort) return ErrorInfo.NoError;
         var model = viewPort.Model;

         await viewPort.UpdateProgress(0);

         // text lists
         foreach (var tableName in new[] {
            HardcodeTablesModel.AbilityNamesTable,
            HardcodeTablesModel.ContestTypesTable,
            HardcodeTablesModel.DecorationsTableName,
            HardcodeTablesModel.DexInfoTableName,
            HardcodeTablesModel.ItemsTableName,
            HardcodeTablesModel.MoveNamesTable,
            HardcodeTablesModel.PokemonNameTable,
            HardcodeTablesModel.TrainerClassNamesTable,
            HardcodeTablesModel.TrainerTableName,
            HardcodeTablesModel.TypesTableName,
            "data.items.berry.stats",
            "data.pokemon.contest.stats",
            "data.pokemon.trades",
         }) {
            var tableAddress = model.GetAddressFromAnchor(viewPort.CurrentChange, -1, tableName);
            if (tableAddress == Pointer.NULL) continue;
            var tableRun = model.GetNextRun(tableAddress);
            if (tableRun.Start != tableAddress || tableRun is not ITableRun table) continue;
            var offset = 0;
            for (int i = 0; i < table.ElementContent.Count; i++) {
               if (table.ElementContent[i].Type == ElementContentType.PCS) {
                  for (int j = 0; j < table.ElementCount; j++) {
                     Decapitalize(model, viewPort.CurrentChange, table.Start + table.ElementLength * j + offset);
                  }
               }
               offset += table.ElementContent[i].Length;
            }
         }

         await viewPort.UpdateProgress(.1);

         // pointer-to-text lists
         foreach (var tableName in new[] {
            HardcodeTablesModel.MapNameTable,
            HardcodeTablesModel.NaturesTableName,
            "data.battle.text",
            "data.maps.dungeons.stats",
            "data.menus.text.options", // TODO the values (FireRed=3CC330): Slow/Mid/Fast, On/Off, Shift/Set, Mono/Stereo, Help/LR/L=A
            "data.menus.text.pc",
            "data.menus.text.pcoptions",
            "data.menus.text.pokemon",
            "data.pokedex.habitat.names",
            "data.pokemon.moves.fallback.names",
            "data.text.menu.pause",
            "data.text.menu.pokemon.options",
            "scripts.newgame.names.female",
            "scripts.newgame.names.male",
            "scripts.newgame.names.rival",
         }) {
            var tableAddress = model.GetAddressFromAnchor(viewPort.CurrentChange, -1, tableName);
            if (tableAddress == Pointer.NULL) continue;
            var tableRun = model.GetNextRun(tableAddress);
            if (tableRun.Start != tableAddress || tableRun is not ITableRun table) continue;
            for (int i = 0; i < table.ElementCount; i++) {
               var segment = table.ElementContent.FirstOfTypeOrDefault<ArrayRunPointerSegment>();
               var offset = table.ElementContent.Until(seg => seg == segment).Sum(seg => seg.Length);
               var destination = model.ReadPointer(table.Start + table.ElementLength * i + offset);
               Decapitalize(model, viewPort.CurrentChange, destination);
            }
         }

         // multichoice [options< [text<""> unused::]/count > count::]
         if (model.GetTable(HardcodeTablesModel.MultichoiceTableName) is ITableRun parent) {
            for (int i = 0; i < parent.ElementCount; i++) {
               var destination = model.ReadPointer(parent.Start + parent.ElementLength * i);
               if (model.GetNextRun(destination) is ITableRun child) {
                  for (int j = 0; j < child.ElementCount; j++) {
                     destination = model.ReadPointer(child.Start + child.ElementLength * j);
                     Decapitalize(model, viewPort.CurrentChange, destination);
                  }
               }
            }
         }

         // single strings
         foreach (var anchor in model.Anchors) {
            var run = model.GetNextRun(model.GetAddressFromAnchor(viewPort.CurrentChange, -1, anchor));
            if (run is PCSRun pcs) Decapitalize(model, viewPort.CurrentChange, pcs.Start);
         }

         await viewPort.UpdateProgress(.2);

         // POKéMON
         var findResults = model.Find(PCSString.Convert("POKéMON").Take(7).ToArray()).ToList();
         for (int i = 0; i < findResults.Count; i++) {
            await viewPort.UpdateProgress((double)i / findResults.Count);
            viewPort.CurrentChange.ChangeData(model, findResults[i], PCSString.Convert("Pokémon").Take(7).ToList());
         }

         viewPort.Refresh();
         return ErrorInfo.NoError;
      }

      public void TabChanged() => CanRunChanged?.Invoke(this, EventArgs.Empty);

      private void Decapitalize(IDataModel model, ModelDelta token, int address) {
         if (address < 0 || address >= model.Count) return;
         var textLength = PCSString.ReadString(model, address, true);
         if (textLength < 3) return;
         var text = PCSString.Convert(model, address, textLength).ToCharArray();
         for (int i = 1; i < text.Length; i++) {
            if (IsLetter(text[i - 1]) && IsCap(text[i])) {
               text[i] += (char)('a' - 'A');
            }
         }
         token.ChangeData(model, address, PCSString.Convert(new string(text)));
      }

      private bool IsLetter(char c) {
         return char.IsLetter(c);
      }

      private bool IsCap(char c) {
         return 'A' <= c && c <= 'Z';
      }
   }
}
