using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.Visitors;
using System.IO;
using Xunit;

namespace HavenSoft.HexManiac.Integration {
   public class DataTests : IntegrationTests{
      [SkippableFact]
      public void PokemonNameEnum_3ByteValue_NoTooltipError() {
         var firered = LoadReadOnlyFireRed();
         var pokenames = firered.Model.Get<ArrayRun>(HardcodeTablesModel.PokemonNameTable);

         ToolTipContentVisitor.GetEnumImage(firered.Model, new(), 1000000, pokenames);

         // no crash = pass
      }

      [SkippableFact]
      public void FireRed_ChangeLengthOfTypeNamesToS_NoAssert() {
         var firered = LoadFireRed();

         firered.Goto.Execute(HardcodeTablesModel.TypesTableName);
         var text = firered.AnchorText;
         text = text.Split("]")[0] + ']';
         firered.AnchorText = text + "s";

         // no debug assert -> pass
      }

      [SkippableFact]
      public void BrockScript_ExpandSelection_EntireScriptSelected() {
         var firered = LoadReadOnlyFireRed();
         firered.Goto.Execute("data.maps.banks/6/maps/2/map/0/events/0/objects/0/script/");

         firered.ExpandSelection(firered.SelectionStart.X, firered.SelectionStart.Y);

         var firstAddress = firered.ConvertViewPointToAddress(firered.SelectionStart);
         var lastAddress = firered.ConvertViewPointToAddress(firered.SelectionEnd);
         var length = lastAddress - firstAddress + 1;
         Assert.Equal(171, length);
      }

      [SkippableFact]
      public void UseTypeSwapScript_UpgradeVersion_TableStillIncluded() {
         var contents = File.ReadAllBytes("resources/scripts/ability_type_swaps.hma");
         var firered = LoadFireRed();
         firered.TryImport(new("script.hma", contents), FileSystem);

         var refTable = singletons.GameReferenceTables[firered.Model.GetGameCode()];
         var metadata = firered.Model.ExportMetadata(refTable, new StubMetadataInfo { VersionNumber = "0.5" });
         var newModel = new PokemonModel(firered.Model.RawData, metadata, singletons);

         var table = newModel.GetTableModel("data.abilities.typeswaps");
         Assert.NotNull(table);
      }

      [SkippableFact]
      public void DefaultMetadata_DefaultHashesForAllAnchors() {
         var firered = LoadFireRed();
         var refTable = singletons.GameReferenceTables[firered.Model.GetGameCode()];

         var metadata = firered.Model.ExportMetadata(refTable, singletons.MetadataInfo);

         Assert.All(metadata.NamedAnchors, anchor => Assert.NotEmpty(anchor.Hash));
      }
   }
}
