using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.ViewModels;
using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xunit;
namespace HavenSoft.HexManiac.Tests {
   /// <summary>Jumping between Pokemon must not build many controls: every new view model is a new control in the window, and that is what made the editor slow.</summary>
   public class PokemonEditorChurnTests : BaseViewModelTestClass {
      [SkippableFact]
      public void RealCube_JumpingBetweenPokemon_BuildsFewNewViewModels() {
         var path = Environment.GetEnvironmentVariable("HMA_INTRO_CUBE");
         Skip.If(string.IsNullOrEmpty(path), "x");
         var model = new HardcodeTablesModel(Singletons, File.ReadAllBytes(path), new StoredMetadata(File.ReadAllLines(Environment.GetEnvironmentVariable("HMA_INTRO_CUBE_TOML"))));
         model.InitializationWorkload.Wait();
         var vp = new ViewPort("x.gba", model, InstantDispatch.Instance, Singletons);
         vp.Goto.Execute("data.pokemon.stats/1");
         var tool = vp.Tools.TableTool;
         var sw = Stopwatch.StartNew();
         var previous = tool.Groups.SelectMany(g => g.Members).ToList();
         foreach (var i in new[] { 2, 3, 25, 26, 133, 134, 135, 136, 150, 151, 1, 2 }) {
            sw.Restart();
            vp.Goto.Execute($"data.pokemon.stats/{i}");
            var t1 = sw.ElapsedMilliseconds;
            var members = tool.Groups.SelectMany(g => g.Members).ToList();
            var fresh = members.Where(m => !previous.Contains(m)).ToList();
            Console.WriteLine($"{i}: {t1} ms, {members.Count} members, {fresh.Count} new view models: {string.Join(",", fresh.GroupBy(m => m.GetType().Name).Select(x => x.Key.Replace("ArrayElementViewModel", "").Replace("ElementViewModel", "") + "x" + x.Count()))}");
            if (i == 2 || i == 135 || i == 150) Assert.Equal(0, fresh.Count);
            Assert.True(fresh.Count <= 25, $"{i}: {fresh.Count} new view models");
            Assert.True(t1 < 500, $"{i}: {t1} ms");
            if (i == 133) Console.WriteLine("   new: " + string.Join(", ", fresh.Select(m => m.GetType().Name.Replace("ArrayElementViewModel", "").Replace("ElementViewModel", "") + ":" + ((m as FieldArrayElementViewModel)?.Name ?? (m as ComboBoxArrayElementViewModel)?.Name ?? (m as ButtonArrayElementViewModel)?.Text ?? ""))));
            previous = members;
         }
      }
   }
}
