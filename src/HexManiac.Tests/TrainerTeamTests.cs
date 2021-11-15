using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class TrainerTeamTests : BaseViewModelTestClass {
      private readonly TrainerPokemonTeamRun run;

      public TrainerTeamTests() {
         CreateTextTable(HardcodeTablesModel.ItemsTableName, 0x100, "potion", "hyper potion", "rare candy", "masterball", "go-goggles", "king's rock");
         CreateTextTable(HardcodeTablesModel.PokemonNameTable, 0x150, "bulbasaur", "farfetch'd", "nidoran \\sm", "mr. mime", "ho-oh");
         CreateTextTable(HardcodeTablesModel.MoveNamesTable, 0x1A0, "tackle", "scratch", "hyper beam", "rest", "name-with-dash");
         run = new TrainerPokemonTeamRun(Model, 0, false, SortedSpan<int>.None);
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

         Assert.Equal(4, options.Count);
         Assert.Equal("- tackle", options[0].LineText);
         Assert.Equal("scratch", options[1].Text);
         Assert.Equal("rest", options[2].Text);
         Assert.Equal("name-with-dash", options[3].Text);
      }

      [Fact]
      public void TrainerData_Serialize_SeeIVLabel() {
         var parent = SetupTrainerTable(0x100, 1);
         parent.WriteValue(4, Model, ViewPort.CurrentChange, 0, "pokemonCount");
         parent.WritePointer(0x80, Model, ViewPort.CurrentChange, 0, "pokemon");
         var teamRun = new TrainerPokemonTeamRun(Model, 0x80, false, new SortedSpan<int>(0x100 + 36));
         Model.ObserveRunWritten(ViewPort.CurrentChange, teamRun);

         var text = teamRun.SerializeRun();

         Assert.Contains("(IVs=0)", text);
      }

      [Fact]
      public void TrainerData_DeserializeWithIVLabel_IVChanges() {
         var parent = SetupTrainerTable(0x100, 1);
         parent.WriteValue(4, Model, ViewPort.CurrentChange, 0, "pokemonCount");
         parent.WritePointer(0x80, Model, ViewPort.CurrentChange, 0, "pokemon");
         var teamRun = new TrainerPokemonTeamRun(Model, 0x80, false, new SortedSpan<int>(0x100 + 36));
         Model.ObserveRunWritten(ViewPort.CurrentChange, teamRun);

         var newRun = teamRun.DeserializeRun("1 bulbasaur (IVs=12) ", ViewPort.CurrentChange, false, false);

         Assert.InRange(newRun.ReadValue(Model, 0, "ivSpread"), 12 * 8, 12 * 8 + 7);
      }

      [Fact]
      public void TrainerWithTwoPokemon_ChangePokemonCountToThree_PokemonTeamRepoints() {
         // write data for two trainers
         Model[TrainerTablePokemonCountOffset] = 2;
         Model.WritePointer(new ModelDelta(), TrainerTablePokemonPointerOffset, 0x80);
         WriteBasicTrainerPokmeon(0x80, 0, 5, 0);
         WriteBasicTrainerPokmeon(0x88, 0, 5, 0);
         Model[TrainerTableElementLength + TrainerTablePokemonCountOffset] = 2;
         Model.WritePointer(new ModelDelta(), TrainerTableElementLength + TrainerTablePokemonPointerOffset, 0x88);
         WriteBasicTrainerPokmeon(0x90, 0, 5, 0);
         WriteBasicTrainerPokmeon(0x98, 0, 5, 0);

         SetupTrainerTable(0, 2);

         // set the trainer to have 3 pokemon
         ViewPort.Refresh();
         var tool = (FieldArrayElementViewModel)ViewPort.Tools.TableTool.Children.Single(child => child is FieldArrayElementViewModel faevm && faevm.Name == "pokemonCount");
         tool.Content = "3";

         Assert.Single(Messages);
         Assert.NotEqual(0x80, Model.GetNextRun(0x80).Start); // run should've repointed
         Assert.NotEqual(0x80, Model.ReadPointer(TrainerTablePokemonPointerOffset));
      }

      [Fact]
      public void DashWithSpace_AutoComplete_NoOptions() {
         var options = run.GetAutoCompleteOptions("- ", 2, 2).ToList();
         Assert.Empty(options);
      }

      [Fact]
      public void DashSpaceDash_AutoComplete_HaveOptions() {
         var options = run.GetAutoCompleteOptions("- -", 3, 3).ToList();
         Assert.NotEmpty(options);
      }

      [Fact]
      public void DashInName_AutoComplete_AutoCompleteOptionsHaveDash() {
         var options = run.GetAutoCompleteOptions("- -", 3, 3).ToList();
         Assert.All(options, option => Assert.Contains("-", option.Text));
      }

      [Theory]
      [InlineData("-")]
      [InlineData("'")]
      [InlineData(".")]
      public void SpecialCharacterInPokemonName_AutoComplete_ListContainsOnlyElementsWithThatCharacter(string specialCharacter) {
         var options = run.GetAutoCompleteOptions("12 " + specialCharacter, 4, 4).ToList();
         Assert.NotEmpty(options);
         Assert.All(options, option => Assert.Contains(specialCharacter, option.Text));
      }

      [Theory]
      [InlineData("-")]
      [InlineData("'")]
      public void SpecialCharacterInItemName_AutoComplete_ListContainsOnlyElementsWithThatCharacter(string specialCharacter) {
         var options = run.GetAutoCompleteOptions("12 bulbsaur @" + specialCharacter, 14, 14).ToList();
         Assert.NotEmpty(options);
         Assert.All(options, option => Assert.Contains(specialCharacter, option.Text));
      }

      [Fact]
      public void TeamWithIVScalingOff_255IVs_Show255() {
         var run = new TrainerPokemonTeamRun(Model, 0, true, SortedSpan<int>.None);
         Model.WriteMultiByteValue(0, 2, Token, 255);

         Assert.IsNotType<ArrayRunTupleSegment>(run.ElementContent[0]);
         Assert.Contains("IVs=255", run.SerializeRun());
      }

      /// <summary>
      /// Basic trainer pokemon are 8 bytes
      /// </summary>
      private void WriteBasicTrainerPokmeon(int address, int ivs, int level, int species) {
         var token = new ModelDelta();
         Model.WriteMultiByteValue(address + 0, 2, token, ivs);
         Model.WriteMultiByteValue(address + 2, 2, token, level);
         Model.WriteMultiByteValue(address + 4, 2, token, species);
         Model.WriteMultiByteValue(address + 6, 2, token, 0);
      }

      const int TrainerTablePokemonCountOffset = 32;
      const int TrainerTablePokemonPointerOffset = 36;
      const int TrainerTableElementLength = 40;
      private ITableRun SetupTrainerTable(int address, int elementCount) {
         ViewPort.Goto.Execute(address);
         // 40 bytes per trainer
         //                                                      4          16      20      24             28   32             36
         ViewPort.Edit($"^trainertable[structType. class. stuff: name\"\"12 items:: items:: doubleBattle:: ai:: pokemonCount:: pokemon<`tpt`>]{elementCount} ");
         return Model.GetTable("trainertable");
      }
   }
}
