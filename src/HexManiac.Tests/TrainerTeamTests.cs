using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class TrainerTeamTests : BaseViewModelTestClass {
      private readonly TrainerPokemonTeamRun run;

      public TrainerTeamTests() {
         CreateTextTable(HardcodeTablesModel.ItemsTableName, 0x100, "potion", "hyper potion", "rare candy", "masterball");
         CreateTextTable(HardcodeTablesModel.PokemonNameTable, 0x140, "bulbasaur", "farfetch'd", "nidoran \\sm", "mr. mime");
         CreateTextTable(HardcodeTablesModel.MoveNamesTable, 0x180, "tackle", "scratch", "hyper beam", "rest");
         run = new TrainerPokemonTeamRun(Model, 0, SortedSpan<int>.None);
      }

      [Fact]
      public void TrainerTeam_RequestAutocompletePokemonLevel_NoOptions() {
         var options = run.GetAutoCompleteOptions("1 bulbasaur (3)", 0, 1);

         Assert.Empty(options);
      }

      [Fact]
      public void TrainerTeam_RequestAutocompletePokemonSpecies_CorrectOptions() {
         var options = run.GetAutoCompleteOptions("1 i (3) @potion", 0, 3);

         Assert.Equal(2, options.Count);
         Assert.Equal("1 \"nidoran \\sm\" (3) @potion", options[0].LineText);
         Assert.Equal("\"mr. mime\"", options[1].Text);
      }

      [Fact]
      public void TrainerTeam_RequestAutocompletePokemonIVs_NoOptions() {
         var options = run.GetAutoCompleteOptions("1 i (3) @potion", 0, 6);

         Assert.Empty(options);
      }

      [Fact]
      public void TrainerTeam_RequestAutocompleteItem_CorrectOptions() {
         var options = run.GetAutoCompleteOptions("1 i (3) @potion", 0, 12);

         Assert.Equal(2, options.Count);
         Assert.Equal("1 i (3) @potion", options[0].LineText);
         Assert.Equal("\"hyper potion\"", options[1].Text);
      }

      [Fact]
      public void TrainerTeam_RequestAutocompleteMove_CorrectOptions() {
         var options = run.GetAutoCompleteOptions("- t", 0, 12);

         Assert.Equal(3, options.Count);
         Assert.Equal("- tackle", options[0].LineText);
         Assert.Equal("scratch", options[1].Text);
         Assert.Equal("rest", options[2].Text);
      }

      [Fact]
      public void TrainerData_Serialize_SeeIVLabel() {
         var parent = SetupTrainerTable(0x100, 1);
         parent.WriteValue(4, Model, ViewPort.CurrentChange, 0, "pokemonCount");
         parent.WritePointer(0x80, Model, ViewPort.CurrentChange, 0, "pokemon");
         var teamRun = new TrainerPokemonTeamRun(Model, 0x80, new SortedSpan<int>(0x100 + 36));
         Model.ObserveRunWritten(ViewPort.CurrentChange, teamRun);

         var text = teamRun.SerializeRun();

         Assert.Contains("(IVs=0)", text);
      }

      [Fact]
      public void TrainerData_DeserializeWithIVLabel_IVChanges() {
         var parent = SetupTrainerTable(0x100, 1);
         parent.WriteValue(4, Model, ViewPort.CurrentChange, 0, "pokemonCount");
         parent.WritePointer(0x80, Model, ViewPort.CurrentChange, 0, "pokemon");
         var teamRun = new TrainerPokemonTeamRun(Model, 0x80, new SortedSpan<int>(0x100 + 36));
         Model.ObserveRunWritten(ViewPort.CurrentChange, teamRun);

         var newRun = teamRun.DeserializeRun("1 bulbasaur (IVs=12) ", ViewPort.CurrentChange, false, false);

         Assert.InRange(newRun.ReadValue(Model, 0, "ivSpread"), 12 * 8, 12 * 8 + 7);
      }

      private ITableRun SetupTrainerTable(int address, int elementCount) {
         ViewPort.Goto.Execute(address);
         ViewPort.Edit($"^trainertable[structType. class. stuff: name\"\"12 items:: items:: doubleBattle:: ai:: pokemonCount:: pokemon<>]{elementCount} ");
         return Model.GetTable("trainertable");
      }
   }
}
