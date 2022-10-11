using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using System;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// Exists as an indirection layer for constructors called by all tests.
   /// Protects tests from changes in constructors.
   /// </summary>
   public class New {
      private readonly IFileSystem fs;
      private readonly Singletons singletons;

      public New(IFileSystem fs, Singletons singletons) {
         this.fs = fs;
         this.singletons = singletons;
      }

      public EditorViewModel EditorViewModel() => new EditorViewModel(fs, singletons.WorkDispatcher, false);

      public IDataModel PokemonModel(byte[] data, StoredMetadata metadata) => new PokemonModel(data, metadata, singletons);

      public IDataModel HardcodeTablesModel(byte[] rawData, StoredMetadata metadata = null) => new HardcodeTablesModel(singletons, rawData, metadata);

      public static IMetadataInfo EarliestVersionInfo => new StubMetadataInfo { VersionNumber = "0.0.1" };
   }
}
