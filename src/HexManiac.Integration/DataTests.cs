using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.Visitors;
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
   }
}
