using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// A list of wild encounters (min level, max level, species) is edited one row per pokemon, one box per value,
   /// instead of as lines of text like "10, 15, Geodude".
   /// </summary>
   public class TableRowsStreamTests : BaseViewModelTestClass {
      private const int TableStart = 0x20, ListStart = 0x80;

      /// <summary>
      /// wild[rate unused*3 list] at 0x20: the list at 0x80 holds 3 encounters of 4 bytes: min level, max level, species (2 bytes).
      /// </summary>
      private TableRowsStreamElementViewModel CreateEncounterList(string listFormat = "[lowLevel. highLevel. species:names]3") {
         CreateTextTable("names", 0x100, "adam", "bob", "carl", "dave");
         var encounters = new[] { (5, 10, 1), (6, 12, 2), (7, 14, 3) };
         for (int i = 0; i < encounters.Length; i++) {
            Model[ListStart + i * 4 + 0] = (byte)encounters[i].Item1;
            Model[ListStart + i * 4 + 1] = (byte)encounters[i].Item2;
            Model[ListStart + i * 4 + 2] = (byte)encounters[i].Item3;
            Model[ListStart + i * 4 + 3] = 0;
         }
         WriteListPointer();
         ViewPort.Edit($"@{TableStart:X2} ^wild[rate. unused. unused. unused. list<{listFormat}>]1 ");
         ViewPort.Goto.Execute(TableStart);
         ViewPort.ChangeHistory.ChangeCompleted(); // the table now exists: a later edit of a cell is a separate step for Undo
         return ViewPort.Tools.TableTool.Children.OfType<TableRowsStreamElementViewModel>().Single();
      }

      /// <summary>
      /// The 'list' field of wild (right after the 'rate' and 3 unused bytes) is a pointer to the list. Written as raw bytes: a table is only given a child run when it is created over an existing pointer.
      /// </summary>
      private void WriteListPointer() {
         Model[TableStart + 4] = ListStart;
         Model[TableStart + 7] = 0x08;
      }

      private TableRowsStreamElementViewModel CurrentRows => ViewPort.Tools.TableTool.Children.OfType<TableRowsStreamElementViewModel>().Single();

      [Fact]
      public void EncounterList_TableTool_OneRowPerEncounterOneCellPerValue() {
         var rows = CreateEncounterList();

         Assert.Equal(3, rows.Rows.Count);
         Assert.Equal(new[] { "Min level", "Max level" }, rows.Columns.Take(2).Select(column => column.Header));
         Assert.Equal(3, rows.Columns.Count);
         var second = rows.Rows[1].Cells;
         Assert.Equal(new[] { "lowLevel", "highLevel", "species" }, second.Select(cell => cell.Name)); // left to right is the order Tab visits them
         Assert.Equal("6", ((NumericTableCellViewModel)second[0]).Content);
         Assert.Equal("12", ((NumericTableCellViewModel)second[1]).Content);
         Assert.Equal("carl", ((EnumTableCellViewModel)second[2]).FilteringComboOptions.DisplayText);
         Assert.Equal(new[] { ListStart + 4, ListStart + 5, ListStart + 6 }, second.Select(cell => cell.Start));
      }

      [Fact]
      public void EncounterList_UnusedFieldsGetNoBox() {
         var rows = CreateEncounterList("[lowLevel. highLevel. unused:]3"); // a list that has no species, and whose last two bytes are padding

         Assert.Equal(new[] { "lowLevel", "highLevel" }, rows.Rows[1].Cells.Select(cell => cell.Name));
         Assert.Equal(new[] { ListStart + 4, ListStart + 5 }, rows.Rows[1].Cells.Select(cell => cell.Start));
      }

      [Fact]
      public void ListWithoutLevels_TableTool_StillUsesTheTextEditor() {
         CreateTextTable("names", 0x100, "adam", "bob", "carl", "dave");
         WriteListPointer();
         ViewPort.Edit($"@{TableStart:X2} ^wild[rate. unused. unused. unused. list<[first. second. species:names]3>]1 ");
         ViewPort.Goto.Execute(TableStart);

         Assert.Empty(ViewPort.Tools.TableTool.Children.OfType<TableRowsStreamElementViewModel>());
         Assert.Single(ViewPort.Tools.TableTool.Children.OfType<TextStreamElementViewModel>());
      }

      [Fact]
      public void EditNumberCell_WritesTheDataAndTellsTheTableTool() {
         var rows = CreateEncounterList();
         var changes = 0;
         rows.DataChanged += (sender, e) => changes++;
         var cell = (NumericTableCellViewModel)rows.Rows[2].Cells[1];

         cell.Content = "20";

         Assert.Equal(20, Model[ListStart + 2 * 4 + 1]);
         Assert.Equal(1, changes);
         Assert.Contains("7, 20, dave", rows.Content);
      }

      [Fact]
      public void EditNumberCell_NotANumber_KeepsTheTextAndWritesNothing() {
         var rows = CreateEncounterList();
         var cell = (NumericTableCellViewModel)rows.Rows[0].Cells[0];

         cell.Content = "1x";

         Assert.Equal(5, Model[ListStart]);
         Assert.Equal("1x", cell.Content);
      }

      [Fact]
      public void EditNumberCell_HexNumber_IsWritten() {
         var rows = CreateEncounterList();

         ((NumericTableCellViewModel)rows.Rows[0].Cells[0]).Content = "0x0A";

         Assert.Equal(10, Model[ListStart]);
      }

      [Fact]
      public void NumberCell_UpAndDown_StayInTheRangeOfTheField() {
         var rows = CreateEncounterList();
         var cell = (NumericTableCellViewModel)rows.Rows[0].Cells[0];

         cell.Increment();
         Assert.Equal(6, Model[ListStart]);
         cell.Content = "255";
         cell.Increment();
         Assert.Equal(255, Model[ListStart]); // one byte
         cell.Content = "0";
         cell.Decrement();
         Assert.Equal(0, Model[ListStart]);
      }

      [Fact]
      public void EditSpeciesCell_FilterAndConfirm_WritesTheSpecies() {
         var rows = CreateEncounterList();
         var species = (EnumTableCellViewModel)rows.Rows[0].Cells[2];

         species.FilteringComboOptions.DisplayText = "dav"; // the box autocompletes while you type
         species.FilteringComboOptions.SelectConfirm();

         Assert.Equal(3, Model[ListStart + 2]);
         Assert.Equal("dave", species.FilteringComboOptions.DisplayText);
         Assert.Contains("5, 10, dave", rows.Content);
      }

      [Fact]
      public void EditCell_TableToolRefresh_KeepsTheSameBoxes() {
         var rows = CreateEncounterList();
         var cell = rows.Rows[1].Cells[0];

         ((NumericTableCellViewModel)cell).Content = "9";

         Assert.Same(rows, CurrentRows);
         Assert.Same(cell, CurrentRows.Rows[1].Cells[0]);
         Assert.Equal(9, Model[ListStart + 4]);
      }

      [Fact]
      public void ChangeDataFromOutside_TableToolRefresh_BoxesShowTheNewValuesButAreNotReplaced() {
         var rows = CreateEncounterList();
         var number = rows.Rows[1].Cells[0];
         var species = rows.Rows[1].Cells[2];

         Model[ListStart + 4] = 33;
         Model[ListStart + 6] = 1;
         ViewPort.Tools.TableTool.DataForCurrentRunChanged();

         Assert.Same(rows, CurrentRows);
         Assert.Same(number, CurrentRows.Rows[1].Cells[0]);
         Assert.Same(species, CurrentRows.Rows[1].Cells[2]);
         Assert.Equal("33", ((NumericTableCellViewModel)number).Content);
         Assert.Equal("bob", ((EnumTableCellViewModel)species).FilteringComboOptions.DisplayText);
      }

      [Fact]
      public void EditCell_Undo_BoxShowsTheOldValue() {
         var rows = CreateEncounterList();
         var cell = (NumericTableCellViewModel)rows.Rows[1].Cells[0];

         cell.Content = "9";
         ViewPort.Undo.Execute();

         Assert.Equal(6, Model[ListStart + 4]);
         Assert.Equal("6", ((NumericTableCellViewModel)CurrentRows.Rows[1].Cells[0]).Content);
      }

      [Fact]
      public void EncounterListWithComments_RowThatStartsAComment_ShowsIt() {
         // the tables of the original games say what the rows mean: the comment starts at the element that is named in it
         var rows = CreateEncounterList("[a|comment=0|common b|comment=2|very_rare lowLevel. highLevel. species:names]3");

         Assert.True(rows.HasRowLabels);
         Assert.Equal("common", rows.Rows[0].Label);
         Assert.Equal(string.Empty, rows.Rows[1].Label);
         Assert.Equal("very rare", rows.Rows[2].Label); // underscores stand for spaces
         Assert.Equal(new[] { "lowLevel", "highLevel", "species" }, rows.Rows[0].Cells.Select(cell => cell.Name));
      }

      [Fact]
      public void ColumnTitle_CamelCaseNames_AreReadable() {
         Assert.Equal("Min level", TableRowsStreamElementViewModel.GetColumnTitle("lowLevel"));
         Assert.Equal("Max level", TableRowsStreamElementViewModel.GetColumnTitle("maxLevel"));
         Assert.Equal("Pokémon", TableRowsStreamElementViewModel.GetColumnTitle("species"));
         Assert.Equal("Held item", TableRowsStreamElementViewModel.GetColumnTitle("heldItem"));
      }
   }
}
