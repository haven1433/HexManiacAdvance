using HavenSoft.HexManiac.Core.Models;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class BadFileLoadTests {
      private static readonly Singletons singletons = new Singletons();

      [Theory]
      [InlineData(HardcodeTablesModel.Ruby)]
      [InlineData(HardcodeTablesModel.Sapphire)]
      [InlineData(HardcodeTablesModel.Ruby1_1)]
      [InlineData(HardcodeTablesModel.Sapphire1_1)]
      [InlineData(HardcodeTablesModel.FireRed)]
      [InlineData(HardcodeTablesModel.LeafGreen)]
      [InlineData(HardcodeTablesModel.FireRed1_1)]
      [InlineData(HardcodeTablesModel.LeafGreen1_1)]
      [InlineData(HardcodeTablesModel.Emerald)]
      public void HardcodeTableModelCanLoadEvenAllBlank(string gamecode) {
         var data = new byte[0x100];
         data[0xAC + 0] = (byte)gamecode[0];
         data[0xAC + 1] = (byte)gamecode[1];
         data[0xAC + 2] = (byte)gamecode[2];
         data[0xAC + 3] = (byte)gamecode[3];

         var model = new HardcodeTablesModel(singletons, data);
      }
   }
}
