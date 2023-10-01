using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.Map;
using System;
using System.Collections.Generic;
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
            HardcodeTablesModel.AbilityDescriptionsTable,
            "data.battle.text",
            "data.maps.dungeons.stats",
            "data.menus.text.options", // TODO the values (FireRed=3CC330): Slow/Mid/Fast, On/Off, Shift/Set, Mono/Stereo, Help/LR/L=A
            "data.menus.text.pc",
            "data.menus.text.pcoptions",
            "data.menus.text.pokemon",
            "data.pokedex.habitat.names",
            "data.pokemon.moves.details.fallback.names",
            "data.text.menu.pause",
            "data.text.menu.pokemon.options",
            "scripts.newgame.names.female",
            "scripts.newgame.names.male",
            "scripts.newgame.names.rival",
            "data.maps.dungeons.stats",
            "scripts.text.names",
            "scripts.text.interviews",
            "scripts.text.destinations",
         }) {
            var tableAddress = model.GetAddressFromAnchor(viewPort.CurrentChange, -1, tableName);
            if (tableAddress == Pointer.NULL) continue;
            var tableRun = model.GetNextRun(tableAddress);
            if (tableRun.Start != tableAddress || tableRun is not ITableRun table) continue;
            for (int i = 0; i < table.ElementCount; i++) {
               foreach (ArrayRunPointerSegment segment in table.ElementContent.OfType<ArrayRunPointerSegment>()) {
                  if (segment.InnerFormat != "\"\"") continue;
                  var offset = table.ElementContent.Until(seg => seg == segment).Sum(seg => seg.Length);
                  var destination = model.ReadPointer(table.Start + table.ElementLength * i + offset);
                  Decapitalize(model, viewPort.CurrentChange, destination);
               }
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

         // "S" / "IES" additions for FR/LG
         var address = model.GetGameCode() switch {
            "BPRE0" => 0x06BDE8, "BPGE0" => 0x06BDE8,
            "BPRE1" => 0x06BDFC, "BPGE1" => 0x06BDFC,
            _ => -1
         };
         if (address >= 0) {
            var destination = model.ReadPointer(address);
            Decapitalize(model, viewPort.CurrentChange, destination, startLowercase: true);
            Decapitalize(model, viewPort.CurrentChange, destination + 2, startLowercase: true);
         }

         await viewPort.UpdateProgress(.2);

         // scripts
         var scriptAddresses = Flags.GetAllTopLevelScripts(model);
         var spots = Flags.GetAllScriptSpots(model, viewPort.Tools.CodeTool.ScriptParser, scriptAddresses, false, 0x0F, 0x67); // loadpointer, preparemsg
         foreach (var spot in spots) {
            var offset = spot.Line.LineCode[0] switch { 0x0F => 2, 0x67 => 1, _ => throw new NotImplementedException() };
            var textStart = model.ReadPointer(spot.Address + offset);
            Decapitalize(model, viewPort.CurrentChange, textStart);
         }

         await viewPort.UpdateProgress(.3);

         // POKéMON
         var findResults = model.Find(model.TextConverter.Convert("POKéMON", out var _).Take(7).ToArray()).ToList();
         for (int i = 0; i < findResults.Count; i++) {
            await viewPort.UpdateProgress((double)i / findResults.Count);
            viewPort.CurrentChange.ChangeData(model, findResults[i], model.TextConverter.Convert("Pokémon", out var _).Take(7).ToList());
         }

         viewPort.Refresh();
         return ErrorInfo.NoError;
      }

      public void TabChanged() => CanRunChanged?.Invoke(this, EventArgs.Empty);

      private void Decapitalize(IDataModel model, ModelDelta token, int address, bool startLowercase = false) {
         if (address < 0 || address >= model.Count) return;
         var textLength = PCSString.ReadString(model, address, true);
         if (textLength < 3 && !startLowercase) return;
         var (_A, _a) = (0xBB, 0xD5);
         var run = new PCSRun(model, address, textLength);
         var pcs0 = run.CreateDataFormat(model, address) as PCS;
         bool previousIsCap = false, previousIsSpecial = false;
         if (pcs0 != null) (previousIsCap, previousIsSpecial) = (IsCap(pcs0.ThisCharacter), IsSpecial(model, pcs0));
         int start = 1;
         if (startLowercase) {
            (previousIsCap, previousIsSpecial) = (false, true);
            start = 0;
         }
         for (int i = start; i < textLength; i++) {
            var format = run.CreateDataFormat(model, address + i);
            if (format is Anchor anchor) format = anchor.OriginalFormat;
            if (format is not PCS pcs) {
               previousIsCap = false;
               continue;
            }
            var isCap = IsCap(pcs.ThisCharacter);
            var isSpecial = IsSpecial(model, pcs);
            if ((previousIsCap || previousIsSpecial) && isCap) {
               token.ChangeData(model, address + i, (byte)(model[address + i] - _A + _a));
            }
            (previousIsCap, previousIsSpecial) = (isCap, isSpecial);
         }
      }

      private bool IsCap(string c) {
         if (c.StartsWith("\"")) c = c.Substring(1);
         if (c.Length != 1) return false;
         return 'A' <= c[0] && c[0] <= 'Z';
      }

      private bool IsSpecial(IDataModel model, PCS pcs) {
         var address = pcs.Source + pcs.Position;
         if (model[address] == 0x1B) return true; // é
         if (model[address] == 0xB4) return true; // '
         return false;
      }
   }
}
