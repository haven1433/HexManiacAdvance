using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// Every row of a wild encounter list shows how often it is the one that appears.
   /// The game picks the slot from its number alone, so the odds depend only on the kind of list (land 12 slots, water / rock smash 5, fishing 10).
   /// </summary>
   public class WildEncounterChanceTests : BaseViewModelTestClass {
      private const int TableStart = 0x20, AreaTables = 0x60, Lists = 0x100, NamesStart = 0x400;

      public WildEncounterChanceTests() : base(0x800) { }

      /// <summary>
      /// Builds a table like the one for the wild encounters of CUBE (and the original games): one element per map,
      /// header (bank, map) and one pointer per kind of encounter to a [rate, list] table, which points to the list itself.
      /// The table tool shows the lists of the first element.
      /// </summary>
      private void CreateWildTable(params (string name, int slots)[] areas) {
         CreateTextTable("names", NamesStart, "adam", "bob", "carl", "dave");
         CreateWildTableData(areas);
         ViewPort.Edit($"@{TableStart:X2} ^wild[bank. map. unused: {FormatOf(areas)}]1 ");
         ViewPort.Goto.Execute(TableStart);
         ViewPort.ChangeHistory.ChangeCompleted();
      }

      private static string FormatOf((string name, int slots)[] areas) {
         return string.Join(" ", areas.Select(area => $"{area.name}<[rate. unused. unused. unused. list<[lowLevel. highLevel. species:names]{area.slots}>]1>"));
      }

      /// <summary>Writes the pointers, the [rate, list] tables and the lists of the first element.</summary>
      private void CreateWildTableData(params (string name, int slots)[] areas) {
         for (int a = 0; a < areas.Length; a++) {
            var areaTable = AreaTables + a * 0x10;
            var list = Lists + a * 0x40;
            Model.WritePointer(Token, TableStart + 4 + a * 4, areaTable);
            Model[areaTable] = 20; // rate
            Model.WritePointer(Token, areaTable + 4, list);
            for (int i = 0; i < areas[a].slots; i++) {
               Model[list + i * 4 + 0] = (byte)(5 + i);
               Model[list + i * 4 + 1] = (byte)(10 + i);
               Model[list + i * 4 + 2] = (byte)(i % 4);
               Model[list + i * 4 + 3] = 0;
            }
         }
      }

      private System.Collections.Generic.List<TableRowsStreamElementViewModel> RowEditors => ViewPort.Tools.TableTool.Children.OfType<TableRowsStreamElementViewModel>().ToList();

      private static string[] ChancesOf(TableRowsStreamElementViewModel rows) => rows.Rows.Select(row => row.Chance).ToArray();
      private static string[] LabelsOf(TableRowsStreamElementViewModel rows) => rows.Rows.Select(row => row.Label).ToArray();

      [Fact]
      public void LandList_EveryRowShowsItsChance() {
         CreateWildTable(("morningGrass", 12));

         var rows = Assert.Single(RowEditors);

         Assert.True(rows.HasChances);
         Assert.Equal(new[] { "20%", "20%", "10%", "10%", "10%", "10%", "5%", "5%", "4%", "4%", "1%", "1%" }, ChancesOf(rows));
         Assert.All(rows.Rows, row => Assert.True(row.ShowChance));
         Assert.False(rows.HasRowLabels); // there is nothing else to say about a land list
      }

      [Fact]
      public void WaterAndRockSmashLists_FiveSlots_ShareTheirChances() {
         CreateWildTable(("daySurf", 5), ("dayTree", 5));

         var lists = RowEditors;

         Assert.Equal(2, lists.Count);
         foreach (var rows in lists) Assert.Equal(new[] { "60%", "30%", "5%", "4%", "1%" }, ChancesOf(rows));
      }

      [Fact]
      public void FishingList_ChancesAndTheNameOfEachRod() {
         CreateWildTable(("dayFish", 10));

         var rows = Assert.Single(RowEditors);

         Assert.Equal(new[] { "70%", "30%", "60%", "20%", "20%", "40%", "40%", "15%", "4%", "1%" }, ChancesOf(rows));
         Assert.True(rows.HasRowLabels);
         Assert.Equal(new[] { "Old rod", "", "Good rod", "", "", "Super rod", "", "", "", "" }, LabelsOf(rows));
      }

      [Fact]
      public void SeveralKindsInOneMap_EachListGetsItsOwnChances() {
         CreateWildTable(("grass", 12), ("surf", 5), ("tree", 5), ("fish", 10));

         var lists = RowEditors;

         Assert.Equal(new[] { 12, 5, 5, 10 }, lists.Select(rows => rows.Rows.Count));
         Assert.Equal("20%", lists[0].Rows[0].Chance);
         Assert.Equal("60%", lists[1].Rows[0].Chance);
         Assert.Equal("60%", lists[2].Rows[0].Chance);
         Assert.Equal("70%", lists[3].Rows[0].Chance);
         Assert.Equal("Super rod", lists[3].Rows[5].Label);
      }

      [Fact]
      public void NameDoesNotSayWhatTheListIs_TheNumberOfSlotsDecides() {
         CreateWildTable(("encounters", 12));

         var rows = Assert.Single(RowEditors);

         Assert.Equal("20%", rows.Rows[0].Chance);
         Assert.Equal("1%", rows.Rows[11].Chance);
      }

      [Fact]
      public void NameDoesNotMatchTheNumberOfSlots_NoChancesAreMadeUp() {
         CreateWildTable(("dayFish", 5));

         var rows = Assert.Single(RowEditors);

         Assert.False(rows.HasChances);
         Assert.All(rows.Rows, row => Assert.Equal(string.Empty, row.Chance));
         Assert.All(rows.Rows, row => Assert.False(row.ShowChance));
      }

      [Fact]
      public void HiddenList_HasNoFixedChances() {
         CreateWildTable(("dayHidden", 3));

         var rows = Assert.Single(RowEditors);

         Assert.False(rows.HasChances);
         Assert.Equal(3, rows.Rows.Count); // still edited as three rows of boxes, just without a chance column
      }

      [Fact]
      public void ListThatIsNotAWildEncounterSize_LooksTheSameAsBefore() {
         CreateWildTable(("unknown", 7));

         var rows = Assert.Single(RowEditors);

         Assert.False(rows.HasChances);
         Assert.False(rows.HasRowLabels);
         Assert.Equal(new[] { "lowLevel", "highLevel", "species" }, rows.Rows[0].Cells.Select(cell => cell.Name));
      }

      [Fact]
      public void ChanceColumn_EditingABox_KeepsTheChances() {
         CreateWildTable(("morningGrass", 12));
         var rows = Assert.Single(RowEditors);
         var cell = (NumericTableCellViewModel)rows.Rows[3].Cells[0];

         cell.Content = "33";

         var after = Assert.Single(RowEditors);
         Assert.Same(rows, after);
         Assert.Equal("10%", after.Rows[3].Chance);
         Assert.Equal(33, Model[Lists + 3 * 4]);
      }

      [Fact]
      public void ChanceColumn_NextMapHasNoGrassList_RowsAreRebuiltWithTheChancesOfTheListThatTookItsPlace() {
         // the first map has a grass list and a surf list, the second only the surf list: in the table tool, the first editor changes from 12 rows to 5
         CreateTextTable("names", NamesStart, "adam", "bob", "carl", "dave");
         CreateWildTableData(("grass", 12), ("surf", 5));
         Model.WritePointer(Token, TableStart + 0xC + 8, AreaTables + 0x10); // second element: no grass (null), the same surf area table
         ViewPort.Edit($"@{TableStart:X2} ^wild[bank. map. unused: {FormatOf(new[] { ("grass", 12), ("surf", 5) })}]2 ");
         ViewPort.Goto.Execute(TableStart);
         var first = RowEditors;
         Assert.Equal(new[] { 12, 5 }, first.Select(rows => rows.Rows.Count));

         ViewPort.Goto.Execute(TableStart + 0xC);

         var second = RowEditors;
         Assert.Equal(new[] { 5 }, second.Select(rows => rows.Rows.Count));
         Assert.Equal(new[] { "60%", "30%", "5%", "4%", "1%" }, ChancesOf(second[0]));
      }

      [Fact]
      public void CommentThatOnlySaysThePercent_IsNotShownTwice() {
         // the table of the original games comments the first row of each tier with "20%", "10%", ...: that is what the chance column says for every row.
         // A '%' can't be typed into an anchor, so the table comes from metadata, the way the toml files of the original tool describe it.
         var data = new byte[0x800];
         WritePointer(data, TableStart + 4, AreaTables);
         WritePointer(data, AreaTables + 4, Lists);
         var format = "[a|comment=0|20% b|comment=2|10% lowLevel. highLevel. species:names]12";
         Model.Load(data, new StoredMetadata(anchors: new[] {
            new StoredAnchor(NamesStart, "names", "[name\"\"5]4"),
            new StoredAnchor(TableStart, "wild", $"[bank. map. unused: grass<[rate. unused. unused. unused. list<{format}>]1>]1"),
         }));
         ViewPort.Goto.Execute(TableStart);

         var rows = Assert.Single(RowEditors);

         Assert.True(rows.HasChances);
         Assert.Equal("20%", rows.Rows[0].Chance);
         Assert.False(rows.HasRowLabels);
         Assert.Equal(string.Empty, rows.Rows[0].Label);
         Assert.Equal(string.Empty, rows.Rows[2].Label);
      }

      private static void WritePointer(byte[] data, int address, int destination) {
         data[address + 0] = (byte)destination;
         data[address + 1] = (byte)(destination >> 8);
         data[address + 2] = (byte)(destination >> 16);
         data[address + 3] = 0x08;
      }

      [Fact]
      public void CommentThatSaysSomethingElse_StaysAndTheChanceIsAdded() {
         CreateTextTable("names", NamesStart, "adam", "bob", "carl", "dave");
         Model.WritePointer(Token, TableStart + 4, AreaTables);
         Model.WritePointer(Token, AreaTables + 4, Lists);
         var format = "[a|comment=0|old_rod b|comment=2|good_rod lowLevel. highLevel. species:names]10";
         ViewPort.Edit($"@{TableStart:X2} ^wild[bank. map. unused: fish<[rate. unused. unused. unused. list<{format}>]1>]1 ");
         ViewPort.Goto.Execute(TableStart);

         var rows = Assert.Single(RowEditors);

         Assert.Equal("old rod", rows.Rows[0].Label);
         Assert.Equal("good rod", rows.Rows[2].Label);
         Assert.Equal("Super rod", rows.Rows[5].Label); // not named by the table: the standard name fills the gap
         Assert.Equal("70%", rows.Rows[0].Chance);
      }

      [Fact]
      public void KnownChances_AddUpToOneHundredPercentPerKindOfLand() {
         Assert.Equal(100, WildEncounterChances.Land.Sum());
         Assert.Equal(100, WildEncounterChances.Water.Sum());
         Assert.Equal(100, WildEncounterChances.RockSmash.Sum());
         Assert.Equal(100, WildEncounterChances.Fishing.Take(2).Sum()); // old rod
         Assert.Equal(100, WildEncounterChances.Fishing.Skip(2).Take(3).Sum()); // good rod
         Assert.Equal(100, WildEncounterChances.Fishing.Skip(5).Sum()); // super rod
      }

      [Theory]
      [InlineData("morningGrass", true, WildEncounterChances.Area.Land)]
      [InlineData("dayFish", true, WildEncounterChances.Area.Fishing)]
      [InlineData("eveningSurf", true, WildEncounterChances.Area.Water)]
      [InlineData("nightTree", true, WildEncounterChances.Area.RockSmash)]
      [InlineData("dayHidden", true, WildEncounterChances.Area.Hidden)]
      [InlineData("grass", true, WildEncounterChances.Area.Land)]
      [InlineData("list", false, WildEncounterChances.Area.Land)]
      [InlineData("", false, WildEncounterChances.Area.Land)]
      public void AreaName_IsRecognizedByTheWordInside(string name, bool known, WildEncounterChances.Area expected) {
         Assert.Equal(known, WildEncounterChances.TryGetArea(name, out var area));
         if (known) Assert.Equal(expected, area);
      }

      [Theory]
      [InlineData("20%", true)]
      [InlineData(" 5% ", true)]
      [InlineData("1%", true)]
      [InlineData("old_rod", false)]
      [InlineData("20% common", false)]
      [InlineData("%", false)]
      [InlineData("", false)]
      public void PercentComment_OnlyAPercentIsRedundant(string comment, bool expected) {
         Assert.Equal(expected, WildEncounterChances.IsPercentComment(comment));
      }
   }
}
