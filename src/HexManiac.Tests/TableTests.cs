using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class TableTests : BaseViewModelTestClass {
      [Fact]
      public void TableCanHaveNumericLimitOnField() {
         ViewPort.Edit("^table[data.4]8 ");
         Assert.Empty(Errors);

         ViewPort.SelectionStart = new Point(2, 0);
         ViewPort.Edit("5 "); // you should still be able to manually enter bad values
         Assert.Equal(5, Model[0x02]);

         // a combobox is used for numeric limit fields
         ViewPort.Tools.TableTool.Children.Single(child => child is ComboBoxArrayElementViewModel);
      }

      [Fact]
      public void TrainerPokemonTeamEnumSelectionSelectsEntireEnum() {
         ArrangeTrainerPokemonTeamData(0, 1);

         ViewPort.SelectionStart = new Point(0, 6);

         Assert.Equal(new Point(1, 6), ViewPort.SelectionEnd);
      }

      [Fact]
      public void HexEditingWorksForTrainerPokemon() {
         ArrangeTrainerPokemonTeamData(0, 1);

         ViewPort.SelectionStart = new Point(TrainerPokemonTeamRun.PokemonFormat_PokemonStart, 6);
         ViewPort.Edit("C ");

         Assert.Equal(2, Model[0x60 + TrainerPokemonTeamRun.PokemonFormat_PokemonStart]);
      }

      [Fact]
      public void CanExtendTrainerTeamViaStream() {
         ArrangeTrainerPokemonTeamData(0, 1);
         Model[0x6C] = 0x0B; // add a random data value so that extending will cause moving

         ViewPort.SelectionStart = new Point(0, 6);
         var streamTool = ViewPort.Tools.StringTool;
         streamTool.Content = $"10 A{Environment.NewLine}10 B";

         Assert.NotEqual(0x60, Model.ReadPointer(0x24));
      }

      [Fact]
      public void CanContractTrainerTeamViaStream() {
         ArrangeTrainerPokemonTeamData(0, 4);

         ViewPort.SelectionStart = new Point(0, 6);
         var streamTool = ViewPort.Tools.StringTool;
         streamTool.Content = $"10 A";

         Assert.Equal(1, Model[TrainerPokemonTeamRun.TrainerFormat_PokemonCountOffset]);
      }

      [Fact]
      public void AppendViaTableToolUpdatesParent() {
         ArrangeTrainerPokemonTeamData(0, 2, 1);

         ViewPort.Edit("@A8 "); // select last pokemon
         var tableTool = ViewPort.Tools.TableTool;
         tableTool.Append.Execute();

         Assert.Equal(3, Model[TrainerPokemonTeamRun.TrainerFormat_PokemonCountOffset]);
      }

      [Fact]
      public void ChangePokemonCountUpdatesTrainerTeam() {
         ArrangeTrainerPokemonTeamData(0, 2, 2);

         // update count via table: child run should update
         var tool = ViewPort.Tools.TableTool;
         var childCountField = (FieldArrayElementViewModel)tool.Children[tool.Children.Count - 3];
         childCountField.Content = "3";
         Assert.Equal(3, ((ITableRun)Model.GetNextRun(0xA0)).ElementCount);

         // update count via inline: child run should update
         ViewPort.Edit("@20 1 ");
         Assert.Equal(1, ((ITableRun)Model.GetNextRun(0xA0)).ElementCount);
      }

      [Fact]
      public void ChangePokemonCountChangesOtherTeamsWithSamePointer() {
         ArrangeTrainerPokemonTeamData(0, 2, 2);
         ViewPort.Edit("@4C <0A0>");

         // update count via table: sibling should update
         var tool = ViewPort.Tools.TableTool;
         var childCountField = (FieldArrayElementViewModel)tool.Children[tool.Children.Count - 3];
         childCountField.Content = "3";
         Assert.Equal(3, Model[0x48]);

         // update count via inline: sibling should update
         ViewPort.Edit("@20 1 ");
         Assert.Equal(1, Model[0x48]);
      }

      [Fact]
      public void ChangeStructTypeChangesTrainerTeam() {
         ArrangeTrainerPokemonTeamData(0, 2, 2);

         // update count via table: child run should update
         var tool = ViewPort.Tools.TableTool;
         var childCountField = (ComboBoxArrayElementViewModel)tool.Children[0];
         childCountField.SelectedIndex = 3;
         Assert.Equal(0x10, ((ITableRun)Model.GetNextRun(0xA0)).ElementLength);

         // update count via inline: child run should update
         ViewPort.Edit("@00 2 ");
         Assert.Equal(0x08, ((ITableRun)Model.GetNextRun(0xA0)).ElementLength);
      }

      [Fact]
      public void ChangeStructTypeChangesOtherTeamsWithSamePointer() {
         ArrangeTrainerPokemonTeamData(0, 2, 2);
         ViewPort.Edit("@4C <0A0>");

         // update count via table: sibling should update
         var tool = ViewPort.Tools.TableTool;
         var childCountField = (ComboBoxArrayElementViewModel)tool.Children[0];
         childCountField.SelectedIndex = 3;
         Assert.Equal(3, Model[0x28]);

         // update count via inline: sibling should update
         ViewPort.Edit("@00 2 ");
         Assert.Equal(2, Model[0x28]);
      }

      [Fact]
      public void SimplifyStructTypeClearsExtraUnusedData() {
         ArrangeTrainerPokemonTeamData(TrainerPokemonTeamRun.INCLUDE_MOVES, 2, 1);
         ViewPort.Edit("@A0 ");
         var tool = ViewPort.Tools.StringTool;

         tool.Content = "10 A";

         Assert.Equal(0, Model[TrainerPokemonTeamRun.TrainerFormat_StructTypeOffset]); // the parent updated to type 0
         for (int i = 8; i < 0x20; i++) { // 0x10 per pokemon for 2 pokemon
            Assert.Equal(0xFF, Model[0xA0 + i]); // the child is just 8 bytes long but used to be 0x20 bytes long, so bytes 8 to 0x20 are all FF
         }
      }

      [Fact]
      public void ChangingTypeFromMovesToMovesAndItemsKeepsSameMoves() {
         ArrangeTrainerPokemonTeamData(TrainerPokemonTeamRun.INCLUDE_MOVES, 1, 1);
         ViewPort.Edit("@A0 ");
         var tool = ViewPort.Tools.StringTool;

         tool.Content = @"10 A
- w
- x
- y
- q";

         ViewPort.Edit("@00 3 @trainertable/0/pokemon/0 ");

         var moves = tool.Content.Split(new[] { Environment.NewLine }, 2, StringSplitOptions.None)[1];
         Assert.Equal(@"- w
- x
- y
- q
", moves);
      }

      private void ArrangeTrainerPokemonTeamData(byte structType, byte pokemonCount, int trainerCount) {
         CreateTextTable(EggMoveRun.PokemonNameTable, 0x180, "ABCDEFGHIJKLMNOP".Select(c => c.ToString()).ToArray());
         CreateTextTable(EggMoveRun.MoveNamesTable, 0x1B0, "qrstuvwxyz".Select(c => c.ToString()).ToArray());
         CreateTextTable(HardcodeTablesModel.ItemsTableName, 0x1D0, "0123456789".Select(c => c.ToString()).ToArray());

         // trainers start at 00. There is room for up to 4.
         // teams start at A0.    There is room for up to 3 pokemon on each team.
         for (int i = 0; i < trainerCount; i++) {
            var initialOffset = i * TrainerPokemonTeamRun.TrainerFormat_Width;
            Model[initialOffset + TrainerPokemonTeamRun.TrainerFormat_StructTypeOffset] = structType;
            Model[initialOffset + TrainerPokemonTeamRun.TrainerFormat_PokemonCountOffset] = pokemonCount;
            Model.WritePointer(new ModelDelta(), initialOffset + TrainerPokemonTeamRun.TrainerFormat_PointerOffset, 0xA0 + 0x30 * i);
         }

         ViewPort.Edit($"@00 ^trainertable[structType.4 class.trainerclassnames introMusic. sprite. name\"\"12 " +
            $"item1:{HardcodeTablesModel.ItemsTableName} item2:{HardcodeTablesModel.ItemsTableName} item3:{HardcodeTablesModel.ItemsTableName} item4:{HardcodeTablesModel.ItemsTableName} " +
            $"doubleBattle:: ai:: pokemonCount:: pokemon<{TrainerPokemonTeamRun.SharedFormatString}>]{trainerCount} @00");
         ViewPort.ResetAlignment.Execute();
      }

      private void ArrangeTrainerPokemonTeamData(byte structType, byte pokemonCount) {
         CreateTextTable(EggMoveRun.PokemonNameTable, 0x100, "ABCDEFGHIJKLMNOP".Select(c => c.ToString()).ToArray());
         CreateTextTable(EggMoveRun.MoveNamesTable, 0x140, "qrstuvwxyz".Select(c => c.ToString()).ToArray());
         CreateTextTable(HardcodeTablesModel.ItemsTableName, 0x180, "0123456789".Select(c => c.ToString()).ToArray());

         Model[TrainerPokemonTeamRun.TrainerFormat_StructTypeOffset] = structType;
         Model[TrainerPokemonTeamRun.TrainerFormat_PokemonCountOffset] = pokemonCount;
         Model.WritePointer(new ModelDelta(), TrainerPokemonTeamRun.TrainerFormat_PointerOffset, 0x60);

         ViewPort.Goto.Execute("00");
         ViewPort.SelectionStart = new Point(4, 2);
         ViewPort.Edit($"^trainertable[team<{TrainerPokemonTeamRun.SharedFormatString}>]1 ");

         ViewPort.Goto.Execute("00");
      }
   }
}
