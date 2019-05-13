using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class EggMoveTests {
      private readonly byte[] data;
      private readonly PokemonModel model;
      private readonly ViewPort viewPort;

      public EggMoveTests() {
         data = Enumerable.Range(0, 0x200).Select(i => (byte)0xFF).ToArray();
         model = new PokemonModel(data);
         viewPort = new ViewPort("file.gba", model);
      }

      [Fact]
      public void CanCreateEggMoveStream() {
         viewPort.Edit("^eggmoves`egg` ");

         Assert.Equal(2, model.GetNextRun(0).Length);
      }
   }
}
