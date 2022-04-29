using HavenSoft.HexManiac.Core.Models;
using System;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// Exists as an indirection layer for constructors called by all tests.
   /// Protects tests from changes in constructors.
   /// </summary>
   public static class New {
      public static IDataModel PokemonModel(byte[] data, StoredMetadata metadata, Singletons singletons) => new PokemonModel(data, metadata, singletons);

      public static IDataModel HardcodeTablesModel(Singletons singletons, byte[] rawData, StoredMetadata metadata = null) => new HardcodeTablesModel(singletons, rawData, metadata);

      public static IMetadataInfo EarliestVersionInfo => new StubMetadataInfo { VersionNumber = "0.0.1" };
   }
}
