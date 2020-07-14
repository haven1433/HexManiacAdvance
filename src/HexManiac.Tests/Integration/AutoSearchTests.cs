
using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.DataFormats;
using HavenSoft.HexManiac.Core.ViewModels.QuickEditItems;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

using static HavenSoft.HexManiac.Core.Models.HardcodeTablesModel;

namespace HavenSoft.HexManiac.Tests {
   internal static class TestExtensions {
      public static ITableRun GetTable(this IDataModel model, string name) {
         var address = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, name);
         return model.GetNextRun(address) as ITableRun;
      }
   }

   /// <summary>
   /// The Auto-Search tests exist to verify that data can be found in the Vanilla roms.
   /// These tests are skippable, so that they'll work even if you don't have the ROMs on your system.
   /// This is important, since the ROMs aren't part of the repository.
   /// </summary>
   public class AutoSearchTests : IClassFixture<AutoSearchFixture> {
      public static IEnumerable<object[]> PokemonGames { get; } = new[] {
         "Ruby",
         "Sapphire",
         "FireRed",
         "LeafGreen",
         "Emerald",
         "FireRed v1.1",
         "LeafGreen v1.1",
         "Ruby v1.1",
         "Sapphire v1.1",
         "DarkRisingKAIZO", // from FireRed
         "Vega 2019-04-20", // from FireRed
         "Clover",          // from FireRed
         "Gaia v3.2",       // from FireRed
         "Altair",          // from Emerald
      }.Select(game => new object[] { "sampleFiles/Pokemon " + game + ".gba" });

      private readonly AutoSearchFixture fixture;
      private readonly NoDataChangeDeltaModel noChange = new NoDataChangeDeltaModel();

      public AutoSearchTests(AutoSearchFixture fixture) => this.fixture = fixture;

      [SkippableTheory]
      [MemberData(nameof(PokemonGames))]
      public void PokemonNamesAreFound(string game) {
         var model = fixture.LoadModel(game);

         var run = model.GetTable(PokemonNameTable);
         if (game.Contains("Gaia")) Assert.Equal(823, run.ElementCount);
         else Assert.Equal(412, run.ElementCount);
      }

      [SkippableTheory]
      [MemberData(nameof(PokemonGames))]
      public void MovesNamesAreFound(string game) {
         var model = fixture.LoadModel(game);

         var run = model.GetTable(MoveNamesTable);
         if (game.Contains("Vega")) Assert.Equal(512, run.ElementCount);
         else if (game.Contains("Clover")) Assert.Equal(512, run.ElementCount);
         else if (game.Contains("Gaia")) Assert.Equal(512, run.ElementCount);
         else Assert.Equal(355, run.ElementCount);
      }

      [SkippableTheory]
      [MemberData(nameof(PokemonGames))]
      public void NaturesAreFound(string game) {
         var model = fixture.LoadModel(game);
         var run = model.GetTable(NaturesTableName);
         Assert.Equal(25, run.ElementCount);
      }

      [SkippableTheory]
      [MemberData(nameof(PokemonGames))]
      public void MoveDescriptionsAreFound(string game) {
         var model = fixture.LoadModel(game);

         var moveNamesRun = model.GetTable(MoveNamesTable);
         var moveDescriptionsRun = model.GetTable(MoveDescriptionsName);

         Assert.Equal(moveNamesRun.ElementCount - 1, moveDescriptionsRun.ElementCount);
      }

      [SkippableTheory]
      [MemberData(nameof(PokemonGames))]
      public void AbilitiyNamesAreFound(string game) {
         var model = fixture.LoadModel(game);

         var run = model.GetTable(AbilityNamesTable);
         if (game.Contains("Clover")) Assert.Equal(156, run.ElementCount);
         else if (game.Contains("Gaia")) Assert.Equal(188, run.ElementCount);
         else Assert.Equal(78, run.ElementCount);
      }

      [SkippableTheory]
      [MemberData(nameof(PokemonGames))]
      public void AbilitiyDescriptionsAreFound(string game) {
         var model = fixture.LoadModel(game);

         var run = model.GetTable(AbilityDescriptionsTable);
         if (game.Contains("Clover")) Assert.Equal(156, run.ElementCount);
         else if (game.Contains("Gaia")) Assert.Equal(188, run.ElementCount);
         else Assert.Equal(78, run.ElementCount);

         if (game.Contains("Gaia")) return; // don't validate description text in Gaia, it's actually invalid.

         for (var i = 0; i < run.ElementCount; i++) {
            var address = model.ReadPointer(run.Start + i * 4);
            var childRun = model.GetNextRun(address);
            Assert.IsType<PCSRun>(childRun);
         }
      }

      [SkippableTheory]
      [MemberData(nameof(PokemonGames))]
      public void TypesAreFound(string game) {
         var model = fixture.LoadModel(game);

         var run = model.GetTable(TypesTableName);
         if (game.Contains("Clover")) Assert.Equal(24, run.ElementCount);
         else if (game.Contains("Gaia")) Assert.Equal(25, run.ElementCount);
         else Assert.Equal(18, run.ElementCount);
      }

      [SkippableTheory]
      [MemberData(nameof(PokemonGames))]
      public void ItemsAreFound(string game) {
         var model = fixture.LoadModel(game);

         var run = model.GetTable(ItemsTableName);
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
      public void ItemImagesAreFound(string game) {
         var model = fixture.LoadModel(game);

         var address = model.GetAddressFromAnchor(noChange, -1, "itemimages");
         if (game.Contains("Ruby") || game.Contains("Sapphire")) {
            Assert.Equal(Pointer.NULL, address);
            return;
         }

         var imagesTable = model.GetTable("itemimages");
         var itemsTable = model.GetTable(ItemsTableName);

         Assert.Equal(itemsTable.ElementCount + 1, imagesTable.ElementCount);
      }

      [SkippableTheory]
      [MemberData(nameof(PokemonGames))]
      public void DecorationsAreFound(string game) {
         var model = fixture.LoadModel(game);

         var run = model.GetTable(DecorationsTableName);
         Assert.Equal(121, run.ElementCount);
      }

      [SkippableTheory]
      [MemberData(nameof(PokemonGames))]
      public void TrainerClassNamesAreFound(string game) {
         var model = fixture.LoadModel(game);

         var run = model.GetTable(TrainerClassNamesTable);
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
         var model = fixture.LoadModel(game);

         var run = model.GetTable(PokemonStatsTable);

         var firstPokemonStats = model.Skip(run.Start + run.ElementLength).Take(6).ToArray();
         var compareSet = new[] { 45, 49, 49, 45, 65, 65 }; // Bulbasaur
         if (game.Contains("Vega")) compareSet = new[] { 42, 53, 40, 70, 63, 40 }; // Nimbleaf
         if (game.Contains("Clover")) compareSet = new[] { 56, 60, 55, 50, 47, 50 }; // Grasshole
         for (int i = 0; i < compareSet.Length; i++) Assert.Equal(compareSet[i], firstPokemonStats[i]);
      }

      [SkippableTheory]
      [MemberData(nameof(PokemonGames))]
      public void LvlUpMovesAreFound(string game) {
         var model = fixture.LoadModel(game);

         var run = model.GetTable(LevelMovesTableName);
         Assert.Equal(PLMRun.SharedFormatString, ((ArrayRunPointerSegment)run.ElementContent[0]).InnerFormat);
      }

      [SkippableTheory]
      [MemberData(nameof(PokemonGames))]
      public void TradeDataIsFound(string game) {
         var model = fixture.LoadModel(game);

         var run = model.GetTable("tradedata");
         Assert.NotNull(run);
      }

      [SkippableTheory]
      [MemberData(nameof(PokemonGames))]
      public void EvolutionsAreFound(string game) {
         var model = fixture.LoadModel(game);

         var run = model.GetTable(EvolutionTableName);
         Assert.IsType<ArrayRun>(run);
      }

      [SkippableTheory]
      [MemberData(nameof(PokemonGames))]
      public void PokedexDataIsFound(string game) {
         var model = fixture.LoadModel(game);

         var regionalRun = model.GetTable(RegionalDexTableName);
         var nationalRun = model.GetTable(NationalDexTableName);
         var conversionRun = model.GetTable(ConversionDexTableName);
         var infoRun = (ArrayRun)model.GetTable(DexInfoTableName);

         Assert.IsType<ArrayRun>(regionalRun);
         Assert.IsType<ArrayRun>(nationalRun);
         Assert.IsType<ArrayRun>(conversionRun);

         if (game.Contains("Clover") || game.Contains("Gaia")) return; // some hacks have busted dex data

         Assert.Equal(387, infoRun.ElementCount);
      }

      [SkippableTheory]
      [MemberData(nameof(PokemonGames))]
      public void PokedexSearchDataIsFound(string game) {
         var model = fixture.LoadModel(game);

         Assert.NotNull(model.GetTable("searchalpha"));
         Assert.NotNull(model.GetTable("searchweight"));
         Assert.NotNull(model.GetTable("searchsize"));

         if (model.GetGameCode().IsAny(Ruby, Ruby1_1, Sapphire, Sapphire1_1, Emerald)) return;
         Assert.NotNull(model.GetTable("searchtype"));
      }

      [SkippableTheory]
      [MemberData(nameof(PokemonGames))]
      public void MoveDataFound(string game) {
         var model = fixture.LoadModel(game);

         var run = model.GetTable(MoveDataTable);

         var poundStats = model.Skip(run.Start + run.ElementLength).Take(8).ToArray();
         var compareSet = new[] { 0, 40, 0, 100, 35, 0, 0, 0 };
         for (int i = 0; i < compareSet.Length; i++) Assert.Equal(compareSet[i], poundStats[i]);

         run = model.GetTable("graphics.moves.animations");
         Assert.Equal(ElementContentType.Pointer, run.ElementContent[0].Type);

         run = model.GetTable("scripts.moves.effects");
         Assert.Equal(ElementContentType.Pointer, run.ElementContent[0].Type);
      }

      [SkippableTheory]
      [MemberData(nameof(PokemonGames))]
      public void EggMoveDataFound(string game) {
         var model = fixture.LoadModel(game);

         var address = model.GetAddressFromAnchor(noChange, -1, HardcodeTablesModel.EggMovesTableName);
         var run = (EggMoveRun)model.GetNextAnchor(address);

         Assert.Equal(2, run.PointerSources.Count);
         var expectedLastElement = model.ReadMultiByteValue(run.PointerSources[1] - 4, 4);
         var expectedLength = expectedLastElement + 1;
         var actualLength = run.Length / 2 - 1;  // remove the closing element.
         Assert.InRange(actualLength, 790, expectedLength);
      }

      [SkippableTheory]
      [MemberData(nameof(PokemonGames))]
      public void TutorsAreFound(string game) {
         var model = fixture.LoadModel(game);

         var movesLocation = model.GetAddressFromAnchor(noChange, -1, MoveTutors);
         var compatibilityLocation = model.GetAddressFromAnchor(noChange, -1, TutorCompatibility);

         // ruby and sapphire have no tutors
         if (game.Contains("Ruby") || game.Contains("Sapphire")) {
            Assert.Equal(Pointer.NULL, movesLocation);
            Assert.Equal(Pointer.NULL, compatibilityLocation);
            return;
         }

         var moves = (ArrayRun)model.GetNextRun(movesLocation);
         var compatibility = (ArrayRun)model.GetNextRun(compatibilityLocation);

         var expectedMoves = game.Contains("Emerald") || game.Contains("Altair") ? 30 : 15;
         var compatibilityElementLength = (int)Math.Ceiling(expectedMoves / 8.0);

         Assert.Equal(expectedMoves, moves.ElementCount);
         Assert.Equal(compatibilityElementLength, compatibility.ElementContent[0].Length);
      }

      [SkippableTheory]
      [MemberData(nameof(PokemonGames))]
      public void WildPokemonAreFound(string game) {
         var model = fixture.LoadModel(game);
         var wild = model.GetTable(WildTableName);
         Assert.NotNull(wild);
      }

      [SkippableTheory]
      [MemberData(nameof(PokemonGames))]
      public void PokeballSpritesAreFound(string game) {
         var model = fixture.LoadModel(game);
         var sprites = model.GetTable(BallSpritesTable);
         var palettes = model.GetTable(BallPalettesTable);
         Assert.NotNull(sprites);
         Assert.NotNull(palettes);
      }

      [SkippableTheory]
      [MemberData(nameof(PokemonGames))]
      public void TrainerSpritesAreFound(string game) {
         var model = fixture.LoadModel(game);
         var sprites = model.GetTable("graphics.trainers.sprites.front");
         var palettes = model.GetTable("graphics.trainers.palettes");
         Assert.NotNull(sprites);
         Assert.NotNull(palettes);

         if (!game.Contains("Ruby") && !game.Contains("Sapphire")) {
            sprites = model.GetTable("graphics.trainers.sprites.back");
            palettes = model.GetTable("graphics.trainers.palettes.back");
            Assert.NotNull(sprites);
            Assert.NotNull(palettes);
         }
      }

      [SkippableTheory]
      [MemberData(nameof(PokemonGames))]
      public void PokeIconsFound(string game) {
         var model = fixture.LoadModel(game);
         Assert.NotNull(model.GetTable(FrontSpritesTable));
         Assert.NotNull(model.GetTable(BackSpritesTable));
         Assert.NotNull(model.GetTable(PokePalettesTable));
         Assert.NotNull(model.GetTable(ShinyPalettesTable));
         Assert.NotNull(model.GetTable(PokeIconsTable));
         Assert.NotNull(model.GetTable(PokeIconPaletteIndexTable));
         Assert.NotNull(model.GetTable(PokeIconPalettesTable));
      }

      [SkippableFact]
      public void TutorsCompatibilityContainsCorrectDataFireRed() {
         var model = fixture.LoadModel(PokemonGames.Select(array => (string)array[0]).First(game => game.Contains("FireRed")));
         var address = model.GetAddressFromAnchor(new NoDataChangeDeltaModel(), -1, TutorCompatibility);
         Assert.Equal(0x409A, model.ReadMultiByteValue(address + 2, 2));
      }

      [SkippableTheory]
      [MemberData(nameof(PokemonGames))]
      public void TmsAreFound(string game) {
         var model = fixture.LoadModel(game);

         var tmMoves = model.GetTable(TmMoves);
         var hmMoves = model.GetTable(HmMoves);
         var compatibility = model.GetTable(TmCompatibility);

         var expectedTmMoves = 58;
         var expectedHmMoves = 8;
         var compatibilityElementLength = 8;

         Assert.Equal(expectedTmMoves, tmMoves.ElementCount);
         Assert.Equal(expectedHmMoves, hmMoves.ElementCount);
         Assert.Equal(compatibilityElementLength, compatibility.ElementContent[0].Length);
      }

      [SkippableTheory]
      [MemberData(nameof(PokemonGames))]
      public void MultichoiceAreFound(string game) {
         var model = fixture.LoadModel(game);

         var multichoice = model.GetTable(MultichoiceTableName);
         if (game.Contains("DarkRising")) return;            // dark rising has bugged pointers in the 2nd one, so we don't expect to find many multichoice.
         Assert.NotInRange(multichoice.ElementCount, 0, 30); // make sure we found at least a few
      }

      [SkippableTheory]
      [MemberData(nameof(PokemonGames))]
      public void TypeChartIsFound(string game) {
         var model = fixture.LoadModel(game);

         var typeChart = model.GetTable(TypeChartTableName);
         Assert.NotInRange(typeChart.ElementCount, 0, 100);
      }

      [SkippableTheory]
      [MemberData(nameof(PokemonGames))]
      public void SpecialsAreFound(string game) {
         var model = fixture.LoadModel(game);

         var specials = model.GetTable(SpecialsTable);
         Assert.NotInRange(specials.ElementCount, 0, 300);
      }

      [SkippableTheory]
      [MemberData(nameof(PokemonGames))]
      public void HabitatsAreFound(string game) {
         var model = fixture.LoadModel(game);

         var habitatNamesAddress = model.GetAddressFromAnchor(noChange, -1, "habitatnames");
         var habitatsAddress = model.GetAddressFromAnchor(noChange, -1, "habitats");

         // ruby / sapphire / emerald have no habitats
         if (model.GetGameCode().IsAny(Ruby, Ruby1_1, Sapphire, Sapphire1_1, Emerald)) {
            Assert.Equal(Pointer.NULL, habitatNamesAddress);
            Assert.Equal(Pointer.NULL, habitatsAddress);
            return;
         }

         var habitatNames = (ITableRun)model.GetNextRun(habitatNamesAddress);
         var habitats = (ITableRun)model.GetNextRun(habitatsAddress);
         Assert.InRange(habitats.ElementCount, 8, 12);
      }

      [SkippableTheory]
      [MemberData(nameof(PokemonGames))]
      public void PickupItemsAreFound(string game) {
         var model = fixture.LoadModel(game);

         var pickupitems = model.GetTable("pickupitems");
         Assert.IsType<ArrayRun>(pickupitems);

         if (model.GetGameCode() == Emerald) {
            var rarepickupitems = model.GetTable("pickupitemsrare");
            Assert.IsType<ArrayRun>(rarepickupitems);
         }
      }

      [SkippableTheory]
      [MemberData(nameof(PokemonGames))]
      public void BattleScriptSourceIsFound(string game) {
         var model = fixture.LoadModel(game);

         var pickupitems = model.GetTable("battlescriptsource");
         Assert.IsType<ArrayRun>(pickupitems);
      }

      public static IEnumerable<object[]> ListData => PokemonGames.Select(array => array[0]).SelectMany(game =>
         new[] {
            new[] { game, MoveEffectListName, 214 },
            new[] { game, MoveInfoListName, 6 },
            new[] { game, MoveTargetListName, 7 },
            new[] { game, DecorationsShapeListName, 10 },
            new[] { game, DecorationsPermissionListName, 5 },
            new[] { game, DecorationsCategoryListName, 8 },
            new[] { game, EvolutionMethodListName, 16 },
            new[] { game, "evbits", 12 },
            new[] { game, "trainerStructType", 4 },
         }
      );

      [SkippableTheory]
      [MemberData(nameof(ListData))]
      public void ListFound(string game, string listName, int listCount) {
         var model = fixture.LoadModel(game);
         using (ModelCacheScope.CreateScope(model)) {
            var options = ModelCacheScope.GetCache(model).GetOptions(listName);
            Assert.Equal(listCount, options.Count);
         }
      }

      // this one actually changes the data, so I can't use the same shared model as everone else.
      [SkippableTheory]
      [MemberData(nameof(PokemonGames))]
      public void ExpandableTutorsWorks(string game) {
         var fileSystem = new StubFileSystem();
         var model = fixture.LoadModelNoCache(game);
         var editor = new EditorViewModel(fileSystem, false);
         var viewPort = new ViewPort(game, model);
         editor.Add(viewPort);
         var expandTutors = editor.QuickEdits.Single(edit => edit.Name == new MakeTutorsExpandable().Name);

         // ruby/sapphire do not support this quick-edit
         var canRun = expandTutors.CanRun(viewPort);
         if (game.Contains("Ruby") || game.Contains("Sapphire")) {
            Assert.False(canRun);
            return;
         } else {
            Assert.True(canRun);
         }

         // run the actual quick-edit
         expandTutors.Run(viewPort);

         // extend the table
         var table = (ArrayRun)model.GetNextRun(model.GetAddressFromAnchor(new ModelDelta(), -1, MoveTutors));
         viewPort.Goto.Execute((table.Start + table.Length).ToString("X6"));
         viewPort.Edit("+");

         // the 4 bytes after the last pointer to tutor-compatibility should store the length of tutormoves
         table = (ArrayRun)model.GetNextRun(model.GetAddressFromAnchor(new ModelDelta(), -1, MoveTutors));
         var tutorCompatibilityPointerSources = model.GetNextRun(model.GetAddressFromAnchor(new ModelDelta(), -1, TutorCompatibility)).PointerSources;
         var word = (WordRun)model.GetNextRun(tutorCompatibilityPointerSources.First() + 4);
         Assert.Equal(table.ElementCount, model.ReadValue(word.Start));
      }

      [SkippableTheory]
      [MemberData(nameof(PokemonGames))]
      public void ExpandableMovesWorks(string game) {
         var fileSystem = new StubFileSystem();
         var model = fixture.LoadModelNoCache(game);
         var editor = new EditorViewModel(fileSystem, false);
         var viewPort = new ViewPort(game, model, fixture.Singletons);
         editor.Add(viewPort);
         var expandMoves = editor.QuickEdits.Single(edit => edit.Name == new MakeMovesExpandable().Name);
         var originalPointerCount = model.GetTable(MoveDataTable).PointerSources.Count;

         Assert.True(expandMoves.CanRun(viewPort));

         // run the actual quick-edit
         var error = expandMoves.Run(viewPort);
         Assert.Equal(ErrorInfo.NoError, error);

         // verify we can't run it again
         Assert.False(expandMoves.CanRun(viewPort));

         // verify that new pointers were added to movedata
         var newPointerCount = model.GetTable(MoveDataTable).PointerSources.Count;
         Assert.Equal(5, newPointerCount - originalPointerCount);
      }

      [SkippableTheory]
      [MemberData(nameof(PokemonGames))]
      public void TrainersAreFound(string game) {
         var model = fixture.LoadModel(game);

         Assert.True(model.TryGetNameArray(TrainerTableName, out var trainers));
         if (game.Contains("Emerald")) Assert.Equal(855, trainers.ElementCount);
         else if (game.Contains("Altair")) Assert.Equal(1, trainers.ElementCount); // actually has 855, but the first element is glitched in a way that I shouldn't auto-recover.
         else if (game.Contains("FireRed")) Assert.Equal(743, trainers.ElementCount);
         else if (game.Contains("LeafGreen")) Assert.Equal(743, trainers.ElementCount);
         else if (game.Contains("Clover")) Assert.Equal(743, trainers.ElementCount);
         else if (game.Contains("Vega")) Assert.Equal(743, trainers.ElementCount);
         else if (game.Contains("DarkRisingKAIZO")) Assert.Equal(742, trainers.ElementCount); // the last one is glitched
         else if (game.Contains("Gaia")) Assert.Equal(743, trainers.ElementCount);
         else if (game.Contains("Ruby")) Assert.Equal(694, trainers.ElementCount);
         else if (game.Contains("Sapphire")) Assert.Equal(694, trainers.ElementCount);
         else throw new NotImplementedException();
      }

      [SkippableTheory]
      [MemberData(nameof(PokemonGames))]
      public void TrainerPayoutsAreFound(string game) {
         var model = fixture.LoadModel(game);
         var address = model.GetAddressFromAnchor(noChange, -1, "trainermoney");
         var run = model.GetNextRun(address) as ITableRun;
         var trainerClassesTable = model.GetTable(TrainerClassNamesTable);

         if (game.Contains("DarkRisingKAIZO")) Assert.Null(run);
         else if (game.Contains("Gaia")) Assert.Null(run);
         else Assert.InRange(run.ElementCount, 0, trainerClassesTable.ElementCount);
      }

      // this one actually changes the data, so I can't use the same shared model as everone else.
      // [SkippableTheory] // test removed until feature is complete.
      // [MemberData(nameof(PokemonGames))]
      private void ExpandableTMsWorks(string game) {
         var fileSystem = new StubFileSystem();
         var model = fixture.LoadModelNoCache(game);
         var editor = new EditorViewModel(fileSystem, false);
         var viewPort = new ViewPort(game, model);
         editor.Add(viewPort);
         var expandTMs = editor.QuickEdits.Single(edit => edit.Name == "Make TMs Expandable");

         // Clover makes changes that prevent us from finding tmmoves/tmcompatibility. Don't support Clover.
         var canRun = expandTMs.CanRun(viewPort);
         if (game.Contains("Clover")) {
            Assert.False(canRun);
            return;
         } else {
            Assert.True(canRun);
         }

         // run the actual quick-edit
         expandTMs.Run(viewPort);

         // extend the table
         var table = (ArrayRun)model.GetNextRun(model.GetAddressFromAnchor(new ModelDelta(), -1, TmMoves));
         viewPort.Goto.Execute((table.Start + table.Length).ToString("X6"));
         viewPort.Edit("+");

         // the 4 bytes after the last pointer to tm-compatibility should store the length of tmmoves
         table = (ArrayRun)model.GetNextRun(model.GetAddressFromAnchor(new ModelDelta(), -1, TmMoves));
         var tmCompatibilityPointerSources = model.GetNextRun(model.GetAddressFromAnchor(new ModelDelta(), -1, TmCompatibility)).PointerSources;
         var word = (WordRun)model.GetNextRun(tmCompatibilityPointerSources.First() + 4);
         Assert.Equal(table.ElementCount, model.ReadValue(word.Start));
      }

      // this one actually changes the data, so I can't use the same shared model as everone else.
      // [SkippableTheory] // test removed until feature is complete.
      // [MemberData(nameof(PokemonGames))]
      private void ExpandableItemsWorks(string game) {
         var fileSystem = new StubFileSystem();
         var model = fixture.LoadModelNoCache(game);
         var editor = new EditorViewModel(fileSystem, false);
         var viewPort = new ViewPort(game, model);
         editor.Add(viewPort);
         var expandItems = editor.QuickEdits.Single(edit => edit.Name == "Make Items Expandable");

         // run the actual quick-edit
         expandItems.Run(viewPort);

         // extend the table
         var table = (ArrayRun)model.GetNextRun(model.GetAddressFromAnchor(new ModelDelta(), -1, ItemsTableName));
         viewPort.Goto.Execute((table.Start + table.Length).ToString("X6"));
         viewPort.Edit("+");

         // 0x14 bytes after the start of the change should store the length of items
         var gameCode = model.GetGameCode();
         var editStart = MakeItemsExpandable.GetPrimaryEditAddress(gameCode);
         table = (ArrayRun)model.GetNextRun(model.GetAddressFromAnchor(new ModelDelta(), -1, ItemsTableName)); // note that since we changed the table, we have to get the run again.
         var word = (WordRun)model.GetNextRun(editStart + 0x14);
         Assert.Equal(table.ElementCount, model.ReadValue(word.Start));
      }
   }

   /// <summary>
   /// Loading the model can take a while.
   /// We want to know that loading the model created the correct arrays,
   /// But loading the same file into a model multiple times just wastes time.
   /// Go ahead and cache a model loaded from a file the first time,
   /// so each individual test doesn't have to do it again.
   ///
   /// This is done as a Fixture instead of a Lazy because all the tests in question
   /// are part of the same Test Collection (because they're in the same class)
   /// </summary>
   public class AutoSearchFixture {
      private readonly IDictionary<string, PokemonModel> modelCache = new Dictionary<string, PokemonModel>();

      public Singletons Singletons { get; } = new Singletons();

      public AutoSearchFixture() {
         Parallel.ForEach(AutoSearchTests.PokemonGames.Select(array => (string)array[0]), name => {
            if (!File.Exists(name)) return;
            var data = File.ReadAllBytes(name);
            var metadata = new StoredMetadata(new string[0]);
            var model = new HardcodeTablesModel(Singletons, data, metadata);
            lock (modelCache) modelCache[name] = model;
         });
      }

      public PokemonModel LoadModel(string name) {
         Skip.IfNot(modelCache.ContainsKey(name));
         return modelCache[name];
      }

      /// <summary>
      /// Make a copy of one of the existing models, but quickly, instead of doing a full load from file.
      /// </summary>
      /// <param name="name"></param>
      /// <returns></returns>
      public IDataModel LoadModelNoCache(string name) {
         Skip.IfNot(File.Exists(name));
         var template = modelCache[name];
         var metadata = template.ExportMetadata(Singletons.MetadataInfo);
         var model = new PokemonModel(template.RawData.ToArray(), metadata);
         return model;
      }
   }
}
