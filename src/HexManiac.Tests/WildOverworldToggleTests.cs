using HavenSoft.HexManiac.Core.ViewModels.Map;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// CUBE: the wild Pokemon editor has an "Overworld wild Pokemon" checkbox per map, backed by the ROM bitmap data.maps.wild.overworld:
   /// byte [group * 32 + map / 8], bit (map % 8); a set bit turns the overworld Pokemon off for that map. All zero = on everywhere.
   /// </summary>
   public class WildOverworldToggleTests : BaseViewModelTestClass {
      private const int TableStart = 0x100, TableLength = WildPokemonViewModel.OverworldGroups * WildPokemonViewModel.OverworldBytesPerGroup;

      public WildOverworldToggleTests() : base(0x100 + TableLength + 0x100) { }

      private void CreateOverworldTable(int length = TableLength) {
         ViewPort.Edit($"@{TableStart:X3} ^{WildPokemonViewModel.OverworldTableName}[bits.]{length} ");
         ViewPort.ChangeHistory.ChangeCompleted();
      }

      private WildPokemonViewModel Wild(int group, int map) => new(ViewPort, new MapTutorialsViewModel(), group, map);

      [Fact]
      public void NoAnchor_NoCheckboxAndNothingIsWritten() {
         var wild = Wild(3, 10);

         Assert.False(wild.HasOverworldWildPokemonOption);
         Assert.True(wild.OverworldWildPokemon);

         wild.OverworldWildPokemon = false;

         Assert.True(wild.OverworldWildPokemon);
         Assert.All(Data, b => Assert.Equal(0, b));
      }

      [Fact]
      public void AnchorWithWrongLength_NoCheckbox() {
         CreateOverworldTable(16);

         var wild = Wild(0, 16);

         Assert.False(wild.HasOverworldWildPokemonOption);
         Assert.True(wild.OverworldWildPokemon);
      }

      [Fact]
      public void ZeroTable_CheckedByDefault() {
         CreateOverworldTable();

         var wild = Wild(0, 16);

         Assert.True(wild.HasOverworldWildPokemonOption);
         Assert.True(wild.OverworldWildPokemon);
      }

      [Fact]
      public void Uncheck_SetsOnlyThisMapsBit() {
         CreateOverworldTable();
         var wild = Wild(3, 10);
         var notified = 0;
         wild.PropertyChanged += (sender, e) => { if (e.PropertyName == nameof(wild.OverworldWildPokemon)) notified++; };

         wild.OverworldWildPokemon = false;

         Assert.False(wild.OverworldWildPokemon);
         Assert.Equal(1, notified);
         var target = TableStart + 3 * 32 + 10 / 8;
         Assert.Equal(1 << (10 % 8), Data[target]);
         for (int i = 0; i < TableLength; i++) {
            if (TableStart + i != target) Assert.Equal(0, Data[TableStart + i]);
         }
         Assert.True(Wild(3, 11).OverworldWildPokemon);  // same byte, other bit
         Assert.True(Wild(2, 10).OverworldWildPokemon);  // same map number, other group
      }

      [Fact]
      public void CheckAgain_ClearsTheBitAndKeepsNeighbours() {
         CreateOverworldTable();
         Wild(3, 11).OverworldWildPokemon = false;
         var wild = Wild(3, 10);
         wild.OverworldWildPokemon = false;

         wild.OverworldWildPokemon = true;

         Assert.True(wild.OverworldWildPokemon);
         Assert.False(Wild(3, 11).OverworldWildPokemon);
         Assert.Equal(1 << 3, Data[TableStart + 3 * 32 + 1]);
      }

      [Fact]
      public void Uncheck_CanBeUndone() {
         CreateOverworldTable();
         var wild = Wild(5, 200);

         wild.OverworldWildPokemon = false;
         ViewPort.Undo.Execute(null);

         Assert.True(wild.OverworldWildPokemon);
         Assert.Equal(0, Data[TableStart + 5 * 32 + 200 / 8]);
      }

      [Fact]
      public void LastGroupAndMap_UseTheLastBit() {
         CreateOverworldTable();

         Wild(255, 255).OverworldWildPokemon = false;

         Assert.Equal(0x80, Data[TableStart + TableLength - 1]);
      }

      [Fact]
      public void SettingTheSameValue_WritesNothing() {
         CreateOverworldTable();
         var wild = Wild(1, 1);

         wild.OverworldWildPokemon = true;

         Assert.True(wild.OverworldWildPokemon);
         Assert.Equal(0, Data[TableStart + 32]);
      }
   }
}
