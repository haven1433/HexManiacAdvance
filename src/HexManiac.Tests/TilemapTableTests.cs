using HavenSoft.HexManiac.Core.Models.Runs;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class TilemapTableTests : BaseViewModelTestClass {
      public TilemapTableTests() {
         SetFullModel(0xFF);

         // setup palette, tileset, and tilemap data
         ViewPort.Edit("@80!00(32) ^pal`ucp4`");
         ViewPort.Edit("@C0!lz(128) ^tileset`lzt4|pal`");
         ViewPort.Edit("@180!lz(32) ^tilemap`lzm4x4x4|tileset`");
         ViewPort.Goto.Execute(0);
      }

      [Fact]
      public void Tilemap_CreateTable_RunExists() {
         ViewPort.Edit("^table[value.]tilemap ");

         var table = Model.GetTable("table");
         Assert.IsType<TilemapTableRun>(table);
         Assert.Equal(4, table.ElementLength);
         Assert.Equal(4, table.ElementCount);
         Assert.Equal(4, table.ElementContent.Count);
      }
   }
}
