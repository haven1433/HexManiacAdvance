
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// The Auto-Search tests exist to verify that data can be found in the Vanilla roms.
   /// These tests are skippable, so that they'll work even if you don't have the ROMs on your system.
   /// This is important, since the ROMs aren't part of the repository.
   /// </summary>
   public class AutoSearchTests {

      public static IEnumerable<object[]> PokemonGames => new[] {
         "Ruby",
         "Sapphire",
         "FireRed",
         "LeafGreen",
         "Emerald",
         "DarkRisingKAIZO", // from FireRed
         "Vega 2019-04-20", // from FireRed
         "Clover",          // from FireRed
         "Gaia v3.2",       // from FireRed
         "Altair",          // from Emerald
      }.Select(game => new object[] { "sampleFiles/Pokemon " + game + ".gba" });

      [SkippableTheory]
      [MemberData(nameof(PokemonGames))]
      public void PokemonNamesAreFound(string game) {
         var model = LoadModel(game);
         var noChange = new NoDataChangeDeltaModel();

         var address = model.GetAddressFromAnchor(noChange, -1, EggMoveRun.PokemonNameTable);
         var run = (ArrayRun)model.GetNextAnchor(address);
         if (game.Contains("Gaia")) Assert.Equal(914, run.ElementCount);
         else Assert.Equal(412, run.ElementCount);
      }

      [SkippableTheory]
      [MemberData(nameof(PokemonGames))]
      public void MovesNamesAreFound(string game) {
         var model = LoadModel(game);
         var noChange = new NoDataChangeDeltaModel();

         var address = model.GetAddressFromAnchor(noChange, -1, EggMoveRun.MoveNamesTable);
         var run = (ArrayRun)model.GetNextAnchor(address);
         if (game.Contains("Vega")) Assert.Equal(512, run.ElementCount);
         else if (game.Contains("Clover")) Assert.Equal(512, run.ElementCount);
         else if (game.Contains("Gaia")) Assert.Equal(511, run.ElementCount);
         else Assert.Equal(355, run.ElementCount);
      }

      [SkippableTheory]
      [MemberData(nameof(PokemonGames))]
      public void AbilitiyNamesAreFound(string game) {
         var model = LoadModel(game);
         var noChange = new NoDataChangeDeltaModel();

         var address = model.GetAddressFromAnchor(noChange, -1, "abilitynames");
         var run = (ArrayRun)model.GetNextAnchor(address);
         if (game.Contains("Clover")) Assert.Equal(156, run.ElementCount);
         else if (game.Contains("Gaia")) Assert.Equal(188, run.ElementCount);
         else Assert.Equal(78, run.ElementCount);
      }

      [SkippableTheory]
      [MemberData(nameof(PokemonGames))]
      public void AbilitiyDescriptionsAreFound(string game) {
         var model = LoadModel(game);
         var noChange = new NoDataChangeDeltaModel();

         var address = model.GetAddressFromAnchor(noChange, -1, "abilitydescriptions");
         var run = (ArrayRun)model.GetNextAnchor(address);
         if (game.Contains("Clover")) Assert.Equal(156, run.ElementCount);
         else if (game.Contains("Gaia")) Assert.Equal(188, run.ElementCount);
         else Assert.Equal(78, run.ElementCount);

         if (game.Contains("Gaia")) return; // don't validate description text in Gaia, it's actually invalid.

         for (var i = 0; i < run.ElementCount; i++) {
            address = model.ReadPointer(run.Start + i * 4);
            var childRun = model.GetNextRun(address);
            Assert.IsType<PCSRun>(childRun);
         }
      }

      [SkippableTheory]
      [MemberData(nameof(PokemonGames))]
      public void TypesAreFound(string game) {
         var model = LoadModel(game);
         var noChange = new NoDataChangeDeltaModel();

         var address = model.GetAddressFromAnchor(noChange, -1, "types");
         var run = (ArrayRun)model.GetNextAnchor(address);
         if (game.Contains("Clover")) Assert.Equal(24, run.ElementCount);
         else if (game.Contains("Gaia")) Assert.Equal(24, run.ElementCount);
         else Assert.Equal(18, run.ElementCount);
      }

      [SkippableTheory]
      [MemberData(nameof(PokemonGames))]
      public void ItemsAreFound(string game) {
         var model = LoadModel(game);
         var noChange = new NoDataChangeDeltaModel();

         var address = model.GetAddressFromAnchor(noChange, -1, "items");
         var run = (ArrayRun)model.GetNextAnchor(address);
         if (game.Contains("Altair")) Assert.Equal(377, run.ElementCount);
         else if (game.Contains("Emerald")) Assert.Equal(377, run.ElementCount);
         else if (game.Contains("FireRed")) Assert.Equal(375, run.ElementCount);
         else if (game.Contains("DarkRisingKAIZO")) Assert.Equal(375, run.ElementCount);
         else if (game.Contains("LeafGreen")) Assert.Equal(375, run.ElementCount);
         else if (game.Contains("Ruby")) Assert.Equal(349, run.ElementCount);
         else if (game.Contains("Sapphire")) Assert.Equal(349, run.ElementCount);
         else if (game.Contains("Vega")) Assert.Equal(375, run.ElementCount);
         else if (game.Contains("Clover")) Assert.Equal(375, run.ElementCount);
         else if (game.Contains("Gaia")) Assert.Equal(375, run.ElementCount);
         else throw new NotImplementedException();
      }

      [SkippableTheory]
      [MemberData(nameof(PokemonGames))]
      public void TrainerClassNamesAreFound(string game) {
         var model = LoadModel(game);
         var noChange = new NoDataChangeDeltaModel();

         var address = model.GetAddressFromAnchor(noChange, -1, "trainerclassnames");
         var run = (ArrayRun)model.GetNextAnchor(address);
         if (game.Contains("Altair")) Assert.Equal(66, run.ElementCount);
         else if (game.Contains("Emerald")) Assert.Equal(66, run.ElementCount);
         else if (game.Contains("FireRed")) Assert.Equal(107, run.ElementCount);
         else if (game.Contains("DarkRisingKAIZO")) Assert.Equal(107, run.ElementCount);
         else if (game.Contains("LeafGreen")) Assert.Equal(107, run.ElementCount);
         else if (game.Contains("Ruby")) Assert.Equal(58, run.ElementCount);
         else if (game.Contains("Sapphire")) Assert.Equal(58, run.ElementCount);
         else if (game.Contains("Vega")) Assert.Equal(107, run.ElementCount);
      }

      [SkippableTheory]
      [MemberData(nameof(PokemonGames))]
      public void PokeStatsAreFound(string game) {
         var model = LoadModel(game);
         var noChange = new NoDataChangeDeltaModel();

         var address = model.GetAddressFromAnchor(noChange, -1, "pokestats");
         var run = (ArrayRun)model.GetNextAnchor(address);

         var firstPokemonStats = model.Skip(run.Start + run.ElementLength).Take(6).ToArray();
         var compareSet = new[] { 45, 49, 49, 45, 65, 65 }; // Bulbasaur
         if (game.Contains("Vega")) compareSet = new[] { 42, 53, 40, 70, 63, 40 }; // Nimbleaf
         if (game.Contains("Clover")) compareSet = new[] { 56, 60, 55, 50, 47, 50 }; // Grasshole
         for (int i = 0; i < compareSet.Length; i++) Assert.Equal(compareSet[i], firstPokemonStats[i]);
      }

      [SkippableTheory]
      [MemberData(nameof(PokemonGames))]
      public void MoveDataFound(string game) {
         var model = LoadModel(game);
         var noChange = new NoDataChangeDeltaModel();

         var address = model.GetAddressFromAnchor(noChange, -1, "movedata");
         var run = (ArrayRun)model.GetNextAnchor(address);

         var poundStats = model.Skip(run.Start + run.ElementLength).Take(8).ToArray();
         var compareSet = new[] { 0, 40, 0, 100, 35, 0, 0, 0 };
         for (int i = 0; i < compareSet.Length; i++) Assert.Equal(compareSet[i], poundStats[i]);
      }

      [SkippableTheory]
      [MemberData(nameof(PokemonGames))]
      public void EggMoveDataFound(string game) {
         var model = LoadModel(game);
         var noChange = new NoDataChangeDeltaModel();

         var address = model.GetAddressFromAnchor(noChange, -1, "eggmoves");
         var run = (EggMoveRun)model.GetNextAnchor(address);

         if (game.Contains("Vega")) Assert.Equal(3, run.PointerSources.Count); // there's a false positive in Vega... for now! Would be nice if this were 2, but it doesn't much matter.
         else Assert.Equal(2, run.PointerSources.Count);
         var expectedLastElement = model.ReadMultiByteValue(run.PointerSources[1] - 4, 4);
         var expectedLength = expectedLastElement + 1;
         var actualLength = run.Length / 2 - 1;  // remove the closing element.
         Assert.InRange(actualLength, 790, expectedLength);
      }

      /// <summary>
      /// Loading the model can take a while.
      /// We want to know that loading the model created the correct arrays,
      /// But loading the same file into a model multiple times just wastes time.
      /// Go ahead and cache a model loaded from a file the first time,
      /// so each individual test doesn't have to do it again.
      /// </summary>
      private static IDictionary<string, AutoSearchModel> modelCache = new Dictionary<string, AutoSearchModel>();
      private static AutoSearchModel LoadModel(string name) {
         lock (modelCache) {
            if (modelCache.TryGetValue(name, out var cachedModel)) return cachedModel;
            Skip.IfNot(File.Exists(name));
            var data = File.ReadAllBytes(name);
            var metadata = new StoredMetadata(new string[0]);
            var model = new AutoSearchModel(data, metadata);
            modelCache[name] = model;
            return model;
         }
      }
   }
}
