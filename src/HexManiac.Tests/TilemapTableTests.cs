using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
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
         Assert.Equal("[value.]tilemap", table.FormatString);
      }

      [Fact]
      public void TilemapTable_NegativeWidthMargins_WidthReduced() {
         ViewPort.Edit("^table[value.]tilemap-1-0-1-0 ");

         var table = Model.GetTable("table");
         Assert.Equal(2, table.ElementLength);
         Assert.Equal(4, table.ElementCount);
         Assert.Equal(2, table.ElementContent.Count);
         Assert.Equal("[value.]tilemap-1+0-1+0", table.FormatString);
      }

      [Fact]
      public void TilemapTable_NegativeHeightMargins_HeightReduced() {
         ViewPort.Edit("^table[value.]tilemap-0-1-0-1 ");

         var table = Model.GetTable("table");
         Assert.Equal(4, table.ElementLength);
         Assert.Equal(2, table.ElementCount);
         Assert.Equal(4, table.ElementContent.Count);
         Assert.Equal("[value.]tilemap+0-1+0-1", table.FormatString);
      }

      [Fact]
      public void TilemapTable_CheckDataFormat_IsSpriteDecorator() {
         ViewPort.Edit("^table[value.]tilemap ");

         var anchor = (Anchor)ViewPort[0, 0].Format;
         var format = (SpriteDecorator)anchor.OriginalFormat;
         Assert.Equal(4, format.CellWidth);
         Assert.Equal(4, format.CellHeight);
         Assert.Equal(4 * 8, format.Pixels.PixelWidth);
         Assert.Equal(4 * 8, format.Pixels.PixelHeight);
      }

      [Fact]
      public void FormatWithLengthMultiplier_Parse_LargerTable() {
         ViewPort.Edit("^table[value.]tilemap*2-0-1-0-1 ");
         ViewPort.Width = 4;

         var table = Model.GetTable("table");
         Assert.Equal(4, table.ElementContent.Count);
         Assert.Equal(4, table.ElementCount);
         Assert.Equal("[value.]tilemap*2+0-1+0-1", table.FormatString);
      }
   }
}
