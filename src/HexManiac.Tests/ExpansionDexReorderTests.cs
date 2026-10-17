using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.QuickEditItems;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   /// <summary>
   /// Utilities > Reorder National / Regional Dex on the pokeemerald-expansion (CUBE), where the order is the species table's 'dexNumber'
   /// plus the 4-byte hoenn->national table, instead of the vanilla data.pokedex.national / regional / stats tables.
   /// Runs against the real CUBE ROM when HMA_INTRO_CUBE and HMA_INTRO_CUBE_TOML point at it, and vanilla Emerald when HMA_INTRO_EMERALD does. Skipped otherwise.
   /// </summary>
   public class ExpansionDexReorderTests {
      private static string[] CubeToml() {
         var path = Environment.GetEnvironmentVariable("HMA_INTRO_CUBE");
         var toml = Environment.GetEnvironmentVariable("HMA_INTRO_CUBE_TOML");
         Skip.If(string.IsNullOrEmpty(path) || !File.Exists(path) || string.IsNullOrEmpty(toml) || !File.Exists(toml), "set HMA_INTRO_CUBE and HMA_INTRO_CUBE_TOML to run this test");
         return File.ReadAllLines(toml);
      }

      private static IDataModel LoadCube(Func<string[], string[]> editToml = null) {
         var lines = CubeToml();
         if (editToml != null) lines = editToml(lines);
         var model = new HardcodeTablesModel(BaseViewModelTestClass.Singletons, File.ReadAllBytes(Environment.GetEnvironmentVariable("HMA_INTRO_CUBE")), new StoredMetadata(lines));
         model.InitializationWorkload.Wait();
         return model;
      }

      private static ViewPort Open(IDataModel model) => new ViewPort("cube.gba", model, InstantDispatch.Instance, BaseViewModelTestClass.Singletons);

      private static DexReorderTab Tab(ViewPort viewPort, bool national) {
         var name = national ? HardcodeTablesModel.NationalDexTableName : HardcodeTablesModel.RegionalDexTableName;
         var tab = new DexReorderTab(new StubFileSystem(), viewPort, name, HardcodeTablesModel.DexInfoTableName, national);
         tab.Refresh();
         return tab;
      }

      /// <summary>Drag the element at 'from' to 'to', like the view does.</summary>
      private static void Move(DexReorderTab tab, int from, int to) {
         tab.SelectionStart = from;
         tab.HandleMove(from, to);
         tab.CompleteCurrentInteraction();
      }

      private static IReadOnlyList<int> SearchValues(IDataModel model, string name) {
         var table = model.GetTable(name);
         return Enumerable.Range(0, table.ElementCount).Select(i => model.ReadMultiByteValue(table.Start + i * 2, 2)).ToList();
      }

      /// <summary>Each national number 1..count is used by at least one species, and all the forms that shared a number still share one.</summary>
      private static void AssertNationalConsistent(int[] before, int[] after, int count) {
         Assert.Equal(before.Length, after.Length);
         Assert.Equal(0, after[0]);
         var oldToNew = new Dictionary<int, int>();
         for (int species = 1; species < before.Length; species++) {
            if (before[species] == 0) { Assert.Equal(0, after[species]); continue; }
            if (oldToNew.TryGetValue(before[species], out var expected)) Assert.Equal(expected, after[species]);
            else oldToNew[before[species]] = after[species];
         }
         Assert.Equal(oldToNew.Count, oldToNew.Values.Distinct().Count()); // a renumbering, not a merge
         Assert.Equal(Enumerable.Range(1, count), after.Where(n => n != 0).Distinct().OrderBy(n => n));
      }

      [SkippableFact]
      public void Cube_LayoutIsRecognized() {
         var model = LoadCube();

         Assert.True(ExpansionDex.IsExpansionLayout(model));
         Assert.Null(ExpansionDex.WhyUnavailable(model));
         Assert.Equal(1025, ExpansionDex.NationalCount(model));
         var regional = ExpansionDex.ReadRegional(model);
         Assert.Equal(252, regional[0]); // Treecko
         Assert.Equal(regional.Length, regional.Distinct().Count());
         Assert.All(regional, national => Assert.InRange(national, 1, 1025));
         var byNational = ExpansionDex.SpeciesByNational(model);
         Assert.Equal(1, ExpansionDex.SpeciesForNational(byNational, 1));
         Assert.True(byNational[3].Count > 1); // Venusaur and its forms (mega, gmax) share national #3
      }

      [SkippableFact]
      public void Cube_ReorderItemsAreEnabledAndOpen() {
         var model = LoadCube();
         var viewPort = Open(model);
         foreach (var (display, table) in new[] { ("National", HardcodeTablesModel.NationalDexTableName), ("Regional", HardcodeTablesModel.RegionalDexTableName) }) {
            var item = new ReorderDex(display, table);
            Assert.True(item.CanRun(viewPort));
            var opened = 0;
            viewPort.RequestTabChange += (sender, e) => { opened++; e.RequestAccepted = true; };
            var result = item.Run(viewPort).Result;
            Assert.False(result.HasError, result.ErrorMessage);
         }
      }

      [SkippableFact]
      public void Cube_NationalTab_ShowsEverySlotWithPicturesAndNames() {
         var model = LoadCube();
         var tab = Tab(Open(model), national: true);

         Assert.Equal(1025, tab.Elements.Count);
         Assert.Equal("bulbasaur", tab.Elements[0].Name.ToLower());
         Assert.Equal(1, tab.Elements[0].CanonicalIndex);
         Assert.Contains(tab.Elements[0].PixelData, pixel => pixel != tab.Elements[0].PixelData[0]); // a real picture, not a blank square
         Assert.Equal(64, tab.Elements[0].PixelWidth);
         Assert.Equal(64, tab.Elements[0].PixelHeight);
         Assert.NotEmpty(tab.Elements[2].ExtraIndices); // Venusaur's forms come along with it
         // every species with a number is in exactly one element
         var all = tab.Elements.SelectMany(e => new[] { e.CanonicalIndex }.Concat(e.ExtraIndices)).Where(i => i != 0).ToList();
         Assert.Equal(all.Count, all.Distinct().Count());
         Assert.Equal(ExpansionDex.ReadNationalNumbers(model).Count(n => n != 0), all.Count);

         tab.Filter = "pikachu";
         Assert.Contains(tab.Elements, e => !e.IsFilteredOut && e.Name.ToLower() == "pikachu");
         Assert.True(tab.Elements.Count(e => !e.IsFilteredOut) < 10);
      }

      [SkippableFact]
      public void Cube_NationalReorder_RenumbersSpeciesAndKeepsRegionalAndSearchOrders() {
         var model = LoadCube();
         var viewPort = Open(model);
         var original = model.RawData.ToArray();
         var before = ExpansionDex.ReadNationalNumbers(model);
         var byNationalBefore = ExpansionDex.SpeciesByNational(model);
         var regionalBefore = ExpansionDex.ReadRegional(model);
         var alphaBefore = SearchValues(model, "data.pokedex.search.alpha");
         var tab = Tab(viewPort, national: true);

         Move(tab, 0, 3);       // Bulbasaur after Charmander
         Move(tab, 251, 499);   // Treecko (a hoenn mon) far down
         Move(tab, 1024, 0);    // Pecharunt first

         var after = ExpansionDex.ReadNationalNumbers(model);
         AssertNationalConsistent(before, after, 1025);
         var pecharunt = ExpansionDex.SpeciesForNational(byNationalBefore, 1025);
         Assert.Equal(1, after[pecharunt]);
         Assert.Equal(5, after[1]);      // Bulbasaur: slot 4, then shifted by Pecharunt
         Assert.Equal(2, after[2]);      // Ivysaur
         Assert.Equal(3, after[3]);      // Venusaur...
         Assert.All(byNationalBefore[3], form => Assert.Equal(3, after[form])); // ...with all its forms
         Assert.Equal(4, after[4]);      // Charmander
         Assert.Equal(501, after[252]);  // Treecko

         // the tab shows the new order, numbered by slot
         Assert.Equal(Enumerable.Range(1, 1025), tab.Elements.Select(e => e.DexNumber));
         Assert.Equal(pecharunt, tab.Elements[0].CanonicalIndex);

         // the regional dex shows the same species in the same order
         var byNationalAfter = ExpansionDex.SpeciesByNational(model);
         var regionalAfter = ExpansionDex.ReadRegional(model);
         Assert.Equal(
            regionalBefore.Select(n => ExpansionDex.SpeciesForNational(byNationalBefore, n)),
            regionalAfter.Select(n => ExpansionDex.SpeciesForNational(byNationalAfter, n)));
         Assert.Equal(501, regionalAfter[0]);

         // the alphabetical search order still lists the same species
         var alphaAfter = SearchValues(model, "data.pokedex.search.alpha");
         Assert.Equal(
            alphaBefore.Select(n => ExpansionDex.SpeciesForNational(byNationalBefore, n)),
            alphaAfter.Select(n => ExpansionDex.SpeciesForNational(byNationalAfter, n)));

         // only the numbers changed: nothing else in the ROM
         var changed = Enumerable.Range(0, original.Length).Where(i => original[i] != model[i]).ToList();
         var stats = model.GetTable(HardcodeTablesModel.PokemonStatsTable);
         var hoenn = model.GetTable(HardcodeTablesModel.ConversionDexTableName);
         var allowed = new List<(int, int)> { (stats.Start, stats.Start + stats.Length), (hoenn.Start, hoenn.Start + hoenn.Length) };
         foreach (var name in ExpansionDex.SearchTables) { var t = model.GetTable(name); allowed.Add((t.Start, t.Start + t.Length)); }
         Assert.All(changed, address => Assert.Contains(allowed, range => range.Item1 <= address && address < range.Item2));

         // undo puts everything back
         while (tab.Undo.CanExecute(null)) tab.Undo.Execute(null);
         Assert.Equal(original, model.RawData.Take(original.Length).ToArray());
         Assert.Equal(1, tab.Elements[0].CanonicalIndex);
      }

      [SkippableFact]
      public void Cube_RegionalReorder_OnlyChangesTheHoennTable() {
         var model = LoadCube();
         var viewPort = Open(model);
         var original = model.RawData.ToArray();
         var regionalBefore = ExpansionDex.ReadRegional(model);
         var tab = Tab(viewPort, national: false);
         Assert.Equal(regionalBefore.Length, tab.Elements.Count);
         Assert.Equal("treecko", tab.Elements[0].Name.ToLower());

         Move(tab, 0, 5);
         Move(tab, regionalBefore.Length - 1, 1);

         var regionalAfter = ExpansionDex.ReadRegional(model);
         Assert.Equal(regionalBefore.OrderBy(n => n), regionalAfter.OrderBy(n => n)); // a permutation
         Assert.Equal(regionalBefore[1], regionalAfter[0]);
         Assert.Equal(regionalBefore[regionalBefore.Length - 1], regionalAfter[1]);
         Assert.Equal(252, regionalAfter[6]);
         Assert.Equal(tab.Elements.Select(e => e.DexNumber), regionalAfter);

         var hoenn = model.GetTable(HardcodeTablesModel.ConversionDexTableName);
         var changed = Enumerable.Range(0, original.Length).Where(i => original[i] != model[i]).ToList();
         Assert.NotEmpty(changed);
         Assert.All(changed, address => Assert.InRange(address, hoenn.Start, hoenn.Start + hoenn.Length - 1));
      }

      [SkippableFact]
      public void Cube_FixedTomlFormat_ReadsTheSameRegionalDex() {
         var oldModel = LoadCube();
         var expected = ExpansionDex.ReadRegional(oldModel);
         var count = expected.Length;
         var model = LoadCube(lines => lines.Select(line => line.Contains("'''[national:]") ? $"Format = '''[national::]{count}'''" : line).ToArray());

         Assert.Equal(4, model.GetTable(HardcodeTablesModel.ConversionDexTableName).ElementLength);
         Assert.Equal(expected, ExpansionDex.ReadRegional(model));
         var tab = Tab(Open(model), national: false);
         Move(tab, 0, 2);
         Assert.Equal(new[] { expected[1], expected[2], expected[0] }, ExpansionDex.ReadRegional(model).Take(3));
      }

      [SkippableFact]
      public void Cube_TomlWithoutHoennTable_ExplainsInsteadOfCrashing() {
         var model = LoadCube(lines => {
            var result = lines.ToList();
            var index = result.FindIndex(line => line.Contains("data.pokedex.hoennToNational"));
            result.RemoveRange(index - 1, 4); // [[NamedAnchors]] Name Address Format
            return result.ToArray();
         });
         var viewPort = Open(model);
         var item = new ReorderDex("National", HardcodeTablesModel.NationalDexTableName);

         Assert.True(item.CanRun(viewPort));
         var result = item.Run(viewPort).Result;
         Assert.True(result.HasError);
         Assert.Contains("hoennToNational", result.ErrorMessage);
      }

      [SkippableFact]
      public void Emerald_VanillaReorderIsUnchanged() {
         var path = Environment.GetEnvironmentVariable("HMA_INTRO_EMERALD");
         Skip.If(string.IsNullOrEmpty(path) || !File.Exists(path), "set HMA_INTRO_EMERALD to run this test");
         var model = new HardcodeTablesModel(BaseViewModelTestClass.Singletons, File.ReadAllBytes(path));
         model.InitializationWorkload.Wait();
         var viewPort = Open(model);

         Assert.False(ExpansionDex.IsExpansionLayout(model));
         Assert.True(new ReorderDex("National", HardcodeTablesModel.NationalDexTableName).CanRun(viewPort));
         Assert.True(new ReorderDex("Regional", HardcodeTablesModel.RegionalDexTableName).CanRun(viewPort));

         var national = model.GetTable(HardcodeTablesModel.NationalDexTableName);
         var tab = Tab(viewPort, national: true);
         Assert.Equal(386, tab.Elements.Count);
         Assert.Equal(1, tab.Elements[0].CanonicalIndex);
         Move(tab, 0, 3);
         Assert.Equal(4, model.ReadMultiByteValue(national.Start, 2)); // Bulbasaur (species 1) is national #4
         Assert.Equal(1, model.ReadMultiByteValue(national.Start + 2, 2)); // Ivysaur is #1
      }
   }
}
