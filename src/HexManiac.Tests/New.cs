using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using System;
using System.Collections.Generic;

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

      public EditorViewModel EditorViewModel() => new EditorViewModel(fs, singletons.WorkDispatcher, false) { AllowMultipleElementsPerLine = true };

      public IDataModel PokemonModel(byte[] data, StoredMetadata metadata) => new PokemonModel(data, metadata, singletons);

      public IDataModel HardcodeTablesModel(byte[] rawData, StoredMetadata metadata = null) => new HardcodeTablesModel(singletons, rawData, metadata);

      public PCSConverter PCSConverter(string gameCode) => new PCSConverter(gameCode, null);

      public static StoredList StoredList(string name, IReadOnlyList<string> contents, string hash = null) => new StoredList(name, contents, new Dictionary<int, string>(), hash);

      public static ValidationList ValidationList(string hash, IReadOnlyList<string> content) => new ValidationList(hash, content, new Dictionary<int, string>());

      public static IMetadataInfo EarliestVersionInfo => new StubMetadataInfo { VersionNumber = "0.0.1" };
   }
}
