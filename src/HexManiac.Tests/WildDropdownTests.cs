using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// The species box of a wild encounter row is a drop-down list with hundreds of entries.
   /// Every change of the data makes the table tool refresh, so the box must only cause the refresh it needs:
   /// one when a species is picked, none while the list is open, none when the list is opened and closed again.
   /// </summary>
   public class WildDropdownTests : BaseViewModelTestClass {
      private const int TableStart = 0x20, ListStart = 0x80, NamesStart = 0x100;
      private int tableRefreshes;

      private TableTool TableTool => ViewPort.Tools.TableTool;
      private TableRowsStreamElementViewModel CurrentRows => TableTool.Children.OfType<TableRowsStreamElementViewModel>().Single();
      private int SpeciesOfRow(int row) => Model.ReadMultiByteValue(ListStart + row * 4 + 2, 2);

      /// <summary>Three encounters at 0x80, species 1, 2 and 3 of "names". The table tool shows them, and counts how often it refreshes from here on.</summary>
      private FilteringComboOptions CreateSpeciesBox(int row) {
         CreateTextTable("names", NamesStart, "adam", "bob", "carl", "dave");
         var encounters = new[] { (5, 10, 1), (6, 12, 2), (7, 14, 3) };
         for (int i = 0; i < encounters.Length; i++) {
            Model[ListStart + i * 4 + 0] = (byte)encounters[i].Item1;
            Model[ListStart + i * 4 + 1] = (byte)encounters[i].Item2;
            Model[ListStart + i * 4 + 2] = (byte)encounters[i].Item3;
            Model[ListStart + i * 4 + 3] = 0;
         }
         Model[TableStart + 4] = ListStart; // the pointer of the 'list' field, as raw bytes: a table only gets a child run when it is created over an existing pointer
         Model[TableStart + 7] = 0x08;
         ViewPort.Edit($"@{TableStart:X2} ^wild[rate. unused. unused. unused. list<[lowLevel. highLevel. species:names]3>]1 ");
         ViewPort.Goto.Execute(TableStart);
         ViewPort.ChangeHistory.ChangeCompleted();

         TableTool.PropertyChanged += (sender, e) => { if (e.PropertyName == nameof(TableTool.Children)) tableRefreshes++; };
         return SpeciesBoxOfRow(row);
      }

      private FilteringComboOptions SpeciesBoxOfRow(int row) => ((EnumTableCellViewModel)CurrentRows.Rows[row].Cells[2]).FilteringComboOptions;

      [Fact]
      public void OpenAndCloseWithoutChoosing_TheTableToolIsNotRefreshed() {
         var box = CreateSpeciesBox(1);

         box.DropDownIsOpen = true;
         box.DropDownIsOpen = false;

         Assert.Equal(0, tableRefreshes);
         Assert.Equal(2, SpeciesOfRow(1));
         Assert.Equal("carl", box.DisplayText);
      }

      [Fact]
      public void PickASpecies_IsWrittenAndTheTableToolRefreshesOnce() {
         var box = CreateSpeciesBox(1);

         box.DropDownIsOpen = true;
         box.SelectedIndex = 3;
         box.DropDownIsOpen = false;

         Assert.Equal(3, SpeciesOfRow(1));
         Assert.Equal("dave", box.DisplayText);
         Assert.Equal(1, tableRefreshes);
         Assert.Equal(new[] { 1, 3, 3 }, Enumerable.Range(0, 3).Select(SpeciesOfRow)); // the other rows are untouched
      }

      [Fact]
      public void TypeInTheOpenList_TheTableToolWaitsUntilTheListIsClosed() {
         var box = CreateSpeciesBox(0);

         box.DropDownIsOpen = true;
         box.DisplayText = "d";
         box.DisplayText = "da";
         box.DisplayText = "dav";

         Assert.Equal(0, tableRefreshes); // a refresh per keystroke would rebuild everything under the box while it is being typed in
         Assert.Single(box.FilteredOptions);

         box.SelectConfirm();

         Assert.Equal(3, SpeciesOfRow(0));
         Assert.Equal("dave", box.DisplayText);
         Assert.Equal(4, box.FilteredOptions.Count); // the filter is cleared again
         Assert.True(tableRefreshes <= 1, $"{tableRefreshes} refreshes for one choice");
      }

      [Fact]
      public void PickASpecies_TheBoxesAreKeptNotRebuilt() {
         var box = CreateSpeciesBox(2);
         var rows = CurrentRows;
         var row = rows.Rows[2];

         box.DropDownIsOpen = true;
         box.SelectedIndex = 0;
         box.DropDownIsOpen = false;

         // the window keeps the drop-down it was working with: replacing the boxes would throw away every list that was already built
         Assert.Same(rows, CurrentRows);
         Assert.Same(row, CurrentRows.Rows[2]);
         Assert.Same(box, SpeciesBoxOfRow(2));
         Assert.Equal("adam", SpeciesBoxOfRow(2).DisplayText);
      }

      [Fact]
      public void PickTwice_EveryPickIsOneRefresh() {
         var box = CreateSpeciesBox(1);

         box.DropDownIsOpen = true;
         box.SelectedIndex = 3;
         box.DropDownIsOpen = false;
         box.DropDownIsOpen = true;
         box.SelectedIndex = 1;
         box.DropDownIsOpen = false;

         Assert.Equal(1, SpeciesOfRow(1));
         Assert.Equal(2, tableRefreshes);
      }
   }
}
