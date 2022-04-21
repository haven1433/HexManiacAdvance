using HavenSoft.HexManiac.Core.Models;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// Exists as an indirection layer for constructors called by all tests.
   /// Protects tests from changes in constructors.
   /// </summary>
   public static class New {
      public static PokemonModel PokemonModel(byte[] data, StoredMetadata metadata, Singletons singletons) => new PokemonModel(data, metadata, singletons);
   }
}
