using HavenSoft.HexManiac.Core;
using HavenSoft.HexManiac.Core.Models;
using HavenSoft.HexManiac.Core.Models.Runs;
using System;
using System.IO;
using System.Text;
using Xunit;

namespace HavenSoft.HexManiac.Tests {
   public class ItemHoldProbe {
      [SkippableFact]
      public void Probe() {
         var rom = Environment.GetEnvironmentVariable("UPD_NEW"); var toml = Environment.GetEnvironmentVariable("UPD_NEW_TOML"); var outp = Environment.GetEnvironmentVariable("UPD_PROBE_OUT");
         Skip.If(string.IsNullOrEmpty(rom) || string.IsNullOrEmpty(outp), "env");
         var model = CubeScriptsTests.Load(File.ReadAllBytes(rom), File.ReadAllLines(toml));
         var items = model.GetTableModel("data.items.stats");
         var sb = new StringBuilder();
         for (int i = 0; i < items.Count; i++) {
            var raw = items[i].GetValue("holdeffect"); var name = items[i].GetStringValue("name");
            sb.AppendLine($"{i}\t{raw}\t{name}\t{items[i].GetValue("param")}");
         }
         File.WriteAllText(outp, sb.ToString());
      }
   }
}
