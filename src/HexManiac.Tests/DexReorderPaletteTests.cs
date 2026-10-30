using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using HavenSoft.HexManiac.Core.ViewModels;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class DexReorderPaletteTests {
      /// <summary>The icons of the Adjust Dex Order tab used the GBA colour order for a display that wants the other one: red and blue were swapped.</summary>
      [SkippableFact]
      public void ReorderDexIcons_ShowTheColorsOfThePalette() {
         var rom = Environment.GetEnvironmentVariable("UPD_NEW"); var toml = Environment.GetEnvironmentVariable("UPD_NEW_TOML");
         Skip.If(string.IsNullOrEmpty(rom), "set UPD_NEW and UPD_NEW_TOML to CUBE.gba and CUBE.toml");
         var model = CubeScriptsTests.Load(File.ReadAllBytes(rom), File.ReadAllLines(toml));
         var stats = model.GetTableModel("data.pokemon.stats");
         foreach (var species in new[] { 1, 4, 7, 25, 150 }) {
            var palette = stats[species].GetAddress("palette");
            var raw = Enumerable.Range(0, 16).Select(i => (ushort)(model.RawData[palette + i * 2] | (model.RawData[palette + i * 2 + 1] << 8))).ToArray();
            var icon = new SortablePokemon(model, species);
            var shown = icon.PixelData.Where(color => color != GbaColor.ToDisplay(raw[0])).Distinct().ToList();
            Assert.NotEmpty(shown);
            Assert.All(shown, color => Assert.Contains(color, raw.Select(GbaColor.ToDisplay)));
         }
      }
   }
}
