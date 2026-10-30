using HavenSoft.HexManiac.Core.Models;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>CUBE has no data.pokemon.names table: the scripts (givePokemon species:data.pokemon.names) have to take the names from the stats table.</summary>
   public class SpeciesNameFallbackTests : BaseViewModelTestClass {
      public SpeciesNameFallbackTests() : base(0x400) { }

      [Fact]
      public void WithoutANameTable_TheStatsTableGivesTheNames() {
         ViewPort.Edit("@100 ^data.pokemon.stats[hp. name\"\"11]3 ");
         ViewPort.Edit("@100 10 \"ALPHA\" @10C 20 \"BETA\" @118 30 \"GAMMA\" ");
         var options = Model.GetOptions(HardcodeTablesModel.PokemonNameTable);
         Assert.Equal(3, options.Count);
         Assert.Equal("ALPHA", options[0]);
         Assert.Equal("BETA", options[1]);
      }

      [Fact]
      public void WithANameTable_ItWins() {
         ViewPort.Edit("@100 ^data.pokemon.stats[hp. name\"\"11]3 ");
         ViewPort.Edit("@100 10 \"ALPHA\" ");
         ViewPort.Edit("@200 ^data.pokemon.names[name\"\"11]3 \"ONE\" @20C \"TWO\" @218 \"THREE\" ");
         var options = Model.GetOptions(HardcodeTablesModel.PokemonNameTable);
         Assert.Equal("ONE", options[0]);
      }
   }
}
